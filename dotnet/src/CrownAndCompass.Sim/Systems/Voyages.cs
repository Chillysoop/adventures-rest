namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// The harbour's ships at sea beyond the trade runs (Conner, 2026-10-22: "Adventurers should be able to go on ships... a natural
/// and integrated part of the campaign map"). A company bound for a site out on the ocean boards a free ship, which carries
/// it there and home and fights whatever it meets on the way with the heroes as marines. A ship also sails without a company
/// on jobs of its own: a patrol of the home waters that keeps the corsairs off, a scouting run that charts a sea land, or a
/// shore bombardment of a sea site before a company goes in. Its free marine places take the town's watchmen or hired
/// sellswords (<see cref="Ship.Guard"/>). How fast a ship goes is its kind's speed, then the navigator's roll (the captain's
/// prowess and the best Survival aboard), then the weather (<see cref="Weather"/>, the one place it is read). Every roll here
/// uses the ship's own dice, never the town's.
/// </summary>
public static class Voyages
{
    public static FleetRules Rules(Town town) => town.Content.Sea;

    private static VoyageRules V(Town town) => town.Content.Sea.Voyages;

    private static Rng Dice(Town town, int salt) => new(town.Seed * 6151 + town.Day * 43 + salt * 89 + 7);

    /// <summary>Whether a site lies out on the open sea, where only a ship (or wings) takes a company.</summary>
    public static bool Offshore(Town town, SiteState site) => site.RegionId is int rid && World.Sea(town.Regions[rid]);

    // ---- Weather and the navigator ----

    /// <summary>
    /// How much the weather slows a ship today off a land (the home waters when none), as a multiple of its days at sea,
    /// and the chance a storm catches it: the worst of the day's sky there (<see cref="Systems.Weather"/>) against sky.json
    /// "stormStep". This is the one place a voyage reads the sky; the wind is <see cref="Tailwind"/>.
    /// </summary>
    public static (double Slow, double Storm) Weather(Town town, Region? region = null)
    {
        var (x, y) = region == null ? (0.0, 0.0) : Lofted.Spot(region);
        var steps = town.Content.Sky.StormStep;
        int step = Systems.Weather.Worst(town, x, y, town.Day).Step;
        double storm = steps.Length == 0 ? 0 : steps[Math.Min(step, steps.Length - 1)];
        return (1 + V(town).Weather * storm, storm);
    }

    /// <summary>The season's wind on a heading: a multiple of the days at sea, quicker with it behind and slower into it.</summary>
    public static double Tailwind(Town town, double bearing)
    {
        double toward = (bearing - Systems.Weather.Prevailing(town, Systems.Weather.TimeOf(town.Tick)) + 360) % 360 * Math.PI / 180;
        return 1 - town.Content.Sky.Wind * Math.Cos(toward);
    }

    /// <summary>The navigator's roll for a passage: d20 + the captain's prowess + the best Survival aboard, against the DC. Its multiple of the days at sea.</summary>
    public static (double Pace, int Roll) Navigate(Town town, Ship s, int survival, Rng rng)
    {
        var v = V(town);
        int d20 = rng.D20();
        int total = d20 + (int)Math.Floor(s.Prowess - 1) + survival + (Fleet.Has(town, Fleet.Rules(town).Harbour.Lighthouse) ? Fleet.Rules(town).Harbour.Light : 0);
        double pace = d20 == 20 ? v.Navigate[0] : d20 == 1 ? v.Navigate[3] : total >= v.NavigateDc ? v.Navigate[1] : v.Navigate[2];
        return (pace, total);
    }

    /// <summary>Hours at sea for each hour of march: the ship's speed, the navigator's roll and the weather.</summary>
    public static double SailPace(Town town, Ship s, int survival, Rng rng, Region? region = null, double? bearing = null) =>
        Js.Round(Fleet.Def(town, s).Speed * Navigate(town, s, survival, rng).Pace * Weather(town, region).Slow * (bearing is double b ? Tailwind(town, b) : 1) * 100) / 100;

    // ---- Ships free for a passage or a job ----

    /// <summary>The company a ship carries, if it is out with one.</summary>
    public static Party? Carrying(Town town, Ship s) => town.Parties.FirstOrDefault(p => p.ShipId == s.Id && !p.Returned);

    /// <summary>At its mooring and fit to sail: not on a trade run or a job, not carrying a company, with hull left.</summary>
    public static bool Moored(Town town, Ship s) =>
        !Fleet.Aloft(town, s) && s.Out == null && s.Flight == null && s.Hull >= Fleet.Def(town, s).Hull * 0.25 && Carrying(town, s) == null;

    /// <summary>The harbour's ships that could take a company over the open sea now, best first: the most marine places, then the fastest.</summary>
    public static IEnumerable<Ship> Free(Town town) =>
        Fleet.Harbour(town) == null || !Fleet.ToSea(town) ? [] : Fleet.AtSea(town).Where(s => Moored(town, s)).OrderByDescending(s => Fleet.Def(town, s).Marines).ThenBy(s => Fleet.Def(town, s).Speed).ThenBy(s => s.Id);

    /// <summary>Whether a company can get to a site at all: one out on the open sea needs every member able to cross water, or a free ship.</summary>
    public static bool CanReach(Town town, IReadOnlyList<Agent> members, SiteState site) =>
        !Offshore(town, site) || (Passage.Abilities(town, members) & Ways.Water) != 0 || Free(town).Any();

    // ---- A company aboard ----

    /// <summary>
    /// A company setting out over the open sea takes the harbour's best free ship, out and home again: the mind it plans
    /// with sails at the ship's pace, and whatever finds the ship on the way is fought with the heroes at the rail.
    /// </summary>
    public static RouteMind Embark(Town town, Party party, IReadOnlyList<Agent> members, RouteMind mind, (double X, double Y) from, (double X, double Y) to)
    {
        // Those who fly or walk on water, or ride the Sky Dock's airship, need no ship.
        if ((mind.Ways & Ways.Water) != 0 || party.Airship) return mind;
        if (!town.Terrain.Any(f => f.Kind.Ship && Crosses(f, from, to))) return mind;
        var ship = Free(town).FirstOrDefault();
        if (ship == null) return mind;
        party.ShipId = ship.Id;
        var rng = Dice(town, ship.Id * 3 + party.Id);
        int survival = mind.Navigator != "" ? mind.NavBonus : 0;
        var where = town.Regions.Where(r => World.Sea(r)).OrderBy(r => Distance(r, to)).ThenBy(r => r.Id).FirstOrDefault();
        double pace = SailPace(town, ship, survival, rng, where, Math.Atan2(to.X - from.X, from.Y - to.Y) * 180 / Math.PI);
        town.Log(LedgerCategory.Quest, Importance.Routine, $"{party.Name} go aboard {ship.Name} at the harbour, {ship.Captain} at the helm, bound over the open sea.");
        // Whatever finds the ship on the way out is met now; the heroes stand at the rail.
        if (rng.Chance(MeetChance(town, where)) && Meet(town, rng, where) is { } foe)
        {
            var heroes = members.Where(a => a.Hero != null).Select(a => a.Id).Take(Fleet.Def(town, ship).Marines).ToList();
            string outcome = ShipFight.Resolve(town, ship, foe, where, heroes, Guards(town, ship, heroes.Count), false, rng, out _);
            Record(town, ship, foe, where, outcome);
            party.Said.Add(Line(town, rng, outcome, ship, where?.Name ?? "the open sea", foe.Name, heroes));
            if (outcome == "wrecked")
            {
                town.Ships.Remove(ship);
                party.ShipId = null;
                pace = Js.Round(pace * 1.5 * 100) / 100;
                party.Said.Add($"They make the rest of the way in {ship.Name}'s boats.");
            }
        }
        return mind with { Ways = mind.Ways | Ways.Water, Carrier = ship.Name, Sail = pace };
    }

    /// <summary>Whether the town's own ship would carry a company from here to there: the way crosses open sea and a ship is free.</summary>
    public static bool Sails(Town town, (double X, double Y) from, (double X, double Y) to) =>
        Free(town).Any() && town.Terrain.Any(f => f.Kind.Ship && Crosses(f, from, to));

    /// <summary>
    /// What a company that sailed in the town's own ship brings home in its hold besides the take: salvage and trade picked
    /// up on the way, more the farther out it went. A company the Sky Dock lent an airship brings nothing extra.
    /// </summary>
    public static double Hold(Town town, Party party, SiteState site, bool earned)
    {
        if (!earned || party.ShipId is not int id || town.Ships.FirstOrDefault(s => s.Id == id) is not { } ship || Fleet.Aloft(town, ship)) return 0;
        int ring = site.RegionId is int rid ? town.Regions[rid].Ring : 0;
        double gold = Js.Round(V(town).Hold * (1 + ring));
        if (gold > 0) town.Log(LedgerCategory.Quest, Importance.Routine, $"{ship.Name}'s hold comes home with the {party.Name}: {Js.Str(gold)} gold of salvage and trade besides.");
        return gold;
    }

    private static bool Crosses(Feature f, (double X, double Y) a, (double X, double Y) b)
    {
        for (int i = 0; i <= 40; i++)
            if (Roads.Contains(f, a.X + (b.X - a.X) * i / 40, a.Y + (b.Y - a.Y) * i / 40)) return true;
        return false;
    }

    private static double Distance(Region r, (double X, double Y) p)
    {
        double a = r.Bearing * Math.PI / 180;
        return Js.Hypot(Math.Sin(a) * r.Days - p.X, -Math.Cos(a) * r.Days - p.Y);
    }

    // ---- Guards ----

    /// <summary>The ship's marine places the heroes leave, filled by its guard: watchmen lent from the town, or hired sellswords.</summary>
    public static int GuardPlaces(Town town, Ship s, int heroes) => s.Guard == "" ? 0 : Math.Max(0, Fleet.Def(town, s).Marines - heroes);

    /// <summary>What the guard at the rail adds to a boarding.</summary>
    public static double Guards(Town town, Ship s, int heroes)
    {
        var g = Rules(town).Guards;
        int n = GuardPlaces(town, s, heroes);
        return s.Guard == "watch" ? Math.Min(n, town.Watch) * g.Watch.Fight : s.Guard == "hired" ? n * g.Hired.Fight : 0;
    }

    /// <summary>Watchmen away at sea on the town's ships: gone from its defence while they are.</summary>
    public static int WatchAway(Town town) =>
        Math.Min(town.Watch, Fleet.AtSea(town).Where(s => s.Guard == "watch" && (s.Flight != null || Carrying(town, s) != null)).Sum(s => GuardPlaces(town, s, s.Flight?.Marines.Count ?? 0)));

    /// <summary>Defence the town lacks while its watchmen are at sea (<see cref="Threat.Defense"/>).</summary>
    public static double Defense(Town town) => -WatchAway(town) * town.Content.Enemy.Defense.Watchman * Planning.WatchFactor(town);

    public static bool SetGuard(Town town, int shipId, string guard)
    {
        var s = town.Ships.FirstOrDefault(x => x.Id == shipId);
        if (s == null || Fleet.Aloft(town, s) || guard is not ("" or "watch" or "hired")) return false;
        s.Guard = guard;
        return true;
    }

    // ---- Jobs ----

    /// <summary>Days out and home to a sea land, moored a day at the far end: the ship's pace, the navigator's roll and the weather.</summary>
    public static int Days(Town town, Ship s, Region region, Rng? rng = null) =>
        2 * Math.Max(1, (int)Math.Ceiling(Math.Max(1, region.Days) * (rng == null ? Fleet.Def(town, s).Speed * Weather(town, region).Slow : SailPace(town, s, 0, rng, region)) - 1e-9)) + Rules(town).Turnaround;

    public static IEnumerable<Agent> Marines(Town town) => Sky.Marines(town);

    /// <summary>Food the crew and the guard eat on a job.</summary>
    public static double Rations(Town town, Ship s, int days) => Js.Round((Fleet.Def(town, s).Crew + GuardPlaces(town, s, 0)) * days * Rules(town).Ration * 10) / 10;

    /// <summary>Gold the hired marines want for a job.</summary>
    public static double Wages(Town town, Ship s, int heroes, int days) => s.Guard == "hired" ? GuardPlaces(town, s, heroes) * Rules(town).Guards.Hired.Wage * days : 0;

    /// <summary>Why the ship can't sail this job now, or null.</summary>
    public static string? Refuse(Town town, Ship s, string job, int regionId, string siteId, IReadOnlyList<int> marines)
    {
        if (Fleet.Aloft(town, s)) return "an airship flies from the Sky Dock";
        if (Fleet.Harbour(town) == null) return "needs a harbour";
        if (!Moored(town, s)) return s.Hull < Fleet.Def(town, s).Hull * 0.25 ? "needs patching first" : "already at sea";
        if (job is not ("patrol" or "scout" or "strike")) return "no such job";
        if (job != "patrol" && !Fleet.ToSea(town)) return "the river doesn't reach the sea";
        if (marines.Count > Fleet.Def(town, s).Marines) return $"carries {Fleet.Def(town, s).Marines} marines at most";
        var free = Marines(town).Select(h => h.Id).ToHashSet();
        if (marines.Any(id => !free.Contains(id))) return "a hero is away or hurt";
        int days = V(town).PatrolDays;
        if (job != "patrol")
        {
            if (regionId < 0 || regionId >= town.Regions.Count || !World.Sea(town.Regions[regionId])) return "not a sea land";
            if (!town.Regions[regionId].Known && job == "strike") return "the town doesn't know that sea";
            var target = town.Sites.FirstOrDefault(x => x.Def.Id == siteId);
            if (job == "strike" && (target == null || target.Cleared || target.RegionId != regionId || target.Known == SiteKnowledge.Unheard))
                return "no lair the town knows of there";
            days = Days(town, s, town.Regions[regionId]);
        }
        if (town.Market.Stock(Good.Food) < Rations(town, s, days)) return "not enough food for the crew";
        if (town.Treasury < Wages(town, s, marines.Count, days)) return "not enough gold for the marines";
        return null;
    }

    public static bool Send(Town town, int shipId, string job, int regionId = -1, string siteId = "", IReadOnlyList<int>? marines = null)
    {
        var s = town.Ships.FirstOrDefault(x => x.Id == shipId);
        marines ??= [];
        if (s == null || Refuse(town, s, job, regionId, siteId, marines) != null) return false;
        var rng = Dice(town, s.Id);
        int days = job == "patrol" ? V(town).PatrolDays : Days(town, s, town.Regions[regionId], rng);
        town.Market.Seed(Good.Food, -Rations(town, s, days));
        double wages = Wages(town, s, marines.Count, days);
        if (wages > 0) town.Book(TreasuryAccount.Works, -wages);
        s.Flight = new SkyFlight { Job = job, RegionId = job == "patrol" ? -1 : regionId, SiteId = siteId, SetOutTick = town.Tick, BackDay = town.Day + days, Marines = [.. marines] };
        foreach (int id in marines) town.Agent(id).Hero!.AwayUntil = town.Tick + days * SimTime.HoursPerDay;
        string where = job == "patrol" ? "the home waters" : job == "strike" ? town.Sites.First(x => x.Def.Id == siteId).Def.Name : town.Regions[regionId].Name;
        town.Log(LedgerCategory.Town, Importance.Notable, Fill(town, rng, "sail", s, where, "") + $" Home in {days} days.");
        return true;
    }

    /// <summary>Ships on patrol in the home waters: each turns away a share of the corsairs' raids on the harbour.</summary>
    public static int Patrolling(Town town) => Fleet.AtSea(town).Count(s => s.Flight?.Job == "patrol");

    /// <summary>Share of corsair raids on the harbour the patrols turn away.</summary>
    public static double RaidGuard(Town town) => Math.Min(0.9, Patrolling(town) * V(town).PatrolGuard);

    // ---- The day ----

    /// <summary>Daily, from <see cref="Fleet.Daily"/>: ships on jobs sail on, meet what they meet, and come home on their day.</summary>
    public static void Daily(Town town)
    {
        foreach (var s in Fleet.AtSea(town).Where(s => s.Flight != null).ToList()) Sail(town, s, s.Flight!);
    }

    private static void Sail(Town town, Ship s, SkyFlight f)
    {
        var v = V(town);
        var d = Fleet.Def(town, s);
        var rng = Dice(town, s.Id);
        var region = f.RegionId >= 0 ? town.Regions[f.RegionId] : null;
        string where = region?.Name ?? "the home waters";
        s.Hull = Math.Max(1, s.Hull - d.Wear);
        var (_, storm) = Weather(town, region);
        if (!f.Stormed && rng.Chance(storm))
        {
            f.Stormed = true;
            f.BackDay += 1;
            town.Log(LedgerCategory.Town, Importance.Routine, Fill(town, rng, "storm", s, where, ""));
        }
        if (rng.Chance(MeetChance(town, region)) && Meet(town, rng, region) is { } foe)
        {
            string outcome = ShipFight.Resolve(town, s, foe, region, f.Marines, Guards(town, s, f.Marines.Count), f.Stormed && town.Day <= f.BackDay, rng, out _);
            Record(town, s, foe, region, outcome);
            town.Log(LedgerCategory.War, outcome == "evaded" ? Importance.Routine : Importance.Notable, Line(town, rng, outcome, s, where, foe.Name, f.Marines));
            if (outcome == "wrecked")
            {
                Lost(town, s, f, region);
                return;
            }
        }
        // Off the sea site, the guns go to work: a day's shot each day it is in reach.
        if (f.Job == "strike" && town.Sites.FirstOrDefault(x => x.Def.Id == f.SiteId) is { Cleared: false } && region != null
            && town.Day >= f.BackDay - Days(town, s, region) / 2 - 1 && f.Struck < v.StrikeMost)
            f.Struck = Math.Min(v.StrikeMost, f.Struck + v.StrikeShare * Math.Max(1, d.Guns) * (1 + 0.1 * (s.Prowess - 1)));
        if (town.Day >= f.BackDay) Home(town, s, f, region, rng);
    }

    /// <summary>The chance something finds a ship at sea today: more far out.</summary>
    public static double MeetChance(Town town, Region? region)
    {
        var v = V(town);
        return Math.Min(v.MeetMax, v.Meet + v.MeetPerRing * (region?.Ring ?? 0));
    }

    private static ShipFoe? Meet(Town town, Rng rng, Region? region)
    {
        var r = Rules(town);
        int ring = region?.Ring ?? 0;
        var kinds = r.Foes.Where(k => ring >= k.Rings[0] && ring <= k.Rings[^1]).ToList();
        if (kinds.Count == 0) return null;
        var k = rng.Pick(kinds);
        return new ShipFoe(k.Name, k.Power + r.PowerPerRing * ring, k.Speed, k.Boards);
    }

    private static void Record(Town town, Ship s, ShipFoe foe, Region? region, string outcome)
    {
        var battles = town.SkyFleet.Battles;
        battles.Add(new SkyBattle { Tick = town.Tick, ShipId = s.Id, Foe = foe.Name, RegionId = region?.Id ?? -1, Outcome = outcome });
        if (battles.Count > 12) battles.RemoveAt(0);
    }

    /// <summary>A ship lost at sea: its marines come home in the boats, the ship is gone.</summary>
    private static void Lost(Town town, Ship s, SkyFlight f, Region? region)
    {
        foreach (var a in f.Marines.Select(town.Agent).Where(a => !a.Dead && a.Hero != null)) a.Hero!.AwayUntil = town.Tick + (2 + (region?.Ring ?? 0)) * SimTime.HoursPerDay;
        s.Flight = null;
        town.Ships.Remove(s);
    }

    /// <summary>Home from a job: a scout's charts, a bombardment's toll on the site, and the marines back in town.</summary>
    private static void Home(Town town, Ship s, SkyFlight f, Region? region, Rng rng)
    {
        s.Flight = null;
        s.Voyages++;
        s.Prowess = Math.Min(5, s.Prowess + Rules(town).Learn * 0.5);
        foreach (var a in f.Marines.Select(town.Agent).Where(a => !a.Dead && a.Hero != null)) a.Hero!.AwayUntil = null;
        if (f.Job == "scout" && region != null)
        {
            Sky.Charted(town, region.Id, (0, 0), Sky.Middle(region), s.Name);
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
        else if (f.Job == "strike" && region != null && town.Sites.FirstOrDefault(x => x.Def.Id == f.SiteId) is { } site)
        {
            double share = site.Cleared ? 0 : f.Struck;
            site.Strength = Math.Max(0, site.Strength * (1 - share));
            site.Found = true;
            s.LastLine = Fill(town, rng, "struck", s, region.Name, "").Replace("{site}", site.Def.Name, StringComparison.Ordinal)
                .Replace("{share}", $"{Js.Str(Js.Round(share * 100))}%", StringComparison.Ordinal);
        }
        else s.LastLine = Fill(town, rng, "patrolled", s, "the home waters", "");
        town.Log(LedgerCategory.Town, Importance.Notable, s.LastLine);
    }

    // ---- Telling ----

    private static string Line(Town town, Rng rng, string outcome, Ship s, string where, string foe, IReadOnlyList<int> marines)
    {
        string names = Expedition.ListNames(marines.Select(town.Agent).Where(a => !a.Dead).Select(a => a.GivenName).DefaultIfEmpty("the crew").ToList());
        return Fill(town, rng, outcome, s, where, foe).Replace("{marines}", names, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> Texts(Town town, string key) => V(town).Texts.TryGetValue(key, out var t) && t.Count > 0 ? t : [""];

    private static string Fill(Town town, Rng rng, string key, Ship s, string where, string foe) =>
        Cap(rng.Pick(Texts(town, key))
            .Replace("{ship}", s.Name, StringComparison.Ordinal)
            .Replace("{captain}", s.Captain, StringComparison.Ordinal)
            .Replace("{where}", where, StringComparison.Ordinal)
            .Replace("{foe}", foe, StringComparison.Ordinal));

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
