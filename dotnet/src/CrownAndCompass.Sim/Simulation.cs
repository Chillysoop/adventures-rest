using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim;

public sealed record DayStats(
    int Day,
    int Population,
    int Homeless,
    int Unemployed,
    int Buildings,
    int Sites,
    double Treasury,
    double FoodPrice,
    double AverageMood,
    double AverageGold);

public sealed record SimulationOptions
{
    public int Seed { get; init; } = 1;
    public int Settlers { get; init; } = 14;
    public double StartingTreasury { get; init; } = 250;
    public bool AutoMayor { get; init; } = true;

    /// <summary>Let the stand-in mayor post bounties; off when a player runs the bounty board.</summary>
    public bool AutoBounties { get; init; } = true;

    /// <summary>Settle council motions by majority at once; off when a player signs or vetoes them.</summary>
    public bool AutoCouncil { get; init; } = true;

    /// <summary>Let the stand-in mayor build the palisade and towers; off when a player does.</summary>
    public bool AutoDefense { get; init; } = true;

    /// <summary>Let heroes form free companies and choose sites themselves, beyond the bounty board.</summary>
    public bool FreeCompanies { get; init; } = true;

    /// <summary>Whether the warband exists at all (sandbox and some tests turn it off).</summary>
    public bool Enemy { get; init; } = true;
}

/// <summary>
/// Entry point for clients. The game client calls Step() once per in-game hour at the speed the
/// player picks (real time with pause); tests and the CLI call RunDays() as fast as possible.
/// </summary>
public sealed class Simulation
{
    public Town Town { get; }
    public SimulationOptions Options { get; }
    public List<DayStats> History { get; } = [];

    public Simulation(SimulationOptions? options = null, Content? content = null)
    {
        Options = options ?? new SimulationOptions();
        Town = new Town(content ?? Content.Default(), Options.Seed);
        Charter();
    }

    public void Step()
    {
        var t = Town;
        Brain.Hourly(t);
        Economy.Hourly(t);
        Property.Hourly(t);
        Social.Hourly(t);
        t.Tick++;
        if (t.Hour == 0) DailyTick();
    }

    public void RunDays(int days)
    {
        for (int i = 0; i < days * SimTime.HoursPerDay; i++) Step();
    }

    /// <summary>Strategic layer, once per in-game day.</summary>
    private void DailyTick()
    {
        var t = Town;
        Brain.Daily(t);
        Economy.Daily(t);
        Property.Daily(t);
        Population.DailyDepartures(t);
        Population.DailyArrivals(t);
        if (Options.AutoMayor) Mayor.Daily(t);
        Social.Daily(t);
        Council.Daily(t, Options.AutoCouncil);
        double pace = Director.Daily(t);
        Threat.Daily(t, pace, Options.AutoMayor && Options.AutoDefense, Options.Enemy);
        World.Daily(t, Options.AutoMayor);
        Quests.Daily(t, Options.AutoMayor && Options.AutoBounties, Options.FreeCompanies);
        Watch.Daily(t, Options.AutoMayor && Options.AutoBounties);
        Economy.Daily(t, true);
        History.Add(Snapshot());
        t.AccountsYesterday = t.AccountsToday;
        t.RecentNet.Add(Js.Sum(t.AccountsToday.Values, v => v));
        if (t.RecentNet.Count > 7) t.RecentNet.RemoveAt(0);
        t.AccountsToday = new();
    }

    /// <summary>The player's lever: post a bounty on a site, paid from the treasury into escrow.</summary>
    public Bounty? PostBounty(string siteId, double reward) => Quests.PostBounty(Town, siteId, reward);

    /// <summary>Send an envoy with gifts to a settlement out in the wilds, to win its people over.</summary>
    public bool SendEnvoy(int settlementId) => World.SendEnvoy(Town, settlementId);

    /// <summary>The player's other lever: sign or veto the motion before the council.</summary>
    public bool DecideMotion(int motionId, bool sign) => Council.Decide(Town, motionId, sign);

    /// <summary>Order a palisade or a watchtower.</summary>
    public bool BuildDefense(DefenseWork what) => Threat.Build(Town, what);

    /// <summary>Pay a caster in town to scry a site, showing it live for a few hours.</summary>
    public Scrying? CommissionScrying(string siteId) => Knowledge.CommissionScrying(Town, siteId);

    /// <summary>Reach a party on the road by sending stone: ask for word, or call it home.</summary>
    public string? SendWord(int partyId, bool recall = false) => Knowledge.Send(Town, partyId, recall);

    /// <summary>Post the best scout in town to watch a site from cover.</summary>
    public Scout? PostScout(string siteId) => Watch.PostScout(Town, siteId);

    /// <summary>Call a scout home early.</summary>
    public bool RecallScout(int scoutId) => Watch.RecallScout(Town, scoutId);

    /// <summary>Pay for a mirror pair and have one hidden at a site, for a lasting live view.</summary>
    public Scout? PlantMirror(string siteId) => Watch.PlantMirror(Town, siteId);

    /// <summary>Commission the Scrying Pool or the Sleepless Spire.</summary>
    public Building? BuildSightWork(string buildingId) => Watch.BuildWork(Town, buildingId);

    public DayStats Snapshot()
    {
        var t = Town;
        var res = t.Residents;
        var workers = t.Workers;
        double Avg(Func<Agent, double> f) => res.Count > 0 ? Js.Sum(res, f) / res.Count : 0;
        return new DayStats(
            Day: t.Day - 1,
            Population: res.Count,
            Homeless: workers.Count(a => a.HomeId == null),
            Unemployed: workers.Count(a => a.JobId == null),
            Buildings: t.Buildings.Count(b => b.Complete),
            Sites: t.Buildings.Count(b => b.IsSite),
            Treasury: t.Treasury,
            FoodPrice: t.Market.Price(Good.Food),
            AverageMood: Avg(a => a.LastDayMood),
            AverageGold: Avg(a => a.Gold));
    }

    /// <summary>Day one: the charter, a wagon of settlers, two charter houses, a farm and a lumber camp.</summary>
    private void Charter()
    {
        var t = Town;
        var c = t.Content;
        t.Treasury = Options.StartingTreasury;
        var center = t.LotAt(Town.Grid / 2 - 1, Town.Grid / 2 - 1);
        center.Zone = Zone.Civic;
        t.AddBuilding(c.Building("town_hall"), center, null, "the Town Hall", true);
        Quests.InitSites(t);
        World.Generate(t);
        Council.InitCouncil(t);
        Threat.InitThreat(t);

        if (Options.AutoMayor) Mayor.Daily(t);
        else
        {
            Zone[] zones = [Zone.Residential, Zone.Residential, Zone.Residential, Zone.Commercial, Zone.Farmland, Zone.Farmland];
            foreach (var z in zones)
            {
                var lot = t.Lots
                    .Where(l => l.Zone == Zone.None)
                    .OrderBy(Town.DistanceFromCenter)
                    .ThenBy(l => l.Id)
                    .First();
                lot.Zone = z;
            }
        }

        var house = c.Building("house");
        for (int i = 0; i < 2; i++) t.AddBuilding(house, FreeLot(Zone.Residential), null, Property.NameFor(t, house, null), true);

        var farmer = Population.CreateAgent(t, new NewAgentOptions { Profession = "farmer", Gold = t.Rng.Range(10, 30) });
        var woodcutter = Population.CreateAgent(t, new NewAgentOptions { Profession = "woodcutter", Gold = t.Rng.Range(10, 30) });
        foreach (var owner in new[] { farmer, woodcutter })
        {
            owner.Ambition = Ambition.OwnHome;
            owner.DreamBusiness = null;
        }

        var farmDef = c.Building("farm");
        var campDef = c.Building("lumber_camp");
        var farm = t.AddBuilding(farmDef, FreeLot(Zone.Farmland), farmer.Id, Property.NameFor(t, farmDef, farmer), true);
        var camp = t.AddBuilding(campDef, FreeLot(Zone.Farmland), woodcutter.Id, Property.NameFor(t, campDef, woodcutter), true);
        farm.Cash = 40;
        camp.Cash = 40;

        for (int i = 2; i < Options.Settlers; i++) Population.CreateAgent(t);

        t.Market.Seed(Good.Food, 150);
        t.Market.Seed(Good.Timber, 60);
        t.Market.Seed(Good.Tools, 8);
        t.Market.Seed(Good.Ale, 15);

        Economy.Daily(t, true);
        if (Options.AutoMayor) Mayor.Daily(t);

        t.Log(
            LedgerCategory.Town,
            Importance.Major,
            $"The charter is signed. {Options.Settlers} settlers and a wagon arrive at the river ford that will become {t.Name}.",
            t.Residents.Select(a => a.Id).ToList());
    }

    private Lot FreeLot(Zone zone) => Property.FindLot(Town, zone)!;
}
