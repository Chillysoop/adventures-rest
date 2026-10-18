namespace CrownAndCompass.Sim.Systems;

public sealed record FenceRules
{
    /// <summary>The secret society that runs the fence.</summary>
    public string Faction { get; init; } = "quiet-hand";

    /// <summary>Share of an item's value the fence pays, the faction's cut of the value, and the heat a sale draws.</summary>
    public double Share { get; init; } = 0.8;
    public double Cut { get; init; } = 0.25;
    public double Heat { get; init; } = 3;

    /// <summary>The fence pays past the honest cap for an item's tier, up to this multiple of it.</summary>
    public double Cap { get; init; } = 1.2;

    /// <summary>Tempers that sell to a fence rather than an honest shop.</summary>
    public List<string> Tempers { get; init; } = [];
}

/// <summary>Shops that buy heroes' loot and sell it on (content/game/merchants.json).</summary>
public sealed record MerchantRules
{
    public List<string> Shops { get; init; } = [];

    /// <summary>Share of an item's value a shop pays (within the gear rules' cap for its tier), and the markup it sells at.</summary>
    public double Share { get; init; } = 0.6;
    public double Resale { get; init; } = 1.1;

    /// <summary>Cash a shop keeps back, and how many second-hand pieces it has room for.</summary>
    public double Keep { get; init; } = 40;
    public int MaxWares { get; init; } = 4;

    /// <summary>Prices move by up to this share either way with how the shopkeeper feels about the hero.</summary>
    public double Standing { get; init; } = 0.2;
    public FenceRules Fence { get; init; } = new();
}

/// <summary>A second-hand magic item on a shop's shelf.</summary>
public sealed class Ware
{
    public int ShopId { get; set; }
    public string ItemId { get; set; } = "";
    public double Paid { get; set; }
    public int Day { get; set; }
}

/// <summary>
/// Merchants with a purse. A hero selling loot goes to a shop in town first: the shop pays from its own
/// cash, so a poor shop can't take a costly piece, and it puts what it buys on its shelf for another hero.
/// Prices follow standing: a shopkeeper pays a friend more and charges them less. Greedy and free-spirited
/// heroes take their loot to the thieves' fence instead, which pays better, feeds the guild and draws the watch.
/// </summary>
public static class Merchants
{
    private static MerchantRules Rules(Town town) => town.Content.Merchants;

    private static List<Building> Shops(Town town) =>
        town.Buildings.Where(b => Rules(town).Shops.Contains(b.Def.Id) && b.Complete && !b.Closed && !b.ForSale).OrderBy(b => b.Id).ToList();

    /// <summary>The price change a shopkeeper gives a hero, from -Standing to +Standing.</summary>
    public static double Standing(Town town, Building shop, Agent hero)
    {
        if (shop.OwnerId is not int owner) return 0;
        return Rules(town).Standing * town.Agent(owner).OpinionOf(hero.Id) / 100.0;
    }

    /// <summary>What a shop asks this hero for a piece on its shelf.</summary>
    public static double Asking(Town town, Building shop, Agent hero, LibItem it) =>
        Math.Ceiling(it.ValueGp * Rules(town).Resale * (1 - Standing(town, shop, hero)));

    public static IEnumerable<Ware> Wares(Town town, Building shop) => town.Wares.Where(w => w.ShopId == shop.Id);

    /// <summary>Sell a piece nobody needs: to the fence, to a shop with the cash, or to a passing merchant. Returns false for a passing merchant (the caller's old way).</summary>
    public static bool Sell(Town town, Agent a, LibItem it)
    {
        var r = Rules(town);
        double cap = Gear.SellCap(town, it);
        if (Fence(town, a) is { } f)
        {
            double paid = Math.Floor(Math.Min(it.ValueGp * r.Fence.Share, cap * r.Fence.Cap));
            a.Gold += paid;
            Factions.AddCoffers(town, f, Math.Floor(it.ValueGp * r.Fence.Cut));
            f.Heat = Math.Min(100, f.Heat + r.Fence.Heat);
            string den = f.DenId != null ? $" in the cellar of {town.Building(f.DenId.Value).Name}" : "";
            town.Log(LedgerCategory.Faction, Importance.Routine, $"{a.Name} sells the {it.Name} to a fence{den} for {Js.Str(paid)} gold, no questions asked.", [a.Id]);
            Factions.CheckExposure(town, f);
            return true;
        }
        Building? best = null;
        double offer = 0;
        foreach (var shop in Shops(town))
        {
            if (Wares(town, shop).Count() >= r.MaxWares) continue;
            double price = Math.Floor(Math.Min(it.ValueGp * r.Share, cap) * (1 + Standing(town, shop, a)));
            if (price > Purse(town, shop) - r.Keep) continue;
            if (price > offer) (best, offer) = (shop, price);
        }
        if (best == null) return false;
        Pay(town, best, offer);
        a.Gold += offer;
        town.Wares.Add(new Ware { ShopId = best.Id, ItemId = it.Id, Paid = offer, Day = town.Day });
        town.Log(LedgerCategory.Business, Importance.Routine, $"{a.Name} sells the {it.Name} to {best.Name} for {Js.Str(offer)} gold. It goes up on the shelf.", [a.Id]);
        return true;
    }

    /// <summary>What a shop can pay with: its till and its keeper's own savings.</summary>
    public static double Purse(Town town, Building shop) => Math.Max(0, shop.Cash) + (shop.OwnerId is int o ? Math.Max(0, town.Agent(o).Gold) : 0);

    private static void Pay(Town town, Building shop, double amount)
    {
        double till = Math.Min(Math.Max(0, shop.Cash), amount);
        shop.Cash -= till;
        if (amount > till && shop.OwnerId is int o) town.Agent(o).Gold -= amount - till;
    }

    /// <summary>The thieves' fence this hero would go to, or null.</summary>
    private static FactionState? Fence(Town town, Agent a)
    {
        var r = Rules(town).Fence;
        if (!r.Tempers.Contains(a.Hero!.Temper)) return null;
        var f = Factions.Find(town, r.Faction);
        return f is { Active: true } && f.Members.Count > 0 ? f : null;
    }

    /// <summary>Pieces on the shelves a hero could buy: the shop, the item and the asking price.</summary>
    public static IEnumerable<(Building Shop, Ware Ware, LibItem Item, double Price)> Offers(Town town, Agent hero)
    {
        foreach (var shop in Shops(town))
            foreach (var w in Wares(town, shop))
                if (town.Content.Library.Item(w.ItemId) is LibItem it)
                    yield return (shop, w, it, Asking(town, shop, hero, it));
    }

    /// <summary>A hero buys a piece off the shelf: the shop takes the money, the town its tax.</summary>
    public static void Bought(Town town, Building shop, Ware w, double price)
    {
        double tax = price * town.TaxRate;
        town.Book(TreasuryAccount.SalesTax, tax);
        shop.Cash += price - tax;
        shop.RevenueToday += price;
        town.Wares.Remove(w);
    }

    /// <summary>Shelves of a shop that closed or was sold are cleared.</summary>
    public static void Daily(Town town)
    {
        var open = Shops(town).Select(b => b.Id).ToHashSet();
        town.Wares.RemoveAll(w => !open.Contains(w.ShopId));
    }
}
