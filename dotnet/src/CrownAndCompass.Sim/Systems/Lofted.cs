namespace CrownAndCompass.Sim.Systems;

/// <summary>A Skywright relic: one of the pieces of lost arcane craft the Lofted City needs, lying at a far site.</summary>
public sealed record RelicDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";

    /// <summary>World themes (world.json) whose far regions may hold it, best first.</summary>
    public List<string> Themes { get; init; } = [];
}

/// <summary>One size of Lofted City: its grand work in buildings.json and what it does once it flies.</summary>
public sealed record LoftedScopeDef
{
    public string Building { get; init; } = "";

    /// <summary>Lore a day its keelstone-wrights add to the library.</summary>
    public double Lore { get; init; }

    /// <summary>Days of flight for each day of road when it sails.</summary>
    public double Speed { get; init; } = 0.6;

    /// <summary>Gold a day while it sails.</summary>
    public double SailGold { get; init; } = 60;
}

/// <summary>A foe racing the town for the relics (lofted.json "race").</summary>
public sealed record LoftedRace
{
    /// <summary>Days after the rival hears of the relics before it takes its first one, then between takings.</summary>
    public int FirstAfter { get; init; } = 30;
    public int ClaimEvery { get; init; } = 45;

    /// <summary>Relics the rival needs to start raising its own dark keel, the days it takes, and the share it adds to its power.</summary>
    public int RaiseAt { get; init; } = 3;
    public int RaiseDays { get; init; } = 90;
    public double Power { get; init; } = 0.5;
}

/// <summary>The Lofted City (content/game/lofted.json, <see cref="Lofted"/>).</summary>
public sealed record LoftedRules
{
    public List<RelicDef> Relics { get; init; } = [];

    /// <summary>The innermost ring of the wilds a relic can lie in.</summary>
    public int MinRing { get; init; } = 5;

    /// <summary>The discovery that tells the town where the relics are rumoured to lie, the one that confirms it, and the one that lets the city be built.</summary>
    public string Reveal { get; init; } = "";
    public string Confirm { get; init; } = "";
    public string Unlock { get; init; } = "";

    /// <summary>Lore the library gains for each relic brought home.</summary>
    public double RelicLore { get; init; } = 60;

    public LoftedRace Race { get; init; } = new();
    public List<LoftedScopeDef> Scopes { get; init; } = [];
    public Dictionary<string, List<string>> Texts { get; init; } = [];

    public LoftedScopeDef? Scope(string building) => Scopes.FirstOrDefault(s => s.Building == building);
}

/// <summary>Where a relic lies and who holds it: "" nobody yet, "town", or "menace:id".</summary>
public sealed class RelicState
{
    public string Id { get; init; } = "";
    public string SiteId { get; init; } = "";
    public string Holder { get; set; } = "";
    public int? Day { get; set; }
}

/// <summary>The relics, the foe racing for them, and where the Lofted City sails.</summary>
public sealed class LoftedState
{
    public List<RelicState> Relics { get; } = [];

    /// <summary>The menace racing the town for the relics, the day it takes its next, and when its dark keel rises (once it has enough).</summary>
    public int? RivalId { get; set; }
    public int NextClaimDay { get; set; }
    public int? RaiseDay { get; set; }
    public bool Raised { get; set; }

    /// <summary>The city's course in day units (x east, y south; 0,0 over the town): from, to, and the ticks it set out and arrives.</summary>
    public double FromX { get; set; }
    public double FromY { get; set; }
    public double ToX { get; set; }
    public double ToY { get; set; }
    public int DepartTick { get; set; }
    public int ArriveTick { get; set; }

    /// <summary>The region it is bound for or hovers over; null over the town.</summary>
    public int? RegionId { get; set; }
}

/// <summary>
/// The Lofted City: the end-game wonder. The Skywrights of Caldor raised cities on keelstones before the Long Night;
/// their relics lie at far sites. Companies bring them home (a relic site cleared), and they spark the ideas that end
/// in lofting, which opens the build: a grand work in one of three sizes (machinery stages, then the city's), not a
/// win but a great boost. Once it flies it can sail to a region and companies set out from under it. A foe that hears
/// of the relics races the town for them; with enough it raises a dark keel of its own, and breaking it gives back
/// what it took.
/// </summary>
public static class Lofted
{
    public static LoftedRules Rules(Town town) => town.Content.Lofted;

    public static RelicDef? Relic(Town town, string id) => Rules(town).Relics.FirstOrDefault(r => r.Id == id);

    /// <summary>The town's Lofted City, rising or flown, if any.</summary>
    public static Building? City(Town town) => town.Buildings.Where(b => b.Def.Lofted).OrderBy(b => b.Id).FirstOrDefault();

    /// <summary>The city stands finished and flies.</summary>
    public static bool Aloft(Town town) => City(town) is { Complete: true };

    public static int Held(Town town) => town.Lofted.Relics.Count(r => r.Holder == "town");

    public static bool Holds(Town town, string relic) => town.Lofted.Relics.Any(r => r.Id == relic && r.Holder == "town");

    /// <summary>The relic said to lie at this site, if any.</summary>
    public static RelicState? At(Town town, string siteId) => town.Lofted.Relics.FirstOrDefault(r => r.SiteId == siteId);

    public static Menace? Rival(Town town) => town.Lofted.RivalId is int id ? town.Saga.Menaces.FirstOrDefault(m => m.Id == id) : null;

    /// <summary>Who holds a relic, in words.</summary>
    public static string HolderName(Town town, RelicState r) =>
        r.Holder == "town" ? "the town" : r.Holder.StartsWith("menace:", StringComparison.Ordinal) && int.TryParse(r.Holder[7..], out int id)
            ? town.Saga.Menaces.FirstOrDefault(m => m.Id == id)?.Name ?? "a foe" : "";

    /// <summary>Why this building can't be raised yet, for the Lofted City's sizes; null when it can (or isn't one).</summary>
    public static string? Locked(Town town, BuildingDef d)
    {
        if (!d.Lofted) return null;
        var r = Rules(town);
        if (r.Unlock != "" && !Research.IsKnown(town, r.Unlock)) return $"needs the idea of {Research.Def(town, r.Unlock).Name.ToLowerInvariant()}";
        if (City(town) is { } c && c.Def.Id != d.Id) return "the town raises one Lofted City";
        return null;
    }

    /// <summary>Lore a day from the city's keelstone-wrights.</summary>
    public static double Lore(Town town) => Aloft(town) ? Rules(town).Scope(City(town)!.Def.Id)?.Lore ?? 0 : 0;

    // ---- Daily ----

    public static void Daily(Town town)
    {
        var r = Rules(town);
        if (r.Relics.Count == 0) return;
        Place(town);
        Gather(town);
        Reveal(town);
        Race(town);
        Sail(town);
    }

    /// <summary>Once: each relic lies at a far site of one of its themes, picked without dice (the farthest, by id).</summary>
    private static void Place(Town town)
    {
        var st = town.Lofted;
        if (st.Relics.Count > 0 || town.Sites.Count == 0) return;
        var r = Rules(town);
        var used = new HashSet<string>();
        var far = town.Sites
            .Where(s => !s.Cleared && World.RegionOf(town, s) is { } g && g.Ring >= r.MinRing)
            .OrderByDescending(s => s.Def.Distance).ThenBy(s => s.Def.Id, StringComparer.Ordinal).ToList();
        if (far.Count == 0)
            far = town.Sites.Where(s => !s.Cleared && s.Def.Distance > 0).OrderByDescending(s => s.Def.Distance).ThenBy(s => s.Def.Id, StringComparer.Ordinal).ToList();
        foreach (var relic in r.Relics)
        {
            var site = relic.Themes.Select(th => far.FirstOrDefault(s => !used.Contains(s.Def.Id) && World.RegionOf(town, s)?.Theme.Id == th)).FirstOrDefault(s => s != null)
                       ?? far.FirstOrDefault(s => !used.Contains(s.Def.Id));
            if (site == null) return;
            used.Add(site.Def.Id);
            st.Relics.Add(new RelicState { Id = relic.Id, SiteId = site.Def.Id });
        }
    }

    /// <summary>A relic site cleared by the town's companies: the relic comes home to the library.</summary>
    private static void Gather(Town town)
    {
        foreach (var rs in town.Lofted.Relics.Where(x => x.Holder == ""))
        {
            var site = Quests.Site(town, rs.SiteId);
            if (site is not { Cleared: true }) continue;
            Take(town, rs, Fill(town, "found", rs, site.Def.Name));
        }
    }

    private static void Take(Town town, RelicState rs, string line)
    {
        rs.Holder = "town";
        rs.Day = town.Day;
        town.Log(LedgerCategory.Quest, Importance.Major, line);
        Research.Insight(town, Sparks.Relic, $"{Relic(town, rs.Id)?.Name ?? rs.Id} studied", Rules(town).RelicLore);
    }

    /// <summary>The Skywrights' histories tell where the relics lie (rumoured), and keelstone lore makes it certain.</summary>
    private static void Reveal(Town town)
    {
        var r = Rules(town);
        bool rumour = r.Reveal != "" && Research.IsKnown(town, r.Reveal);
        bool sure = r.Confirm != "" && Research.IsKnown(town, r.Confirm);
        if (!rumour && !sure) return;
        foreach (var rs in town.Lofted.Relics)
        {
            if (Quests.Site(town, rs.SiteId) is not { } site) continue;
            if (sure && site.Known != SiteKnowledge.Confirmed) Rumors.Confirm(town, site, "the keelstone-wrights");
            else if (site.Known == SiteKnowledge.Unheard) World.Tell(town, site, 70, "the Skywright histories");
        }
    }

    /// <summary>
    /// Once the town knows where the relics lie, so does its strongest foe: it takes one the town hasn't every so
    /// often, and with enough raises a dark keel that makes it far stronger. Breaking it returns what it took.
    /// </summary>
    private static void Race(Town town)
    {
        var r = Rules(town);
        var st = town.Lofted;
        // A broken foe's relics are found in its seat.
        foreach (var rs in st.Relics.Where(x => x.Holder.StartsWith("menace:", StringComparison.Ordinal)).ToList())
        {
            var m = int.TryParse(rs.Holder[7..], out int id) ? town.Saga.Menaces.FirstOrDefault(x => x.Id == id) : null;
            if (m is { Alive: true }) continue;
            Take(town, rs, Fill(town, "returned", rs, m?.Name ?? "the foe"));
        }
        if (r.Reveal == "" || !Research.IsKnown(town, r.Reveal) || st.Relics.All(x => x.Holder == "town")) return;
        var rival = Rival(town);
        if (rival is not { Alive: true })
        {
            st.RaiseDay = null;
            st.Raised = false;
            rival = Menaces.Alive(town).Where(m => Menaces.Hostile(town, m)).OrderByDescending(m => m.Power).ThenBy(m => m.Id).FirstOrDefault();
            st.RivalId = rival?.Id;
            if (rival == null) return;
            st.NextClaimDay = town.Day + r.Race.FirstAfter;
            rival.Named = true;
            town.Log(LedgerCategory.War, Importance.Major, Fill(town, "rival", null, rival.Name));
            return;
        }
        string holder = $"menace:{rival.Id}";
        if (town.Day >= st.NextClaimDay)
        {
            st.NextClaimDay = town.Day + r.Race.ClaimEvery;
            var free = st.Relics.FirstOrDefault(x => x.Holder == "" && Quests.Site(town, x.SiteId) is { Cleared: false });
            if (free != null)
            {
                free.Holder = holder;
                free.Day = town.Day;
                town.Log(LedgerCategory.War, Importance.Major, Fill(town, "claimed", free, rival.Name), []);
            }
        }
        int held = st.Relics.Count(x => x.Holder == holder);
        if (st.RaiseDay == null && held >= r.Race.RaiseAt)
        {
            st.RaiseDay = town.Day + r.Race.RaiseDays;
            town.Log(LedgerCategory.War, Importance.Major, Fill(town, "keel", null, rival.Name));
        }
        if (st.RaiseDay is int day && town.Day >= day && !st.Raised)
        {
            st.Raised = true;
            rival.Power *= 1 + r.Race.Power;
            town.Log(LedgerCategory.War, Importance.Major, Fill(town, "raised", null, rival.Name));
        }
    }

    // ---- Sailing ----

    /// <summary>Where the city is now, in day units from the town.</summary>
    public static (double X, double Y) Pos(Town town)
    {
        var st = town.Lofted;
        if (town.Tick >= st.ArriveTick || st.ArriveTick <= st.DepartTick) return (st.ToX, st.ToY);
        double u = (town.Tick - st.DepartTick) / (double)(st.ArriveTick - st.DepartTick);
        return (st.FromX + (st.ToX - st.FromX) * u, st.FromY + (st.ToY - st.FromY) * u);
    }

    /// <summary>Under way to somewhere.</summary>
    public static bool Sailing(Town town) => town.Tick < town.Lofted.ArriveTick;

    /// <summary>Hovering away from the town.</summary>
    public static bool Away(Town town) => Aloft(town) && (town.Lofted.ToX != 0 || town.Lofted.ToY != 0 || Sailing(town));

    /// <summary>A region's middle, in day units from the town.</summary>
    public static (double X, double Y) Spot(Region g)
    {
        double a = g.Bearing * Math.PI / 180;
        return (g.Days * Math.Sin(a), -g.Days * Math.Cos(a));
    }

    /// <summary>Why the city can't sail there now, or null.</summary>
    public static string? SailBlocked(Town town, int regionId)
    {
        if (!Aloft(town)) return "the Lofted City isn't flying yet";
        if (Sailing(town)) return "it is already under way";
        if (regionId >= town.Regions.Count) return "no such land";
        if (regionId >= 0 && !town.Regions[regionId].Known) return "the town doesn't know that land";
        if (regionId == (town.Lofted.RegionId ?? -1)) return "it is there already";
        return null;
    }

    /// <summary>Set sail for a region (or -1: home over the town). Days of flight cost gold each.</summary>
    public static bool SetSail(Town town, int regionId)
    {
        if (SailBlocked(town, regionId) != null) return false;
        var st = town.Lofted;
        var scope = Rules(town).Scope(City(town)!.Def.Id) ?? new LoftedScopeDef();
        var from = Pos(town);
        var to = regionId < 0 ? (0.0, 0.0) : Spot(town.Regions[regionId]);
        double dist = Math.Sqrt((to.Item1 - from.X) * (to.Item1 - from.X) + (to.Item2 - from.Y) * (to.Item2 - from.Y));
        // The same winds as the town's airships (Sky.FlightDays): sailing with the season's wind is quicker.
        double heading = (Math.Atan2(to.Item1 - from.X, -(to.Item2 - from.Y)) * 180 / Math.PI + 360) % 360;
        int days = Sky.FlightDays(town, dist, scope.Speed, heading);
        town.Book(TreasuryAccount.GrandWorks, -scope.SailGold * days);
        st.FromX = from.X;
        st.FromY = from.Y;
        (st.ToX, st.ToY) = to;
        st.DepartTick = town.Tick;
        st.ArriveTick = town.Tick + days * SimTime.HoursPerDay;
        st.RegionId = regionId < 0 ? null : regionId;
        string where = regionId < 0 ? $"home over {town.Name}" : $"for {town.Regions[regionId].Name}";
        town.Log(LedgerCategory.Town, Importance.Notable, $"{Cap(City(town)!.Name)} slips its anchors and sails {where}: {days} {(days == 1 ? "day" : "days")} aloft.");
        return true;
    }

    /// <summary>Daily: the city arrives, and what lies below it is seen.</summary>
    private static void Sail(Town town)
    {
        var st = town.Lofted;
        if (st.RegionId is not int id || id >= town.Regions.Count || Sailing(town) || !Aloft(town)) return;
        var g = town.Regions[id];
        if (!g.Visited)
        {
            g.Visited = true;
            town.Log(LedgerCategory.Town, Importance.Notable, $"{Cap(City(town)!.Name)} rides at anchor over {g.Name}. Its watch sees every road below.");
        }
        foreach (string sid in g.SiteIds)
            if (Quests.Site(town, sid) is { } s && (s.Known != SiteKnowledge.Confirmed || s.ThoughtCleared != s.Cleared)) Rumors.Confirm(town, s, "the Lofted City's watch");
    }

    /// <summary>Where a company bound for this spot sets out from: under the Lofted City, when that is nearer than the town.</summary>
    public static (double X, double Y) Start(Town town, (double X, double Y) to)
    {
        if (!Away(town)) return (0, 0);
        var p = Pos(town);
        double fromCity = Math.Sqrt((to.X - p.X) * (to.X - p.X) + (to.Y - p.Y) * (to.Y - p.Y));
        return fromCity < Math.Sqrt(to.X * to.X + to.Y * to.Y) ? p : (0, 0);
    }

    /// <summary>The city stands finished: it rises.</summary>
    public static void Risen(Town town, Building b)
    {
        town.Log(LedgerCategory.Town, Importance.Major, Fill(town, "risen", null, b.Name));
        town.FestivalDays = Math.Max(town.FestivalDays, 5);
    }

    private static string Fill(Town town, string key, RelicState? rs, string who)
    {
        var lines = Rules(town).Texts.TryGetValue(key, out var l) && l.Count > 0 ? l : ["{who}: {relic}."];
        string line = lines[(town.Day + (rs?.Id.Length ?? 0)) % lines.Count];
        var site = rs == null ? null : Quests.Site(town, rs.SiteId);
        return Cap(line.Replace("{who}", who, StringComparison.Ordinal)
            .Replace("{relic}", rs == null ? "" : Relic(town, rs.Id)?.Name ?? rs.Id, StringComparison.Ordinal)
            .Replace("{site}", site?.Def.Name ?? "", StringComparison.Ordinal)
            .Replace("{town}", town.Name, StringComparison.Ordinal));
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
