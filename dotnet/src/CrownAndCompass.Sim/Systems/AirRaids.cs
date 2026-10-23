namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Late in the game, something in the sky comes for the town itself (sky.json `airRaids`): a roost dragon, the sky armada,
/// the storm cult or the cloud lords, whichever still holds isles. Rare, and always warned of days ahead; walls are no help.
/// The moored and patrolling airships' guns, the watchtowers, the watch and the heroes at home meet it. Beaten off, the
/// heroes make a name; landed, it robs the treasury or burns timber buildings, and spares the townsfolk. Rolls on its own dice.
/// </summary>
public static class AirRaids
{
    public static AirRaidRules Rules(Town town) => town.Content.Sky.AirRaids;

    private static Rng Dice(Town town, int salt) => new(town.Seed * 8231 + town.Day * 67 + salt * 89 + 11);

    /// <summary>Who could come: attackers whose isles still stand uncleared, with how many.</summary>
    public static List<(AirAttackerDef Def, List<SiteState> Isles)> Attackers(Town town) =>
        Rules(town).Attackers
            .Select(a => (a, town.Sites.Where(s => s.Def.Aloft && !s.Cleared && (a.Foe != "" && s.Def.Foe == a.Foe || a.Group != "" && s.Def.Group == a.Group)).OrderBy(s => s.Def.Id, StringComparer.Ordinal).ToList()))
            .Where(x => x.Item2.Count > 0)
            .ToList();

    /// <summary>The town's guard against the air: ships' guns at the mast or on patrol, watchtowers, the watch and the heroes at home.</summary>
    public static double Guard(Town town)
    {
        var r = Rules(town);
        var sky = Sky.Rules(town);
        double guns = Js.Sum(Sky.Ships(town).Where(s => Sky.Moored(town, s) || s.Flight?.Job == "patrol").ToList(), s => Fleet.Def(town, s).Guns + sky.ProwessFight * (s.Prowess - 1));
        double heroes = Js.Sum(town.Heroes.Where(a => !a.IsAway && a.Hero!.Hp > 0).ToList(), a => a.Hero!.Level);
        return guns + Watch.Watchtowers(town).Count * sky.TowerGuns + town.Watch * sky.WatchFight + heroes * r.HeroLevel;
    }

    /// <summary>How strong it comes: its own strength, more as the years pass.</summary>
    public static double Strength(Town town, AirAttackerDef a) => a.Strength + Rules(town).PerYear * Math.Max(0, town.Day - Rules(town).From) / 360.0;

    public static void Daily(Town town)
    {
        var r = Rules(town);
        var st = town.SkyFleet;
        if (r.Attackers.Count == 0 || town.Day < r.From) return;
        if (st.AirRaid is { } due)
        {
            if (town.Day >= due.Day) Strike(town, due);
            return;
        }
        if (town.Day < st.NextAirRaidDay) return;
        st.NextAirRaidDay = town.Day + r.Every;
        var rng = Dice(town, 1);
        if (!rng.Chance(r.Chance)) return;
        var pool = Attackers(town);
        if (pool.Count == 0) return;
        double total = Js.Sum(pool, p => p.Isles.Count * p.Def.Weight);
        double roll = rng.Next() * total;
        var pick = pool[^1];
        foreach (var p in pool)
        {
            roll -= p.Isles.Count * p.Def.Weight;
            if (roll < 0) { pick = p; break; }
        }
        var from = pick.Isles[rng.Range(0, pick.Isles.Count)];
        int warn = r.WarnDays + (Sky.Has(town, Sky.Rules(town).Parts.Signal) ? r.SignalDays : 0);
        st.AirRaid = new AirRaid { Attacker = pick.Def.Id, SiteId = from.Def.Id, Day = town.Day + warn };
        Rumors.Hear(town, from, Rumors.Occupants(town.Content, from.Def), from.Def.Tier, 90);
        town.Log(LedgerCategory.War, Importance.Major, Fill(pick.Def.Warn, from, warn));
    }

    private static void Strike(Town town, AirRaid raid)
    {
        var r = Rules(town);
        town.SkyFleet.AirRaid = null;
        var a = r.Attackers.FirstOrDefault(x => x.Id == raid.Attacker);
        var from = Quests.Site(town, raid.SiteId);
        if (a == null || from == null) return;
        // Cleared before it could fly: nothing comes.
        if (from.Cleared)
        {
            town.Log(LedgerCategory.War, Importance.Notable, Fill(a.Called, from, 0));
            return;
        }
        var rng = Dice(town, 2);
        double guard = Guard(town), strength = Strength(town, a);
        var heroes = town.Heroes.Where(h => !h.IsAway && h.Hero!.Hp > 0).OrderByDescending(h => h.Hero!.Level).ThenBy(h => h.Id).Take(4).ToList();
        if (rng.D20() + guard >= 10 + strength)
        {
            foreach (var h in heroes) Notoriety.Deed(town, h, "slay", r.Fame);
            from.Strength = Math.Max(0, from.Strength * (1 - r.Hurt));
            town.Mandate = Math.Min(100, town.Mandate + r.Mandate);
            town.Log(LedgerCategory.War, Importance.Major, Fill(a.Repelled, from, 0), heroes.Select(h => h.Id).ToList());
            return;
        }
        double gold = Math.Min(Math.Max(0, town.Treasury), a.Gold);
        if (gold > 0) town.Book(TreasuryAccount.Discrepancies, -gold);
        var burned = new List<string>();
        var timber = town.Buildings.Where(b => b.Complete && !b.Def.IsGrand && Blows.Burns(b) && b.Def.Zone is Zone.Residential or Zone.Commercial or Zone.Industry).OrderBy(b => b.Id).ToList();
        for (int i = 0; i < a.Burns && timber.Count > 0; i++)
        {
            if (rng.Chance(Research.Fireproof(town))) continue;
            var b = timber[rng.Range(0, timber.Count)];
            timber.Remove(b);
            Blows.Wreck(town, b, 0.4 + rng.Next() * 0.3);
            burned.Add(b.Name);
        }
        town.Tension += 10 + 5 * burned.Count;
        town.Mandate = Math.Max(0, town.Mandate - r.Mandate);
        Saga.Scar(town, burned.Count > 0 ? 1 : 0);
        town.Log(LedgerCategory.War, Importance.Major, Fill(a.Landed, from, 0)
            .Replace("{gold}", Js.Str(Js.Round(gold)), StringComparison.Ordinal)
            .Replace("{burned}", burned.Count == 0 ? "nothing" : Expedition.ListNames(burned), StringComparison.Ordinal));
    }

    private static string Fill(string text, SiteState from, int days) => text
        .Replace("{site}", from.Def.Name, StringComparison.Ordinal)
        .Replace("{days}", days == 1 ? "a day" : $"{days} days", StringComparison.Ordinal);
}

/// <summary>An attack from the air on its way: who, from which isle, and the day it comes.</summary>
public sealed class AirRaid
{
    public string Attacker { get; init; } = "";
    public string SiteId { get; init; } = "";
    public int Day { get; init; }
}

public sealed record AirAttackerDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Its isles: those of this band foe (foes.json) or this sky group (groups.json).</summary>
    public string Foe { get; init; } = "";
    public string Group { get; init; } = "";
    public double Weight { get; init; } = 1;
    public double Strength { get; init; } = 20;
    public double Gold { get; init; }
    public int Burns { get; init; }
    public string Warn { get; init; } = "";
    public string Repelled { get; init; } = "";
    public string Landed { get; init; } = "";
    public string Called { get; init; } = "";
}

/// <summary>sky.json `airRaids`: from `from` (day), every `every` days a `chance`; warned `warnDays` ahead (`signalDays` more with the signal tower).</summary>
public sealed record AirRaidRules
{
    public int From { get; init; } = 400;
    public int Every { get; init; } = 30;
    public double Chance { get; init; } = 0.2;
    public int WarnDays { get; init; } = 3;
    public int SignalDays { get; init; } = 2;
    public double HeroLevel { get; init; } = 0.5;
    public double PerYear { get; init; } = 4;
    public int Fame { get; init; } = 3;
    public double Hurt { get; init; } = 0.3;
    public double Mandate { get; init; } = 3;
    public IReadOnlyList<AirAttackerDef> Attackers { get; init; } = [];
}
