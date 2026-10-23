namespace CrownAndCompass.Sim.Systems;

/// <summary>Exploring companies and what they find (content/game/explore.json).</summary>
public sealed record ExploreRules
{
    public CommissionRules Commission { get; init; } = new();

    /// <summary>Days a company wanders a region before turning home, and the days more each "keep exploring" buys once that is up.</summary>
    public int ExploreDays { get; init; } = 3;

    /// <summary>Rune-lit stone circles the town holds once a company wakes one (<see cref="Circles"/>).</summary>
    public CircleRules Circles { get; init; } = new();
    public int KeepDays { get; init; } = 2;

    /// <summary>How close (days of march) a company must pass to a place to find it.</summary>
    public double Sight { get; init; } = 0.5;

    /// <summary>Chance each day in the region of meeting something on the move: a company, a caravan, a font, a ruin.</summary>
    public double EncounterChance { get; init; } = 0.4;

    /// <summary>Hours the company waits on the mayor's word before its leader decides.</summary>
    public int DecideHours { get; init; } = 36;

    /// <summary>Hours a bird takes to carry a report home when nobody can cast a sending.</summary>
    public int BirdHours { get; init; } = 10;

    /// <summary>Added to a chapter's check for every time it failed before: each try teaches them something.</summary>
    public int ClueBonus { get; init; } = 2;

    /// <summary>A company this hurt (share of hit points) turns home on its own.</summary>
    public double HomeHp { get; init; } = 0.45;
    public List<FindKindDef> Kinds { get; init; } = [];

    /// <summary>How likely each kind of chance meeting is.</summary>
    public Dictionary<string, double> Weights { get; init; } = [];
    public ExploreRewards Rewards { get; init; } = new();
    public Dictionary<string, string> Texts { get; init; } = [];

    public FindKindDef? Kind(string id) => Kinds.FirstOrDefault(k => k.Id == id);
}

public sealed record CommissionRules
{
    public double Base { get; init; } = 40;
    public double PerRing { get; init; } = 35;
    public int Days { get; init; } = 12;
    public int MinHeroes { get; init; } = 2;
    public int MaxHeroes { get; init; } = 4;
    public int AutoEveryDays { get; init; } = 10;
    public double AutoReserve { get; init; } = 150;

    /// <summary>A spot the mayor marks on the map: days the company looks round it, how far round it (days of march), and how many may be out at once.</summary>
    public int SpotDays { get; init; } = 1;
    public double SpotRadius { get; init; } = 0.35;
    public int MaxSpots { get; init; } = 3;
}

public sealed record ExploreRewards
{
    public double Weakness { get; init; } = 0.6;
    public double Friends { get; init; } = 18;
    public double Parley { get; init; } = 8;
    public double Offend { get; init; } = -6;
    public int MapsTrust { get; init; } = 75;
    public int InviteDays { get; init; } = 4;
    public double EscortGold { get; init; } = 30;
    public double EscortFood { get; init; } = 40;
    public double Bargain { get; init; } = 25;
    public int AttuneXp { get; init; } = 150;
    public double VaultGold { get; init; } = 120;
}

/// <summary>A kind of thing explorers find, the chapters of investigating it, and what else they can do about it.</summary>
public sealed record FindKindDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>The report that reaches the mayor ({leader}, {what}, {region}, {days}, {sign}, {settlement}, {band}, {count}, {wagons}).</summary>
    public string Found { get; init; } = "";
    public List<string> Signs { get; init; } = [];
    public List<string> Bands { get; init; } = [];
    public List<ChapterDef> Chapters { get; init; } = [];
    public List<FindOptionDef> Options { get; init; } = [];
}

/// <summary>One step of investigating a find: time spent, a party check against a DC, a risk if it fails, and a reward on the last.</summary>
public sealed record ChapterDef
{
    public string Title { get; init; } = "";
    public string Text { get; init; } = "";

    /// <summary>Game days the step takes (a look over a caravan is part of a day, reading a ruin's walls several); <see cref="Hours"/> when unset.</summary>
    public double Days { get; init; }
    public int Hours { get; init; } = 8;

    /// <summary>The hours the step takes.</summary>
    public int Span => Days > 0 ? (int)Js.Round(Days * SimTime.HoursPerDay) : Hours;
    public string Skill { get; init; } = "";
    public int Dc { get; init; } = 12;

    /// <summary>Added to the DC for each level the place is built for (lairs).</summary>
    public int DcPerTier { get; init; }

    /// <summary>alert, fight, wound, time or offend.</summary>
    public string Risk { get; init; } = "wound";
    public string Reward { get; init; } = "";
    public string Success { get; init; } = "";
    public string Failure { get; init; } = "";
}

/// <summary>Something a company can do about a find besides investigating: engage, parley, trade news, escort...</summary>
public sealed record FindOptionDef
{
    public string Act { get; init; } = "";
    public string Label { get; init; } = "";
    public string Skill { get; init; } = "";
    public int Dc { get; init; }
    public double Cost { get; init; }
}

/// <summary>The council's offer to pay a company to explore a region.</summary>
public sealed class Commission
{
    public int Id { get; init; }
    public int RegionId { get; init; }
    public double Reward { get; init; }
    public int PostedTick { get; init; }
    public int ExpiresTick { get; init; }
    public BountyStatus Status { get; set; } = BountyStatus.Open;
    public int? SurveyId { get; set; }

    /// <summary>A spot the mayor marked on the map (days of march east and south of the town), for a company to go and look at.</summary>
    public double? SpotX { get; init; }
    public double? SpotY { get; init; }

    /// <summary>The painted zone it was posted for (<see cref="Wilds"/>), if any.</summary>
    public int? ZoneId { get; set; }
}

public enum SurveyPhase
{
    Outbound,
    Exploring,

    /// <summary>Stopped where it found something, waiting for the mayor's word.</summary>
    Waiting,

    /// <summary>Working through a chapter of a find.</summary>
    Investigating,
    Homeward,
    Home,

    /// <summary>Went in to fight; the members are a <see cref="Party"/> now.</summary>
    Engaged,
    Lost,
}

/// <summary>A stretch of a company's march: from a spot along a road, starting at a moment, cut short at Stop.</summary>
public sealed class SurveyLeg
{
    public double FromX { get; init; }
    public double FromY { get; init; }
    public List<Waypoint> Route { get; init; } = [];
    public double Start { get; init; }
    public double? Stop { get; set; }
    public string Kind { get; init; } = "wander";
}

/// <summary>A company out exploring a region on a council commission.</summary>
public sealed class Survey
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public List<int> Members { get; } = [];
    public int RegionId { get; init; }
    public int? CommissionId { get; init; }

    /// <summary>The marked spot it was sent to look at, if it was sent to one rather than to explore a whole region.</summary>
    public double? SpotX { get; init; }
    public double? SpotY { get; init; }

    /// <summary>The painted zone it was sent to walk, if any.</summary>
    public int? ZoneId { get; set; }
    public int DepartTick { get; init; }
    public SurveyPhase Phase { get; set; }
    public List<SurveyLeg> Legs { get; } = [];

    /// <summary>When it turns for home, unless told to keep exploring.</summary>
    public int ExploreUntil { get; set; }

    /// <summary>The find it is stopped at, and when the chapter in hand is done.</summary>
    public int? AtFind { get; set; }
    public int? ResumeTick { get; set; }
    public int ReturnTick { get; set; }
    public int Sendings { get; set; }

    /// <summary>Guarding a caravan home: it explores no more.</summary>
    public bool Escorting { get; set; }
    public List<int> Finds { get; } = [];

    /// <summary>The company's own account of the trip, told at the tavern when it is home.</summary>
    public List<string> Journal { get; } = [];

    /// <summary>Painted cells it walked that the town hasn't heard of yet ("zone:x:y"), told when it is home (<see cref="Wilds.Told"/>).</summary>
    public List<string> Saw { get; } = [];

    /// <summary>The day each find was rolled for, so a day in the region rolls once.</summary>
    public int RolledDay { get; set; } = -1;
}

public enum FindState
{
    /// <summary>Waiting on the mayor's (or the leader's) word.</summary>
    Open,

    /// <summary>A chapter of it is being worked.</summary>
    Busy,
    Done,
}

/// <summary>Something an exploring company came on: a lair, a settlement, another company, a caravan, a font, a ruin.</summary>
public sealed class Find
{
    public int Id { get; init; }
    public int SurveyId { get; init; }
    public string Kind { get; init; } = "";
    public string? SiteId { get; init; }
    public int? SettlementId { get; init; }
    public int RegionId { get; init; }

    /// <summary>What it is called in the reports: the {band} of a company, the sign of a font or ruin.</summary>
    public string Band { get; init; } = "";
    public string Sign { get; init; } = "";
    public int Count { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public int FoundTick { get; init; }
    public FindState State { get; set; }

    /// <summary>Chapters investigated so far, and failed tries since the last one went well.</summary>
    public int Chapter { get; set; }
    public int Clues { get; set; }

    /// <summary>When the report reaches the mayor (a bird is slower than a sending), and when the company stops waiting for an answer.</summary>
    public int ReportTick { get; set; }
    public int DueTick { get; set; }

    /// <summary>The town knows of it: reported by sending or bird, or told at the tavern once the company is home.</summary>
    public bool Known { get; set; }

    /// <summary>Its story so far, oldest first: the report, each chapter, each choice.</summary>
    public List<string> Log { get; } = [];

    /// <summary>The last check rolled on it, for the client's die.</summary>
    public StoryBeat? Beat { get; set; }

    /// <summary>Invited company heroes ride in on this day.</summary>
    public int? InviteDay { get; set; }
}

/// <summary>What the mayor can tell explorers to do about a find.</summary>
public sealed record FindChoice(string Act, string Label, string Detail, double Chance, string? Blocked);

/// <summary>
/// Exploring the wilds. The town does not see its sites by itself: someone must tell it (tavern talk,
/// a settlement's runner, an ally's maps) or a company must go and look. The council commissions a
/// company to explore a region; heroes take it like a bounty, march out and wander the region, and
/// whatever they come on (a lair, a settlement, another company, a caravan, a magical font, an old
/// ruin) they report by sending (a spell slot) or by bird, and wait for word before doing anything
/// about it. The mayor can have them investigate further, a chapter at a time (hours of work, a party
/// check, a risk if it goes wrong and more learned if it goes right, the last chapter bringing its
/// reward), go in and fight, deal with it as its kind allows, keep exploring, or come home. Left
/// unanswered, the leader decides; the stand-in mayor answers headless.
/// </summary>
public static class Explore
{
    private static ExploreRules Rules(Town town) => town.Content.Explore;

    public static Survey? Survey(Town town, int id) => town.Surveys.FirstOrDefault(s => s.Id == id);

    public static Find? FindOf(Town town, int id) => town.Finds.FirstOrDefault(f => f.Id == id);

    public static Commission? CommissionOf(Town town, int id) => town.Commissions.FirstOrDefault(c => c.Id == id);

    public static bool Out(Survey s) => s.Phase is not (SurveyPhase.Home or SurveyPhase.Engaged or SurveyPhase.Lost);

    /// <summary>Finds waiting on the mayor's word right now, oldest first.</summary>
    public static List<Find> Waiting(Town town) =>
        town.Finds.Where(f => f.State == FindState.Open && f.Known && town.Tick >= f.ReportTick).OrderBy(f => f.Id).ToList();

    public static double SuggestedReward(Town town, Region r) => Rules(town).Commission.Base + Rules(town).Commission.PerRing * r.Ring;

    public static bool Active(Commission c) => c.Status is BountyStatus.Open or BountyStatus.Taken;

    /// <summary>Regions a company could be sent to explore: heard of, and not being explored already.</summary>
    public static bool CanCommission(Town town, int regionId) =>
        regionId >= 0 && regionId < town.Regions.Count && town.Regions[regionId].Known && !town.Commissions.Any(c => c.RegionId == regionId && c.SpotX == null && c.ZoneId == null && Active(c));

    /// <summary>Spots marked for a company to look at that nobody has finished with yet.</summary>
    public static List<Commission> Spots(Town town) => town.Commissions.Where(c => c.SpotX != null && Active(c)).OrderBy(c => c.Id).ToList();

    /// <summary>The region a spot lies in: the ring nearest its distance, then the region of that ring nearest its bearing.</summary>
    public static Region? RegionAt(Town town, double x, double y)
    {
        if (town.Regions.Count == 0) return null;
        double d = Js.Hypot(x, y);
        double bearing = (Math.Atan2(x, -y) * 180 / Math.PI + 360) % 360;
        double Off(Region r)
        {
            double a = Math.Abs(r.Bearing - bearing) % 360;
            return Math.Min(a, 360 - a);
        }
        int ring = town.Regions.OrderBy(r => Math.Abs(r.Days - d)).ThenBy(r => r.Ring).First().Ring;
        return town.Regions.Where(r => r.Ring == ring).OrderBy(Off).ThenBy(r => r.Id).First();
    }

    /// <summary>Why a company can't be sent to look at this spot, or null when it can.</summary>
    public static string? SpotBlocked(Town town, double x, double y)
    {
        var rules = Rules(town).Commission;
        double d = Js.Hypot(x, y);
        if (d < rules.SpotRadius) return "that is the town itself";
        if (town.Regions.Count == 0 || d > town.Regions.Max(r => r.Days) + 1) return "too far for any company to walk";
        if (Spots(town).Count >= rules.MaxSpots) return $"{rules.MaxSpots} spots are marked already";
        if (Spots(town).Any(c => Js.Hypot(c.SpotX!.Value - x, c.SpotY!.Value - y) < rules.SpotRadius)) return "a company is already asked to look there";
        if (town.Treasury < SpotReward(town, x, y)) return "not enough gold";
        return null;
    }

    /// <summary>What the council offers for a look at a spot: what exploring its region would pay.</summary>
    public static double SpotReward(Town town, double x, double y) => RegionAt(town, x, y) is { } r ? SuggestedReward(town, r) : 0;

    /// <summary>
    /// Mark a spot on the map for a company to go and look at: like a commission to explore a region, but the company walks
    /// straight there, looks round it for a day, and comes home to say what it saw. It may be a spot nobody has heard of.
    /// </summary>
    public static Commission? PostSpot(Town town, double x, double y, double? reward = null)
    {
        if (SpotBlocked(town, x, y) != null) return null;
        var region = RegionAt(town, x, y)!;
        double pay = reward ?? SpotReward(town, x, y);
        if (pay <= 0 || town.Treasury < pay) return null;
        town.Book(TreasuryAccount.Exploring, -pay);
        var c = new Commission
        {
            Id = town.Commissions.Count + 1,
            RegionId = region.Id,
            Reward = pay,
            PostedTick = town.Tick,
            ExpiresTick = town.Tick + Rules(town).Commission.Days * SimTime.HoursPerDay,
            SpotX = x,
            SpotY = y,
        };
        town.Commissions.Add(c);
        town.Log(LedgerCategory.Town, Importance.Notable, $"The council marks a spot {Bearing(x, y)} on the map, {Js.Str(Js.Round(Js.Hypot(x, y) * 10) / 10)} days out{(region.Known ? $" in {region.Name}" : "")}, and offers {Js.Str(pay)} gold to any company that will go and see what is there.");
        return c;
    }

    /// <summary>"to the north-east": the compass point a spot lies toward from the town.</summary>
    public static string Bearing(double x, double y)
    {
        string[] points = ["north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west"];
        double b = (Math.Atan2(x, -y) * 180 / Math.PI + 360) % 360;
        return "to the " + points[(int)Math.Round(b / 45) % 8];
    }

    /// <summary>Where a commission sends a company: its marked spot, or its region's name.</summary>
    private static string Goal(Town town, Commission c) =>
        c.ZoneId is int zid && Wilds.Of(town, zid) is { } z ? $"{Wilds.Name(town, z)} the council painted"
        : c.SpotX is double x && c.SpotY is double y ? $"the spot the council marked {Bearing(x, y)}" : town.Regions[c.RegionId].Name;

    /// <summary>
    /// Post the next trip into a painted explore zone: the pay for the cells still unseen, held from the treasury until the
    /// company is home. Nothing is posted while the treasury can't spare it; the zone waits.
    /// </summary>
    public static Commission? PostZone(Town town, WildZone z)
    {
        if (z.Kind != Wilds.Explore || Wilds.Unseen(z) == 0 || Wilds.Busy(town, z)) return null;
        var (mx, my) = Wilds.Middle(town, z);
        var region = RegionAt(town, mx, my);
        if (region == null) return null;
        double pay = Wilds.TripReward(town, z);
        if (pay <= 0 || town.Treasury < pay) return null;
        town.Book(TreasuryAccount.Exploring, -pay);
        var c = new Commission
        {
            Id = town.Commissions.Count + 1,
            RegionId = region.Id,
            Reward = pay,
            PostedTick = town.Tick,
            ExpiresTick = town.Tick + Rules(town).Commission.Days * SimTime.HoursPerDay,
            ZoneId = z.Id,
        };
        town.Commissions.Add(c);
        town.Log(LedgerCategory.Town, Importance.Routine, $"The council offers {Js.Str(pay)} gold to any company that will walk {Wilds.Name(town, z)} it painted, {Wilds.Unseen(z)} {(Wilds.Unseen(z) == 1 ? "stretch" : "stretches")} nobody has seen.");
        return c;
    }

    /// <summary>The player's lever: pay a company to explore a region. The reward is held from the treasury until they come home.</summary>
    public static Commission? Post(Town town, int regionId, double? reward = null)
    {
        if (!CanCommission(town, regionId)) return null;
        var region = town.Regions[regionId];
        double pay = reward ?? SuggestedReward(town, region);
        if (pay <= 0 || town.Treasury < pay) return null;
        if (Stewards.Intercept(town, "bounties", new StewardAsk { Area = "bounties", Kind = "survey", Title = $"Survey {region.Name} · {Js.Str(pay)}g", Why = "• Unknown country nearby\n• A company maps it and brings back finds", I1 = regionId, D1 = pay })) return null;
        town.Book(TreasuryAccount.Exploring, -pay);
        var c = new Commission
        {
            Id = town.Commissions.Count + 1,
            RegionId = regionId,
            Reward = pay,
            PostedTick = town.Tick,
            ExpiresTick = town.Tick + Rules(town).Commission.Days * SimTime.HoursPerDay,
        };
        town.Commissions.Add(c);
        town.Log(LedgerCategory.Town, Importance.Notable, $"The council offers {Js.Str(pay)} gold to any company that will go and explore {region.Name} and send word of what it finds.");
        return c;
    }

    // ---------- The daily tick ----------

    public static void Daily(Town town, bool autoMayor)
    {
        var rules = Rules(town);
        foreach (var c in town.Commissions)
        {
            if (c.Status != BountyStatus.Open || town.Tick < c.ExpiresTick) continue;
            c.Status = BountyStatus.Expired;
            town.Book(TreasuryAccount.Exploring, c.Reward);
            town.Log(LedgerCategory.Town, Importance.Routine, $"No company will go and look at {Goal(town, c)}; the council withdraws the offer.");
        }
        Invited(town);
        if (autoMayor) AutoPost(town);
        Form(town);
        // A day in the region can bring a chance meeting.
        foreach (var s in town.Surveys)
        {
            if (s.Phase != SurveyPhase.Exploring || s.Escorting || s.RolledDay == town.Day) continue;
            s.RolledDay = town.Day;
            if (town.Rng.Chance(rules.EncounterChance)) Meet(town, s);
        }
    }

    /// <summary>Stand-in mayor: keep a company exploring the nearest country with most left to find.</summary>
    private static void AutoPost(Town town)
    {
        var rules = Rules(town).Commission;
        if (town.Commissions.Any(Active) || town.Surveys.Any(Out)) return;
        if (town.Commissions.Count > 0 && town.Tick - town.Commissions[^1].PostedTick < rules.AutoEveryDays * SimTime.HoursPerDay) return;
        if (town.Heroes.Count(Quests.Available) < rules.MinHeroes) return;
        int strongest = town.Heroes.Select(h => h.Hero!.Level).DefaultIfEmpty(1).Max();
        var target = town.Regions
            .Where(r => r.Known && r.Levels[0] <= strongest + 1)
            .Select(r => (Region: r, Left: r.SiteIds.Count(id => Quests.Site(town, id) is { Found: false, Cleared: false })))
            .Where(x => x.Left > 0 || !x.Region.Visited)
            .OrderBy(x => x.Region.Ring)
            .ThenByDescending(x => x.Left)
            .ThenBy(x => x.Region.Id)
            .Select(x => x.Region)
            .FirstOrDefault();
        if (target == null) return;
        double reward = SuggestedReward(town, target);
        if (town.Treasury < reward + rules.AutoReserve) return;
        Post(town, target.Id, reward);
    }

    private static double Interest(Town town, Agent a, Commission c, int size)
    {
        var region = town.Regions[c.RegionId];
        int tier = Math.Max(1, region.Levels[0]);
        double share = c.Reward / Math.Max(1, size);
        return share / (tier * 25)
            + (Watch.CanScout(town, a) ? 0.25 : 0)
            + (a.Has(Trait.Ambitious) ? 0.1 : 0)
            - (a.Has(Trait.Lazy) ? 0.3 : 0)
            + town.Rng.Next() * 0.3;
    }

    /// <summary>Heroes take open commissions: a scout to lead if there is one, a caster to send word if they can, friends after.</summary>
    private static void Form(Town town)
    {
        var rules = Rules(town).Commission;
        foreach (var c in town.Commissions)
        {
            if (c.Status != BountyStatus.Open) continue;
            var region = town.Regions[c.RegionId];
            int tier = Math.Max(1, region.Levels[0]);
            var pool = town.Heroes.Where(h => Muster.Free(town, h)).OrderByDescending(h => Watch.CanScout(town, h)).ThenByDescending(h => h.Hero!.Level).ThenBy(h => h.Id).ToList();
            if (pool.Count < rules.MinHeroes) continue;
            var leader = pool.FirstOrDefault(h => Interest(town, h, c, 3) >= 0.5);
            if (leader == null) continue;
            var members = new List<Agent> { leader };
            bool Sender(Agent a) => Knowledge.Casts(town, a, town.Content.Knowledge.SendSpell);
            var rest = pool.Where(h => h.Id != leader.Id && Muster.Rides(town, leader, h))
                .OrderByDescending(h => !members.Any(Sender) && Sender(h))
                .ThenByDescending(h => leader.OpinionOf(h.Id))
                .ThenByDescending(h => h.Hero!.Level)
                .ThenBy(h => h.Id)
                .ToList();
            foreach (var r in rest)
            {
                if (members.Count >= rules.MaxHeroes) break;
                if (leader.OpinionOf(r.Id) < -10 || r.OpinionOf(leader.Id) < -10) continue;
                if (!Muster.Rides(town, leader, r, members.Count(m => m.Hero!.PartyName == null))) continue;
                if (Interest(town, r, c, members.Count + 1) < 0.4) continue;
                members.Add(r);
            }
            int levels = members.Sum(m => m.Hero!.Level);
            if (members.Count < rules.MinHeroes || levels < 2 * tier) continue;
            Depart(town, c, members);
        }
    }

    private static void Depart(Town town, Commission c, List<Agent> members)
    {
        var region = town.Regions[c.RegionId];
        string? name = members[0].Hero!.PartyName;
        if (name == null || town.Parties.Any(p => !p.Returned && p.Name == name) || town.Surveys.Any(x => Out(x) && x.Name == name)) name = Muster.Name(town);
        var s = new Survey
        {
            Id = town.Surveys.Count + 1,
            Name = name,
            RegionId = region.Id,
            CommissionId = c.Id,
            SpotX = c.SpotX,
            SpotY = c.SpotY,
            ZoneId = c.ZoneId,
            DepartTick = town.Tick,
            Phase = SurveyPhase.Outbound,
        };
        s.Members.AddRange(members.Select(m => m.Id));
        c.Status = BountyStatus.Taken;
        c.SurveyId = s.Id;
        var zone = c.ZoneId is int zid ? Wilds.Of(town, zid) : null;
        if (zone != null) zone.Trips++;
        var entry = zone != null && Wilds.NextCell(town, zone, (0, 0)) is { } first ? first
            : c.SpotX is double sx && c.SpotY is double sy ? (sx, sy) : Entry(town, region);
        var said = new List<string>();
        var route = Roads.Plan(town, (0, 0), entry, Roads.Mind(town, members, false, false) with { Explorer = true }, town.Rng, said);
        s.Legs.Add(new SurveyLeg { FromX = 0, FromY = 0, Route = route, Start = town.Tick, Kind = "out" });
        Country.Wear(town, (0, 0), route);
        s.Journal.AddRange(said);
        town.Surveys.Add(s);
        foreach (var m in members)
        {
            m.Hero!.PartyName = name;
            m.Activity = Activity.Idle;
            m.LocationId = null;
        }
        SetAway(town, s);
        town.Log(
            LedgerCategory.Quest,
            Importance.Notable,
            $"The {name} ({string.Join(", ", members.Select(m => $"{m.GivenName} the {m.Hero!.Cls.Name}"))}) take the council's coin to {(c.SpotX == null ? "explore " : "look at ")}{Goal(town, c)}, and march out at dawn{(Sends(town, s) ? "" : " with a cage of homing birds, since none of them can cast a sending")}.",
            s.Members);
    }

    /// <summary>Where a company enters a region: on its bearing, a little short of its sites.</summary>
    private static (double X, double Y) Entry(Town town, Region r)
    {
        double reach = Math.Max(0.6, r.Days * 0.8);
        double a = r.Bearing * Math.PI / 180;
        return (reach * Math.Sin(a), -reach * Math.Cos(a));
    }

    /// <summary>Where a settlement lies: the heart of its region, a little past its sites.</summary>
    public static (double X, double Y) SettlementSpot(Town town, Settlement st)
    {
        var r = town.Regions[st.RegionId];
        double a = (r.Bearing + r.Arc * 0.18) * Math.PI / 180;
        double reach = r.Days + 0.3;
        return (reach * Math.Sin(a), -reach * Math.Cos(a));
    }

    // ---------- The march ----------

    private static double End(Town town, SurveyLeg l) =>
        l.Stop ?? Roads.Finish(town.Content, l.Start, Roads.Hours(town, (l.FromX, l.FromY), l.Route));

    private static (double X, double Y) On(Town town, SurveyLeg l, double t)
    {
        if (t <= l.Start) return (l.FromX, l.FromY);
        double hours = Roads.Marched(town.Content, l.Start, Math.Min(t, End(town, l)));
        return Roads.Along(town, (l.FromX, l.FromY), l.Route, hours);
    }

    /// <summary>Where a company is at moment t (fractional ticks), in day units from the town.</summary>
    public static (double X, double Y) Where(Town town, Survey s, double t)
    {
        var leg = s.Legs.LastOrDefault(l => l.Start <= t) ?? s.Legs.FirstOrDefault();
        return leg == null ? (0, 0) : On(town, leg, t);
    }

    /// <summary>Whether the company is on the move at moment t (not camped, resting, or stopped at a find).</summary>
    public static bool Marching(Town town, Survey s, double t)
    {
        if (!Out(s) || s.Phase is SurveyPhase.Waiting or SurveyPhase.Investigating) return false;
        var leg = s.Legs.LastOrDefault(l => l.Start <= t);
        return leg != null && t < End(town, leg) && Roads.Marched(town.Content, t, t + 1) > 0;
    }

    /// <summary>The roads the company walked, each a line of spots, for its report.</summary>
    public static List<List<(double X, double Y)>> Walked(Town town, Survey s)
    {
        var lines = new List<List<(double X, double Y)>>();
        foreach (var l in s.Legs)
        {
            if (l.Start > town.Tick) continue;
            var line = new List<(double X, double Y)> { (l.FromX, l.FromY) };
            double end = Math.Min(End(town, l), town.Tick);
            double hours = Roads.Marched(town.Content, l.Start, end);
            double h = 0;
            var cur = (X: l.FromX, Y: l.FromY);
            foreach (var w in l.Route)
            {
                double leg = Roads.Hours(town, cur, [w]);
                if (h + leg >= hours)
                {
                    line.Add(Roads.Along(town, (l.FromX, l.FromY), l.Route, hours));
                    break;
                }
                line.Add((w.X, w.Y));
                h += leg;
                cur = (w.X, w.Y);
            }
            lines.Add(line);
        }
        return lines;
    }

    private static void SetAway(Town town, Survey s)
    {
        // The town cannot know when they will be back; the sheets carry a far guess so nothing books them.
        foreach (int id in s.Members) town.Agent(id).Hero!.AwayUntil = town.Tick + 60 * SimTime.HoursPerDay;
    }

    private static List<Agent> Living(Town town, Survey s) => s.Members.Select(town.Agent).Where(a => !a.Dead && !a.Departed).ToList();

    private static double Hp(Town town, Survey s)
    {
        var m = Living(town, s);
        return m.Count == 0 ? 0 : m.Sum(a => (double)a.Hero!.Hp) / Math.Max(1, m.Sum(a => a.Hero!.MaxHp));
    }

    /// <summary>Set off on a new stretch of road from where the company stands.</summary>
    private static void March(Town town, Survey s, (double X, double Y) to, string kind)
    {
        // Nobody left standing to walk it (all fell at the find): the company's fate is settled elsewhere.
        var living = Living(town, s);
        if (living.Count == 0) return;
        var from = Where(town, s, town.Tick);
        if (s.Legs.Count > 0 && s.Legs[^1].Stop == null && End(town, s.Legs[^1]) > town.Tick) s.Legs[^1].Stop = town.Tick;
        var said = new List<string>();
        // Out exploring they keep to the land's lines; going home they take the best road.
        var route = Roads.Plan(town, from, to, Roads.Mind(town, living, false, Hp(town, s) < 0.6) with { Explorer = kind != "home" }, town.Rng, said);
        s.Legs.Add(new SurveyLeg { FromX = from.X, FromY = from.Y, Route = route, Start = town.Tick, Kind = kind });
        Country.Wear(town, from, route);
    }

    /// <summary>A spot to make for next in the region: somewhere they have not found yet, or just more of the country.</summary>
    private static (double X, double Y) Next(Town town, Survey s)
    {
        var region = town.Regions[s.RegionId];
        var rng = town.Rng;
        if (s.ZoneId is int zid && Wilds.Of(town, zid) is { } zone && Wilds.NextCell(town, zone, Where(town, s, town.Tick)) is { } cell) return cell;
        if (s.SpotX is double sx && s.SpotY is double sy)
        {
            // Sent to a marked spot: they cast about it rather than wander the whole region.
            double ra = rng.Next() * 2 * Math.PI;
            double rr = Rules(town).Commission.SpotRadius * (0.4 + rng.Next() * 0.6);
            return (sx + rr * Math.Cos(ra), sy + rr * Math.Sin(ra));
        }
        var unfound = region.SiteIds.Select(id => Quests.Site(town, id)!).Where(x => !x.Found && !x.Cleared && !x.Hidden).ToList();
        if (unfound.Count > 0 && rng.Chance(0.55))
        {
            var (x, y) = World.Spot(town, rng.Pick(unfound));
            return (x + (rng.Next() - 0.5) * 0.4, y + (rng.Next() - 0.5) * 0.4);
        }
        if (World.SettlementIn(town, region) is { Razed: false } st && !st.Met && rng.Chance(0.4)) return SettlementSpot(town, st);
        // Landmarks: the edge of a ridge or a mire, a strange place; they walk the land's lines rather than open country.
        if (rng.Chance(Country.Rules(town).Landmark) && Country.Landmark(town, region, Where(town, s, town.Tick), rng) is { } mark) return mark;
        double bearing = region.Bearing + (rng.Next() - 0.5) * region.Arc * 0.7;
        double reach = region.Days + (rng.Next() - 0.5) * 0.9;
        double a = bearing * Math.PI / 180;
        return (Math.Max(0.5, reach) * Math.Sin(a), -Math.Max(0.5, reach) * Math.Cos(a));
    }

    // ---------- The hourly tick ----------

    public static void Hourly(Town town, bool autoMayor)
    {
        foreach (var s in town.Surveys)
        {
            if (!Out(s)) continue;
            switch (s.Phase)
            {
                case SurveyPhase.Waiting:
                {
                    var f = FindOf(town, s.AtFind!.Value)!;
                    if (town.Tick == f.ReportTick && f.Log.Count > 0 && town.Tick > f.FoundTick) Announce(town, f);
                    if (town.Tick >= f.DueTick) Decide(town, f, lapsed: true);
                    else if (town.Tick >= f.ReportTick && autoMayor) Decide(town, f, lapsed: false);
                    break;
                }
                case SurveyPhase.Investigating:
                    if (town.Tick >= s.ResumeTick) Dig(town, s, FindOf(town, s.AtFind!.Value)!);
                    break;
                default:
                    Walk(town, s);
                    break;
            }
        }
    }

    private static void Walk(Town town, Survey s)
    {
        var leg = s.Legs[^1];
        bool done = town.Tick >= End(town, leg);
        // Out on the road, they see the painted ground they pass.
        if (town.Wilds.Count > 0) Wilds.See(town, s, Where(town, s, town.Tick));
        if (s.Phase == SurveyPhase.Homeward)
        {
            if (done) Home(town, s);
            return;
        }
        if (s.Phase == SurveyPhase.Exploring && !s.Escorting && Spot(town, s)) return;
        if (!done) return;
        if (s.Phase == SurveyPhase.Outbound)
        {
            s.Phase = SurveyPhase.Exploring;
            var zone = s.ZoneId is int zid ? Wilds.Of(town, zid) : null;
            s.ExploreUntil = town.Tick + (zone != null ? Wilds.TripHours(town, zone) : (s.SpotX == null ? Rules(town).ExploreDays : Rules(town).Commission.SpotDays) * SimTime.HoursPerDay);
            s.Journal.Add(zone != null ? $"They reach {Wilds.Name(town, zone)} the council painted, and set about walking it." : s.SpotX == null ? $"They reach {town.Regions[s.RegionId].Name}." : "They reach the spot the council marked, and cast about it.");
            // Out there, they learn the name of the country the spot lies in.
            town.Regions[s.RegionId].Known = true;
        }
        bool walked = s.ZoneId is int wz && (Wilds.Of(town, wz) is not { } wild || Wilds.Unseen(wild) == 0);
        if (s.Escorting || walked || town.Tick >= s.ExploreUntil || Hp(town, s) < Rules(town).HomeHp) GoHome(town, s);
        else March(town, s, Next(town, s), "wander");
    }

    private static void GoHome(Town town, Survey s)
    {
        s.Phase = SurveyPhase.Homeward;
        March(town, s, (0, 0), "home");
    }

    /// <summary>Whether the company, where it stands, has come within sight of a place nobody found yet; if so it stops and reports.</summary>
    private static bool Spot(Town town, Survey s)
    {
        var p = Where(town, s, town.Tick);
        double sight = Rules(town).Sight;
        double D((double X, double Y) q) => Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y));
        var site = town.Sites
            .Where(x => !x.Found && !x.Cleared && !x.Held && !x.Hidden && x.RegionId != null && D(World.Spot(town, x)) <= sight)
            .OrderBy(x => D(World.Spot(town, x)))
            .ThenBy(x => x.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (site != null)
        {
            var (x, y) = World.Spot(town, site);
            Report(town, s, new Find
            {
                Id = town.Finds.Count + 1,
                SurveyId = s.Id,
                Kind = "lair",
                SiteId = site.Def.Id,
                RegionId = site.RegionId!.Value,
                Sign = Sign(town, "lair"),
                X = x,
                Y = y,
                FoundTick = town.Tick,
            });
            return true;
        }
        var st = town.Settlements.FirstOrDefault(x => !x.Met && !x.Razed && D(SettlementSpot(town, x)) <= sight);
        if (st != null)
        {
            var (x, y) = SettlementSpot(town, st);
            Report(town, s, new Find { Id = town.Finds.Count + 1, SurveyId = s.Id, Kind = "settlement", SettlementId = st.Id, RegionId = st.RegionId, X = x, Y = y, FoundTick = town.Tick });
            return true;
        }
        return false;
    }

    private static string Sign(Town town, string kind) =>
        Rules(town).Kind(kind) is { Signs.Count: > 0 } k ? town.Rng.Pick(k.Signs) : "";

    /// <summary>A chance meeting on the road: another company, a caravan, a font, a ruin.</summary>
    private static void Meet(Town town, Survey s)
    {
        var rules = Rules(town);
        var rng = town.Rng;
        var region = town.Regions[s.RegionId];
        var kinds = rules.Weights.Where(w => rules.Kind(w.Key) != null && w.Value > 0).OrderBy(w => w.Key, StringComparer.Ordinal).ToList();
        // Caravans come from peoples the town could trade with.
        var homes = town.Settlements.Where(x => !x.Razed && Math.Abs(town.Regions[x.RegionId].Ring - region.Ring) <= 1).OrderBy(x => x.Id).ToList();
        if (homes.Count == 0) kinds = kinds.Where(k => k.Key != "caravan").ToList();
        if (kinds.Count == 0) return;
        double roll = rng.Next() * Js.Sum(kinds, k => k.Value);
        string kind = kinds[^1].Key;
        foreach (var k in kinds)
        {
            roll -= k.Value;
            if (roll < 0)
            {
                kind = k.Key;
                break;
            }
        }
        var def = rules.Kind(kind)!;
        var (x, y) = Where(town, s, town.Tick);
        Report(town, s, new Find
        {
            Id = town.Finds.Count + 1,
            SurveyId = s.Id,
            Kind = kind,
            RegionId = region.Id,
            SettlementId = kind == "caravan" ? rng.Pick(homes).Id : null,
            Band = def.Bands.Count > 0 ? rng.Pick(def.Bands) : "",
            Sign = Sign(town, kind),
            Count = kind == "company" ? rng.Range(3, 6) : kind == "caravan" ? rng.Range(3, 9) : 0,
            X = x,
            Y = y,
            FoundTick = town.Tick,
        });
    }

    // ---------- Reports ----------

    /// <summary>Whether the company can still cast a sending (one of its casters' spell slots).</summary>
    public static bool Sends(Town town, Survey s)
    {
        var k = town.Content.Knowledge;
        int slots = 0;
        foreach (var a in Living(town, s))
            if (Knowledge.Casts(town, a, k.SendSpell)) slots += k.SlotsPerCaster + k.SlotsPerTwoLevels * (a.Hero!.Level / 2);
        return slots > s.Sendings;
    }

    /// <summary>The company stops where it is and sends word of what it found.</summary>
    private static void Report(Town town, Survey s, Find f)
    {
        town.Finds.Add(f);
        s.Finds.Add(f.Id);
        if (f.SiteId is string sid && Quests.Site(town, sid) is { } site)
        {
            // They have seen it with their own eyes: it goes on their map, where it really is.
            site.Found = true;
            site.FoundTick ??= town.Tick;
        }
        if (f.SettlementId is int stid && f.Kind == "settlement") town.Settlements[stid].Met = true;
        s.Legs[^1].Stop = town.Tick;
        s.Phase = SurveyPhase.Waiting;
        s.AtFind = f.Id;
        f.Log.Add(Fill(town, f, Def(town, f).Found));
        s.Journal.Add($"They come on {What(town, f)}.");
        Send(town, s, f);
    }

    /// <summary>Word goes home: by sending at once if a caster has a slot, by bird otherwise.</summary>
    private static void Send(Town town, Survey s, Find f)
    {
        var rules = Rules(town);
        f.State = FindState.Open;
        f.Known = true;
        if (Sends(town, s))
        {
            s.Sendings++;
            f.ReportTick = town.Tick;
        }
        else
        {
            f.ReportTick = town.Tick + rules.BirdHours;
            if (f.Log.Count == 1) f.Log.Add(Text(town, "bird"));
        }
        f.DueTick = f.ReportTick + rules.DecideHours;
        if (f.ReportTick == town.Tick) Announce(town, f);
    }

    private static void Announce(Town town, Find f)
    {
        var s = Survey(town, f.SurveyId)!;
        // The first report of a find is news; how each chapter goes is for whoever follows it.
        // Word that came by bird tells what they found, not how it travelled.
        string bird = Text(town, "bird");
        string word = f.Log.Count > 1 && f.Log[^1] == bird ? f.Log[^2] : f.Log[^1];
        town.Log(LedgerCategory.Quest, f.Chapter == 0 && f.Clues == 0 ? Importance.Notable : Importance.Routine, $"Word from the {s.Name} in {town.Regions[f.RegionId].Name}: {word}", s.Members);
    }

    public static FindKindDef Def(Town town, Find f) => Rules(town).Kind(f.Kind) ?? throw new KeyNotFoundException($"Unknown find kind {f.Kind}");

    /// <summary>What it is, as the company would put it: "a lair of goblins", "the dwarves of Ironhold".</summary>
    public static string What(Town town, Find f)
    {
        switch (f.Kind)
        {
            case "lair":
            {
                var site = Quests.Site(town, f.SiteId!)!;
                return f.Chapter > 0 ? $"{site.Def.Name}, the den of {Rumors.Occupants(town.Content, site.Def)}" : $"{site.Def.Name}, a lair of some kind";
            }
            case "settlement":
                return town.Settlements[f.SettlementId!.Value] is var st && f.Chapter > 0 ? st.People : town.Settlements[f.SettlementId!.Value].Name;
            case "company":
                return $"the {f.Band}";
            case "caravan":
                return $"a caravan out of {town.Settlements[f.SettlementId!.Value].Name}";
            case "font":
                return "a magical font";
            case "circle":
                return "a rune-lit stone circle";
            default:
                return "an ancient ruin";
        }
    }

    /// <summary>A short name for the find's card and its map marker.</summary>
    public static string Title(Town town, Find f) => f.Kind switch
    {
        "lair" => Quests.Site(town, f.SiteId!)!.Def.Name,
        "settlement" => town.Settlements[f.SettlementId!.Value].Name,
        "company" => $"The {f.Band}",
        "caravan" => $"Caravan of {town.Settlements[f.SettlementId!.Value].Name}",
        _ => Cap(Def(town, f).Name),
    };

    public static string Fill(Town town, Find f, string text)
    {
        var s = Survey(town, f.SurveyId)!;
        var leader = Living(town, s).FirstOrDefault() ?? town.Agent(s.Members[0]);
        var site = f.SiteId is string sid ? Quests.Site(town, sid) : null;
        var st = f.SettlementId is int id ? town.Settlements[id] : null;
        double days = Math.Round(Math.Sqrt(f.X * f.X + f.Y * f.Y) * 2) / 2;
        string power = town.Content.History.Powers.Where(p => p.To < town.Content.History.Year).OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => p.Name).ElementAtOrDefault(f.Id % Math.Max(1, town.Content.History.Powers.Count(p => p.To < town.Content.History.Year))) ?? "an older age";
        string t = text
            .Replace("{leader}", leader.GivenName, StringComparison.Ordinal)
            .Replace("{party}", s.Name, StringComparison.Ordinal)
            .Replace("{what}", What(town, f), StringComparison.Ordinal)
            .Replace("{region}", town.Regions[f.RegionId].Name, StringComparison.Ordinal)
            .Replace("{days}", days <= 1 ? "a day" : $"{Js.Str(days)} days", StringComparison.Ordinal)
            .Replace("{sign}", f.Sign, StringComparison.Ordinal)
            .Replace("{band}", f.Band, StringComparison.Ordinal)
            .Replace("{count}", f.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{wagons}", f.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{settlement}", st?.Name ?? "a far town", StringComparison.Ordinal)
            .Replace("{people}", st?.People ?? "they", StringComparison.Ordinal)
            .Replace("{who}", site != null ? Rumors.Occupants(town.Content, site.Def) : "", StringComparison.Ordinal)
            .Replace("{tier}", site != null ? site.Def.Tier.ToString(System.Globalization.CultureInfo.InvariantCulture) : "", StringComparison.Ordinal)
            .Replace("{power}", power, StringComparison.Ordinal)
            .Replace("{town}", town.Name, StringComparison.Ordinal);
        return Cap(t);
    }

    private static string Text(Town town, string key) => Rules(town).Texts.GetValueOrDefault(key, "");

    // ---------- Choices ----------

    /// <summary>The chapter they would work next, or null when there is no more to learn.</summary>
    public static ChapterDef? NextChapter(Town town, Find f)
    {
        var d = Def(town, f);
        return f.Chapter < d.Chapters.Count ? d.Chapters[f.Chapter] : null;
    }

    private static int Dc(Town town, Find f, ChapterDef ch) =>
        ch.Dc + ch.DcPerTier * (f.SiteId is string sid ? Quests.Site(town, sid)!.Def.Tier : town.Regions[f.RegionId].Levels[0]) - Rules(town).ClueBonus * f.Clues;

    /// <summary>What the mayor can tell the company to do about a find, with the odds of any check.</summary>
    public static List<FindChoice> Choices(Town town, Find f)
    {
        var s = Survey(town, f.SurveyId)!;
        var members = Living(town, s);
        var list = new List<FindChoice>();
        if (NextChapter(town, f) is { } ch)
        {
            string skill = SkillName(town, ch.Skill);
            list.Add(new FindChoice("dig", $"Investigate further: {Lower(ch.Title)}", $"{SpanText(ch.Span)}. {skill} check, DC {Dc(town, f, ch)}{(f.Clues > 0 ? $" (easier for {f.Clues} {(f.Clues == 1 ? "try" : "tries")} already)" : "")}. {RiskText(ch.Risk)}", PartyChance(town, members, ch.Skill, Dc(town, f, ch)), null));
        }
        foreach (var o in Def(town, f).Options)
        {
            string? blocked = o.Cost > 0 && town.Treasury < o.Cost ? $"needs {Js.Str(o.Cost)} gold" : null;
            if (o.Act == "engage" && Quests.Site(town, f.SiteId!) is { Cleared: true }) continue;
            if (o.Act == "engage" && members.Count < 2) blocked = "too few of them left";
            string detail = o.Act switch
            {
                "engage" => EngageText(town, f, members),
                "mark" or "onward" => "They note it on their map and go on exploring.",
                "parley" => $"{SkillName(town, o.Skill)} check, DC {o.Dc}. Goodwill with {town.Settlements[f.SettlementId!.Value].People}, and they name the dangers nearby.",
                "news" => $"{SkillName(town, o.Skill)} check, DC {o.Dc}. They may tell of a place the town has never heard of.",
                "invite" => $"{SkillName(town, o.Skill)} check, DC {o.Dc}. One of them may come to {town.Name} to work.",
                "escort" => $"They walk the caravan home and explore no further. Goods and gold for {town.Name}.",
                "maps" => $"{Js.Str(o.Cost)} gold from the treasury for their road maps.",
                _ => "",
            };
            double chance = o.Skill != "" ? PartyChance(town, members, o.Skill, o.Dc) : 1;
            list.Add(new FindChoice(o.Act, Fill(town, f, o.Label), detail, chance, blocked));
        }
        if (!list.Any(c => c.Act is "onward" or "mark"))
            list.Add(new FindChoice("onward", "Keep exploring", "They leave it and go on exploring.", 1, null));
        list.Add(new FindChoice("home", $"Return to {town.Name}", "They turn for home with what they have.", 1, null));
        return list;
    }

    private static string EngageText(Town town, Find f, List<Agent> members)
    {
        var site = Quests.Site(town, f.SiteId!)!;
        int levels = members.Sum(m => m.Hero!.Level);
        string odds = f.Chapter == 0 ? "They do not know what is inside." : levels >= 3 * site.Def.Tier && members.Count >= 3 ? "They look strong enough for it." : "It looks like more than they can handle.";
        return $"They go in from where they stand. {odds}{(f.Chapter >= Def(town, f).Chapters.Count ? " They know its weakness." : "")}";
    }

    /// <summary>How long a step takes, as the company would put it: "About 8 hours", "About a day", "About 3 days".</summary>
    public static string SpanText(int hours)
    {
        if (hours < 18) return $"About {hours} hours";
        double days = Js.Round(hours / (double)SimTime.HoursPerDay * 2) / 2;
        return days <= 1 ? "About a day" : $"About {Js.Str(days)} days";
    }

    private static string RiskText(string risk) => risk switch
    {
        "alert" => "If it goes wrong, the place is roused.",
        "fight" => "If it goes wrong, there will be a fight.",
        "wound" => "If it goes wrong, someone gets hurt.",
        "time" => "If it goes wrong, it costs a day.",
        "offend" => "If it goes wrong, they give offence.",
        _ => "",
    };

    public static string SkillName(Town town, string skill) => town.Content.Stories.Skill(skill)?.Name ?? Cap(skill);

    // ---------- The party's dice ----------

    private static string Kind(string skill) => skill switch
    {
        "perception" or "insight" => SkillKind.Perception,
        "stealth" or "investigation" or "sleight-of-hand" => SkillKind.Trap,
        "persuasion" or "deception" or "intimidation" or "performance" => SkillKind.Parley,
        "arcana" or "history" or "religion" => SkillKind.Puzzle,
        _ => SkillKind.Hazard,
    };

    /// <summary>A member's bonus on a check: the one on their character sheet (<see cref="Skills.Bonus"/>).</summary>
    public static int Bonus(Town town, Agent a, string skill) => Skills.Bonus(town, a, skill);

    /// <summary>Who in the company makes a check: the best at it.</summary>
    public static Agent? Best(Town town, IReadOnlyList<Agent> members, string skill) =>
        members.OrderByDescending(a => Bonus(town, a, skill)).ThenBy(a => a.Id).FirstOrDefault();

    public static double PartyChance(Town town, IReadOnlyList<Agent> members, string skill, int dc)
    {
        var who = Best(town, members, skill);
        if (who == null) return 0;
        int bonus = Bonus(town, who, skill);
        int faces = 0;
        for (int d = 1; d <= 20; d++)
            if (d == 20 || (d != 1 && d + bonus >= dc)) faces++;
        return faces / 20.0;
    }

    /// <summary>The company's best at a skill rolls it, as the mayor's checks roll.</summary>
    private static (StoryBeat Beat, Agent Who) Roll(Town town, IReadOnlyList<Agent> members, string skill, int dc)
    {
        var who = Best(town, members, skill)!;
        int bonus = Bonus(town, who, skill);
        int die = town.Rng.D20();
        var beat = new StoryBeat
        {
            Tick = town.Tick,
            Skill = skill,
            Die = die,
            Modifier = bonus,
            Total = die + bonus,
            Dc = dc,
            Advantage = $"{who.GivenName} the {who.Hero!.Cls.Name}",
            Result = die == 20 ? StoryResult.Critical : die == 1 ? StoryResult.Fumble : die + bonus >= dc ? StoryResult.Success : StoryResult.Failure,
        };
        return (beat, who);
    }

    private static string RollText(Town town, StoryBeat b) => $"({b.Advantage}: {SkillName(town, b.Skill)} {b.Total} against DC {b.Dc}, {Stories.Word(b.Result)})";

    // ---------- Orders ----------

    /// <summary>The mayor's word on a find. False when the find is not waiting on it or the order cannot be followed.</summary>
    public static bool Order(Town town, int findId, string act)
    {
        if (FindOf(town, findId) is not { State: FindState.Open } f || town.Tick < f.ReportTick) return false;
        var choice = Choices(town, f).FirstOrDefault(c => c.Act == act);
        if (choice == null || choice.Blocked != null) return false;
        Carry(town, f, choice, "");
        return true;
    }

    /// <summary>Nobody answered in time, or the stand-in mayor answers: the likeliest good course.</summary>
    private static void Decide(Town town, Find f, bool lapsed)
    {
        var choice = Pick(town, f);
        Carry(town, f, choice, lapsed ? Fill(town, f, Text(town, "lapse").Replace("{choice}", Lower(choice.Label), StringComparison.Ordinal)) : "");
    }

    /// <summary>What a sensible leader (or the stand-in mayor) does about a find.</summary>
    public static FindChoice Pick(Town town, Find f)
    {
        var s = Survey(town, f.SurveyId)!;
        var choices = Choices(town, f).Where(c => c.Blocked == null).ToList();
        FindChoice? Get(string act) => choices.FirstOrDefault(c => c.Act == act);
        var members = Living(town, s);
        double hp = Hp(town, s);
        var dig = Get("dig");
        bool hurt = hp < 0.6;
        switch (f.Kind)
        {
            case "lair":
            {
                var site = Quests.Site(town, f.SiteId!)!;
                int levels = members.Sum(m => m.Hero!.Level);
                bool strong = members.Count >= 3 && levels >= 3 * site.Def.Tier && !hurt;
                if (dig != null && f.Chapter < 1 && dig.Chance >= 0.35) return dig;
                if (strong && Get("engage") is { } e) return e;
                if (dig != null && dig.Chance >= 0.5 && !hurt) return dig;
                break;
            }
            case "settlement":
                if (dig != null && dig.Chance >= 0.45) return dig;
                if (Get("parley") is { } p) return p;
                break;
            case "company":
                if (Get("news") is { } n && n.Chance >= 0.4) return n;
                break;
            case "caravan":
                if (Get("maps") is { } m && town.Treasury >= 240) return m;
                if (dig != null) return dig;
                break;
            default:
                if (dig != null && dig.Chance >= 0.45 && !hurt) return dig;
                break;
        }
        if (hurt || town.Tick >= s.ExploreUntil) return Get("home")!;
        return Get("onward") ?? Get("mark") ?? Get("home")!;
    }

    private static void Carry(Town town, Find f, FindChoice c, string lapse)
    {
        var s = Survey(town, f.SurveyId)!;
        var rules = Rules(town);
        if (lapse != "") f.Log.Add(lapse);
        else f.Log.Add($"Mayor {town.Stories.Mayor.Name} sends word: {Lower(c.Label)}.");
        var members = Living(town, s);
        switch (c.Act)
        {
            case "dig":
            {
                var ch = NextChapter(town, f)!;
                f.State = FindState.Busy;
                s.Phase = SurveyPhase.Investigating;
                s.ResumeTick = town.Tick + ch.Span;
                f.Log.Add(Fill(town, f, ch.Text));
                return;
            }
            case "engage":
                Engage(town, s, f);
                return;
            case "parley":
            case "news":
            case "invite":
            {
                var o = Def(town, f).Options.First(x => x.Act == c.Act);
                var (beat, _) = Roll(town, members, o.Skill, o.Dc);
                f.Beat = beat;
                bool won = Sheet.Succeeded(beat);
                string said = c.Act switch
                {
                    "parley" => won ? Befriend(town, f, rules.Rewards.Parley) : Offend(town, f),
                    "news" => won ? News(town, f, 1) : $"The {f.Band} keep their own counsel.",
                    _ => won ? Invite(town, f) : $"The {f.Band} laugh and say they have work enough.",
                };
                f.Log.Add($"{said} {RollText(town, beat)}");
                break;
            }
            case "maps":
            {
                var o = Def(town, f).Options.First(x => x.Act == "maps");
                town.Book(TreasuryAccount.Exploring, -o.Cost);
                f.Log.Add(News(town, f, 2));
                break;
            }
            case "escort":
                s.Escorting = true;
                f.Log.Add($"They fall in beside the wagons and turn for {town.Name}.");
                break;
            case "mark":
                f.Log.Add(Fill(town, f, Text(town, "mark")));
                break;
            case "onward":
                f.Log.Add(Fill(town, f, Text(town, "onward")));
                break;
            case "home":
                f.Log.Add(Fill(town, f, Text(town, "home")));
                break;
        }
        f.State = FindState.Done;
        s.AtFind = null;
        s.Phase = SurveyPhase.Exploring;
        if (c.Act is "home" || s.Escorting) GoHome(town, s);
        else
        {
            // Told to keep going after their time was up: a little longer out.
            if (town.Tick >= s.ExploreUntil) s.ExploreUntil = town.Tick + rules.KeepDays * SimTime.HoursPerDay;
            March(town, s, Next(town, s), "wander");
        }
        Settle(town, f);
    }

    /// <summary>Write the find's latest line to the Ledger when the town hears it at once.</summary>
    private static void Settle(Town town, Find f)
    {
        var s = Survey(town, f.SurveyId)!;
        town.Log(LedgerCategory.Quest, Importance.Routine, $"The {s.Name} at {Title(town, f)}: {f.Log[^1]}", s.Members);
    }

    /// <summary>A chapter's work is done: the check, what they learn or what goes wrong, and a fresh report.</summary>
    private static void Dig(Town town, Survey s, Find f)
    {
        var ch = NextChapter(town, f)!;
        var members = Living(town, s);
        // All of them fell (in a raid, a duel, a plague) while they dug: nobody is left to finish it.
        if (members.Count == 0)
        {
            Lost(town, s);
            return;
        }
        var (beat, who) = Roll(town, members, ch.Skill, Dc(town, f, ch));
        f.Beat = beat;
        s.ResumeTick = null;
        if (Sheet.Succeeded(beat))
        {
            f.Chapter++;
            f.Clues = 0;
            string extra = Learn(town, f, ch);
            f.Log.Add($"{Fill(town, f, ch.Success)}{extra} {RollText(town, beat)}");
        }
        else
        {
            f.Clues++;
            string hurt = Risk(town, s, f, ch.Risk, beat.Result == StoryResult.Fumble);
            f.Log.Add($"{Fill(town, f, ch.Failure)}{hurt} {RollText(town, beat)}");
        }
        s.Journal.Add($"At {Title(town, f)}: {f.Log[^1]}");
        if (Living(town, s).Count == 0)
        {
            Lost(town, s);
            return;
        }
        s.Phase = SurveyPhase.Waiting;
        Send(town, s, f);
    }

    /// <summary>What a chapter done well teaches the town, and the reward of the last one.</summary>
    private static string Learn(Town town, Find f, ChapterDef ch)
    {
        var rw = Rules(town).Rewards;
        var s = Survey(town, f.SurveyId)!;
        if (f.SiteId is string sid && Quests.Site(town, sid) is { } site && f.Chapter == 1)
        {
            // Seen close: the town knows what lives there for certain.
            Rumors.Confirm(town, site, $"the {s.Name}");
            site.HeardFrom = $"the {s.Name}, who watched it";
        }
        if (f.Kind == "settlement" && f.Chapter == 1) World.Shift(town, town.Settlements[f.SettlementId!.Value], rw.Parley / 2);
        switch (ch.Reward)
        {
            case "weakness":
            {
                var site2 = Quests.Site(town, f.SiteId!)!;
                site2.Strength *= rw.Weakness;
                return "";
            }
            case "friends":
                return " " + Befriend(town, f, rw.Friends);
            case "maps":
                return " " + News(town, f, 2);
            case "bargain":
                town.Market.Seed(Good.Food, rw.Bargain);
                return $" They load {Js.Str(rw.Bargain)} measures of food for {town.Name} at a good price.";
            case "attune":
            {
                var casters = Living(town, s).Where(a => a.Hero!.Cls.Magic != "").ToList();
                foreach (var a in casters.Count > 0 ? casters : Living(town, s).Take(1).ToList()) Quests.GainXp(town, a, rw.AttuneXp);
                Sheet.Inspire(town, 1, "a font of magic attuned");
                return " The mayor is heartened by the news (inspiration).";
            }
            case "circle":
                return Circles.Claim(town, f);
            case "vault":
            {
                double gold = Math.Round(rw.VaultGold * Math.Max(1, town.Regions[f.RegionId].Levels[0]));
                town.Book(TreasuryAccount.Exploring, gold);
                foreach (var a in Living(town, s)) Notoriety.Deed(town, a, "clear", 2);
                return $" They send {Js.Str(gold)} gold of old coin home to the treasury.";
            }
        }
        return "";
    }

    /// <summary>What a chapter gone wrong costs: the place roused, a fight, a wound, a day, offence. A natural 1 adds a wound.</summary>
    private static string Risk(Town town, Survey s, Find f, string risk, bool fumble)
    {
        var said = "";
        var site = f.SiteId is string sid ? Quests.Site(town, sid) : null;
        switch (risk)
        {
            case "alert":
                if (site != null)
                {
                    site.Strength = Math.Min(100, site.Strength + 15);
                    said += " " + Text(town, "spotted");
                }
                break;
            case "fight":
                said += Fight(town, s, f);
                break;
            case "wound":
                said += Wound(town, s, f);
                break;
            case "time":
                s.ExploreUntil += SimTime.HoursPerDay;
                said += " " + Text(town, "time");
                break;
            case "offend":
                if (f.SettlementId is int st) said += " " + Offend(town, f);
                break;
        }
        if (fumble && risk != "wound" && risk != "fight") said += Wound(town, s, f);
        return said;
    }

    private static string Wound(Town town, Survey s, Find f)
    {
        var members = Living(town, s);
        if (members.Count == 0) return "";
        var a = members[town.Rng.Range(0, members.Count)];
        int tier = Math.Max(1, town.Regions[f.RegionId].Levels[0]);
        int dmg = town.Rng.Roll(tier + 1, 6);
        a.Hero!.Hp -= dmg;
        string said = " " + Text(town, "wound").Replace("{who}", a.GivenName, StringComparison.Ordinal).Replace("{dmg}", dmg.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        if (a.Hero.Hp > 0) return said;
        a.Hero.Hp = 0;
        Die(town, s, a);
        return said + " " + Text(town, "dies").Replace("{who}", a.GivenName, StringComparison.Ordinal);
    }

    /// <summary>Something comes for them: a fight in the open, fought as any fight on the road.</summary>
    private static string Fight(Town town, Survey s, Find f)
    {
        var members = Living(town, s);
        var site = f.SiteId is string sid ? Quests.Site(town, sid) : null;
        var region = town.Regions[f.RegionId];
        int tier = site?.Def.Tier ?? region.Levels[0];
        var v = new Expedition.Venture(town.Rng, town.Content, members, site?.Def) { Clock = town.Tick };
        v.Wilds(tier, site?.Def.Habitats ?? region.Theme.Habitats, site != null ? $"out of {site.Def.Name} they come" : "something comes out of the dark", true);
        var result = v.Result(v.Wiped ? ExpeditionOutcome.Wiped : ExpeditionOutcome.Retreated, Title(town, f));
        result.Apply(members);
        Patrons.Home(town, result);
        foreach (var id in result.Kills.Keys) town.Agent(id).Hero!.Kills += result.Kills.Get(id, 0);
        foreach (int id in result.Dead) Die(town, s, town.Agent(id));
        Wounds.Home(town, result, Living(town, s), site?.Def.Name ?? "");
        foreach (var line in v.Lines) s.Journal.Add(line);
        var dead = result.Dead.Select(id => town.Agent(id).GivenName).ToList();
        int share = members.Count == 0 ? 0 : result.Xp / members.Count;
        foreach (var m in Living(town, s)) Quests.GainXp(town, m, share);
        return dead.Count == 0 ? " They fight their way clear." : $" They fight their way clear, but {Expedition.ListNames(dead)} {(dead.Count == 1 ? "falls" : "fall")}.";
    }

    private static void Die(Town town, Survey s, Agent a)
    {
        if (a.Dead) return;
        a.Dead = true;
        a.Hero!.AwayUntil = null;
        Population.Depart(town, a);
        foreach (var other in town.Residents) if (other.OpinionOf(a.Id) >= 40) other.Loneliness = Math.Min(100, other.Loneliness + 30);
        town.Log(LedgerCategory.Quest, Importance.Major, $"{a.Name} the {a.Hero.Cls.Name} dies exploring {town.Regions[s.RegionId].Name} with the {s.Name}.", [a.Id]);
    }

    private static string Befriend(Town town, Find f, double gain)
    {
        var st = town.Settlements[f.SettlementId!.Value];
        World.Shift(town, st, gain);
        st.Met = true;
        int told = 0;
        // Locals know their own country: they name the dangers around them.
        foreach (var id in town.Regions[st.RegionId].SiteIds)
        {
            var site = Quests.Site(town, id)!;
            if (site.Cleared || site.Known != SiteKnowledge.Unheard) continue;
            World.Tell(town, site, 70, st.People);
            told++;
        }
        return $"{Cap(st.People)} welcome them{(told > 0 ? $" and name {told} {(told == 1 ? "danger" : "dangers")} nearby" : "")}.";
    }

    private static string Offend(Town town, Find f)
    {
        if (f.SettlementId is not int id) return "";
        var st = town.Settlements[id];
        World.Shift(town, st, Rules(town).Rewards.Offend);
        return Text(town, "offend").Replace("{people}", Cap(st.People), StringComparison.Ordinal).Replace("{town}", town.Name, StringComparison.Ordinal);
    }

    /// <summary>Other travellers tell of places the town has not heard of: the nearest first.</summary>
    private static string News(Town town, Find f, int n)
    {
        var who = f.Kind == "company" ? $"the {f.Band}" : $"the caravan master of {town.Settlements[f.SettlementId!.Value].Name}";
        var region = town.Regions[f.RegionId];
        var sites = town.Sites
            .Where(x => x.Known == SiteKnowledge.Unheard && !x.Cleared && x.RegionId is int rid && Math.Abs(town.Regions[rid].Ring - region.Ring) <= 1)
            .OrderBy(x => x.RegionId == region.Id ? 0 : 1)
            .ThenBy(x => x.Def.Distance)
            .ThenBy(x => x.Def.Id, StringComparer.Ordinal)
            .Take(n)
            .ToList();
        if (sites.Count == 0) return $"{Cap(who)} have nothing the town does not know already.";
        foreach (var x in sites)
        {
            World.Tell(town, x, Rules(town).Rewards.MapsTrust, who);
            if (town.Regions[x.RegionId!.Value] is { Known: false } r) r.Known = true;
        }
        return $"{Cap(who)} tell of {Expedition.ListNames(sites.Select(x => $"{x.Def.Name} ({x.Believed})").ToList())}.";
    }

    private static string Invite(Town town, Find f)
    {
        f.InviteDay = town.Day + Rules(town).Rewards.InviteDays;
        return $"One of the {f.Band} says they might look in on {town.Name}.";
    }

    /// <summary>Heroes of companies met on the road who said they would come.</summary>
    private static void Invited(Town town)
    {
        foreach (var f in town.Finds)
        {
            if (f.InviteDay != town.Day || town.Taverns.Count == 0 || town.Heroes.Count >= City.HeroCap(town)) continue;
            var a = Quests.CreateHero(town, Quests.PickClass(town), town.Rng.Chance(0.5) ? 2 : 3);
            town.Log(LedgerCategory.Arrival, Importance.Notable, $"{a.Describe()}, late of the {f.Band}, walks into {town.Taverns[0].Name}: they met the town's explorers on the road. {Annals.Past(town, a)} ({a.TraitList()})", [a.Id]);
        }
    }

    /// <summary>The company goes in to fight from where it stands: its members become a party on the road to the lair.</summary>
    private static void Engage(Town town, Survey s, Find f)
    {
        var site = Quests.Site(town, f.SiteId!)!;
        var members = Living(town, s);
        var at = Where(town, s, town.Tick);
        f.State = FindState.Done;
        s.AtFind = null;
        s.Phase = SurveyPhase.Engaged;
        Pay(town, s);
        var party = new Party
        {
            Id = town.Parties.Count + 1,
            Name = s.Name,
            Members = members.Select(m => m.Id).ToList(),
            SiteId = site.Def.Id,
            DepartTick = town.Tick,
            Start = at,
            SurveyId = s.Id,
        };
        Roads.Depart(town, party, members, site, null);
        town.Parties.Add(party);
        site.Attempts++;
        foreach (var m in members) m.Hero!.AwayUntil = party.ReturnTick;
        f.Log.Add(Fill(town, f, Text(town, "engage")));
        town.Log(LedgerCategory.Quest, Importance.Notable, $"The {s.Name} go in at {site.Def.Name} from where they found it.", party.Members);
    }

    // ---------- Home ----------

    private static void Pay(Town town, Survey s)
    {
        if (s.CommissionId is not int cid || CommissionOf(town, cid) is not { Status: BountyStatus.Taken } c) return;
        c.Status = BountyStatus.Completed;
        var living = Living(town, s);
        if (living.Count == 0)
        {
            town.Book(TreasuryAccount.Exploring, c.Reward);
            return;
        }
        foreach (var m in living)
        {
            m.Gold += c.Reward / living.Count;
            m.Hero!.Pay += c.Reward / living.Count;
        }
        Intel.Deal(town, s.Name, town.Content.World.Intel.Bounty);
    }

    private static void Home(Town town, Survey s)
    {
        var rules = Rules(town);
        s.Phase = SurveyPhase.Home;
        s.ReturnTick = town.Tick;
        Wilds.Told(town, s, true);
        Pay(town, s);
        var living = Living(town, s);
        var region = town.Regions[s.RegionId];
        foreach (var m in living)
        {
            m.Hero!.AwayUntil = null;
            m.Hero.Expeditions++;
            // Seeing the country is worth something to a hero.
            Quests.GainXp(town, m, 25 * Math.Max(1, region.Levels[0]) * (1 + s.Finds.Count));
        }
        // What they saw becomes the town's: the country is described, and anything they never got word home about is told now.
        bool first = !region.Visited;
        region.Visited = true;
        foreach (int id in s.Finds) FindOf(town, id)!.Known = true;
        if (first) Annals.Explored(town, s.Name, region);
        foreach (var f in s.Finds.Select(id => FindOf(town, id)!).Where(f => f.Kind == "caravan" && s.Escorting && f.State == FindState.Done && f.Log.Any(l => l.Contains("turn for", StringComparison.Ordinal))))
        {
            town.Market.Seed(Good.Food, rules.Rewards.EscortFood);
            town.Book(TreasuryAccount.Exploring, rules.Rewards.EscortGold);
            World.Shift(town, town.Settlements[f.SettlementId!.Value], rules.Rewards.Parley / 2);
            town.Log(LedgerCategory.Town, Importance.Notable, $"The caravan of {town.Settlements[f.SettlementId!.Value].Name} rolls in behind the {s.Name}, with {Js.Str(rules.Rewards.EscortFood)} measures of food and {Js.Str(rules.Rewards.EscortGold)} gold in tolls.");
        }
        string finds = s.Finds.Count == 0 ? ", having found nothing worth the telling" : $" with word of {Expedition.ListNames(s.Finds.Select(id => FindOf(town, id)!).Select(f => What(town, f)).ToList())}";
        town.Log(LedgerCategory.Quest, Importance.Notable, Text(town, "back").Replace("{party}", s.Name, StringComparison.Ordinal).Replace("{region}", region.Name, StringComparison.Ordinal).Replace("{finds}", finds, StringComparison.Ordinal), s.Members);
    }

    private static void Lost(Town town, Survey s)
    {
        s.Phase = SurveyPhase.Lost;
        Wilds.Told(town, s, false);
        if (s.AtFind is int fid) FindOf(town, fid)!.State = FindState.Done;
        s.AtFind = null;
        if (s.CommissionId is int cid && CommissionOf(town, cid) is { Status: BountyStatus.Taken } c)
        {
            c.Status = BountyStatus.Failed;
            town.Book(TreasuryAccount.Exploring, c.Reward);
        }
        town.Log(LedgerCategory.Quest, Importance.Major, $"No more word comes from the {s.Name} in {town.Regions[s.RegionId].Name}. They are presumed dead.");
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
