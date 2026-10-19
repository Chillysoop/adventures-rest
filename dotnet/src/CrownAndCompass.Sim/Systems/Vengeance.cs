namespace CrownAndCompass.Sim.Systems;

/// <summary>How enemies hold a grudge (crises.json "vengeance").</summary>
public sealed record VengeanceRules
{
    /// <summary>Heat a grudge needs before the foe strikes, and what each strike spends; heat cools this much a day.</summary>
    public double StrikeAt { get; init; } = 30;
    public double Decay { get; init; } = 0.4;
    public double Cost { get; init; } = 30;
    public int[] EveryDays { get; init; } = [6, 12];

    /// <summary>The dullest wits that plot revenge (a cunning tier id); beasts and the mindless only lash out.</summary>
    public string MinCunning { get; init; } = "crafty";

    /// <summary>Heat for each kind of wrong, and how the foe names it: {seat}, {where}.</summary>
    public IReadOnlyDictionary<string, double> Heat { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, string> Deeds { get; init; } = new Dictionary<string, string>();
    public string Vow { get; init; } = "";

    /// <summary>The blows it strikes, by weight; a marked hero in town is always the first target.</summary>
    public IReadOnlyDictionary<string, double> Blows { get; init; } = new Dictionary<string, double>();
}

/// <summary>A foe's grudge against the town, and the heroes it has marked for death.</summary>
public sealed class Grudge
{
    /// <summary>Who holds it: "menace:12", "faction:guild-of-the-gilded-scale"; any system with a foe can keep one.</summary>
    public string Foe { get; init; } = "";
    public string Name { get; set; } = "";
    public string Leader { get; set; } = "";
    public string Seat { get; set; } = "";
    public double Heat { get; set; }

    /// <summary>How hard its agents are to catch (a blow's cunning, see <see cref="Blows"/>).</summary>
    public double Cunning { get; set; } = 1;
    public List<int> Marked { get; } = [];

    /// <summary>The foe's kind and wits (a menace kind and cunning tier), for the plots only some foes hatch (<see cref="Infiltration"/>).</summary>
    public string Kind { get; set; } = "";
    public string Wits { get; set; } = "";
    public int NextDay { get; set; }
    public bool Vowed { get; set; }
}

/// <summary>
/// Vengeance: an enemy the town has wronged (its lairs cleared, its seat broken, its host fallen on in the field,
/// its demand refused) remembers, and a cunning one sends knives. Each wrong heats its grudge and marks the heroes
/// who did it; past the line it strikes (<see cref="Blows"/>), at a marked hero in town first, otherwise at a
/// councillor, a house, the treasury or the walls, and the strike spends the heat. Grudges cool by themselves and
/// die with the foe. Other systems keep grudges too: call <see cref="Wrong"/> with a foe key of their own.
/// </summary>
public static class Vengeance
{
    private static VengeanceRules Rules(Town town) => town.Content.Crises.Vengeance;

    public static Grudge? Of(Town town, string foe) => town.Grudges.FirstOrDefault(g => g.Foe == foe);

    public static string Key(Menace m) => $"menace:{m.Id}";

    /// <summary>The town wronged a foe: heat its grudge by this kind of wrong (a key of crises.json vengeance.heat), and mark these heroes.</summary>
    public static void Wrong(Town town, string foe, Culprit who, string wrong, double cunning, IEnumerable<int>? heroes = null, string where = "", double scale = 1)
    {
        var r = Rules(town);
        double heat = r.Heat.GetValueOrDefault(wrong, 15) * scale;
        if (heat <= 0) return;
        var g = Of(town, foe);
        if (g == null)
        {
            g = new Grudge { Foe = foe };
            town.Grudges.Add(g);
        }
        g.Name = who.Name;
        g.Leader = who.Leader;
        g.Seat = who.Seat;
        g.Cunning = cunning;
        g.Heat += heat;
        foreach (int id in heroes ?? []) if (!g.Marked.Contains(id)) g.Marked.Add(id);
        if (g.NextDay == 0) g.NextDay = town.Day + town.Rng.Range(r.EveryDays[0], r.EveryDays[^1] + 1);
        if (g.Vowed || g.Heat < r.StrikeAt || r.Vow == "") return;
        g.Vowed = true;
        string deed = r.Deeds.GetValueOrDefault(wrong, "what it did").Replace("{seat}", who.Seat).Replace("{where}", where);
        town.Log(LedgerCategory.War, Importance.Notable, Saga.Sentences(r.Vow.Replace("{seat}", who.Seat).Replace("{leader}", who.Leader).Replace("{town}", town.Name).Replace("{deed}", deed)));
    }

    /// <summary>A menace the town wronged; dull ones don't plot, and the heroes of the last party sent at it are marked.</summary>
    internal static void Wrong(Town town, Menace m, string wrong, string where = "", IEnumerable<int>? heroes = null)
    {
        if (!m.Alive || m.Origin == Menaces.FromWarband) return;
        if (Menaces.CunningRank(town, m.Cunning) < Menaces.CunningRank(town, Rules(town).MinCunning)) return;
        var sites = m.Holdings.Prepend(m.SeatId).ToHashSet();
        heroes ??= town.Parties.Where(p => sites.Contains(p.SiteId)).OrderByDescending(p => p.DepartTick).ThenByDescending(p => p.Id).FirstOrDefault()?.Members ?? [];
        var seat = Quests.Site(town, m.SeatId);
        double cunning = town.Content.Crises.Score.Cunning.GetValueOrDefault(m.Cunning, 1);
        Wrong(town, Key(m), new Culprit(m.Name, m.Leader, seat?.Def.Name ?? m.Region), wrong, cunning, heroes, where);
        if (Of(town, Key(m)) is { } g)
        {
            g.Kind = m.Kind;
            g.Wits = m.Cunning;
        }
    }

    public static void Daily(Town town)
    {
        var r = Rules(town);
        foreach (var g in town.Grudges.ToList())
        {
            if (g.Foe.StartsWith("menace:", StringComparison.Ordinal) && int.TryParse(g.Foe[7..], out int id) && id >= 1 && id <= town.Saga.Menaces.Count && !town.Saga.Menaces[id - 1].Alive)
            {
                town.Grudges.Remove(g);
                continue;
            }
            g.Heat = Math.Max(0, g.Heat - r.Decay);
            g.Marked.RemoveAll(h => town.Agent(h) is { } a && (a.Dead || a.Departed));
            if (g.Heat < r.StrikeAt)
            {
                if (g.Heat <= 0 && g.Marked.Count == 0) town.Grudges.Remove(g);
                continue;
            }
            if (town.Day < g.NextDay) continue;
            g.NextDay = town.Day + town.Rng.Range(r.EveryDays[0], r.EveryDays[^1] + 1);
            g.Heat -= r.Cost;
            g.Vowed = false;
            Strike(town, g);
            if (town.Outcome != null) return;
        }
    }

    private static void Strike(Town town, Grudge g)
    {
        var by = new Culprit(g.Name, g.Leader, g.Seat);
        var mark = g.Marked.Select(town.Agent).Where(a => !a.Dead && !a.Departed && !a.IsAway).OrderBy(a => a.Id).FirstOrDefault();
        if (mark != null)
        {
            if (Blows.Murder(town, by, g.Cunning, mark)) g.Marked.Remove(mark.Id);
            return;
        }
        // A clever foe may buy, charm or blood-bind a councillor instead.
        if (Infiltration.Try(town, g)) return;
        var weights = Rules(town).Blows.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        if (weights.Count == 0) return;
        double roll = town.Rng.Next() * weights.Sum(kv => kv.Value);
        string kind = weights[^1].Key;
        foreach (var (k, w) in weights)
        {
            roll -= w;
            if (roll < 0)
            {
                kind = k;
                break;
            }
        }
        Blows.Strike(town, kind, by, 40 * g.Cunning, g.Cunning);
    }
}
