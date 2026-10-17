using CrownAndCompass.Sim.Agents;
using CrownAndCompass.Sim.Core;

namespace CrownAndCompass.Sim.World;

public sealed class Lot
{
    public int Id { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public Zone Zone { get; set; }
    /// <summary>Agent id, or null when the town (the mayor's charter) owns it.</summary>
    public int? OwnerId { get; set; }
    public int? BuildingId { get; set; }

    public bool IsVacant => BuildingId == null;
}

public sealed class Building
{
    public int Id { get; init; }
    public BuildingDef Def { get; init; } = null!;
    public int LotId { get; init; }
    public string Name { get; set; } = "";
    public int? OwnerId { get; set; }

    public double BuildProgress { get; set; }
    public bool Complete => BuildProgress >= Def.BuildHours;
    /// <summary>Set once the completion has been handled (workers released, owner moved in).</summary>
    public bool Opened { get; set; }
    public double LaborBudget { get; set; }

    /// <summary>Business account: revenue in, wages and inputs out.</summary>
    public double Cash { get; set; }
    public double RevenueToday { get; set; }
    public int DaysInDebt { get; set; }
    public bool ForSale { get; set; }
    public bool Closed { get; set; }

    public Dictionary<Good, double> Stock { get; } = new();
    public List<int> Workers { get; } = new();
    public List<int> Residents { get; } = new();

    public int OpenJobs => Complete && !Closed && !ForSale ? Math.Max(0, Def.Jobs - Workers.Count) : 0;
    public int OpenBeds => Complete ? Math.Max(0, Def.Housing - Residents.Count) : 0;
    public bool IsSite => !Complete;

    public double StockOf(Good g) => Stock.TryGetValue(g, out var v) ? v : 0;
    public void AddStock(Good g, double qty) => Stock[g] = StockOf(g) + qty;
}

/// <summary>
/// The town exchange. Producers sell into it and consumers buy from it; prices float with stock.
/// Caravans cover shortfalls at a steep import price and soak up surplus cheaply, so money
/// flows in and out of the frontier town rather than being conserved.
/// </summary>
public sealed class Market
{
    public static readonly IReadOnlyDictionary<Good, double> BasePrice = new Dictionary<Good, double>
    {
        [Good.Food] = 1.0, [Good.Timber] = 1.5, [Good.Tools] = 4.0, [Good.Ale] = 1.2,
    };

    private readonly Dictionary<Good, double> _stock = new();
    private readonly Dictionary<Good, double> _target = new();
    public double ImportedToday { get; private set; }

    public Market()
    {
        foreach (var g in Enum.GetValues<Good>()) { _stock[g] = 0; _target[g] = 20; }
    }

    public double Stock(Good g) => _stock[g];
    public void SetTarget(Good g, double target) => _target[g] = Math.Max(5, target);
    public void Seed(Good g, double qty) => _stock[g] += qty;

    public double Price(Good g)
    {
        double ratio = Math.Sqrt(_target[g] / (_stock[g] + 1));
        return BasePrice[g] * Math.Clamp(ratio, 0.4, 3.0);
    }

    /// <summary>Cost of buying qty now, importing the shortfall by caravan at a premium.</summary>
    public double Quote(Good g, double qty)
    {
        double fromStock = Math.Min(qty, _stock[g]);
        return fromStock * Price(g) + (qty - fromStock) * BasePrice[g] * 3.0;
    }

    public double Buy(Good g, double qty)
    {
        double cost = Quote(g, qty);
        double fromStock = Math.Min(qty, _stock[g]);
        _stock[g] -= fromStock;
        ImportedToday += qty - fromStock;
        return cost;
    }

    public double Sell(Good g, double qty)
    {
        double revenue = qty * Price(g) * 0.9;
        _stock[g] += qty;
        return revenue;
    }

    /// <summary>Daily caravan: surplus above target is exported, so prices hover near base when supply is healthy.</summary>
    public void DailyCaravan()
    {
        foreach (var g in Enum.GetValues<Good>())
        {
            double cap = _target[g] * 1.2;
            if (_stock[g] > cap) _stock[g] = cap;
        }
        ImportedToday = 0;
    }
}

public sealed class Town
{
    public const int GridSize = 16;

    public string Name { get; init; } = "Briarford";
    public Content Content { get; }
    public Rng Rng { get; }
    public Ledger Ledger { get; } = new();
    public Market Market { get; } = new();
    public long Tick { get; set; }
    public double Treasury { get; set; }
    public double TaxRate { get; set; } = 0.10;

    public List<Lot> Lots { get; } = new();
    public List<Building> Buildings { get; } = new();
    public List<Agent> Agents { get; } = new();

    private int _nextAgentId = 1;

    public Town(Content content, ulong seed)
    {
        Content = content;
        Rng = new Rng(seed);
        for (int y = 0; y < GridSize; y++)
            for (int x = 0; x < GridSize; x++)
                Lots.Add(new Lot { Id = Lots.Count, X = x, Y = y });
    }

    public int Day => SimTime.DayOf(Tick);
    public int Hour => SimTime.HourOf(Tick);

    public IEnumerable<Agent> Residents => Agents.Where(a => !a.Departed);
    public int Population => Agents.Count(a => !a.Departed);

    public Agent Agent(int id) => Agents[id - 1];
    public Building Building(int id) => Buildings[id];
    public Lot Lot(int id) => Lots[id];
    public Lot LotAt(int x, int y) => Lots[y * GridSize + x];

    public int NewAgentId() => _nextAgentId++;

    public static double DistanceFromCenter(Lot l)
    {
        double c = (GridSize - 1) / 2.0;
        return Math.Sqrt((l.X - c) * (l.X - c) + (l.Y - c) * (l.Y - c));
    }

    public double LotPrice(Lot lot) => 15 + Population * 0.4 + Math.Max(0, 8 - DistanceFromCenter(lot)) * 2;

    public Building AddBuilding(BuildingDef def, Lot lot, int? ownerId, string name, bool complete)
    {
        var b = new Building
        {
            Id = Buildings.Count, Def = def, LotId = lot.Id, OwnerId = ownerId, Name = name,
            BuildProgress = complete ? def.BuildHours : 0,
            Opened = complete,
        };
        Buildings.Add(b);
        lot.BuildingId = b.Id;
        lot.OwnerId = ownerId;
        return b;
    }

    public IEnumerable<Building> Completed => Buildings.Where(b => b.Complete);
    public IEnumerable<Building> Taverns => Buildings.Where(b => b.Complete && b.Def.Venue && !b.Closed && !b.ForSale);

    public string OwnerName(int? ownerId) => ownerId is int id ? Agent(id).Name : "the town";

    public LedgerEntry Log(LedgerCategory cat, Importance imp, string text, params int[] agents) =>
        Ledger.Write(Tick, cat, imp, text, agents);
}
