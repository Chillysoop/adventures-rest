using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim;

public sealed class Lot(int id, int x, int y)
{
    public int Id { get; } = id;
    public int X { get; } = x;
    public int Y { get; } = y;

    public Zone Zone { get; set; } = Zone.None;

    /// <summary>Agent id, or null when the town (the mayor's charter) owns it.</summary>
    public int? OwnerId { get; set; }

    public int? BuildingId { get; set; }

    public bool IsVacant => BuildingId == null;
}

public sealed class Building(int id, BuildingDef def, int lotId)
{
    public int Id { get; } = id;
    public BuildingDef Def { get; } = def;
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

    /// <summary>Patron-hours of evening talk since the last rumour roll (taverns only).</summary>
    public int Talk { get; set; }
    public int DaysInDebt { get; set; }
    public bool ForSale { get; set; }
    public bool Closed { get; set; }

    public OrderedMap<Good, double> Stock { get; } = new();
    public List<int> Workers { get; } = [];
    public List<int> Residents { get; } = [];

    public bool Complete => BuildProgress >= Def.BuildHours;
    public bool IsSite => !Complete;
    public int OpenJobs => Complete && !Closed && !ForSale ? Math.Max(0, Def.Jobs - Workers.Count) : 0;
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
    public static double BasePrice(Good g) => g switch
    {
        Good.Food => 1.0,
        Good.Timber => 1.5,
        Good.Tools => 4.0,
        Good.Ale => 1.2,
        _ => throw new ArgumentOutOfRangeException(nameof(g)),
    };

    private readonly Dictionary<Good, double> _stock = [];
    private readonly Dictionary<Good, double> _target = [];

    public double ImportedToday { get; private set; }

    public Market()
    {
        foreach (var g in Goods.All)
        {
            _stock[g] = 0;
            _target[g] = 20;
        }
    }

    public double Stock(Good g) => _stock[g];

    public void SetTarget(Good g, double target) => _target[g] = Math.Max(5, target);

    public void Seed(Good g, double qty) => _stock[g] = Stock(g) + qty;

    public double Price(Good g)
    {
        double ratio = Math.Sqrt(_target[g] / (Stock(g) + 1));
        return BasePrice(g) * Math.Max(0.4, Math.Min(3.0, ratio));
    }

    /// <summary>Cost of buying qty now, importing the shortfall by caravan at a premium.</summary>
    public double Quote(Good g, double qty)
    {
        double fromStock = Math.Min(qty, Stock(g));
        return fromStock * Price(g) + (qty - fromStock) * BasePrice(g) * 3.0;
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
        double revenue = qty * Price(g) * 0.9;
        _stock[g] = Stock(g) + qty;
        return revenue;
    }

    /// <summary>Daily caravan: surplus above target is exported, so prices hover near base when supply is healthy.</summary>
    public void DailyCaravan()
    {
        foreach (var g in Goods.All)
        {
            double cap = _target[g] * 1.2;
            if (Stock(g) > cap) _stock[g] = cap;
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
    public const string Sight = "Sight works";
    public const string Envoys = "Envoys";
    public const string Temple = "Temple";
    public const string Outposts = "Outposts";
    public const string Tribute = "Tribute";
    public const string Fines = "Fines";
    public const string Factions = "Factions";
}

/// <summary>How the campaign ended.</summary>
public enum Outcome
{
    Victory,
    Defeat,
}

public sealed class Town
{
    public const int Grid = 16;

    public string Name { get; } = "Briarford";
    public int Seed { get; }
    public Rng Rng { get; }
    public Ledger Ledger { get; } = new();
    public Market Market { get; } = new();
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
    public List<Building> Buildings { get; } = [];
    public List<Agent> Agents { get; } = [];
    public List<SiteState> Sites { get; } = [];
    public List<Bounty> Bounties { get; } = [];
    public List<Party> Parties { get; } = [];
    public List<Chronicle> Chronicles { get; } = [];
    public List<Seat> Seats { get; } = [];
    public List<Motion> Motions { get; } = [];

    /// <summary>The themed regions of the wilds, nearest ring first (<see cref="World"/>).</summary>
    public List<Region> Regions { get; } = [];

    /// <summary>Villages, holds and enclaves out in the wilds, by id.</summary>
    public List<Settlement> Settlements { get; } = [];

    /// <summary>Settlements' requests for help, oldest first.</summary>
    public List<Plea> Pleas { get; } = [];

    public int LastEnvoyDay { get; set; } = -99;

    /// <summary>Scryings the mayor has commissioned, oldest first.</summary>
    public List<Scrying> Scryings { get; } = [];

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

    /// <summary>Free scryings from the Scrying Pool used today.</summary>
    public int FreeScryingsUsed { get; set; }

    /// <summary>Sight works whose opening has been announced.</summary>
    public HashSet<int> SightWorksOpened { get; } = [];

    /// <summary>The townsfolk's approval of the mayor, 0 to 100.</summary>
    public double Mandate { get; set; } = 60;

    /// <summary>Paid watchmen keeping the peace.</summary>
    public int Watch { get; set; }

    /// <summary>Days of festival left.</summary>
    public int FestivalDays { get; set; }

    public EnemyState Enemy { get; } = new();

    /// <summary>The campaign arc: its acts, the menaces abroad, and the town's dread (<see cref="Systems.Saga"/>).</summary>
    public SagaState Saga { get; } = new();
    public Defenses Defenses { get; } = new();

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
        for (int y = 0; y < Grid; y++)
            for (int x = 0; x < Grid; x++)
                Lots.Add(new Lot(Lots.Count, x, y));
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
    public Lot LotAt(int x, int y) => Lots[y * Grid + x];

    public int NewAgentId() => _nextAgentId++;

    public static double DistanceFromCenter(Lot l)
    {
        double c = (Grid - 1) / 2.0;
        return Math.Sqrt((l.X - c) * (l.X - c) + (l.Y - c) * (l.Y - c));
    }

    public double LotPrice(Lot lot) => 15 + Population * 0.4 + Math.Max(0, 8 - DistanceFromCenter(lot)) * 2;

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
        lot.BuildingId = b.Id;
        lot.OwnerId = ownerId;
        return b;
    }

    public List<Building> Taverns => Buildings.Where(b => b.Complete && b.Def.Venue && !b.Closed && !b.ForSale).ToList();

    public int BusinessCount() => Buildings.Count(b => b.Def.IsBusiness);

    public string OwnerName(int? ownerId) => ownerId == null ? "the town" : Agent(ownerId.Value).Name;

    public LedgerEntry Log(LedgerCategory cat, Importance imp, string text, IReadOnlyList<int>? agents = null) =>
        Ledger.Write(Tick, cat, imp, text, agents);
}
