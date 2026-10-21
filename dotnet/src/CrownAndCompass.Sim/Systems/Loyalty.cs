namespace CrownAndCompass.Sim.Systems;

public sealed record RewardRules
{
    /// <summary>The stand-in mayor's purse for a restless hero: this much gold per level.</summary>
    public double PerLevel { get; init; } = 10;

    /// <summary>Loyalty a gift buys per gold, divided by the hero's level; at most MaxLoyalty at once.</summary>
    public double LoyaltyPerGold { get; init; } = 0.6;
    public double MaxLoyalty { get; init; } = 15;
    public int GapDays { get; init; } = 10;

    /// <summary>Treasury the stand-in mayor keeps back before rewarding anyone.</summary>
    public double Reserve { get; init; } = 300;
}

public sealed record KnightRules
{
    public double Cost { get; init; } = 60;

    /// <summary>Gold a day from the treasury to each landless knight.</summary>
    public double Stipend { get; init; } = 2;
    public int Renown { get; init; } = 8;
    public int Level { get; init; } = 3;
    public double Loyalty { get; init; } = 25;

    /// <summary>Strength each knight at home adds at the walls, on top of their level.</summary>
    public double Defense { get; init; } = 4;
    public int Max { get; init; } = 4;

    /// <summary>Days between knightings the council will propose.</summary>
    public int GapDays { get; init; } = 12;
}

public sealed record FiefRules
{
    public double Cost { get; init; } = 40;

    /// <summary>Gold a day the land yields its knight (the stipend stops).</summary>
    public double Rent { get; init; } = 3;
    public double Loyalty { get; init; } = 20;
}

public sealed record ChampionRules
{
    public int Level { get; init; } = 5;
    public double Stipend { get; init; } = 4;
    public double Loyalty { get; init; } = 15;
    public double Defense { get; init; } = 10;
}

/// <summary>What sways a hero's loyalty and what they do without it (content/game/loyalty.json).</summary>
public sealed record LoyaltyRules
{
    public double Start { get; init; } = 50;

    /// <summary>Share of the gap to where their feelings point that loyalty closes each day.</summary>
    public double Drift { get; init; } = 0.1;
    public double PayDecay { get; init; } = 0.95;

    /// <summary>Town gold per level, lately, that wins the full Pay points.</summary>
    public double PayPerLevel { get; init; } = 20;
    public double Pay { get; init; } = 20;
    public double NoBed { get; init; } = -6;

    /// <summary>Points for a purse below BrokeGold, and for mourning a friend or spouse.</summary>
    public double Broke { get; init; } = -8;
    public double BrokeGold { get; init; } = 10;
    public double Bereaved { get; init; } = -6;
    public double GodHonoured { get; init; } = 5;
    public double EvilGod { get; init; } = -15;
    public double SecretFaction { get; init; } = -8;

    /// <summary>Points per point of a faction's standing with the town hall, for its members.</summary>
    public double FactionStanding { get; init; } = 0.1;
    public double Mandate { get; init; } = 0.15;

    /// <summary>Points per step the saga's tone has darkened past hopeful.</summary>
    public double Tone { get; init; } = -5;
    public double Yoke { get; init; } = -8;

    /// <summary>Points per menace abroad in power or worse.</summary>
    public double Menace { get; init; } = -3;
    public double Mood { get; init; } = 0.25;

    /// <summary>Points for each honour: a knighthood, lands, the champion's title.</summary>
    public double Rank { get; init; } = 10;
    public double PassedOver { get; init; } = -12;
    public double Greedy { get; init; } = -5;
    public double Generous { get; init; } = 5;

    /// <summary>One-off loyalty for being raised from the dead at the town's temple.</summary>
    public double Raised { get; init; } = 20;
    public double Restless { get; init; } = 30;
    public double Desert { get; init; } = 25;
    public int DesertDays { get; init; } = 3;
    public double LeaveChance { get; init; } = 0.25;
    public double Betray { get; init; } = 25;
    public double BetrayChance { get; init; } = 0.4;

    /// <summary>Power a menace gains per level of a hero who goes over to it.</summary>
    public double TurncoatPower { get; init; } = 12;
    public double TheftShare { get; init; } = 0.05;
    public double TheftCap { get; init; } = 80;
    public RewardRules Reward { get; init; } = new();
    public KnightRules Knight { get; init; } = new();
    public FiefRules Fief { get; init; } = new();
    public ChampionRules Champion { get; init; } = new();
}

/// <summary>
/// Heroes' loyalty, and the honours that buy it. Each hero's loyalty drifts toward where their
/// feelings point: the town's pay (bounties, gifts, stipends), a bed, their god honoured here, the
/// standing of their factions with the town hall, the mayor's mandate, the town's fortunes in the saga,
/// their own temper and the honours they hold. The council moves to knight the town's best (Ser, a
/// stipend and an oath), the mayor grants knights a cleared site as their land (the town stops paying
/// its garrison) and names a champion. Heroes pushed too far leave: they desert, run to a secret faction
/// with what they can carry from the treasury, or go over to a festering menace, which grows on what they
/// know. The worst of them wait for a siege and open a gate.
/// </summary>
public static class Loyalty
{
    private static LoyaltyRules Rules(Town town) => town.Content.Loyalty;

    public static void Arrive(Town town, Agent a)
    {
        var s = a.Hero!;
        s.Loyalty = Rules(town).Start + town.Rng.Range(-10, 11);
    }

    /// <summary>"Ser Wellby Underbough of the Wolfwood Den" for a landed knight, else the plain name.</summary>
    public static string Styled(Town town, Agent a)
    {
        var s = a.Hero;
        if (s == null || !s.Knight) return Notoriety.Known(a);
        string of = s.FiefId != null && Quests.Site(town, s.FiefId) is SiteState site ? $" of {site.Def.Name}" : "";
        return $"Ser {Notoriety.Known(a)}{of}";
    }

    // What sways them -----------------------------------------------------------------------------

    /// <summary>Why the hero feels as they do, in points: what pulls their loyalty up or down.</summary>
    public static List<(string Why, double Points)> Factors(Town town, Agent a)
    {
        var r = Rules(town);
        var s = a.Hero!;
        var list = new List<(string, double)>();
        void Add(string why, double pts)
        {
            if (Math.Abs(pts) >= 0.5) list.Add((why, pts));
        }
        Add("paid well by the town", Math.Min(r.Pay, s.Pay / (s.Level * r.PayPerLevel) * r.Pay));
        if (s.AwayUntil == null && !s.Lodging) Add("no bed to sleep in", r.NoBed);
        if (a.Gold < r.BrokeGold) Add("an empty purse", r.Broke);
        if (Factions.Bereaved(town, a)) Add("friends buried here", r.Bereaved);
        var god = Faith.Of(town, a);
        if (god != null && god.Evil) Add($"keeps {god.Name}", r.EvilGod);
        else if (god != null && town.Faiths.Dedications.Values.Contains(god.Id)) Add($"{god.Name} has a house here", r.GodHonoured);
        foreach (var f in Factions.Of(town, a))
        {
            if (f.Secret) Add($"sworn to {f.Name}", r.SecretFaction);
            Add($"{f.Name} and the town hall", f.Standing * r.FactionStanding);
        }
        Add("the mayor's standing", (town.Mandate - 50) * r.Mandate);
        if (town.Saga.Enabled) Add("dark times", (int)town.Saga.Tone * r.Tone);
        if (town.Saga.YokeId != null && !town.Saga.Defied) Add("the town pays tribute", r.Yoke);
        Add("menaces abroad", Menaces.Alive(town).Count(m => m.Stage >= MenaceStage.Power) * r.Menace);
        Add("spirits", (a.LastDayMood - 65) * r.Mood);
        foreach (var (why, pts) in Decor.Loyalty(town, a)) Add(why, pts);
        Add("memorials to the fallen", Legacies.Loyalty(town));
        foreach (var (why, pts) in Bastions.Loyalty(town, a)) Add(why, pts);
        var (regardWhy, regard) = Regard.Loyalty(town, a);
        Add(regardWhy, regard);
        if (Callings.Of(town, a) is { } calling) Add(calling.Name.ToLowerInvariant(), calling.Loyalty);
        foreach (var (why, pts) in Notoriety.LoyaltyFactors(town, a)) Add(why, pts);
        foreach (var (why, pts) in Regalia.LoyaltyFactors(town)) Add(why, pts);
        foreach (var (why, pts) in Mayoralty.LoyaltyFactors(town)) Add(why, pts);
        if (s.Knight) Add("knighted", r.Rank);
        if (s.FiefId != null) Add("lands of their own", r.Rank);
        if (s.Champion) Add($"champion of {town.Name}", r.Rank);
        if (!s.Knight && a.Has(Trait.Ambitious) && s.Level >= r.Knight.Level && s.Renown >= r.Knight.Renown) Add("passed over for knighthood", r.PassedOver);
        if (a.Has(Trait.Greedy)) Add("greedy", r.Greedy);
        if (a.Has(Trait.Generous)) Add("generous", r.Generous);
        return list;
    }

    public static double Target(Town town, Agent a) => Math.Clamp(Rules(town).Start + Js.Sum(Factors(town, a), f => f.Points), 0, 100);

    /// <summary>A kindness or a slight that moves loyalty at once.</summary>
    public static void Sway(Agent a, double points)
    {
        if (a.Hero is HeroSheet s) s.Loyalty = Math.Clamp(s.Loyalty + points, 0, 100);
    }

    // The day -------------------------------------------------------------------------------------

    public static void Daily(Town town, bool auto)
    {
        var r = Rules(town);
        foreach (var a in town.Heroes)
        {
            var s = a.Hero!;
            s.Pay *= r.PayDecay;
            Honours(town, a);
            s.Loyalty = Math.Clamp(s.Loyalty + (Target(town, a) - s.Loyalty) * r.Drift, 0, 100);
            if (s.AwayUntil != null) continue;
            if (s.Loyalty < r.Restless && !s.Restless)
            {
                s.Restless = true;
                var worst = Factors(town, a).OrderBy(f => f.Points).First();
                town.Log(LedgerCategory.Quest, Importance.Notable, $"{Styled(town, a)} grumbles in the tavern that {town.Name} has done little for them ({worst.Why}).", [a.Id]);
            }
            else if (s.Loyalty >= r.Restless + 10) s.Restless = false;
            s.DisloyalDays = s.Loyalty < r.Desert ? s.DisloyalDays + 1 : 0;
            if (s.DisloyalDays >= r.DesertDays && town.Rng.Chance(r.LeaveChance)) Leave(town, a);
        }
        Reckon(town);
        if (auto) Policy(town);
    }

    /// <summary>Stipends from the treasury, rent from the land, and lands lost when the site is.</summary>
    private static void Honours(Town town, Agent a)
    {
        var r = Rules(town);
        var s = a.Hero!;
        if (s.FiefId != null && Quests.Site(town, s.FiefId) is SiteState fief)
        {
            if (fief.Cleared && fief.Held)
            {
                a.Gold += r.Fief.Rent;
                s.Pay += r.Fief.Rent;
            }
            else
            {
                s.FiefId = null;
                Sway(a, -r.Fief.Loyalty);
                town.Log(LedgerCategory.War, Importance.Notable, $"Ser {a.Name} has lost {fief.Def.Name}, and with it their lands.", [a.Id]);
            }
        }
        double stipend = (s.Knight && s.FiefId == null ? r.Knight.Stipend : 0) + (s.Champion ? r.Champion.Stipend : 0);
        if (stipend <= 0) return;
        if (town.Treasury < stipend)
        {
            Sway(a, -2);
            return;
        }
        town.Book(TreasuryAccount.Honours, -stipend);
        a.Gold += stipend;
        s.Pay += stipend;
    }

    /// <summary>The town learns what became of its turncoats when the menace they served is broken.</summary>
    private static void Reckon(Town town)
    {
        foreach (var a in town.Agents)
        {
            if (a.Hero is not { TurnedTo: int id } s || a.Dead) continue;
            var m = town.Saga.Menaces[id - 1];
            if (m.Alive) continue;
            a.Dead = true;
            town.Log(LedgerCategory.War, Importance.Major, $"Among the dead where {m.Name} fell lies {a.Name}, who left {town.Name} to serve it.", [a.Id]);
        }
    }

    // Leaving -------------------------------------------------------------------------------------

    private static bool Dark(Town town, Agent a) =>
        a.Has(Trait.HotTempered) || a.Has(Trait.Greedy) || Faith.Of(town, a)?.Evil == true || a.Hero!.Loyalty < 10 || Notoriety.Dark(town, a.Hero);

    /// <summary>The menace a turncoat would run to: the strongest one abroad that has risen.</summary>
    private static Menace? Patron(Town town) =>
        Menaces.Alive(town)
            .Where(m => m.Origin != Menaces.FromWarband && m.Stage >= MenaceStage.Rising)
            .OrderByDescending(m => m.Power)
            .ThenBy(m => m.Id)
            .FirstOrDefault();

    private static void Leave(Town town, Agent a)
    {
        var r = Rules(town);
        var s = a.Hero!;
        var faction = Factions.Of(town, a).FirstOrDefault(f => f.Secret || f.Standing <= -30);
        var menace = Patron(town);
        string name = Styled(town, a);
        if (faction != null)
        {
            double theft = Math.Floor(Math.Min(r.TheftCap, Math.Max(0, town.Treasury) * r.TheftShare));
            if (theft > 0) town.Book(TreasuryAccount.Discrepancies, -theft);
            Factions.AddCoffers(town, faction, theft);
            faction.Heat = Math.Min(100, faction.Heat + 15);
            s.DefectedTo = faction.Id;
            string took = theft > 0 ? $", and {Js.Str(theft)} gold goes missing from the treasury the same night" : "";
            town.Log(
                LedgerCategory.Faction,
                Importance.Major,
                faction.Known
                    ? $"{name} throws in with {faction.Name} and is gone from the taverns{took}."
                    : $"{name} vanishes from {town.Name}{took}. Nobody saw who helped them.",
                [a.Id]);
            Notoriety.Betrayed(town, a);
        }
        else if (menace != null && Dark(town, a))
        {
            Menaces.Feed(town, menace, s.Level * r.TurncoatPower);
            s.TurnedTo = menace.Id;
            LoseLands(town, a);
            town.Log(LedgerCategory.War, Importance.Major, $"{name} rides out of {town.Name} and does not come back. Travellers swear they saw them at {menace.SeatName}, serving {menace.Name}.", [a.Id]);
            Notoriety.Betrayed(town, a);
        }
        else
        {
            town.Log(LedgerCategory.Departure, Importance.Major, $"{name} slips out of {town.Name} before dawn with {Js.Str(Js.Round(a.Gold))} gold and all their gear, saying the town never did right by them.", [a.Id]);
            Notoriety.Deserted(town, a);
        }
        Strip(a);
        Population.Depart(town, a);
    }

    /// <summary>The mayor sends a hero packing: they leave with their gear and lose any honours.</summary>
    internal static void Dismiss(Town town, Agent a)
    {
        town.Log(LedgerCategory.Departure, Importance.Major, $"{Styled(town, a)} is told to leave {town.Name}, and rides out with their gear and a grudge.", [a.Id]);
        LoseLands(town, a);
        Strip(a);
        Population.Depart(town, a);
        Stories.Remember(town, "hero-dismissed");
    }

    /// <summary>A turncoat's land is lost: the gates are opened to whatever comes.</summary>
    private static void LoseLands(Town town, Agent a)
    {
        var s = a.Hero!;
        if (s.FiefId == null || Quests.Site(town, s.FiefId) is not SiteState site) return;
        site.Held = false;
        site.ResettleTick = town.Tick + SimTime.HoursPerDay;
        s.FiefId = null;
    }

    internal static void Strip(Agent a)
    {
        var s = a.Hero!;
        s.Knight = false;
        s.Champion = false;
        s.FiefId = null;
    }

    /// <summary>
    /// The siege: heroes at home who have given up on the town may open a gate or slip away at the worst
    /// moment. Returns the strength the defence loses, and writes it into the battle's lines.
    /// </summary>
    public static double Betray(Town town, string foe, Menace? m, List<string> lines)
    {
        var r = Rules(town);
        var d = town.Content.Enemy.Defense;
        double lost = 0;
        int opened = 0;
        string[] gates = ["postern", "river", "west"];
        var traitors = town.Heroes
            .Where(a => !a.IsAway && a.Hero!.Loyalty < r.Betray)
            .OrderBy(a => a.Hero!.Loyalty)
            .ThenBy(a => a.Id)
            .ToList();
        foreach (var a in traitors)
        {
            var s = a.Hero!;
            bool sworn = m?.FactionId != null && Factions.Of(town, a).Any(f => f.Id == m.FactionId);
            if (!town.Rng.Chance(r.BetrayChance + (sworn ? 0.3 : 0))) continue;
            lost += s.Level * d.HeroLevel * 2 * Threat.Fortification(town);
            string name = Styled(town, a);
            string line = $"{name} unbars the {gates[opened++ % gates.Length]} gate for {foe} and is gone into the dark.";
            lines.Add(line);
            town.Log(LedgerCategory.War, Importance.Major, $"Treachery at the walls: {line}", [a.Id]);
            Notoriety.Betrayed(town, a);
            if (m != null && m.Origin != Menaces.FromWarband)
            {
                s.TurnedTo = m.Id;
                Menaces.Feed(town, m, s.Level * r.TurncoatPower);
            }
            LoseLands(town, a);
            Strip(a);
            Population.Depart(town, a);
        }
        return lost;
    }

    /// <summary>Extra strength at the walls from the knights and the champion at home.</summary>
    public static double Defense(Town town)
    {
        var r = Rules(town);
        double d = 0;
        foreach (var a in town.Heroes)
        {
            if (a.IsAway) continue;
            if (a.Hero!.Knight) d += r.Knight.Defense;
            if (a.Hero.Champion) d += r.Champion.Defense;
        }
        return d;
    }

    /// <summary>The knight whose land a site is, if any.</summary>
    public static Agent? Lord(Town town, string siteId) => town.Heroes.FirstOrDefault(a => a.Hero!.FiefId == siteId);

    // Honours: the mayor's and the council's levers ------------------------------------------------

    public static string? KnightBlocked(Town town, Agent a)
    {
        var r = Rules(town).Knight;
        var s = a.Hero;
        if (s == null || a.Departed) return "only a hero in town can be knighted";
        if (s.Knight) return "already a knight";
        if (s.Level < r.Level) return $"needs level {r.Level}";
        if (s.Renown < r.Renown) return $"needs renown {r.Renown}";
        if (Notoriety.TooInfamousToKnight(town, s)) return "too notorious for the council to knight";
        if (town.Heroes.Count(h => h.Hero!.Knight) >= r.Max) return $"the town can keep {r.Max} knights";
        if (town.Treasury < r.Cost) return $"needs {Js.Str(r.Cost)} gold";
        return null;
    }

    /// <summary>The hero the council would put forward for knighthood: the most renowned who qualifies.</summary>
    public static Agent? Candidate(Town town) =>
        town.Heroes
            .Where(a => KnightBlocked(town, a) == null && a.Hero!.Loyalty >= Rules(town).Restless)
            .OrderByDescending(a => a.Hero!.Renown)
            .ThenBy(a => a.Id)
            .FirstOrDefault();

    /// <summary>The day the town last knighted anyone, or null if it never has.</summary>
    public static int? LastKnighting(Town town) => town.Agents.Select(a => a.Hero?.KnightedDay).Where(d => d != null).DefaultIfEmpty(null).Max();

    public static bool Knight(Town town, Agent a)
    {
        var r = Rules(town).Knight;
        if (KnightBlocked(town, a) != null) return false;
        var s = a.Hero!;
        town.Book(TreasuryAccount.Honours, -r.Cost);
        s.Knight = true;
        s.KnightedDay = town.Day;
        Sway(a, r.Loyalty);
        town.Mandate = Math.Min(100, town.Mandate + 2);
        town.Log(LedgerCategory.Council, Importance.Major, $"In the Town Hall the mayor lays a sword on {a.Name}'s shoulder. Rise, Ser {a.GivenName}, knight of {town.Name}.", [a.Id]);
        Notoriety.Honoured(town, a);
        return true;
    }

    /// <summary>A knighthood the council turned down stings.</summary>
    public static void Slighted(Town town, Agent a)
    {
        Sway(a, -Rules(town).Knight.Loyalty / 2);
        town.Log(LedgerCategory.Quest, Importance.Notable, $"{a.Name} hears the council would not knight them, and takes it badly.", [a.Id]);
    }

    public static string? FiefBlocked(Town town, Agent a, SiteState site)
    {
        var s = a.Hero;
        if (s == null || a.Departed || !s.Knight) return "only a knight can hold lands";
        if (s.FiefId != null) return "already holds lands";
        if (!site.Cleared || !site.ThoughtCleared) return "only a cleared site can be granted";
        if (site.Def.Id == town.Content.Enemy.Outpost.Id) return "not the warband's camp";
        if (Undercity.IsUnder(town, site)) return "not under the town";
        if (Lord(town, site.Def.Id) != null) return "already another knight's";
        if (town.Treasury < Rules(town).Fief.Cost) return $"needs {Js.Str(Rules(town).Fief.Cost)} gold";
        return null;
    }

    /// <summary>Grant a knight a cleared site as their land: their retainers hold it, and the town stops paying any garrison there.</summary>
    public static bool Grant(Town town, Agent a, string siteId)
    {
        var r = Rules(town).Fief;
        if (Quests.Site(town, siteId) is not SiteState site || FiefBlocked(town, a, site) != null) return false;
        if (Stewards.Intercept(town, "honours", new StewardAsk { Area = "honours", Kind = "fief", Title = $"Grant {site.Def.Name} to Ser {a.GivenName} · {Js.Str(r.Cost)}g", Why = "• A loyal knight without lands\n• Lands bind a knight to the town", S1 = siteId, I1 = a.Id })) return false;
        town.Book(TreasuryAccount.Honours, -r.Cost);
        a.Hero!.FiefId = siteId;
        bool garrisoned = site.Held;
        site.Held = true;
        site.ResettleTick = null;
        Sway(a, r.Loyalty);
        town.Log(
            LedgerCategory.Council,
            Importance.Major,
            $"The mayor grants {site.Def.Name} to Ser {a.Name} and their heirs. {(garrisoned ? "The watch's garrison marches home; the knight's own retainers hold it now" : "The knight's retainers ride out to hold it")}.",
            [a.Id]);
        return true;
    }

    public static string? ChampionBlocked(Town town, Agent a)
    {
        var s = a.Hero;
        if (s == null || a.Departed) return "only a hero in town can be champion";
        if (s.Champion) return "already the champion";
        if (s.Level < Rules(town).Champion.Level) return $"needs level {Rules(town).Champion.Level}";
        return null;
    }

    /// <summary>Name the town's champion; the one before loses the title, and minds.</summary>
    public static bool NameChampion(Town town, Agent a)
    {
        var r = Rules(town).Champion;
        if (ChampionBlocked(town, a) != null) return false;
        if (Stewards.Intercept(town, "honours", new StewardAsk { Area = "honours", Kind = "champion", Title = $"Name {a.Name} champion", Why = "• The town has no champion\n• The best-loved hero in town", I1 = a.Id })) return false;
        var old = town.Heroes.FirstOrDefault(h => h.Hero!.Champion);
        if (old != null)
        {
            old.Hero!.Champion = false;
            Sway(old, -r.Loyalty);
        }
        a.Hero!.Champion = true;
        Sway(a, r.Loyalty);
        town.Log(
            LedgerCategory.Council,
            Importance.Major,
            $"{Styled(town, a)} is named champion of {town.Name}{(old != null ? $", in place of {old.Name}" : "")}. They will stand first on the walls.",
            old != null ? [a.Id, old.Id] : [a.Id]);
        Notoriety.Honoured(town, a);
        return true;
    }

    /// <summary>A purse from the treasury, to win back a hero or thank one.</summary>
    public static bool Reward(Town town, Agent a, double gold)
    {
        var r = Rules(town).Reward;
        var s = a.Hero;
        if (s == null || a.Departed || gold <= 0 || town.Treasury < gold) return false;
        if (Stewards.Intercept(town, "honours", new StewardAsk { Area = "honours", Kind = "reward", Title = $"Purse for {a.Name} · {Js.Str(gold)}g", Why = $"• Loyalty {Math.Round(s.Loyalty)}: restless\n• A purse keeps them from drifting off", I1 = a.Id, D1 = gold })) return false;
        town.Book(TreasuryAccount.Honours, -gold);
        a.Gold += gold;
        s.Pay += gold;
        s.LastRewardDay = town.Day;
        Sway(a, Math.Min(r.MaxLoyalty, gold * r.LoyaltyPerGold / s.Level));
        town.Log(LedgerCategory.Quest, Importance.Notable, $"The mayor sends {Styled(town, a)} a purse of {Js.Str(gold)} gold with the town's thanks.", [a.Id]);
        return true;
    }

    /// <summary>An honour the mayor could give now, for the alert strip: lands for a loyal knight, or a champion for a town without one.</summary>
    public sealed record Offer(string Key, string Title, string Tip, int? AgentId = null, string? SiteId = null);

    /// <summary>Lands for the most loyal landless knight, and a champion when the town has none: what the stand-in mayor would give, put to the player.</summary>
    public static IEnumerable<Offer> Honours(Town town)
    {
        var r = Rules(town);
        var knight = town.Heroes.Where(a => a.Hero!.Knight && a.Hero.FiefId == null && a.Hero.Loyalty >= 60).OrderByDescending(a => a.Hero!.Loyalty).ThenBy(a => a.Id).FirstOrDefault();
        var site = knight == null ? null : FiefFor(town, knight);
        if (knight != null && site != null)
            yield return new($"fief:{knight.Id}", $"Lands for Ser {knight.GivenName}",
                $"Ser {knight.Name} has served loyally and holds no land. {site.Def.Name} is cleared; grant it for {Js.Str(r.Fief.Cost)}g and their retainers hold it while they take its rents.", SiteId: site.Def.Id);
        if (!town.Heroes.Any(a => a.Hero!.Champion) && ChampionFor(town) is { } best)
            yield return new($"champion:{best.Id}", "Name a champion",
                $"{town.Name} has no champion. {Styled(town, best)}, level {best.Hero!.Level}, would stand first on the walls; name them from their sheet.", AgentId: best.Id);
    }

    private static SiteState? FiefFor(Town town, Agent knight) => town.Sites
        .Where(x => FiefBlocked(town, knight, x) == null)
        .OrderByDescending(x => x.Held)
        .ThenBy(x => x.Def.Distance)
        .ThenBy(x => x.Def.Id, StringComparer.Ordinal)
        .FirstOrDefault();

    private static Agent? ChampionFor(Town town) => town.Heroes
        .Where(a => ChampionBlocked(town, a) == null && a.Hero!.Loyalty >= 50)
        .OrderByDescending(a => a.Hero!.Level)
        .ThenByDescending(a => a.Hero!.Renown)
        .ThenBy(a => a.Id)
        .FirstOrDefault();

    /// <summary>Stand-in mayor: purses for the restless, lands for loyal knights, and a champion once one has earned it.</summary>
    private static void Policy(Town town)
    {
        var r = Rules(town);
        foreach (var a in town.Heroes)
        {
            var s = a.Hero!;
            if (s.AwayUntil != null || s.Loyalty >= r.Restless + 5 || town.Day - s.LastRewardDay < r.Reward.GapDays) continue;
            double gold = Js.Round(r.Reward.PerLevel * s.Level);
            if (town.Treasury >= r.Reward.Reserve + gold) Reward(town, a, gold);
        }
        if (town.Treasury >= r.Fief.Cost + 400)
        {
            var knight = town.Heroes.FirstOrDefault(a => a.Hero!.Knight && a.Hero.FiefId == null && a.Hero.Loyalty >= 60);
            var site = knight == null ? null : FiefFor(town, knight);
            if (knight != null && site != null) Grant(town, knight, site.Def.Id);
        }
        if (!town.Heroes.Any(a => a.Hero!.Champion))
        {
            if (ChampionFor(town) is { } best) NameChampion(town, best);
        }
    }
}
