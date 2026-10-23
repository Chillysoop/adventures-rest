namespace CrownAndCompass.Sim.Systems;

/// <summary>A kind of foe met in the air (sky.json "foes"): the rings it flies in, its strength, its speed, and whether it boards.</summary>
public sealed record SkyFoeDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int[] Rings { get; init; } = [0, 20];
    public double Power { get; init; } = 20;
    public double Speed { get; init; } = 2;
    public bool Boards { get; init; }
}

/// <summary>content/game/sky.json: the town's airships, the winds and storms they fly through, and how they fight.</summary>
public sealed record SkyFleetRules
{
    public double Wind { get; init; } = 0.2;
    /// <summary>Chance a day aloft is held by the weather where the ship flies, by its step (fair, grey, foul, storm, tempest).</summary>
    public double[] StormStep { get; init; } = [0, 0.01, 0.1, 0.6, 1];
    public int[] StormDays { get; init; } = [1, 2];
    public int Turnaround { get; init; } = 1;
    public double Learn { get; init; } = 0.25;
    public double ProwessFight { get; init; } = 1;
    public int PatrolDays { get; init; } = 6;
    public int PatrolHours { get; init; } = 12;

    /// <summary>The most a scouting flight charts round the middle of the land it flew to, in days of road.</summary>
    public double ChartMost { get; init; } = 1.2;
    public double StrikeShare { get; init; } = 0.06;
    public double StrikeMost { get; init; } = 0.45;
    public double Meet { get; init; } = 0.06;
    public double MeetPerRing { get; init; } = 0.03;
    public double MenaceMeet { get; init; } = 0.08;
    public double MeetMax { get; init; } = 0.4;
    public double MenaceShare { get; init; } = 0.12;
    public double StormApproach { get; init; } = 4;
    public double EvadeAt { get; init; } = 1.6;
    public int Rounds { get; init; } = 3;
    public double Low { get; init; } = 0.75;
    public double High { get; init; } = 1.25;
    public double Gasbag { get; init; } = 0.35;
    public double Break { get; init; } = 0.45;
    public double CrewFight { get; init; } = 1.5;
    public double HeroLevel { get; init; } = 4;
    public double HeroHurt { get; init; } = 0.3;
    public double Prize { get; init; } = 25;
    public int Fame { get; init; } = 2;
    public double MenaceHurt { get; init; } = 0.08;
    public int WalkDays { get; init; } = 3;
    public double Salvage { get; init; } = 180;
    public int SalvageDays { get; init; } = 4;
    public int StrandDays { get; init; } = 30;

    /// <summary>How high the Sky Dock's own lent airship climbs (<see cref="Reaches"/>): higher bands need one of the town's ships.</summary>
    public double DockCeiling { get; init; } = 0.45;
    public CrystalRules Crystal { get; init; } = new();
    public HeightRules Heights { get; init; } = new();
    public ExpeditionRules Expedition { get; init; } = new();
    public DockParts Parts { get; init; } = new();
    public AirRaidRules AirRaids { get; init; } = new();
    public int RaidEvery { get; init; } = 24;
    public double RaidChance { get; init; } = 0.3;
    public double RaidStrength { get; init; } = 8;
    public double RaidGold { get; init; } = 80;
    public double RaidLift { get; init; } = 4;
    public double TowerGuns { get; init; } = 2;
    public double WatchFight { get; init; } = 0.4;
    public double PatchBelow { get; init; } = 0.5;
    public double ChargeBelow { get; init; } = 0.5;
    public double PowerPerRing { get; init; } = 3;
    public IReadOnlyList<ShipDef> Ships { get; init; } = [];
    public IReadOnlyList<string> Names { get; init; } = [];
    public IReadOnlyList<string> Captains { get; init; } = [];
    public IReadOnlyList<SkyFoeDef> Foes { get; init; } = [];
    public Dictionary<string, List<string>> Texts { get; init; } = [];
}

/// <summary>
/// Where the Sky Dock may stand (Conner, 2026-10-22: "must be built at elevation either on mountains or a tall castle or a
/// floating city"): `building` only on ground at least `minHeight` tiles up (a hilltop), within `towerReach` tiles of the
/// keep's `tower` once it stands, or anywhere once the Lofted City flies.
/// </summary>
public sealed record HeightRules
{
    public string Building { get; init; } = "sky_dock";
    public double MinHeight { get; init; } = 0.8;
    public string Tower { get; init; } = "keep_high_tower";
    public double TowerReach { get; init; } = 8;
}

/// <summary>
/// Lift-crystal, the airships' fuel (Conner, 2026-10-22: "even if you rush to airships, you still need a fuel source"). Every
/// day of lift a charge puts back burns a day's crystal from the dock's store (less what discoveries save, <see cref="Research.Crystal"/>).
/// The dock comes with `start`; traders sell `buyMost` every `buyDays` at `buy` gold; companies bring home `shardsPerTier` a tier
/// from a cleared site in the sky; and a cleared isle from the `seamFrom` height up shows a seam the town can mine for
/// `mineCost` gold, yielding `mineYield` (plus `mineYieldPerRing` a ring out) a day while it stays clear.
/// </summary>
public sealed record CrystalRules
{
    public double Start { get; init; } = 16;

    /// <summary>Days a griffon egg brought home from the isles takes to hatch.</summary>
    public int HatchDays { get; init; } = 30;
    public double Buy { get; init; } = 18;
    public double BuyMost { get; init; } = 12;
    public int BuyDays { get; init; } = 30;
    public double ShardsPerTier { get; init; } = 1.5;
    public string SeamFrom { get; init; } = "high";
    public double MineCost { get; init; } = 900;
    public double MineYield { get; init; } = 0.6;
    public double MineYieldPerRing { get; init; } = 0.15;
}

/// <summary>
/// A company carried to a site in the sky (<see cref="Reaches"/>): the ship's ballistae open the fight (`edge` on the
/// company's checks and blows), it waits `wait` days at the isle, and a company whose ship is lost is stranded until a
/// rescue flight finds it (`search` days of looking, none where the site is scried) or `strandMost` days pass and it
/// makes its own way down, `walkHurt` of their hit points the worse.
/// </summary>
public sealed record ExpeditionRules
{
    public int Edge { get; init; } = 1;
    public int Wait { get; init; } = 1;
    public int[] Search { get; init; } = [1, 4];
    public int StrandMost { get; init; } = 40;
    public double WalkHurt { get; init; } = 0.3;
}

/// <summary>An airship's job: patrol over the town, scout a region, or strike a lair; when it set out and is due home.</summary>
public sealed class SkyFlight
{
    public string Job { get; init; } = "";

    /// <summary>The region scouted or struck over (the town's own for a patrol), and the site struck, if any.</summary>
    public int RegionId { get; init; } = -1;
    public string SiteId { get; init; } = "";
    public int SetOutTick { get; init; }
    public int BackDay { get; set; }

    /// <summary>Heroes aboard as marines.</summary>
    public List<int> Marines { get; init; } = [];
    public bool Stormed { get; set; }

    /// <summary>The strike's volleys landed so far: its share of the lair's strength is taken when the ship turns for home.</summary>
    public double Struck { get; set; }
}

/// <summary>A fight in the air, kept for the ship list and the map: who, against what, where, and how it went.</summary>
public sealed class SkyBattle
{
    public int Tick { get; init; }
    public int ShipId { get; init; }
    public string Foe { get; init; } = "";
    public int RegionId { get; init; }

    /// <summary>"evaded", "won", "boarded", "repelled", "lost", "downed" or "wrecked".</summary>
    public string Outcome { get; init; } = "";
    public int Volleys { get; init; }
}

/// <summary>
/// What the Sky Dock's parts do (sky.json `parts`, the parts themselves in buildings.json with `partOf: sky_dock`): the dock
/// moors `berths` airships, and each mast `perMast` more; the refinery makes crystal go further, the rigger hall cuts the
/// riggers' gold for charges and repairs, the aerie sells griffons, the signal tower spots raiders sooner and finds the stranded at once.
/// </summary>
public sealed record DockParts
{
    public int Berths { get; init; } = 2;
    public int PerMast { get; init; } = 2;
    public IReadOnlyList<string> Masts { get; init; } = [];
    public string Refinery { get; init; } = "";
    public double Refines { get; init; } = 0.15;
    public string Riggers { get; init; } = "";
    public double RiggerCost { get; init; } = 0.6;
    public string Signal { get; init; } = "";
    public int SignalHours { get; init; } = 6;
}

public sealed class SkyEgg
{
    public int HeroId { get; init; }
    public string Mount { get; init; } = "";
    public int Day { get; init; }
}

/// <summary>
/// Country a ship charted for the council: the line it flew out (from, to, in days from the town) and the land round its
/// end, <see cref="Radius"/> days across. The map lifts the fog there as described, as of <see cref="Tick"/>.
/// </summary>
public sealed class Chart
{
    public int RegionId { get; init; }
    public double FromX { get; init; }
    public double FromY { get; init; }
    public double ToX { get; init; }
    public double ToY { get; init; }
    public double Radius { get; init; }
    public int Tick { get; init; }

    /// <summary>The ship that flew it.</summary>
    public string By { get; init; } = "";
}

public sealed class SkyFleetState
{
    public bool Granted { get; set; }

    /// <summary>Lift-crystal in the dock's store, in days of lift (<see cref="Sky.Charge"/>), and what was bought of it since <see cref="BoughtDay"/>.</summary>
    public double Crystal { get; set; }
    public double Bought { get; set; }
    public int BoughtDay { get; set; }

    /// <summary>Griffon eggs brought home from the isles, each kept for the hero who found it (<see cref="Systems.Crystal.Hatch"/>).</summary>
    public List<SkyEgg> Eggs { get; } = [];
    public int NextRaidDay { get; set; }

    /// <summary>An attack from the air on its way (<see cref="Systems.AirRaids"/>), and the day the next may be rolled.</summary>
    public AirRaid? AirRaid { get; set; }
    public int NextAirRaidDay { get; set; }
    public List<SkyBattle> Battles { get; } = [];
}

/// <summary>
/// The town's airships (Conner, 2026-10-21: "airship travel and combat... different from the seafaring system but with
/// similarities in ship management"). They are ships of the town's one fleet (<see cref="Fleet"/>: hull, crew, captains who
/// grow from green to legendary, repairs), built and flown from the Sky Dock by any town that has one. What differs is the
/// sky: each ship runs on lift-crystal and comes down where it runs dry, to be salvaged or lost; the seasons' winds speed or
/// slow it by its heading and storms hold it aloft; and it fights in three steps (the approach, ballista volleys, then a
/// boarding its hero marines fight). Its jobs are its own: patrols that see raiders coming sooner, scouting the council
/// pays for, and strikes that bombard a lair before a company goes in. It still carries companies (<see cref="Passage.Board"/>)
/// and flies the dock's merchant runs (<see cref="Skyship"/>). Every roll here uses the ship's own dice, never the town's.
/// </summary>
public static class Sky
{
    public static SkyFleetRules Rules(Town town) => town.Content.Sky;

    private static SkyFleetState State(Town town) => town.SkyFleet;

    /// <summary>The town's Sky Dock, standing.</summary>
    public static Building? Dock(Town town) => town.Buildings.FirstOrDefault(b => b.Def.Id == "sky_dock" && b.Complete && !b.Closed);

    /// <summary>The town's airships, in id order.</summary>
    public static IEnumerable<Ship> Ships(Town town) => town.Ships.Where(s => Fleet.Aloft(town, s)).OrderBy(s => s.Id);

    /// <summary>Home at the mast and fit to fly: not on a job, not down in the wilds, with hull and lift left.</summary>
    public static bool Moored(Town town, Ship s) => s.Flight == null && s.DownedRegion == null && s.Hull > 0 && Voyages.Carrying(town, s) == null;

    /// <summary>Airships at the mast that could take a company or a merchant run now.</summary>
    public static int Ready(Town town) => Ships(town).Count(s => Moored(town, s) && s.Lift >= 2);

    // ---- Where the dock may stand ----

    /// <summary>Whether a building that wants height (the Sky Dock) may stand here: a hilltop, beside the keep's tall tower, or anywhere once the Lofted City flies.</summary>
    public static bool SitsHigh(Town town, BuildingDef d, double x, double y)
    {
        var h = Rules(town).Heights;
        if (d.Id != h.Building || Lofted.Aloft(town)) return true;
        if (Hills.Height(town, x, y) >= h.MinHeight) return true;
        return town.Buildings.Any(b => b.Def.Id == h.Tower && b.Complete && Js.Hypot(town.Lot(b.LotId).X - x, town.Lot(b.LotId).Y - y) <= h.TowerReach);
    }

    /// <summary>Why the dock can stand nowhere yet: no hilltop in the opened rings, no tall tower, no flying city. Null when somewhere will do.</summary>
    public static string? HeightBlocked(Town town, BuildingDef d)
    {
        var h = Rules(town).Heights;
        if (d.Id != h.Building || Lofted.Aloft(town) || town.Buildings.Any(b => b.Def.Id == h.Tower && b.Complete)) return null;
        var grid = Hills.Grid(town);
        for (int i = 0; i < grid.Length; i++)
        {
            if (grid[i] < h.MinHeight) continue;
            double x = (i % Land.Size + 0.5) / Land.Res, y = (i / Land.Size + 0.5) / Land.Res;
            if (City.IsOpen(town, x, y)) return null;
        }
        return "needs high ground: a hilltop in the town's opened rings, the keep's high tower, or the Lofted City";
    }

    /// <summary>Whether a part of the Sky Dock stands (buildings.json `partOf: sky_dock`).</summary>
    public static bool Has(Town town, string partId) =>
        partId != "" && Dock(town) is { } dock && town.Buildings.Any(b => b.ParentId == dock.Id && b.Def.Id == partId && b.Complete && !b.Closed);

    /// <summary>Airships the dock can moor: its own berths and each mast's.</summary>
    public static int Berths(Town town) => Rules(town).Parts.Berths + Rules(town).Parts.PerMast * Rules(town).Parts.Masts.Count(m => Has(town, m));

    /// <summary>The share of a charge's crystal saved: discoveries, and the refinery.</summary>
    public static double Saved(Town town) => Math.Min(0.75, Research.Crystal(town) + (Has(town, Rules(town).Parts.Refinery) ? Rules(town).Parts.Refines : 0));

    /// <summary>What the riggers' work costs: less with the rigger hall.</summary>
    public static double RiggerShare(Town town) => Has(town, Rules(town).Parts.Riggers) ? Rules(town).Parts.RiggerCost : 1;

    public static string? BuildBlocked(Town town, string kind)
    {
        if (Dock(town) == null) return "needs the Sky Dock";
        if (Ships(town).Count() >= Berths(town)) return "the dock's berths are full: add a mast";
        if (Fleet.Kind(town, kind) is not { Realm: "sky" } d) return "no such airship";
        if (d.Tier != "" && !City.Reached(town, d.Tier)) return $"needs the town to be a {City.TierName(town, d.Tier).ToLowerInvariant()}";
        if (town.Treasury < Fleet.Cost(town, d)) return "not enough gold";
        return null;
    }

    // ---- Winds and storms (the one sky-travel model: Lofted's sailing can read it too) ----

    /// <summary>Days aloft for a straight flight of so many road days on a bearing: the wind behind is quicker, ahead slower.</summary>
    public static int FlightDays(Town town, double roadDays, double speed, double bearing)
    {
        // The prevailing wind of the day (Weather): with it is quicker, against it slower.
        double toward = (bearing - Weather.Prevailing(town, Weather.TimeOf(town.Tick)) + 360) % 360 * Math.PI / 180;
        return Math.Max(1, (int)Math.Ceiling(roadDays * speed * (1 - Rules(town).Wind * Math.Cos(toward)) - 1e-9));
    }

    /// <summary>The chance the weather over a land holds a ship today, less the share of storms it climbs above.</summary>
    public static double StormChance(Town town, ShipDef d, Region? region)
    {
        var (x, y) = region == null ? (0.0, 0.0) : Lofted.Spot(town, region);
        var steps = Rules(town).StormStep;
        int step = Weather.Worst(town, x, y, town.Day).Step;
        return steps.Length == 0 ? 0 : steps[Math.Min(step, steps.Length - 1)] * (1 - d.Ceiling);
    }

    /// <summary>Days there and back to a region, moored a day at the far end.</summary>
    public static int Days(Town town, Ship s, Region region) =>
        2 * FlightDays(town, Math.Max(1, region.Days), Fleet.Def(town, s).Speed, region.Bearing) + Rules(town).Turnaround;

    // ---- Jobs ----

    public static IEnumerable<Agent> Marines(Town town) =>
        town.Heroes.Where(h => !h.Dead && !h.IsAway && !h.Departed && h.Hero is { Hp: > 0 }).OrderBy(h => h.Id);

    /// <summary>Lift-crystal a job burns: a day's worth a day aloft.</summary>
    public static int JobDays(Town town, Ship s, string job, int regionId) =>
        job == "patrol" ? Rules(town).PatrolDays : Days(town, s, town.Regions[regionId]);

    /// <summary>Why the ship can't fly this job now, or null.</summary>
    public static string? Refuse(Town town, Ship s, string job, int regionId, string siteId, IReadOnlyList<int> marines)
    {
        if (!Fleet.Aloft(town, s)) return "not an airship";
        if (Dock(town) == null) return "needs the Sky Dock";
        if (s.DownedRegion != null) return "down in the wilds";
        if (s.Flight != null) return "already aloft";
        if (Skyship.Flying(town, s.Id)) return "out on a merchant run";
        if (Passage.AirshipsFree(town) <= 0) return "every airship at the mast is carrying a company";
        if (job is not ("patrol" or "scout" or "strike")) return "no such job";
        if (s.Hull < Fleet.Def(town, s).Hull * 0.25) return "needs patching first";
        if (marines.Count > Fleet.Def(town, s).Marines) return $"carries {Fleet.Def(town, s).Marines} marines at most";
        var free = Marines(town).Select(h => h.Id).ToHashSet();
        if (marines.Any(id => !free.Contains(id))) return "a hero is away or hurt";
        if (job == "patrol") return s.Lift < Rules(town).PatrolDays ? "not enough lift-crystal" : null;
        if (regionId < 0 || regionId >= town.Regions.Count) return "no such land";
        var region = town.Regions[regionId];
        if (job == "strike")
        {
            var site = town.Sites.FirstOrDefault(x => x.Def.Id == siteId);
            if (site == null || site.Cleared || site.RegionId != regionId || site.Known == SiteKnowledge.Unheard) return "no lair the town knows of there";
        }
        if (s.Lift < Days(town, s, region)) return "not enough lift-crystal to get there and back";
        return null;
    }

    public static bool Send(Town town, int shipId, string job, int regionId = -1, string siteId = "", IReadOnlyList<int>? marines = null)
    {
        var s = town.Ships.FirstOrDefault(x => x.Id == shipId);
        marines ??= [];
        if (s == null || Refuse(town, s, job, regionId, siteId, marines) != null) return false;
        int days = JobDays(town, s, job, regionId);
        s.Flight = new SkyFlight { Job = job, RegionId = job == "patrol" ? -1 : regionId, SiteId = siteId, SetOutTick = town.Tick, BackDay = town.Day + days, Marines = [.. marines] };
        foreach (int id in marines) town.Agent(id).Hero!.AwayUntil = town.Tick + days * SimTime.HoursPerDay;
        string where = job == "patrol" ? "the skies over the town" : job == "strike" ? Site(town, siteId)?.Def.Name ?? town.Regions[regionId].Name : town.Regions[regionId].Name;
        town.Log(LedgerCategory.Town, Importance.Notable, Fill(town, Dice(town, s.Id), "launch", s, where, ""));
        return true;
    }

    /// <summary>Hours sooner the town sees raiders coming, for each airship out on patrol (<see cref="Institutions.SkyWatch"/>).</summary>
    public static int PatrolHours(Town town) =>
        Ships(town).Count(s => s.Flight?.Job == "patrol") * Rules(town).PatrolHours + (Has(town, Rules(town).Parts.Signal) ? Rules(town).Parts.SignalHours : 0);

    // ---- Lift, repairs, salvage ----

    /// <summary>Days of lift a charge would put back now: what the ship lacks, as far as the dock's crystal goes.</summary>
    public static double ChargeLift(Town town, Ship s)
    {
        double missing = Math.Max(0, Fleet.Def(town, s).Lift - s.Lift);
        double burn = 1 - Saved(town);
        return Math.Min(missing, burn <= 0 ? missing : State(town).Crystal / burn);
    }

    /// <summary>The riggers' gold for a charge (the crystal is the store's).</summary>
    public static double ChargeCost(Town town, Ship s) => Js.Round(ChargeLift(town, s) * Fleet.Def(town, s).Recharge * RiggerShare(town));

    /// <summary>Fill an airship's lift-crystal at the dock, as far as the store goes.</summary>
    public static bool Charge(Town town, int shipId)
    {
        var s = town.Ships.FirstOrDefault(x => x.Id == shipId);
        if (s == null || !Fleet.Aloft(town, s) || !Moored(town, s) || Dock(town) == null || s.Lift >= Fleet.Def(town, s).Lift) return false;
        double lift = ChargeLift(town, s);
        double cost = ChargeCost(town, s);
        if (lift < 1 || town.Treasury < cost) return false;
        town.Book(TreasuryAccount.Works, -cost);
        s.Lift += lift;
        State(town).Crystal = Math.Max(0, State(town).Crystal - lift * (1 - Saved(town)));
        town.Log(LedgerCategory.Town, Importance.Routine, $"The riggers charge {s.Name}'s lift-crystal ({Js.Str(Js.Round(lift))} days of lift, {Js.Str(cost)} gold).");
        return true;
    }

    /// <summary>Send riggers with fresh crystal to a downed airship: it flies home in a few days.</summary>
    public static bool Salvage(Town town, int shipId)
    {
        var s = town.Ships.FirstOrDefault(x => x.Id == shipId);
        var r = Rules(town);
        if (s == null || s.DownedRegion == null || s.SalvageDay != null || Dock(town) == null || town.Treasury < r.Salvage) return false;
        town.Book(TreasuryAccount.Works, -r.Salvage);
        s.SalvageDay = town.Day + r.SalvageDays + town.Regions[s.DownedRegion.Value].Ring;
        town.Log(LedgerCategory.Town, Importance.Notable, $"Riggers set out with fresh crystal for {s.Name}, down in {town.Regions[s.DownedRegion.Value].Name} ({Js.Str(r.Salvage)} gold).");
        return true;
    }

    // ---- The day ----

    /// <summary>Daily, from <see cref="Trade.Daily"/>: the dock's first ship, ships aloft, downed ships, pirates, and the stand-in.</summary>
    public static void Daily(Town town, bool autoMayor)
    {
        var r = Rules(town);
        var st = State(town);
        if (Dock(town) == null || r.Ships.Count == 0) return;
        if (!st.Granted)
        {
            st.Granted = true;
            st.Crystal += r.Crystal.Start;
            st.NextRaidDay = town.Day + r.RaidEvery;
            if (!Ships(town).Any()) Fleet.Build(town, r.Ships[0].Id, free: true);
        }
        foreach (var s in Ships(town).ToList())
        {
            if (s.Flight is { } f) Fly(town, s, f);
            else if (s.DownedRegion is int region) Downed(town, s, region);
        }
        if (town.Day >= st.NextRaidDay) Raid(town);
        if (autoMayor) StandIn(town);
    }

    private static Rng Dice(Town town, int salt) => new(town.Seed * 8761 + town.Day * 37 + salt * 101 + 5);

    private static SiteState? Site(Town town, string id) => id == "" ? null : town.Sites.FirstOrDefault(x => x.Def.Id == id);

    /// <summary>A day aloft: lift burns, storms may hold the ship, a foe may find it, and on its day it comes home.</summary>
    private static void Fly(Town town, Ship s, SkyFlight f)
    {
        var r = Rules(town);
        var d = Fleet.Def(town, s);
        var rng = Dice(town, s.Id);
        var region = f.RegionId >= 0 ? town.Regions[f.RegionId] : null;
        string where = region?.Name ?? "the town";
        s.Lift = Math.Max(0, s.Lift - 1);
        s.Hull = Math.Max(0, s.Hull - d.Wear);
        if (!f.Stormed && rng.Chance(StormChance(town, d, region)))
        {
            f.Stormed = true;
            int late = rng.Range(r.StormDays[0], r.StormDays[^1] + 1);
            f.BackDay += late;
            s.Lift = Math.Max(0, s.Lift - late);
            town.Log(LedgerCategory.Town, Importance.Routine, Fill(town, rng, "storm", s, where, ""));
        }
        int ring = region?.Ring ?? 0;
        if (rng.Chance(MeetChance(town, region)) && Meet(town, rng, region) is { } foe)
            if (!Fight(town, s, f, foe, region, rng)) return;
        // Over the lair, the ballistae go to work: a volley a day it is in reach.
        if (f.Job == "strike" && Site(town, f.SiteId) is { Cleared: false } site && town.Day >= f.BackDay - Days(town, s, town.Regions[f.RegionId]) / 2 - 1 && f.Struck < r.StrikeMost)
            f.Struck = Math.Min(r.StrikeMost, f.Struck + r.StrikeShare * d.Guns * (1 + 0.1 * (s.Prowess - 1)));
        if (s.Lift <= 0 && town.Day < f.BackDay)
        {
            Down(town, s, f, region, rng, "downed");
            return;
        }
        if (town.Day >= f.BackDay) Home(town, s, f, region, rng);
    }

    /// <summary>The chance a foe finds a ship over a land today: more far out, and more where flying menaces live.</summary>
    public static double MeetChance(Town town, Region? region)
    {
        var r = Rules(town);
        int ring = region?.Ring ?? 0;
        double menaces = region == null ? 0 : Js.Sum(Skyship.FliersNear(town, region), m => Passage.Fliers(town, m.Kind));
        return Math.Min(r.MeetMax, r.Meet + r.MeetPerRing * ring + r.MenaceMeet * menaces);
    }

    /// <summary>A foe in the air: a flying menace near, if one comes, or one of sky.json's foes for the ring.</summary>
    /// <summary>A foe in the air, as the shared ship fight takes it.</summary>
    internal static ShipFoe? MeetFoe(Town town, Rng rng, Region? region) => Meet(town, rng, region) is { } f ? new ShipFoe(f.Name, f.Power, f.Speed, f.Boards, f.Menace) : null;

    private static (string Name, double Power, double Speed, bool Boards, Menace? Menace)? Meet(Town town, Rng rng, Region? region)
    {
        var r = Rules(town);
        int ring = region?.Ring ?? 0;
        if (region != null && Skyship.FliersNear(town, region).OrderByDescending(m => m.Power).ThenBy(m => m.Id).FirstOrDefault() is { } m && rng.Chance(0.5))
            return (m.Name, Math.Max(10, m.Power * r.MenaceShare), 3, false, m);
        var kinds = r.Foes.Where(k => ring >= k.Rings[0] && ring <= k.Rings[^1]).ToList();
        if (kinds.Count == 0) return null;
        var k = rng.Pick(kinds);
        return (k.Name, k.Power + r.PowerPerRing * ring, k.Speed, k.Boards, null);
    }

    /// <summary>
    /// A fight in the air (<see cref="ShipFight"/>): the approach, the volleys, then a boarding if the foe boards. Returns
    /// false when the ship is down or wrecked and its job is over.
    /// </summary>
    private static bool Fight(Town town, Ship s, SkyFlight f, (string Name, double Power, double Speed, bool Boards, Menace? Menace) foe, Region? region, Rng rng)
    {
        string where = region?.Name ?? "the town";
        bool storm = f.Stormed && town.Day <= f.BackDay;
        string outcome = ShipFight.Resolve(town, s, new ShipFoe(foe.Name, foe.Power, foe.Speed, foe.Boards, foe.Menace), region, f.Marines, 0, storm, rng, out int volleys);
        Record(town, s, foe.Name, region, outcome, volleys);
        if (outcome == "evaded")
        {
            town.Log(LedgerCategory.War, Importance.Routine, Fill(town, rng, "evade", s, where, foe.Name));
            return true;
        }
        if (outcome is "downed" or "wrecked")
        {
            Down(town, s, f, region, rng, outcome, foe.Name);
            return false;
        }
        string marinesNames = Expedition.ListNames(f.Marines.Select(town.Agent).Where(a => !a.Dead).Select(a => a.GivenName).DefaultIfEmpty("the crew").ToList());
        town.Log(LedgerCategory.War, Importance.Notable, Fill(town, rng, outcome, s, where, foe.Name).Replace("{marines}", marinesNames, StringComparison.Ordinal));
        return true;
    }

    /// <summary>
    /// Stage a fight now between an airship and one of sky.json's foes over its job's land (for tests and screenshots):
    /// the outcome, as <see cref="SkyBattle.Outcome"/> records it.
    /// </summary>
    public static string Engage(Town town, int shipId, string foeId)
    {
        var s = town.Ships.FirstOrDefault(x => x.Id == shipId);
        var k = Rules(town).Foes.FirstOrDefault(x => x.Id == foeId);
        if (s == null || k == null || !Fleet.Aloft(town, s) || s.DownedRegion != null) return "";
        var f = s.Flight ?? new SkyFlight { Job = "patrol", SetOutTick = town.Tick, BackDay = town.Day + 1 };
        var region = f.RegionId >= 0 ? town.Regions[f.RegionId] : null;
        bool up = s.Flight != null;
        s.Flight = f;
        Fight(town, s, f, (k.Name, k.Power + Rules(town).PowerPerRing * (region?.Ring ?? 0), k.Speed, k.Boards, null), region, Dice(town, s.Id + State(town).Battles.Count * 7));
        if (!up && s.Flight == f) s.Flight = null;
        return State(town).Battles.LastOrDefault()?.Outcome ?? "";
    }

    internal static void Record(Town town, Ship s, string foe, Region? region, string outcome, int volleys)
    {
        var battles = State(town).Battles;
        battles.Add(new SkyBattle { Tick = town.Tick, ShipId = s.Id, Foe = foe, RegionId = region?.Id ?? -1, Outcome = outcome, Volleys = volleys });
        if (battles.Count > 12) battles.RemoveAt(0);
    }

    /// <summary>The ship comes down (lift spent) or is wrecked: the crew and the marines walk home, the ship waits or is gone.</summary>
    private static void Down(Town town, Ship s, SkyFlight f, Region? region, Rng rng, string how, string foe = "")
    {
        var r = Rules(town);
        int ring = region?.Ring ?? 0;
        int walk = r.WalkDays * (1 + ring);
        foreach (var a in f.Marines.Select(town.Agent).Where(a => !a.Dead && a.Hero != null)) a.Hero!.AwayUntil = town.Tick + walk * SimTime.HoursPerDay;
        s.Flight = null;
        // Over the town itself a ship with no lift settles onto the fields and is towed back to the mast.
        if (how == "downed" && region == null)
        {
            s.Lift = 0;
            foreach (var a in f.Marines.Select(town.Agent).Where(a => !a.Dead && a.Hero != null)) a.Hero!.AwayUntil = null;
            town.Log(LedgerCategory.War, Importance.Notable, $"{Cap(s.Name)} settles onto the fields outside town with her lift spent, and is towed back to the mast.");
            return;
        }
        town.Log(LedgerCategory.War, Importance.Major, Fill(town, rng, how, s, region?.Name ?? "the town", foe));
        if (how == "wrecked" || region == null)
        {
            town.Ships.Remove(s);
            return;
        }
        s.DownedRegion = region.Id;
        s.DownedDay = town.Day;
        s.Lift = 0;
    }

    /// <summary>The middle of a land, in days from the town (the way <see cref="World.Spot"/> places a site).</summary>
    public static (double X, double Y) Middle(Town town, Region region) => World.Middle(town, region);

    /// <summary>
    /// A ship home from scouting charts what it flew over: the line out from <paramref name="from"/> to <paramref name="to"/>
    /// (days from the town) and the land round its end. The fog lifts there as described. Sea voyages chart the same way.
    /// </summary>
    public static void Charted(Town town, int regionId, (double X, double Y) from, (double X, double Y) to, string by)
    {
        var region = town.Regions[regionId];
        double half = region.Arc * Math.PI / 360;
        double radius = Math.Min(Rules(town).ChartMost, Math.Max(0.3, region.Days * Math.Sin(half)));
        town.Charts.Add(new Chart { RegionId = regionId, FromX = from.X, FromY = from.Y, ToX = to.X, ToY = to.Y, Radius = radius, Tick = town.Tick, By = by });
    }

    /// <summary>A downed ship: riggers bring it home, or after long enough the wilds keep it.</summary>
    private static void Downed(Town town, Ship s, int region)
    {
        var r = Rules(town);
        var rng = Dice(town, s.Id);
        if (s.SalvageDay is int back && town.Day >= back)
        {
            s.DownedRegion = null;
            s.SalvageDay = null;
            s.Lift = Math.Max(2, Fleet.Def(town, s).Lift * 0.25);
            town.Log(LedgerCategory.Town, Importance.Notable, Fill(town, rng, "salvaged", s, town.Regions[region].Name, ""));
            return;
        }
        if (s.SalvageDay == null && town.Day - s.DownedDay >= r.StrandDays)
        {
            town.Ships.Remove(s);
            town.Log(LedgerCategory.Town, Importance.Notable, Fill(town, rng, "lostWild", s, town.Regions[region].Name, ""));
        }
    }

    /// <summary>Home from a job: a scout's charts, a strike's toll on the lair, and the marines back in town.</summary>
    private static void Home(Town town, Ship s, SkyFlight f, Region? region, Rng rng)
    {
        var r = Rules(town);
        s.Flight = null;
        s.Voyages++;
        s.Prowess = Math.Min(5, s.Prowess + r.Learn * 0.5);
        foreach (var a in f.Marines.Select(town.Agent).Where(a => !a.Dead && a.Hero != null)) a.Hero!.AwayUntil = null;
        if (f.Job == "scout" && region != null)
        {
            Charted(town, region.Id, (0, 0), Middle(town, region), s.Name);
            region.Known = true;
            var seen = town.Sites.Where(x => x.RegionId == region.Id && !x.Cleared && !x.Hidden).OrderBy(x => x.Def.Id, StringComparer.Ordinal).ToList();
            foreach (var site in seen)
            {
                site.Found = true;
                Rumors.Confirm(town, site, s.Name);
            }
            string found = seen.Count == 0 ? "nothing there worth a company's time" : Expedition.ListNames(seen.Select(x => x.Def.Name).Take(4).ToList());
            s.LastLine = Fill(town, rng, "scouted", s, region.Name, "").Replace("{found}", found, StringComparison.Ordinal);
        }
        else if (f.Job == "strike" && region != null && Site(town, f.SiteId) is { } site)
        {
            double share = site.Cleared ? 0 : f.Struck;
            site.Strength = Math.Max(0, site.Strength * (1 - share));
            site.Found = true;
            site.FoundTick ??= town.Tick;
            s.LastLine = Fill(town, rng, "struck", s, region.Name, "").Replace("{site}", site.Def.Name, StringComparison.Ordinal)
                .Replace("{share}", $"{Js.Str(Js.Round(share * 100))}%", StringComparison.Ordinal);
        }
        else s.LastLine = $"{Cap(s.Name)} moors at the Sky Dock home from patrol.";
        town.Log(LedgerCategory.Town, Importance.Notable, s.LastLine);
    }

    /// <summary>Sky pirates come for the dock: the moored ships' ballistae, the watchtowers and the watch drive them off, or they rob it.</summary>
    private static void Raid(Town town)
    {
        var r = Rules(town);
        State(town).NextRaidDay = town.Day + r.RaidEvery;
        var rng = Dice(town, 977);
        if (!rng.Chance(r.RaidChance)) return;
        var moored = Ships(town).Where(s => Moored(town, s)).ToList();
        double guard = Js.Sum(moored, s => Fleet.Def(town, s).Guns + r.ProwessFight * (s.Prowess - 1)) + Watch.Watchtowers(town).Count * r.TowerGuns + town.Watch * r.WatchFight;
        if (rng.D20() + guard >= 10 + r.RaidStrength + town.Day / 120.0)
        {
            town.Log(LedgerCategory.War, Importance.Notable, rng.Pick(Texts(town, "raidRepelled")));
            return;
        }
        double gold = Math.Min(Math.Max(0, town.Treasury), r.RaidGold);
        town.Book(TreasuryAccount.Works, -gold);
        var robbed = moored.Count > 0 ? moored[rng.Range(0, moored.Count)] : null;
        if (robbed != null) robbed.Lift = Math.Max(0, robbed.Lift - r.RaidLift);
        town.Log(LedgerCategory.War, Importance.Notable, rng.Pick(Texts(town, "raided"))
            .Replace("{gold}", Js.Str(Js.Round(gold)), StringComparison.Ordinal)
            .Replace("{ship}", robbed?.Name ?? "the dock", StringComparison.Ordinal));
    }

    /// <summary>The stand-in mayor patches and charges the ships, keeps one on patrol, and adds a frigate when it can spare the gold.</summary>
    private static void StandIn(Town town)
    {
        var r = Rules(town);
        foreach (var s in Ships(town).Where(s => Moored(town, s)).ToList())
        {
            var d = Fleet.Def(town, s);
            if (s.Hull < d.Hull * r.PatchBelow && town.Treasury > Fleet.RepairCost(town, s) + 300) Fleet.Repair(town, s.Id);
            if (s.Lift < d.Lift * r.ChargeBelow && town.Treasury > ChargeCost(town, s) + 300) Charge(town, s.Id);
        }
        foreach (var s in Ships(town).Where(s => s.DownedRegion != null && s.SalvageDay == null).ToList())
            if (town.Treasury > r.Salvage + 400) Salvage(town, s.Id);
        if (r.Ships.Count > 1 && Ships(town).Count() < 2 && BuildBlocked(town, r.Ships[1].Id) == null && town.Treasury > Fleet.Cost(town, r.Ships[1]) + 800)
            Fleet.Build(town, r.Ships[1].Id);
        // One ship on patrol while another stays home for the companies and the merchant runs.
        if (Ships(town).Count() >= 2 && !Ships(town).Any(s => s.Flight?.Job == "patrol")
            && Ships(town).Where(s => Moored(town, s) && !Skyship.Flying(town, s.Id)).OrderByDescending(s => Fleet.Def(town, s).Guns).ThenBy(s => s.Id).FirstOrDefault() is { } ship)
            Send(town, ship.Id, "patrol");
    }

    private static IReadOnlyList<string> Texts(Town town, string key) => Rules(town).Texts.TryGetValue(key, out var t) && t.Count > 0 ? t : [""];

    internal static string Fill(Town town, Rng rng, string key, Ship s, string where, string foe) =>
        Cap(rng.Pick(Texts(town, key))
            .Replace("{ship}", s.Name, StringComparison.Ordinal)
            .Replace("{captain}", s.Captain, StringComparison.Ordinal)
            .Replace("{where}", where, StringComparison.Ordinal)
            .Replace("{foe}", foe, StringComparison.Ordinal));

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
