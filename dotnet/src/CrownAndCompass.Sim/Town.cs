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

    /// <summary>A site: the share of its timber and stone carried onto it so far (<see cref="Systems.Construction"/>).</summary>
    public double Hauled { get; set; } = 1;

    /// <summary>Set once the completion has been handled (workers released, owner moved in).</summary>
    public bool Opened { get; set; }

    public double LaborBudget { get; set; }

    /// <summary>A monument's part: the building it belongs to (<see cref="Systems.Monuments"/>).</summary>
    public int? ParentId { get; set; }

    /// <summary>A monument: whether the council adds its next part on its own when the treasury can spare it.</summary>
    public bool Grow { get; set; } = true;

    /// <summary>A council site whose timber and stone come off the market as the haulers carry them, paid load by load (<see cref="Systems.Construction.Haul"/>).</summary>
    public bool Draws { get; set; }

    /// <summary>A site the player put first: builders and the town's timber and stone go to it ahead of the others.</summary>
    public bool Priority { get; set; }

    /// <summary>A council site waiting for timber or stone the market hasn't got: the tick it stalled, or -1.</summary>
    public int StalledSince { get; set; } = -1;

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

    /// <summary>Visitor-hours today (an hour each townsperson spent in on an outing), hours someone found it full and went elsewhere, the most in at once, and the same for yesterday (<see cref="Systems.Crowds"/>).</summary>
    public int Visits { get; set; }
    public int TurnedAway { get; set; }
    public int Peak { get; set; }
    public int VisitsLast { get; set; }
    public int TurnedAwayLast { get; set; }
    public int PeakLast { get; set; }
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

    /// <summary>As <see cref="Raise(double)"/>, no further than the materials carried onto the site allow.</summary>
    public void Raise(Town town, double hours) =>
        BuildProgress = Math.Min(BuildProgress + hours, Math.Max(BuildProgress, Math.Min(Funded, Systems.Construction.Ready(town, this))));

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

    // What the town bought from the market today and yesterday, and what the caravans had to bring in yesterday.
    private readonly Dictionary<Good, double> _bought = [];
    private readonly Dictionary<Good, double> _boughtYesterday = [];
    private readonly Dictionary<Good, double> _importedYesterday = [];
    private readonly Dictionary<Good, double> _importedToday = [];

    public double BoughtYesterday(Good g) => _boughtYesterday.GetValueOrDefault(g);

    public double ImportedYesterday(Good g) => _importedYesterday.GetValueOrDefault(g);

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
        _bought[g] = _bought.GetValueOrDefault(g) + qty;
        _importedToday[g] = _importedToday.GetValueOrDefault(g) + qty - fromStock;
        return cost;
    }

    /// <summary>Take up to qty from the stock alone (no caravan), returning how much there was and what it cost.</summary>
    public (double Qty, double Cost) Take(Good g, double qty)
    {
        double took = Math.Min(qty, Stock(g));
        if (took <= 0) return (0, 0);
        double cost = took * Price(g);
        _stock[g] = Stock(g) - took;
        return (took, cost);
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
            _boughtYesterday[g] = _bought.GetValueOrDefault(g);
            _bought[g] = 0;
            _importedYesterday[g] = _importedToday.GetValueOrDefault(g);
            _importedToday[g] = 0;
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
    public const string CountingHouse = "Counting house";
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
    public const string Broadsheet = "Broadsheet";
    public const string Clockwork = "Clockwork";
    public const string Orchards = "Orchards";
    public const string Stores = "Winter stores";
    public const string Intel = "Intel";
    public const string Stories = "Stories";
    public const string GrandWorks = "Grand works";
    public const string Mercenaries = "Mercenaries";
    public const string Army = "Army";
    public const string Powers = "Minor powers";
    public const string Decrees = "Decrees";
    public const string Trade = "Trade";
    public const string Armory = "Armory";
    public const string Alchemy = "Alchemy";
    public const string Bastions = "Bastions";
    public const string Callings = "Callings";
    public const string Legacies = "Legacies";
    public const string Undercity = "Undercity";
    public const string Research = "Research";
    public const string Region = "The region";
    public const string Institutions = "Institutions";
    public const string Tourney = "Tourney";
    public const string Guilds = "Guilds";
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
    public const int Grid = 96;

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

    /// <summary>Townsfolk carried off in raids, held until rescued or lost (<see cref="Captives"/>).</summary>
    public List<Captivity> Captivities { get; } = [];

    /// <summary>Companies wiped out in the wilds, and the gear that lies with them (<see cref="Remains"/>).</summary>
    public List<FallenCompany> Fallen { get; } = [];

    /// <summary>The town broadsheet: its issues and the slant paid for in the next (<see cref="Systems.Broadsheet"/>).</summary>
    public PressState Press { get; } = new();

    /// <summary>Second-hand magic items on the shops' shelves (<see cref="Systems.Merchants"/>).</summary>
    public List<Ware> Wares { get; } = [];

    /// <summary>Companies' own compounds in town, by founding (<see cref="Systems.Bastions"/>).</summary>
    public List<BastionState> Bastions { get; } = [];

    /// <summary>Companies' requests for the council's leave to raise a bastion, oldest first.</summary>
    public List<BastionRequest> BastionRequests { get; } = [];
    public List<Chronicle> Chronicles { get; } = [];

    /// <summary>Heroes' recent level-ups and magic items, for the client's cards (<see cref="Systems.Moments"/>).</summary>
    public List<HeroMoment> Moments { get; } = [];

    /// <summary>The dragon's egg the town holds, or the dragon it hatched (<see cref="Systems.Eggs"/>).</summary>
    public Systems.DragonEgg? Egg { get; set; }

    /// <summary>Raiders who fell at the town since a spirit-caller last called them up (foes.json).</summary>
    public double FallenRaiders { get; set; }

    /// <summary>The rival dark ranger's dealings with the town, once she has started taking its bounties.</summary>
    public Systems.RangerState? Ranger { get; set; }

    public List<Seat> Seats { get; } = [];
    public List<Motion> Motions { get; } = [];

    /// <summary>The themed regions of the wilds, nearest ring first (<see cref="World"/>).</summary>
    public List<Region> Regions { get; } = [];

    /// <summary>The country that bends the roads between the town and its sites (<see cref="Systems.Roads"/>).</summary>
    public List<Feature> Terrain { get; } = [];

    /// <summary>Old gateways in the wilds that link two far places (<see cref="Systems.Wayfaring"/>).</summary>
    public List<Gateway> Gateways { get; } = [];

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

    /// <summary>Stone circles companies woke for the town, oldest first (<see cref="Systems.Circles"/>).</summary>
    public List<Systems.StoneCircle> Circles { get; } = [];

    /// <summary>Townsfolk killed by drunken heroes in tavern brawls, and whether their kin were paid (<see cref="Systems.Taverns"/>).</summary>
    public List<Systems.BloodDebt> BloodDebts { get; } = [];

    /// <summary>The law book: the punishment the council has set for each crime, by crime id (<see cref="Systems.Laws"/>).</summary>
    public Dictionary<string, string> Laws { get; } = new();

    /// <summary>Scouts posted to watch sites, oldest first.</summary>
    public List<Scout> Scouts { get; } = [];

    /// <summary>Enemies on the move that someone saw and reported (<see cref="Systems.Sightings"/>), and how many reports there have been.</summary>
    public List<Systems.Sighting> Sightings { get; } = [];

    /// <summary>Every foe and power the town has come across, in the order it met them (<see cref="Systems.Discovery"/>).</summary>
    public List<Systems.Meeting> Discovered { get; } = [];

    /// <summary>Whether the peoples are met one by one (<see cref="Systems.Neighbours.Know"/>); false in saves from before.</summary>
    public bool PeoplesMet { get; set; }
    public int SightingCount { get; set; }

    /// <summary>Enemies' agents loose in town, looking over what they mean to strike tonight (<see cref="Systems.Lurkers"/>).</summary>
    public List<Systems.Lurker> Lurkers { get; } = [];
    public int LurkerCount { get; set; }

    /// <summary>What the mayor has looked for, and found, in councillors' pasts (<see cref="Systems.Secrets"/>).</summary>
    public List<Systems.Secret> Secrets { get; } = [];

    /// <summary>The trade guilds that have formed, in the order they formed (<see cref="Systems.Guilds"/>).</summary>
    public List<Systems.Guild> Guilds { get; } = [];

    /// <summary>Where the town stands between its guilds and its mages (<see cref="Systems.Union"/>).</summary>
    public Systems.UnionState Union { get; } = new();

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

    /// <summary>Clockwork wardens on the walls (<see cref="Systems.Clockwork"/>).</summary>
    public ClockworkState Clockwork { get; } = new();

    /// <summary>The town's services weighed against its income, and the unrest when they go short (<see cref="Systems.Taxes"/>).</summary>
    public TaxState Taxes { get; } = new();

    /// <summary>The last month of the town's key numbers, for the top bar (<see cref="Systems.Trends"/>).</summary>
    public TrendState Trends { get; } = new();

    /// <summary>Sellsword companies under contract to the town (<see cref="Systems.Mercenaries"/>).</summary>
    public List<Hire> Hires { get; } = [];

    /// <summary>The town's own regiments and the levy laws (<see cref="Systems.Army"/>).</summary>
    public ArmyState Army { get; } = new();

    /// <summary>The minor powers and whose side each is on (<see cref="Systems.Powers"/>).</summary>
    public List<PowerState> Powers { get; } = [];

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

    /// <summary>Tick the last festival began, by motion or by the mayor's own word, or null before the first.</summary>
    public int? FestivalTick { get; set; }

    public EnemyState Enemy { get; } = new();

    /// <summary>The campaign arc: its acts, the menaces abroad, and the town's dread (<see cref="Systems.Saga"/>).</summary>
    public SagaState Saga { get; } = new();

    /// <summary>The town's tier, its wards and what its households want (<see cref="Systems.City"/>).</summary>
    public CityState City { get; } = new();
    public Defenses Defenses { get; } = new();

    /// <summary>The town guard on the walls and the orders it holds (<see cref="Systems.Garrison"/>).</summary>
    public GarrisonState Garrison { get; } = new();

    /// <summary>The last few sieges as they were fought, for the map to play back (<see cref="Systems.Battles"/>).</summary>
    public List<Systems.Battle> Battles { get; } = [];

    /// <summary>Trees, benches, lamps, statues: the set dressing the player has placed (<see cref="Systems.Decor"/>).</summary>
    public DecorState Decor { get; } = new();

    /// <summary>The charter goals met in the first days (<see cref="Systems.Goals"/>).</summary>
    public Systems.GoalsState Goals { get; } = new();

    /// <summary>The turning year: festival days and the visitors (<see cref="Systems.Seasons"/>).</summary>
    public SeasonState Seasons { get; } = new();

    /// <summary>The town's trade routes to the other settlements (<see cref="Systems.Trade"/>).</summary>
    public TradeState Trade { get; } = new();

    /// <summary>The town armory: items to gift heroes, and loot offered to the town (<see cref="Systems.Armory"/>).</summary>
    public ArmoryState Armory { get; } = new();

    /// <summary>The alchemy bench: monster parts in store, potions on the shelf and the town's recipe book (<see cref="Systems.Alchemy"/>).</summary>
    public AlchemyState Alchemy { get; } = new();

    /// <summary>The recipes the town has named, for tutorial steps to count.</summary>
    public IReadOnlyList<RecipeEntry> Recipes => Alchemy.Book;

    /// <summary>The people of the wilds the town has dealt with, who remember it (<see cref="Systems.Neighbours"/>).</summary>
    public NeighbourState Neighbours { get; } = new();

    /// <summary>The hall of heroes: those who fell in the town's service or retired here, in the order they did (<see cref="Systems.Legacies"/>).</summary>
    public List<Systems.Legacy> Legacies { get; } = [];

    /// <summary>Enemy powers' end-game crises and the clashes between them (<see cref="Systems.Crises"/>).</summary>
    public Systems.CrisisBook Crises { get; } = new();

    /// <summary>Foes' grudges against the town (<see cref="Systems.Vengeance"/>).</summary>
    public List<Systems.Grudge> Grudges { get; } = [];
    public List<Systems.Mole> Moles { get; } = [];

    /// <summary>The open levels of the undercity, shallowest first (<see cref="Systems.Undercity"/>).</summary>
    public List<Systems.UnderLevel> Undercity { get; } = [];

    /// <summary>The library, its spellbooks and what the town's scholars have discovered.</summary>
    public Systems.ResearchState Research { get; } = new();

    /// <summary>The city's institutions, in the order they opened (<see cref="Systems.Institutions"/>).</summary>
    public List<Systems.InstitutionState> Institutions { get; } = [];

    /// <summary>The institutions the town has heard of, with what revealed each; the rest stay blank slots (<see cref="Systems.Institutions.Seen"/>).</summary>
    public OrderedMap<string, string> InstitutionsSeen { get; } = new();

    /// <summary>The yearly tourney: this year's draw, heats and bets, and past champions (<see cref="Systems.Tournaments"/>).</summary>
    public Systems.TourneyState Tourney { get; } = new();

    /// <summary>Townsfolk's inspirations and breaks: when the Ledger last told of one (<see cref="Systems.Moods"/>).</summary>
    public Systems.MoodState Moods { get; } = new();

    /// <summary>The storyteller the town was founded under (storytellers.json id); null in a save from before there were any (<see cref="Systems.Storyteller"/>).</summary>
    public string? Storyteller { get; set; }

    /// <summary>Where the town was founded (founding.json id); null for the river ford, and in saves from before the choice (<see cref="Systems.Founding"/>).</summary>
    public string? Founding { get; set; }

    /// <summary>The town's ships, for a town on the sea (<see cref="Systems.Fleet"/>).</summary>
    public List<Systems.Ship> Ships { get; } = [];
    public Systems.FleetState FleetState { get; } = new();

    /// <summary>The mayor's character sheet and the story events open and past (<see cref="Systems.Stories"/>).</summary>
    public Systems.StoryState Stories => _stories ??= new Systems.StoryState { Mayor = Systems.Stories.NewMayor(this) };
    // Made on first use, so a save from before stories existed gets a mayor too.
    private Systems.StoryState? _stories;

    /// <summary>The roads to victory open in this campaign and how long each has held (<see cref="Systems.Victory"/>).</summary>
    public Systems.VictoryState Victory { get; } = new();

    /// <summary>Boons from the player's earlier wins the town was founded with, by id, at their rank (<see cref="Systems.Boons"/>).</summary>
    public OrderedMap<string, int> Boons { get; } = new();

    /// <summary>How the campaign ended: won by one of the roads (<see cref="Systems.Victory"/>), or the town fallen.</summary>
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
            Hauled = Systems.Construction.Start(def, complete),
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

    /// <summary>While the townsfolk choose their hour (Brain.Hourly): the places open to visitors by kind, and how many are out at each (<see cref="Systems.Crowds"/>).</summary>
    [NotSaved]
    internal OrderedMap<string, List<Building>>? HourPlaces;
    [NotSaved]
    internal List<string>? HourErrands;
    [NotSaved]
    internal Dictionary<int, int>? Present;
    [NotSaved]
    internal int? PresentWasAt;
    /// <summary>The decree and institution yield of each good, worked out once an hour (<see cref="Systems.Economy"/>).</summary>
    [NotSaved]
    internal double[]? HourYields;
    [NotSaved]
    internal int HourYieldsAt = -1;

    public List<Building> Taverns => Buildings.Where(b => b.Complete && b.Def.Venue && !b.Closed && !b.ForSale).ToList();

    public int BusinessCount() => Buildings.Count(b => b.Def.IsBusiness);

    public string OwnerName(int? ownerId) => ownerId == null ? "the town" : Agent(ownerId.Value).Name;

    public LedgerEntry Log(LedgerCategory cat, Importance imp, string text, IReadOnlyList<int>? agents = null) =>
        Ledger.Write(Tick, cat, imp, text, agents);
}
