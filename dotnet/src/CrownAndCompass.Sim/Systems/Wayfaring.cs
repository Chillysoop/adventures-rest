namespace CrownAndCompass.Sim.Systems;

/// <summary>One job a hero takes on the road (roads.json <c>wayfaring.jobs</c>).</summary>
public sealed record JobDef
{
    /// <summary>The skill it rolls (stories.json <c>skills</c>).</summary>
    public string Skill { get; init; } = "survival";
    public string Name { get; init; } = "";
    public string Verb { get; init; } = "";
    public int Dc { get; init; } = 10;

    /// <summary>DC added per level of the country.</summary>
    public double PerTier { get; init; }

    /// <summary>Medic: hit points a treated short rest gives back, plus the medic's Wisdom.</summary>
    public int Heal { get; init; }

    /// <summary>Forager: DC added in country with little to eat.</summary>
    public int Harsh { get; init; }
    public IReadOnlyList<string> HarshHabitats { get; init; } = [];

    /// <summary>One line for the tooltip: what the skill does in this job.</summary>
    public string Why { get; init; } = "";
}

/// <summary>Nature as a monster: an avalanche, a sandstorm, a cliff climb, fought like an encounter.</summary>
public sealed record PerilDef
{
    public string Name { get; init; } = "";

    /// <summary>Chance a crossing of the country turns into the peril instead of its everyday hazard.</summary>
    public double Chance { get; init; }

    /// <summary>The skills that help: each hero rolls their best of them.</summary>
    public IReadOnlyList<string> Skills { get; init; } = [];
    public int Dc { get; init; } = 12;

    /// <summary>Successes they need before this many failures.</summary>
    public int Need { get; init; } = 3;
    public int Fail { get; init; } = 3;

    /// <summary>Damage die each failed roll costs the one who rolled it, scaled with the country's level.</summary>
    public int Die { get; init; } = 6;

    /// <summary>What losing costs: "packs" (rations and potions) or "hurt" (another blow to all).</summary>
    public string Cost { get; init; } = "hurt";
    public IReadOnlyList<string> Text { get; init; } = [];
    public string Win { get; init; } = "";
    public string Lose { get; init; } = "";
}

/// <summary>A strange place that bends the rules, as roads.json <c>kinds</c> describe it.</summary>
public sealed record WonderDef
{
    /// <summary>hush, well, ferry, bones, ring, oasis or mirage.</summary>
    public string Kind { get; init; } = "";
    public IReadOnlyList<string> Text { get; init; } = [];
    public IReadOnlyList<string> Wish { get; init; } = [];
    public IReadOnlyList<string> Boon { get; init; } = [];
    public IReadOnlyList<string> Nothing { get; init; } = [];
    public IReadOnlyList<string> Curse { get; init; } = [];
    public IReadOnlyList<string> Missed { get; init; } = [];

    /// <summary>Gold a wish or a find is worth per level of the country.</summary>
    public int Gold { get; init; }

    /// <summary>Gold a head the ferryman asks.</summary>
    public int Toll { get; init; }
    public int Dc { get; init; } = 12;
    public string Skill { get; init; } = "perception";

    /// <summary>What lives in it (the false oasis).</summary>
    public string Monster { get; init; } = "";

    /// <summary>Region themes it is placed in; empty for anywhere.</summary>
    public IReadOnlyList<string> Themes { get; init; } = [];
}

public sealed record WonderRules
{
    public int[] Rings { get; init; } = [1, 4];
    public int PerRing { get; init; } = 1;

    /// <summary>Oases (real or false) in each desert region.</summary>
    public int Oases { get; init; } = 2;
}

public sealed record GatewayRules
{
    public int Count { get; init; } = 1;
    public int[] Near { get; init; } = [1, 2];
    public int[] Far { get; init; } = [3, 4];
    public int RiddleDc { get; init; } = 14;

    /// <summary>Days from an end within which a passing party notices it.</summary>
    public double Notice { get; init; } = 0.6;

    /// <summary>Share of the walk a gateway must save to be worth the detour.</summary>
    public double Saving { get; init; } = 0.25;
    public IReadOnlyList<string> Locks { get; init; } = ["open"];
    public IReadOnlyList<string> Names { get; init; } = [];
    public IReadOnlyList<string> Words { get; init; } = [];
    public IReadOnlyDictionary<string, List<string>> Texts { get; init; } = new Dictionary<string, List<string>>();
}

/// <summary>Jobs on the road, strange places and old gateways (roads.json <c>wayfaring</c>).</summary>
public sealed record WayRules
{
    public IReadOnlyList<string> Order { get; init; } = [];

    /// <summary>Jobs done at camp and on rests, which go to whoever is best at them on top of their day's job.</summary>
    public IReadOnlyList<string> Night { get; init; } = [];

    /// <summary>Navigation DC a unit of the country's slowness adds: a mire is harder to find the way through than a ridge.</summary>
    public double SlowDc { get; init; } = 5;

    /// <summary>Nights of food a party carries before someone has to forage.</summary>
    public int Rations { get; init; } = 3;

    /// <summary>Share of a stretch's hours a lost navigator adds, and the least.</summary>
    public double Lost { get; init; } = 0.5;
    public double LostMin { get; init; } = 3;
    public int HungerMax { get; init; } = 3;
    public IReadOnlyDictionary<string, JobDef> Jobs { get; init; } = new Dictionary<string, JobDef>();
    public IReadOnlyDictionary<string, List<string>> Texts { get; init; } = new Dictionary<string, List<string>>();
    public WonderRules Wonders { get; init; } = new();
    public GatewayRules Gateways { get; init; } = new();

    public JobDef Job(string id) => Jobs.TryGetValue(id, out var j) ? j : new JobDef();
}

/// <summary>Who does what on the road, and their bonus at it.</summary>
public sealed record RoadJob(string Job, int AgentId, string Name, int Bonus);

/// <summary>
/// An old arch in the wilds that opens on another far away. <see cref="Known"/> once a company has seen it;
/// <see cref="Open"/> once its lock is answered.
/// </summary>
public sealed class Gateway
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public double Ax { get; set; }
    public double Ay { get; set; }
    public double Bx { get; set; }
    public double By { get; set; }
    public int RegionA { get; init; }
    public int RegionB { get; init; }

    /// <summary>"open", "riddle" or "password".</summary>
    public string Lock { get; init; } = "open";
    public bool Known { get; set; }
    public bool Open { get; set; }
}

/// <summary>
/// The road between the town and its sites, as the heroes live it. Each hero takes a job (navigate, keep watch,
/// treat wounds, forage, track) and rolls the skill that fits it; a job is routine when 10 + their bonus meets the
/// DC, so the everyday never fails. Hard country can turn into a peril fought like an encounter, strange places
/// bend the rules, and old gateways shorten far roads. Rolls made while planning go by hash, not the town's dice.
/// </summary>
public static class Wayfaring
{
    public static WayRules Rules(Content content) => content.Roads.Wayfaring;

    /// <summary>A job needs no roll when the hero would make it taking ten.</summary>
    public static bool Routine(int bonus, int dc) => 10 + bonus >= dc;

    /// <summary>Hand out the jobs: the day's in order, each to the free hero best at its skill (ties to the lower id); the night's to the best at them.</summary>
    public static List<RoadJob> Assign(Town town, IReadOnlyList<Agent> members)
    {
        var rules = Rules(town.Content);
        var free = members.OrderBy(a => a.Id).ToList();
        var jobs = new List<RoadJob>();
        foreach (var id in rules.Order)
        {
            if (free.Count == 0) break;
            var job = rules.Job(id);
            var best = free.OrderByDescending(a => Skills.Bonus(town, a, job.Skill)).ThenBy(a => a.Id).First();
            free.Remove(best);
            jobs.Add(new RoadJob(id, best.Id, best.GivenName, Skills.Bonus(town, best, job.Skill)));
        }
        foreach (var id in rules.Night)
        {
            if (members.Count == 0) break;
            var job = rules.Job(id);
            var best = members.OrderByDescending(a => Skills.Bonus(town, a, job.Skill)).ThenBy(a => a.Id).First();
            jobs.Add(new RoadJob(id, best.Id, best.GivenName, Skills.Bonus(town, best, job.Skill)));
        }
        return jobs;
    }

    /// <summary>Each hero's bonus at every skill the road can ask for, worked out once for the venture.</summary>
    public static Dictionary<int, Dictionary<string, int>> Bonuses(Town town, IReadOnlyList<Agent> members)
    {
        var skills = new HashSet<string>(StringComparer.Ordinal);
        var rules = Rules(town.Content);
        foreach (var j in rules.Jobs.Values) skills.Add(j.Skill);
        foreach (var k in town.Content.Roads.Kinds)
        {
            if (k.Peril != null) skills.UnionWith(k.Peril.Skills);
            if (k.Wonder != null) skills.Add(k.Wonder.Skill);
        }
        skills.Add("investigation");
        return members.ToDictionary(a => a.Id, a => skills.OrderBy(s => s, StringComparer.Ordinal).ToDictionary(s => s, s => Skills.Bonus(town, a, s)));
    }

    /// <summary>The line a party's card shows for a job: "Mira navigates (Survival +5)".</summary>
    public static string Describe(Town town, RoadJob j) =>
        $"{j.Name} {Rules(town.Content).Job(j.Job).Verb} ({Skills.Name(town, Rules(town.Content).Job(j.Job).Skill)} {Skills.Sign(j.Bonus)})";

    /// <summary>Dice for a party's planning, seeded from the town, the hour and the leader, so they never move the town's own.</summary>
    public static Rng Dice(Town town, int salt) => new(unchecked((int)Voices.Hash("wayfaring", town.Seed, town.Tick, salt)));

    internal static string Pick(Rng rng, IReadOnlyDictionary<string, List<string>> texts, string key) =>
        texts.TryGetValue(key, out var lines) && lines.Count > 0 ? rng.Pick(lines) : "";

    /// <summary>
    /// The navigator's roll through or round a stretch of hard country: the hours they lose if they lose the way
    /// (0 when they don't), with what they say.
    /// </summary>
    public static double Navigate(Content content, RouteMind mind, Feature f, double legHours, List<string> said)
    {
        if (mind.Navigator == "" || mind.Dice == null) return 0;
        var rules = Rules(content);
        var job = rules.Job("navigate");
        int dc = job.Dc + (int)(f.Tier * job.PerTier + Math.Max(0, f.Kind.Slow - 1) * rules.SlowDc) + mind.Murk;
        if (Routine(mind.NavBonus, dc)) return 0;
        int roll = mind.Dice.D20();
        if (roll != 1 && (roll == 20 || roll + mind.NavBonus >= dc))
        {
            if (mind.Dice.Chance(0.3)) said.Add(Fill(Pick(mind.Dice, rules.Texts, "found"), mind.Navigator, f.Name, ""));
            return 0;
        }
        double lost = Math.Max(rules.LostMin, legHours * rules.Lost);
        said.Add(Fill(Pick(mind.Dice, rules.Texts, "lost"), mind.Navigator, f.Name, lost >= content.Roads.MarchHours ? $"{Js.Round(lost / content.Roads.MarchHours)} day{(lost >= content.Roads.MarchHours * 1.5 ? "s" : "")}" : "half a day"));
        return Js.Round(lost * 10) / 10.0;
    }

    private static string Fill(string s, string name, string feature, string days) =>
        Cap(s.Replace("{name}", name, StringComparison.Ordinal).Replace("{feature}", feature, StringComparison.Ordinal).Replace("{days}", days, StringComparison.Ordinal));

    internal static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    // ---- Strange places ----

    /// <summary>
    /// After the rest of the world: strange places in open ground between the sites, oases in the deserts, a bone
    /// ferryman on one lake and the old gateways. On their own dice, so nothing else a seed makes shifts.
    /// </summary>
    public static void Place(Town town)
    {
        var rng = new Rng(town.Seed * 3253 + 9001);
        var rules = Rules(town.Content);
        var kinds = town.Content.Roads.Kinds;
        var common = kinds.Where(k => k.Wonder is { Kind: not ("oasis" or "mirage" or "ferry") }).ToList();
        var w = rules.Wonders;
        for (int ring = w.Rings[0]; ring <= w.Rings[^1] && common.Count > 0; ring++)
        {
            var regions = town.Regions.Where(r => r.Ring == ring && !r.Theme.Sea).ToList();
            for (int i = 0; i < w.PerRing && regions.Count > 0; i++)
            {
                var region = rng.Pick(regions);
                var kind = rng.Pick(common);
                Spot(town, rng, region, kind, 0.22);
            }
        }
        var oasis = kinds.FirstOrDefault(k => k.Wonder?.Kind == "oasis");
        var mirage = kinds.FirstOrDefault(k => k.Wonder?.Kind == "mirage");
        if (oasis != null)
            foreach (var region in town.Regions.Where(r => oasis.Wonder!.Themes.Contains(r.Theme.Id)))
                for (int i = 0; i < w.Oases; i++)
                    Spot(town, rng, region, mirage != null && i > 0 && rng.Chance(0.5) ? mirage : oasis, 0.16);
        // The bone ferryman keeps one lake: anyone may cross it, for his toll.
        var ferry = kinds.FirstOrDefault(k => k.Wonder?.Kind == "ferry");
        var lakes = town.Terrain.Where(f => f.Kind.Id == "lake").OrderBy(f => f.Id).ToList();
        if (ferry != null && lakes.Count > 0)
        {
            var lake = rng.Pick(lakes);
            int at = town.Terrain.IndexOf(lake);
            town.Terrain[at] = new Feature
            {
                Id = lake.Id,
                Name = Fresh(town, rng, ferry, town.Regions.FirstOrDefault(g => g.Id == lake.RegionId)?.Theme),
                Kind = ferry with { Slow = lake.Kind.Slow, Habitats = lake.Kind.Habitats },
                RegionId = lake.RegionId,
                Tier = lake.Tier,
                Bearing = lake.Bearing,
                Arc = lake.Arc,
                Inner = lake.Inner,
                Outer = lake.Outer,
                Shape = lake.Shape,
            };
        }
        PlaceGateways(town, rng);
    }

    /// <summary>A name from the kind's patterns: {A} one of its words, {B} a name from the region's country.</summary>
    private static string Name(Rng rng, TerrainKindDef kind, ThemeDef? theme)
    {
        string pattern = kind.Names.Count > 0 ? rng.Pick(kind.Names) : $"the {{A}} {kind.Name}";
        string word = kind.Words.Count > 0 ? rng.Pick(kind.Words) : "Old";
        string name = theme is { Names.Count: > 0 } ? rng.Pick(theme.Names) : word;
        return pattern.Replace("{A}", word, StringComparison.Ordinal).Replace("{B}", name, StringComparison.Ordinal);
    }

    /// <summary>A name no other country on the map has, if a few tries find one.</summary>
    private static string Fresh(Town town, Rng rng, TerrainKindDef kind, ThemeDef? theme)
    {
        string name = Name(rng, kind, theme);
        for (int i = 0; i < 8 && town.Terrain.Any(f => f.Name == name); i++) name = Name(rng, kind, theme);
        return name;
    }

    /// <summary>
    /// A small place on the way out to one of the region's sites (where companies will walk by it), clear of the sites
    /// and other country; false if none fits.
    /// </summary>
    private static bool Spot(Town town, Rng rng, Region region, TerrainKindDef kind, double depth)
    {
        var spots = town.Sites.Select(s => World.Spot(town, s)).ToList();
        var targets = town.Sites.Where(s => s.RegionId == region.Id && !s.Def.Aloft).OrderBy(s => s.Def.Id, StringComparer.Ordinal).ToList();
        for (int tries = 0; tries < 12; tries++)
        {
            double r, bearing;
            if (targets.Count > 0)
            {
                var p = World.Spot(town, rng.Pick(targets));
                double reach = Js.Hypot(p.X, p.Y);
                r = Math.Max(0.6, reach * (0.45 + rng.Next() * 0.35));
                bearing = (Math.Atan2(p.X, -p.Y) * 180 / Math.PI + (rng.Next() - 0.5) * 6 + 360) % 360;
            }
            else
            {
                r = Math.Max(0.6, region.Days - 0.9 + rng.Next() * 1.2);
                bearing = (region.Bearing + (rng.Next() - 0.5) * 0.8 * region.Arc + 360) % 360;
            }
            double arc = depth / r * 180 / Math.PI * 1.4;
            var f = new Feature
            {
                Id = town.Terrain.Count,
                Name = Fresh(town, rng, kind, region.Theme),
                Kind = kind,
                RegionId = region.Id,
                Tier = region.Levels[0],
                Bearing = Js.Round(bearing * 10) / 10.0,
                Arc = Js.Round(arc * 10) / 10.0,
                Inner = Js.Round((r - depth / 2) * 100) / 100.0,
                Outer = Js.Round((r + depth / 2) * 100) / 100.0,
            };
            if (spots.Any(p => Roads.Contains(f, p.X, p.Y, 0.25)) || town.Terrain.Any(o => Near(o, f))) continue;
            town.Terrain.Add(f);
            return true;
        }
        return false;
    }

    private static bool Near(Feature a, Feature b)
    {
        double ra = (a.Inner + a.Outer) / 2, rb = (b.Inner + b.Outer) / 2;
        var pa = Roads.At(a, a.Bearing, ra);
        var pb = Roads.At(b, b.Bearing, rb);
        // Inside the other's span, or within a little of its middle.
        return Roads.Contains(a, pb.X, pb.Y, 0.3) || Roads.Contains(b, pa.X, pa.Y, 0.3) || Js.Hypot(pa.X - pb.X, pa.Y - pb.Y) < 0.5;
    }

    private static (double X, double Y) At(double bearing, double r)
    {
        double a = bearing * Math.PI / 180;
        return (r * Math.Sin(a), -r * Math.Cos(a));
    }

    // ---- Old gateways ----

    private static void PlaceGateways(Town town, Rng rng)
    {
        var g = Rules(town.Content).Gateways;
        for (int i = 0; i < g.Count; i++)
        {
            var near = town.Regions.Where(r => r.Ring >= g.Near[0] && r.Ring <= g.Near[^1]).ToList();
            var far = town.Regions.Where(r => r.Ring >= g.Far[0] && r.Ring <= g.Far[^1]).ToList();
            if (near.Count == 0 || far.Count == 0) return;
            var a = rng.Pick(near);
            // The far end lies well round the world from the near one, so the arch is worth the walk to it.
            var b = far.OrderByDescending(r => Math.Abs(World.AngleBetween(r.Bearing, a.Bearing)) + rng.Next() * 60).ThenBy(r => r.Id).First();
            var pa = At(a.Bearing + (rng.Next() - 0.5) * 0.4 * a.Arc, a.Days - 0.4);
            var pb = At(b.Bearing + (rng.Next() - 0.5) * 0.4 * b.Arc, b.Days - 0.4);
            string word = g.Words.Count > 0 ? rng.Pick(g.Words) : "Old";
            string name = g.Names.Count > 0 ? rng.Pick(g.Names).Replace("{A}", word).Replace("{B}", a.Theme.Names.Count > 0 ? rng.Pick(a.Theme.Names) : word) : $"the {word} Gate";
            string lk = g.Locks.Count > 0 ? rng.Pick(g.Locks) : "open";
            town.Gateways.Add(new Gateway
            {
                Id = town.Gateways.Count,
                Name = name,
                Ax = Js.Round(pa.X * 100) / 100.0,
                Ay = Js.Round(pa.Y * 100) / 100.0,
                Bx = Js.Round(pb.X * 100) / 100.0,
                By = Js.Round(pb.Y * 100) / 100.0,
                RegionA = a.Id,
                RegionB = b.Id,
                Lock = lk,
                Open = lk == "open",
            });
        }
    }

    /// <summary>
    /// The way from one spot to another by a gateway, if a known one saves enough walking and the party can open it:
    /// the near arch, then the far one (the step between them takes no time). Null to walk.
    /// </summary>
    internal static (Gateway Gate, (double X, double Y) In, (double X, double Y) Out)? Shortcut(Town town, (double X, double Y) from, (double X, double Y) to, RouteMind mind, List<string> said)
    {
        var rules = Rules(town.Content).Gateways;
        double direct = Js.Hypot(to.X - from.X, to.Y - from.Y);
        (Gateway, (double, double), (double, double))? best = null;
        double bestLen = direct * (1 - rules.Saving);
        foreach (var g in town.Gateways.Where(g => g.Known))
            foreach (var (a, b) in new[] { ((g.Ax, g.Ay), (g.Bx, g.By)), ((g.Bx, g.By), (g.Ax, g.Ay)) })
            {
                double len = Js.Hypot(a.Item1 - from.X, a.Item2 - from.Y) + Js.Hypot(to.X - b.Item1, to.Y - b.Item2);
                if (len < bestLen)
                {
                    bestLen = len;
                    best = (g, a, b);
                }
            }
        if (best is not { } pick) return null;
        var gate = pick.Item1;
        if (!gate.Open)
        {
            // A riddle the party can try; a password only the peoples who live there know.
            if (gate.Lock != "riddle" || mind.Dice == null || mind.Puzzler == "") return null;
            int roll = mind.Dice.D20();
            int total = roll + mind.PuzzleBonus;
            string rolled = $"d20 {roll}, {total} vs DC {rules.RiddleDc}";
            if (roll == 1 || (roll != 20 && total < rules.RiddleDc))
            {
                said.Add(Cap(Pick(mind.Dice, rules.Texts, "riddleFailed").Replace("{name}", mind.Puzzler).Replace("{gate}", gate.Name).Replace("{roll}", rolled)));
                return null;
            }
            gate.Open = true;
            said.Add(Cap(Pick(mind.Dice, rules.Texts, "riddle").Replace("{name}", mind.Puzzler).Replace("{gate}", gate.Name).Replace("{roll}", rolled)));
        }
        said.Add(Cap(Pick(mind.Dice ?? new Rng(gate.Id), rules.Texts, "through").Replace("{gate}", gate.Name)));
        return pick;
    }

    /// <summary>Companies walking near an arch nobody knew of bring word of it home.</summary>
    internal static void Notice(Town town, IReadOnlyList<Waypoint> route, (double X, double Y) from)
    {
        var rules = Rules(town.Content).Gateways;
        foreach (var g in town.Gateways.Where(g => !g.Known))
        {
            var cur = from;
            foreach (var w in route)
            {
                if (SegmentNear(cur, (w.X, w.Y), (g.Ax, g.Ay), rules.Notice) || SegmentNear(cur, (w.X, w.Y), (g.Bx, g.By), rules.Notice))
                {
                    g.Known = true;
                    town.Log(LedgerCategory.Quest, Importance.Notable, Cap(Pick(new Rng((int)Voices.Hash("gate-found", town.Seed, g.Id)), rules.Texts, "found").Replace("{gate}", g.Name)));
                    break;
                }
                cur = (w.X, w.Y);
            }
        }
    }

    private static bool SegmentNear((double X, double Y) a, (double X, double Y) b, (double X, double Y) p, double reach)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double l2 = dx * dx + dy * dy;
        double t = l2 < 1e-12 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
        return Js.Hypot(a.X + dx * t - p.X, a.Y + dy * t - p.Y) <= reach;
    }

    /// <summary>Daily: an allied people living by a password gateway tells the town its word.</summary>
    public static void Daily(Town town)
    {
        var rules = Rules(town.Content).Gateways;
        foreach (var g in town.Gateways.Where(g => !g.Open && g.Lock == "password"))
        {
            var friend = town.Settlements
                .Where(s => s.Allied && !s.Razed && (s.RegionId == g.RegionA || s.RegionId == g.RegionB))
                .OrderBy(s => s.Id)
                .FirstOrDefault();
            if (friend == null) continue;
            g.Open = true;
            g.Known = true;
            town.Log(LedgerCategory.Faction, Importance.Notable, Cap(Pick(new Rng((int)Voices.Hash("gate-word", town.Seed, g.Id)), rules.Texts, "learned").Replace("{gate}", g.Name).Replace("{people}", friend.Kin.People)));
        }
    }
}
