namespace CrownAndCompass.Sim.Systems;

/// <summary>What the town knows of a site: nothing, tavern talk, or a first-hand report or sight.</summary>
public enum SiteKnowledge
{
    Unheard,
    Rumoured,
    Confirmed,
}

/// <summary>
/// A site as it really is (Def, Cleared) and as the town believes it to be (Known, Believed,
/// BelievedTier, ThoughtCleared). Only a party's report or live sight brings belief back to the truth.
/// </summary>
public sealed class SiteState(SiteDef def)
{
    public SiteDef Def { get; set; } = def;
    public bool Cleared { get; set; }
    public int Attempts { get; set; }

    /// <summary>The region of the wilds it lies in (<see cref="World"/>), or null for sites raised later.</summary>
    public int? RegionId { get; set; }

    /// <summary>Built by world generation rather than written by hand in sites.json.</summary>
    public bool Generated { get; init; }

    public SiteKnowledge Known { get; set; }

    /// <summary>Someone has seen it with their own eyes (a party, a scout, explorers, an ally's map): the town knows where it really is, not just roughly.</summary>
    public bool Found { get; set; }

    /// <summary>Who the town last heard of it from ("a tinker passing through", "the dwarves of Ironhold").</summary>
    public string HeardFrom { get; set; } = "";

    /// <summary>Who the town thinks lives there ("goblins", "an ogre"), from the last rumour or report.</summary>
    public string Believed { get; set; } = "";
    public int BelievedTier { get; set; }

    /// <summary>The town thinks the site is cleared (it may have been resettled since).</summary>
    public bool ThoughtCleared { get; set; }

    /// <summary>How far the town trusts what it believes, 0 to 100: the teller's reliability, 100 once confirmed.</summary>
    public int Trust { get; set; }

    /// <summary>When it was last cleared.</summary>
    public int? ClearedTick { get; set; }

    /// <summary>When the first-hand account the town believes was taken; an older report bought later does not overrule it.</summary>
    public int? ReportTick { get; set; }

    // The lair, off-screen (see Lairs) ------------------------------------------------------------

    /// <summary>How many live there and how bold they are; swells the fights and drives what they do.</summary>
    public double Strength { get; set; }

    /// <summary>When the lair next acts on the world; null until it has woken.</summary>
    public int? NextActTick { get; set; }

    /// <summary>Raids put off while the heroes were home.</summary>
    public int Waited { get; set; }

    /// <summary>Paid tribute buys quiet until this tick.</summary>
    public int? QuietUntil { get; set; }

    /// <summary>When something new moves into the cleared site, unless the town holds it.</summary>
    public int? ResettleTick { get; set; }

    /// <summary>A tower stands here (the town's, or a knight's): nothing resettles it, and the town sees it live.</summary>
    public bool Held { get; set; }

    /// <summary>A tower is rising here, done at this tick (<see cref="Lairs.Hold"/>); nothing moves in while builders are on site.</summary>
    public int? TowerDue { get; set; }

    /// <summary>Something comes to test the tower at this tick (<see cref="Lairs.Tested"/>), and who it is.</summary>
    public int? ThreatTick { get; set; }
    public string ThreatBy { get; set; } = "";

    /// <summary>The tower was reinforced against the coming test.</summary>
    public bool Reinforced { get; set; }

    /// <summary>The town pays a standing patrol to keep it clear: newcomers are met and, mostly, driven off (<see cref="Lairs.Patrol"/>).</summary>
    public bool Patrolled { get; set; }

    /// <summary>A ruin the town restored once it was cleared (<see cref="Ruins"/>): it serves the town and is never resettled.</summary>
    public bool Restored { get; set; }

    /// <summary>How it picks its night (lairs.json habits); empty until it first acts.</summary>
    public string Habit { get; set; } = "";

    /// <summary>Times running the town has turned its bands back (each comes bigger), and its threats have come to nothing (they escalate).</summary>
    public int Rebuffs { get; set; }
    public int Stirs { get; set; }

    /// <summary>A band on its way: the building it means to hit and how many are in it.</summary>
    public int? BandTarget { get; set; }
    public double BandSize { get; set; }
    public int? LastRaidDay { get; set; }

    /// <summary>The town refused its tribute: a tribute-taker raids next.</summary>
    public bool Refused { get; set; }
}

public enum BountyStatus
{
    Open,
    Taken,
    Completed,
    Failed,
    Expired,
}

public sealed class Bounty
{
    public int Id { get; init; }
    public required string SiteId { get; init; }
    public double Reward { get; init; }
    public int PostedTick { get; init; }
    public int ExpiresTick { get; init; }
    public BountyStatus Status { get; set; } = BountyStatus.Open;
    public int? PartyId { get; set; }

    /// <summary>The building whose coffers hold the escrow (the temple), or null for the treasury.</summary>
    public int? FunderId { get; init; }

    /// <summary>A townsperson whose own purse holds the escrow (a notable's quest), and the notable it is for (<see cref="Notables"/>).</summary>
    public int? PatronId { get; init; }
    public int? NotableId { get; set; }

    /// <summary>How the job is to be done: slay, drive, parley, rescue or fetch (<see cref="Systems.Terms"/>).</summary>
    public string Terms { get; init; } = "slay";

    /// <summary>A wizard's errand: the spell the funder casts for the town when the job is done (<see cref="Systems.Errands"/>).</summary>
    public string Errand { get; set; } = "";

    /// <summary>Loot terms: the company hands the hoard's magic to the town armory (<see cref="Armory"/>).</summary>
    public bool LootToTown { get; init; }

    /// <summary>The classic tale the job is told as ("The Vault Job"), "" for none (<see cref="PlotDef"/>).</summary>
    public string Plot { get; init; } = "";
}

public sealed class Party
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<int> Members { get; init; }
    /// <summary>The bounty it took, or null for a company out on its own account.</summary>
    public int? BountyId { get; init; }

    /// <summary>The settlement's plea it is answering, if any.</summary>
    public int? PleaId { get; set; }
    public required string SiteId { get; init; }
    public int DepartTick { get; init; }
    public int ReturnTick { get; set; }
    public bool Returned { get; set; }

    /// <summary>Whether it went in the sky dock's airship, which stays with it until it is home.</summary>
    public bool Airship { get; set; }

    /// <summary>The harbour's ship that carries it over the open sea, or the town's airship that carries it into the sky, out and home again (<see cref="Voyages"/>, <see cref="Reaches"/>).</summary>
    public int? ShipId { get; set; }

    /// <summary>Its airship was lost on the way (<see cref="Reaches"/>), and when it was left stranded at the site.</summary>
    public bool ShipLost { get; set; }
    public int? StrandedTick { get; set; }

    /// <summary>Where it set out from, in day units: the town, or the spot where explorers chose to go in (<see cref="Explore"/>).</summary>
    public (double X, double Y) Start { get; init; }

    /// <summary>The exploring company it grew out of, which the council paid for.</summary>
    public int? SurveyId { get; init; }

    /// <summary>When the town last reckoned them home, and when it last had word of them: the day they left, or a sending since.</summary>
    public int Expected { get; set; }
    public int HeardTick { get; set; }

    /// <summary>Sending-stone messages exchanged on the road; each costs one of the party's spell slots.</summary>
    public int Sendings { get; set; }

    /// <summary>Set when a sending called the party home before it reached the site.</summary>
    public int? RecalledTick { get; set; }

    /// <summary>Sending-stone quests the party has asked the mayor about on this venture (<see cref="Sendings"/>).</summary>
    public int Asked { get; set; }

    /// <summary>What the mayor's answers did to the delve to come: a bonus on the party's checks and blows, foes added to each fight.</summary>
    public int Edge { get; set; }
    public int Swell { get; set; }

    /// <summary>Gold the answers turned up on the way, split with the take at the homecoming.</summary>
    public double Bonus { get; set; }

    // The road (see Roads) ----------------------------------------------------------------------

    /// <summary>The road out to the site, and the one home (chosen when they leave the site).</summary>
    public List<Waypoint> Out { get; set; } = [];
    public List<Waypoint> Home { get; set; } = [];

    /// <summary>What they said at the gate about the way to go; it opens the chronicle.</summary>
    public List<string> Said { get; } = [];

    /// <summary>When they reach the site, and when they set off home from it.</summary>
    public int ArriveTick { get; set; }
    public int LeaveTick { get; set; }

    /// <summary>Stops near the site: a camp before going in, the delve, falling back to rest.</summary>
    public List<Halt> Halts { get; } = [];

    /// <summary>The venture has been settled (on reaching the site); <see cref="Result"/> waits for the homecoming.</summary>
    public bool Resolved { get; set; }
    public ExpeditionResult? Result { get; set; }

    /// <summary>They found the site empty and turned back.</summary>
    public bool Empty { get; set; }

    /// <summary>When the last of them died, for a party that never comes home.</summary>
    public int? GoneTick { get; set; }

    /// <summary>
    /// Home, but keeping its report to itself: no council bounty paid for it, so what it saw stays off
    /// the town's map until the town buys it (<see cref="Intel"/>).
    /// </summary>
    public bool Withheld { get; set; }

    /// <summary>The site stood empty when they left it (their report's word on it).</summary>
    public bool SawCleared { get; set; }

    /// <summary>Set when the company sold its report to the town, and for how much.</summary>
    public int? SoldTick { get; set; }
    public double SoldFor { get; set; }
}

public sealed record Chronicle(
    int Id,
    string PartyName,
    string SiteName,
    IReadOnlyList<string> Members,
    int DepartDay,
    int ReturnDay,
    ExpeditionOutcome Outcome,
    IReadOnlyList<string> Lines,
    bool GoalMet = false)
{
    /// <summary>The moments worth telling at the tavern (<see cref="TaleBeat"/>), the heroes by id, and who did not come home.</summary>
    public IReadOnlyList<TaleBeat> Beats { get; init; } = [];
    public IReadOnlyList<int> MemberIds { get; init; } = [];
    public IReadOnlyList<int> Dead { get; init; } = [];

    /// <summary>The bounty paid them, and what they split.</summary>
    public int Gold { get; init; }

    /// <summary>How it ended in a word: a party that pulled out with what the bounty asked for (captives, the fallen, the hoard) got the job done.</summary>
    public string Verdict => GoalMet && Outcome == ExpeditionOutcome.Retreated ? "job done" : Outcome.ToString().ToLowerInvariant();

    public string Format()
    {
        string head = $"Chronicle {Id}: the {PartyName} at {SiteName} (days {DepartDay} to {ReturnDay}, {Verdict})";
        return string.Join("\n", new[] { head }.Concat(Members.Select(m => $"  {m}")).Concat(Lines.Select(l => $"  - {l}")));
    }
}

/// <summary>
/// Heroes, the bounty board and expeditions. The mayor never sends a hero anywhere: the town
/// posts bounties with gold held in escrow, and heroes decide for themselves whether the odds and
/// the pay are worth it, forming parties with people they like.
/// </summary>
public static class Quests
{
    private const int BountyDays = 10;
    private const double Lodging = 1;
    private static readonly int[] XpLevels =
        [0, 300, 900, 2700, 6500, 14000, 23000, 34000, 48000, 64000, 85000, 100000, 120000, 140000, 165000, 195000, 225000, 265000, 305000, 355000];

    /// <summary>XP from the start of a level to the next.</summary>
    internal static int XpToNext(int level) => level >= XpLevels.Length ? 0 : XpLevels[level] - XpLevels[level - 1];

    /// <summary>The XP a level starts at and the next one's, for a sheet's bar; null at the top level.</summary>
    public static (int From, int To)? XpBand(int level) => level < 1 || level >= XpLevels.Length ? null : (XpLevels[level - 1], XpLevels[level]);

    public static void InitSites(Town town)
    {
        foreach (var def in town.Content.Sites)
        {
            var s = new SiteState(def);
            town.Sites.Add(s);
            // The settlers know the local menace; everything else waits for tavern talk.
            if (def.Known) Rumors.Hear(town, s, Rumors.Occupants(town.Content, def), def.Tier);
        }
    }

    public static SiteState? Site(Town town, string id) => town.Sites.FirstOrDefault(s => s.Def.Id == id);

    internal static bool IsActive(Bounty b) => b.Status is BountyStatus.Open or BountyStatus.Taken;

    /// <summary>
    /// Post a bounty, holding the reward in escrow from the treasury, or from a funder's coffers (the
    /// temple). Returns null if the payer can't cover it or the town knows of no such danger.
    /// </summary>
    public static Bounty? PostBounty(Town town, string siteId, double reward, Building? funder = null, string terms = "slay", bool lootToTown = false, Agent? patron = null)
    {
        var s = Site(town, siteId);
        double purse = patron?.Gold ?? funder?.Cash ?? town.Treasury;
        if (s == null || s.Known == SiteKnowledge.Unheard || s.ThoughtCleared || reward <= 0 || purse < reward) return null;
        if (town.Bounties.Any(b => b.SiteId == siteId && IsActive(b))) return null;
        if (funder == null && patron == null && Stewards.Intercept(town, "bounties", new StewardAsk
            {
                Area = "bounties",
                Kind = "bounty",
                Title = $"Bounty · {s.Def.Name} · {Js.Str(reward)}g",
                Why = $"• {Stewards.Cap(s.Believed == "" ? "trouble, by report" : s.Believed)}\n• Tier {s.BelievedTier} · {s.Attempts} tries so far\n• Pay asked for its danger",
                S1 = siteId,
                S2 = terms,
                I1 = lootToTown ? 1 : 0,
                D1 = reward,
            })) return null;
        var term = town.Content.Bounties.Term(terms);
        if (term.Id != terms || Systems.Terms.Blocked(town, s, term) != null) return null;
        if (patron != null) patron.Gold -= reward;
        else if (funder != null) funder.Cash -= reward;
        else town.Book(TreasuryAccount.Bounties, -reward);
        var b = new Bounty
        {
            Id = town.Bounties.Count + 1,
            SiteId = siteId,
            Reward = reward,
            PostedTick = town.Tick,
            ExpiresTick = town.Tick + BountyDays * SimTime.HoursPerDay,
            FunderId = funder?.Id,
            PatronId = patron?.Id,
            Terms = term.Id,
            LootToTown = lootToTown,
            Plot = Systems.Terms.PlotFor(town, s, term)?.Id ?? "",
        };
        town.Bounties.Add(b);
        string loot = lootToTown ? ", the hoard's magic to come to the town armory" : "";
        string plot = town.Content.Bounties.Plots.FirstOrDefault(p => p.Id == b.Plot) is { } pd ? $" The taverns call it {pd.Name}: {pd.Pitch}" : "";
        if (funder == null && patron == null)
            town.Log(LedgerCategory.Town, Importance.Notable, (term.Id == "slay"
                ? $"The council posts a bounty of {Js.Str(reward)} gold on {s.Def.Name}{loot}."
                : $"The council posts a bounty of {Js.Str(reward)} gold to {term.Verb} {s.Def.Name}{loot}.") + plot);
        // The heroes judge the terms as they judge any choice of the mayor's.
        if (funder == null && patron == null && term.Tags.Count > 0) Regard.Judge(town, term.Tags, $"posted a bounty to {term.Verb} {s.Def.Name}", "Bounties");
        return b;
    }

    /// <summary>Hand an unearned reward back to whoever put it up.</summary>
    public static void Refund(Town town, Bounty b)
    {
        if (b.PatronId != null) town.Agent(b.PatronId.Value).Gold += b.Reward;
        else if (b.FunderId != null) town.Building(b.FunderId.Value).Cash += b.Reward;
        else town.Book(TreasuryAccount.Bounties, b.Reward);
    }

    public static int SuggestedReward(int tier) => 50 * tier + 30 * tier * tier;

    /// <summary>What heroes ask of this town for a site of this tier: steeper for great danger, dearer when the treasury is full.</summary>
    public static double Asking(Town town, int tier)
    {
        var p = town.Content.Bounties.Pay;
        return Js.Round((p.PerTier * tier + p.PerTierSq * tier * tier + p.PerTierCube * tier * tier * tier) * Wealth(town));
    }

    /// <summary>How much more heroes expect from a rich town, from 1 up to 1 + WealthCap.</summary>
    public static double Wealth(Town town)
    {
        var p = town.Content.Bounties.Pay;
        return 1 + Math.Min(p.WealthCap, Math.Max(0, town.Treasury - p.WealthFree) / p.WealthPer);
    }

    /// <param name="people">The people (content/game/world.json) who sent them, if any.</param>
    public static Agent CreateHero(Town town, ClassDef cls, int level, string? race = null, string? people = null)
    {
        var a = Population.CreateAgent(town, new NewAgentOptions { Profession = cls.Name, Gold = town.Rng.Range(10, 40), Race = race });
        a.Ambition = Ambition.None;
        a.DreamBusiness = null;
        // Heroes favor their class's key ability: reroll it once and keep the better.
        int reroll = Abilities.Roll(town.Rng)[cls.Primary];
        a.Abilities[cls.Primary] = Math.Max(a.Abilities[cls.Primary], reroll);
        var sheet = new HeroSheet
        {
            Cls = cls,
            Level = level,
            Xp = XpLevels[level - 1],
            MaxHp = 1,
            Hp = 1,
        };
        // Masters of arms who retired here train the newcomers.
        sheet.Xp += Legacies.MentorXp(town, level);
        sheet.Xp += (int)Math.Floor(Research.Xp(town) * XpToNext(level));
        sheet.MaxHp = Expedition.HeroMaxHp(sheet, a.Abilities.Con);
        sheet.Hp = sheet.MaxHp;
        Gear.Equip(town.Content.Library, sheet);
        Annals.Raise(town, a, sheet, people);
        a.Hero = sheet;
        Loyalty.Arrive(town, a);
        Regard.Arrive(town, a);
        return a;
    }

    internal static ClassDef PickClass(Town town)
    {
        var classes = town.Content.Classes;
        int W(ClassDef c) => c.Weight + Institutions.Calling(town, c.Id);
        int total = classes.Sum(W);
        int roll = town.Rng.Range(0, total);
        foreach (var c in classes)
        {
            if (roll < W(c)) return c;
            roll -= W(c);
        }
        return classes[0];
    }

    public static void Daily(Town town, bool autoMayor, bool companies = true)
    {
        ReturnParties(town);
        Voices.Forsworn(town);
        HeroArrivals(town);
        LodgingAndRest(town);
        Gear.Shop(town);
        ExpireBounties(town);
        Captives.Daily(town);
        if (autoMayor) Captives.AutoPost(town);
        if (autoMayor) AutoPost(town);
        Muster.Daily(town);
        FormParties(town);
        if (companies) Campaigns.Daily(town);
    }

    private static void HeroArrivals(Town town)
    {
        var taverns = town.Taverns;
        if (taverns.Count == 0) return;
        int heroes = town.Heroes.Count;
        if (heroes >= City.HeroCap(town)) return;
        int open = town.Bounties.Count(b => b.Status == BountyStatus.Open);
        // A famous company in town draws others who want to ride with it, or against it.
        int fame = Notoriety.TownFame(town);
        // So does good word on the roads, and bad word keeps them away.
        double chance = Math.Clamp(0.12 + 0.25 * open + (heroes < 4 ? 0.2 : 0) + town.Content.Notoriety.Arrivals * fame + Word.Arrivals(town) + Omens.Heroes(town), 0.02, 0.9);
        if (!town.Rng.Chance(chance)) return;

        // More taverns, more talk on the roads: seasoned heroes hear of the town and come.
        double green = Math.Max(0.3, Math.Max(0.4, 0.7 - town.Content.Rumors.BetterHeroesPerTavern * (taverns.Count - 1)) - town.Content.Notoriety.Seasoned * fame - Word.Seasoned(town));
        int level = (town.Rng.Chance(green) ? 1 : town.Rng.Chance(0.8) ? 2 : 3) + Saga.ArrivalBonus(town);
        var a = CreateHero(town, PickClass(town), level);
        string where = taverns[town.Rng.Range(0, taverns.Count)].Name;
        town.Log(LedgerCategory.Arrival, Importance.Notable, $"{a.Describe()}, walks into {where} looking for paid work. {Annals.Past(town, a)} ({a.TraitList()})", [a.Id]);
    }

    private static void LodgingAndRest(Town town)
    {
        var taverns = town.Taverns;
        foreach (var a in town.Heroes)
        {
            var s = a.Hero!;
            if (s.AwayUntil != null) continue;
            // A bed at the company's bastion, or above the tavern.
            bool home = Bastions.Lodges(town, a) || s.Sworn || s.Holding != "";
            s.Lodging = home || (taverns.Count > 0 && a.Gold >= Lodging);
            if (s.Lodging && !home)
            {
                var t = taverns[a.Id % taverns.Count];
                a.Gold -= Lodging;
                t.Cash += Lodging;
            }
            // A long rest heals; a bed heals faster, and the shrine or temple more.
            int healed = (int)Math.Ceiling(s.MaxHp * (s.Lodging ? 0.5 : 0.25) * Curses.Heal(town.Content, s));
            if (s.Hp < s.MaxHp) healed = Shrines.Rest(town, a, healed);
            s.Hp = Math.Min(s.MaxHp, s.Hp + healed);
        }
    }

    private static void ExpireBounties(Town town)
    {
        foreach (var b in town.Bounties)
        {
            if (b.Status != BountyStatus.Open || town.Tick < b.ExpiresTick) continue;
            b.Status = BountyStatus.Expired;
            Refund(town, b);
            Notables.QuestLapsed(town, b);
            town.Log(LedgerCategory.Town, Importance.Routine, $"No one takes the bounty on {Site(town, b.SiteId)!.Def.Name}; the council withdraws it.");
        }
    }

    /// <summary>Stand-in mayor: put idle treasury gold to work on the easiest uncleared site.</summary>
    private static void AutoPost(Town town)
    {
        if (town.Bounties.Any(IsActive)) return;
        if (town.Heroes.Count == 0 && town.Taverns.Count == 0) return;
        var target = town.Sites
            .Where(s => s.Known != SiteKnowledge.Unheard && !s.ThoughtCleared && s.Trust >= 50)
            // The undercity only once it stirs: the war on the surface comes first.
            .Where(s => !Undercity.IsUnder(town, s) || Undercity.Stirring(town, s))
            // The big bad's quest comes first (DarkQuests); then the trouble near town, which feeds the warband, before the far wilds.
            .OrderBy(s => DarkQuests.Sought(town, s) ? 0 : 1)
            .ThenBy(s => s.Generated)
            .ThenBy(s => s.BelievedTier)
            .ThenBy(s => s.Attempts)
            .FirstOrDefault();
        if (target == null) return;
        double reward = Asking(town, target.BelievedTier) + 20 * target.Attempts;
        if (town.Treasury < reward + 100) return;
        PostBounty(town, target.Def.Id, reward);
    }

    private static double Interest(Town town, Agent a, Bounty bounty, int partySize) =>
        Motive(town, a, bounty, partySize) + town.Rng.Next() * 0.3;

    /// <summary>What a hero makes of a bounty before the mood of the day: the pay against what they'd ask, their traits and the terms.</summary>
    internal static double Motive(Town town, Agent a, Bounty bounty, int partySize) =>
        // Summed term by term in this order, so the same heroes decide the same way they always have.
        PayRatio(town, a, bounty, partySize) +
        (a.Has(Trait.Greedy) ? 0.2 : 0) +
        (a.Has(Trait.HotTempered) ? 0.15 : 0) +
        (a.Has(Trait.Ambitious) ? 0.1 : 0) -
        (a.Has(Trait.Lazy) ? 0.3 : 0) +
        Systems.Terms.Interest(town, a, bounty) -
        Armory.TermsCut(town, a, bounty) +
        (a.Hero!.Vow == bounty.SiteId ? town.Content.Voices.VowPull : 0);

    /// <summary>How a hero's traits tilt them toward paid work.</summary>
    internal static double Leaning(Agent a) =>
        (a.Has(Trait.Greedy) ? 0.2 : 0) +
        (a.Has(Trait.HotTempered) ? 0.15 : 0) +
        (a.Has(Trait.Ambitious) ? 0.1 : 0) -
        (a.Has(Trait.Lazy) ? 0.3 : 0);

    /// <summary>A hero's share of the reward against the share that makes the work worth it to them.</summary>
    internal static double PayRatio(Town town, Agent a, Bounty bounty, int partySize)
    {
        // Heroes judge a site by what the town believes of it, so a rumour that undersells it is deadly.
        int tier = Math.Max(1, Site(town, bounty.SiteId)!.BelievedTier);
        // A famous name wants a bigger share before the work is worth it.
        double share = bounty.Reward / Math.Max(1, partySize) / Notoriety.FeeFactor(town, a);
        // A rich town and a seasoned hero both raise the share that makes the work worth it.
        double expected = tier * 40 * Wealth(town) * (1 + town.Content.Bounties.Pay.HeroLevel * a.Hero!.Level);
        return share / expected;
    }

    public static bool Available(Agent a)
    {
        var s = a.Hero!;
        return s.AwayUntil == null && s.JailedUntil == null && s.Hp >= s.MaxHp * 0.7 && !a.Departed && s.Duty == HeroDuty.None;
    }

    private static void FormParties(Town town)
    {
        foreach (var bounty in town.Bounties)
        {
            if (bounty.Status != BountyStatus.Open) continue;
            var s = Site(town, bounty.SiteId)!;
            int tier = Math.Max(1, s.BelievedTier);
            var pool = town.Heroes.Where(h => Muster.Free(town, h)).OrderByDescending(h => h.Hero!.Level).ThenBy(h => h.Id).ToList();
            if (pool.Count == 0) continue;

            var leader = pool.FirstOrDefault(h => Interest(town, h, bounty, 3) >= 0.6);
            if (leader == null) continue;

            var members = new List<Agent> { leader };
            int levels = leader.Hero!.Level;
            // A company rides with its own first, then newcomers it has room for.
            var recruits = pool
                .Where(h => h.Id != leader.Id && Muster.Rides(town, leader, h))
                .OrderByDescending(h => h.Hero!.PartyName != null)
                .ThenByDescending(h => leader.OpinionOf(h.Id))
                .ThenByDescending(h => h.Hero!.Level)
                .ThenBy(h => h.Id)
                .ToList();
            foreach (var r in recruits)
            {
                if (members.Count >= 4 || (members.Count >= 3 && levels >= 3 * tier + 2)) break;
                if (leader.OpinionOf(r.Id) < -10 || r.OpinionOf(leader.Id) < -10) continue;
                if (!Muster.Rides(town, leader, r, members.Count(m => m.Hero!.PartyName == null))) continue;
                if (Interest(town, r, bounty, members.Count + 1) < 0.5) continue;
                members.Add(r);
                levels += r.Hero!.Level;
            }
            // Heroes won't walk into a site they can't handle; the bounty waits for a stronger company.
            if (levels < 3 * tier || members.Count < 3) continue;
            if (!Passage.CanReach(town, members, s)) continue;

            var party = Launch(town, members, s, bounty);
            town.Log(
                LedgerCategory.Quest,
                Importance.Notable,
                $"The {party.Name} ({string.Join(", ", members.Select(m => $"{m.GivenName} the {m.Hero!.Cls.Name}"))}) take the bounty on {s.Def.Name} and {(s.Def.Distance == 0 ? "go down with lanterns at dusk" : "march out at dawn")}{Roads.Summary(party)}.",
                members.Select(m => m.Id).ToList());
        }
    }

    /// <summary>Send a party out to a site, taking its bounty (if given) and any settlement's open plea for it.</summary>
    internal static Party Launch(Town town, List<Agent> members, SiteState s, Bounty? bounty)
    {
        var leader = members[0];
        // A company keeps its name; newcomers riding with it join it, and a band of newcomers takes a new one.
        string? name = leader.Hero!.PartyName;
        if (name == null || town.Parties.Any(p => !p.Returned && p.Name == name)) name = Muster.Name(town);
        var party = new Party
        {
            Id = town.Parties.Count + 1,
            Name = name,
            Members = members.Select(m => m.Id).ToList(),
            BountyId = bounty?.Id,
            SiteId = s.Def.Id,
            DepartTick = town.Tick,
            Start = Lofted.Start(town, World.Spot(town, s)),
        };
        // A name the foe has never heard of doesn't make it run: more of them stand and fight.
        int unproven = Notoriety.Swell(town, name, s);
        if (unproven > 0)
        {
            party.Swell += unproven;
            party.Said.Add($"Nobody at {s.Def.Name} has heard of the {name}, so more of the foe stand and fight (renown {Notoriety.RenownOf(town, name)}, it suggests {Notoriety.Suggested(town, s)}).");
        }
        Roads.Depart(town, party, members, s, bounty);
        town.Parties.Add(party);
        if (bounty != null)
        {
            bounty.Status = BountyStatus.Taken;
            bounty.PartyId = party.Id;
        }
        var plea = town.Pleas.FirstOrDefault(p => p.SiteId == s.Def.Id && p.Status == PleaStatus.Open);
        if (plea != null)
        {
            plea.Status = PleaStatus.Taken;
            plea.PartyId = party.Id;
            party.PleaId = plea.Id;
        }
        s.Attempts++;
        Shrines.Outfit(town, members);
        Scrolls.Outfit(town, members);
        Alchemy.Outfit(town, members, s.Def);
        foreach (var m in members)
        {
            m.Hero!.PartyName = name;
            m.Hero.AwayUntil = party.ReturnTick;
            m.Activity = Activity.Idle;
            m.LocationId = null;
        }
        return party;
    }

    private static void ReturnParties(Town town)
    {
        foreach (var party in town.Parties)
        {
            if (party.Returned || town.Tick < party.ReturnTick) continue;
            party.Returned = true;
            var s = Site(town, party.SiteId)!;
            var bounty = party.BountyId is int bid ? town.Bounties[bid - 1] : null;
            var members = party.Members.Select(town.Agent).ToList();
            // Only a council bounty buys the council the report; anyone else keeps theirs to sell.
            party.Withheld = !Intel.Commissioned(town, party);
            if (party.RecalledTick != null)
            {
                Knowledge.ComeHome(town, party, s, bounty, members);
                continue;
            }
            if (!party.Resolved) Roads.Arrive(town, party);
            if (town.Tick < party.ReturnTick)
            {
                // Stranded where it went (Reaches): not home after all.
                party.Returned = false;
                continue;
            }
            if (party.Empty)
            {
                // The rumour that sent them was false: the place stands empty.
                foreach (var m in members) m.Hero!.AwayUntil = null;
                if (bounty != null)
                {
                    bounty.Status = BountyStatus.Failed;
                    Refund(town, bounty);
                    Notables.QuestHome(town, bounty, party, false);
                }
                if (party.PleaId is int pid) World.Reopen(town.Pleas[pid - 1]);
                s.ClearedTick = town.Tick;
                party.SawCleared = true;
                // Whatever killed the fallen is gone too: the party carries them home.
                if (Remains.At(town, s.Def.Id) is { } lost) Remains.Recover(town, lost, party);
                // Word that the place is empty gets round the taverns either way; the report itself is for sale.
                if (!party.Withheld) Rumors.Confirm(town, s, $"the {party.Name}");
                else s.ThoughtCleared = true;
                town.Log(
                    LedgerCategory.Quest,
                    Importance.Notable,
                    $"The {party.Name} find {s.Def.Name} empty, as cold as the day it was cleared. The tavern was wrong" + (bounty != null ? "; the bounty goes back to the treasury." : ".") + (party.Withheld ? Intel.Keeping : ""),
                    members.Select(m => m.Id).ToList());
                continue;
            }
            var result = party.Result!;
            result.Apply(members);
            // What they passed on the road, the town hears of now (an old gateway).
            if (result.Outcome != ExpeditionOutcome.Wiped)
            {
                Wayfaring.Notice(town, party.Out, party.Start);
                Wayfaring.Notice(town, party.Home, World.Spot(town, s));
            }
            Patrons.Home(town, result);

            var chronicle = new Chronicle(
                town.Chronicles.Count + 1,
                party.Name,
                s.Def.Name,
                members.Select(m => m.Describe()).ToList(),
                SimTime.DayOf(party.DepartTick),
                town.Day,
                result.Outcome,
                result.Lines,
                result.GoalMet && result.Outcome != ExpeditionOutcome.Wiped)
            {
                Beats = result.Beats,
                MemberIds = members.Select(m => m.Id).ToList(),
                Dead = result.Dead,
            };
            town.Chronicles.Add(chronicle);

            // Bodies carried home can be raised at the temple; a wiped party leaves nobody to carry them.
            // A rescue or a raid on the hoard earns the bounty without clearing the place.
            bool earned = result.Outcome == ExpeditionOutcome.Cleared || result.GoalMet;
            double pay = result.Gold + (earned ? bounty?.Reward ?? 0 : 0);
            var rites = new List<(string Text, int Id)>();
            var raised = result.Outcome == ExpeditionOutcome.Wiped ? [] : Shrines.Raise(town, result.Dead, party.Name, ref pay, rites);
            var dead = result.Dead.Where(id => !raised.Contains(id)).ToList();
            foreach (int id in raised) Loyalty.Sway(town.Agent(id), town.Content.Loyalty.Raised);
            var survivors = members.Where(m => !dead.Contains(m.Id)).ToList();
            Wounds.Home(town, result, survivors, s.Def.Name);
            foreach (var m in members)
            {
                var sheet = m.Hero!;
                sheet.AwayUntil = null;
                sheet.Expeditions++;
                sheet.Kills += result.Kills.Get(m.Id, 0);
            }

            // The dead are mourned; the living split the take.
            foreach (int id in dead)
            {
                var d = town.Agent(id);
                d.Dead = true;
                Population.Depart(town, d);
                foreach (var other in town.Residents) if (other.OpinionOf(id) >= 40) other.Loneliness = Math.Min(100, other.Loneliness + 30);
            }

            if (result.Outcome != ExpeditionOutcome.Wiped) Heraldry.Home(town, party.Name, result.Dead.Count > 0 || result.Kills.Count > 0 && survivors.Any(m => m.Hero!.Hp < m.Hero.MaxHp / 2));
            if (result.Outcome == ExpeditionOutcome.Wiped) party.Withheld = false;
            party.SawCleared = result.Outcome == ExpeditionOutcome.Cleared;
            if (result.Outcome != ExpeditionOutcome.Wiped && !party.Withheld) Rumors.Confirm(town, s, $"the {party.Name}");
            else if (result.Outcome == ExpeditionOutcome.Wiped && result.SiteCleared)
            {
                // They killed it and died on the road home; the town never learns.
                s.Cleared = true;
                s.ClearedTick = town.Tick;
            }
            if (bounty != null && bounty.Plot != "" && result.Outcome != ExpeditionOutcome.Wiped && (result.GoalMet || result.Outcome == ExpeditionOutcome.Cleared)
                && town.Content.Bounties.Plots.FirstOrDefault(p => p.Id == bounty.Plot) is { } told)
            {
                Notoriety.CompanyDeed(town, party.Name, told.Renown);
                town.Log(LedgerCategory.Quest, Importance.Notable, $"The {party.Name} pull off {told.Name}, and the taverns will be telling it for a year.", party.Members.ToList());
            }
            if (result.GoalMet && result.Outcome != ExpeditionOutcome.Wiped)
            {
                if (bounty != null) bounty.Status = BountyStatus.Completed;
                if (Captives.At(town, s.Def.Id) is { } held && bounty?.Terms == "rescue") Captives.Free(town, held, party);
                if (Remains.At(town, s.Def.Id) is { } lost && bounty != null && Systems.Terms.Of(town, bounty).Fallen) Remains.Recover(town, lost, party);
            }
            else if (result.Outcome == ExpeditionOutcome.Cleared)
            {
                if (result.Talked) Stories.Remember(town, "parleyed");
                s.Cleared = true;
                if (Captives.At(town, s.Def.Id) is { } freed) Captives.Free(town, freed, party);
                if (Remains.At(town, s.Def.Id) is { } fallen) Remains.Recover(town, fallen, party);
                // A victory is the talk of the taverns even when the report isn't sold: the town knows the
                // place is cleared, though not what the company saw on the way.
                s.ThoughtCleared = true;
                s.ClearedTick = town.Tick;
                if (bounty != null) bounty.Status = BountyStatus.Completed;
                if (Intel.Commissioned(town, party))
                {
                    Intel.Deal(town, party.Name, town.Content.World.Intel.Bounty);
                    Intel.Company(town, party.Name).Bounties++;
                }
            }
            else if (bounty != null)
            {
                bounty.Status = BountyStatus.Failed;
                Refund(town, bounty);
            }
            if (earned && result.Outcome != ExpeditionOutcome.Wiped && bounty is { Errand: not "" }) Errands.Keep(town, bounty);
            if (bounty is { NotableId: not null }) Notables.QuestHome(town, bounty, party, bounty.Status == BountyStatus.Completed);
            pay += World.AfterDelve(town, party, s, result.Outcome) + party.Bonus;
            Alchemy.Homecoming(town, party, s.Def, result.Outcome, survivors);
            if (result.Outcome == ExpeditionOutcome.Cleared && survivors.Count > 0) Notoriety.Cleared(town, party, survivors, s.Def.Tier, party.PleaId != null);
            if (result.Outcome == ExpeditionOutcome.Cleared && survivors.Count > 0) Eggs.Found(town, party, s);
            if (survivors.Count > 0)
            {
                double share = pay / survivors.Count;
                double townShare = earned && bounty != null ? bounty.Reward / survivors.Count : 0;
                // Clearing a site is a milestone worth as much again as a hard fight there.
                int xp = result.Xp + (result.Outcome == ExpeditionOutcome.Cleared ? World.Milestone(town, s) : 0);
                int xpShare = (int)Math.Floor((double)xp / survivors.Count);
                foreach (var m in survivors)
                {
                    m.Gold += share;
                    m.Hero!.Pay += townShare;
                    GainXp(town, m, xpShare);
                    foreach (var o in survivors) if (o.Id != m.Id) m.ChangeOpinion(o.Id, 15);
                }
            }

            string names = Expedition.ListNames(members.Select(m => m.GivenName).ToList());
            string deadNames = Expedition.ListNames(result.Dead.Select(id => town.Agent(id).GivenName).ToList());
            string text;
            if (result.Outcome == ExpeditionOutcome.Cleared)
            {
                text = $"The {party.Name} return from {s.Def.Name} victorious with {Js.Str(Js.Round(pay))} gold to split" +
                       (result.Dead.Count > 0 ? $", but {deadNames} did not come home." : ".");
            }
            else if (result.Outcome == ExpeditionOutcome.Wiped)
            {
                text = $"The {party.Name} never return from {s.Def.Name}. {names} {(members.Count == 1 ? "is" : "are")} presumed dead.";
            }
            else if (chronicle.GoalMet)
            {
                text = $"The {party.Name} are back from {s.Def.Name} with what they went for" +
                       (result.Dead.Count > 0 ? $", but {deadNames} did not come home." : ".");
            }
            else
            {
                text = $"The {party.Name} limp back from {s.Def.Name} empty-handed" +
                       (result.Dead.Count > 0 ? $", carrying {(result.Dead.Count == 1 ? $"{deadNames}'s body" : $"the bodies of {deadNames}")}." : ".");
            }
            if (party.Withheld) text += Intel.Keeping;
            town.Chronicles[^1] = chronicle with { Gold = (int)Js.Round(pay) };
            town.Log(LedgerCategory.Quest, Importance.Major, $"{text} (Chronicle {chronicle.Id})", members.Select(m => m.Id).ToList());
            // The fallen are buried (or mourned, with nobody to carry them home), a friend may swear to finish
            // what killed them, and the living come home with new names for each other.
            Voices.Funeral(town, dead.Select(town.Agent).ToList(), survivors, s.Def.Id, s.Def.Name, result.Outcome != ExpeditionOutcome.Wiped);
            if (result.Outcome == ExpeditionOutcome.Cleared) Voices.Cleared(town, s.Def.Id, s.Def.Name, survivors);
            Voices.Jokes(town, party.Name, result.Gags, survivors);
            // A company lost to the last hero: their gear lies with them, and a card asks whether to go after it.
            if (result.Outcome == ExpeditionOutcome.Wiped) Stories.Lost(town, Remains.Lie(town, party, s, members), party);
            foreach (var (rite, id) in rites) town.Log(LedgerCategory.Faith, Importance.Major, rite, [id]);
            Lairs.AfterDelve(town, s, result);
            foreach (var it in result.Items)
            {
                if (it.HeroId == null) Watch.ReceiveRelic(town, it.Item, party.Name);
                else if (survivors.Any(m => m.Id == it.HeroId) && !Armory.HandIn(town, party, bounty, town.Agent(it.HeroId.Value), it.Item))
                {
                    Gear.Claim(town, town.Agent(it.HeroId.Value), it.Item);
                    Moments.Claimed(town, town.Agent(it.HeroId.Value), it.Item, party.Name, s, survivors);
                }
            }
        }
    }

    internal static void GainXp(Town town, Agent a, int xp)
    {
        var s = a.Hero!;
        s.Xp += xp;
        while (s.Level < XpLevels.Length && s.Xp >= XpLevels[s.Level])
        {
            s.Level++;
            int before = s.MaxHp;
            s.MaxHp = Expedition.HeroMaxHp(s, a.Abilities.Con);
            s.Hp += s.MaxHp - before;
            // Crossing into a new tier of play: the realm, then the world, start to know the name.
            string fame = s.Level switch
            {
                5 => " Bards in other towns have started singing about them.",
                11 => " Kings send letters to them by name.",
                17 => " The whole world knows the name.",
                _ => "",
            };
            town.Log(LedgerCategory.Quest, fame != "" ? Importance.Major : Importance.Notable, $"{a.Name} reaches level {s.Level}.{fame}", [a.Id]);
            Notoriety.Leveled(town, a, s.Level);
            Moments.Leveled(town, a, s.Level, before);
        }
    }
}
