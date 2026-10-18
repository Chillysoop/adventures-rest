using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim;

/// <summary>The patch of ground one building stands on, anywhere and at any heading inside the town (<see cref="Systems.Land"/>).</summary>
public sealed class Lot(int id, double x, double y)
{
    public int Id { get; } = id;

    /// <summary>Centre, in tiles from the town's corner.</summary>
    public double X { get; } = x;
    public double Y { get; } = y;

    /// <summary>Footprint in plan cells: width along the front, depth front to back.</summary>
    public int W { get; init; } = 3;
    public int D { get; init; } = 3;

    /// <summary>Heading in radians: 0 faces +y, and the front turns to (−sin, cos).</summary>
    public double Yaw { get; init; }

    public Zone Zone { get; set; } = Zone.None;

    /// <summary>Agent id, or null when the town (the mayor's charter) owns it.</summary>
    public int? OwnerId { get; set; }

    public int? BuildingId { get; set; }

    public bool IsVacant => BuildingId == null;
}

public sealed class Building(int id, BuildingDef def, int lotId)
{
    public int Id { get; } = id;
    /// <summary>What it is; a house that grows a storey or falls back takes a new one.</summary>
    public BuildingDef Def { get; set; } = def;
    public int LotId { get; } = lotId;

    public string Name { get; set; } = "";
    public int? OwnerId { get; set; }
    public double BuildProgress { get; set; }

    /// <summary>Set once the completion has been handled (workers released, owner moved in).</summary>
    public bool Opened { get; set; }

    public double LaborBudget { get; set; }

    /// <summary>Business account: revenue in, wages and inputs out.</summary>
    public double Cash { get; set; }

    public double RevenueToday { get; set; }

    /// <summary>How today's work went at its craft check, in percent of yield (<see cref="Systems.Skills.Daily"/>).</summary>
    public int KnackPct { get; set; }

    /// <summary>Walks to or from it since yesterday while it stands off the streets, and the sum of where they came from (tiles).</summary>
    public int Trips { get; set; }
    public double TripX { get; set; }
    public double TripY { get; set; }

    /// <summary>Patron-hours of evening talk since the last rumour roll (taverns only).</summary>
    public int Talk { get; set; }
    public int DaysInDebt { get; set; }
    public bool ForSale { get; set; }
    public bool Closed { get; set; }

    /// <summary>A house: how content its families were yesterday with what the town gives them (0 to 1).</summary>
    public double Contentment { get; set; } = 1;

    /// <summary>Days in a row its families were content enough to grow, or wanting enough to fall back.</summary>
    public int UpDays { get; set; }
    public int DownDays { get; set; }

    public OrderedMap<Good, double> Stock { get; } = new();
    public List<int> Workers { get; } = [];
    public List<int> Residents { get; } = [];

    /// <summary>A grand work: stages funded so far. Builders can't go past the last funded stage (<see cref="Systems.GrandWorks"/>).</summary>
    public int Stage { get; set; }

    /// <summary>Hours builders may reach before the next stage is funded: all of them for any other building.</summary>
    public double Funded => Def.IsGrand ? Def.Stages.Take(Stage).Sum(s => s.Hours) : Def.BuildHours;

    /// <summary>Builders' work on a site, stopping at the funded stage.</summary>
    public void Raise(double hours) => BuildProgress = Math.Min(BuildProgress + hours, Math.Max(BuildProgress, Funded));

    public bool Complete => BuildProgress >= Def.BuildHours;
    public bool IsSite => !Complete;
    /// <summary>The town hall's road crews: extra hands the treasury hires out of the town's upkeep.</summary>
    public int Crew { get; set; }

    public int OpenJobs => Complete && !Closed && !ForSale ? Math.Max(0, Def.Jobs + Crew - Workers.Count) : 0;
    public int OpenBeds => Complete ? Math.Max(0, Def.Housing - Residents.Count) : 0;

    public double StockOf(Good g) => Stock.Get(g, 0);
    public void AddStock(Good g, double qty) => Stock[g] = StockOf(g) + qty;
}

/// <summary>
/// The town exchange. Producers sell into it and consumers buy from it; prices float with stock.
/// Caravans cover shortfalls at a steep import price and export surplus, so money flows in and
/// out of the frontier town rather than being conserved.
/// </summary>
public sealed class Market
{
    private readonly Dictionary<Good, double> _base = [];
    private readonly Dictionary<Good, double> _stock = [];
    private readonly Dictionary<Good, double> _target = [];

    // Stock the town keeps back from the export barges: winter stores (see Seasons).
    private readonly Dictionary<Good, double> _hold = [];

    // What makers sold today, and the average price it fetched yesterday.
    private readonly Dictionary<Good, double> _soldQty = [];
    private readonly Dictionary<Good, double> _soldValue = [];
    private readonly Dictionary<Good, double> _realized = [];

    public double ImportedToday { get; private set; }

    /// <param name="basePrice">Each good's price when supply meets demand (content/game/city.json).</param>
    public Market(Func<Good, double> basePrice)
    {
        foreach (var g in Goods.All)
        {
            _base[g] = basePrice(g);
            _stock[g] = 0;
            _target[g] = 20;
            _soldQty[g] = 0;
            _soldValue[g] = 0;
            _realized[g] = 0;
        }
    }

    /// <summary>
    /// What makers actually got for a unit yesterday, before the cut; today's price if nothing sold.
    /// A glut that sinks the price through the day shows here even after the evening caravan clears it.
    /// </summary>
    public double Realized(Good g) => _realized[g] > 0 ? _realized[g] : Price(g);

    public double BasePrice(Good g) => _base[g];

    public double Target(Good g) => _target[g];

    public double Stock(Good g) => _stock[g];

    public void SetTarget(Good g, double target) => _target[g] = Math.Max(5, target);

    /// <summary>Stock up to this much stays in town instead of going out with the evening caravan.</summary>
    public double Held(Good g) => _hold.GetValueOrDefault(g);

    public void Hold(Good g, double qty) => _hold[g] = Math.Max(0, qty);

    public void Seed(Good g, double qty) => _stock[g] = Stock(g) + qty;

    /// <summary>Share of base price the river barges and caravans pay for any surplus: the floor under every price.</summary>
    public double ExportShare { get; set; } = 0.75;

    /// <summary>What the caravans charge for a shortfall, times base price: dearer when the roads are bad.</summary>
    public double ImportPremium { get; set; } = 3.0;

    public double Price(Good g)
    {
        double ratio = Math.Sqrt(_target[g] / (Stock(g) + 1));
        return BasePrice(g) * Math.Max(ExportShare, Math.Min(3.0, ratio));
    }

    /// <summary>Cost of buying qty now, importing the shortfall by caravan at a premium.</summary>
    public double Quote(Good g, double qty)
    {
        double fromStock = Math.Min(qty, Stock(g));
        // A save from before seasons has no premium until the next day sets one.
        return fromStock * Price(g) + (qty - fromStock) * BasePrice(g) * (ImportPremium > 0 ? ImportPremium : 3.0);
    }

    public double Buy(Good g, double qty)
    {
        double cost = Quote(g, qty);
        double fromStock = Math.Min(qty, Stock(g));
        _stock[g] = Stock(g) - fromStock;
        ImportedToday += qty - fromStock;
        return cost;
    }

    public double Sell(Good g, double qty)
    {
        double price = Price(g);
        double revenue = qty * price * 0.9;
        _stock[g] = Stock(g) + qty;
        _soldQty[g] += qty;
        _soldValue[g] += qty * price;
        return revenue;
    }

    /// <summary>Daily caravan: surplus above target is exported, so prices hover near base when supply is healthy.</summary>
    public void DailyCaravan()
    {
        foreach (var g in Goods.All)
        {
            double cap = Math.Max(_target[g] * 1.2, Held(g));
            if (Stock(g) > cap) _stock[g] = cap;
            _realized[g] = _soldQty[g] > 0 ? _soldValue[g] / _soldQty[g] : 0;
            _soldQty[g] = 0;
            _soldValue[g] = 0;
        }
        ImportedToday = 0;
    }
}

/// <summary>The treasury's books.</summary>
public static class TreasuryAccount
{
    public const string SalesTax = "Sales tax";
    public const string AleTax = "Ale tax";
    public const string Rent = "Rent";
    public const string LandSales = "Land sales";
    public const string TownBusinesses = "Town businesses";
    public const string Bailouts = "Bailouts";
    public const string CharterHouses = "Charter houses";
    public const string Bounties = "Bounties";
    public const string Upkeep = "Upkeep";
    public const string Watch = "Watch";
    public const string CouncilMotions = "Council motions";
    public const string Discrepancies = "Discrepancies";
    public const string Defenses = "Defenses";
    public const string Patron = "Patron";
    public const string Scrying = "Scrying";
    public const string Scouts = "Scouts";
    public const string Exploring = "Exploring";
    public const string Sight = "Sight works";
    public const string Envoys = "Envoys";
    public const string Temple = "Temple";
    public const string Outposts = "Outposts";
    public const string Tribute = "Tribute";
    public const string Fines = "Fines";
    public const string Factions = "Factions";
    public const string Works = "Town works";
    public const string Honours = "Honours";
    public const string Charters = "Charters";
    public const string HearthTax = "Hearth tax";
    public const string Wards = "Wards";
    public const string Decorations = "Decorations";
    public const string Orchards = "Orchards";
    public const string Stores = "Winter stores";
    public const string Intel = "Intel";
    public const string Stories = "Stories";
    public const string GrandWorks = "Grand works";
    public const string Mercenaries = "Mercenaries";
    public const string Decrees = "Decrees";
    public const string Trade = "Trade";
    public const string Bastions = "Bastions";
    public const string Callings = "Callings";
    public const string Legacies = "Legacies";
}

/// <summary>How the campaign ended.</summary>
public enum Outcome
{
    Victory,
    Defeat,
}

public sealed class Town
{
    /// <summary>Tiles on a side: the old town in the middle, then the walled ring, then the outer wards (<see cref="Systems.City"/>).</summary>
    public const int Grid = 48;

    public string Name { get; } = "Briarford";
    public int Seed { get; }
    public Rng Rng { get; }
    public Ledger Ledger { get; } = new();
    public Market Market { get; }
    public int Tick { get; set; }
    public double Treasury { get; set; }
    public double TaxRate { get; set; }

    /// <summary>The town's books: gold in (positive) and out (negative) by account, all time and today.</summary>
    public OrderedMap<string, double> Accounts { get; } = new();

    public OrderedMap<string, double> AccountsToday { get; set; } = new();

    /// <summary>The last full day's books, for the player to read.</summary>
    public OrderedMap<string, double> AccountsYesterday { get; set; } = new();

    /// <summary>Net change of the treasury on each recent day, oldest first.</summary>
    public List<double> RecentNet { get; } = [];

    /// <summary>Consecutive days the treasury could not cover upkeep.</summary>
    public int UnpaidDays { get; set; }

    public List<Lot> Lots { get; } = [];

    /// <summary>The painted plan: zones and built ground, cell by cell (<see cref="Systems.Land"/>).</summary>
    public Systems.LandPlan Plan { get; } = new();
    public List<Building> Buildings { get; } = [];
    public List<Agent> Agents { get; } = [];
    public List<SiteState> Sites { get; } = [];
    public List<Bounty> Bounties { get; } = [];
    public List<Party> Parties { get; } = [];

    /// <summary>Adventuring companies the town has dealt with, by first dealing (<see cref="Intel"/>).</summary>
    public List<CompanyState> Companies { get; } = [];

    /// <summary>Companies' own compounds in town, by founding (<see cref="Systems.Bastions"/>).</summary>
    public List<BastionState> Bastions { get; } = [];

    /// <summary>Companies' requests for the council's leave to raise a bastion, oldest first.</summary>
    public List<BastionRequest> BastionRequests { get; } = [];
    public List<Chronicle> Chronicles { get; } = [];
    public List<Seat> Seats { get; } = [];
    public List<Motion> Motions { get; } = [];

    /// <summary>The themed regions of the wilds, nearest ring first (<see cref="World"/>).</summary>
    public List<Region> Regions { get; } = [];

    /// <summary>The country that bends the roads between the town and its sites (<see cref="Systems.Roads"/>).</summary>
    public List<Feature> Terrain { get; } = [];

    /// <summary>Villages, holds and enclaves out in the wilds, by id.</summary>
    public List<Settlement> Settlements { get; } = [];

    /// <summary>Settlements' requests for help, oldest first.</summary>
    public List<Plea> Pleas { get; } = [];

    public int LastEnvoyDay { get; set; } = -99;

    /// <summary>Scryings the mayor has commissioned, oldest first.</summary>
    public List<Scrying> Scryings { get; } = [];

    /// <summary>The council's offers to pay a company to explore a region, oldest first (<see cref="Systems.Explore"/>).</summary>
    public List<Systems.Commission> Commissions { get; } = [];

    /// <summary>Companies sent out exploring, oldest first.</summary>
    public List<Systems.Survey> Surveys { get; } = [];

    /// <summary>What exploring companies have come on, oldest first.</summary>
    public List<Systems.Find> Finds { get; } = [];

    /// <summary>Scouts posted to watch sites, oldest first.</summary>
    public List<Scout> Scouts { get; } = [];

    /// <summary>Mirrors hidden at sites, oldest first; smashed ones stay with SmashedTick set.</summary>
    public List<Mirror> Mirrors { get; } = [];

    /// <summary>Everything the taverns have said about the wilds, oldest first.</summary>
    public List<Rumor> Rumors { get; } = [];

    /// <summary>The pantheon in town: dedications, divine favour, blessings and miracles.</summary>
    public FaithState Faiths { get; } = new();

    /// <summary>Every faction that can take root, active or not, in content order.</summary>
    public List<FactionState> Factions { get; } = [];

    /// <summary>Library items parties brought home for the town (ids), in the order they came.</summary>
    public List<string> Relics { get; } = [];

    /// <summary>Sellsword companies under contract to the town (<see cref="Systems.Mercenaries"/>).</summary>
    public List<Hire> Hires { get; } = [];

    /// <summary>Free scryings from the Scrying Pool used today.</summary>
    public int FreeScryingsUsed { get; set; }

    /// <summary>Sight works whose opening has been announced.</summary>
    public HashSet<int> SightWorksOpened { get; } = [];

    /// <summary>Day a buyer last found no land of each zone, so the notice comes once a week at most.</summary>
    public Dictionary<Zone, int> NoLandDay { get; } = [];

    /// <summary>The townsfolk's approval of the mayor, 0 to 100.</summary>
    public double Mandate { get; set; } = 60;

    /// <summary>Paid watchmen keeping the peace.</summary>
    public int Watch { get; set; }

    /// <summary>The skill checks rolled lately, oldest first, for the client's dice (<see cref="Systems.Skills"/>); not saved.</summary>
    public List<Systems.SkillCheck> Checks => _checks ??= [];
    [NotSaved]
    private List<Systems.SkillCheck>? _checks;

    /// <summary>Checks rolled so far; each check's serial, so the client knows which it has shown.</summary>
    public int CheckSerial { get; set; }

    /// <summary>Days of festival left.</summary>
    public int FestivalDays { get; set; }

    public EnemyState Enemy { get; } = new();

    /// <summary>The campaign arc: its acts, the menaces abroad, and the town's dread (<see cref="Systems.Saga"/>).</summary>
    public SagaState Saga { get; } = new();

    /// <summary>The town's tier, its wards and what its households want (<see cref="Systems.City"/>).</summary>
    public CityState City { get; } = new();
    public Defenses Defenses { get; } = new();

    /// <summary>The town guard on the walls and the orders it holds (<see cref="Systems.Garrison"/>).</summary>
    public GarrisonState Garrison { get; } = new();

    /// <summary>Trees, benches, lamps, statues: the set dressing the player has placed (<see cref="Systems.Decor"/>).</summary>
    public DecorState Decor { get; } = new();

    /// <summary>The turning year: festival days and the visitors (<see cref="Systems.Seasons"/>).</summary>
    public SeasonState Seasons { get; } = new();

    /// <summary>The town's trade routes to the other settlements (<see cref="Systems.Trade"/>).</summary>
    public TradeState Trade { get; } = new();

    /// <summary>The hall of heroes: those who fell in the town's service or retired here, in the order they did (<see cref="Systems.Legacies"/>).</summary>
    public List<Systems.Legacy> Legacies { get; } = [];

    /// <summary>The mayor's character sheet and the story events open and past (<see cref="Systems.Stories"/>).</summary>
    public Systems.StoryState Stories => _stories ??= new Systems.StoryState { Mayor = Systems.Stories.NewMayor(this) };
    // Made on first use, so a save from before stories existed gets a mayor too.
    private Systems.StoryState? _stories;

    /// <summary>How the campaign ended: the big bad slain, or the town fallen.</summary>
    public Outcome? Outcome { get; set; }

    /// <summary>The Director's read of recent drama; it decays each day.</summary>
    public double Tension { get; set; }

    public int QuietDays { get; set; }

    private int _nextAgentId = 1;

    public Content Content { get; }

    public Town(Content content, int seed)
    {
        Content = content;
        Seed = seed;
        Rng = new Rng(seed);
        TaxRate = content.Rules.TaxRate;
        Market = new Market(content.City.BasePrice) { ExportShare = content.City.ExportShare };
    }

    /// <summary>Move gold into (positive) or out of (negative) the treasury, booked under an account.</summary>
    public void Book(string account, double amount)
    {
        Treasury += amount;
        Accounts[account] = Accounts.Get(account, 0) + amount;
        AccountsToday[account] = AccountsToday.Get(account, 0) + amount;
    }

    public int Day => SimTime.DayOf(Tick);
    public int Hour => SimTime.HourOf(Tick);

    public List<Agent> Residents => Agents.Where(a => !a.Departed).ToList();

    /// <summary>Townsfolk who work and rent (heroes live by bounties instead).</summary>
    public List<Agent> Workers => Agents.Where(a => !a.Departed && a.Hero == null).ToList();

    public List<Agent> Heroes => Agents.Where(a => !a.Departed && a.Hero != null).ToList();

    /// <summary>The callings heroes hold (<see cref="Systems.Callings"/>); the tutorial watches it.</summary>
    public IReadOnlyList<int> Callings => Stories.Callings.Keys;

    public int Population
    {
        get
        {
            int n = 0;
            foreach (var a in Agents) if (!a.Departed) n++;
            return n;
        }
    }

    public Agent Agent(int id) => Agents[id - 1];
    public Building Building(int id) => Buildings[id];
    public Lot Lot(int id) => Lots[id];

    public int NewAgentId() => _nextAgentId++;

    /// <summary>The middle of the town, where the hall stands, in tiles from its corner.</summary>
    public const double Center = Grid / 2.0;

    public static double DistanceFromCenter(Lot l) => Math.Sqrt((l.X - Center) * (l.X - Center) + (l.Y - Center) * (l.Y - Center));

    public double LotPrice(Lot lot)
    {
        var ward = Systems.City.WardOf(this, lot);
        return LotPrice(DistanceFromCenter(lot)) * ward.Land * Systems.Density.LandFactor(this, lot) * Systems.Decor.Prestige(this, ward.Id) + Systems.Decor.LandValue(this, lot);
    }

    /// <summary>Land is dearer near the hall (before the ward's own price and the beauty round it).</summary>
    public double LotPrice(double distance) =>
        15 + Math.Min(Population, 300) * 0.4 + Math.Max(0, Population - 300) * 0.1 + Math.Max(0, 8 - distance) * 2;

    public Building AddBuilding(BuildingDef def, Lot lot, int? ownerId, string name, bool complete)
    {
        var b = new Building(Buildings.Count, def, lot.Id)
        {
            OwnerId = ownerId,
            Name = name,
            BuildProgress = complete ? def.BuildHours : 0,
            Opened = complete,
        };
        Buildings.Add(b);
        if (lot.Id != Lots.Count) throw new InvalidOperationException("A lot is claimed once, as soon as it is found.");
        Systems.Decor.ClearLot(this, lot);
        Lots.Add(lot);
        Systems.Land.Claim(this, lot);
        lot.BuildingId = b.Id;
        lot.OwnerId = ownerId;
        return b;
    }

    /// <summary>While the townsfolk choose their hour (Brain.Hourly): the open taverns and how many drink in each.</summary>
    [NotSaved]
    internal List<Building>? HourTaverns;
    [NotSaved]
    internal Dictionary<int, int>? Drinkers;
    [NotSaved]
    internal int? DrinkerWasAt;

    public List<Building> Taverns => Buildings.Where(b => b.Complete && b.Def.Venue && !b.Closed && !b.ForSale).ToList();

    public int BusinessCount() => Buildings.Count(b => b.Def.IsBusiness);

    public string OwnerName(int? ownerId) => ownerId == null ? "the town" : Agent(ownerId.Value).Name;

    public LedgerEntry Log(LedgerCategory cat, Importance imp, string text, IReadOnlyList<int>? agents = null) =>
        Ledger.Write(Tick, cat, imp, text, agents);
}
