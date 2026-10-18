namespace CrownAndCompass.Sim.Systems;

/// <summary>A step on the fame or infamy ladder: the points it starts at and what people call it.</summary>
public sealed record NotorietyTier
{
    public int At { get; init; }
    public string Name { get; init; } = "";
}

/// <summary>A kind of deed: the epithets it earns and how the bards say why.</summary>
public sealed record DeedKind
{
    public List<string> Epithets { get; init; } = [];
    public string Text { get; init; } = "";
}

public sealed record FameDeeds
{
    public int Plea { get; init; } = 3;
    public int Honour { get; init; } = 5;

    /// <summary>Fame for reaching a level, by level.</summary>
    public Dictionary<string, int> Levels { get; init; } = [];
}

public sealed record InfamyDeeds
{
    public int Brawl { get; init; } = 3;
    public int Theft { get; init; } = 5;
    public int Desert { get; init; } = 8;
    public int Betray { get; init; } = 25;
}

/// <summary>Heroes who make trouble in town, and the watch that catches them.</summary>
public sealed record CrimeRules
{
    /// <summary>Daily chance a hero with a bad name makes trouble, and what a hot temper and greed add (others never do).</summary>
    public double Chance { get; init; } = 0.002;
    public double HotTempered { get; init; } = 0.004;
    public double Greedy { get; init; } = 0.003;

    /// <summary>The chance grows by this share per infamy tier, and is multiplied for a restless hero.</summary>
    public double PerInfamyTier { get; init; } = 0.5;
    public double Restless { get; init; } = 2;

    /// <summary>The watch catches them with chance watch / (watch + WatchCatch).</summary>
    public double WatchCatch { get; init; } = 3;
    public double Fine { get; init; } = 10;
    public double FinePerLevel { get; init; } = 5;
    public double Theft { get; init; } = 5;
    public double TheftPerLevel { get; init; } = 3;
    public double CaughtLoyalty { get; init; } = -3;

    /// <summary>Mandate lost when nobody is caught.</summary>
    public double Mandate { get; init; } = -1;
}

/// <summary>Fame and infamy (content/game/notoriety.json).</summary>
public sealed record NotorietyRules
{
    public List<NotorietyTier> Fame { get; init; } = [];
    public List<NotorietyTier> Infamy { get; init; } = [];
    public Dictionary<string, DeedKind> Deeds { get; init; } = [];
    public FameDeeds FameDeeds { get; init; } = new();
    public InfamyDeeds InfamyDeeds { get; init; } = new();

    /// <summary>Share of a member's infamy the company's name takes on.</summary>
    public double CompanyShare { get; init; } = 0.5;

    /// <summary>The tier at which the bards give a hero an epithet.</summary>
    public int EpithetTier { get; init; } = 2;

    /// <summary>Tiers a hero needs to open the stories about famous or infamous heroes.</summary>
    public int StoryFame { get; init; } = 2;
    public int StoryInfamy { get; init; } = 1;

    /// <summary>Loyalty points per fame tier (their name is sung here), per tier past the first when no honour holds them (other towns court them), and per infamy tier.</summary>
    public double Sung { get; init; } = 2;
    public double Courted { get; init; } = -4;
    public double Scorned { get; init; } = -4;

    /// <summary>A famous hero wants a bigger share before a bounty interests them: share / (1 + Fee × tier).</summary>
    public double Fee { get; init; } = 0.1;

    /// <summary>The town's most famous company draws heroes: chance per tier, and the share of greenhorns it turns into veterans.</summary>
    public double Arrivals { get; init; } = 0.04;
    public double Seasoned { get; init; } = 0.05;

    /// <summary>Strength at the walls per fame tier of each hero at home famed enough for an epithet: the foe knows the name.</summary>
    public double Defense { get; init; } = 3;

    /// <summary>A company's asking price for a report moves by this much per fame and infamy tier.</summary>
    public double IntelFame { get; init; } = 0.1;
    public double IntelInfamy { get; init; } = -0.1;

    /// <summary>Infamy tier at which the council won't knight a hero, and at which a hero counts as dark when they leave.</summary>
    public int KnightInfamy { get; init; } = 2;
    public int DarkInfamy { get; init; } = 2;

    /// <summary>A hero's chance to fall grows by this share per infamy tier.</summary>
    public double Fall { get; init; } = 0.5;

    /// <summary>Infamy fades by one point every this many days.</summary>
    public int DecayDays { get; init; } = 15;
    public CrimeRules Crime { get; init; } = new();
}

/// <summary>
/// Notoriety: what the world says about a hero or a company. Deeds earn fame (ruins cleared, foes
/// slain at the walls, villages answered, honours, levels) or infamy (brawls, theft, desertion,
/// treachery), and both stick to the company's name as well as the hero's. Past a tier the bards give
/// a hero an epithet. Fame draws heroes to the town and stiffens the walls, but a famous hero wants
/// more for a bounty, and other towns court them unless an honour holds them. Infamy sours the
/// townsfolk on a hero: the council won't knight them, they are quicker to make trouble, to leave
/// badly, to turn or to fall, and their stories catch up with them. Famous and infamous heroes are
/// what the next campaign remembers (<see cref="Legends"/>).
/// </summary>
public static class Notoriety
{
    private static NotorietyRules Rules(Town town) => town.Content.Notoriety;

    public static int Tier(IReadOnlyList<NotorietyTier> tiers, double points)
    {
        int t = 0;
        for (int i = 0; i < tiers.Count; i++) if (points >= tiers[i].At) t = i;
        return t;
    }

    public static int FameTier(Town town, HeroSheet s) => Tier(Rules(town).Fame, s.Renown);
    public static int InfamyTier(Town town, HeroSheet s) => Tier(Rules(town).Infamy, s.Infamy);
    public static int FameTier(Town town, CompanyState c) => Tier(Rules(town).Fame, c.Renown);
    public static int InfamyTier(Town town, CompanyState c) => Tier(Rules(town).Infamy, c.Infamy);

    public static string FameName(Town town, double points) => Rules(town).Fame.Count == 0 ? "" : Rules(town).Fame[Tier(Rules(town).Fame, points)].Name;
    public static string InfamyName(Town town, double points) => Rules(town).Infamy.Count == 0 ? "" : Rules(town).Infamy[Tier(Rules(town).Infamy, points)].Name;

    /// <summary>"Wellby Underbough the Delver": the name with the epithet the bards gave them.</summary>
    public static string Known(Agent a) => a.Hero is { Epithet: not "" } s ? $"{a.Name} {s.Epithet}" : a.Name;

    // Deeds -----------------------------------------------------------------------------------------

    /// <summary>
    /// A hero's deed: fame for a fame deed, infamy for an infamous one (by kind in notoriety.json), and
    /// the company's name takes a share of the infamy. Crossing the epithet tier earns an epithet.
    /// </summary>
    public static void Deed(Town town, Agent a, string kind, int fame, int infamy = 0)
    {
        if (a.Hero is not HeroSheet s || (fame <= 0 && infamy <= 0)) return;
        var r = Rules(town);
        int fameBefore = FameTier(town, s), infamyBefore = InfamyTier(town, s);
        s.Renown += Math.Max(0, fame);
        s.Infamy += Math.Max(0, infamy);
        if (infamy > 0 && s.PartyName != null)
        {
            int share = (int)Math.Ceiling(infamy * r.CompanyShare);
            if (share > 0) Intel.Company(town, s.PartyName).Infamy += share;
        }
        int fameNow = FameTier(town, s), infamyNow = InfamyTier(town, s);
        // The bards name them once, for the deed that made the name; an ill name, once earned, outshouts a good one.
        bool infamous = !s.EpithetIll && infamyNow >= r.EpithetTier && infamyBefore < r.EpithetTier;
        bool famous = s.Epithet == "" && fameNow >= r.EpithetTier && fameBefore < r.EpithetTier;
        if ((infamous || famous) && r.Deeds.TryGetValue(kind, out var deed) && deed.Epithets.Count > 0)
        {
            string epithet = deed.Epithets[town.Rng.Range(0, deed.Epithets.Count)];
            if (epithet == s.Epithet) return;
            string old = Loyalty.Styled(town, a);
            s.Epithet = epithet;
            s.EpithetIll = infamous;
            town.Log(LedgerCategory.Quest, Importance.Notable,
                infamous
                    ? $"Folk have started calling {old} \"{epithet}\" behind their back, {deed.Text}."
                    : $"The bards have taken to calling {old} \"{epithet}\", {deed.Text}.",
                [a.Id]);
        }
    }

    /// <summary>A company's deed: its name earns fame (or infamy) of its own.</summary>
    public static void CompanyDeed(Town town, string name, int fame, int infamy = 0)
    {
        var c = Intel.Company(town, name);
        c.Renown += Math.Max(0, fame);
        c.Infamy += Math.Max(0, infamy);
    }

    /// <summary>Survivors of a party home from a cleared site: each earns fame by the site's tier, and so does the company.</summary>
    public static void Cleared(Town town, Party party, IReadOnlyList<Agent> survivors, int tier, bool plea)
    {
        var r = Rules(town);
        int fame = 2 * tier + (plea ? r.FameDeeds.Plea : 0);
        foreach (var m in survivors) Deed(town, m, plea ? "plea" : "clear", fame);
        CompanyDeed(town, party.Name, fame);
    }

    /// <summary>Fame for reaching a level the realm notices.</summary>
    public static void Leveled(Town town, Agent a, int level)
    {
        if (Rules(town).FameDeeds.Levels.TryGetValue(level.ToString(), out int fame)) Deed(town, a, "level", fame);
    }

    public static void Honoured(Town town, Agent a) => Deed(town, a, "honour", Rules(town).FameDeeds.Honour);

    /// <summary>A hero walks out on the town, or betrays it; their company's name suffers with them.</summary>
    public static void Deserted(Town town, Agent a) => Deed(town, a, "desert", 0, Rules(town).InfamyDeeds.Desert);

    public static void Betrayed(Town town, Agent a) => Deed(town, a, "betray", 0, Rules(town).InfamyDeeds.Betray);

    // What it changes ------------------------------------------------------------------------------

    /// <summary>What a hero's name does to their loyalty, as (why, points) for <see cref="Loyalty.Factors"/>.</summary>
    public static IEnumerable<(string Why, double Points)> LoyaltyFactors(Town town, Agent a)
    {
        var r = Rules(town);
        var s = a.Hero!;
        int fame = FameTier(town, s), infamy = InfamyTier(town, s);
        if (fame > 0) yield return ($"their name is sung in {town.Name}", fame * r.Sung);
        bool honoured = s.Knight || s.Champion || s.FiefId != null;
        if (fame > 1 && !honoured) yield return ("other towns court them", (fame - 1) * r.Courted);
        if (infamy > 0) yield return ("scorned in the streets", infamy * r.Scorned);
    }

    /// <summary>How much more a famous hero wants for a bounty: their share is divided by this.</summary>
    public static double FeeFactor(Town town, Agent a) => a.Hero is HeroSheet s ? 1 + Rules(town).Fee * FameTier(town, s) : 1;

    /// <summary>The fame tier of the most famous company with someone in town.</summary>
    public static int TownFame(Town town)
    {
        int best = 0;
        foreach (var c in town.Companies)
            if (c.Renown > 0 && town.Heroes.Any(h => h.Hero!.PartyName == c.Name)) best = Math.Max(best, FameTier(town, c));
        return best;
    }

    /// <summary>Strength at the walls from famous heroes at home: the foe knows their names.</summary>
    public static double Defense(Town town)
    {
        var r = Rules(town);
        double d = 0;
        foreach (var a in town.Heroes)
        {
            if (a.IsAway) continue;
            int t = FameTier(town, a.Hero!);
            if (t >= r.EpithetTier) d += t * r.Defense;
        }
        return d;
    }

    /// <summary>A famous company asks more for its reports, an infamous one less (nobody else will buy).</summary>
    public static double IntelMarkup(Town town, string company)
    {
        var r = Rules(town);
        if (Intel.Find(town, company) is not CompanyState c) return 0;
        return FameTier(town, c) * r.IntelFame + InfamyTier(town, c) * r.IntelInfamy;
    }

    public static bool TooInfamousToKnight(Town town, HeroSheet s) => InfamyTier(town, s) >= Rules(town).KnightInfamy;

    public static bool Dark(Town town, HeroSheet s) => InfamyTier(town, s) >= Rules(town).DarkInfamy;

    public static double FallFactor(Town town, HeroSheet s) => 1 + Rules(town).Fall * InfamyTier(town, s);

    /// <summary>What a hero's name does for them and to them, in a few words, for the inspector.</summary>
    public static List<string> Effects(Town town, Agent a)
    {
        var r = Rules(town);
        var s = a.Hero!;
        var list = new List<string>();
        int fame = FameTier(town, s), infamy = InfamyTier(town, s);
        if (fame > 0) list.Add($"wants {Js.Str(Js.Round(r.Fee * fame * 100))}% more for a bounty");
        if (fame >= r.EpithetTier) list.Add($"+{Js.Str(fame * r.Defense)} at the walls");
        if (infamy >= r.KnightInfamy) list.Add("too notorious to knight");
        if (infamy > 0) list.Add("quick to make trouble");
        return list;
    }

    // The day --------------------------------------------------------------------------------------

    public static void Daily(Town town)
    {
        var r = Rules(town);
        if (r.DecayDays > 0 && town.Day % r.DecayDays == 0)
        {
            foreach (var a in town.Heroes) if (a.Hero!.Infamy > 0) a.Hero.Infamy--;
            foreach (var c in town.Companies) if (c.Infamy > 0) c.Infamy--;
        }
        Crime(town);
    }

    /// <summary>Hot-tempered, greedy and infamous heroes now and then start a brawl or lift a purse in town.</summary>
    private static void Crime(Town town)
    {
        var r = Rules(town);
        var c = r.Crime;
        var lr = town.Content.Loyalty;
        foreach (var a in town.Heroes)
        {
            var s = a.Hero!;
            if (s.AwayUntil != null) continue;
            double brawl = (InfamyTier(town, s) > 0 ? c.Chance : 0) + (a.Has(Trait.HotTempered) ? c.HotTempered : 0);
            double theft = a.Has(Trait.Greedy) ? c.Greedy : 0;
            double scale = (1 + c.PerInfamyTier * InfamyTier(town, s)) * (s.Loyalty < lr.Restless ? c.Restless : 1);
            if ((theft + brawl) * scale <= 0) continue;
            double roll = town.Rng.Next();
            if (roll < theft * scale) Theft(town, a);
            else if (roll < (theft + brawl) * scale) Brawl(town, a);
        }
    }

    /// <summary>The watch's chance to catch a hero at it.</summary>
    private static bool Caught(Town town) => town.Watch > 0 && town.Rng.Chance(town.Watch / (town.Watch + Rules(town).Crime.WatchCatch));

    private static void Brawl(Town town, Agent a)
    {
        var r = Rules(town);
        var s = a.Hero!;
        var taverns = town.Taverns;
        string where = taverns.Count > 0 ? taverns[a.Id % taverns.Count].Name : "the square";
        string name = Loyalty.Styled(town, a);
        if (Caught(town))
        {
            double fine = Math.Min(Math.Floor(a.Gold), r.Crime.Fine + r.Crime.FinePerLevel * s.Level);
            a.Gold -= fine;
            if (fine > 0) town.Book(TreasuryAccount.Fines, fine);
            Loyalty.Sway(a, r.Crime.CaughtLoyalty);
            town.Log(LedgerCategory.Brawl, Importance.Notable, $"{name} breaks a chair over a drover's back at {where}. The watch hauls them out{(fine > 0 ? $" and fines them {Js.Str(fine)} gold" : "")}.", [a.Id]);
        }
        else
        {
            town.Mandate = Math.Clamp(town.Mandate + r.Crime.Mandate, 0, 100);
            town.Log(LedgerCategory.Brawl, Importance.Notable, $"{name} wrecks the taproom at {where} and walks off before anyone can stop them. The townsfolk ask what the mayor means to do about it.", [a.Id]);
        }
        Deed(town, a, "brawl", 0, r.InfamyDeeds.Brawl);
    }

    private static void Theft(Town town, Agent a)
    {
        var r = Rules(town);
        var s = a.Hero!;
        var mark = town.Buildings.Where(b => b.Complete && b.Def.IsBusiness && !b.Closed && b.Cash > 1).OrderBy(b => b.Id).ToList();
        if (mark.Count == 0) return;
        var shop = mark[town.Rng.Range(0, mark.Count)];
        double take = Math.Floor(Math.Min(shop.Cash, r.Crime.Theft + r.Crime.TheftPerLevel * s.Level));
        if (take <= 0) return;
        string name = Loyalty.Styled(town, a);
        if (Caught(town))
        {
            double fine = Math.Min(Math.Floor(a.Gold), r.Crime.Fine + r.Crime.FinePerLevel * s.Level);
            a.Gold -= fine;
            if (fine > 0) town.Book(TreasuryAccount.Fines, fine);
            Loyalty.Sway(a, r.Crime.CaughtLoyalty);
            town.Log(LedgerCategory.Hardship, Importance.Notable, $"The watch catches {name} with a hand in the till at {shop.Name}{(fine > 0 ? $", and the magistrate fines them {Js.Str(fine)} gold" : "")}.", [a.Id]);
        }
        else
        {
            shop.Cash -= take;
            a.Gold += take;
            town.Mandate = Math.Clamp(town.Mandate + r.Crime.Mandate, 0, 100);
            town.Log(LedgerCategory.Hardship, Importance.Notable, $"{Js.Str(take)} gold goes missing from the till at {shop.Name}. Everyone says it was {name}; nobody can prove it.", [a.Id]);
        }
        Deed(town, a, "theft", 0, r.InfamyDeeds.Theft);
    }
}
