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

    /// <summary>Let the stand-in mayor buy loot for the town armory and gift it to heroes; off when a player keeps the armory.</summary>
    public bool AutoArmory { get; init; } = true;

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

    /// <summary>Let the stand-in mayor hand out purses, fiefs and the championship; off, they come to the player as choices.</summary>
    public bool AutoHonours { get; init; } = true;

    /// <summary>Let the stand-in mayor answer story events; off when a player steps into them.</summary>
    public bool AutoStories { get; init; } = true;

    /// <summary>The stand-in mayor answers exploring companies' reports (with AutoBounties); off, they wait for the player.</summary>
    public bool AutoExplore { get; init; } = true;

    /// <summary>Run the campaign arc (acts, festering menaces, dread); off, the Last Stand ends the game.</summary>
    public bool Saga { get; init; } = true;

    /// <summary>Who tells the campaign: "steady", "rising" (the default) or "chaotic" (content/game/storytellers.json).</summary>
    public string Storyteller { get; init; } = "rising";

    /// <summary>Foes and heroes from the player's earlier campaigns, who may come back (<see cref="Systems.Legends"/>).</summary>
    public IReadOnlyList<Legend> Legends { get; init; } = [];

    /// <summary>Roads to victory switched off for this campaign (victory.json ids); the rest are open (<see cref="Systems.Victory"/>).</summary>
    public IReadOnlyList<string> RoadsOff { get; init; } = [];

    /// <summary>Boons from the player's cabinet the town is founded with, as "id:rank" (<see cref="Systems.Boons"/>).</summary>
    public IReadOnlyList<string> Boons { get; init; } = [];

    /// <summary>Endless mode: a win closes an era and a worse foe comes; only a fall ends the campaign (<see cref="Systems.Endless"/>).</summary>
    public bool Endless { get; init; }
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
        Town.Storyteller = Systems.Storyteller.Def(Town.Content, Options.Storyteller)?.Id ?? Town.Content.Storytellers.Default;
        Town.Saga.Enabled = Options.Saga && Options.Enemy;
        Town.Saga.Endless.On = Options.Endless;
        Town.Saga.Legends.AddRange(Options.Legends.Where(l => l.Seed != Options.Seed));
        Town.Victory.Off.AddRange(Options.RoadsOff.Where(id => id != Systems.Victory.Slay && Systems.Victory.Def(Town, id) != null).Distinct().Order(StringComparer.Ordinal));
        Systems.Boons.Choose(Town, Options.Boons);
        Charter();
        Systems.Boons.Found(Town);
        Goals.Begin(Town);
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
        Lurkers.Hourly(t);
        Tournaments.Hourly(t);
        Goals.Hourly(t);
        AllyCalls.Hourly(t);
        Field.Hourly(t);
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
        Tournaments.Daily(t, Options.AutoMayor && Options.AutoStories);
        Seasons.Daily(t);
        Economy.Daily(t);
        Skills.Daily(t);
        Merchants.Daily(t);
        City.Daily(t, Options.AutoMayor && Options.AutoPlanning);
        GrandWorks.Daily(t, Options.AutoMayor && Options.AutoPlanning);
        Monuments.Daily(t);
        // Squares first, so the shops sited today front them.
        Streets.Markets(t);
        if (Options.AutoMayor && Options.AutoPlanning) Construction.Daily(t);
        Streets.Wear(t);
        Streets.Closes(t);
        Paving.Daily(t);
        Property.Daily(t);
        Population.DailyDepartures(t);
        Population.DailyArrivals(t);
        if (Options.AutoMayor) Mayor.Daily(t, Options.AutoPlanning, Options.AutoDefense);
        Social.Daily(t);
        Moods.Daily(t);
        Council.Daily(t, Options.AutoCouncil);
        double pace = Director.Daily(t);
        Threat.Daily(t, pace, Options.AutoMayor && Options.AutoDefense, Options.Enemy);
        Garrison.Daily(t, Options.AutoMayor && Options.AutoDefense);
        Army.Daily(t);
        AllyCalls.Daily(t);
        Conquest.Daily(t);
        World.Daily(t, Options.AutoMayor);
        Trade.Daily(t, Options.AutoMayor);
        Armory.Daily(t, Options.AutoMayor && Options.AutoArmory);
        Alchemy.Daily(t, Options.AutoMayor && Options.AutoArmory);
        Neighbours.Daily(t, Options.AutoMayor);
        Mercenaries.Daily(t, Options.AutoMayor && Options.AutoDefense);
        Clockwork.Daily(t, Options.AutoMayor && Options.AutoDefense);
        Quests.Daily(t, Options.AutoMayor && Options.AutoBounties, Options.FreeCompanies);
        Explore.Daily(t, Options.AutoMayor && Options.AutoBounties);
        Intel.Daily(t, Options.AutoMayor && Options.AutoBounties && Options.AutoIntel);
        Bastions.Daily(t, Options.AutoMayor && Options.AutoBastions);
        Watch.Daily(t, Options.AutoMayor && Options.AutoBounties);
        Undercity.Daily(t, Options.AutoMayor);
        Research.Daily(t, Options.AutoMayor);
        HedgeMages.Daily(t);
        Lairs.Daily(t, Options.AutoMayor, Options.AutoDefense);
        Rumors.Daily(t);
        Shrines.Daily(t, Options.AutoMayor);
        Faith.Daily(t);
        Factions.Daily(t, Options.AutoMayor);
        Saga.Daily(t, Options.AutoMayor);
        DarkQuests.Daily(t);
        Endless.Daily(t);
        Crises.Daily(t, Options.AutoMayor && Options.AutoDefense);
        Powers.Daily(t);
        Vengeance.Daily(t);
        Sightings.Daily(t);
        Discovery.Daily(t);
        Menaces.Track(t);
        Infiltration.Daily(t);
        Turning.Daily(t);
        Legacies.Daily(t, Options.AutoMayor);
        Institutions.Daily(t, Options.AutoMayor && Options.AutoPlanning);
        Regard.Daily(t);
        Wards.Daily(t);
        Broadsheet.Daily(t, Options.AutoMayor && Options.AutoStories);
        Loyalty.Daily(t, Options.AutoMayor && Options.AutoHonours);
        Secrets.Daily(t, Options.AutoMayor && Options.AutoCouncil);
        Guilds.Daily(t, Options.AutoMayor && Options.AutoCouncil);
        Union.Daily(t, Options.AutoMayor && Options.AutoCouncil);
        Patrons.Daily(t);
        Curses.Daily(t);
        Downtime.Daily(t);
        Notoriety.Daily(t);
        Heraldry.Daily(t);
        Eggs.Daily(t);
        Poaching.Daily(t);
        Bridges.Daily(t, Options.AutoMayor);
        Ruins.Daily(t, Options.AutoMayor);
        Passage.Daily(t);
        Decor.Daily(t);
        Stories.Daily(t, Options.AutoMayor && Options.AutoStories);
        Sheet.Daily(t, Options.AutoMayor && Options.AutoStories);
        Economy.Daily(t, true);
        Victory.Daily(t);
        Taxes.Close(t);
        Trends.Close(t);
        History.Add(Snapshot());
        t.AccountsYesterday = t.AccountsToday;
        t.RecentNet.Add(Js.Sum(t.AccountsToday.Values, v => v));
        if (t.RecentNet.Count > 7) t.RecentNet.RemoveAt(0);
        t.AccountsToday = new();
    }

    /// <summary>Claim a road to victory whose terms hold: the big bad marches to stop it, and the terms must hold for the road's days (<see cref="Victory"/>).</summary>
    public bool ClaimRoad(string id) => Victory.Claim(Town, id);

    /// <summary>Have the watch seize an enemy's agent the townsfolk pointed out, before it strikes tonight.</summary>
    public bool Seize(int lurkerId) => Lurkers.Seize(Town, lurkerId);

    /// <summary>The player's lever: post a bounty on a site, paid from the treasury into escrow; terms say how (slay, drive, parley, rescue, fetch); with loot to the town the hoard's magic comes to the armory.</summary>
    public Bounty? PostBounty(string siteId, double reward, string terms = "slay", bool lootToTown = false) => Quests.PostBounty(Town, siteId, reward, null, terms, lootToTown);

    /// <summary>Give a hero in town an item from the armory: magic arms, armour or a charm to wear, or a sending stone to carry.</summary>
    public bool Gift(int stockId, int heroId) => Armory.Gift(Town, stockId, heroId);

    /// <summary>Buy loot a hero or a caravan offers the town, into the armory.</summary>
    public bool BuyLoot(int offerId) => Armory.Buy(Town, offerId);

    /// <summary>Sell an item from the armory to the merchants.</summary>
    public bool SellStock(int stockId) => Armory.Sell(Town, stockId);

    /// <summary>Hand the armory's best to a group of heroes in one go (one hero, a company, the heroes on guard).</summary>
    public int Arm(IEnumerable<int> heroIds) => Armory.Arm(Town, heroIds);

    /// <summary>Post a hero to the walls or the streets, or stand them down.</summary>
    public bool SetDuty(int heroId, HeroDuty duty) => Garrison.SetDuty(Town, heroId, duty);

    /// <summary>Brew a batch at the alchemy bench from two or three ingredients (herbs, monster parts); a new recipe gets a name in the town's book.</summary>
    public bool Brew(IReadOnlyList<string> ingredients) => Alchemy.Brew(Town, ingredients);

    /// <summary>Sell every dose of a potion on the town's shelf.</summary>
    public bool SellPotions(string recipeId) => Alchemy.Sell(Town, recipeId);

    /// <summary>Offer a company a loot pact: the magic it finds comes to the armory, for a finder's fee. A wary company refuses.</summary>
    public bool OfferPact(string company) => Armory.OfferPact(Town, company);

    /// <summary>Release a company from its loot pact.</summary>
    public bool EndPact(string company) => Armory.EndPact(Town, company);

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

    /// <summary>Send a hero (the best speaker free, unless one is named) to rally a settlement under an enemy's shadow.</summary>
    public bool RallySettlement(int settlementId, int? heroId = null) => Shadows.Rally(Town, settlementId, heroId);

    /// <summary>Pay for a shared improvement (a watchtower, a bridge, a waystation) at a settlement.</summary>
    public bool FundProject(int settlementId, string projectId) => Neighbours.Fund(Town, settlementId, projectId);

    /// <summary>Open a trade route to a settlement: export goes out, import comes back.</summary>
    public bool OpenTradeRoute(int settlementId, Good export, Good import) => Trade.OpenRoute(Town, settlementId, export, import) != null;

    public bool CloseTradeRoute(int routeId) => Trade.CloseRoute(Town, routeId);

    public bool AddWagon(int routeId) => Trade.AddWagon(Town, routeId);

    /// <summary>Send the sky dock's airship on a trade run: cargo out to a settlement's market, gold and their goods home.</summary>
    public bool SendSkyship(int settlementId, Good cargo) => Skyship.Send(Town, settlementId, cargo) != null;

    /// <summary>Raise a memorial to a hero in the hall of heroes.</summary>
    public bool RaiseMemorial(int heroId) => Legacies.RaiseMemorial(Town, heroId);

    /// <summary>Send the watch down into a level of the undercity each night, or call them back.</summary>
    public bool PatrolUndercity(string levelId, bool on) => Undercity.SetPatrol(Town, levelId, on);

    /// <summary>Set the town's scholars on a discovery (the first time pays its commission).</summary>
    public bool StudyDiscovery(string id) => Research.Study(Town, id);

    /// <summary>How much the council pays its scholars: 0 none up to 4, a royal academy.</summary>
    public bool FundResearch(int level) => Research.Fund(Town, level);

    /// <summary>Set how generously the council funds an institution: 0 unfunded, 1 modest, 2 generous.</summary>
    public bool FundInstitution(string institutionId, int level) => Institutions.Fund(Town, institutionId, level);

    /// <summary>Make an institution's choice: which god's temple gets the land, the tavern's style, what the bazaar trades in.</summary>
    public bool ChooseInstitution(string institutionId, string option) => Institutions.Choose(Town, institutionId, option);

    /// <summary>The player's lever: set the tax rate (a share, e.g. 0.12); higher fills the treasury and sours the town.</summary>
    public bool SetTax(double rate) => Taxes.Set(Town, rate);

    /// <summary>The player's other lever: sign or veto the motion before the council.</summary>
    public bool DecideMotion(int motionId, bool sign) => Council.Decide(Town, motionId, sign);

    /// <summary>Take one councillor aside before the vote and try to turn their vote, by an approach: "persuade", "intimidate" or "deceive".</summary>
    public StoryBeat? SwayCouncillor(int motionId, string seat, string approach) => Council.Sway(Town, motionId, seat, approach);

    /// <summary>Call a festival on the mayor's own word: mood for a few days and a little Mandate, for gold.</summary>
    public bool HoldFestival() => Council.HoldFestival(Town);

    /// <summary>Order a palisade or a watchtower.</summary>
    public bool BuildDefense(DefenseWork what) => Threat.Build(Town, what);

    /// <summary>Lays out where a wall runs (ring 0 round the old town, 1 the city wall) as a closed loop in tiles; null, or why not.</summary>
    public string? DrawWall(int ring, IEnumerable<(double X, double Y)> points) => WallLines.Draw(Town, ring, points.ToList());

    /// <summary>A gate on a wall's line, snapped to the nearest point of it; null, or why not.</summary>
    public string? PlaceGate(int ring, double x, double y) => WallLines.PlaceGate(Town, ring, x, y);

    public string? RemoveGate(int ring, int index) => WallLines.RemoveGate(Town, ring, index);

    /// <summary>Sound the call to arms on a wall section, a tower or the gate: the nearest of the watch run to man it.</summary>
    public GuardPost? CallToArms(PostKind kind, int index) => Garrison.CallToArms(Town, kind, index);

    /// <summary>Man the walls at once: a call to arms on the gate and on every side a host is marching from.</summary>
    public int ManTheWalls() => Garrison.ManTheWalls(Town);

    /// <summary>Plant a flag where the watch should stand guard (tiles from the town's corner), with this many guards.</summary>
    public GuardPost? PlantFlag(double x, double y, int guards = 0) => Garrison.PlantFlag(Town, x, y, guards);

    /// <summary>Stand a call to arms or a flag down; its guards go back to walking the walls.</summary>
    public bool StandDown(int postId) => Garrison.StandDown(Town, postId);

    /// <summary>Answer a clash between two enemies in crisis: ride out against one of them (its menace id), or 0 to keep behind the walls.</summary>
    public bool JoinClash(int clashId, int againstMenaceId) => Crises.Join(Town, clashId, againstMenaceId);

    /// <summary>Name a councillor the taverns whisper about as a foe's servant.</summary>
    public bool AccuseCouncillor(int agentId) => Infiltration.Accuse(Town, agentId);

    /// <summary>Pay to have a councillor's past looked into: a d20 check, helped by the watch.</summary>
    public bool DigIntoCouncillor(int agentId) => Secrets.Dig(Town, agentId);

    /// <summary>Spend a councillor's secret to have them vote aye or nay on the motion before the council.</summary>
    public bool PressCouncillor(int agentId, bool yes) => Secrets.Press(Town, agentId, yes);

    /// <summary>Make a councillor's secret public, costing them their seat.</summary>
    public bool ExposeCouncillor(int agentId) => Secrets.Expose(Town, agentId);

    /// <summary>Give a trade guild what its master asked for: a festival, lower taxes, more watchmen, a monument's next part.</summary>
    public bool MeetGuildDemand(string guildId) => Guilds.Meet(Town, guildId);

    /// <summary>Pay a trade guild to drop its demand or call off its strike.</summary>
    public bool PayOffGuild(string guildId) => Guilds.PayOff(Town, guildId);

    /// <summary>Talk a guild's demand or strike away, by an approach: "persuade", "intimidate" or "deceive" (negotiation.json).</summary>
    public StoryBeat? NegotiateGuild(string guildId, string approach) => Guilds.Negotiate(Town, guildId, approach);

    /// <summary>Send the watch to break a guild's strike, at a cost in Mandate.</summary>
    public bool BreakStrike(string guildId) => Guilds.Break(Town, guildId);

    /// <summary>Hear one side out with gold: "union" (the Common Cause, the workers' movement) or "mages".</summary>
    public bool HearUnion(string side) => Union.Hear(Town, side);

    /// <summary>Take a side outright between the Common Cause and the mages ("union" or "mages") for a season, and face the other's anger.</summary>
    public bool TakeSide(string side) => Union.Side(Town, side);

    /// <summary>Answer a menace's demand for fealty: bend the knee and pay tribute, or refuse and face its host.</summary>
    public bool AnswerDemand(int menaceId, bool submit) => Menaces.Answer(Town, menaceId, submit);

    /// <summary>Scry a site live for a few hours: the Scrying Pool if it has one left, else a rested caster (the named one) on their terms: free, gold or a favour.</summary>
    public Scrying? CommissionScrying(string siteId, int? casterId = null) => Knowledge.CommissionScrying(Town, siteId, casterId);

    /// <summary>Mark a spot in the wilds (days of march east and south of the town) and pay a company to go and look at it.</summary>
    public Commission? MarkSpot(double x, double y, double? reward = null) => Explore.PostSpot(Town, x, y, reward);

    /// <summary>Reach a party on the road by sending stone: ask for word, or call it home.</summary>
    public string? SendWord(int partyId, bool recall = false) => Knowledge.Send(Town, partyId, recall);

    /// <summary>Buy a company's report of a trip the council didn't pay for: at their asking price, or haggled down.</summary>
    /// <summary>Offer one company a job by name; it takes it and marches at once, or turns it down. Null when it can't be offered.</summary>
    public JobAnswer? OfferJob(string company, string siteId, double reward, string terms = "slay", bool lootToTown = false) =>
        Roster.Offer(Town, company, siteId, reward, terms, lootToTown);

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

    /// <summary>Write a punishment for a crime into the law book (laws.json).</summary>
    public bool SetLaw(string crime, string punishment) => Laws.Set(Town, crime, punishment);

    /// <summary>Hold every trial under a truth spell from laws.json "trial" (Zone of Truth), or "" for none.</summary>
    public bool SetTrialMagic(string spell) => Laws.SetTrialMagic(Town, spell);

    /// <summary>Send the watch against a faction the town has exposed.</summary>
    public bool CrackDown(string factionId) => Factions.CrackDown(Town, factionId);

    /// <summary>Back a faction the town knows with treasury gold.</summary>
    public bool SupportFaction(string factionId, double gold) => Factions.Support(Town, factionId, gold);

    /// <summary>Claim the next charter (hamlet, village, town...) once the town has earned it.</summary>
    public bool RaiseCharter() => City.RaiseCharter(Town);

    /// <summary>
    /// Paint a brush stroke of a zone over open ground in open wards (Zone.None clears it): dabs in tiles from the town's corner.
    /// Homes and shops take a density (0 low, 1 medium, 2 high, as the charter allows), and land of the zone already built on takes it too. Returns cells changed.
    /// </summary>
    /// <param name="crop">Farmland: the crop its fields are sown with (content/game/farming.json); null sows wheat.</param>
    public int PaintZone(IEnumerable<(double X, double Y)> dabs, double radius, Zone zone, int density = 0, string? crop = null) => Planning.Paint(Town, dabs, radius, zone, density, crop);

    /// <summary>Place a government building, charter house or town-owned business at a point and heading (null faces the nearest lane), paid from the treasury.</summary>
    public Building? Place(string buildingId, double x, double y, double? yaw = null) => Planning.Place(Town, buildingId, x, y, yaw);

    /// <summary>Add a part to a monument (temple, keep, town hall): a nave, a bell tower, a curtain wall.</summary>
    public Building? AddPart(int monumentId, string partId) => Monuments.Add(Town, Town.Building(monumentId), partId);

    /// <summary>Add every part a monument can take now, as far as the gold goes. Returns how many were begun.</summary>
    public int AddAllParts(int monumentId) => Monuments.AddAll(Town, Town.Building(monumentId));

    /// <summary>Whether the council grows a monument a part at a time on its own, from gold it can spare.</summary>
    public bool GrowMonument(int monumentId, bool grow)
    {
        var b = Town.Building(monumentId);
        if (!Monuments.IsMonument(Town, b.Def)) return false;
        b.Grow = grow;
        return true;
    }

    /// <summary>Put a building site ahead of the others for builders and materials, or back in line.</summary>
    public bool PrioritiseSite(int buildingId, bool first) => Construction.Prioritise(Town, Town.Building(buildingId), first);

    /// <summary>Send a caravan for the timber and stone a stalled site is waiting on, at the import premium.</summary>
    public bool SendCaravan(int buildingId) => Construction.SendCaravan(Town, Town.Building(buildingId));

    /// <summary>Rebuild a rope bridge raiders cut, in stone that can't be cut again.</summary>
    public bool RebuildBridge(int featureId) => Bridges.Rebuild(Town, featureId);

    /// <summary>Restore a cleared ruin (an aqueduct, a gatehouse, a watchtower, a star fort) to serve the town.</summary>
    public bool RestoreRuin(string siteId) => Ruins.Restore(Town, siteId);

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

    /// <summary>Pay for the year's tourney at a level: 0 modest, 1 grand, 2 legendary.</summary>
    public bool FundTourney(int level) => Tournaments.Fund(Town, level);

    /// <summary>Let the year's tourney go (the town is let down).</summary>
    public bool SkipTourney() => Tournaments.Skip(Town);

    /// <summary>Stake treasury gold on one rider in one of the tourney's events, before its first heat.</summary>
    public bool PlaceBet(int eventIndex, int entrantId, double stake) => Tournaments.Bet(Town, eventIndex, entrantId, stake);

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

    /// <summary>Send a minor power gifts: favour that lifts its standing, and outbids an enemy courting it.</summary>
    public bool CourtPower(string powerId) => Powers.Court(Town, powerId);

    /// <summary>Pay a minor power's price to make it the town's ally, for a daily tithe.</summary>
    public bool AllyWithPower(string powerId) => Powers.Ally(Town, powerId);

    /// <summary>End an alliance with a minor power.</summary>
    public bool BreakWithPower(string powerId) => Powers.Break(Town, powerId);

    /// <summary>Answer a minor power's envoy: accept its offer or demand and pay its price, or send it home.</summary>
    public bool AnswerEnvoy(string powerId, bool accept) => Powers.Answer(Town, powerId, accept);

    /// <summary>Pay off a sellsword company that is home.</summary>
    public bool DismissMercenaries(string companyId) => Mercenaries.Dismiss(Town, companyId);

    /// <summary>Build a clockwork warden for the walls (a city with a mage tower or guildhall).</summary>
    public bool BuildWarden() => Clockwork.Build(Town);

    /// <summary>Break up a clockwork warden.</summary>
    public bool ScrapWarden() => Clockwork.Scrap(Town);

    /// <summary>Fund a campaign: send a hired company against a menace's seat.</summary>
    public bool MarchMercenaries(string companyId, int menaceId) => Mercenaries.March(Town, companyId, menaceId);

    /// <summary>Raise a regiment of the town's own soldiers at a training hall: recruits come in each day, armed from the treasury and the market.</summary>
    public Regiment? RaiseRegiment(string unitId, int size, ArmyPost post = ArmyPost.Reserve) => Army.Raise(Town, unitId, size, post);

    /// <summary>Take a regiment up or down to a size; those cut go home to their trades.</summary>
    public bool ResizeRegiment(int regimentId, int size) => Army.Resize(Town, regimentId, size);

    /// <summary>Stand a regiment (or the levy) down: its soldiers go home to their trades.</summary>
    public bool DisbandRegiment(int regimentId) => Army.Disband(Town, regimentId);

    /// <summary>Post a regiment: the walls, its hall in reserve, the town guard's patrol, or the mayor's guard.</summary>
    public bool PostRegiment(int regimentId, ArmyPost post) => Army.SetPost(Town, regimentId, post);

    /// <summary>Put a hero at the head of a regiment, or take them off it (null).</summary>
    public bool CommandRegiment(int regimentId, int? heroId) => Army.SetCommander(Town, regimentId, heroId);

    /// <summary>Call townsfolk up from their work into the levy, under the town's laws.</summary>
    public Regiment? RaiseLevy(int size, ArmyPost post = ArmyPost.Walls) => Army.RaiseLevy(Town, size, post);

    /// <summary>The levy law: volunteers only, or conscription.</summary>
    public bool SetServiceLaw(ServiceLaw law) => Army.SetService(Town, law);

    /// <summary>Whether the levy is paid a wage while it serves.</summary>
    public bool SetLevyPay(bool paid) => Army.SetLevyPay(Town, paid);

    /// <summary>Send riders to the allies named, calling their hosts to the town.</summary>
    public List<AllyCall> CallAllies(IEnumerable<int> settlementIds) => AllyCalls.Call(Town, settlementIds);

    /// <summary>Pay a holdout ally's price for its host.</summary>
    public bool BribeAlly(int callId) => AllyCalls.Bribe(Town, callId);

    /// <summary>Talk a holdout ally into marching, by an approach: "persuade", "intimidate" or "deceive" (negotiation.json).</summary>
    public StoryBeat? NegotiateAlly(int callId, string approach) => AllyCalls.Negotiate(Town, callId, approach);

    /// <summary>Let a holdout ally keep its host.</summary>
    public bool LetAllyGo(int callId) => AllyCalls.LetGo(Town, callId);

    /// <summary>Pick a regiment to march out with the host, or to stay on the walls.</summary>
    public bool SetMarches(int regimentId, bool marches) => Field.SetMarches(Town, regimentId, marches);

    /// <summary>Pick an allied host camped at the gate to march out with the town's host.</summary>
    public bool SetAllyMarches(int callId, bool marches) => Field.SetAllyMarches(Town, callId, marches);

    /// <summary>Give a hero a goal on the field ("leader", "banner", "flank", "shield"), or "" to keep them home.</summary>
    public bool SetFieldGoal(int heroId, string goal) => Field.SetGoal(Town, heroId, goal);

    /// <summary>March the host out to meet a foe on the road ("warband" or "menace:id") under an order.</summary>
    public Sortie? MarchOut(string foe, string order) => Field.MarchOut(Town, foe, order);

    /// <summary>Change the host's order before it meets the foe.</summary>
    public bool SetFieldOrder(string order) => Field.SetOrder(Town, order);

    /// <summary>Call the host home before it meets the foe.</summary>
    public bool RecallHost() => Field.Recall(Town);

    /// <summary>March the host on a settlement the town has met.</summary>
    public Sortie? Attack(int settlementId, string order) => Field.MarchOut(Town, $"{Conquest.Prefix}{settlementId}", order);

    /// <summary>The mayor's word on a won settlement: occupy, pillage, cripple or spare it.</summary>
    public bool SettleConquest(int settlementId, ConquestChoice choice) => Conquest.Settle(Town, settlementId, choice);

    /// <summary>Leave a regiment to hold an occupied settlement, or bring it home with null.</summary>
    public bool GarrisonSettlement(int settlementId, int? regimentId) => Conquest.SetGarrison(Town, settlementId, regimentId);

    /// <summary>Give an occupied settlement its freedom back.</summary>
    public bool FreeSettlement(int settlementId) => Conquest.Free(Town, settlementId);

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
        Powers.Init(t);
        Armory.Init(t);

        if (Options.AutoMayor && Options.AutoPlanning) Mayor.Daily(t);
        else
        {
            // The charter's own plan, a patch of each zone round the hall: the player paints everything after it.
            foreach (var (z, r, ring) in new[] { (Zone.Residential, 2.2, 0.0), (Zone.Commercial, 1.7, 0.0), (Zone.Industry, 1.8, 3.5), (Zone.Farmland, 2.2, 5.0) })
                Land.PaintPatch(t, z, r, Town.Grid, ring, z == Zone.Farmland ? Farming.Code(t, "wheat") : (byte)0);
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
        // The charter pays a pair of watchmen and swears a knight to the town, so it can stand guard before it can build a watch house.
        t.Watch = c.Council.StartingWatch;
        Agent? knight = null;
        if (c.Rules.CharterKnight != "")
        {
            knight = Quests.CreateHero(t, c.Classes.First(k => k.Id == c.Rules.CharterKnight), c.Rules.CharterKnightLevel + Systems.Boons.KnightLevels(t));
            knight.Hero!.Sworn = true;
            knight.Hero.Lodging = true;
        }

        t.Market.Seed(Good.Food, 150);
        t.Market.Seed(Good.Timber, 60);
        t.Market.Seed(Good.Tools, 8);
        t.Market.Seed(Good.Ale, 15);

        Economy.Daily(t, true);
        if (Options.AutoMayor) Mayor.Daily(t, Options.AutoPlanning, Options.AutoDefense);

        t.Log(
            LedgerCategory.Town,
            Importance.Major,
            $"The charter is signed. {t.Population} settlers and a wagon arrive at the river ford that will become {t.Name}. {Annals.Founding(t)}{(knight != null ? $" {knight.Name}, a knight sworn to the charter, rides at the wagon's side, and two watchmen walk behind it." : "")}",
            t.Residents.Select(a => a.Id).Concat(knight != null ? [knight.Id] : []).Distinct().ToList());
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
