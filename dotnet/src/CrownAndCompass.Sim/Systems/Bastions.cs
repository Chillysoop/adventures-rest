namespace CrownAndCompass.Sim.Systems;

/// <summary>Who may raise a bastion and when (content/game/bastions.json "found").</summary>
public sealed record BastionFoundRules
{
    /// <summary>Level the company's best must have reached.</summary>
    public int Level { get; init; } = 5;

    /// <summary>Renown the company's name has earned (<see cref="Notoriety"/>); an infamous company is refused.</summary>
    public int Renown { get; init; } = 80;
    public int Members { get; init; } = 2;
    public int FirstDay { get; init; } = 60;

    /// <summary>Bastions a town of each tier has room for, by tier id.</summary>
    public IReadOnlyDictionary<string, int> PerTier { get; init; } = new Dictionary<string, int>();

    /// <summary>The members' spare gold must cover the keep this many times over before they ask.</summary>
    public double Spare { get; init; } = 1.15;

    /// <summary>Tiles from the hall a compound keeps away: companies build on the edge of town.</summary>
    public double Nearest { get; init; } = 7;

    /// <summary>Facility spots a keep's ground must have round it.</summary>
    public int SlotsNeeded { get; init; } = 3;
}

/// <summary>What the council's answer to a request does.</summary>
public sealed record BastionCouncilRules
{
    /// <summary>Days the council has to answer; silence is taken as leave.</summary>
    public int AnswerDays { get; init; } = 10;
    public int RefuseDays { get; init; } = 60;
    public double GrantDealings { get; init; } = 4;
    public double GiftDealings { get; init; } = 12;
    public double GiftLoyalty { get; init; } = 6;
    public double RefuseDealings { get; init; } = -12;
    public double RefuseLoyalty { get; init; } = -8;

    /// <summary>Loyalty points, while the refusal is fresh, for members of a company the council turned down.</summary>
    public double RefusedFeeling { get; init; } = -8;
}

public sealed record BastionMemberRules
{
    public int Max { get; init; } = 6;

    /// <summary>Loyalty points for a hero with a bastion of their own in town.</summary>
    public double Loyalty { get; init; } = 10;

    /// <summary>Gold a member keeps back from the chest: this, plus so much a level.</summary>
    public double ReserveBase { get; init; } = 150;
    public double ReservePerLevel { get; init; } = 30;

    /// <summary>Share of their gold above the reserve a member puts in the chest when it is short.</summary>
    public double Tithe { get; init; } = 0.5;
}

public sealed record BastionUpkeepRules
{
    /// <summary>Gold the chest keeps for wages before it builds.</summary>
    public double ChestReserve { get; init; } = 120;

    /// <summary>A bastion that ran dry lays its hirelings off until its chest is this full again.</summary>
    public double ReopenAt { get; init; } = 200;

    /// <summary>The chest must hold a facility's whole cost this many times over before the company builds it.</summary>
    public double BuildSpare { get; init; } = 1.25;
}

/// <summary>Special facilities a bastion has room for once its best member reaches a level.</summary>
public sealed record BastionSlot
{
    public int Level { get; init; }
    public int Facilities { get; init; }
}

/// <summary>A special facility: the building it is, the level it needs, and the order it carries out each bastion turn.</summary>
public sealed record FacilityDef
{
    public required string Building { get; init; }
    public int Level { get; init; } = 5;

    /// <summary>harvest, drill, craft, research, tend, trade, train, recruit or vault.</summary>
    public string Order { get; init; } = "";

    /// <summary>Lower builds first.</summary>
    public int Priority { get; init; }

    /// <summary>Built first of all while enemies threaten the town.</summary>
    public bool Threatened { get; init; }

    /// <summary>The order's size: draughts, folk fed, gold, xp, a hero's level or a magic item's tier.</summary>
    public double Amount { get; init; }

    /// <summary>harvest: draughts a member carries at most; sale: gold the rest fetch each.</summary>
    public int Carry { get; init; }
    public double Sale { get; init; }

    /// <summary>research: how far the town trusts what the map room turns up.</summary>
    public int Trust { get; init; } = 70;

    /// <summary>trade: extra gold for every hundred people in town.</summary>
    public double PerHundred { get; init; }

    /// <summary>train: extra xp for every level of the member.</summary>
    public double PerLevel { get; init; }
}

/// <summary>Something that befalls a bastion on its turn.</summary>
public sealed record BastionEventDef
{
    public required string Id { get; init; }
    public int Weight { get; init; }

    /// <summary>Gold, or for a thief the share of the chest.</summary>
    public double Gold { get; init; }
    public int Settlers { get; init; }
    public double Mandate { get; init; }
    public string Text { get; init; } = "";
}

/// <summary>Adventuring companies' bastions (content/game/bastions.json).</summary>
public sealed record BastionRules
{
    public string Keep { get; init; } = "bastion_keep";
    public BastionFoundRules Found { get; init; } = new();
    public BastionCouncilRules Council { get; init; } = new();
    public BastionMemberRules Members { get; init; } = new();
    public BastionUpkeepRules Upkeep { get; init; } = new();
    public int TurnDays { get; init; } = 7;
    public IReadOnlyList<BastionSlot> Slots { get; init; } = [];

    /// <summary>Strength at the walls of each sentry, and what each week of drill adds to it, up to MaxVeterancy weeks.</summary>
    public double Defender { get; init; } = 2.5;
    public double Veterancy { get; init; } = 0.5;
    public int MaxVeterancy { get; init; } = 4;
    public IReadOnlyList<FacilityDef> Facilities { get; init; } = [];
    public IReadOnlyList<BastionEventDef> Events { get; init; } = [];
    public IReadOnlyList<string> Prefixes { get; init; } = [];
    public IReadOnlyList<string> Suffixes { get; init; } = [];
}

/// <summary>A company's compound: its keep, its special facilities, its chest and its people.</summary>
public sealed class BastionState
{
    public int Id { get; init; }
    public required string Name { get; init; }

    /// <summary>The number its grounds carry on the plan (<see cref="LandPlan.Grounds"/>): the id of the bastion first raised here.</summary>
    public int Ground { get; init; }

    /// <summary>The company it belongs to; its members ride under this name.</summary>
    public required string Company { get; init; }

    /// <summary>The member who holds it (and owns its buildings), or null once it stands empty.</summary>
    public int? LordId { get; set; }

    /// <summary>Heroes who live here and ride for it, in the order they joined.</summary>
    public List<int> Members { get; } = [];

    /// <summary>Its keep and special facilities, by building id, in the order they were raised.</summary>
    public int KeepId { get; set; }
    public List<int> Facilities { get; } = [];

    /// <summary>The company's purse: hirelings' wages out, what the facilities make in.</summary>
    public double Chest { get; set; }
    public int FoundedDay { get; init; }
    public int NextTurnDay { get; set; }
    public int Turns { get; set; }

    /// <summary>Weeks the sentries have drilled.</summary>
    public int Veterancy { get; set; }

    /// <summary>Hirelings laid off for want of wages.</summary>
    public bool Idle { get; set; }

    /// <summary>Its company is gone: the buildings stand empty until another company takes them on.</summary>
    public bool Abandoned { get; set; }

    /// <summary>The bastion that took it on once it stood empty.</summary>
    public int? TakenBy { get; set; }

    /// <summary>What its last turns came to, newest last, for the Companies window.</summary>
    public List<string> Recent { get; } = [];
}

public enum BastionRequestStatus
{
    Pending,
    Granted,
    Gifted,
    Refused,
    Founded,
    Withdrawn,
}

/// <summary>A company asking the council's leave to raise a bastion.</summary>
public sealed class BastionRequest
{
    public int Id { get; init; }
    public required string Company { get; init; }
    public int Tick { get; init; }
    public BastionRequestStatus Status { get; set; }
    public int? AnsweredTick { get; set; }

    /// <summary>An empty bastion the company would take on rather than build.</summary>
    public int? TakeOver { get; set; }

    /// <summary>What the land would cost them when they asked (an empty bastion: its land and half its keep).</summary>
    public double Land { get; init; }
}

/// <summary>The council's answer to a company that asks for a bastion.</summary>
public enum BastionAnswer
{
    /// <summary>Leave to build: they buy the land at the town's price.</summary>
    Grant,

    /// <summary>The land is the town's gift: no price, and the company remembers it.</summary>
    Gift,
    Refuse,
}

/// <summary>
/// Bastions, after the 2024 rules in our own words. Once a company has made its name (its best member level
/// 5, renown between them) and has the gold, it asks the council's leave to raise a compound on open ground at
/// the edge of town. Its members live there instead of the tavern, which binds them to the town, and pay
/// into its chest. The keep and each special facility are built by the town's labourers and staffed by
/// townsfolk the company hires, so a bastion is a pull on the town's labour: idle hands first, then workers
/// lured from poorer wages. A company gets more facilities as its best member rises (2 at level 5, 4 at 9,
/// 5 at 13, 6 at 17). Every week is a bastion turn: each staffed facility carries out its order (draughts
/// from the garden, drill for the sentries, arms from the smithy, rumours from the map room, alms from the
/// sanctum, trade from the counting house, training, heroes answering the banners, wonders from the vault),
/// and something may befall the place. The sentries stand on the walls in a siege. When the company is gone
/// the bastion stands empty for another to take on.
/// </summary>
public static class Bastions
{
    private static BastionRules Rules(Town town) => town.Content.Bastions;

    public static BastionState? Of(Town town, Agent a) =>
        a.Hero?.BastionId is int id ? town.Bastions[id - 1] : null;

    public static BastionState? OfCompany(Town town, string company) =>
        town.Bastions.FirstOrDefault(b => !b.Abandoned && b.Company == company);

    public static Building Keep(Town town, BastionState b) => town.Building(b.KeepId);

    public static bool Standing(Town town, BastionState b) => !b.Abandoned && Keep(town, b).Complete;

    public static FacilityDef? Facility(Town town, BuildingDef d) => Rules(town).Facilities.FirstOrDefault(f => f.Building == d.Id);

    /// <summary>The special facilities a company whose best member is this level may have.</summary>
    public static int Slots(Town town, int level) =>
        Rules(town).Slots.Where(s => level >= s.Level).Select(s => s.Facilities).DefaultIfEmpty(0).Max();

    public static List<Agent> Members(Town town, BastionState b) => b.Members.Select(town.Agent).ToList();

    /// <summary>The level of the company's best, which sets how many facilities it may raise.</summary>
    public static int Level(Town town, BastionState b) => b.Members.Select(id => town.Agent(id).Hero!.Level).DefaultIfEmpty(0).Max();

    /// <summary>Bastions the town has room for at its tier.</summary>
    public static int Room(Town town) => Rules(town).Found.PerTier.GetValueOrDefault(City.Tier(town).Id, 0);

    /// <summary>A member sleeps in the keep once it stands.</summary>
    public static bool Lodges(Town town, Agent a) => Of(town, a) is { } b && Standing(town, b);

    // The council's leave ---------------------------------------------------------------------

    public static List<BastionRequest> Pending(Town town) => town.BastionRequests.Where(r => r.Status == BastionRequestStatus.Pending).ToList();

    public static int DaysLeft(Town town, BastionRequest r) =>
        Math.Max(1, (int)Math.Ceiling((r.Tick + Rules(town).Council.AnswerDays * SimTime.HoursPerDay - town.Tick) / (double)SimTime.HoursPerDay));

    /// <summary>An empty bastion goes to a new company for its land and half what the keep cost.</summary>
    public static double TakeOverPrice(Town town, BastionState b) => Js.Round(town.LotPrice(town.Lot(Keep(town, b).LotId)) + Keep(town, b).Def.Cost * 0.5);

    /// <summary>The player's answer (or the stand-in mayor's). False when there is no such request waiting.</summary>
    public static bool Answer(Town town, int requestId, BastionAnswer answer)
    {
        if (requestId < 1 || requestId > town.BastionRequests.Count) return false;
        var r = town.BastionRequests[requestId - 1];
        if (r.Status != BastionRequestStatus.Pending) return false;
        var c = Rules(town).Council;
        r.AnsweredTick = town.Tick;
        var members = Intel.Members(town, r.Company);
        switch (answer)
        {
            case BastionAnswer.Grant:
                r.Status = BastionRequestStatus.Granted;
                Intel.Deal(town, r.Company, c.GrantDealings);
                town.Log(LedgerCategory.Council, Importance.Notable, $"The council grants the {r.Company} leave to raise a bastion, at the town's price for the land.", members.Select(m => m.Id).ToList());
                break;
            case BastionAnswer.Gift:
                r.Status = BastionRequestStatus.Gifted;
                Intel.Deal(town, r.Company, c.GiftDealings);
                foreach (var m in members) Systems.Loyalty.Sway(m, c.GiftLoyalty);
                town.Log(LedgerCategory.Council, Importance.Notable, $"The council gives the {r.Company} the land for their bastion, free, in thanks for what they have done for {town.Name}.", members.Select(m => m.Id).ToList());
                break;
            default:
                r.Status = BastionRequestStatus.Refused;
                Intel.Deal(town, r.Company, c.RefuseDealings);
                foreach (var m in members) Systems.Loyalty.Sway(m, c.RefuseLoyalty);
                town.Log(LedgerCategory.Council, Importance.Notable, $"The council refuses the {r.Company} leave to raise a bastion. They take it badly.", members.Select(m => m.Id).ToList());
                break;
        }
        if (r.Status != BastionRequestStatus.Refused) Raise(town, r);
        return true;
    }

    /// <summary>The company was refused lately, and has not forgotten it.</summary>
    public static bool Refused(Town town, string company) =>
        town.BastionRequests.Any(r => r.Company == company && r.Status == BastionRequestStatus.Refused && town.Tick - r.AnsweredTick < Rules(town).Council.RefuseDays * SimTime.HoursPerDay);

    // Loyalty and defence -----------------------------------------------------------------------

    /// <summary>What the bastion (or the council's refusal of one) does for a hero's loyalty.</summary>
    public static IEnumerable<(string Why, double Points)> Loyalty(Town town, Agent a)
    {
        if (Lodges(town, a)) yield return ($"lives at {Of(town, a)!.Name}", Rules(town).Members.Loyalty);
        else if (a.Hero?.PartyName is string company && Refused(town, company)) yield return ("refused a bastion", Rules(town).Council.RefusedFeeling);
    }

    /// <summary>The bastions' sentries on the walls: so much each, more for every week they have drilled.</summary>
    public static double Defense(Town town)
    {
        var r = Rules(town);
        double d = 0;
        foreach (var b in town.Bastions)
        {
            if (b.Abandoned) continue;
            double each = r.Defender + r.Veterancy * b.Veterancy;
            foreach (int id in b.Facilities)
            {
                var f = town.Building(id);
                if (f.Complete && Facility(town, f.Def)?.Order == "drill") d += f.Workers.Count * each;
            }
        }
        return d;
    }

    /// <summary>Sentries at the bastions, for the War window.</summary>
    public static int Sentries(Town town) =>
        town.Bastions.Where(b => !b.Abandoned).Sum(b => b.Facilities.Select(town.Building).Where(f => f.Complete && Facility(town, f.Def)?.Order == "drill").Sum(f => f.Workers.Count));

    // The day -----------------------------------------------------------------------------------

    public static void Daily(Town town, bool auto)
    {
        foreach (var b in town.Bastions)
        {
            if (b.Abandoned) continue;
            Muster(town, b);
            if (b.Abandoned) continue;
            Accounts(town, b);
            Tithe(town, b);
            Grow(town, b);
            Hire(town, b);
            if (Standing(town, b) && town.Day >= b.NextTurnDay)
            {
                Turn(town, b);
                b.NextTurnDay = town.Day + Rules(town).TurnDays;
            }
        }
        Requests(town, auto);
    }

    /// <summary>Who still rides for the bastion: the dead, the gone and the turned leave it; companions who rode under its name join.</summary>
    private static void Muster(Town town, BastionState b)
    {
        foreach (int id in b.Members.ToList())
        {
            var a = town.Agent(id);
            if (!a.Dead && !a.Departed && a.Hero!.TurnedTo == null && a.Hero.DefectedTo == null) continue;
            b.Members.Remove(id);
            a.Hero!.BastionId = null;
        }
        int max = Rules(town).Members.Max;
        foreach (var h in town.Heroes.Where(h => h.Hero!.PartyName == b.Company && h.Hero.BastionId == null && !h.IsAway && !h.Dead).OrderBy(h => h.Id))
        {
            if (b.Members.Count >= max) break;
            b.Members.Add(h.Id);
            h.Hero!.BastionId = b.Id;
            if (Standing(town, b)) town.Log(LedgerCategory.Quest, Importance.Routine, $"{h.Name} moves into {b.Name} with the {b.Company}.", [h.Id]);
        }
        // Back in town, members ride under the bastion's banner again.
        foreach (var a in Members(town, b)) if (!a.IsAway) a.Hero!.PartyName = b.Company;

        if (b.Members.Count == 0)
        {
            Abandon(town, b);
            return;
        }
        if (b.LordId is int lord && b.Members.Contains(lord)) return;
        var heir = Members(town, b).OrderByDescending(m => m.Hero!.Level).ThenBy(m => m.Id).First();
        bool had = b.LordId != null;
        Hold(town, b, heir.Id);
        if (had) town.Log(LedgerCategory.Quest, Importance.Notable, $"{heir.Name} takes up the lordship of {b.Name} for the {b.Company}.", [heir.Id]);
    }

    /// <summary>Make a member the bastion's lord: its buildings and their ground are theirs.</summary>
    private static void Hold(Town town, BastionState b, int? lordId)
    {
        b.LordId = lordId;
        foreach (int id in b.Facilities.Prepend(b.KeepId))
        {
            var bld = town.Building(id);
            bld.OwnerId = lordId;
            town.Lot(bld.LotId).OwnerId = lordId;
        }
    }

    private static void Abandon(Town town, BastionState b)
    {
        b.Abandoned = true;
        Hold(town, b, null);
        foreach (int id in b.Facilities.Prepend(b.KeepId))
        {
            var bld = town.Building(id);
            Economy.ReleaseWorkers(town, bld);
            bld.Closed = true;
            bld.Cash = 0;
        }
        if (b.Chest > 0) town.Book(TreasuryAccount.Bastions, b.Chest);
        string left = b.Chest >= 1 ? $" What was left in its chest, {Js.Str(Js.Round(b.Chest))} gold, goes to the town." : "";
        b.Chest = 0;
        town.Log(LedgerCategory.Quest, Importance.Major, $"No one of the {b.Company} is left to hold {b.Name}. Its hirelings are paid off and its gates shut; it stands empty for another company to take on.{left}");
    }

    /// <summary>The facilities' takings and wages settle with the chest each day; the lord covers a shortfall, and a dry chest lays the hirelings off.</summary>
    private static void Accounts(Town town, BastionState b)
    {
        var u = Rules(town).Upkeep;
        foreach (int id in b.Facilities.Prepend(b.KeepId))
        {
            var bld = town.Building(id);
            b.Chest += bld.Cash;
            bld.Cash = 0;
            bld.RevenueToday = 0;
        }
        if (b.Chest < 0 && b.LordId is int lord)
        {
            var a = town.Agent(lord);
            double cover = Math.Min(Math.Max(0, a.Gold), -b.Chest);
            a.Gold -= cover;
            b.Chest += cover;
        }
        if (!b.Idle && b.Chest < 0)
        {
            b.Idle = true;
            foreach (int id in b.Facilities.Prepend(b.KeepId))
            {
                var bld = town.Building(id);
                Economy.ReleaseWorkers(town, bld);
                bld.Closed = true;
            }
            town.Log(LedgerCategory.Business, Importance.Notable, $"The {b.Company} cannot meet the wages at {b.Name}; its hirelings are let go until the chest is full again.", b.LordId is int l ? [l] : []);
        }
        else if (b.Idle && b.Chest >= u.ReopenAt)
        {
            b.Idle = false;
            foreach (int id in b.Facilities.Prepend(b.KeepId)) town.Building(id).Closed = false;
            town.Log(LedgerCategory.Business, Importance.Routine, $"{b.Name} is hiring again.");
        }
    }

    /// <summary>What the chest wants: a month of wages, and the next facility's cost when there is room for one.</summary>
    public static double Need(Town town, BastionState b)
    {
        var u = Rules(town).Upkeep;
        double wages = 0;
        foreach (int id in b.Facilities.Prepend(b.KeepId))
        {
            var bld = town.Building(id);
            wages += bld.Def.Jobs * bld.Def.Wage * 8 * 28;
        }
        double build = Next(town, b) is { } next ? FacilityCost(town, b, next) * u.BuildSpare : 0;
        return u.ChestReserve + wages + build;
    }

    /// <summary>Members at home put gold above their own reserve into the chest while it is short.</summary>
    private static void Tithe(Town town, BastionState b)
    {
        var m = Rules(town).Members;
        double short_ = Need(town, b) - b.Chest;
        foreach (var a in Members(town, b).OrderBy(a => a.Id))
        {
            if (short_ <= 0) break;
            if (a.IsAway) continue;
            double spare = a.Gold - (m.ReserveBase + m.ReservePerLevel * a.Hero!.Level);
            if (spare <= 0) continue;
            double give = Math.Min(short_, spare * m.Tithe);
            a.Gold -= give;
            b.Chest += give;
            short_ -= give;
        }
    }

    // Building ------------------------------------------------------------------------------------

    /// <summary>The facility the company would raise next, or null when it has all it has room for.</summary>
    public static BuildingDef? Next(Town town, BastionState b)
    {
        var r = Rules(town);
        int level = Level(town, b);
        if (b.Facilities.Count >= Slots(town, level)) return null;
        var have = b.Facilities.Select(id => town.Building(id).Def.Id).ToHashSet();
        bool threatened = town.Enemy.Stage is ThreatStage.Raids or ThreatStage.Marching || Menaces.Alive(town).Any(m => m.ArrivalTick != null);
        var pick = r.Facilities
            .Where(f => f.Level <= level && !have.Contains(f.Building))
            .OrderByDescending(f => threatened && f.Threatened ? 1 : 0)
            .ThenBy(f => f.Priority)
            .ThenBy(f => f.Building, StringComparer.Ordinal)
            .FirstOrDefault();
        return pick == null ? null : town.Content.Building(pick.Building);
    }

    /// <summary>Gold a facility costs the chest: its ground, its build, materials and the labourers' wages.</summary>
    public static double FacilityCost(Town town, BastionState b, BuildingDef d) =>
        town.LotPrice(town.Lot(Keep(town, b).LotId)) + d.Cost + Planning.Materials(town, d) + d.BuildHours * Brain.ConstructionWage;

    private static void Grow(Town town, BastionState b)
    {
        if (b.Idle || !Standing(town, b) || town.Buildings.Any(x => x.IsSite && b.Facilities.Contains(x.Id))) return;
        var d = Next(town, b);
        if (d == null) return;
        double cost = FacilityCost(town, b, d);
        if (b.Chest < cost * Rules(town).Upkeep.BuildSpare + Rules(town).Upkeep.ChestReserve) return;
        var keep = Keep(town, b);
        var lot = Spots(town, town.Lot(keep.LotId), d, b.Ground).FirstOrDefault();
        if (lot == null) return;
        Pay(town, b, lot, d, gift: false);
        var site = town.AddBuilding(d, lot, b.LordId, $"the {d.Name} of {b.Name}", false);
        site.LaborBudget = d.BuildHours * Brain.ConstructionWage;
        b.Facilities.Add(site.Id);
        town.Log(LedgerCategory.Construction, Importance.Notable, $"The {b.Company} break ground on {site.Name} at {b.Name}.", b.LordId is int l ? [l] : []);
    }

    /// <summary>The chest pays for ground (to the town, unless the land was the council's gift), materials and the build.</summary>
    private static void Pay(Town town, BastionState b, Lot lot, BuildingDef d, bool gift)
    {
        double land = Js.Round(town.LotPrice(lot));
        if (!gift)
        {
            b.Chest -= land;
            town.Book(TreasuryAccount.LandSales, land);
        }
        b.Chest -= Planning.BuyMaterials(town, d) + d.Cost + d.BuildHours * Brain.ConstructionWage;
    }

    /// <summary>Open posts filled from the town: the idle first (through the job market), then a worker lured from poorer pay, one a day.</summary>
    private static void Hire(Town town, BastionState b)
    {
        if (b.Idle) return;
        var post = b.Facilities.Prepend(b.KeepId).Select(town.Building).Where(x => x.OpenJobs > 0).OrderBy(x => x.Id).FirstOrDefault();
        if (post == null) return;
        if (town.Workers.Any(a => a.JobId == null && a.Age >= 18)) return;
        var from = town.Buildings
            .Where(x => x.Complete && !x.Def.Bastion && x.Crew == 0 && x.Def.Wage < post.Def.Wage && x.Workers.Any(w => w != x.OwnerId))
            .OrderBy(x => x.Def.Wage)
            .ThenBy(x => x.Id)
            .FirstOrDefault();
        if (from == null) return;
        int wid = from.Workers.Last(w => w != from.OwnerId);
        Population.Remove(from.Workers, wid);
        var a = town.Agent(wid);
        a.JobId = post.Id;
        post.Workers.Add(wid);
        town.Log(LedgerCategory.Business, Importance.Routine, $"{a.Name} leaves {from.Name} for better pay at {post.Name}.", [wid]);
    }

    // Founding --------------------------------------------------------------------------------

    /// <summary>A company that could ask for a bastion: under one name, in town, not in a bastion, with its best at level and a name famous (and clean) enough.</summary>
    public static List<(string Company, List<Agent> Members)> Hopefuls(Town town)
    {
        var f = Rules(town).Found;
        return town.Heroes
            .Where(h => h.Hero!.PartyName != null && h.Hero.BastionId == null && !h.Dead)
            .GroupBy(h => h.Hero!.PartyName!)
            .Select(g => (Company: g.Key, Members: g.OrderByDescending(h => h.Hero!.Level).ThenBy(h => h.Id).ToList()))
            .Where(x => x.Members.Count >= f.Members && x.Members[0].Hero!.Level >= f.Level && Intel.Find(town, x.Company) is { } c && c.Renown >= f.Renown && Notoriety.InfamyTier(town, c) < town.Content.Notoriety.KnightInfamy && OfCompany(town, x.Company) == null)
            .OrderBy(x => x.Company, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The members' gold above their own reserves: what they can put toward a keep.</summary>
    public static double Spare(Town town, IEnumerable<Agent> members)
    {
        var m = Rules(town).Members;
        return Js.Sum(members.Where(a => !a.IsAway), a => Math.Max(0, a.Gold - (m.ReserveBase + m.ReservePerLevel * a.Hero!.Level)));
    }

    /// <summary>What a new keep would cost them: its ground, the build, materials and labour.</summary>
    public static double KeepCost(Town town, Lot? lot)
    {
        var keep = town.Content.Building(Rules(town).Keep);
        return (lot != null ? town.LotPrice(lot) : 0) + keep.Cost + Planning.Materials(town, keep) + keep.BuildHours * Brain.ConstructionWage;
    }

    private static void Requests(Town town, bool auto)
    {
        var f = Rules(town).Found;
        var c = Rules(town).Council;
        // Answered requests wait for the company to find its ground; silence past the deadline is taken as leave.
        foreach (var r in town.BastionRequests)
        {
            if (r.Status == BastionRequestStatus.Pending && town.Tick - r.Tick >= c.AnswerDays * SimTime.HoursPerDay)
            {
                r.Status = BastionRequestStatus.Granted;
                r.AnsweredTick = town.Tick;
                town.Log(LedgerCategory.Council, Importance.Notable, $"The council never answers the {r.Company}; they take its silence as leave, and buy their ground at the town's price.");
            }
            if (r.Status is BastionRequestStatus.Granted or BastionRequestStatus.Gifted) Raise(town, r);
        }
        if (town.Day < f.FirstDay) return;
        int open = town.Bastions.Count(b => !b.Abandoned) + town.BastionRequests.Count(r => r.Status is BastionRequestStatus.Pending or BastionRequestStatus.Granted or BastionRequestStatus.Gifted);
        foreach (var (company, members) in Hopefuls(town))
        {
            if (open >= Room(town)) break;
            if (town.BastionRequests.Any(r => r.Company == company && (r.Status is BastionRequestStatus.Pending or BastionRequestStatus.Granted or BastionRequestStatus.Gifted))) continue;
            if (Refused(town, company)) continue;
            var empty = town.Bastions.FirstOrDefault(b => b.Abandoned && b.TakenBy == null && !town.BastionRequests.Any(r => r.TakeOver == b.Id && r.Status is BastionRequestStatus.Pending or BastionRequestStatus.Granted or BastionRequestStatus.Gifted));
            double spare = Spare(town, members);
            // The cheap test first: the search for ground is the dear part.
            if (spare < (empty != null ? TakeOverPrice(town, empty) : KeepCost(town, null)) * f.Spare) continue;
            double land;
            if (empty != null) land = TakeOverPrice(town, empty);
            else
            {
                var lot = Site(town, town.Content.Building(Rules(town).Keep));
                if (lot == null || spare < KeepCost(town, lot) * f.Spare) continue;
                land = Js.Round(town.LotPrice(lot));
            }
            var req = new BastionRequest { Id = town.BastionRequests.Count + 1, Company = company, Tick = town.Tick, TakeOver = empty?.Id, Land = land };
            town.BastionRequests.Add(req);
            open++;
            string what = empty != null ? $"to take on the empty {empty.Name}" : "for leave to raise a bastion of their own on the edge of town";
            town.Log(LedgerCategory.Council, Importance.Notable, $"The {company}, led by {members[0].Name}, ask the council {what}. They would hire townsfolk to staff it.", members.Select(m => m.Id).ToList());
            if (auto) Answer(town, req.Id, BastionAnswer.Grant);
        }
    }

    /// <summary>Leave given: the company pools its gold and breaks ground, or takes on the empty bastion. Waits a day when it can't yet.</summary>
    private static void Raise(Town town, BastionRequest r)
    {
        if (r.Status is not (BastionRequestStatus.Granted or BastionRequestStatus.Gifted)) return;
        var members = Intel.Members(town, r.Company).Where(m => m.Hero!.BastionId == null).ToList();
        if (members.Count == 0)
        {
            r.Status = BastionRequestStatus.Withdrawn;
            return;
        }
        if (members.All(m => m.IsAway)) return;
        bool gift = r.Status == BastionRequestStatus.Gifted;
        var keepDef = town.Content.Building(Rules(town).Keep);
        BastionState b;
        if (r.TakeOver is int bid)
        {
            var old = town.Bastions[bid - 1];
            double price = gift ? 0 : TakeOverPrice(town, old);
            if (!old.Abandoned || old.TakenBy != null || Spare(town, members) < price) return;
            Pool(town, members, price, out double chest);
            if (!gift) town.Book(TreasuryAccount.LandSales, price);
            b = new BastionState
            {
                Id = town.Bastions.Count + 1,
                Name = old.Name,
                Ground = old.Ground,
                Company = r.Company,
                KeepId = old.KeepId,
                FoundedDay = town.Day,
                NextTurnDay = town.Day + Rules(town).TurnDays,
                Chest = chest - price,
            };
            b.Facilities.AddRange(old.Facilities);
            old.Facilities.Clear();
            old.TakenBy = b.Id;
            foreach (int id in b.Facilities.Prepend(b.KeepId)) town.Building(id).Closed = false;
            Enlist(town, b, members);
            town.Bastions.Add(b);
            Hold(town, b, members[0].Id);
            town.Log(LedgerCategory.Property, Importance.Major, $"The {r.Company} take on the empty {b.Name} and open its gates again. It will need townsfolk to staff it.", members.Select(m => m.Id).ToList());
        }
        else
        {
            var lot = Site(town, keepDef);
            if (lot == null) return;
            double cost = KeepCost(town, lot) - (gift ? town.LotPrice(lot) : 0);
            if (Spare(town, members) < cost) return;
            Pool(town, members, cost, out double chest);
            var used = new HashSet<string>(town.Bastions.Select(x => x.Name));
            var rr = Rules(town);
            string name = "";
            for (int i = 0; i < 20 && (name == "" || used.Contains(name)); i++) name = town.Rng.Pick(rr.Prefixes) + town.Rng.Pick(rr.Suffixes);
            if (used.Contains(name)) name = $"{name} No. {town.Bastions.Count + 1}";
            lot.Zone = Zone.Civic;
            b = new BastionState
            {
                Id = town.Bastions.Count + 1,
                Name = name,
                Ground = town.Bastions.Count + 1,
                Company = r.Company,
                FoundedDay = town.Day,
                NextTurnDay = town.Day + rr.TurnDays,
                Chest = chest,
            };
            Pay(town, b, lot, keepDef, gift);
            var keep = town.AddBuilding(keepDef, lot, members[0].Id, name, false);
            keep.LaborBudget = keepDef.BuildHours * Brain.ConstructionWage;
            b.KeepId = keep.Id;
            Reserve(town, b, lot);
            Enlist(town, b, members);
            town.Bastions.Add(b);
            Hold(town, b, members[0].Id);
            string land = gift ? "on land the council gave them" : $"on ground bought from the town for {Js.Str(Js.Round(town.LotPrice(lot)))} gold";
            town.Log(LedgerCategory.Property, Importance.Major, $"The {r.Company} break ground on {name}, their bastion, {land}. The town's labourers will raise it, and townsfolk will be hired to keep it.", members.Select(m => m.Id).ToList());
        }
        r.Status = BastionRequestStatus.Founded;
    }

    private static void Enlist(Town town, BastionState b, List<Agent> members)
    {
        foreach (var m in members.Take(Rules(town).Members.Max))
        {
            b.Members.Add(m.Id);
            m.Hero!.BastionId = b.Id;
        }
    }

    /// <summary>The members at home put their spare gold together, each by their share of it, until the cost is met with a chest for wages besides.</summary>
    private static void Pool(Town town, List<Agent> members, double cost, out double chest)
    {
        var m = Rules(town).Members;
        double want = cost + Rules(town).Upkeep.ChestReserve;
        double spare = Spare(town, members);
        double share = spare <= 0 ? 0 : Math.Min(1, want / spare);
        chest = 0;
        foreach (var a in members.Where(a => !a.IsAway).OrderBy(a => a.Id))
        {
            double give = Math.Max(0, a.Gold - (m.ReserveBase + m.ReservePerLevel * a.Hero!.Level)) * share;
            a.Gold -= give;
            chest += give;
        }
    }

    // Where a compound goes ------------------------------------------------------------------------

    /// <summary>
    /// Open, unzoned ground for a keep on a street at the edge of town, with room round it for facilities: the
    /// first spot down the streets, from the hall outward, past <see cref="BastionFoundRules.Nearest"/> tiles. Null when there is none.
    /// </summary>
    public static Lot? Site(Town town, BuildingDef keep)
    {
        var f = Rules(town).Found;
        var (w, d) = Land.Footprint(keep);
        var probe = town.Content.Building(Rules(town).Facilities.OrderByDescending(x => Land.Footprint(town.Content.Building(x.Building)).W * Land.Footprint(town.Content.Building(x.Building)).D).ThenBy(x => x.Building, StringComparer.Ordinal).First().Building);
        int tried = 0;
        foreach (var (_, x, y, yaw) in Streets.Fronts(town, d, Zone.None))
        {
            if (Math.Sqrt((x - Town.Center) * (x - Town.Center) + (y - Town.Center) * (y - Town.Center)) < f.Nearest) continue;
            if (!City.Allows(town, keep, x, y) || !Land.Fits(town, x, y, w, d, yaw, Zone.None)) continue;
            if (++tried > 300) break;
            var lot = new Lot(town.Lots.Count, x, y) { W = w, D = d, Yaw = yaw, Zone = Zone.Civic };
            if (Spots(town, lot, probe, 0).Take(f.SlotsNeeded).Count() >= f.SlotsNeeded) return lot;
        }
        return null;
    }

    private static readonly double[] Rings = [2.1, 2.9, 3.6];

    /// <summary>Tiles round the keep the company holds as its grounds.</summary>
    private const double Reach = 4.4;

    /// <summary>Hold the open, unpainted ground round a new keep for the company's facilities.</summary>
    private static void Reserve(Town town, BastionState b, Lot keep)
    {
        var plan = town.Plan;
        int r = (int)Math.Ceiling(Reach * Land.Res);
        int cx = (int)(keep.X * Land.Res), cy = (int)(keep.Y * Land.Res);
        for (int y = Math.Max(0, cy - r); y <= Math.Min(Land.Size - 1, cy + r); y++)
            for (int x = Math.Max(0, cx - r); x <= Math.Min(Land.Size - 1, cx + r); x++)
            {
                int i = y * Land.Size + x;
                double dx = x + 0.5 - keep.X * Land.Res, dy = y + 0.5 - keep.Y * Land.Res;
                if (dx * dx + dy * dy > r * r || plan.Taken[i] != 0 || plan.Paved[i] != 0 || plan.Zones[i] != (byte)Zone.None || plan.Grounds[i] != 0 || !Land.Open(town, i)) continue;
                plan.Grounds[i] = (byte)b.Ground;
            }
        plan.Changed();
    }

    /// <summary>Spots round a keep for a facility of this kind, behind it first and round to the sides, each facing the keep.</summary>
    public static IEnumerable<Lot> Spots(Town town, Lot keep, BuildingDef d, int ground)
    {
        var (w, dd) = Land.Footprint(d);
        // The keep's front looks to (−sin, cos); behind it is the other way.
        int[] order = [0, 1, -1, 2, -2, 3, -3, 4, -4, 5, -5, 6, -6, 7, -7];
        for (int ring = 0; ring < Rings.Length; ring++)
            foreach (int k in order)
            {
                double a = keep.Yaw + Math.PI + k * Math.PI / 8 + ring * Math.PI / 16;
                double x = keep.X - Math.Sin(a) * Rings[ring], y = keep.Y + Math.Cos(a) * Rings[ring];
                double yaw = Land.FaceToward(x, y, keep.X, keep.Y);
                if (!City.Allows(town, d, x, y) || !Land.Fits(town, x, y, w, dd, yaw, Zone.None, ground)) continue;
                yield return new Lot(town.Lots.Count, x, y) { W = w, D = dd, Yaw = yaw, Zone = Zone.Civic };
            }
    }

    // The bastion turn ------------------------------------------------------------------------------

    private static void Note(BastionState b, string text)
    {
        b.Recent.Add(text);
        while (b.Recent.Count > 8) b.Recent.RemoveAt(0);
    }

    /// <summary>Every staffed facility carries out its order, then something may befall the place.</summary>
    private static void Turn(Town town, BastionState b)
    {
        b.Turns++;
        var home = Members(town, b).Where(m => !m.IsAway).OrderBy(m => m.Id).ToList();
        var done = new List<string>();
        foreach (int id in b.Facilities)
        {
            var f = town.Building(id);
            var def = Facility(town, f.Def);
            if (def == null || !f.Complete || f.Closed || f.Workers.Count == 0) continue;
            string? line = Order(town, b, f, def, home);
            if (line != null) done.Add(line);
        }
        if (done.Count > 0)
        {
            string text = $"Bastion turn at {b.Name}: {string.Join("; ", done)}.";
            Note(b, $"Week {b.Turns}: {string.Join("; ", done)}.");
            town.Log(LedgerCategory.Quest, Importance.Routine, text, home.Select(m => m.Id).ToList());
        }
        Befall(town, b, home);
    }

    private static string? Order(Town town, BastionState b, Building f, FacilityDef def, List<Agent> home)
    {
        switch (def.Order)
        {
            case "harvest":
            {
                int made = (int)def.Amount * Math.Max(1, home.Count), given = 0;
                foreach (var m in home)
                    while (made > 0 && m.Hero!.Potions < def.Carry)
                    {
                        m.Hero.Potions++;
                        made--;
                        given++;
                    }
                double sold = made * def.Sale;
                b.Chest += sold;
                return given > 0 ? $"the gardeners brew {given} healing {(given == 1 ? "draught" : "draughts")}" + (sold > 0 ? $" and sell the rest for {Js.Str(sold)} gold" : "")
                    : sold > 0 ? $"the gardeners sell their draughts for {Js.Str(sold)} gold" : null;
            }
            case "drill":
                if (b.Veterancy >= Rules(town).MaxVeterancy) return $"{f.Workers.Count} sentries stand watch";
                b.Veterancy++;
                return $"{f.Workers.Count} sentries drill in the yard";
            case "craft":
            {
                foreach (var m in home.OrderBy(m => Gear.Edge(town.Content, m)).ThenBy(m => m.Id))
                    if (Gear.Forge(town, m) is string made) return $"the smiths make {m.GivenName} {made}";
                return null;
            }
            case "research":
            {
                var site = town.Sites
                    .Where(s => s.Known == SiteKnowledge.Unheard && !s.Cleared)
                    .OrderBy(s => s.Def.Distance)
                    .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (site == null) return null;
                Rumors.Hear(town, site, Rumors.Occupants(town.Content, site.Def), site.Def.Tier, def.Trust);
                town.Log(LedgerCategory.Rumor, Importance.Notable, $"The cartographer at {b.Name} pieces together old charts and travellers' tales: there is something at {site.Def.Name}.");
                return $"the map room finds word of {site.Def.Name}";
            }
            case "tend":
            {
                foreach (var m in home) m.Hero!.Hp = m.Hero.MaxHp;
                int fed = 0;
                foreach (var a in town.Workers.Where(a => a.Hunger >= 60).OrderByDescending(a => a.Hunger).ThenBy(a => a.Id).Take((int)def.Amount).ToList())
                {
                    b.Chest -= town.Market.Buy(Good.Food, 1);
                    a.Hunger = 0;
                    fed++;
                }
                return fed > 0 ? $"the acolytes feed {fed} hungry townsfolk" : home.Count > 0 ? "the acolytes tend the company's wounds" : null;
            }
            case "trade":
            {
                double gain = Js.Round(def.Amount + def.PerHundred * town.Population / 100.0);
                double tax = Js.Round(gain * town.TaxRate);
                b.Chest += gain - tax;
                town.Book(TreasuryAccount.SalesTax, tax);
                return $"the factors turn {Js.Str(gain)} gold of trade";
            }
            case "train":
            {
                if (home.Count == 0) return null;
                foreach (var m in home) Quests.GainXp(town, m, (int)(def.Amount + def.PerLevel * m.Hero!.Level));
                return $"the drillmaster puts {Expedition.ListNames(home.Select(m => m.GivenName).ToList())} through their paces";
            }
            case "recruit":
            {
                if (b.Members.Count >= Rules(town).Members.Max || town.Heroes.Count >= City.HeroCap(town) + 1) return null;
                var hero = Guest(town, b, (int)def.Amount);
                return $"{hero.Name} answers the banners";
            }
            case "vault":
            {
                var item = Wonder(town, (int)def.Amount);
                if (item == null) return null;
                Bestow(town, b, home, item);
                return $"the vault gives up {item.Name}";
            }
        }
        return null;
    }

    /// <summary>A hero comes to join the company, and lives at the bastion.</summary>
    private static Agent Guest(Town town, BastionState b, int level)
    {
        var a = Quests.CreateHero(town, Quests.PickClass(town), Math.Max(1, level));
        a.Hero!.PartyName = b.Company;
        if (b.Members.Count < Rules(town).Members.Max)
        {
            b.Members.Add(a.Id);
            a.Hero.BastionId = b.Id;
        }
        town.Log(LedgerCategory.Arrival, Importance.Notable, $"{a.Describe()}, rides up to {b.Name} to join the {b.Company}. {Annals.Past(town, a)} ({a.TraitList()})", [a.Id]);
        return a;
    }

    private static LibItem? Wonder(Town town, int tier)
    {
        var items = town.Content.LootItems.Where(i => i.Tier >= 1 && i.Tier <= tier).OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
        return items.Count == 0 ? null : items[town.Rng.Range(0, items.Count)];
    }

    /// <summary>Whoever gains most wears it; if nobody does, it goes to the town hall.</summary>
    private static void Bestow(Town town, BastionState b, List<Agent> home, LibItem item)
    {
        var to = home.OrderByDescending(m => Gear.Gain(town.Content, m, item)).ThenBy(m => m.Id).FirstOrDefault();
        if (to != null && Gear.Gain(town.Content, to, item) > 0) Gear.Claim(town, to, item);
        else Watch.ReceiveRelic(town, item, b.Company);
    }

    private static void Befall(Town town, BastionState b, List<Agent> home)
    {
        var events = Rules(town).Events;
        int total = events.Sum(e => e.Weight);
        if (total <= 0) return;
        int roll = town.Rng.Range(0, total);
        var ev = events[^1];
        foreach (var e in events)
        {
            if (roll < e.Weight)
            {
                ev = e;
                break;
            }
            roll -= e.Weight;
        }
        var staffed = b.Facilities.Select(town.Building).Where(f => f.Complete && f.Workers.Count > 0).OrderBy(f => f.Id).ToList();
        var vars = new Dictionary<string, string> { ["company"] = b.Company, ["bastion"] = b.Name, ["town"] = town.Name };
        string? text = null;
        var imp = Importance.Notable;
        switch (ev.Id)
        {
            case "visitors":
                b.Chest += ev.Gold;
                vars["gold"] = Js.Str(ev.Gold);
                text = Fill(ev.Text, vars);
                break;
            case "attack":
            {
                int sentries = staffed.Where(f => Facility(town, f.Def)?.Order == "drill").Sum(f => f.Workers.Count);
                int check = town.Rng.Range(1, 21) + sentries * 2 + b.Veterancy + home.Count * 3;
                if (check >= 15)
                {
                    foreach (var m in home) Notoriety.Deed(town, m, "slay", 1);
                    text = Fill(ev.Text, vars) + " " + (home.Count > 0 ? "The company is home, and the raiders are cut down in the yard." : $"The sentries drive them off.");
                }
                else
                {
                    double lost = Js.Round(b.Chest * 0.15);
                    b.Chest -= lost;
                    var yard = staffed.FirstOrDefault(f => Facility(town, f.Def)?.Order == "drill");
                    if (yard != null) Fire(town, yard, yard.Workers[^1]);
                    text = Fill(ev.Text, vars) + $" They get away with {Js.Str(lost)} gold" + (yard != null ? ", and a sentry is hurt too badly to stand watch again." : ".");
                    imp = Importance.Major;
                }
                break;
            }
            case "thief":
            {
                var f = staffed.FirstOrDefault();
                if (f == null || b.Chest <= 0) break;
                int wid = f.Workers[^1];
                vars["hireling"] = town.Agent(wid).Name;
                vars["facility"] = f.Name;
                double lost = Js.Round(b.Chest * ev.Gold);
                b.Chest -= lost;
                Fire(town, f, wid);
                text = Fill(ev.Text, vars) + $" {Js.Str(lost)} gold is gone.";
                break;
            }
            case "quit":
            {
                var f = staffed.Count > 0 ? staffed[town.Rng.Range(0, staffed.Count)] : null;
                if (f == null) break;
                vars["facility"] = f.Name;
                Economy.ReleaseWorkers(town, f);
                text = Fill(ev.Text, vars);
                break;
            }
            case "refugees":
                Population.Settle(town, ev.Settlers);
                text = Fill(ev.Text, vars);
                break;
            case "guest":
            {
                if (town.Heroes.Count >= City.HeroCap(town) + 1) break;
                int level = Math.Max(1, Level(town, b) - 4);
                var hero = Guest(town, b, level);
                Note(b, $"{hero.Name} rides up asking to join the company.");
                break;
            }
            case "treasure":
            {
                var item = Wonder(town, 1);
                if (item == null) break;
                vars["item"] = item.Name;
                text = Fill(ev.Text, vars);
                town.Log(LedgerCategory.Quest, imp, text, home.Select(m => m.Id).ToList());
                Note(b, text);
                Bestow(town, b, home, item);
                return;
            }
            case "aid":
            {
                if (town.Treasury >= 200 || b.Chest < ev.Gold * 2) break;
                b.Chest -= ev.Gold;
                town.Book(TreasuryAccount.Bastions, ev.Gold);
                town.Mandate = Math.Min(100, town.Mandate + ev.Mandate);
                Intel.Deal(town, b.Company, 3);
                vars["gold"] = Js.Str(ev.Gold);
                text = Fill(ev.Text, vars);
                break;
            }
        }
        if (text == null) return;
        Note(b, text);
        town.Log(LedgerCategory.Quest, imp, text, home.Select(m => m.Id).ToList());
    }

    private static void Fire(Town town, Building f, int wid)
    {
        Population.Remove(f.Workers, wid);
        town.Agent(wid).JobId = null;
    }

    private static string Fill(string text, Dictionary<string, string> vars)
    {
        foreach (var (k, v) in vars) text = text.Replace("{" + k + "}", v);
        return text;
    }
}
