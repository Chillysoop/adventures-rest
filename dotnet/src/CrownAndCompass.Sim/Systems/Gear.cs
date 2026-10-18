using System.Text.RegularExpressions;

namespace CrownAndCompass.Sim.Systems;

/// <summary>Magic gear a forge can make once the town is big enough (and, for the best, has a chapter of the faction named).</summary>
public sealed record EnchantTier
{
    public IReadOnlyList<string> Items { get; init; } = [];
    public int Population { get; init; }
    public string Faction { get; init; } = "";
}

/// <summary>What heroes can buy, where, and what they get for what they don't need (content/game/gear.json).</summary>
public sealed record GearRules
{
    public string Forge { get; init; } = "forge";
    public string Store { get; init; } = "general_store";

    /// <summary>The store carries light armour and weapons, brought in by caravan at this multiple of the list price.</summary>
    public double StoreMarkup { get; init; } = 1.5;

    /// <summary>Share of a sale that leaves town for iron, leather and reagents.</summary>
    public double Wholesale { get; init; } = 0.4;

    /// <summary>Gold a hero keeps back for beds and potions; greedy heroes keep this many times more.</summary>
    public double Reserve { get; init; } = 30;
    public double GreedyReserve { get; init; } = 3;
    public IReadOnlyList<EnchantTier> Enchanted { get; init; } = [];

    /// <summary>Loot a hero has no use for goes to passing merchants for this share of its value, capped by tier.</summary>
    public double SellShare { get; init; } = 0.5;
    public double[] SellCap { get; init; } = [0, 25, 100, 400, 1000, 2500];

    /// <summary>Points of gear above a fresh kit a company counts as one level when it picks a site.</summary>
    public double EdgePerLevel { get; init; } = 3;
}

/// <summary>
/// Heroes' own gear, MMORPG Tycoon style. Every hero arrives with their class's kit from the library's
/// equipment lists (a longsword, chain mail and a shield; a longbow and leather); the gold they bring
/// home buys upgrades in town: better armour at the forge (light armour and weapons at the store,
/// dearer), and once the town is big enough the forge's +1 and +2 arms. Magic loot goes to whoever gains
/// most from it, and what nobody needs is sold. Gear is what they fight with: armour class, attack and
/// damage come from it, and a well-armed company dares sites a level above its own.
/// </summary>
public static class Gear
{
    public static readonly string[] Slots = ["weapon", "armor", "shield", "trinket"];

    private static GearRules Rules(Town town) => town.Content.Gear;

    private static readonly Regex Plus = new(@"\+(\d)", RegexOptions.CultureInvariant);
    private static readonly Regex Plain = new(@"^(Weapon|Armor|Shield) \(?\+(\d)\)?$", RegexOptions.CultureInvariant);

    /// <summary>The kit the class arrives with.</summary>
    public static void Equip(Library lib, HeroSheet s)
    {
        s.Weapon = lib.GearOf(s.Cls.Weapon);
        s.Armor = s.Cls.Wears.Count > 0 ? lib.GearOf(s.Cls.Wears[0]) : null;
        s.Shield = s.Cls.Shield && s.Weapon?.TwoHanded != true ? lib.GearOf("shield") : null;
    }

    // What gear does in a fight -------------------------------------------------------------------

    /// <summary>The slot a magic item really fills: brooches and rings of "shielding" are worn, not carried on an arm.</summary>
    public static string SlotOf(LibItem it) => it.Slot is "armor" or "shield" && it.Category != "armor" ? "trinket" : it.Slot;

    /// <summary>What a magic item adds in its slot: the +N in its name, or by tier.</summary>
    public static int BonusOf(Content c, LibItem it)
    {
        var m = Plus.Match(it.Name);
        if (m.Success) return int.Parse(m.Groups[1].Value);
        return SlotOf(it) switch
        {
            "weapon" => c.Loot.WeaponBonus[Math.Min(it.Tier, c.Loot.WeaponBonus.Length - 1)],
            "armor" or "shield" => it.Tier >= 4 ? 2 : 1,
            _ => Math.Max(1, it.Tier / 2),
        };
    }

    private static int Bonus(Content c, HeroSheet s, string slot) => s.Magic.TryGetValue(slot, out var it) ? BonusOf(c, it) : 0;

    private static int ArmorAc(LibGear? armor, int dex, Agent a)
    {
        var s = a.Hero!;
        if (armor == null) return 10 + dex + (s.Cls.Unarmored != "" ? Math.Max(0, Rng.Modifier(a.Abilities[s.Cls.Unarmored])) : 0);
        return armor.Ac + (armor.AddDex ? (armor.DexCap is int cap ? Math.Min(cap, dex) : dex) : 0);
    }

    public static int Ac(Content c, Agent a)
    {
        var s = a.Hero!;
        int ac = ArmorAc(s.Armor, Rng.Modifier(a.Abilities.Dex), a) + (s.Shield?.Ac ?? 0);
        return ac + Bonus(c, s, "armor") + Bonus(c, s, "shield") + s.ArmorBonus;
    }

    /// <summary>To hit: proficiency, the class's key ability, the weapon's magic, and a caster's focus.</summary>
    public static int Attack(Content c, Agent a)
    {
        var s = a.Hero!;
        return Expedition.Proficiency(s.Level) + Rng.Modifier(a.Abilities[s.Cls.Primary]) + s.WeaponBonus + Bonus(c, s, "weapon") + (s.Cls.Spell ? Bonus(c, s, "trinket") : 0);
    }

    /// <summary>The weapon's dice (a caster's attack spell instead), doubled on a critical hit.</summary>
    public static int Damage(Content c, Rng rng, Agent a, bool crit)
    {
        var s = a.Hero!;
        var dice = s.Cls.Spell || s.Weapon?.Damage == null ? new Dice(1, s.Cls.DamageDie > 0 ? s.Cls.DamageDie : 6, 0) : s.Weapon.Damage.Value;
        return dice.Roll(rng, crit) + Rng.Modifier(a.Abilities[s.Cls.Primary]) + s.WeaponBonus + Bonus(c, s, "weapon");
    }

    /// <summary>A charm, a cloak or a wand helps with traps, riddles and parley.</summary>
    public static int Skill(Content c, HeroSheet s) => Bonus(c, s, "trinket");

    /// <summary>Points of gear above the kit the class arrives with.</summary>
    public static int Edge(Content c, Agent a)
    {
        var s = a.Hero!;
        int dex = Rng.Modifier(a.Abilities.Dex);
        var kit = s.Cls.Wears.Count > 0 ? c.Library.GearOf(s.Cls.Wears[0]) : null;
        int edge = Math.Max(0, ArmorAc(s.Armor, dex, a) - ArmorAc(kit, dex, a));
        foreach (var slot in Slots) edge += Bonus(c, s, slot);
        return edge;
    }

    /// <summary>How many levels a company's gear is worth when it sizes up a site.</summary>
    public static double Levels(Content c, IReadOnlyList<Agent> members) =>
        members.Count == 0 ? 0 : members.Average(m => Edge(c, m)) / c.Gear.EdgePerLevel;

    /// <summary>What the item would add over what the hero wears in its slot; zero or less if they have no use for it.</summary>
    public static int Gain(Content c, Agent a, LibItem it)
    {
        var s = a.Hero!;
        string slot = SlotOf(it);
        if (slot == "armor" && s.Armor == null) return 0;
        if (slot == "shield" && (s.Shield == null || !s.Cls.Shield)) return 0;
        if (!Slots.Contains(slot)) return 0;
        return BonusOf(c, it) - Bonus(c, s, slot);
    }

    // Names -------------------------------------------------------------------------------------

    /// <summary>"+1 longsword", "Frost Brand", "+2 chain mail".</summary>
    public static string Label(HeroSheet s, LibItem it)
    {
        var m = Plain.Match(it.Name);
        if (!m.Success) return it.Name;
        var on = m.Groups[1].Value switch { "Weapon" => s.Weapon, "Armor" => s.Armor, _ => s.Shield };
        return $"+{m.Groups[2].Value} {Library.Prose(on?.Name ?? m.Groups[1].Value)}";
    }

    /// <summary>What the hero fights with, for the sheet: "+1 longsword, chain mail, shield; Cloak of Elvenkind".</summary>
    public static string Describe(HeroSheet s)
    {
        string Piece(LibGear? g, string slot)
        {
            if (s.Magic.TryGetValue(slot, out var it)) return Label(s, it);
            return g == null ? "" : Library.Prose(g.Name);
        }
        var parts = new[] { Piece(s.Weapon, "weapon"), Piece(s.Armor, "armor"), Piece(s.Shield, "shield") }.Where(p => p != "").ToList();
        string kit = parts.Count > 0 ? string.Join(", ", parts) : "bare hands";
        return s.Magic.TryGetValue("trinket", out var t) ? $"{kit}; {t.Name}" : kit;
    }

    private static void Relist(HeroSheet s)
    {
        s.Items.Clear();
        foreach (var slot in Slots)
            if (s.Magic.TryGetValue(slot, out var it)) s.Items.Add(Label(s, it));
    }

    // Loot ----------------------------------------------------------------------------------------

    /// <summary>Gold merchants give for a magic item nobody needs.</summary>
    public static double SellValue(Town town, LibItem it)
    {
        var r = Rules(town);
        return Math.Floor(Math.Min(it.ValueGp * r.SellShare, r.SellCap[Math.Min(it.Tier, r.SellCap.Length - 1)]));
    }

    /// <summary>A hero brings home an item from a hoard: they wear it if it beats theirs, and sell whichever they don't wear.</summary>
    public static void Claim(Town town, Agent a, LibItem it)
    {
        var s = a.Hero!;
        if (Gain(town.Content, a, it) > 0) Wear(town, a, it);
        else Sell(town, a, it);
    }

    private static void Wear(Town town, Agent a, LibItem it)
    {
        var s = a.Hero!;
        string slot = SlotOf(it);
        var old = s.Magic.TryGetValue(slot, out var o) ? o : null;
        s.Magic[slot] = it;
        Relist(s);
        if (old != null) Sell(town, a, old);
    }

    private static void Sell(Town town, Agent a, LibItem it)
    {
        double gold = SellValue(town, it);
        a.Gold += gold;
        town.Log(LedgerCategory.Quest, Importance.Routine, $"{a.Name} sells the {it.Name} to a passing merchant for {Js.Str(gold)} gold.", [a.Id]);
    }

    // Shopping ------------------------------------------------------------------------------------

    private sealed record Offer(string Slot, double Price, double Points, Building Shop, LibGear? Piece, LibItem? Magic);

    private static List<Building> Open(Town town, string id) =>
        town.Buildings.Where(b => b.Def.Id == id && b.Complete && !b.Closed && !b.ForSale).ToList();

    /// <summary>Magic arms the forges can make for a town of this size.</summary>
    public static List<LibItem> Enchanted(Town town)
    {
        var list = new List<LibItem>();
        foreach (var tier in Rules(town).Enchanted)
        {
            if (town.Population < tier.Population) continue;
            if (tier.Faction != "" && Factions.Find(town, tier.Faction) is not { Active: true }) continue;
            foreach (var id in tier.Items)
                if (town.Content.Library.Item(id) is LibItem it) list.Add(it);
        }
        return list;
    }

    private static double Avg(Dice? d) => d is Dice x ? x.Count * (x.Die + 1) / 2.0 + x.Bonus : 0;

    private static bool Light(LibGear g) => g.AddDex && g.DexCap == null;

    /// <summary>Each hero in town with gold to spare buys the best upgrade they can afford, one a day.</summary>
    public static void Shop(Town town)
    {
        var r = Rules(town);
        var forges = Open(town, r.Forge);
        var stores = Open(town, r.Store);
        if (forges.Count == 0 && stores.Count == 0) return;
        var magic = forges.Count > 0 ? Enchanted(town) : [];
        var lib = town.Content.Library;
        foreach (var a in town.Heroes)
        {
            var s = a.Hero!;
            if (s.AwayUntil != null) continue;
            double budget = a.Gold - r.Reserve * (a.Has(Trait.Greedy) ? r.GreedyReserve : 1);
            if (budget <= 0) continue;
            var forge = forges.Count > 0 ? forges[a.Id % forges.Count] : null;
            var store = stores.Count > 0 ? stores[a.Id % stores.Count] : null;
            var offers = new List<Offer>();

            void Mundane(LibGear g, string slot, double points, bool storeSells)
            {
                if (points <= 0) return;
                if (forge != null) offers.Add(new Offer(slot, g.CostGp, points, forge, g, null));
                else if (store != null && storeSells) offers.Add(new Offer(slot, Math.Ceiling(g.CostGp * r.StoreMarkup), points, store, g, null));
            }

            if (!s.Cls.Spell)
                foreach (var id in s.Cls.Better)
                    if (lib.GearOf(id) is LibGear w) Mundane(w, "weapon", (Avg(w.Damage) - Avg(s.Weapon?.Damage)) / 2, true);
            int dex = Rng.Modifier(a.Abilities.Dex);
            foreach (var id in s.Cls.Wears)
                if (lib.GearOf(id) is LibGear arm && a.Abilities.Str >= arm.Strength && s.Armor != null)
                    Mundane(arm, "armor", ArmorAc(arm, dex, a) - ArmorAc(s.Armor, dex, a), Light(arm));
            if (forge != null)
                foreach (var it in magic)
                {
                    // A war mage's wand is for casters; the rest is for whoever can wear it.
                    if (SlotOf(it) == "trinket" && !s.Cls.Spell) continue;
                    int gain = Gain(town.Content, a, it);
                    if (gain > 0) offers.Add(new Offer(SlotOf(it), it.ValueGp, gain, forge, null, it));
                }

            var buy = offers
                .Where(o => o.Price <= budget)
                .OrderByDescending(o => o.Points)
                .ThenBy(o => o.Price)
                .ThenBy(o => o.Piece?.Id ?? o.Magic!.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (buy != null) Buy(town, a, buy);
        }
    }

    private static void Buy(Town town, Agent a, Offer o)
    {
        var s = a.Hero!;
        double tax = o.Price * town.TaxRate;
        a.Gold -= o.Price;
        town.Book(TreasuryAccount.SalesTax, tax);
        o.Shop.Cash += o.Price - tax - o.Price * Rules(town).Wholesale;
        o.Shop.RevenueToday += o.Price;
        string what;
        if (o.Magic != null)
        {
            Wear(town, a, o.Magic);
            what = Label(s, o.Magic);
        }
        else
        {
            var g = o.Piece!;
            if (o.Slot == "weapon") s.Weapon = g;
            else if (o.Slot == "armor") s.Armor = g;
            else s.Shield = g;
            Relist(s);
            what = Library.Prose(g.Name);
            if (o.Slot != "armor") what = $"{Agent.Article(what)} {what}";
        }
        town.Log(
            LedgerCategory.Quest,
            o.Magic != null ? Importance.Notable : Importance.Routine,
            $"{a.Name} buys {(o.Magic != null ? "a " : "")}{what} at {o.Shop.Name} for {Js.Str(o.Price)} gold.",
            [a.Id]);
    }
}
