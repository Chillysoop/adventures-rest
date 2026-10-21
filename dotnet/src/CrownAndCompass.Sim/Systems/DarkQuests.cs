namespace CrownAndCompass.Sim.Systems;

/// <summary>What a finished dark quest gives the big bad (content/game/darkquests.json).</summary>
public sealed record DarkGainDef
{
    /// <summary>Share added to every march's host, and to how much each march grows.</summary>
    public double Host { get; init; }
    public double Growth { get; init; }

    /// <summary>Defeats it survives beyond the finale's.</summary>
    public int Escapes { get; init; }

    /// <summary>Settlements turned against the town, minor powers bound to it, and dread in town.</summary>
    public int Sway { get; init; }
    public int Pact { get; init; }
    public double Dread { get; init; }
}

/// <summary>One quest the big bad may go on: where its prize is told, and what it gains.</summary>
public sealed record DarkQuestDef
{
    public string Id { get; init; } = "";

    /// <summary>Menace kinds that go on it; empty for any.</summary>
    public IReadOnlyList<string> Kinds { get; init; } = [];
    public IReadOnlyList<string> Things { get; init; } = [];
    public string Text { get; init; } = "";
    public string Done { get; init; } = "";
    public string Foiled { get; init; } = "";
    public DarkGainDef Gain { get; init; } = new();
}

public sealed record DarkQuestRules
{
    public int[] First { get; init; } = [15, 30];
    public int[] Every { get; init; } = [20, 35];
    public int Most { get; init; } = 3;
    public int[] Days { get; init; } = [40, 55];

    /// <summary>The quest's site is about this many tiers below the town's champions, so heroes can beat it there.</summary>
    public int Below { get; init; } = 1;

    /// <summary>How sure the town is of what waits at the site once word of the quest comes.</summary>
    public int Trust { get; init; } = 70;

    /// <summary>Share of its power the big bad loses when a quest is foiled.</summary>
    public double FoilPower { get; init; } = 0.15;
    public List<DarkQuestDef> Quests { get; init; } = [];
}

/// <summary>The quest the big bad is on.</summary>
public sealed class DarkQuest
{
    public string Def { get; set; } = "";
    public string Thing { get; set; } = "";
    public string SiteId { get; set; } = "";
    public int StartDay { get; set; }
    public int DueDay { get; set; }

    /// <summary>Once over: the town's heroes got there first.</summary>
    public bool Foiled { get; set; }
}

/// <summary>The big bad's quests: the one under way, those it finished and those the town foiled, and what they gave it.</summary>
public sealed class DarkQuestState
{
    public DarkQuest? Current { get; set; }
    public int NextDay { get; set; }

    /// <summary>Quests over, finished or foiled, in order.</summary>
    public List<DarkQuest> Past { get; } = [];
    public double Host { get; set; }
    public double Growth { get; set; }
    public int Escapes { get; set; }
}

/// <summary>
/// The big bad has quests of its own. Once crowned it goes after, one at a time, things that make it far
/// stronger: a relic, a rite, a hoard, a hidden heart, a pact with a great power, the lords of the far valleys.
/// Each happens at a site in the wilds the town hears of at once; heroes who clear that site before it is done
/// foil it and bring the prize home. Left alone, each quest makes every host it sends larger, lets it survive
/// more defeats, binds powers or settlements to it, or deepens the dread. It can only win by taking the town;
/// its quests are how it gets strong enough to.
/// </summary>
public static class DarkQuests
{
    public static DarkQuestRules Rules(Town town) => town.Content.DarkQuests;

    public static DarkQuestState State(Town town) => town.Saga.DarkQuests;

    public static DarkQuestDef? Def(Town town, string id) => Rules(town).Quests.FirstOrDefault(q => q.Id == id);

    /// <summary>The quest under way, if the big bad lives.</summary>
    public static DarkQuest? Current(Town town) => Saga.BigBad(town) is { Alive: true } ? State(town).Current : null;

    /// <summary>The site the big bad's quest is at: the stand-in mayor sends heroes there first.</summary>
    public static bool Sought(Town town, SiteState s) => Current(town)?.SiteId == s.Def.Id;

    public static int DaysLeft(Town town) => Current(town) is { } q ? Math.Max(0, q.DueDay - town.Day) : 0;

    /// <summary>What the quest is, told as the town hears it.</summary>
    public static string Describe(Town town, DarkQuest q) =>
        Def(town, q.Def) is { } d && Saga.BigBad(town) is { } m ? Fill(town, d.Text, m, q) : "";

    public static void Daily(Town town)
    {
        if (!town.Saga.Enabled || town.Outcome != null || Saga.BigBad(town) is not { Alive: true } m) return;
        var st = State(town);
        var r = Rules(town);
        if (st.Current is { } q)
        {
            var site = Quests.Site(town, q.SiteId);
            if (site == null || site.Cleared) Foil(town, m, q, site);
            else if (town.Day >= q.DueDay) Finish(town, m, q);
            return;
        }
        if (st.Past.Count >= r.Most) return;
        if (st.NextDay == 0) st.NextDay = town.Saga.CrownDay + town.Rng.Range(r.First[0], r.First[1] + 1);
        if (town.Day >= st.NextDay) Begin(town, m);
    }

    private static void Begin(Town town, Menace m)
    {
        var st = State(town);
        var r = Rules(town);
        var used = st.Past.Select(x => x.Def).ToHashSet();
        var fits = r.Quests.Where(d => (d.Kinds.Count == 0 || d.Kinds.Contains(m.Kind)) && !used.Contains(d.Id) && d.Things.Count > 0).ToList();
        var site = SiteFor(town, m);
        if (fits.Count == 0 || site == null)
        {
            st.NextDay = town.Day + r.Every[0];
            return;
        }
        var def = fits[town.Rng.Range(0, fits.Count)];
        var q = new DarkQuest
        {
            Def = def.Id,
            Thing = def.Things[town.Rng.Range(0, def.Things.Count)],
            SiteId = site.Def.Id,
            StartDay = town.Day,
            DueDay = town.Day + town.Rng.Range(r.Days[0], r.Days[1] + 1),
        };
        st.Current = q;
        if (site.Known == SiteKnowledge.Unheard || site.Trust < r.Trust) Rumors.Hear(town, site, Rumors.Occupants(town.Content, site.Def), site.Def.Tier, r.Trust);
        town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Describe(town, q)) + $" Clear {site.Def.Name} within {q.DueDay - town.Day} days and it will not have it.");
    }

    /// <summary>
    /// A site out in the wilds nobody holds, about as hard as the town's champions less a tier, nearest the
    /// big bad's own seat: the prize is somewhere its servants can reach and the town's heroes too.
    /// </summary>
    private static SiteState? SiteFor(Town town, Menace m)
    {
        var held = Menaces.Alive(town).SelectMany(x => x.Holdings.Append(x.SeatId)).ToHashSet();
        int tier = Math.Max(1, Saga.ChampionLevel(town) - Rules(town).Below);
        var home = Quests.Site(town, m.SeatId) is { RegionId: int rid } ? town.Regions[rid] : null;
        return town.Sites
            .Where(s => !s.Cleared && !held.Contains(s.Def.Id) && !Undercity.IsUnder(town, s) && s.RegionId != null && s.Def.Tier >= 1)
            .OrderBy(s => Math.Abs(s.Def.Tier - tier))
            .ThenBy(s => home == null ? 0 : World.AngleBetween(town.Regions[s.RegionId!.Value].Bearing, home.Bearing))
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static void Foil(Town town, Menace m, DarkQuest q, SiteState? site)
    {
        var st = State(town);
        var def = Def(town, q.Def);
        st.Current = null;
        q.Foiled = true;
        st.Past.Add(q);
        st.NextDay = town.Day + town.Rng.Range(Rules(town).Every[0], Rules(town).Every[1] + 1);
        m.Power = Math.Max(0, m.Power * (1 - Rules(town).FoilPower));
        if (def != null) town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Fill(town, def.Foiled, m, q)));
        if (site != null && Plots.RelicFor(town, site) is { } item) Watch.ReceiveRelic(town, item, $"heroes who cleared {site.Def.Name}");
    }

    private static void Finish(Town town, Menace m, DarkQuest q)
    {
        var st = State(town);
        var def = Def(town, q.Def);
        st.Current = null;
        st.Past.Add(q);
        st.NextDay = town.Day + town.Rng.Range(Rules(town).Every[0], Rules(town).Every[1] + 1);
        if (def == null) return;
        var g = def.Gain;
        st.Host += g.Host;
        st.Growth += g.Growth;
        st.Escapes += g.Escapes;
        if (g.Dread > 0) Saga.Scar(town, g.Dread);
        for (int i = 0; i < g.Sway; i++) Sway(town, m);
        for (int i = 0; i < g.Pact; i++) Powers.Bind(town, m);
        town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Fill(town, def.Done, m, q)));
    }

    /// <summary>The settlement nearest its seat that isn't already its own turns against the town.</summary>
    private static void Sway(Town town, Menace m)
    {
        var home = Quests.Site(town, m.SeatId) is { RegionId: int rid } ? town.Regions[rid] : null;
        var s = town.Settlements
            .Where(x => !x.Razed && !Shadows.Shadowed(x) && x.Standing > -50)
            .OrderBy(x => home == null ? 0 : Math.Abs(town.Regions[x.RegionId].Ring - home.Ring))
            .ThenBy(x => home == null ? 0 : World.AngleBetween(town.Regions[x.RegionId].Bearing, home.Bearing))
            .ThenBy(x => x.Id)
            .FirstOrDefault();
        if (s == null) return;
        m.Domain++;
        s.Allied = false;
        s.Standing = Math.Max(-100, Math.Min(s.Standing, 0) - town.Content.Saga.Menaces.SettlementHit);
    }

    private static string Fill(Town town, string text, Menace m, DarkQuest q) =>
        Saga.Fill(town, text.Replace("{thing}", q.Thing).Replace("{site}", Quests.Site(town, q.SiteId)?.Def.Name ?? "the wilds"), m);
}
