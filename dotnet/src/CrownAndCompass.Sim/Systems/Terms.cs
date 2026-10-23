namespace CrownAndCompass.Sim.Systems;

/// <summary>How a bounty is to be done: slay, drive off, talk down, rescue or fetch (content/game/bounties.json).</summary>
public sealed record TermDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>"drive the occupants out of", for "a bounty to drive the occupants out of the Goblin Warrens".</summary>
    public string Verb { get; init; } = "";
    public string Text { get; init; } = "";

    /// <summary>The suggested reward, as a share of a slaying's.</summary>
    public double Pay { get; init; } = 1;

    /// <summary>What heroes make of the choice (<see cref="Regard"/>).</summary>
    public List<string> Tags { get; init; } = [];

    /// <summary>Share of a site's rooms the party fights through before the job is done (0: all of them and the boss).</summary>
    public double Rooms { get; init; }

    /// <summary>Share of the hoard's gold they bring back when the job ends short of the heart of the place.</summary>
    public double Loot { get; init; }

    /// <summary>How soon something moves back in, as a share of the usual wait.</summary>
    public double Resettle { get; init; } = 1;

    /// <summary>Talking down: the boss's Intelligence it needs, and the DC of the party's best voice.</summary>
    public int MinInt { get; init; }
    public int Dc { get; init; }
    public int DcPerTier { get; init; }

    /// <summary>Only where townsfolk are held captive.</summary>
    public bool Captives { get; init; }

    /// <summary>The party brings back the best thing in the hoard for certain.</summary>
    public bool Item { get; init; }

    /// <summary>Only where a company of the town's was wiped out: the party brings home their bones and their gear.</summary>
    public bool Fallen { get; init; }

    /// <summary>A quiet job: the party sneaks past rooms rather than fighting them, and every slip raises the alarm (<see cref="HeistRules"/>).</summary>
    public bool Heist { get; init; }
}

/// <summary>
/// Heists and rescues (bounties.json "heist"): before each room the party's quietest tries to slip past (DC sneakDc plus
/// perTier a tier); each failure fights the room and raises the alarm, and at alarmMax the whole place wakes (wake more
/// foes in every room after). A hoard sits behind a vault of layers locks (by tier, at most the list's last), each a
/// check of its kind (trap: locks, puzzle: wards, perception: the hidden catch) at layerDc plus perTier a tier, which
/// shares the same alarm: a failed lock raises it, and at alarmMax inside the vault the door slams and they run with
/// bolted of the gold and no treasure.
/// </summary>
/// <summary>
/// A bounty told like a classic tale (bounties.json "plots"): the first plot whose terms and when fit the job names it
/// in the posting and on the board, and the company that pulls it off earns renown for it. When: captives, item,
/// fallen, undead (an undead master), named (a named master clever enough to scheme), beast (a master with no more
/// wits than a beast), camp (the warband's camp), or any.
/// </summary>
public sealed record PlotDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Pitch { get; init; } = "";
    public List<string> Terms { get; init; } = [];
    public string When { get; init; } = "any";
    public int Renown { get; init; } = 1;
}

/// <summary>
/// A strong caster's own way past a kind of trouble on a delve (bounties.json "ways"): a hero of one of the classes at
/// minLevel or more gets the party past a hazard, trap, puzzle or parley without the roll, or past a room of guards
/// without the fight, once a delve; the report says how ({name}, {class}, {text}).
/// </summary>
public sealed record CasterWay
{
    public string Kind { get; init; } = "";
    public List<string> Classes { get; init; } = [];
    public int MinLevel { get; init; } = 5;
    public string Line { get; init; } = "";
}

public sealed record HeistRules
{
    public int AlarmMax { get; init; } = 3;
    public int SneakDc { get; init; } = 10;
    public int LayerDc { get; init; } = 11;
    public int PerTier { get; init; } = 2;
    public int Wake { get; init; } = 1;
    public List<int> Layers { get; init; } = [1, 1, 2, 2, 3];
    public List<string> Kinds { get; init; } = ["trap", "puzzle", "perception"];
    public double Bolted { get; init; } = 0.5;
}

public sealed record CaptiveRules
{
    /// <summary>No captives are taken before this day; a raid that gets through carries some off at this chance.</summary>
    public int FirstDay { get; init; } = 25;
    public double Chance { get; init; } = 0.6;
    public int Min { get; init; } = 1;
    public int Max { get; init; } = 3;

    /// <summary>Days before the captives are lost, and days more for each tier of the place they are held.</summary>
    public int Days { get; init; } = 10;
    public int DaysPerTier { get; init; } = 2;
    public double FreedMandate { get; init; } = 5;
    public int FreedRenown { get; init; } = 2;
    public double LostMandate { get; init; } = 6;
    public double LostTension { get; init; } = 12;

    /// <summary>What the captors gain from captives they keep: thralls, ransom, recruits.</summary>
    public double LostStrength { get; init; } = 6;
}

/// <summary>A spell a mage casts for the town in return for an errand (content/game/bounties.json).</summary>
public sealed record ErrandBoon
{
    public string Id { get; init; } = "";

    /// <summary>"a warding on the walls".</summary>
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
    public double Dread { get; init; }
    public double Food { get; init; }
    public double Mandate { get; init; }
    public int Reveal { get; init; }
}

public sealed record BountyRules
{
    public List<ErrandBoon> Errands { get; init; } = [];

    /// <summary>A hero's interest in a bounty, more or less, when its terms suit their temper or go against it.</summary>
    public double Temper { get; init; } = 0.25;
    public List<TermDef> Terms { get; init; } = [];
    public CaptiveRules Captives { get; init; } = new();
    public BountyPay Pay { get; init; } = new();
    public OfferRules Offers { get; init; } = new();
    public HeistRules Heist { get; init; } = new();
    public List<CasterWay> Ways { get; init; } = [];
    public List<PlotDef> Plots { get; init; } = [];

    public TermDef Term(string id) => Terms.FirstOrDefault(t => t.Id == id) ?? Terms.FirstOrDefault() ?? new TermDef { Id = "slay", Name = "Slay", Verb = "clear" };
}

/// <summary>
/// What heroes ask to take on a bounty: a price that climbs steeply with the danger (PerTier, PerTierSq and PerTierCube
/// times the tier), and more from a town known to be rich (one more share of it per WealthPer gold the treasury holds
/// above WealthFree, at most WealthCap more). Seasoned heroes expect HeroLevel more of their share for each level.
/// </summary>
public sealed record BountyPay
{
    public double PerTier { get; init; } = 50;
    public double PerTierSq { get; init; } = 30;
    public double PerTierCube { get; init; }
    public double WealthFree { get; init; } = 2000;
    public double WealthPer { get; init; } = 10000;
    public double WealthCap { get; init; } = 2;
    public double HeroLevel { get; init; }
}

/// <summary>
/// A job offered to one company by name (<see cref="Roster"/>). Its riders weigh it as they weigh any bounty
/// (pay, traits, terms) and add what the company thinks of the council, being asked by name, and the danger
/// against their strength. The answer is that weighing plus a mood of the day, Spread wide, against Accept.
/// </summary>
public sealed record OfferRules
{
    /// <summary>Being asked by name flatters a company.</summary>
    public double Asked { get; init; } = 0.1;

    /// <summary>Per point of relation above (or below) 50, times this.</summary>
    public double Relation { get; init; } = 0.004;

    /// <summary>Lost at most when the riders' levels fall short of three per tier of the place, in proportion.</summary>
    public double Danger { get; init; } = 0.8;

    /// <summary>A famous company (FameTier at least PettyFame) finds a place of PettyTier or less beneath it.</summary>
    public double Petty { get; init; } = 0.15;
    public int PettyFame { get; init; } = 2;
    public int PettyTier { get; init; } = 1;

    public double Accept { get; init; } = 0.6;
    public double Spread { get; init; } = 0.4;

    /// <summary>The leaning words, by score: at least Eager, Willing, Unsure, Reluctant; anything less is Unwilling.</summary>
    public double Eager { get; init; } = 0.8;
    public double Willing { get; init; } = 0.65;
    public double Unsure { get; init; } = 0.5;
    public double Reluctant { get; init; } = 0.4;

    /// <summary>An offer below Insult of the asking price that is turned down sours dealings by InsultDealings; one taken warms them by AcceptDealings.</summary>
    public double Insult { get; init; } = 0.6;
    public double InsultDealings { get; init; } = -3;
    public double AcceptDealings { get; init; } = 2;

    /// <summary>Most riders a company sends; heroes with no company join one short of three.</summary>
    public int MaxRiders { get; init; } = 4;

    /// <summary>Keeping a company on the town's retainer (<see cref="Retainers"/>).</summary>
    public RetainerRules Retainer { get; init; } = new();
}

/// <summary>
/// A company kept on the town's retainer: a signing price of Base, plus PerLevel times its members' levels raised to
/// LevelPower, plus PerRenown per point of renown, then Wage of that a day. It needs Relation with the council to sign.
/// On the retainer it leans Bonus more to every job the council offers, its own trips' reports come home free, its
/// members are Loyalty happier, and it never leaves. A wage the treasury can't pay for LapseDays lets it go, Dealings sourer.
/// </summary>
public sealed record RetainerRules
{
    public double Base { get; init; } = 2500;
    public double PerLevel { get; init; } = 40;
    public double LevelPower { get; init; } = 1.5;
    public double PerRenown { get; init; } = 15;
    public double Wage { get; init; } = 0.01;
    public double Relation { get; init; } = 40;
    public double Bonus { get; init; } = 0.6;
    public double Loyalty { get; init; } = 10;
    public int LapseDays { get; init; } = 3;
    public double Dealings { get; init; } = -10;
}

/// <summary>Townsfolk carried off in a raid, held at a site until rescued or lost.</summary>
public sealed class Captivity
{
    public int Id { get; set; }
    public string SiteId { get; set; } = "";
    public List<int> Captives { get; set; } = [];
    public int TakenTick { get; set; }
    public int DueTick { get; set; }
    public bool Done { get; set; }
    public bool Freed { get; set; }
}

/// <summary>A company of the town's wiped out at a site, and the magic gear they carried in with them.</summary>
public sealed class FallenCompany
{
    public int Id { get; set; }
    public string SiteId { get; set; } = "";
    public string Company { get; set; } = "";
    public List<int> Heroes { get; set; } = [];
    public List<LibItem> Gear { get; set; } = [];

    /// <summary>Sending stones of the town's they carried.</summary>
    public int Stones { get; set; }
    public int Tick { get; set; }
    public bool Done { get; set; }
}

/// <summary>
/// Bounty terms: the mayor says how a job is to be done, not only where. Slaying clears a site for good;
/// driving the occupants off is quicker and cheaper but they come back sooner; talking them down costs no
/// blood when it works; a rescue or a fetch goes in only as far as it must. Heroes favour terms that suit
/// their temper, and every hero judges the terms the mayor posts.
/// </summary>
public static class Terms
{
    private static BountyRules Rules(Town town) => town.Content.Bounties;

    public static TermDef Of(Town town, Bounty b) => Rules(town).Term(b.Terms);

    /// <summary>Why these terms can't be posted on this site, or null.</summary>
    public static string? Blocked(Town town, SiteState s, TermDef term)
    {
        if (term.MinInt > 0)
        {
            var boss = town.Content.Monster(s.Def.Boss.Monster);
            // The town only knows what it believes lives there; the bargain is made with the real occupant.
            if (boss.Int < term.MinInt) return $"what lives at {s.Def.Name} won't bargain";
        }
        if (term.Captives && Captives.At(town, s.Def.Id) == null) return "nobody is held captive there";
        if (term.Fallen && Remains.At(town, s.Def.Id) == null) return "no company of ours fell there";
        return null;
    }

    /// <summary>The reward a bounty on these terms should carry, after the suggested reward for the believed tier.</summary>
    public static double Suggested(Town town, SiteState s, TermDef term) => Js.Round(Quests.Asking(town, Math.Max(1, s.BelievedTier)) * term.Pay);

    /// <summary>A hero's interest in the terms: more when they suit the hero's temper, less when they go against it.</summary>
    public static double Interest(Town town, Agent a, Bounty b)
    {
        double v = Regard.Moves(town, a, Of(town, b).Tags);
        return v > 0 ? Rules(town).Temper : v < 0 ? -Rules(town).Temper : 0;
    }

    /// <summary>The classic tale a job would be told as: the first plot in content order whose terms and when fit it.</summary>
    public static PlotDef? PlotFor(Town town, SiteState s, TermDef term)
    {
        var boss = town.Content.Monster(s.Def.Boss.Monster);
        bool Fits(string when) => when switch
        {
            "captives" => term.Captives,
            "item" => term.Item,
            "fallen" => term.Fallen,
            "undead" => boss.Undead,
            "named" => boss.Named && boss.Int >= 8,
            "beast" => boss.Int <= 3,
            "camp" => s.Def.Id == town.Content.Enemy.Outpost.Id,
            _ => true,
        };
        return town.Content.Bounties.Plots.FirstOrDefault(p => (p.Terms.Count == 0 || p.Terms.Contains(term.Id)) && Fits(p.When));
    }
}

/// <summary>
/// Rescues against the clock. A raid that gets through can carry townsfolk off to the raiders' camp. They
/// are held there for a few days: a party that clears the place, or goes in on a rescue, brings them home;
/// if the days run out they are lost, and the town remembers it.
/// </summary>
public static class Captives
{
    private static CaptiveRules Rules(Town town) => town.Content.Bounties.Captives;

    public static Captivity? At(Town town, string siteId) => town.Captivities.FirstOrDefault(c => !c.Done && c.SiteId == siteId);

    public static IEnumerable<Captivity> Held(Town town) => town.Captivities.Where(c => !c.Done);

    /// <summary>After a raid gets through at <paramref name="target"/>: perhaps its workers are carried off to <paramref name="site"/>. Returns the line for the raid's Ledger entry.</summary>
    public static string Take(Town town, SiteState? site, Building? target)
    {
        var r = Rules(town);
        if (site == null || site.Cleared || target == null || town.Day < r.FirstDay || At(town, site.Def.Id) != null || !town.Rng.Chance(r.Chance)) return "";
        // Hands without property or a council seat: the ones working the outlying fields at night.
        var seated = town.Seats.Where(x => x.HolderId != null).Select(x => x.HolderId!.Value).ToHashSet();
        var owners = town.Buildings.Where(b => b.OwnerId != null).Select(b => b.OwnerId!.Value).ToHashSet();
        var pool = target.Workers.Select(town.Agent).Where(a => !a.Departed && a.Hero == null && !seated.Contains(a.Id) && !owners.Contains(a.Id)).OrderBy(a => a.Id).ToList();
        if (pool.Count == 0) return "";
        int n = Math.Min(pool.Count, town.Rng.Range(r.Min, r.Max + 1));
        var taken = new List<Agent>();
        for (int i = 0; i < n; i++)
        {
            var a = pool[town.Rng.Range(0, pool.Count)];
            pool.Remove(a);
            taken.Add(a);
        }
        foreach (var a in taken) Population.Depart(town, a);
        int days = r.Days + r.DaysPerTier * Math.Max(1, site.Def.Tier);
        town.Captivities.Add(new Captivity
        {
            Id = town.Captivities.Count + 1,
            SiteId = site.Def.Id,
            Captives = taken.Select(a => a.Id).ToList(),
            TakenTick = town.Tick,
            DueTick = town.Tick + days * SimTime.HoursPerDay,
        });
        if (site.Known == SiteKnowledge.Unheard) Rumors.Hear(town, site, Rumors.Occupants(town.Content, site.Def), site.Def.Tier, 60);
        return $". They drag {Names(taken)} off toward {site.Def.Name}; whoever goes after them has {days} days";
    }

    private static string Names(List<Agent> people) =>
        people.Count == 1 ? people[0].Name : $"{string.Join(", ", people.Take(people.Count - 1).Select(a => a.Name))} and {people[^1].Name}";

    /// <summary>Captives whose days have run out are lost.</summary>
    public static void Daily(Town town)
    {
        foreach (var c in Held(town).ToList())
        {
            var s = Quests.Site(town, c.SiteId);
            if (s == null || s.Cleared) Free(town, c, null);
            else if (town.Tick >= c.DueTick) Lose(town, c);
        }
    }

    /// <summary>The captives come home: by a party's hand, or because their captors are gone.</summary>
    public static void Free(Town town, Captivity c, Party? by)
    {
        var r = Rules(town);
        c.Done = true;
        c.Freed = true;
        var people = c.Captives.Select(town.Agent).Where(a => !a.Dead).ToList();
        foreach (var a in people)
        {
            // They come home with nothing: no bed, no job, until they find them again.
            a.Departed = false;
            a.Hunger = Math.Max(a.Hunger, 60);
            // A cult that dropped them while they were gone has lost its hold: they come home keeping no god.
            if (Faith.Of(town, a)?.Evil == true && Factions.Of(town, a).Count == 0)
            {
                a.Faith = "";
                a.Devout = false;
            }
        }
        town.Mandate = Math.Min(100, town.Mandate + r.FreedMandate);
        Stories.Remember(town, "captives-freed");
        if (by != null)
            foreach (var m in by.Members.Select(town.Agent).Where(m => !m.Departed && m.Hero != null))
                m.Hero!.Renown += r.FreedRenown;
        string site = Quests.Site(town, c.SiteId)?.Def.Name ?? "the raiders' camp";
        if (by != null) Notoriety.Record(town, by.Name, $"brought {(people.Count == 1 ? "a captive" : $"{people.Count} captives")} home from {site}");
        town.Log(LedgerCategory.Quest, Importance.Major,
            by != null
                ? $"The {by.Name} bring {Names(people)} home from {site}. The whole street turns out for them."
                : $"With {site} empty, {Names(people)} find their own way home.",
            people.Select(a => a.Id).ToList());
    }

    private static void Lose(Town town, Captivity c)
    {
        var r = Rules(town);
        c.Done = true;
        var people = c.Captives.Select(town.Agent).ToList();
        foreach (var a in people)
        {
            a.Dead = true;
            foreach (var other in town.Residents) if (other.OpinionOf(a.Id) >= 40) other.Loneliness = Math.Min(100, other.Loneliness + 30);
        }
        town.Mandate = Math.Max(0, town.Mandate - r.LostMandate);
        town.Tension += r.LostTension;
        Stories.Remember(town, "captives-lost");
        var s = Quests.Site(town, c.SiteId);
        if (s != null) s.Strength = Math.Min(town.Content.Lairs.MaxStrength, s.Strength + r.LostStrength * people.Count);
        town.Log(LedgerCategory.Hardship, Importance.Major,
            $"Nobody came for {Names(people)}. Word comes back from {s?.Def.Name ?? "the raiders' camp"} that they are gone, and their families wear grey.",
            people.Select(a => a.Id).ToList());
    }

    /// <summary>Stand-in mayor: a rescue bounty for every camp holding townsfolk, before anything else.</summary>
    public static void AutoPost(Town town)
    {
        var rescue = town.Content.Bounties.Terms.FirstOrDefault(t => t.Captives);
        if (rescue == null) return;
        foreach (var c in Held(town).OrderBy(c => c.Id))
        {
            if (town.Bounties.Any(b => b.SiteId == c.SiteId && Quests.IsActive(b))) continue;
            if (Quests.Site(town, c.SiteId) is not { } s) continue;
            double reward = Terms.Suggested(town, s, rescue);
            if (town.Treasury < reward + 50) continue;
            Quests.PostBounty(town, s.Def.Id, reward, null, rescue.Id);
        }
    }
}

/// <summary>
/// Wizard's errands. A circle of mages with a hall in town sends heroes to fetch the best of a hoard, paid
/// from its own coffers, and when the job is done casts a spell for the town in thanks: a warding on the
/// walls, rain on the fields, a scrying of the wilds.
/// </summary>
public static class Errands
{
    public static ErrandBoon? Pick(Town town)
    {
        var boons = town.Content.Bounties.Errands;
        if (boons.Count == 0 || town.Content.Bounties.Terms.All(t => t.Id != "fetch")) return null;
        return boons[town.Rng.Range(0, boons.Count)];
    }

    public static void Keep(Town town, Bounty b)
    {
        var boon = town.Content.Bounties.Errands.FirstOrDefault(x => x.Id == b.Errand);
        if (boon == null) return;
        b.Errand = "";
        if (boon.Dread != 0) town.Saga.Dread = Math.Clamp(town.Saga.Dread + boon.Dread, 0, 100);
        if (boon.Food > 0) town.Market.Seed(Good.Food, boon.Food);
        if (boon.Mandate != 0) town.Mandate = Math.Clamp(town.Mandate + boon.Mandate, 0, 100);
        for (int i = 0; i < boon.Reveal; i++)
        {
            var s = town.Sites.Where(x => x.Known == SiteKnowledge.Unheard).OrderBy(x => x.Def.Distance).ThenBy(x => x.Def.Id, StringComparer.Ordinal).FirstOrDefault();
            if (s == null) break;
            Rumors.Hear(town, s, Rumors.Occupants(town.Content, s.Def), s.Def.Tier, 70);
        }
        string who = b.FunderId is int f ? town.Building(f).Name : "The mages";
        town.Log(LedgerCategory.Faction, Importance.Notable, $"{Stories.Cap(who)} keeps its word for the errand: {boon.Text}");
    }
}

/// <summary>
/// The fallen: when a company is wiped out its bones and gear lie where it fell. A card asks the mayor whether to send
/// another party after them; a bounty on the Recover terms, or any party that clears the place, brings the gear home
/// to the armory.
/// </summary>
public static class Remains
{
    public static FallenCompany? At(Town town, string siteId) => town.Fallen.FirstOrDefault(f => !f.Done && f.SiteId == siteId);

    public static TermDef? Terms(Town town) => town.Content.Bounties.Terms.FirstOrDefault(t => t.Fallen);

    /// <summary>A company is lost at a site: what they carried stays there with them.</summary>
    public static FallenCompany Lie(Town town, Party party, SiteState site, List<Agent> members)
    {
        // A second company lost at the same place lies with the first.
        if (At(town, site.Def.Id) is { } already)
        {
            already.Company += $" and the {party.Name}";
            already.Heroes.AddRange(members.Select(a => a.Id));
            already.Gear.AddRange(members.SelectMany(a => a.Hero!.Magic.Values));
            already.Stones += members.Count(a => a.Hero!.Stone);
            foreach (var a in members) a.Hero!.Stone = false;
            return already;
        }
        var f = new FallenCompany
        {
            Id = town.Fallen.Count + 1,
            SiteId = site.Def.Id,
            Company = party.Name,
            Heroes = members.Select(a => a.Id).ToList(),
            Gear = members.SelectMany(a => a.Hero!.Magic.Values).ToList(),
            Stones = members.Count(a => a.Hero!.Stone),
            Tick = town.Tick,
        };
        foreach (var a in members) a.Hero!.Stone = false;
        town.Fallen.Add(f);
        return f;
    }

    /// <summary>The recovery bounty the card offers: on the Recover terms, at the reward they suggest.</summary>
    public static double Reward(Town town, FallenCompany f) =>
        Terms(town) is { } t && Quests.Site(town, f.SiteId) is { } s ? Systems.Terms.Suggested(town, s, t) : 0;

    public static bool CanPost(Town town, string siteId) =>
        At(town, siteId) is { } f && Terms(town) is { } t && Quests.Site(town, siteId) is { } s
        && Systems.Terms.Blocked(town, s, t) == null && !s.ThoughtCleared && town.Treasury >= Reward(town, f)
        && !town.Bounties.Any(b => b.SiteId == siteId && Quests.IsActive(b));

    public static Bounty? Post(Town town, string siteId) =>
        CanPost(town, siteId) ? Quests.PostBounty(town, siteId, Reward(town, At(town, siteId)!), null, Terms(town)!.Id) : null;

    /// <summary>A party brings the fallen home: their gear goes to the armory, and the town buries its own.</summary>
    public static void Recover(Town town, FallenCompany f, Party by)
    {
        f.Done = true;
        foreach (var it in f.Gear) Armory.Add(town, it, $"the {f.Company}");
        if (Armory.StoneItem(town) is { } stone)
            for (int i = 0; i < f.Stones; i++) Armory.Add(town, stone, $"the {f.Company}");
        town.Mandate = Math.Min(100, town.Mandate + 2);
        string site = Quests.Site(town, f.SiteId)?.Def.Name ?? "the wilds";
        Notoriety.Record(town, by.Name, $"brought the fallen {f.Company} home from {site}");
        int pieces = f.Gear.Count + f.Stones;
        string gear = pieces == 0 ? "" : pieces == 1 ? $" Their {(f.Gear.Count == 1 ? f.Gear[0].Name : "sending stone")} goes to the armory." : $" {pieces} pieces of their gear go to the armory.";
        town.Log(LedgerCategory.Quest, Importance.Major, $"The {by.Name} bring the {f.Company} home from {site}, and the town buries its own.{gear}", f.Heroes);
    }
}
