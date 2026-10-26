namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// A themed stretch of the wilds (mountains, deep forest, caverns, castle ruins...) in one ring around
/// the town. The client draws it as a block of territories at <see cref="Bearing"/>, <see cref="Days"/> out.
/// </summary>
public sealed class Region
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required ThemeDef Theme { get; init; }

    /// <summary>Ring index, 0 nearest the world's heart (<see cref="World.Heart"/>): the deeper the ring, the harder its sites.</summary>
    public int Ring { get; init; }

    /// <summary>Days' march from the town to its middle.</summary>
    public int Days { get; set; }

    /// <summary>Days out from the world's heart its ring lies; 0 in a world whose town stands at the heart (then <see cref="Days"/>).</summary>
    public int Out { get; set; }

    /// <summary>Party levels its sites are built for.</summary>
    public required int[] Levels { get; init; }

    /// <summary>Compass bearing from the world's heart in degrees (0 north, clockwise) and the arc it spans.</summary>
    public double Bearing { get; init; }
    public double Arc { get; init; }

    /// <summary>Heard of in the taverns: heroes may choose to go there.</summary>
    public bool Known { get; set; }

    /// <summary>A party has been there and described the terrain.</summary>
    public bool Visited { get; set; }
    public int? SettlementId { get; set; }
    public List<string> SiteIds { get; } = [];

    /// <summary>A town site not chosen at the founding (founding.json id), held by its settlement or by foes (<see cref="Founding"/>).</summary>
    public string? Site { get; set; }
}

/// <summary>A pocket of light out in the wilds: a village, a dwarf hold, an elf enclave.</summary>
public sealed class Settlement
{
    public int Id { get; init; }

    /// <summary>How much of the road here painted patrols keep, worked out once a day (<see cref="Wilds.Road"/>).</summary>
    [NotSaved] internal double RoadCover;
    [NotSaved] internal int RoadDay = -1;
    [NotSaved] internal int RoadStamp = -1;
    public required string Name { get; init; }
    public required PeopleDef Kin { get; init; }
    public int RegionId { get; init; }

    /// <summary>How its people feel about the town, -100 to 100.</summary>
    public double Standing { get; set; }
    public bool Allied { get; set; }

    /// <summary>Explorers or envoys have met its people: it shows on the town's map.</summary>
    public bool Met { get; set; }

    /// <summary>Burned, emptied or bent to a menace's will (<see cref="Menaces"/>): it pleads, trades and allies no more.</summary>
    public bool Razed { get; set; }
    public int Envoys { get; set; }
    public int NextPleaDay { get; set; }

    // Its own life (<see cref="Neighbours"/>) ---------------------------------------------------

    /// <summary>Who leads it; empty until the settlement is first looked at.</summary>
    public string Leader { get; set; } = "";

    /// <summary>How well it is doing and how safe it is, 0 to 100.</summary>
    public double Prosperity { get; set; } = 50;
    public double Security { get; set; } = 50;
    public SettlementTrouble? Trouble { get; set; }
    public int NextTroubleDay { get; set; }
    public LocalBand? Band { get; set; }

    /// <summary>Projects the town has paid for there, and the one being built and when it is done.</summary>
    public List<string> Projects { get; } = [];
    public string? Works { get; set; }
    public int WorksDoneDay { get; set; }

    /// <summary>The day it last sent relief to the town.</summary>
    public int ReliefDay { get; set; } = -999;

    /// <summary>Its ruler's temperament, age and reign (<see cref="Reigns"/>); null until first looked at.</summary>
    public Reign? Reign { get; set; }

    /// <summary>An enemy's shadow over it (<see cref="Shadows"/>), if one lies there.</summary>
    public SettlementShadow? Shadow { get; set; }

    /// <summary>"the dwarves of Ironhold".</summary>
    public string People => Kin.Name.Replace("{S}", Name);
}

public enum PleaStatus
{
    Open,
    Taken,
    Answered,
    Lapsed,
}

/// <summary>A settlement asking for help with a site near it; it pays the heroes who clear it.</summary>
public sealed class Plea
{
    public int Id { get; init; }
    public int SettlementId { get; init; }
    public required string SiteId { get; init; }
    public double Reward { get; init; }
    public int ExpiresDay { get; init; }
    public PleaStatus Status { get; set; } = PleaStatus.Open;
    public int? PartyId { get; set; }
}

/// <summary>
/// The wilds beyond the town: rings of themed regions, each with adventure sites built for a band of
/// party levels, and the peoples who live there. The map is generated once from the seed (its own
/// stream, so the town's dice are untouched by how big the world is); after that, regions become
/// known through returning parties and allied maps, settlements plead for help, and peoples whose
/// standing rises high enough ally with the town and send it goods and heroes.
/// </summary>
public static class World
{
    public static Region? RegionOf(Town town, SiteState site) => site.RegionId is int id ? town.Regions[id] : null;

    public static Settlement? SettlementIn(Town town, Region r) => r.SettlementId is int id ? town.Settlements[id] : null;

    /// <summary>
    /// Whether the site's region is within the town's reach of talk: taverns only turn up rumours of
    /// sites in regions someone has heard of. Sites outside any region always are.
    /// </summary>
    public static bool RegionKnown(Town town, SiteState site) => RegionOf(town, site)?.Known ?? true;

    /// <summary>The town learns of a site from people who should know; a better source replaces a worse one.</summary>
    public static void Tell(Town town, SiteState site, int trust, string from)
    {
        if (site.Known == SiteKnowledge.Confirmed || (site.Known == SiteKnowledge.Rumoured && site.Trust >= trust)) return;
        Rumors.Hear(town, site, Rumors.Occupants(town.Content, site.Def), site.Def.Tier, trust);
        site.HeardFrom = from;
    }

    /// <summary>A fixed hash to [0, 1) for placing things; never the town's dice. The client's Geo.Hash is the same function.</summary>
    public static double Hash(int n)
    {
        uint x = unchecked((uint)n * 0x45d9f3bu);
        x = unchecked(((x >> 16) ^ x) * 0x45d9f3bu);
        x = (x >> 16) ^ x;
        return x / 4294967296.0;
    }

    /// <summary>
    /// Where a site lies from the world's heart: its compass bearing (degrees, 0 north, clockwise) and how far out it is in
    /// days of open-ground march. A region spreads its sites across its arc; sites outside any region (the war camp) sit
    /// between the hand-written ones.
    /// </summary>
    public static (double Bearing, double Reach) Place(Town town, SiteState s)
    {
        // The deeps lie under the site they open from.
        if (s.Def.Under != "" && Quests.Site(town, s.Def.Under) is { } up && up != s) return Place(town, up);
        int i = town.Sites.IndexOf(s);
        double reach = Out(s) * (0.82 + Hash(i * 7 + 3) * 0.3);
        if (RegionOf(town, s) is { } region)
        {
            int k = region.SiteIds.IndexOf(s.Def.Id);
            int n = region.SiteIds.Count;
            double bearing = region.Bearing + (k - (n - 1) / 2.0) * region.Arc * 0.6 / Math.Max(1, n);
            return (bearing, reach + (k % 2) * 0.14);
        }
        int baseCount = Math.Max(1, town.Content.Sites.Count);
        double slot = i < baseCount ? i : i - baseCount + 0.5;
        return (slot / baseCount * 360 + 20.05, reach);
    }

    /// <summary>The site's spot in day units from the town: x east, y south.</summary>
    public static (double X, double Y) Spot(Town town, SiteState s)
    {
        var (b, r) = Place(town, s);
        return AtHeart(town, b, r);
    }

    /// <summary>The world's heart, in day units from the town (<see cref="Town.HeartX"/>).</summary>
    public static (double X, double Y) Heart(Town town) => (town.HeartX, town.HeartY);

    /// <summary>A spot on this bearing (degrees from north) this many days out from the world's heart, in day units from the town.</summary>
    public static (double X, double Y) AtHeart(Town town, double bearing, double reach)
    {
        double a = bearing * Math.PI / 180;
        return (town.HeartX + reach * Math.Sin(a), town.HeartY - reach * Math.Cos(a));
    }

    /// <summary>A spot in day units from the town, as days out from the world's heart and the bearing it lies on from there.</summary>
    public static (double Reach, double Bearing) FromHeart(Town town, double x, double y)
    {
        double dx = x - town.HeartX, dy = y - town.HeartY;
        return (Js.Hypot(dx, dy), (Math.Atan2(dx, -dy) * 180 / Math.PI + 360) % 360);
    }

    /// <summary>Days out from the world's heart a region's ring lies.</summary>
    public static int Out(Region r) => r.Out > 0 ? r.Out : r.Days;

    /// <summary>Days out from the world's heart a site lies, before its own spread.</summary>
    public static int Out(SiteState s) => s.Out > 0 ? s.Out : s.Def.Distance;

    /// <summary>A region's middle, in day units from the town.</summary>
    public static (double X, double Y) Middle(Town town, Region r) => AtHeart(town, r.Bearing, Out(r));

    /// <summary>Experience for clearing a site: the encounter budget of its level.</summary>
    public static int Milestone(Town town, SiteState site)
    {
        var b = town.Content.World.Budget;
        return b.Length == 0 ? 0 : b[Math.Min(LevelOf(site), b.Length) - 1];
    }

    /// <summary>Party level a site is built for, read from its tier.</summary>
    public static int LevelOf(SiteState site) => Math.Max(1, site.Def.Tier);

    // ---- Generation ----

    /// <summary>The world for a new town, founded at <paramref name="homeX"/>, <paramref name="homeY"/> days from the heart (<see cref="Founding.Home"/>).</summary>
    public static void Generate(Town town, double? homeX = null, double? homeY = null)
    {
        var w = town.Content.World;
        if (w.Rings.Count == 0 || w.Themes.Count == 0) return;
        var rng = new Rng(town.Seed * 7919 + 104729);
        var used = new HashSet<string>(town.Sites.Select(s => s.Def.Name));
        PlaceOcean(town);

        for (int ring = 0; ring < w.Rings.Count; ring++)
        {
            var rd = w.Rings[ring];
            var inRing = w.Themes.Where(t => t.MinRing <= ring && ring <= t.MaxRing).ToList();
            double arc = 360.0 / rd.Regions;
            double offset = rng.Next() * 360;
            var picked = new List<ThemeDef>();
            for (int i = 0; i < rd.Regions; i++)
            {
                // The lands follow the climate: the region's spot on the map decides how hot and wet it is.
                double at = (offset + i * arc) % 360;
                // The ocean's regions are open water and islands; the rest of the world is land.
                bool sea = InOcean(town, ring, at);
                var allowed = inRing.Where(t => t.Sea == sea).ToList();
                if (allowed.Count == 0) allowed = inRing.Where(t => !t.Sea).ToList();
                // Each ring shows as many different kinds of country as it can before repeating one.
                var fresh = allowed.Where(t => !picked.Contains(t)).ToList();
                var (heat, wet) = Climate(town, ring, at);
                // Its neighbours so far: the region before it in the ring (and the first, closing the ring), and those inward of it.
                var beside = town.Regions.Where(r => r.Ring == ring - 1 && Math.Abs(AngleBetween(r.Bearing, at)) < (r.Arc + arc) / 2).Select(r => r.Theme).ToList();
                if (picked.Count > 0) beside.Add(picked[^1]);
                if (i == rd.Regions - 1 && picked.Count > 0) beside.Add(picked[0]);
                var again = allowed.Where(t => picked.Count == 0 || t != picked[^1]).ToList();
                var pool = Fitting(w.Climate, fresh.Count > 0 ? fresh : again.Count > 0 ? again : allowed, heat, beside);
                var theme = ClimateTheme(rng, w.Climate, pool, heat, wet);
                picked.Add(theme);
                double bearing = (offset + i * arc + (rng.Next() - 0.5) * 0.3 * arc) % 360;
                var region = new Region
                {
                    Id = town.Regions.Count,
                    Name = Fill(rng, theme, theme.Regions, used),
                    Theme = theme,
                    Ring = ring,
                    Days = rd.Days,
                    Levels = rd.Levels,
                    Bearing = Js.Round(bearing * 10) / 10.0,
                    Arc = Js.Round(arc * 10) / 10.0,
                    Known = ring == 0,
                };
                town.Regions.Add(region);
            }
        }

        PlaceAuthoredSites(town);

        // Each lair-holder rules one lair in the world while the library has others of the country left.
        var holders = new HashSet<string>(StringComparer.Ordinal);
        foreach (var region in town.Regions)
        {
            var rd = w.Rings[region.Ring];
            int count = Math.Max(1, rng.Range(w.SitesPerRegion[0], w.SitesPerRegion[1] + 1) - region.SiteIds.Count);
            for (int i = 0; i < count; i++)
            {
                int lo = rd.Levels[0];
                int hi = rd.Levels[^1];
                // Deeper into the region, harder sites: spread the ring's band across them.
                int level = count == 1 ? rng.Range(lo, hi + 1) : lo + (int)Math.Round((hi - lo) * (double)i / (count - 1));
                var kind = rng.Pick(region.Theme.Sites);
                var group = PickGroup(town.Content, rng, region.Theme, level);
                if (group != null) kind = new SiteKindDef { Names = group.Names, Habitats = group.Habitats, Deck = group.Deck };
                var def = BuildSite(town.Content, rng, region, kind, level, $"{region.Theme.Id}-{region.Id}-{i}", Fill(rng, region.Theme, kind.Names, used), holders, group);
                town.Sites.Add(new SiteState(def) { RegionId = region.Id, Generated = true });
                region.SiteIds.Add(def.Id);
            }

            double chance = region.Theme.SettlementChance ?? w.SettlementChance;
            if (region.Theme.Peoples.Count > 0 && rng.Chance(chance))
            {
                // The first people named is the likeliest to live there.
                var fid = rng.Chance(0.6) ? region.Theme.Peoples[0] : rng.Pick(region.Theme.Peoples);
                var people = w.Peoples.First(f => f.Id == fid);
                var s = new Settlement
                {
                    Id = town.Settlements.Count,
                    Name = Fill(rng, region.Theme, people.Settlements, used),
                    Kin = people,
                    RegionId = region.Id,
                    Standing = people.Start,
                    NextPleaDay = 2 + rng.Range(w.Standing.PleaEveryDays[0], w.Standing.PleaEveryDays[1] + 1),
                };
                town.Settlements.Add(s);
                region.SettlementId = s.Id;
            }
        }

        IsleCity(town, rng, used);
        // The sea goes in before the hard country, so no mountains or marsh stand in open water.
        SeaFeatures(town);
        // Last, so the country between the sites never shifts the regions and sites a seed makes.
        Roads.Generate(town, rng);
        Bridges.Span(town);
        PlaceFoes(town, used);
        PlaceIsles(town, used);
        SkyPeoples(town, used);
        Ruins.Place(town, used, Fill);
        Wayfaring.Place(town);
        Founding.Place(town, homeX, homeY);
    }

    /// <summary>
    /// Where the ocean lies (world.json "ocean"): a wedge of the map from <see cref="OceanRules.FromRing"/> out, its bearing
    /// and width rolled on its own dice so the rest of the world a seed makes keeps its draws.
    /// </summary>
    private static void PlaceOcean(Town town)
    {
        var o = town.Content.World.Ocean;
        if (o.Arc.Length == 0) return;
        var rng = new Rng(town.Seed * 3571 + 9973);
        town.OceanBearing = Js.Round(rng.Next() * 3600) / 10.0;
        town.OceanArc = Js.Round((o.Arc[0] + rng.Next() * (o.Arc[^1] - o.Arc[0])) * 10) / 10.0;
    }

    /// <summary>Whether a region of this ring centred on this bearing is open sea.</summary>
    public static bool InOcean(Town town, int ring, double bearing) =>
        town.OceanArc > 0 && ring >= town.Content.World.Ocean.FromRing && Math.Abs(AngleBetween(bearing, town.OceanBearing)) <= town.OceanArc / 2;

    /// <summary>Whether the region is open sea.</summary>
    public static bool Sea(Region region) => region.Theme.Sea;

    /// <summary>
    /// The great island city: a settlement of world.json's isle people in the sea region of its rings nearest the ocean's
    /// middle, rich from the start and growing (<see cref="PeopleDef.Grows"/>). It takes the place of any settlement there.
    /// </summary>
    /// <summary>
    /// An isle without a band foe may go to a group of the sky (groups.json `bands`): its site is built again around the
    /// group's foes and bosses. The first isle a group with a harbour takes in a world is that harbour, hidden.
    /// </summary>
    private static void SkyGroup(Town town, Rng rng, SkyBandDef band, SiteState site, HashSet<string> used)
    {
        if (band.Groups <= 0 || !rng.Chance(band.Groups)) return;
        int level = site.Def.Tier;
        var fits = town.Content.Groups.Groups.Where(g => g.Bands.Contains(band.Id) && GroupBosses(town.Content, g, level).Count > 0).ToList();
        if (fits.Count == 0) return;
        var g = rng.Pick(fits);
        var region = town.Regions[site.RegionId!.Value];
        bool harbour = g.Harbour.Count > 0 && !town.Sites.Any(s => s.Def.Group == g.Id);
        var kind = new SiteKindDef { Names = harbour ? g.Harbour : g.Names, Habitats = g.Habitats, Deck = g.Deck };
        used.Remove(site.Def.Name);
        var def = BuildSite(town.Content, rng, region, kind, level, site.Def.Id, Fill(rng, region.Theme, kind.Names, used), null, g);
        site.Def = def with { Aloft = true, Band = band.Id, Loot = [(int)Math.Round(def.Loot[0] * band.Hoard), (int)Math.Round(def.Loot[^1] * band.Hoard)] };
        site.Hidden = harbour;
    }

    /// <summary>
    /// The peoples of the sky (world.json isles `peoples`): the cloud court and the metallic ancients each settle a free land
    /// region of the far rings, their themes first, on their own dice so the world a seed makes keeps its draws. Added last, so
    /// no settlement's id moves.
    /// </summary>
    private static void SkyPeoples(Town town, HashSet<string> used)
    {
        var rng = new Rng(town.Seed * 7451 + 911);
        foreach (var c in town.Content.World.Isles.Peoples)
        {
            var people = town.Content.World.Peoples.FirstOrDefault(p => p.Id == c.People);
            if (people == null || rng.Next() >= c.Chance) continue;
            var pool = town.Regions
                .Where(r => !Sea(r) && r.SettlementId == null && r.Ring >= c.Rings[0] && r.Ring <= c.Rings[^1])
                .OrderBy(r => r.Id)
                .ToList();
            var best = pool.Where(r => c.Themes.Contains(r.Theme.Id)).ToList();
            if (best.Count > 0) pool = best;
            if (pool.Count == 0) continue;
            var region = pool[rng.Range(0, pool.Count)];
            var s = new Settlement
            {
                Id = town.Settlements.Count,
                Name = Fill(rng, region.Theme, people.Settlements, used),
                Kin = people,
                RegionId = region.Id,
                Standing = people.Start,
                Prosperity = c.Prosperity,
                NextPleaDay = 2 + rng.Range(town.Content.World.Standing.PleaEveryDays[0], town.Content.World.Standing.PleaEveryDays[1] + 1),
            };
            town.Settlements.Add(s);
            region.SettlementId = s.Id;
        }
    }

    private static void IsleCity(Town town, Rng rng, HashSet<string> used)
    {
        var c = town.Content.World.Ocean.City;
        var people = town.Content.World.Peoples.FirstOrDefault(p => p.Id == c.People);
        if (people == null || town.OceanArc <= 0) return;
        var region = town.Regions
            .Where(r => Sea(r) && r.Ring >= c.Rings[0] && r.Ring <= c.Rings[^1])
            .OrderBy(r => Math.Abs(AngleBetween(r.Bearing, town.OceanBearing)))
            .ThenBy(r => r.Ring)
            .ThenBy(r => r.Id)
            .FirstOrDefault();
        if (region == null) return;
        var s = new Settlement
        {
            Id = region.SettlementId ?? town.Settlements.Count,
            Name = Fill(rng, region.Theme, people.Settlements, used),
            Kin = people,
            RegionId = region.Id,
            Standing = people.Start,
            Prosperity = c.Prosperity,
            NextPleaDay = 2 + rng.Range(town.Content.World.Standing.PleaEveryDays[0], town.Content.World.Standing.PleaEveryDays[1] + 1),
        };
        if (region.SettlementId is int id) town.Settlements[id] = s;
        else town.Settlements.Add(s);
        region.SettlementId = s.Id;
    }

    /// <summary>
    /// The ocean's water on the map: one stretch of open sea per ring, over that ring's sea regions, from the gap before
    /// the ring to the gap after it. Its sites are islands, wrecks and sea caves in it; the boats take parties over.
    /// </summary>
    private static void SeaFeatures(Town town)
    {
        var kind = town.Content.Roads.Kinds.FirstOrDefault(k => k.Id == "ocean");
        var rings = town.Content.World.Rings;
        if (kind == null || town.OceanArc <= 0) return;
        var rng = new Rng(town.Seed * 3571 + 4441);
        string name = (kind.Names.Count > 0 ? rng.Pick(kind.Names) : "the {A} Sea").Replace("{A}", kind.Words.Count > 0 ? rng.Pick(kind.Words) : "Grey", StringComparison.Ordinal);
        for (int ring = 0; ring < rings.Count; ring++)
        {
            var seas = town.Regions.Where(r => r.Ring == ring && Sea(r)).OrderBy(r => r.Id).ToList();
            if (seas.Count == 0) continue;
            // The sea regions of a ring lie side by side: from the westmost's edge round to the eastmost's, about the ocean's middle.
            double from = seas.Min(r => Turn(r.Bearing, town.OceanBearing) - r.Arc / 2) + town.OceanBearing;
            double to = seas.Max(r => Turn(r.Bearing, town.OceanBearing) + r.Arc / 2) + town.OceanBearing;
            // Each ring's water runs a little into the next ring's, so the open sea has no seams between them.
            bool seaIn = town.Regions.Any(r => r.Ring == ring - 1 && Sea(r)), seaOut = town.Regions.Any(r => r.Ring == ring + 1 && Sea(r));
            double inner = (ring == 0 ? 0.3 : Gap(rings, ring - 1)) - (seaIn ? 0.4 : 0);
            double outer = (ring + 1 < rings.Count ? Gap(rings, ring) : rings[ring].Days * 1.25 + 2.5) + (seaOut ? 0.4 : 0);
            town.Terrain.Add(new Feature
            {
                Id = town.Terrain.Count,
                Name = name,
                Kind = kind,
                RegionId = seas[seas.Count / 2].Id,
                Tier = seas.Max(r => r.Levels[0]),
                Bearing = Js.Round(((from + to) / 2 + 360) % 360 * 10) / 10.0,
                Arc = Js.Round((to - from) * 10) / 10.0,
                Inner = Js.Round(inner * 100) / 100.0,
                Outer = Js.Round(outer * 100) / 100.0,
            });
        }
    }

    /// <summary>Degrees from b round to a, -180 to 180.</summary>
    private static double Turn(double a, double b) => ((a - b) % 360 + 540) % 360 - 180;

    /// <summary>Days from the world's heart a ring's country runs from and to: the gaps either side of its sites.</summary>
    public static (double Inner, double Outer) Band(Town town, int ring)
    {
        var rings = town.Content.World.Rings;
        return (ring == 0 ? 0 : Gap(rings, ring - 1), ring + 1 < rings.Count ? Gap(rings, ring) : rings[ring].Days * 1.25 + 2.5);
    }

    /// <summary>Days out to the middle of the gap between a ring's sites and the next ring's.</summary>
    private static double Gap(IReadOnlyList<RingDef> rings, int ring) => (rings[ring].Days * 1.12 + 0.14 + rings[ring + 1].Days * 0.82) / 2;

    /// <summary>
    /// The sky's heights (<see cref="Reaches"/>): band by band, a few far sites have torn loose and drift above the clouds,
    /// and only a company that can fly (where the band lets fliers in) or an airship that climbs high enough gets there.
    /// A band's own foe takes its sites, each with its own master. On their own dice, so nothing else shifts.
    /// </summary>
    private static void PlaceIsles(Town town, HashSet<string> used)
    {
        var rng = new Rng(town.Seed * 6007 + 4241);
        foreach (var band in town.Content.World.Isles.Bands)
        {
            var foe = town.Content.Foes.Of(band.Foe);
            var pool = town.Sites
                .Where(s => s.Generated && !s.Def.Aloft && s.Def.Foe == "" && s.Def.Group == "" && s.RegionId is int rid && !town.Regions[rid].Theme.Sea
                    && town.Regions[rid].Ring >= band.Rings[0] && town.Regions[rid].Ring <= band.Rings[^1]
                    && (band.Themes.Count == 0 || band.Themes.Contains(town.Regions[rid].Theme.Id)))
                .OrderBy(s => s.Def.Id, StringComparer.Ordinal)
                .ToList();
            for (int i = 0; i < band.Count && pool.Count > 0; i++)
            {
                var site = pool[rng.Range(0, pool.Count)];
                pool.Remove(site);
                used.Remove(site.Def.Name);
                string name = band.Names.Count > 0 ? Fill(rng, town.Regions[site.RegionId!.Value].Theme, band.Names, used) : site.Def.Name;
                site.Def = site.Def with { Aloft = true, Band = band.Id, Name = name, Loot = [(int)Math.Round(site.Def.Loot[0] * band.Hoard), (int)Math.Round(site.Def.Loot[^1] * band.Hoard)] };
                if (foe == null)
                {
                    SkyGroup(town, rng, band, site, used);
                    continue;
                }
                string boss = band.Bosses.Count > 0 ? band.Bosses[rng.Range(0, band.Bosses.Count)] : foe.Monster;
                if (town.Content.Library.Monster(boss) == null) continue;
                site.Def = site.Def with
                {
                    Kind = foe.Kind,
                    Foe = foe.Id,
                    Boss = new EncounterDef { Kind = "combat", Monster = town.Content.BossOf(boss).Id, Count = [1, 1], Text = foe.Text != "" ? foe.Text : site.Def.Boss.Text },
                };
            }
        }
    }

    /// <summary>
    /// Each signature foe takes over one site in country it likes, in its rings (on its own dice, so the rest of the
    /// world a seed makes is unchanged): its master and escort replace the library's pick, and the site its name. A site a
    /// ready-made group (groups.json) holds stays theirs.
    /// </summary>
    private static void PlaceFoes(Town town, HashSet<string> used)
    {
        var rng = new Rng(town.Seed * 4099 + 31337);
        var taken = new HashSet<string>();
        foreach (var f in town.Content.Foes.Foes)
        {
            if (town.Content.Library.Monster(f.Monster) is not { } boss) continue;
            // A foe a height of the sky seats is placed with its band (PlaceIsles).
            if (town.Content.World.Isles.Bands.Any(b => b.Foe == f.Id)) continue;
            var pool = town.Sites
                .Where(s => s.Generated && s.Def.Group == "" && !taken.Contains(s.Def.Id) && s.RegionId is int rid && town.Regions[rid].Ring >= f.Rings[0] && town.Regions[rid].Ring <= f.Rings[^1])
                .OrderBy(s => Math.Max(0, f.Themes.ToList().IndexOf(town.Regions[s.RegionId!.Value].Theme.Id)) + (f.Themes.Contains(town.Regions[s.RegionId!.Value].Theme.Id) ? 0 : 100))
                .ThenBy(s => Math.Abs(s.Def.Tier - Math.Max(1, (int)Math.Round(boss.Cr))))
                .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
                .Take(3)
                .ToList();
            if (pool.Count == 0) continue;
            var site = rng.Pick(pool);
            taken.Add(site.Def.Id);
            var region = town.Regions[site.RegionId!.Value];
            string name = f.Names.Count > 0 ? Fill(rng, region.Theme, f.Names, used) : site.Def.Name;
            used.Remove(site.Def.Name);
            site.Def = site.Def with
            {
                Name = name,
                Kind = f.Kind,
                Foe = f.Id,
                Aloft = f.Aloft,
                Band = f.Aloft ? f.Band : "",
                Boss = new EncounterDef
                {
                    Kind = "combat",
                    Monster = town.Content.BossOf(boss.Id).Id,
                    Count = [1, 1],
                    Escort = f.Escort != "" && town.Content.Library.Monster(f.Escort) != null ? f.Escort : null,
                    EscortCount = f.Escort != "" && f.EscortCount[^1] > 0 ? f.EscortCount : null,
                    Text = f.Text != "" ? f.Text : site.Def.Boss.Text,
                },
            };
        }
    }

    /// <summary>
    /// How hot and wet the land is at a bearing in a ring: each seed turns a warm side and a wet side of the map, and
    /// the far rings feel it more than the town's own. Hash-made, so it moves none of the world's dice.
    /// </summary>
    public static (double Heat, double Wet) Climate(Town town, int ring, double bearing)
    {
        var c = town.Content.World.Climate;
        double warm = Warm(town);
        double wetSide = (warm + 90 + Voices.Hash("climate-wet", town.Seed) % 180) % 360;
        double amp = c.Amp.Length == 0 ? 1 : c.Amp[Math.Min(ring, c.Amp.Length - 1)];
        return (amp * Math.Cos((bearing - warm) * Math.PI / 180), amp * Math.Cos((bearing - wetSide) * Math.PI / 180));
    }

    /// <summary>The bearing from the heart of the world's warm side.</summary>
    public static double Warm(Town town) => Voices.Hash("climate-warm", town.Seed) % 360;

    /// <summary>The themes a region of this heat may hold: never jungle beside the tundra, unless nothing else is left.</summary>
    private static List<ThemeDef> Fitting(ClimateRules c, List<ThemeDef> themes, double heat, List<ThemeDef> beside)
    {
        bool Clashes(ThemeDef t) => t.Heat is double h && beside.Any(b => b.Heat is double bh && Math.Abs(h - bh) >= c.Clash);
        var fit = themes.Where(t => !Clashes(t) && (t.Heat is not double h || Math.Abs(h - heat) <= c.Hard)).ToList();
        if (fit.Count > 0) return fit;
        fit = themes.Where(t => !Clashes(t)).ToList();
        return fit.Count > 0 ? fit : themes;
    }

    /// <summary>A theme by weight, each weighed by how well it fits the region's climate.</summary>
    private static ThemeDef ClimateTheme(Rng rng, ClimateRules c, IReadOnlyList<ThemeDef> themes, double heat, double wet)
    {
        double Fit(ThemeDef t)
        {
            double d = (t.Heat is double h ? (h - heat) * (h - heat) : 0) + (t.Wet is double w ? c.WetShare * (w - wet) * (w - wet) : 0);
            return t.Weight * Math.Max(0.02, Math.Exp(-d / Math.Max(0.01, c.Spread)));
        }
        double total = themes.Sum(Fit);
        double roll = rng.Next() * total;
        foreach (var t in themes)
        {
            if (roll < Fit(t)) return t;
            roll -= Fit(t);
        }
        return themes[^1];
    }


    /// <summary>A fresh name from the patterns: {A} a theme word, {B} a theme name.</summary>
    private static string Fill(Rng rng, ThemeDef theme, IReadOnlyList<string> patterns, HashSet<string> used)
    {
        string name = "";
        for (int tries = 0; tries < 30; tries++)
        {
            name = rng.Pick(patterns).Replace("{A}", rng.Pick(theme.Words)).Replace("{B}", rng.Pick(theme.Names));
            if (used.Add(name)) return name;
        }
        for (int n = 2; ; n++)
            if (used.Add($"{name} {Roman(n)}")) return $"{name} {Roman(n)}";
    }

    private static string Roman(int n) => n switch { 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => n.ToString(System.Globalization.CultureInfo.InvariantCulture) };

    /// <summary>The hand-written sites go to the region whose country fits them best, as near their own distance as can be.</summary>
    private static void PlaceAuthoredSites(Town town)
    {
        foreach (var site in town.Sites)
        {
            int Fit(Region g) =>
                2 * g.Theme.Habitats.Concat(g.Theme.Sites.SelectMany(k => k.Habitats)).Distinct().Count(site.Def.Habitats.Contains) -
                3 * Math.Abs(g.Days - site.Def.Distance);
            var region = town.Regions
                .OrderByDescending(Fit)
                .ThenBy(g => g.SiteIds.Count)
                .ThenBy(g => g.Id)
                .First();
            site.RegionId = region.Id;
            region.SiteIds.Add(site.Def.Id);
        }
    }

    /// <summary>Build a site for parties of the given level from the library's creatures of its country.</summary>
    /// <summary>
    /// Now and then a site goes to a ready-made enemy group (groups.json) of the theme instead, when one of its
    /// bosses suits the level. Themes with no fitting group roll nothing.
    /// </summary>
    public static GroupDef? PickGroup(Content content, Rng rng, ThemeDef theme, int level)
    {
        var fits = content.Groups.Groups.Where(g => g.Bands.Count == 0 && g.Themes.Contains(theme.Id) && GroupBosses(content, g, level).Count > 0).ToList();
        if (fits.Count == 0 || !rng.Chance(content.Groups.Chance)) return null;
        return rng.Pick(fits);
    }

    /// <summary>A group's bosses about as strong as a party of this level.</summary>
    public static List<LibMonster> GroupBosses(Content content, GroupDef g, int level)
    {
        double lo = Math.Max(0.5, level * 0.75) - 1, hi = Math.Max(1, level) + 1;
        return content.Library.Monsters.Where(m => g.Bosses.Contains(m.Id) && m.Damage != null && m.Cr >= lo && m.Cr <= hi).ToList();
    }

    public static SiteDef BuildSite(Content content, Rng rng, Region region, SiteKindDef kind, int level, string id, string name, HashSet<string>? holders = null, GroupDef? group = null)
    {
        var w = content.World;
        var theme = region.Theme;
        double crLo = Math.Max(0.125, level / 8.0);
        double crHi = Math.Max(0.5, level * 2 / 3.0);
        int budget = w.Budget.Length == 0 ? 100 * level : w.Budget[Math.Min(level, w.Budget.Length) - 1];
        double hoard = w.HoardPerLevel * Math.Pow(level, 1.5);
        int[] Gold(double lo, double hi) => [(int)Math.Round(hoard * lo), (int)Math.Round(hoard * hi)];
        string Text(string k) =>
            group != null && group.Texts.TryGetValue(k, out var own) && own.Count > 0 ? rng.Pick(own)
            : theme.Texts.TryGetValue(k, out var lines) && lines.Count > 0 ? rng.Pick(lines) : k;

        var habitats = kind.Habitats.Count > 0 ? kind.Habitats.ToList() : theme.Habitats.ToList();
        var probe = new SiteDef { Id = id, Name = name, Boss = new EncounterDef { Kind = "combat" }, Habitats = habitats, Group = group?.Id ?? "" };
        if (content.Wanderers(probe, [crLo, crHi]).Count == 0)
        {
            habitats = habitats.Concat(theme.Habitats).Distinct().ToList();
            probe = probe with { Habitats = habitats };
        }

        int dc = 11 + (level + 1) / 2;
        int die = level <= 1 ? 6 : level <= 3 ? 8 : level <= 6 ? 10 : 12;
        var deck = new List<EncounterDef>();
        foreach (var k in kind.Deck)
        {
            deck.Add(k switch
            {
                "wander" => new EncounterDef { Kind = k, Cr = [crLo, crHi], Budget = budget, Text = Text(k) },
                "trap" or "hazard" => new EncounterDef { Kind = k, Dc = dc, DamageDie = die, Text = Text(k) },
                "puzzle" or "parley" => new EncounterDef { Kind = k, Dc = dc + 1, Gold = Gold(0.1, 0.25), Text = Text(k) },
                _ => new EncounterDef { Kind = "treasure", Gold = Gold(0.2, 0.5), MimicChance = Math.Min(0.25, 0.08 + 0.02 * level), Text = Text("treasure") },
            });
        }

        // The boss: a creature of this country about as strong as the party, with lesser ones at its side.
        double bLo = Math.Max(0.5, level * 0.75);
        double bHi = Math.Max(1, level);
        List<LibMonster> bosses = group != null ? GroupBosses(content, group, level) : [];
        for (int widen = 0; widen < 12 && bosses.Count == 0; widen++)
        {
            bosses = content.Library.Monsters
                .Where(m => m.Damage != null && m.Band != "" && !m.Alignment.Contains("good") && m.Cr >= bLo && m.Cr <= bHi && m.Habitats.Any(habitats.Contains))
                .ToList();
            bLo = Math.Max(0.125, bLo - 0.5);
            bHi += 1;
        }
        if (bosses.Count == 0)
            bosses = content.Library.Monsters.Where(m => m.Damage != null && m.Band != "" && !m.Alignment.Contains("good") && m.Cr >= 0.5 && m.Cr <= Math.Max(1, level)).ToList();
        // At the edge of the world the legends wait: the ancient dragons, the lich, the tarrasque may hold the last lairs.
        if (group == null && level >= content.World.LegendLevel && content.World.LegendLevel > 0)
            bosses = bosses.Concat(content.Library.Monsters.Where(m => m.Damage != null && m.Band != "" && !m.Alignment.Contains("good") && m.Cr > 20 && m.Habitats.Any(habitats.Contains))).ToList();
        // Lair-holders first: the library marks some creatures as bosses.
        var marked = bosses.Where(m => m.Boss).ToList();
        var from = marked.Count > 0 && rng.Chance(0.5) ? marked : bosses;
        // One that holds no other lair yet, when the country has one.
        if (holders != null && from.Where(m => !holders.Contains(m.Id)).ToList() is { Count: > 0 } fresh) from = fresh;
        var boss = rng.Pick(from);
        holders?.Add(boss.Id);
        var escorts = content.Wanderers(probe, [crLo, Math.Max(crLo, Math.Min(crHi, boss.Cr / 2))]).Where(m => m.Id != boss.Id).ToList();
        var bossEnc = new EncounterDef
        {
            Kind = "combat",
            Monster = content.BossOf(boss.Id).Id,
            Count = [1, 1],
            Escort = escorts.Count > 0 ? rng.Pick(escorts).Id : null,
            EscortCount = escorts.Count > 0 ? [1, 1 + level / 3] : null,
            Text = Text("boss"),
        };

        return new SiteDef
        {
            Id = id,
            Name = name,
            Tier = level,
            Kind = Lairs.KindOf(boss.Type, ""),
            Group = group?.Id ?? "",
            Distance = region.Days,
            Encounters = deck,
            Boss = bossEnc,
            Loot = Gold(0.7, 1.4),
            Habitats = habitats,
        };
    }

    // ---- The daily tick ----

    public static void Daily(Town town, bool autoMayor)
    {
        var w = town.Content.World;
        if (town.Regions.Count == 0) return;
        foreach (var s in town.Settlements)
        {
            // Feelings cool or warm back toward where they started.
            double d = s.Kin.Start + Reigns.Standing(town, s) - s.Standing;
            if (!s.Allied && !s.Razed) s.Standing += Math.Sign(d) * Math.Min(Math.Abs(d), w.Standing.Drift);
        }
        LapsePleas(town);
        PostPleas(town);
        Boons(town);
        if (autoMayor) AutoEnvoy(town);
    }

    private static void LapsePleas(Town town)
    {
        foreach (var p in town.Pleas)
        {
            if (p.Status != PleaStatus.Open || town.Day < p.ExpiresDay) continue;
            p.Status = PleaStatus.Lapsed;
            var s = town.Settlements[p.SettlementId];
            Shift(town, s, town.Content.World.Standing.PleaIgnored);
            town.Log(LedgerCategory.Quest, Importance.Routine, $"No one answers the plea from {s.Name} about {Quests.Site(town, p.SiteId)!.Def.Name}. {Cap(s.Kin.People)} will remember it.");
        }
    }

    private static void PostPleas(Town town)
    {
        var rules = town.Content.World.Standing;
        // Runners come only for trouble the town's heroes could face, a few at a time.
        int strongest = town.Heroes.Select(h => h.Hero!.Level).DefaultIfEmpty(0).Max();
        foreach (var s in town.Settlements)
        {
            var region = town.Regions[s.RegionId];
            if (!region.Known || s.Razed || town.Day < s.NextPleaDay) continue;
            if (town.Pleas.Count(p => p.Status is PleaStatus.Open or PleaStatus.Taken) >= rules.MaxOpen) break;
            s.NextPleaDay = town.Day + town.Rng.Range(rules.PleaEveryDays[0], rules.PleaEveryDays[1] + 1);
            if (town.Pleas.Any(p => p.SettlementId == s.Id && p.Status is PleaStatus.Open or PleaStatus.Taken)) continue;
            // The nearest trouble first: their own region, then the rest of their ring.
            var site = town.Sites
                .Where(x => !x.Cleared && LevelOf(x) <= strongest + 1 && x.RegionId is int rid && town.Regions[rid].Ring == region.Ring && town.Regions[rid].Known)
                .Where(x => !town.Pleas.Any(p => p.SiteId == x.Def.Id && p.Status is PleaStatus.Open or PleaStatus.Taken))
                .OrderBy(x => x.Def.Id == s.Trouble?.SiteId || (Shadows.Shadowed(s) && x.Def.Id == s.Shadow!.SourceId) ? 0 : 1)
                .ThenBy(x => x.RegionId == region.Id ? 0 : 1)
                .ThenBy(x => x.Def.Tier)
                .ThenBy(x => x.Def.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (site == null) continue;
            Plead(town, s, site, 1);
        }
    }

    /// <summary>A settlement begs the town's heroes for help with a site, offering a reward (more when its own band failed).</summary>
    /// <remarks>With <paramref name="exiles"/>, it is the settlement's refugees in the town's camp who ask, to retake their lost home: razed or not, ruler or none.</remarks>
    internal static Plea? Plead(Town town, Settlement s, SiteState site, double bonus, bool exiles = false)
    {
        var rules = town.Content.World.Standing;
        if (!exiles && (s.Razed || !Neighbours.Known(town, s) || Reigns.Plea(town, s) <= 0)) return null;
        if (town.Pleas.Any(p => p.SiteId == site.Def.Id && p.Status is PleaStatus.Open or PleaStatus.Taken)) return null;
        double reward = Js.Round(rules.PleaRewardPerLevel * LevelOf(site) * (s.Allied ? 1.5 : 1) * bonus * (exiles ? 1 : Reigns.Plea(town, s)));
        Tell(town, site, rules.PleaTrust, exiles ? $"the refugees from {s.Name}" : $"a runner from {s.Name}");
        var plea = new Plea
        {
            Id = town.Pleas.Count + 1,
            SettlementId = s.Id,
            SiteId = site.Def.Id,
            Reward = reward,
            ExpiresDay = town.Day + rules.PleaDays,
        };
        town.Pleas.Add(plea);
        town.Log(
            LedgerCategory.Quest,
            Importance.Notable,
            exiles
                ? $"In the taverns, the refugees from {s.Name} pool what they have: {Js.Str(reward)} gold to whoever takes {site.Def.Name} back so they can go home."
                : $"A runner from {s.Name} reaches the taverns: {s.People} beg for help with {site.Def.Name} and offer {Js.Str(reward)} gold to whoever deals with it.");
        return plea;
    }

    /// <summary>The peoples allied with the town send goods to market and, now and then, a hero.</summary>
    private static void Boons(Town town)
    {
        foreach (var s in town.Settlements)
        {
            // A people paying an enemy tribute has nothing left to send.
            if (!s.Allied || Shadows.Paying(s)) continue;
            var b = s.Kin.Boon;
            if (b.Good is Good g && b.Qty > 0) town.Market.Seed(g, b.Qty * Neighbours.BoonShare(town, s));
            if (b.HeroChance <= 0 || b.Classes.Count == 0 || s.Kin.Race == "") continue;
            if (town.Taverns.Count == 0 || town.Heroes.Count >= City.HeroCap(town) || !town.Rng.Chance(b.HeroChance)) continue;
            var cls = town.Content.Classes.FirstOrDefault(c => c.Id == town.Rng.Pick(b.Classes));
            if (cls == null) continue;
            var a = Quests.CreateHero(town, cls, town.Rng.Chance(0.6) ? 2 : 3, s.Kin.Race, s.Kin.Id);
            town.Log(LedgerCategory.Arrival, Importance.Notable, $"{a.Describe()}, comes from {s.Name} with a letter from {s.People}: they have heard the town needs blades. {Annals.Past(town, a)} ({a.TraitList()})", [a.Id]);
        }
    }

    /// <summary>Stand-in mayor: every so often, send gifts to the friendliest people not yet allied.</summary>
    private static void AutoEnvoy(Town town)
    {
        var rules = town.Content.World.Standing;
        if (town.Day - town.LastEnvoyDay < rules.EnvoyEveryDays || town.Treasury < rules.EnvoyCost + 200) return;
        var target = town.Settlements
            .Where(s => !s.Allied && !s.Razed && Neighbours.Known(town, s) && Reaches.Unreached(town, s) == null)
            .OrderByDescending(s => s.Standing)
            .ThenBy(s => s.Id)
            .FirstOrDefault();
        if (target != null) SendEnvoy(town, target.Id);
    }

    /// <summary>The player's lever: send an envoy with gifts to a settlement the town has heard of.</summary>
    public static bool SendEnvoy(Town town, int settlementId)
    {
        var rules = town.Content.World.Standing;
        if (settlementId < 0 || settlementId >= town.Settlements.Count) return false;
        var s = town.Settlements[settlementId];
        if (!Neighbours.Known(town, s) || s.Razed || town.Treasury < rules.EnvoyCost || Reaches.Unreached(town, s) != null) return false;
        if (Stewards.Intercept(town, "realm", new StewardAsk { Area = "realm", Kind = "envoy", Title = $"Send an envoy to {s.Name} · {Js.Str(rules.EnvoyCost)}g", Why = "• Gifts win standing\n• The friendliest people not yet allied", I1 = settlementId, Cost = rules.EnvoyCost })) return false;
        town.Book(TreasuryAccount.Envoys, -rules.EnvoyCost);
        town.LastEnvoyDay = town.Day;
        // Gifts please less each time.
        double gain = rules.Envoy / (1 + 0.25 * s.Envoys);
        s.Envoys++;
        town.Log(LedgerCategory.Town, Importance.Routine, $"The council sends an envoy with {Js.Str(rules.EnvoyCost)} gold of gifts to {s.People}.");
        Shift(town, s, gain + Sheet.Gain(town, "envoy", Sheet.Decide(town, "envoy")));
        Shadows.Envoy(town, s);
        return true;
    }

    /// <summary>Move a people's standing, and seal an alliance when it climbs high enough.</summary>
    public static void Shift(Town town, Settlement s, double delta)
    {
        if (s.Razed) return;
        // A diplomatic people warms faster to a good turn; a closed one is slower to.
        if (delta > 0) delta *= Character.Diplomacy(town, s);
        s.Standing = Math.Clamp(s.Standing + delta, -100, 100);
        if (s.Allied || s.Standing < town.Content.World.Standing.Ally) return;
        s.Allied = true;
        var b = s.Kin.Boon;
        town.Log(
            LedgerCategory.Town,
            Importance.Major,
            $"{Cap(s.People)} pledge friendship to {town.Name}." +
            (b.Text != "" ? $" From now on, {b.Text.Replace("{S}", s.Name)}" : "") +
            (b.Troops > 0 ? $"{(b.Text != "" ? ", and" : " They pledge")} their warriors will come if the walls are ever attacked." : "."));
        Annals.Allied(town, s);
        Character.Allied(town, s);
        if (b.Maps) Reveal(town, town.Regions[s.RegionId], $"{Cap(s.People)} share their maps", town.Content.World.Standing.MapTrust);
    }

    /// <summary>Defence the town's allies add at its walls.</summary>
    public static double AllyTroops(Town town)
    {
        double t = 0;
        foreach (var s in town.Settlements)
            if (s.Allied) t += s.Kin.Boon.Troops;
        return t + Neighbours.Troops(town);
    }

    // ---- Parties coming home ----

    /// <summary>
    /// A party is back: the region they went to is described, word of the lands beyond spreads, and the
    /// people living there remember who cleared their trouble. Returns gold a settlement pays the party.
    /// </summary>
    public static double AfterDelve(Town town, Party party, SiteState site, ExpeditionOutcome outcome)
    {
        Neighbours.AfterDelve(town, party, site, outcome);
        var plea = party.PleaId is int pid ? town.Pleas[pid - 1] : null;
        var region = RegionOf(town, site);
        if (region == null) return 0;
        double paid = 0;
        // The dead tell no tales: only a party that comes home describes the country, and only to the
        // council when it paid for the trip; anyone else must buy the report (Intel).
        if (outcome != ExpeditionOutcome.Wiped && !party.Withheld) Describe(town, party, region);
        if (outcome == ExpeditionOutcome.Cleared)
        {
            var rules = town.Content.World.Standing;
            var home = SettlementIn(town, region);
            if (home != null && (plea == null || plea.SettlementId != home.Id)) Shift(town, home, rules.Cleared);
            if (plea != null)
            {
                plea.Status = PleaStatus.Answered;
                var s = town.Settlements[plea.SettlementId];
                paid = plea.Reward;
                town.Log(LedgerCategory.Quest, Importance.Notable, $"{Cap(s.People)} pay the {party.Name} {Js.Str(plea.Reward)} gold for {site.Def.Name}, and toast {town.Name}'s heroes.");
                Shift(town, s, rules.Plea + rules.Cleared);
            }
        }
        else if (plea != null) Reopen(plea);
        return paid;
    }

    /// <summary>A party's report reaches the council: the region is described and word of the lands beyond spreads.</summary>
    public static void Describe(Town town, Party party, Region region)
    {
        bool first = !region.Visited;
        region.Visited = true;
        region.Known = true;
        // On the way they hear of the region's other sites from whoever lives there; empty country tells nothing.
        if (SettlementIn(town, region) is { Razed: false } home)
            foreach (var id in region.SiteIds) Tell(town, Quests.Site(town, id)!, town.Content.World.Standing.VisitTrust, home.People);
        if (first) Annals.Visited(town, party, region);
        if (first) Reveal(town, region, $"The {party.Name} bring back word of the lands beyond {region.Name}", 0);
    }

    /// <summary>The party that took a plea came back without clearing the site: it stands open again.</summary>
    public static void Reopen(Plea plea)
    {
        plea.Status = PleaStatus.Open;
        plea.PartyId = null;
    }

    /// <summary>Regions in the next ring out, around this one's bearing, become known by rumor.</summary>
    /// <param name="trust">When above 0, the regions' sites are heard of too, at this trust (an ally's maps).</param>
    private static void Reveal(Town town, Region from, string lead, int trust)
    {
        var news = town.Regions
            .Where(r => !r.Known && (r.Ring == from.Ring + 1 || r.Ring == from.Ring) && AngleBetween(r.Bearing, from.Bearing) <= from.Arc * 0.75 + r.Arc * 0.5)
            .ToList();
        if (news.Count == 0) return;
        foreach (var r in news)
        {
            r.Known = true;
            if (trust <= 0) continue;
            foreach (var id in r.SiteIds)
            {
                var site = Quests.Site(town, id)!;
                Tell(town, site, trust, lead);
                // An ally's map puts the place where it really is.
                site.Found = true;
                site.FoundTick ??= town.Tick;
            }
        }
        string lands = Expedition.ListNames(news.Select(r =>
        {
            var s = SettlementIn(town, r);
            return $"{r.Name} ({r.Theme.Name}{(s != null ? $", home to {s.People}" : "")})";
        }).ToList());
        town.Log(LedgerCategory.Quest, Importance.Notable, $"{lead}: {lands}.");
    }

    public static double AngleBetween(double a, double b)
    {
        double d = Math.Abs(a - b) % 360;
        return d > 180 ? 360 - d : d;
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
