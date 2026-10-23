namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Potions, oils and scrolls from a hoard (enchantments.json draughts and items): a healing potion or a spell scroll goes
/// into the hero's stock; anything else is carried to the next delve and drunk at its start, its powers lasting that
/// delve (a giant's strength, a hasting draught, an oil that keeps the blade keen).
/// </summary>
public static class Draughts
{
    /// <summary>A draught for the hero: stock, a carried flask, or sold if they carry enough already.</summary>
    public static void Take(Town town, Agent a, LibItem it)
    {
        var r = Enchant.Rules(town.Content);
        var s = a.Hero!;
        if (r.Draughts.TryGetValue(it.Id, out var d) && (d.Potions > 0 || d.Scrolls > 0))
        {
            s.Potions += d.Potions;
            s.Scrolls += d.Scrolls;
            return;
        }
        if (Enchant.Of(town.Content, it).Count > 0 && s.Draughts.Count < r.DraughtsMost)
        {
            s.Draughts.Add(it);
            return;
        }
        Gear.SellOnRoad(town, a, it);
    }

    /// <summary>The powers of the draughts the hero carries, for the next delve.</summary>
    public static List<(ItemPowerDef Power, LibItem Item)> Powers(Content c, HeroSheet s) =>
        s.Draughts.SelectMany(it => Enchant.Of(c, it).Select(p => (p, it))).ToList();

    /// <summary>What the draughts add to the hero's blows and armour while they last: might over what the hero wears, and wards.</summary>
    public static (int Attack, int Ac) Edge(Content c, HeroSheet s)
    {
        if (s.Draughts.Count == 0) return (0, 0);
        var ps = Powers(c, s);
        int might = (int)ps.Where(p => p.Power.Kind == "might").Select(p => p.Power.Amount).DefaultIfEmpty(0).Max();
        int ac = (int)Js.Sum(ps.Where(p => p.Power.Kind == "ac"), p => p.Power.Amount);
        return (Math.Max(0, might - Enchant.Might(c, s)), ac);
    }
}
