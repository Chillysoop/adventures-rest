using CrownAndCompass.Sim.Agents;
using CrownAndCompass.Sim.Core;
using CrownAndCompass.Sim.Systems;
using CrownAndCompass.Sim.World;

namespace CrownAndCompass.Sim;

public sealed record DayStats(
    int Day, int Population, int Homeless, int Unemployed, int Buildings, int Sites,
    double Treasury, double FoodPrice, double AverageMood, double AverageGold);

public sealed class SimulationOptions
{
    public ulong Seed { get; init; } = 1;
    public int Settlers { get; init; } = 14;
    public double StartingTreasury { get; init; } = 250;
    public bool AutoMayor { get; init; } = true;
}

/// <summary>
/// Entry point for clients. The game client calls Step() once per in-game hour at the speed the
/// player picks (real time with pause); tests and the CLI call RunDays() as fast as possible.
/// </summary>
public sealed class Simulation
{
    public Town Town { get; }
    public SimulationOptions Options { get; }
    public List<DayStats> History { get; } = new();

    public Simulation(SimulationOptions options, Content? content = null)
    {
        Options = options;
        Town = new Town(content ?? Content.LoadDefault(), options.Seed);
        Charter();
    }

    public void Step()
    {
        Brain.Hourly(Town);
        Economy.Hourly(Town);
        Property.Hourly(Town);
        Social.Hourly(Town);

        Town.Tick++;
        if (Town.Hour == 0) DailyTick();
    }

    public void RunDays(int days)
    {
        for (int i = 0; i < days * SimTime.HoursPerDay; i++) Step();
    }

    /// <summary>Strategic layer, once per in-game day.</summary>
    private void DailyTick()
    {
        Brain.Daily(Town);
        Economy.Daily(Town);
        Property.Daily(Town);
        Population.DailyDepartures(Town);
        Population.DailyArrivals(Town);
        if (Options.AutoMayor) Mayor.Daily(Town);
        Social.Daily(Town);
        Economy.Daily(Town, jobsAndHousingOnly: true);
        History.Add(Snapshot());
    }

    public DayStats Snapshot()
    {
        var res = Town.Residents.ToList();
        return new DayStats(
            Town.Day - 1, res.Count,
            res.Count(a => a.HomeId == null), res.Count(a => a.JobId == null),
            Town.Buildings.Count(b => b.Complete), Town.Buildings.Count(b => b.IsSite),
            Town.Treasury, Town.Market.Price(Good.Food),
            res.Count > 0 ? res.Average(a => a.LastDayMood) : 0,
            res.Count > 0 ? res.Average(a => a.Gold) : 0);
    }

    /// <summary>Day one: the charter, a wagon of settlers, two charter houses, a farm and a lumber camp.</summary>
    private void Charter()
    {
        var t = Town;
        t.Treasury = Options.StartingTreasury;
        var center = t.LotAt(Town.GridSize / 2 - 1, Town.GridSize / 2 - 1);
        center.Zone = Zone.Civic;
        t.AddBuilding(t.Content.Building("town_hall"), center, null, "the Town Hall", complete: true);

        if (Options.AutoMayor) Mayor.Daily(t);
        else
        {
            foreach (var z in new[] { Zone.Residential, Zone.Residential, Zone.Residential, Zone.Commercial, Zone.Farmland, Zone.Farmland })
                t.Lots.Where(l => l.Zone == Zone.None).OrderBy(Town.DistanceFromCenter).ThenBy(l => l.Id).First().Zone = z;
        }

        var house = t.Content.Building("house");
        for (int i = 0; i < 2; i++)
        {
            var lot = FreeLot(Zone.Residential);
            t.AddBuilding(house, lot, null, Property.NameFor(t, house, null), complete: true);
        }

        var farmer = Population.CreateAgent(t, "farmer", gold: t.Rng.Range(10, 30));
        var woodcutter = Population.CreateAgent(t, "woodcutter", gold: t.Rng.Range(10, 30));
        farmer.Ambition = Ambition.OwnHome; farmer.DreamBusiness = null;
        woodcutter.Ambition = Ambition.OwnHome; woodcutter.DreamBusiness = null;

        var farm = t.AddBuilding(t.Content.Building("farm"), FreeLot(Zone.Farmland), farmer.Id, Property.NameFor(t, t.Content.Building("farm"), farmer), complete: true);
        var camp = t.AddBuilding(t.Content.Building("lumber_camp"), FreeLot(Zone.Farmland), woodcutter.Id, Property.NameFor(t, t.Content.Building("lumber_camp"), woodcutter), complete: true);
        farm.Cash = 40; camp.Cash = 40;

        for (int i = 2; i < Options.Settlers; i++) Population.CreateAgent(t);

        t.Market.Seed(Good.Food, 150);
        t.Market.Seed(Good.Timber, 60);
        t.Market.Seed(Good.Tools, 8);
        t.Market.Seed(Good.Ale, 15);

        Economy.Daily(t, jobsAndHousingOnly: true);
        if (Options.AutoMayor) Mayor.Daily(t);

        t.Log(LedgerCategory.Town, Importance.Major,
            $"The charter is signed. {Options.Settlers} settlers and a wagon arrive at the river ford that will become {t.Name}.",
            t.Residents.Select(a => a.Id).ToArray());
    }

    private Lot FreeLot(Zone zone) =>
        Town.Lots.Where(l => l.Zone == zone && l.IsVacant && l.OwnerId == null)
            .OrderBy(Town.DistanceFromCenter).ThenBy(l => l.Id).First();
}
