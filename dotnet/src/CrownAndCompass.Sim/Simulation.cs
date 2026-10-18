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

    /// <summary>Let the stand-in mayor zone land and raise government buildings; off when a player plans the town.</summary>
    public bool AutoPlanning { get; init; } = true;

    /// <summary>Let the stand-in mayor post bounties; off when a player runs the bounty board.</summary>
    public bool AutoBounties { get; init; } = true;

    /// <summary>Let the stand-in mayor buy companies' reports of new ground; off when a player deals with the companies.</summary>
    public bool AutoIntel { get; init; } = true;

    /// <summary>Let the stand-in mayor give companies leave to raise bastions; off when a player answers them.</summary>
    public bool AutoBastions { get; init; } = true;

    /// <summary>Settle council motions by majority at once; off when a player signs or vetoes them.</summary>
    public bool AutoCouncil { get; init; } = true;

    /// <summary>Let the stand-in mayor build the palisade and towers; off when a player does.</summary>
    public bool AutoDefense { get; init; } = true;

    /// <summary>Let heroes form free companies and choose sites themselves, beyond the bounty board.</summary>
    public bool FreeCompanies { get; init; } = true;

    /// <summary>Whether the warband exists at all (sandbox and some tests turn it off).</summary>
    public bool Enemy { get; init; } = true;

    /// <summary>Let the stand-in mayor answer story events; off when a player steps into them.</summary>
    public bool AutoStories { get; init; } = true;

    /// <summary>The stand-in mayor answers exploring companies' reports (with AutoBounties); off, they wait for the player.</summary>
    public bool AutoExplore { get; init; } = true;

    /// <summary>Run the campaign arc (acts, festering menaces, dread); off, the Last Stand ends the game.</summary>
    public bool Saga { get; init; } = true;

    /// <summary>Foes and heroes from the player's earlier campaigns, who may come back (<see cref="Systems.Legends"/>).</summary>
    public IReadOnlyList<Legend> Legends { get; init; } = [];
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
        Town.Saga.Enabled = Options.Saga && Options.Enemy;
        Town.Saga.Legends.AddRange(Options.Legends.Where(l => l.Seed != Options.Seed));
        Charter();
    }

    public void Step()
    {
        var t = Town;
        Brain.Hourly(t);
        Economy.Hourly(t);
        Property.Hourly(t);
        Social.Hourly(t);
        Roads.Hourly(t);
        Sendings.Hourly(t, Options.AutoMayor && Options.AutoStories);
        Explore.Hourly(t, Options.AutoMayor && Options.AutoBounties && Options.AutoExplore);
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
        Seasons.Daily(t);
        Economy.Daily(t);
        Merchants.Daily(t);
        City.Daily(t, Options.AutoMayor && Options.AutoPlanning);
        GrandWorks.Daily(t, Options.AutoMayor && Options.AutoPlanning);
        Streets.Wear(t);
        Property.Daily(t);
        Population.DailyDepartures(t);
        Population.DailyArrivals(t);
        if (Options.AutoMayor) Mayor.Daily(t, Options.AutoPlanning, Options.AutoDefense);
        Social.Daily(t);
        Council.Daily(t, Options.AutoCouncil);
        double pace = Director.Daily(t);
        Threat.Daily(t, pace, Options.AutoMayor && Options.AutoDefense, Options.Enemy);
        Garrison.Daily(t, Options.AutoMayor && Options.AutoDefense);
        World.Daily(t, Options.AutoMayor);
        Trade.Daily(t, Options.AutoMayor);
        Neighbours.Daily(t, Options.AutoMayor);
        Mercenaries.Daily(t, Options.AutoMayor && Options.AutoDefense);
        Clockwork.Daily(t, Options.AutoMayor && Options.AutoDefense);
        Quests.Daily(t, Options.AutoMayor && Options.AutoBounties, Options.FreeCompanies);
        Explore.Daily(t, Options.AutoMayor && Options.AutoBounties);
        Intel.Daily(t, Options.AutoMayor && Options.AutoBounties && Options.AutoIntel);
        Bastions.Daily(t, Options.AutoMayor && Options.AutoBastions);
        Watch.Daily(t, Options.AutoMayor && Options.AutoBounties);
        Lairs.Daily(t, Options.AutoMayor);
        Rumors.Daily(t);
        Shrines.Daily(t, Options.AutoMayor);
        Faith.Daily(t);
        Factions.Daily(t, Options.AutoMayor);
        Saga.Daily(t, Options.AutoMayor);
        Turning.Daily(t);
        Legacies.Daily(t, Options.AutoMayor);
        Institutions.Daily(t, Options.AutoMayor && Options.AutoPlanning);
        Regard.Daily(t);
        Wards.Daily(t);
        Broadsheet.Daily(t, Options.AutoMayor && Options.AutoStories);
        Loyalty.Daily(t, Options.AutoMayor);
        Notoriety.Daily(t);
        Decor.Daily(t);
        Stories.Daily(t, Options.AutoMayor && Options.AutoStories);
        Sheet.Daily(t, Options.AutoMayor && Options.AutoStories);
        Economy.Daily(t, true);
        History.Add(Snapshot());
        t.AccountsYesterday = t.AccountsToday;
        t.RecentNet.Add(Js.Sum(t.AccountsToday.Values, v => v));
        if (t.RecentNet.Count > 7) t.RecentNet.RemoveAt(0);
        t.AccountsToday = new();
    }

    /// <summary>The player's lever: post a bounty on a site, paid from the treasury into escrow.</summary>
    public Bounty? PostBounty(string siteId, double reward, string terms = "slay") => Quests.PostBounty(Town, siteId, reward, null, terms);

    /// <summary>The player's lever: pay a company to go and explore a region, the reward held until they come home.</summary>
    public Commission? PostSurvey(int regionId, double? reward = null) => Explore.Post(Town, regionId, reward);

    /// <summary>The player's word to explorers about something they found ("dig", "engage", "onward", "home", "parley"...).</summary>
    public bool OrderExplorers(int findId, string act) => Explore.Order(Town, findId, act);

    /// <summary>Pay the printer for the broadsheet's next front page ("praise", "hero" with a hero, "expose"). Returns why not, or null when bought.</summary>
    public string? SlantBroadsheet(string slant, int? heroId = null) => Broadsheet.Buy(Town, slant, heroId);

    /// <summary>Send an envoy with gifts to a settlement out in the wilds, to win its people over.</summary>
    public bool SendEnvoy(int settlementId) => World.SendEnvoy(Town, settlementId);

    /// <summary>Send coin and goods to see a settlement through its trouble.</summary>
    public bool AidSettlement(int settlementId) => Neighbours.Aid(Town, settlementId);

    /// <summary>Pay for a shared improvement (a watchtower, a bridge, a waystation) at a settlement.</summary>
    public bool FundProject(int settlementId, string projectId) => Neighbours.Fund(Town, settlementId, projectId);

    /// <summary>Open a trade route to a settlement: export goes out, import comes back.</summary>
    public bool OpenTradeRoute(int settlementId, Good export, Good import) => Trade.OpenRoute(Town, settlementId, export, import) != null;

    public bool CloseTradeRoute(int routeId) => Trade.CloseRoute(Town, routeId);

    /// <summary>Raise a memorial to a hero in the hall of heroes.</summary>
    public bool RaiseMemorial(int heroId) => Legacies.RaiseMemorial(Town, heroId);

    /// <summary>Set how generously the council funds an institution: 0 unfunded, 1 modest, 2 generous.</summary>
    public bool FundInstitution(string institutionId, int level) => Institutions.Fund(Town, institutionId, level);

    /// <summary>Make an institution's choice: which god's temple gets the land, the tavern's style, what the bazaar trades in.</summary>
    public bool ChooseInstitution(string institutionId, string option) => Institutions.Choose(Town, institutionId, option);

    /// <summary>The player's other lever: sign or veto the motion before the council.</summary>
    public bool DecideMotion(int motionId, bool sign) => Council.Decide(Town, motionId, sign);

    /// <summary>Order a palisade or a watchtower.</summary>
    public bool BuildDefense(DefenseWork what) => Threat.Build(Town, what);

    /// <summary>Sound the call to arms on a wall section, a tower or the gate: the nearest of the watch run to man it.</summary>
    public GuardPost? CallToArms(PostKind kind, int index) => Garrison.CallToArms(Town, kind, index);

    /// <summary>Plant a flag where the watch should stand guard (tiles from the town's corner), with this many guards.</summary>
    public GuardPost? PlantFlag(double x, double y, int guards = 0) => Garrison.PlantFlag(Town, x, y, guards);

    /// <summary>Stand a call to arms or a flag down; its guards go back to walking the walls.</summary>
    public bool StandDown(int postId) => Garrison.StandDown(Town, postId);

    /// <summary>Answer a menace's demand for fealty: bend the knee and pay tribute, or refuse and face its host.</summary>
    public bool AnswerDemand(int menaceId, bool submit) => Menaces.Answer(Town, menaceId, submit);

    /// <summary>Pay a caster in town to scry a site, showing it live for a few hours.</summary>
    public Scrying? CommissionScrying(string siteId) => Knowledge.CommissionScrying(Town, siteId);

    /// <summary>Reach a party on the road by sending stone: ask for word, or call it home.</summary>
    public string? SendWord(int partyId, bool recall = false) => Knowledge.Send(Town, partyId, recall);

    /// <summary>Buy a company's report of a trip the council didn't pay for: at their asking price, or haggled down.</summary>
    public IntelDeal? BuyIntel(int partyId, bool haggle = false) => Intel.Buy(Town, partyId, haggle);

    /// <summary>Answer a company asking leave to raise a bastion: grant it (they buy the land), gift them the land, or refuse.</summary>
    public bool AnswerBastion(int requestId, BastionAnswer answer) => Bastions.Answer(Town, requestId, answer);

    /// <summary>Post the best scout in town to watch a site from cover.</summary>
    public Scout? PostScout(string siteId) => Watch.PostScout(Town, siteId);

    /// <summary>Call a scout home early.</summary>
    public bool RecallScout(int scoutId) => Watch.RecallScout(Town, scoutId);

    /// <summary>Pay for a mirror pair and have one hidden at a site, for a lasting live view.</summary>
    public Scout? PlantMirror(string siteId) => Watch.PlantMirror(Town, siteId);

    /// <summary>Garrison a cleared site so nothing moves back in, and the town sees it live.</summary>
    public bool HoldSite(string siteId) => Lairs.Hold(Town, siteId);

    /// <summary>Endow a temple to the town's god once the town is big enough.</summary>
    public Building? BuildTemple() => Shrines.BuildTemple(Town);

    /// <summary>Send the watch against a faction the town has exposed.</summary>
    public bool CrackDown(string factionId) => Factions.CrackDown(Town, factionId);

    /// <summary>Back a faction the town knows with treasury gold.</summary>
    public bool SupportFaction(string factionId, double gold) => Factions.Support(Town, factionId, gold);

    /// <summary>Claim the next charter (hamlet, village, town...) once the town has earned it.</summary>
    public bool RaiseCharter() => City.RaiseCharter(Town);

    /// <summary>Survey a ward and lay out its streets, opening its ground to zoning.</summary>
    public bool OpenWard(string wardId) => City.OpenWard(Town, wardId);

    /// <summary>
    /// Paint a brush stroke of a zone over open ground in open wards (Zone.None clears it): dabs in tiles from the town's corner.
    /// Homes and shops take a density (0 low, 1 medium, 2 high, as the charter allows), and land of the zone already built on takes it too. Returns cells changed.
    /// </summary>
    public int PaintZone(IEnumerable<(double X, double Y)> dabs, double radius, Zone zone, int density = 0) => Planning.Paint(Town, dabs, radius, zone, density);

    /// <summary>Place a government building, charter house or town-owned business at a point and heading (null faces the nearest lane), paid from the treasury.</summary>
    public Building? Place(string buildingId, double x, double y, double? yaw = null) => Planning.Place(Town, buildingId, x, y, yaw);

    /// <summary>Knight a hero of renown: Ser, a stipend and an oath to the town.</summary>
    public bool Knight(int heroId) => Loyalty.Knight(Town, Town.Agent(heroId));

    /// <summary>Grant a knight a cleared site as their land; their retainers hold it in place of a garrison.</summary>
    public bool GrantFief(int heroId, string siteId) => Loyalty.Grant(Town, Town.Agent(heroId), siteId);

    /// <summary>Name the town's champion, who stands first on the walls.</summary>
    public bool NameChampion(int heroId) => Loyalty.NameChampion(Town, Town.Agent(heroId));

    /// <summary>Send a hero a purse from the treasury, to win them back or thank them.</summary>
    public bool RewardHero(int heroId, double gold) => Loyalty.Reward(Town, Town.Agent(heroId), gold);

    /// <summary>Commission the Scrying Pool or the Sleepless Spire.</summary>
    public Building? BuildSightWork(string buildingId) => Watch.BuildWork(Town, buildingId);

    /// <summary>Set down a decoration anywhere at any angle; a statue honours the given hero or god.</summary>
    public Decoration? Decorate(string decorId, double x, double y, double angle, int? heroId = null, string? deity = null) =>
        Decor.Place(Town, decorId, x, y, angle, heroId, deity);

    /// <summary>Buy grain off the traders for the winter stores (autumn and winter).</summary>
    public bool LayInStores() => Seasons.LayIn(Town);

    /// <summary>Clear a decoration away for half its price back.</summary>
    public bool ClearDecoration(int decorationId) => Decor.Remove(Town, decorationId);

    /// <summary>Step into a story: take an option of its current stage, rolling the mayor's d20 against its DC (with inspiration, advantage).</summary>
    public StoryBeat? ResolveStory(int storyId, int option, bool inspired = false) => Stories.Resolve(Town, storyId, option, inspired);

    /// <summary>Say who the mayor was before the charter, which sets their abilities and skills.</summary>
    public bool ChooseBackground(string backgroundId) => Stories.ChooseBackground(Town, backgroundId);

    /// <summary>Spend the mayor's next level on an advance: "ability:str,cha", "feat:lucky", "skill:arcana" or "expertise:persuasion".</summary>
    public bool AdvanceMayor(string advance) => Sheet.Advance(Town, advance);

    /// <summary>Sign a sellsword company: a fee now and a wage every day it stays.</summary>
    public bool HireMercenaries(string companyId) => Mercenaries.Hire(Town, companyId);

    /// <summary>Pay off a sellsword company that is home.</summary>
    public bool DismissMercenaries(string companyId) => Mercenaries.Dismiss(Town, companyId);

    /// <summary>Build a clockwork warden for the walls (a city with a mage tower or guildhall).</summary>
    public bool BuildWarden() => Clockwork.Build(Town);

    /// <summary>Break up a clockwork warden.</summary>
    public bool ScrapWarden() => Clockwork.Scrap(Town);

    /// <summary>Fund a campaign: send a hired company against a menace's seat.</summary>
    public bool MarchMercenaries(string companyId, int menaceId) => Mercenaries.March(Town, companyId, menaceId);

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

    /// <summary>
    /// Day one, in the shape of Phandalin (GDD rule C2): the charter, a wagon of settlers, two charter
    /// houses, a shrine, a farm, a lumber camp and a general store; the inn follows within days. The settlers know the local menace.
    /// </summary>
    private void Charter()
    {
        var t = Town;
        var c = t.Content;
        t.Treasury = Options.StartingTreasury;
        var hallDef = c.Building("town_hall");
        var (hw, hd) = Land.Footprint(hallDef);
        t.AddBuilding(hallDef, new Lot(0, Town.Center, Town.Center) { W = hw, D = hd, Zone = Zone.Civic }, null, "the Town Hall", true);
        Streets.Square(t, t.Lot(0));
        Quests.InitSites(t);
        World.Generate(t);
        Council.InitCouncil(t);
        Threat.InitThreat(t);
        Factions.InitFactions(t);

        if (Options.AutoMayor && Options.AutoPlanning) Mayor.Daily(t);
        else
        {
            // The charter's own plan, a patch of each zone round the hall: the player paints everything after it.
            foreach (var (z, r, ring) in new[] { (Zone.Residential, 2.2, 0.0), (Zone.Commercial, 1.7, 0.0), (Zone.Industry, 1.8, 3.5), (Zone.Farmland, 2.2, 5.0) })
                Land.PaintPatch(t, z, r, Town.Grid, ring);
        }

        var house = c.Building("house");
        for (int i = 0; i < 2; i++) t.AddBuilding(house, FreeLot(Zone.Residential, house), null, Property.NameFor(t, house, null), true);

        Shrines.RaiseShrine(t);

        var farmer = Population.CreateAgent(t, new NewAgentOptions { Profession = "farmer", Gold = t.Rng.Range(10, 30) });
        var woodcutter = Population.CreateAgent(t, new NewAgentOptions { Profession = "woodcutter", Gold = t.Rng.Range(10, 30) });
        var shopkeeper = Population.CreateAgent(t, new NewAgentOptions { Profession = "shopkeeper", Gold = t.Rng.Range(10, 30) });
        foreach (var owner in new[] { farmer, woodcutter, shopkeeper })
        {
            owner.Ambition = Ambition.OwnHome;
            owner.DreamBusiness = null;
        }

        var farmDef = c.Building("farm");
        var campDef = c.Building("lumber_camp");
        var farm = t.AddBuilding(farmDef, FreeLot(Zone.Farmland, farmDef), farmer.Id, Property.NameFor(t, farmDef, farmer), true);
        var camp = t.AddBuilding(campDef, FreeLot(Zone.Industry, campDef), woodcutter.Id, Property.NameFor(t, campDef, woodcutter), true);
        var storeDef = c.Building("general_store");
        var store = t.AddBuilding(storeDef, FreeLot(Zone.Commercial, storeDef), shopkeeper.Id, Property.NameFor(t, storeDef, shopkeeper), true);
        farm.Cash = 40;
        camp.Cash = 40;
        store.Cash = 40;

        // The shopkeeper comes on top of the wagon's settlers. The inn is left to the first entrepreneur
        // (an inn on day one drained the settlers' purses into ale before they had homes).
        for (int i = 2; i < Options.Settlers; i++) Population.CreateAgent(t);

        t.Market.Seed(Good.Food, 150);
        t.Market.Seed(Good.Timber, 60);
        t.Market.Seed(Good.Tools, 8);
        t.Market.Seed(Good.Ale, 15);

        Economy.Daily(t, true);
        if (Options.AutoMayor) Mayor.Daily(t, Options.AutoPlanning, Options.AutoDefense);

        t.Log(
            LedgerCategory.Town,
            Importance.Major,
            $"The charter is signed. {t.Population} settlers and a wagon arrive at the river ford that will become {t.Name}. {Annals.Founding(t)}",
            t.Residents.Select(a => a.Id).ToList());
    }

    /// <summary>A founder's lot in the zone's patch, or, failing that, anywhere open (it takes the zone all the same).</summary>
    private Lot FreeLot(Zone zone, BuildingDef def)
    {
        if (Property.FindLot(Town, zone, def) is { } lot) return lot;
        lot = Land.Find(Town, def, null)!;
        lot.Zone = zone;
        return lot;
    }
}
