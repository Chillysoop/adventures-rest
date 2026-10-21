namespace CrownAndCompass.Sim.Systems;

/// <summary>How the town guard is posted, and what the wall works do (content/game/garrison.json).</summary>
public sealed record GarrisonRules
{
    /// <summary>The wall's sections round the town, from the south gate and on round to the west (clockwise from above).</summary>
    public IReadOnlyList<string> Sections { get; init; } = ["south", "south-west", "west", "north-west", "north", "north-east", "east", "south-east"];

    /// <summary>Tiles a guard covers in an hour on the way to a post.</summary>
    public double WalkPerHour { get; init; } = 12;

    /// <summary>Guards a call to arms sends to a wall section, a tower and the gate.</summary>
    public int WallPost { get; init; } = 4;
    public int TowerPost { get; init; } = 2;
    public int GatePost { get; init; } = 4;

    /// <summary>Guards a flag asks for unless told otherwise, and how many flags the watch will hold at once.</summary>
    public int FlagGuards { get; init; } = 3;
    public int MaxFlags { get; init; } = 4;

    /// <summary>Tiles round a flag its guards cover, and how much each guard there adds to the defence of what they cover (up to the cap).</summary>
    public double FlagRadius { get; init; } = 5;
    public double FlagPerGuard { get; init; } = 0.15;
    public double FlagCap { get; init; } = 0.75;

    /// <summary>How much a section's share of the watch, above or below an even share, moves its strength in a siege; and the bounds.</summary>
    public double Spread { get; init; } = 0.15;
    public double FactorLow { get; init; } = 0.85;
    public double FactorHigh { get; init; } = 1.4;

    /// <summary>A manned tower's archers add this to the strength of its section.</summary>
    public double TowerManned { get; init; } = 0.08;

    /// <summary>A stone gatehouse multiplies the gate's strength, and adds to the town's fortification.</summary>
    public double Gatehouse { get; init; } = 1.3;
    public double GatehouseFort { get; init; } = 0.05;

    /// <summary>A moat multiplies the town's fortification, and thins every wave that wades it.</summary>
    public double Moat { get; init; } = 1.12;
    public double MoatWave { get; init; } = 0.9;

    /// <summary>Watchmen who live in each tower and each wall barracks.</summary>
    public int TowerHouses { get; init; } = 2;
    public int BarracksHouses { get; init; } = 8;

    /// <summary>Share of the wage a watchman housed in the walls still draws; how much faster they muster; their weight at night raids.</summary>
    public double HousedWage { get; init; } = 0.5;
    public double HousedMuster { get; init; } = 2;
    public double HousedRaid { get; init; } = 1.25;

    /// <summary>Towers the stone walls and the city wall carry (the palisade's is the tower work's own max).</summary>
    public int StoneTowers { get; init; } = 6;
    public int CityTowers { get; init; } = 8;

    /// <summary>The section each tower stands on, in the order they are built.</summary>
    public IReadOnlyList<int> TowerOrder { get; init; } = [4, 2, 6, 3, 5, 1, 7, 0];

    public WorkSpec GatehouseWork { get; init; } = new() { Cost = 450, Timber = 10, Stone = 60, Days = 8, Tier = "town" };
    public WorkSpec MoatWork { get; init; } = new() { Cost = 600, Days = 10, Tier = "town" };
    public WorkSpec BarracksWork { get; init; } = new() { Cost = 350, Timber = 15, Stone = 40, Days = 6, Max = 2, Tier = "town" };

    /// <summary>How the map plays a siege back (<see cref="Battles"/>).</summary>
    public BattleRules Battle { get; init; } = new();

    /// <summary>Heroes the mayor posts to guard the town (<see cref="HeroDuty"/>).</summary>
    public DutyRules Duty { get; init; } = new();
}

/// <summary>What a hero on guard duty costs and does (garrison.json "duty").</summary>
public sealed record DutyRules
{
    /// <summary>The daily wage: a base, and more for each level.</summary>
    public double Wage { get; init; } = 3;
    public double WagePerLevel { get; init; } = 1;

    /// <summary>Strength at the walls for each level of a hero posted there, and for each tier of magic they wear.</summary>
    public double WallsPerLevel { get; init; } = 2;
    public double WallsPerTier { get; init; } = 3;

    /// <summary>What a hero on the streets adds to the watch's eye (<see cref="Council.WatchStrength"/>), by level and gear, and the most they all add.</summary>
    public double StreetsPerLevel { get; init; } = 0.015;
    public double StreetsPerTier { get; init; } = 0.01;
    public double StreetsCap { get; init; } = 0.3;

    /// <summary>Loyalty a hero loses when the town can't pay the wage and they walk off duty.</summary>
    public double Unpaid { get; init; } = -8;
}

/// <summary>A hero's post at home: on the walls against what comes from outside, or on the streets against what works within.</summary>
public enum HeroDuty
{
    None,
    Walls,
    Streets,

    /// <summary>Leading one of the town's regiments (<see cref="Army.SetCommander"/>).</summary>
    Command,

    /// <summary>Out with the host on the field, with a goal (<see cref="Field"/>).</summary>
    Field,
}

/// <summary>Where a guard can be sent: a wall section, a tower, the gate, or a flag the player planted.</summary>
public enum PostKind
{
    Wall,
    Tower,
    Gate,
    Flag,
}

/// <summary>An order the watch is holding: a stretch of wall, a tower or the gate called to arms, or a flag.</summary>
public sealed class GuardPost
{
    public int Id { get; init; }
    public PostKind Kind { get; init; }

    /// <summary>The section of a wall post or the gate (0); the tower's number for a tower.</summary>
    public int Index { get; init; }

    /// <summary>Where a flag stands, in tiles from the town's corner.</summary>
    public double X { get; init; }
    public double Y { get; init; }

    /// <summary>Guards the order asked for.</summary>
    public int Wanted { get; init; }
    public int PlacedTick { get; init; }

    /// <summary>Raised by the stand-in mayor for a siege, and stood down by it after.</summary>
    public bool Auto { get; init; }
}

/// <summary>One of the watch: walking a beat on the wall, or posted where an order sent them.</summary>
public sealed class Guard
{
    public int Id { get; init; }

    /// <summary>The section of wall they walk when no order holds them.</summary>
    public int Beat { get; init; }
    public int? PostId { get; set; }

    /// <summary>Where they set out from, and when they left and get there.</summary>
    public double FromX { get; set; }
    public double FromY { get; set; }
    public int DepartTick { get; set; }
    public int ArriveTick { get; set; }
}

public sealed class GarrisonState
{
    public List<Guard> Guards { get; } = [];
    public List<GuardPost> Posts { get; } = [];
    public int NextGuard { get; set; } = 1;
    public int NextPost { get; set; } = 1;
}

/// <summary>
/// The town guard in the field, without real-time control: the watch walks the walls in beats, and the
/// mayor directs it by orders, never by the hand. A call to arms on a wall section, a tower or the gate
/// sends the nearest guards to hold it; a flag planted anywhere posts guards there to cover the buildings
/// round it. A siege comes from a side the scouts name when the march begins, and each wave hits a section,
/// so a wall manned where the blow lands holds better than one spread thin. Watchmen live in the towers and
/// wall barracks and muster quicker; those in the barracks are cheaper to keep and stronger at night. A
/// gatehouse and a moat round out the stone walls.
/// </summary>
public static class Garrison
{
    public static GarrisonRules Rules(Town town) => town.Content.Garrison;

    public static int Sections(Town town) => Rules(town).Sections.Count;

    // Geometry ----------------------------------------------------------------------------------

    /// <summary>Whether the town has a finished wall for the watch to stand on.</summary>
    public static bool Walled(Town town) => town.Defenses.Palisade == PalisadeState.Built || town.Defenses.StoneWall == PalisadeState.Built;

    /// <summary>The way a section faces from the town's heart: section 0 due south (+y), then on clockwise as seen from above.</summary>
    public static (double X, double Y) Bearing(Town town, int section)
    {
        double a = Math.PI / 2 + section * 2 * Math.PI / Sections(town);
        return (Math.Cos(a), Math.Sin(a));
    }

    /// <summary>The section a point lies in, by its bearing from the town's heart.</summary>
    public static int SectionAt(Town town, double x, double y)
    {
        int n = Sections(town);
        double a = Math.Atan2(y - Town.Center, x - Town.Center) - Math.PI / 2;
        int s = (int)Math.Round(a / (2 * Math.PI / n));
        return ((s % n) + n) % n;
    }

    /// <summary>
    /// How far the outer wall stands from the town's heart along a section's bearing: the city wall, else the stone wall
    /// or the palisade, which stand on the same line round the old town (<see cref="City.Wall"/>).
    /// </summary>
    public static double Radius(Town town, int section)
    {
        double a = Math.PI / 2 + section * 2 * Math.PI / Sections(town);
        return City.Wall(town, town.Defenses.CityWall == PalisadeState.Built ? 1 : 0, a);
    }

    /// <summary>The middle of a section of the outer wall.</summary>
    public static (double X, double Y) WallPoint(Town town, int section)
    {
        // The gate post stands at the outer wall's main gate, wherever it was put.
        int ring = town.Defenses.CityWall == PalisadeState.Built ? 1 : 0;
        if (section == 0 && Walled(town) && WallLines.Gates(town, ring) is { Count: > 0 } gates) return WallLines.Point(town, ring, gates[0]);
        var (dx, dy) = Bearing(town, section);
        double r = Radius(town, section);
        return (Town.Center + dx * r, Town.Center + dy * r);
    }

    /// <summary>What the Ledger calls a section: the gate, or a side of the wall (or of the town, with no wall yet).</summary>
    public static string Place(Town town, int section)
    {
        if (section == 0) return Walled(town) ? "gate" : "south road";
        return $"{Rules(town).Sections[section]} {(Walled(town) ? "wall" : "side")}";
    }

    // Towers and housing -----------------------------------------------------------------------

    /// <summary>How many towers the outer wall can carry.</summary>
    public static int MaxTowers(Town town)
    {
        var d = town.Defenses;
        return d.CityWall == PalisadeState.Built ? Rules(town).CityTowers
            : d.StoneWall == PalisadeState.Built ? Rules(town).StoneTowers
            : town.Content.Enemy.Tower.Max;
    }

    /// <summary>The section a tower stands on.</summary>
    public static int TowerSection(Town town, int tower)
    {
        var order = Rules(town).TowerOrder;
        return tower < order.Count ? order[tower] : tower % Sections(town);
    }

    public static bool TowerOn(Town town, int section)
    {
        for (int i = 0; i < town.Defenses.Towers; i++) if (TowerSection(town, i) == section) return true;
        return false;
    }

    /// <summary>Watchmen the walls can house: in each tower, and in each barracks built into the stone.</summary>
    public static int Housing(Town town) => town.Defenses.Towers * Rules(town).TowerHouses + town.Defenses.Barracks * Rules(town).BarracksHouses;

    /// <summary>How many of the watch live in the walls.</summary>
    public static int Housed(Town town) => Math.Min(town.Watch, Housing(town));

    /// <summary>How many of the watch have bunks in the wall barracks, where they draw less pay and sleep on the walls.</summary>
    public static int Barracked(Town town) => Math.Min(town.Watch, town.Defenses.Barracks * Rules(town).BarracksHouses);

    /// <summary>The first guards by id have the bunks in the walls.</summary>
    public static bool LivesInWalls(Town town, Guard g) => town.Garrison.Guards.IndexOf(g) < Housed(town);

    /// <summary>The watch's daily wage, less what the guards in the wall barracks give up for their bunks.</summary>
    public static double Wages(Town town)
    {
        double w = town.Content.Council.WatchWage;
        return town.Watch * w - Barracked(town) * w * (1 - Rules(town).HousedWage);
    }

    /// <summary>The watch's weight at a night raid: those who sleep in the wall barracks count for more.</summary>
    public static double NightWatch(Town town) => town.Watch + Barracked(town) * (Rules(town).HousedRaid - 1);

    // Where the guards are -------------------------------------------------------------------

    public static GuardPost? Post(Town town, int? id) => id is int i ? town.Garrison.Posts.FirstOrDefault(p => p.Id == i) : null;

    /// <summary>Where an order stands.</summary>
    public static (double X, double Y) PostPoint(Town town, GuardPost p) => p.Kind switch
    {
        PostKind.Flag => (p.X, p.Y),
        PostKind.Tower => WallPoint(town, TowerSection(town, p.Index)),
        _ => WallPoint(town, p.Index),
    };

    /// <summary>Where a guard is headed: their post, else their beat on the wall, else the town square.</summary>
    public static (double X, double Y) Target(Town town, Guard g)
    {
        if (Post(town, g.PostId) is { } p) return PostPoint(town, p);
        return Walled(town) ? WallPoint(town, g.Beat) : (Town.Center, Town.Center);
    }

    /// <summary>Where a guard is at a moment (on the way, they walk straight there).</summary>
    public static (double X, double Y) Position(Town town, Guard g, double tick)
    {
        var to = Target(town, g);
        if (tick >= g.ArriveTick || g.ArriveTick <= g.DepartTick) return to;
        double f = Math.Clamp((tick - g.DepartTick) / (g.ArriveTick - g.DepartTick), 0, 1);
        return (g.FromX + (to.X - g.FromX) * f, g.FromY + (to.Y - g.FromY) * f);
    }

    public static bool Arrived(Town town, Guard g) => town.Tick >= g.ArriveTick;

    /// <summary>Guards standing at an order now.</summary>
    public static int At(Town town, GuardPost p) => town.Garrison.Guards.Count(g => g.PostId == p.Id && Arrived(town, g));

    /// <summary>Guards an order holds, there or on the way.</summary>
    public static int Sent(Town town, GuardPost p) => town.Garrison.Guards.Count(g => g.PostId == p.Id);

    /// <summary>Guards on a section of the wall now: its own order, its towers, the gate, and those walking their beat there.</summary>
    public static int Manning(Town town, int section)
    {
        int n = 0;
        foreach (var g in town.Garrison.Guards)
        {
            if (!Arrived(town, g)) continue;
            var p = Post(town, g.PostId);
            if (p == null ? g.Beat == section
                : p.Kind is PostKind.Wall or PostKind.Gate ? p.Index == section
                : p.Kind == PostKind.Tower && TowerSection(town, p.Index) == section) n++;
        }
        return n;
    }

    private static void Send(Town town, Guard g, GuardPost? post)
    {
        var (x, y) = Position(town, g, town.Tick);
        g.PostId = post?.Id;
        var to = Target(town, g);
        double speed = Rules(town).WalkPerHour * (LivesInWalls(town, g) ? Rules(town).HousedMuster : 1);
        g.FromX = x;
        g.FromY = y;
        g.DepartTick = town.Tick;
        g.ArriveTick = town.Tick + (int)Math.Ceiling(Js.Hypot(to.X - x, to.Y - y) / speed);
    }

    /// <summary>Keep one guard for every watchman: new hires walk out of the town square to their beat, the last hired leave first.</summary>
    public static void Sync(Town town)
    {
        var gs = town.Garrison;
        while (gs.Guards.Count > town.Watch) gs.Guards.RemoveAt(gs.Guards.Count - 1);
        while (gs.Guards.Count < town.Watch)
        {
            var g = new Guard { Id = gs.NextGuard++, Beat = gs.Guards.Count % Sections(town), FromX = Town.Center, FromY = Town.Center, DepartTick = town.Tick };
            gs.Guards.Add(g);
            Send(town, g, null);
        }
        // An order nobody holds any more is struck off; a flag stays until it is stood down.
        gs.Posts.RemoveAll(p => p.Kind != PostKind.Flag && !gs.Guards.Any(g => g.PostId == p.Id));
    }

    // Orders ------------------------------------------------------------------------------------

    /// <summary>Why the watch can't answer a call to arms here, or null when it can.</summary>
    public static string? CallBlocked(Town town, PostKind kind, int index)
    {
        if (town.Watch == 0) return "the town has no watch";
        if (kind == PostKind.Flag) return "plant a flag instead";
        if (!Walled(town)) return "the town has no wall yet";
        if (kind == PostKind.Tower && (index < 0 || index >= town.Defenses.Towers)) return "no such tower";
        if (kind != PostKind.Tower && (index < 0 || index >= Sections(town))) return "no such stretch of wall";
        return null;
    }

    /// <summary>
    /// Sound the horn on a wall section, a tower or the gate: the nearest guards on their beats run to hold it,
    /// then, if that is not enough, the nearest from other orders.
    /// </summary>
    public static GuardPost? CallToArms(Town town, PostKind kind, int index, bool auto = false)
    {
        Sync(town);
        if (CallBlocked(town, kind, index) != null) return null;
        if (kind == PostKind.Wall && index == 0) kind = PostKind.Gate;
        var gs = town.Garrison;
        var r = Rules(town);
        var post = gs.Posts.FirstOrDefault(p => p.Kind == kind && p.Index == index);
        if (post == null)
        {
            post = new GuardPost { Id = gs.NextPost++, Kind = kind, Index = index, PlacedTick = town.Tick, Auto = auto, Wanted = kind switch { PostKind.Tower => r.TowerPost, PostKind.Gate => r.GatePost, _ => r.WallPost } };
            gs.Posts.Add(post);
        }
        int sent = Fill(town, post);
        string where = kind == PostKind.Tower ? $"the {r.Sections[TowerSection(town, index)]} tower" : $"the {Place(town, index)}";
        if (sent > 0 && !auto)
            town.Log(LedgerCategory.War, Importance.Notable, $"The horn sounds on {where}: {Count(sent)} of the watch {(sent == 1 ? "runs" : "run")} to man it.");
        return post;
    }

    private static string Count(int n) => n == 1 ? "one" : n.ToString();

    /// <summary>
    /// The quick order to man the walls: the horn sounds on every side a host is marching from and on the gate,
    /// or on the gate alone when the scouts name no side. Returns how many orders it gave.
    /// </summary>
    public static int ManTheWalls(Town town)
    {
        if (CallBlocked(town, PostKind.Gate, 0) != null) return 0;
        int n = 0;
        foreach (int s in Threatened(town).Distinct().Order())
            if (s != 0 && CallToArms(town, PostKind.Wall, s) != null) n++;
        if (CallToArms(town, PostKind.Gate, 0) != null) n++;
        return n;
    }

    /// <summary>Whether every side a host comes from, and the gate, has a call to arms on it.</summary>
    public static bool WallsManned(Town town)
    {
        bool Held(PostKind k, int i) => town.Garrison.Posts.Any(p => p.Kind == k && p.Index == i);
        return Held(PostKind.Gate, 0) && Threatened(town).All(s => s == 0 || Held(PostKind.Wall, s));
    }


    /// <summary>Send the nearest guards an order still lacks: those on their beats first, then those holding other orders.</summary>
    private static int Fill(Town town, GuardPost post)
    {
        var at = PostPoint(town, post);
        int lack = post.Wanted - Sent(town, post);
        if (lack <= 0) return 0;
        var picked = town.Garrison.Guards
            .Where(g => g.PostId != post.Id)
            .OrderBy(g => g.PostId == null ? 0 : 1)
            .ThenBy(g =>
            {
                var (x, y) = Position(town, g, town.Tick);
                return Js.Hypot(x - at.X, y - at.Y);
            })
            .ThenBy(g => g.Id)
            .Take(lack)
            .ToList();
        foreach (var g in picked) Send(town, g, post);
        return picked.Count;
    }

    public static string? FlagBlocked(Town town, double x, double y)
    {
        if (town.Watch == 0) return "the town has no watch";
        if (x < 0 || y < 0 || x > Town.Grid || y > Town.Grid) return "too far from town";
        if (town.Garrison.Posts.Count(p => p.Kind == PostKind.Flag) >= Rules(town).MaxFlags) return $"the watch holds {Rules(town).MaxFlags} flags already";
        return null;
    }

    /// <summary>Plant a flag: the nearest guards go to stand there and cover the buildings round it.</summary>
    public static GuardPost? PlantFlag(Town town, double x, double y, int guards = 0)
    {
        Sync(town);
        if (FlagBlocked(town, x, y) != null) return null;
        var gs = town.Garrison;
        var post = new GuardPost { Id = gs.NextPost++, Kind = PostKind.Flag, X = x, Y = y, PlacedTick = town.Tick, Wanted = Math.Clamp(guards > 0 ? guards : Rules(town).FlagGuards, 1, Math.Max(1, town.Watch)) };
        gs.Posts.Add(post);
        int sent = Fill(town, post);
        string by = Near(town, x, y) is Building b ? $"by {b.Name}" : $"on the {Rules(town).Sections[SectionAt(town, x, y)]} side of town";
        town.Log(LedgerCategory.War, Importance.Routine, $"The captain of the watch plants a flag {by} and posts {Count(sent)} {(sent == 1 ? "guard" : "guards")} there.");
        return post;
    }

    /// <summary>The finished building nearest a point, within a flag's reach.</summary>
    private static Building? Near(Town town, double x, double y)
    {
        double reach = Rules(town).FlagRadius;
        return town.Buildings
            .Where(b => b.Complete)
            .Select(b => (B: b, D: Js.Hypot(town.Lot(b.LotId).X - x, town.Lot(b.LotId).Y - y)))
            .Where(p => p.D <= reach)
            .OrderBy(p => p.D)
            .ThenBy(p => p.B.Id)
            .Select(p => p.B)
            .FirstOrDefault();
    }

    /// <summary>Stand an order down: its guards walk back to their beats.</summary>
    public static bool StandDown(Town town, int postId)
    {
        var gs = town.Garrison;
        var post = Post(town, postId);
        if (post == null) return false;
        foreach (var g in gs.Guards.Where(g => g.PostId == postId).ToList()) Send(town, g, null);
        gs.Posts.Remove(post);
        return true;
    }

    // In battle ----------------------------------------------------------------------------------

    /// <summary>The side a host comes from, read off the day it sets out (no dice, so nothing else's luck moves).</summary>
    public static int Approach(Town town, int salt)
    {
        uint x = unchecked((uint)(town.Tick * 2654435761L + salt * 40503L + 17));
        x = unchecked(((x >> 16) ^ x) * 0x45d9f3bu);
        x = (x >> 16) ^ x;
        return (int)(x % (uint)Sections(town));
    }

    /// <summary>The section each wave of a siege hits: the side it came from, then the gate, then the side again.</summary>
    public static int WaveSection(Town town, int approach, int wave)
    {
        if (wave != 2) return approach;
        return approach != 0 ? 0 : 1;
    }

    /// <summary>
    /// How a section stands up to a wave, against an even spread of the watch: more of it there holds better,
    /// a bare stretch worse; a manned tower's archers and a stone gatehouse add to it. With no wall it is all one.
    /// </summary>
    public static double SectionFactor(Town town, int section)
    {
        if (!Walled(town)) return 1;
        var r = Rules(town);
        double f = 1;
        int total = town.Garrison.Guards.Count;
        if (total > 0)
        {
            double even = total / (double)Sections(town);
            f = Math.Clamp(1 + r.Spread * (Manning(town, section) - even) / Math.Max(1, even), r.FactorLow, r.FactorHigh);
        }
        foreach (var p in town.Garrison.Posts)
            if (p.Kind == PostKind.Tower && TowerSection(town, p.Index) == section && At(town, p) > 0) f += r.TowerManned;
        if (section == 0 && town.Defenses.Gatehouse == PalisadeState.Built) f *= r.Gatehouse;
        return f;
    }

    /// <summary>A wave thinned by the moat it had to wade; the share of it that flies or swims (<paramref name="fliers"/>) goes over unharmed.</summary>
    public static double Wade(Town town, double wave, double fliers = 0) =>
        town.Defenses.Moat == PalisadeState.Built ? wave * (fliers + (1 - fliers) * Rules(town).MoatWave) : wave;

    /// <summary>What the gatehouse and the moat add to the town's fortification.</summary>
    public static double FortBonus(Town town) => town.Defenses.Gatehouse == PalisadeState.Built ? Rules(town).GatehouseFort : 0;

    public static double MoatFactor(Town town) => town.Defenses.Moat == PalisadeState.Built ? Rules(town).Moat : 1;

    /// <summary>Whether a flag's guards stand over a building.</summary>
    public static bool Covered(Town town, Building b) => FlagGuardsOver(town, b) > 0;

    private static int FlagGuardsOver(Town town, Building b)
    {
        var lot = town.Lot(b.LotId);
        double reach = Rules(town).FlagRadius;
        int n = 0;
        foreach (var p in town.Garrison.Posts)
            if (p.Kind == PostKind.Flag && Js.Hypot(p.X - lot.X, p.Y - lot.Y) <= reach) n += At(town, p);
        return n;
    }

    public static bool AnyFlags(Town town) => town.Garrison.Posts.Any(p => p.Kind == PostKind.Flag);

    /// <summary>Raiders who see a flag's guards over their mark go elsewhere when they can (GDD rule E8).</summary>
    public static List<Building> Unguarded(Town town, List<Building> marks)
    {
        if (!AnyFlags(town)) return marks;
        var open = marks.Where(b => !Covered(town, b)).ToList();
        return open.Count > 0 ? open : marks;
    }

    /// <summary>How much stronger the defence of a building is with a flag's guards over it.</summary>
    public static double FlagFactor(Town town, Building? b)
    {
        if (b == null || !AnyFlags(town)) return 1;
        var r = Rules(town);
        return 1 + Math.Min(r.FlagCap, r.FlagPerGuard * FlagGuardsOver(town, b));
    }

    // The stand-in mayor --------------------------------------------------------------------------

    /// <summary>The side each host on the march comes from.</summary>
    public static List<int> Threatened(Town town)
    {
        var sides = new List<int>();
        if (town.Enemy.Stage == ThreatStage.Marching && town.Enemy.ArrivalTick != null) sides.Add(town.Enemy.Approach);
        foreach (var m in town.Saga.Menaces.Where(m => m.Alive && m.ArrivalTick != null).OrderBy(m => m.Id)) sides.Add(m.Approach);
        return sides;
    }

    // Heroes on guard duty -----------------------------------------------------------------

    /// <summary>Heroes at home on the duty, by id.</summary>
    public static List<Agent> OnDuty(Town town, HeroDuty duty) =>
        town.Heroes.Where(a => a.Hero!.Duty == duty && !a.IsAway).OrderBy(a => a.Id).ToList();

    /// <summary>Every hero posted, walls first.</summary>
    public static List<Agent> Defenders(Town town) => [.. OnDuty(town, HeroDuty.Walls), .. OnDuty(town, HeroDuty.Streets)];

    public static double Wage(Town town, Agent a) => Rules(town).Duty.Wage + Rules(town).Duty.WagePerLevel * a.Hero!.Level;

    public static double DutyWages(Town town) => town.Heroes.Where(a => a.Hero!.Duty != HeroDuty.None).Sum(a => Wage(town, a));

    private static int GearTiers(Agent a) => a.Hero!.Magic.Values.Sum(i => i.Tier);

    /// <summary>Strength the heroes on the walls add, their levels and the magic they wear.</summary>
    public static double DutyDefense(Town town)
    {
        var r = Rules(town).Duty;
        double d = 0;
        foreach (var a in OnDuty(town, HeroDuty.Walls)) d += a.Hero!.Level * r.WallsPerLevel + GearTiers(a) * r.WallsPerTier;
        return d;
    }

    /// <summary>What the heroes on the streets add to the watch's eye for spies, knives, thieves and fire-raisers.</summary>
    public static double StreetWatch(Town town)
    {
        var r = Rules(town).Duty;
        double w = 0;
        foreach (var a in OnDuty(town, HeroDuty.Streets)) w += a.Hero!.Level * r.StreetsPerLevel + GearTiers(a) * r.StreetsPerTier;
        return Math.Min(r.StreetsCap, w);
    }

    /// <summary>Why the hero can't take the post now, or null.</summary>
    public static string? DutyBlocked(Town town, Agent a, HeroDuty duty)
    {
        var s = a.Hero;
        if (s == null || a.Dead || a.Departed) return "gone";
        if (s.Duty == duty) return duty == HeroDuty.None ? "not on duty" : "already there";
        if (duty == HeroDuty.None) return null;
        if (a.IsAway) return "out of town";
        if (s.JailedUntil != null) return "in the cells";
        if (town.Treasury < Wage(town, a)) return "no gold for the wage";
        return null;
    }

    /// <summary>
    /// Post a hero to guard the town, or stand them down. A posted hero stays home on a daily wage and takes no
    /// bounties: on the walls they stand with the watch against raids and sieges and meet the warlord first; on
    /// the streets they help the watch catch spies, turned councillors, knives in the night, thieves and fire-raisers.
    /// </summary>
    public static bool SetDuty(Town town, int heroId, HeroDuty duty)
    {
        if (heroId < 1 || heroId > town.Agents.Count) return false;
        var a = town.Agent(heroId);
        if (DutyBlocked(town, a, duty) != null) return false;
        a.Hero!.Duty = duty;
        town.Log(LedgerCategory.War, Importance.Routine, duty switch
        {
            HeroDuty.Walls => $"{a.Name} takes a post on the walls of {town.Name}, for {Js.Str(Wage(town, a))} gold a day.",
            HeroDuty.Streets => $"{a.Name} walks the streets of {town.Name} with the watch, for {Js.Str(Wage(town, a))} gold a day.",
            _ => $"{a.Name} stands down from guard duty, free to take bounties again.",
        }, [a.Id]);
        return true;
    }

    /// <summary>The day's wages for heroes on duty; one the town can't pay walks off, and minds.</summary>
    private static void PayDuty(Town town)
    {
        foreach (var a in town.Heroes.Where(a => a.Hero!.Duty != HeroDuty.None).OrderBy(a => a.Id).ToList())
        {
            var s = a.Hero!;
            if (a.Dead || a.Departed)
            {
                s.Duty = HeroDuty.None;
                continue;
            }
            double wage = Wage(town, a);
            if (town.Treasury < wage)
            {
                s.Duty = HeroDuty.None;
                Loyalty.Sway(a, Rules(town).Duty.Unpaid);
                town.Log(LedgerCategory.War, Importance.Notable, $"With no gold for their wage, {a.Name} walks off guard duty.", [a.Id]);
                continue;
            }
            town.Book(TreasuryAccount.Watch, -wage);
            a.Gold += wage;
            s.Pay += wage;
        }
    }

    public static void Daily(Town town, bool auto)
    {
        Sync(town);
        PayDuty(town);
        if (!auto) return;
        // The stand-in mayor mans the side a host comes from and the gate, and stands them down once it is over.
        var sides = Threatened(town);
        if (sides.Count > 0 && Walled(town) && town.Watch > 0)
            foreach (int s in sides.Distinct())
            {
                if (s != 0) CallToArms(town, PostKind.Wall, s, auto: true);
                CallToArms(town, PostKind.Gate, 0, auto: true);
            }
        else
            foreach (var p in town.Garrison.Posts.Where(p => p.Auto).ToList()) StandDown(town, p.Id);
        Works(town);
    }

    /// <summary>
    /// Once the stone walls stand, the warband is dealt with and the treasury is deep, the stand-in mayor adds a gatehouse,
    /// then a moat, then barracks, then more towers: a late sink for a rich town's gold against the menaces of the wilds.
    /// </summary>
    private static void Works(Town town)
    {
        if (town.Defenses.StoneWall != PalisadeState.Built || Threat.IsActive(town)) return;
        foreach (var w in new[] { DefenseWork.Gatehouse, DefenseWork.Moat, DefenseWork.Barracks, DefenseWork.Tower })
        {
            if (Threat.BuildBlocked(town, w) is string why)
            {
                if (why == "not enough gold") return;
                continue;
            }
            if (town.Treasury > Threat.BuildCost(town, w) + 900) Threat.Build(town, w);
            return;
        }
    }
}
