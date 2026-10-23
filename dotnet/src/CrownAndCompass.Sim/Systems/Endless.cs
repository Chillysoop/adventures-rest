namespace CrownAndCompass.Sim.Systems;

/// <summary>Something that strikes the town as a new era begins (content/game/endless.json).</summary>
public sealed record ComplicationDef
{
    public string Id { get; init; } = "";
    public string Text { get; init; } = "";
    public double Dread { get; init; }
    public double Mandate { get; init; }

    /// <summary>Share of the treasury lost, share every living menace grows, and share the farms lose for <see cref="Days"/>.</summary>
    public double Gold { get; init; }
    public double Empower { get; init; }
    public double Harvest { get; init; }
    public int Days { get; init; }
}

public sealed record EndlessRules
{
    public int[] RestDays { get; init; } = [20, 40];
    public int LevelUp { get; init; } = 2;
    public List<FallbackFoe> Heralds { get; init; } = [];
    public List<ComplicationDef> Complications { get; init; } = [];
}

/// <summary>An era of an endless campaign that ended: how, when, and against whom.</summary>
public sealed class Era
{
    public int Number { get; set; }
    public int Day { get; set; }
    public string Road { get; set; } = "";
    public string Foe { get; set; } = "";
}

public sealed class EndlessState
{
    public bool On { get; set; }
    public List<Era> Eras { get; } = [];

    /// <summary>When the next herald comes, while the land rests between eras.</summary>
    public int? NextHeraldDay { get; set; }
    public int FoeLevel { get; set; }
    public List<string> HeraldsUsed { get; } = [];
    public List<string> ComplicationsUsed { get; } = [];

    /// <summary>The farms yield this share less until <see cref="HarvestUntil"/>.</summary>
    public double HarvestCut { get; set; }
    public int HarvestUntil { get; set; }
}

/// <summary>
/// Endless mode: a win closes an era instead of the campaign. The land rests a few weeks, then something worse
/// comes, a herald out of the world's history crowned the next big bad, stronger than the last, with a
/// complication to make the new era hard (a cracked moon, a blight, a dead god stirring, wild magic, cursed gold).
/// Roads to victory open again against it. Only a fall ends an endless campaign.
/// </summary>
public static class Endless
{
    public static EndlessRules Rules(Town town) => town.Content.Endless;

    public static EndlessState State(Town town) => town.Saga.Endless;

    public static bool On(Town town) => town.Saga.Enabled && State(town).On;

    /// <summary>The era the town is in, from 1.</summary>
    public static int EraNumber(Town town) => State(town).Eras.Count + 1;

    /// <summary>Days until the next herald comes, while the land rests.</summary>
    public static int? RestLeft(Town town) => State(town).NextHeraldDay is int d ? Math.Max(0, d - town.Day) : null;

    /// <summary>The big bad's threat is ended by this road: the era closes, the land rests, and the next herald is due.</summary>
    public static void EraWon(Town town, string road, Menace m)
    {
        var st = State(town);
        var s = town.Saga;
        st.Eras.Add(new Era { Number = st.Eras.Count + 1, Day = town.Day, Road = road, Foe = m.Name });
        st.FoeLevel = Math.Max(st.FoeLevel, m.Level);
        s.BigBadId = null;
        s.BigBadEscapes = 0;
        DarkQuests.Reset(town);
        town.Victory.Claimed = "";
        town.Mandate = Math.Min(100, town.Mandate + 25);
        if (m.Alive)
        {
            m.BigBad = false;
            Menaces.End(town, m);
        }
        var r = Rules(town);
        st.NextHeraldDay = town.Day + town.Rng.Range(r.RestDays[0], r.RestDays[1] + 1);
        string how = Victory.Def(town, road)?.Title ?? "The Long Night is over";
        town.Log(LedgerCategory.War, Importance.Major, Saga.Sentences($"Era {st.Eras.Count} ends. {how}: {m.Name} is finished. {town.Name} rests, but the land is never quiet for long."));
    }

    public static void Daily(Town town)
    {
        if (!On(town) || town.Outcome != null) return;
        var st = State(town);
        if (st.HarvestUntil > 0 && town.Day >= st.HarvestUntil)
        {
            st.HarvestCut = 0;
            st.HarvestUntil = 0;
        }
        if (st.NextHeraldDay is int day && town.Day >= day) Herald(town);
    }

    /// <summary>The next era begins: a herald crowned the big bad, stronger than the last, and a complication strikes.</summary>
    private static void Herald(Town town)
    {
        var st = State(town);
        var r = Rules(town);
        var s = town.Saga;
        var f = town.Content.Saga.Finale;
        st.NextHeraldDay = null;
        string old = st.Eras.Count > 0 ? Leader(town, st.Eras[^1].Foe) : "";
        var pool = r.Heralds.Where(h => !st.HeraldsUsed.Contains(Id(h))).ToList();
        if (pool.Count == 0)
        {
            st.HeraldsUsed.Clear();
            pool = [.. r.Heralds];
        }
        if (pool.Count == 0) return;
        var h = pool[town.Rng.Range(0, pool.Count)];
        st.HeraldsUsed.Add(Id(h));
        var foe = h with { Leader = h.Leader.Replace("{old}", old == "" ? "the last tyrant" : old) };
        var m = Menaces.Awaken(town, foe);
        if (m == null) return;
        s.BigBadId = m.Id;
        s.CrownDay = town.Day;
        s.Horde.Stage = HordeStage.None;
        s.Horde.Breached = false;
        s.NextRideDay = town.Day + f.RideAfterDays;
        m.BigBad = true;
        Menaces.Ascend(town, m);
        m.Level = Math.Max(m.Level, Math.Min(town.Content.Saga.Menaces.MaxLevel, st.FoeLevel + r.LevelUp));
        string text = Saga.Fill(town, h.Text.Replace("{old}", old == "" ? "the last tyrant" : old), m);
        town.Log(LedgerCategory.War, Importance.Major, Saga.Sentences($"Era {EraNumber(town)}. {text} {Saga.Cap(m.Name)} is the big bad now."));
        Complicate(town);
    }

    private static void Complicate(Town town)
    {
        var st = State(town);
        var r = Rules(town);
        var pool = r.Complications.Where(c => !st.ComplicationsUsed.Contains(c.Id)).ToList();
        if (pool.Count == 0)
        {
            st.ComplicationsUsed.Clear();
            pool = [.. r.Complications];
        }
        if (pool.Count == 0) return;
        var c = pool[town.Rng.Range(0, pool.Count)];
        st.ComplicationsUsed.Add(c.Id);
        if (c.Dread > 0) Saga.Scar(town, c.Dread);
        if (c.Mandate != 0) town.Mandate = Math.Clamp(town.Mandate + c.Mandate, 0, 100);
        if (c.Gold > 0 && town.Treasury > 0) town.Book(TreasuryAccount.Discrepancies, -Math.Floor(town.Treasury * c.Gold));
        if (c.Empower > 0)
            foreach (var m in Menaces.Alive(town).Where(m => !m.BigBad)) m.Power *= 1 + c.Empower;
        if (c.Harvest > 0)
        {
            st.HarvestCut = c.Harvest;
            st.HarvestUntil = town.Day + Math.Max(1, c.Days);
        }
        town.Log(LedgerCategory.Town, Importance.Major, Saga.Sentences(c.Text));
    }

    /// <summary>The share of their yield the farms keep under a blight.</summary>
    public static double Harvest(Town town) => On(town) ? 1 - State(town).HarvestCut : 1;

    private static string Id(FallbackFoe h) => $"{h.Kind}:{h.Leader}";

    /// <summary>The leader's name out of a menace's name ("Morrak's horde" is Morrak's).</summary>
    private static string Leader(Town town, string foe) =>
        town.Saga.Menaces.LastOrDefault(m => m.Name == foe)?.Leader ?? foe;
}
