namespace CrownAndCompass.Sim.Systems;

/// <summary>A place on the mayor for one piece of regalia (regalia.json "slots").</summary>
public sealed record RegaliaSlotDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
}

/// <summary>
/// What a piece of regalia does while the mayor wears it: bonuses on the mayor's checks, more inspiration,
/// Mandate and council seats' loyalty each day, loyalty from heroes, and a weapon bonus on companies that
/// march under the banner. <see cref="Tier"/> is the charter a commission needs; null, it can't be commissioned.
/// </summary>
public sealed record RegaliaDef
{
    public string Ref { get; init; } = "";
    public string Slot { get; init; } = "";
    public int? Tier { get; init; }
    public Dictionary<string, int> Skills { get; init; } = [];
    public int Inspiration { get; init; }
    public double Mandate { get; init; }
    public double Council { get; init; }
    public double Loyalty { get; init; }
    public int March { get; init; }

    /// <summary>Share an assassin's chance at the mayor falls by, and that the guard's chance to stop a knife rises by.</summary>
    public double Ward { get; init; }

    /// <summary>How much faster a councillor serving a foe is found out; whether it wards the chamber against charms.</summary>
    public double Truth { get; init; }
    public bool Charm { get; init; }

    /// <summary>Hall pieces: share off the forge's gold; share off the chance sickness comes to town; a twin that lets the
    /// senior company on the road send word home.</summary>
    public double Craft { get; init; }
    public double Sick { get; init; }
    public bool Sending { get; init; }

    /// <summary>A hall piece's (slot "hall") part in the town's services, as an institution's at full strength.</summary>
    public ServiceDef? Service { get; init; }
}

/// <summary>Commissions from the town's craftsmen (regalia.json "commission").</summary>
public sealed record RegaliaCommissionRules
{
    /// <summary>Price, times the library's value.</summary>
    public double Markup { get; init; } = 0.4;

    /// <summary>Gold the treasury keeps after a commission.</summary>
    public double Reserve { get; init; } = 100;
}

/// <summary>The mayor's regalia (content/game/regalia.json).</summary>
public sealed record RegaliaRules
{
    public List<RegaliaSlotDef> Slots { get; init; } = [];
    public List<RegaliaDef> Items { get; init; } = [];

    /// <summary>What the mayor wears on charter day.</summary>
    public List<string> Start { get; init; } = [];
    public RegaliaCommissionRules Commission { get; init; } = new();

    public RegaliaDef? Item(string id) => Items.FirstOrDefault(i => i.Ref == id);
    public RegaliaSlotDef? Slot(string id) => Slots.FirstOrDefault(s => s.Id == id);
}

public sealed class RegaliaState
{
    /// <summary>What the mayor wears, one piece a slot, in the order put on.</summary>
    public List<StockItem> Worn { get; } = [];

    /// <summary>The banner's weapon bonus on companies out, taken off when they come home.</summary>
    public List<Blessing> Boons { get; } = [];
    public int Commissioned { get; set; }
}

/// <summary>
/// The mayor's regalia: a chain of office, a signet, a sceptre, a mantle and a banner, each a slot the mayor
/// fills from the town armory's stock. Pieces come from the charter (the chain), from commissions to the town's
/// craftsmen once the charter is high enough, and from hoards heroes haul home. A worn piece helps at its
/// call site: the mayor's d20 (<see cref="Stories.Bonus"/>), inspiration (<see cref="Sheet.InspirationMax"/>),
/// Mandate (<see cref="Council"/>'s drift), seats' loyalty, heroes' loyalty (<see cref="Loyalty.Factors"/>) and
/// companies marching under the banner. Taking one off puts it back in the armory.
/// </summary>
public static class Regalia
{
    private static RegaliaRules Rules(Town town) => town.Content.Regalia;

    public static RegaliaDef? Def(Town town, LibItem it) => Rules(town).Item(it.Id);

    /// <summary>A piece the mayor wears; hall pieces (slot "hall") are relics the town keeps instead.</summary>
    public static bool Is(Town town, LibItem it) => Def(town, it) is { Slot: not Hall };

    public const string Hall = "hall";

    /// <summary>The relics the hall keeps that do something, each once however many were hauled home.</summary>
    public static IEnumerable<RegaliaDef> InHall(Town town)
    {
        if (town.Relics.Count == 0) yield break;
        foreach (var id in town.Relics.Distinct().OrderBy(x => x, StringComparer.Ordinal))
            if (Rules(town).Item(id) is { Slot: Hall } d) yield return d;
    }

    /// <summary>A piece's own powers over the mayor's work, in short phrases ("+2 persuasion", "wards off assassins").</summary>
    public static List<string> Describe(Town town, RegaliaDef d)
    {
        var said = d.Skills.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"+{k.Value} {k.Key}").ToList();
        if (d.Inspiration > 0) said.Add($"+{d.Inspiration} inspiration");
        if (d.Mandate > 0) said.Add("a steadier Mandate");
        if (d.Council > 0) said.Add("a readier council");
        if (d.Loyalty > 0) said.Add("loyal heroes");
        if (d.March > 0) said.Add("a faster muster");
        if (d.Ward > 0) said.Add("a ward against assassins");
        if (d.Truth > 0) said.Add("turncoats found sooner");
        if (d.Charm) said.Add("a ward against charms");
        if (d.Craft > 0) said.Add($"the forge {Js.Round(d.Craft * 100)}% cheaper");
        if (d.Sick > 0) said.Add($"{Js.Round(d.Sick * 100)}% less sickness");
        if (d.Sending) said.Add("word from the senior company");
        return said;
    }

    public static IEnumerable<ServiceDef> HallServices(Town town) => InHall(town).Where(d => d.Service != null).Select(d => d.Service!);

    /// <summary>What the mayor wears and what the hall keeps, for the sums below.</summary>
    private static IEnumerable<RegaliaDef> Held(Town town) => Worn(town).Select(w => w.Def).Concat(InHall(town));

    /// <summary>The worn pieces and what each does, in slot order.</summary>
    public static IEnumerable<(StockItem Piece, RegaliaDef Def)> Worn(Town town) =>
        town.Regalia.Worn
            .Select(s => (s, Def(town, s.Item)))
            .Where(x => x.Item2 != null)
            .Select(x => (x.s, x.Item2!))
            .OrderBy(x => Rules(town).Slots.FindIndex(sl => sl.Id == x.Item2.Slot));

    public static StockItem? In(Town town, string slot) => town.Regalia.Worn.FirstOrDefault(s => Def(town, s.Item)?.Slot == slot);

    // What it does --------------------------------------------------------------------------------

    public static int Skill(Town town, string skill) => Held(town).Sum(d => d.Skills.GetValueOrDefault(skill));
    public static int Inspiration(Town town) => Held(town).Sum(d => d.Inspiration);
    public static double Mandate(Town town) => Js.Sum(Held(town), d => d.Mandate);
    public static double Council(Town town) => Js.Sum(Held(town), d => d.Council);
    public static int March(Town town) => Held(town).Sum(d => d.March);
    public static double Ward(Town town) => Math.Min(0.9, Js.Sum(Held(town), d => d.Ward));
    public static double Truth(Town town) => Js.Sum(Held(town), d => d.Truth);
    public static bool Charm(Town town) => Held(town).Any(d => d.Charm);
    public static double Craft(Town town) => Math.Min(0.5, Js.Sum(InHall(town), d => d.Craft));
    public static double Sick(Town town) => Math.Min(0.8, Js.Sum(InHall(town), d => d.Sick));

    /// <summary>The hall keeps a twin of something that carries words, and this is the senior company with the town's name.</summary>
    public static bool Sends(Town town, Party party) =>
        town.Relics.Count > 0 && InHall(town).Any(d => d.Sending) && town.Parties.Where(p => p.Members.Count > 0).Min(p => (int?)p.Id) == party.Id;

    /// <summary>Heroes' loyalty from the mayor's regalia, piece by piece, for <see cref="Loyalty.Factors"/>.</summary>
    public static IEnumerable<(string Why, double Points)> LoyaltyFactors(Town town) =>
        Worn(town).Where(w => w.Def.Loyalty != 0).Select(w => ($"the mayor's {w.Piece.Item.Name}", w.Def.Loyalty))
            .Concat(InHall(town).Where(d => d.Loyalty != 0).Select(d => ($"the {town.Content.Library.Item(d.Ref)?.Name ?? d.Ref} in the hall", d.Loyalty)));

    // Wearing -------------------------------------------------------------------------------------

    /// <summary>The charter's chain, put on at once.</summary>
    public static void Init(Town town)
    {
        foreach (var id in Rules(town).Start)
            if (town.Content.Library.Item(id) is LibItem it && Is(town, it))
                town.Regalia.Worn.Add(new StockItem { Id = town.Armory.NextId++, Item = it, Day = town.Day, From = "the charter" });
    }

    /// <summary>Put on a piece from the armory; what was in its slot goes back to the armory.</summary>
    public static bool Equip(Town town, int stockId)
    {
        var stock = town.Armory.Stock.FirstOrDefault(s => s.Id == stockId);
        if (stock == null || Def(town, stock.Item) is not { Slot: not Hall } d) return false;
        var old = In(town, d.Slot);
        town.Armory.Stock.Remove(stock);
        if (old != null)
        {
            town.Regalia.Worn.Remove(old);
            town.Armory.Stock.Add(old);
        }
        town.Regalia.Worn.Add(stock);
        Clamp(town);
        var m = town.Stories.Mayor;
        string slot = Rules(town).Slot(d.Slot)?.Name.ToLowerInvariant() ?? d.Slot;
        town.Log(LedgerCategory.Story, Importance.Routine, $"Mayor {m.Name} takes up the {stock.Item.Name} as {slot}{(old != null ? $", and lays the {old.Item.Name} by in the armory" : "")}.");
        return true;
    }

    /// <summary>Take off what the mayor wears in a slot, back into the armory.</summary>
    public static bool Unequip(Town town, string slot)
    {
        var old = In(town, slot);
        if (old == null) return false;
        town.Regalia.Worn.Remove(old);
        town.Armory.Stock.Add(old);
        Clamp(town);
        return true;
    }

    private static void Clamp(Town town)
    {
        var m = town.Stories.Mayor;
        m.Inspiration = Math.Min(m.Inspiration, Sheet.InspirationMax(town));
    }

    // Commissions ---------------------------------------------------------------------------------

    public static double Price(Town town, LibItem it) => Math.Ceiling(it.ValueGp * Rules(town).Commission.Markup);

    /// <summary>Why the piece can't be commissioned now, or null.</summary>
    public static string? Refuse(Town town, string id)
    {
        var d = Rules(town).Item(id);
        var it = town.Content.Library.Item(id);
        if (d == null || it == null) return "no such piece";
        if (d.Tier is not int tier) return "can't be made";
        if (town.City.Tier < tier) return $"needs a {town.Content.City.Tiers[Math.Min(tier, town.Content.City.Tiers.Count - 1)].Name.ToLowerInvariant()}";
        if (town.Regalia.Worn.Any(s => s.Item.Id == id) || town.Armory.Stock.Any(s => s.Item.Id == id)) return "already held";
        if (town.Treasury < Price(town, it)) return "not enough gold";
        return null;
    }

    /// <summary>Have the town's craftsmen make a piece: paid from the treasury, it goes to the armory to wear.</summary>
    public static StockItem? Commission(Town town, string id)
    {
        if (Refuse(town, id) != null) return null;
        var it = town.Content.Library.Item(id)!;
        double price = Price(town, it);
        town.Book(TreasuryAccount.Armory, -price);
        town.Armory.Spent += price;
        town.Regalia.Commissioned++;
        var s = Armory.Add(town, it, "made for the mayor");
        town.Log(LedgerCategory.Town, Importance.Routine, $"The town's craftsmen finish the {it.Name} for the mayor, for {Js.Str(price)} gold.");
        return s;
    }

    /// <summary>Commissions the mayor could make, cheapest first: the pieces not yet held, with why each can't be made now.</summary>
    public static List<(LibItem Item, RegaliaDef Def, string? Refused)> Commissions(Town town) =>
        Rules(town).Items
            .Where(d => d.Tier != null)
            .Select(d => (Item: town.Content.Library.Item(d.Ref), Def: d))
            .Where(x => x.Item != null && Refuse(town, x.Def.Ref) != "already held")
            .Select(x => (x.Item!, x.Def, Refuse(town, x.Def.Ref)))
            .OrderBy(x => x.Item1.ValueGp)
            .ThenBy(x => x.Item1.Id, StringComparer.Ordinal)
            .ToList();

    // The day -------------------------------------------------------------------------------------

    public static void Daily(Town town, bool auto)
    {
        Unboon(town);
        Boon(town);
        if (auto) AutoWear(town);
    }

    /// <summary>Companies that marched today carry the banner: a weapon bonus for the venture.</summary>
    private static void Boon(Town town)
    {
        int bonus = March(town);
        if (bonus <= 0) return;
        var banner = In(town, "banner");
        foreach (var party in town.Parties)
        {
            if (party.Returned || party.DepartTick < town.Tick - SimTime.HoursPerDay) continue;
            if (town.Regalia.Boons.Any(b => b.PartyId == party.Id)) continue;
            var carried = new List<Agent>();
            foreach (int id in party.Members)
            {
                var m = town.Agent(id);
                if (m.Dead || m.Hero == null) continue;
                m.Hero.WeaponBonus += bonus;
                town.Regalia.Boons.Add(new Blessing(m.Id, party.Id, bonus));
                carried.Add(m);
            }
            if (carried.Count > 0)
                town.Log(LedgerCategory.Quest, Importance.Routine, $"The {party.Name} march out under the mayor's {banner?.Item.Name ?? "banner"}.", carried.Select(m => m.Id).ToList());
        }
    }

    private static void Unboon(Town town)
    {
        var list = town.Regalia.Boons;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var b = list[i];
            if (!town.Parties[b.PartyId - 1].Returned) continue;
            if (town.Agent(b.AgentId).Hero is { } s) s.WeaponBonus -= b.Bonus;
            list.RemoveAt(i);
        }
    }

    /// <summary>The stand-in mayor wears the finest piece the armory holds for each empty or poorer slot.</summary>
    private static void AutoWear(Town town)
    {
        foreach (var s in town.Armory.Stock.Where(x => Is(town, x.Item)).OrderByDescending(x => x.Item.ValueGp).ThenBy(x => x.Id).ToList())
        {
            var d = Def(town, s.Item)!;
            if (In(town, d.Slot) is { } worn && worn.Item.ValueGp >= s.Item.ValueGp) continue;
            Equip(town, s.Id);
        }
    }
}
