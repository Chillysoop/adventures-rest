namespace CrownAndCompass.Sim.Systems;

/// <summary>Who a faction wants: points per trait, ability modifier, class, profession, faith and circumstance.</summary>
public sealed record FactionRecruit
{
    public IReadOnlyDictionary<string, double> Traits { get; init; } = new Dictionary<string, double>();

    /// <summary>Points per point of positive ability modifier.</summary>
    public IReadOnlyDictionary<string, double> Abilities { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, double> Classes { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, double> Professions { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, double> Faiths { get; init; } = new Dictionary<string, double>();

    /// <summary>Points for a near-empty purse, a bad week, a friend in the grave, a business, a hero's sheet.</summary>
    public double Poor { get; init; }
    public double Miserable { get; init; }
    public double Bereaved { get; init; }
    public double Owners { get; init; }
    public double Heroes { get; init; }
}

/// <summary>How one faction behaves in town; its name and lore come from the library entry it refers to.</summary>
public sealed record FactionDef
{
    public required string Ref { get; init; }

    /// <summary>Residents the town needs before the faction takes root.</summary>
    public int Population { get; init; }

    /// <summary>Willing townsfolk it needs to found a chapter.</summary>
    public int Founders { get; init; } = 2;
    public double MinScore { get; init; } = 3;
    public FactionRecruit Recruit { get; init; } = new();

    /// <summary>What it does with its days, by weight.</summary>
    public IReadOnlyDictionary<string, double> Actions { get; init; } = new Dictionary<string, double>();

    /// <summary>The name of its hall once it builds one; empty for factions that never show their faces.</summary>
    public string Hall { get; init; } = "";

    /// <summary>The Ledger line when it takes root: {names}, {den}, {patron}, {name}, {town}.</summary>
    public string Founded { get; init; } = "";

    /// <summary>Shares its takings out among its members, keeping only a small stash in the den (<see cref="FactionRules.Stash"/>).</summary>
    public bool Shares { get; init; }

    /// <summary>Survivors of a crackdown strike back at the town for a while (<see cref="FactionRules.Vendetta"/>).</summary>
    public bool Vendetta { get; init; }
}

/// <summary>What a guild keeps in its den before the rest is shared out: Base plus PerMember for each member.</summary>
public sealed record StashRules
{
    public double Base { get; init; } = 60;
    public double PerMember { get; init; } = 15;

    /// <summary>Shares the leader takes for each one a member gets.</summary>
    public double LeaderShares { get; init; } = 3;

    /// <summary>The share of an exiled member's purse the watch takes with them in a crackdown.</summary>
    public double Seize { get; init; } = 0.75;
}

/// <summary>How a guild that survives a crackdown gets its own back.</summary>
public sealed record VendettaRules
{
    public int Days { get; init; } = 45;
    public double Chance { get; init; } = 0.2;

    /// <summary>A bribed clerk's take from the treasury: Share of it, at most Cap.</summary>
    public double Share { get; init; } = 0.03;
    public double Cap { get; init; } = 300;

    /// <summary>Gold of damage when the guild burns a civic building's stores, from Fire[0] to Fire[1].</summary>
    public int[] Fire { get; init; } = [40, 120];

    /// <summary>Mandate the mayor loses when their door is marked.</summary>
    public double Threat { get; init; } = 3;
}

/// <summary>Numbers shared by every faction (content/game/factions.json).</summary>
public sealed record FactionRules
{
    public int MaxPerAgent { get; init; } = 2;
    /// <summary>Daily dues, as a percentage of a member's purse, up to MaxDues gold.</summary>
    public double Dues { get; init; } = 1;
    public double MaxDues { get; init; } = 2;
    public double RecruitChance { get; init; } = 0.12;
    public double ActionChance { get; init; } = 0.55;
    public double HeatDecay { get; init; } = 1.5;
    public double ExposeAt { get; init; } = 60;
    public double CrackdownCost { get; init; } = 40;
    public double CrackdownMandate { get; init; } = 2;
    public int DormantDays { get; init; } = 30;
    public int QuietDays { get; init; } = 20;

    /// <summary>Share of their purse founders put into a new faction's coffers.</summary>
    public double FounderGift { get; init; } = 0.15;

    /// <summary>Days before an agenda item can be repeated, by kind (protest, graves...).</summary>
    public IReadOnlyDictionary<string, int> Cooldowns { get; init; } = new Dictionary<string, int>();
    public double SupportPerStanding { get; init; } = 4;
    public string HallBuilding { get; init; } = "guildhall";
    public double HallReserve { get; init; } = 20;
    public double TheftDc { get; init; } = 12;
    public double TheftDcPerWatch { get; init; } = 10;
    public double[] TheftShare { get; init; } = [0.15, 0.35];
    public double TheftCap { get; init; } = 60;

    /// <summary>Thieves only bother with a till holding at least this much.</summary>
    public double TheftMinCash { get; init; } = 60;
    public double Fine { get; init; } = 20;
    public int CaughtToExile { get; init; } = 2;
    public int[] Racket { get; init; } = [3, 8];
    public IReadOnlyDictionary<string, double> Smuggle { get; init; } = new Dictionary<string, double>();
    public double SmuggleTake { get; init; } = 8;
    public double CaravanCost { get; init; } = 30;
    public IReadOnlyDictionary<string, double> Caravan { get; init; } = new Dictionary<string, double>();
    public int VigilanceDays { get; init; } = 8;
    public double VigilanceCost { get; init; } = 15;
    public double VigilanceDc { get; init; } = 4;
    public double ContractMin { get; init; } = 80;
    public int HeroCap { get; init; } = 12;
    public double RecruitHeroCost { get; init; } = 40;
    public double DefensePerWarden { get; init; } = 6;
    public double WardenWatch { get; init; } = 0.15;
    public double CorruptWatch { get; init; } = 0.6;
    public double BlessFood { get; init; } = 25;
    public int ProtestCamps { get; init; } = 4;
    public double RiteFavor { get; init; } = 8;
    public double RiteHeat { get; init; } = 6;
    public double GraveHeat { get; init; } = 10;
    public double WakenHeat { get; init; } = 30;
    public int WhisperReliability { get; init; } = 70;
    public SiteDef? CryptSite { get; init; }
    public StashRules Stash { get; init; } = new();
    public VendettaRules Vendetta { get; init; } = new();
    public IReadOnlyList<FactionDef> Factions { get; init; } = [];
}

/// <summary>One faction as it stands in town.</summary>
public sealed class FactionState(FactionDef def, LibFaction lore)
{
    public FactionDef Def { get; } = def;
    public LibFaction Lore { get; } = lore;
    public string Id => Def.Ref;
    public string Name => Lore.Name.StartsWith("The ", StringComparison.Ordinal) ? "t" + Lore.Name[1..] : Lore.Name;
    public bool Secret => Lore.Secret;

    /// <summary>"the Wardens of the Dawn Gate" take a plural verb; "the Quiet Hand" a singular one.</summary>
    public bool Plural
    {
        get
        {
            string head = Lore.Name.Split(" of ")[0];
            return head.EndsWith('s');
        }
    }

    /// <summary>The verb that agrees with the name: Verb("spends", "spend").</summary>
    public string Verb(string singular, string plural) => Plural ? plural : singular;

    /// <summary>Has a chapter in town.</summary>
    public bool Active { get; set; }
    public int? FoundedTick { get; set; }

    /// <summary>Members by agent id, in the order they joined; the first is the leader.</summary>
    public List<int> Members { get; } = [];
    public int? LeaderId => Members.Count > 0 ? Members[0] : null;

    /// <summary>Gold held before it has a hall; once it has one, the hall's coffers.</summary>
    public double Purse { get; set; }
    public int? HallId { get; set; }

    /// <summary>The tavern whose cellar a secret faction meets in.</summary>
    public int? DenId { get; set; }

    /// <summary>How much the watch and the town suspect it, 0 to 100; a secret faction is exposed past the line.</summary>
    public double Heat { get; set; }

    /// <summary>The town knows who its members are (always, for open factions).</summary>
    public bool Known { get; set; }

    /// <summary>Its relationship with the town hall, -100 to 100.</summary>
    public double Standing { get; set; }

    public int VigilanceUntil { get; set; }
    public int Thefts { get; set; }
    public int? LastTheftDay { get; set; }

    /// <summary>Times each member has been caught.</summary>
    public OrderedMap<int, int> Caught { get; } = new();

    /// <summary>Day before which it can't take root again after being broken.</summary>
    public int DormantUntil { get; set; }

    /// <summary>Day before which it lies low after a raid, doing nothing.</summary>
    public int QuietUntil { get; set; }

    /// <summary>Owners who refused the racket once and had their windows smashed for it; they pay after that.</summary>
    public HashSet<int> Refused { get; } = [];

    /// <summary>Day each agenda item can next be done, for those that shouldn't repeat every few days.</summary>
    public OrderedMap<string, int> Cooldowns { get; } = new();

    public string? CryptId { get; set; }
    public bool CryptBroken { get; set; }

    /// <summary>Day until which the guild strikes back at the town for a crackdown.</summary>
    public int VendettaUntil { get; set; }
}

/// <summary>
/// Campaign factions (GDD rules C10 and C11: the town's two arms of law, and an underworld that
/// grows with it). Each faction takes root once the town is big enough and enough townsfolk fit it:
/// a thieves' guild among the greedy, the poor and the rogues; a merchants' guild among the shop
/// owners; an adventurers' lodge among the heroes; an arcane order among the casters; a knightly
/// order among the paladins and the lawful faithful; a druid circle among the wild-folk; and a death
/// cult among the miserable and the bereaved. Members pay dues and recruit; each day a faction may act
/// on its agenda (burgle, run a racket, smuggle, sell a tip, send a caravan, post a reward, fund a
/// contract, call a hero, sponsor a scrying, patrol, hunt cults, bless the fields, protest, hold a
/// rite, rob graves). Secret factions build heat until the watch exposes them; then the mayor can
/// crack down, and open factions can be backed with treasury gold.
/// </summary>
public static class Factions
{
    private static FactionRules Rules(Town town) => town.Content.Factions;

    public static void InitFactions(Town town)
    {
        foreach (var def in Rules(town).Factions)
        {
            var lore = town.Content.Library.Faction(def.Ref) ?? throw new KeyNotFoundException($"Faction {def.Ref} is not in the content library");
            town.Factions.Add(new FactionState(def, lore) { Known = !lore.Secret });
        }
    }

    public static FactionState? Find(Town town, string id) => town.Factions.FirstOrDefault(f => f.Id == id);

    public static List<FactionState> Of(Town town, Agent a) => town.Factions.Where(f => f.Active && f.Members.Contains(a.Id)).ToList();

    public static double Coffers(Town town, FactionState f) => f.HallId != null ? town.Building(f.HallId.Value).Cash : f.Purse;

    internal static void AddCoffers(Town town, FactionState f, double amount)
    {
        if (f.HallId != null) town.Building(f.HallId.Value).Cash += amount;
        else f.Purse += amount;
    }

    private static Building? Hall(Town town, FactionState f) => f.HallId != null ? town.Building(f.HallId.Value) : null;

    /// <summary>Extra defence at the walls from the knights in town.</summary>
    public static double Defense(Town town)
    {
        var wardens = Find(town, "dawn-wardens");
        if (wardens == null || !wardens.Active) return 0;
        return wardens.Members.Count(id => !town.Agent(id).IsAway) * Rules(town).DefensePerWarden;
    }

    /// <summary>The watch's strength multiplier: knights help, a bribed captain hurts, a law god's justice helps.</summary>
    public static double WatchFactor(Town town)
    {
        var r = Rules(town);
        double f = 1 + Faith.WatchBonus(town);
        var wardens = Find(town, "dawn-wardens");
        if (wardens is { Active: true }) f += r.WardenWatch;
        var hand = Find(town, "quiet-hand");
        var captain = Council.FindSeat(town, SeatId.Captain)?.HolderId;
        if (hand is { Active: true } && captain != null && hand.Members.Contains(captain.Value)) f *= r.CorruptWatch;
        return f;
    }

    // The day -------------------------------------------------------------------------------------

    public static void Daily(Town town, bool auto)
    {
        foreach (var f in town.Factions)
        {
            Prune(town, f);
            if (!f.Active)
            {
                TryFound(town, f);
                continue;
            }
            Dues(town, f);
            Share(town, f);
            Recruit(town, f);
            f.Heat = Math.Max(0, f.Heat - Rules(town).HeatDecay);
            if (f.Secret && f.DenId == null && town.Taverns.Count > 0) f.DenId = town.Taverns[0].Id;
            CheckExposure(town, f);
            if (f.Active && town.Day >= f.QuietUntil && town.Rng.Chance(Rules(town).ActionChance)) Act(town, f);
            if (f.Active && town.Day < f.VendettaUntil && town.Rng.Chance(Rules(town).Vendetta.Chance)) StrikeBack(town, f);
        }
        CryptCleared(town);
        if (auto) Policy(town);
    }

    private static void Prune(Town town, FactionState f)
    {
        if (!f.Active) return;
        f.Members.RemoveAll(id => town.Agent(id).Departed);
        if (f.Members.Count > 0) return;
        f.Active = false;
        f.DormantUntil = town.Day + Rules(town).DormantDays;
        if (f.Known) town.Log(LedgerCategory.Faction, Importance.Notable, $"With {f.Verb("its", "their")} last member gone, {f.Name} {f.Verb("has", "have")} no one left in {town.Name}.");
    }

    // Membership ----------------------------------------------------------------------------------

    private static double Get(IReadOnlyDictionary<string, double> d, string key) => d.TryGetValue(key, out var v) ? v : 0;

    /// <summary>How well an agent fits a faction; below the faction's MinScore they won't join.</summary>
    public static double Score(Town town, FactionState f, Agent a)
    {
        var r = f.Def.Recruit;
        double s = 0;
        foreach (var t in a.Traits) s += Get(r.Traits, t.ToString());
        foreach (var (ab, w) in r.Abilities) s += w * Math.Max(0, Rng.Modifier(a.Abilities[ab]));
        if (a.Hero != null)
        {
            s += Get(r.Classes, a.Hero.Cls.Id) + r.Heroes;
        }
        else s += Get(r.Professions, a.Profession);
        if (!string.IsNullOrEmpty(a.Faith)) s += Get(r.Faiths, a.Faith) * (a.Devout ? 1.5 : 1);
        if (a.Gold < 10) s += r.Poor;
        if (a.MiserableDays > 0 || a.LastDayMood < 45) s += r.Miserable;
        if (r.Bereaved > 0 && Bereaved(town, a)) s += r.Bereaved;
        if (r.Owners > 0 && town.Buildings.Any(b => b.OwnerId == a.Id && b.Def.IsBusiness)) s += r.Owners;
        // The sworn of a god who hates the faction's patron want nothing to do with it.
        var god = Faith.Of(town, a);
        if (god != null && a.Devout && god.Rivals.Contains(f.Lore.Patron)) s -= 4;
        return s;
    }

    /// <summary>Mourning someone: a spouse or a close friend in the grave.</summary>
    public static bool Bereaved(Town town, Agent a) => town.Agents.Any(d => d.Dead && (a.SpouseId == d.Id || a.OpinionOf(d.Id) >= 40));

    private static bool CanJoin(Town town, FactionState f, Agent a)
    {
        if (a.Departed || f.Members.Contains(a.Id)) return false;
        var mine = Of(town, a);
        if (mine.Count >= Rules(town).MaxPerAgent) return false;
        return !mine.Any(m => m.Lore.Rivals.Contains(f.Id) || f.Lore.Rivals.Contains(m.Id));
    }

    private static List<(Agent Agent, double Score)> Candidates(Town town, FactionState f, double min) =>
        town.Residents
            .Where(a => CanJoin(town, f, a))
            .Select(a => (Agent: a, Score: Score(town, f, a)))
            .Where(x => x.Score >= min)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Agent.Id)
            .ToList();

    private static void TryFound(Town town, FactionState f)
    {
        if (town.Population < f.Def.Population || town.Day < f.DormantUntil) return;
        var pool = Candidates(town, f, f.Def.MinScore);
        if (pool.Count < f.Def.Founders) return;
        var founders = pool.Take(f.Def.Founders).Select(x => x.Agent).ToList();
        f.Active = true;
        f.FoundedTick = town.Tick;
        f.Heat = 0;
        f.Known = !f.Secret;
        f.Members.Clear();
        foreach (var a in founders)
        {
            Join(town, f, a, false);
            double gift = Math.Floor(a.Gold * Rules(town).FounderGift);
            a.Gold -= gift;
            f.Purse += gift;
        }
        var den = town.Taverns.FirstOrDefault();
        f.DenId = f.Secret ? den?.Id : null;
        var patron = Faith.God(town, f.Lore.Patron);
        string names = Expedition.ListNames(founders.Select(a => a.Name).ToList());
        string text = f.Def.Founded
            .Replace("{names}", names, StringComparison.Ordinal)
            .Replace("{den}", den?.Name ?? "an empty barn", StringComparison.Ordinal)
            .Replace("{patron}", patron != null ? Faith.Prose(patron) : "their god", StringComparison.Ordinal)
            .Replace("{name}", f.Name, StringComparison.Ordinal)
            .Replace("{town}", town.Name, StringComparison.Ordinal);
        if (text == "") text = $"{names} found a chapter of {f.Name} in {town.Name}.";
        // A secret faction's founding is only a hint; the town doesn't learn who until it is exposed.
        town.Log(LedgerCategory.Faction, Importance.Major, Cap(text), f.Secret ? [] : founders.Select(a => a.Id).ToList());
    }

    private static void Join(Town town, FactionState f, Agent a, bool log = true)
    {
        foreach (int id in f.Members)
        {
            a.ChangeOpinion(id, 8);
            town.Agent(id).ChangeOpinion(a.Id, 8);
        }
        f.Members.Add(a.Id);
        // The cult's chosen turn from the gods of light.
        if (f.Lore.Patron != "" && f.Lore.Kind == "cult")
        {
            a.Faith = f.Lore.Patron;
            a.Devout = true;
        }
        if (log && f.Known)
            town.Log(LedgerCategory.Faction, Importance.Routine, $"{a.Name} joins {f.Name}.", [a.Id]);
    }

    private static void Dues(Town town, FactionState f)
    {
        var r = Rules(town);
        foreach (int id in f.Members)
        {
            var a = town.Agent(id);
            if (a.Gold < 30) continue;
            double dues = Math.Min(r.MaxDues, Math.Floor(a.Gold * r.Dues) / 100);
            a.Gold -= dues;
            AddCoffers(town, f, dues);
        }
    }

    /// <summary>A guild that shares its takings keeps a small stash in the den and splits the rest among its members, so no raid finds a fortune.</summary>
    private static void Share(Town town, FactionState f)
    {
        if (!f.Def.Shares || f.Members.Count == 0) return;
        var stash = Rules(town).Stash;
        double excess = Coffers(town, f) - (stash.Base + stash.PerMember * f.Members.Count);
        // The leader takes LeaderShares to everyone else's one.
        double each = Math.Floor(excess / (f.Members.Count - 1 + stash.LeaderShares));
        if (each < 1) return;
        foreach (int id in f.Members) town.Agent(id).Gold += id == f.LeaderId ? each * stash.LeaderShares : each;
        AddCoffers(town, f, -each * (f.Members.Count - 1 + stash.LeaderShares));
    }

    private static void Recruit(Town town, FactionState f)
    {
        if (!town.Rng.Chance(Rules(town).RecruitChance)) return;
        var pick = Candidates(town, f, f.Def.MinScore + 1).FirstOrDefault();
        if (pick.Agent != null) Join(town, f, pick.Agent);
    }

    // Heat and exposure ---------------------------------------------------------------------------

    internal static void CheckExposure(Town town, FactionState f)
    {
        if (!f.Secret || f.Known || f.Members.Count == 0 || f.Heat < Rules(town).ExposeAt) return;
        f.Known = true;
        var leader = town.Agent(f.LeaderId!.Value);
        town.Log(
            LedgerCategory.Faction,
            Importance.Major,
            $"The watch has names at last: {(f.Members.Count == 1 ? "one townsperson belongs" : $"{f.Members.Count} townsfolk belong")} to {f.Name}, and {leader.Name} leads them." +
            (f.DenId != null ? $" They meet in the cellar of {town.Building(f.DenId.Value).Name}." : ""),
            f.Members.ToList());
    }

    /// <summary>
    /// A god's servants speak against the secret faction it hates most (by patron), adding heat.
    /// Returns the faction, or null when there is none to denounce.
    /// </summary>
    public static FactionState? Denounce(Town town, LibDeity god, double heat, string? patron = null)
    {
        var target = town.Factions
            .Where(f => f.Active && f.Secret && !f.Known && (patron != null ? f.Lore.Patron == patron : god.Rivals.Contains(f.Lore.Patron)))
            .OrderByDescending(f => f.Heat)
            .ThenBy(f => f.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (target == null) return null;
        target.Heat = Math.Min(100, target.Heat + heat);
        CheckExposure(town, target);
        return target;
    }

    // Agendas -------------------------------------------------------------------------------------

    private static void Act(Town town, FactionState f)
    {
        if (f.Def.Hall != "" && f.HallId == null && BuildHall(town, f)) return;
        var actions = f.Def.Actions.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        double total = actions.Sum(kv => kv.Value);
        double roll = town.Rng.Next() * total;
        string kind = actions[^1].Key;
        foreach (var (k, w) in actions)
        {
            if (roll < w)
            {
                kind = k;
                break;
            }
            roll -= w;
        }
        // A guild on good terms with the hall keeps its hands off the town's purses.
        if (kind == "theft" && f.Standing >= 30) kind = "smuggle";
        if (town.Day < f.Cooldowns.Get(kind, 0)) return;
        if (Rules(town).Cooldowns.TryGetValue(kind, out int wait)) f.Cooldowns[kind] = town.Day + wait;
        switch (kind)
        {
            case "theft": Theft(town, f); break;
            case "racket": Racket(town, f); break;
            case "smuggle": Smuggle(town, f); break;
            case "whisper": Whisper(town, f); break;
            case "caravan": Caravan(town, f); break;
            case "vigilance": Vigilance(town, f); break;
            case "contract": Contract(town, f); break;
            case "recruitHero": CallHero(town, f); break;
            case "scry": Scry(town, f); break;
            case "study": Study(town, f); break;
            case "patrol": Patrol(town, f); break;
            case "hunt": Hunt(town, f); break;
            case "bless": BlessFields(town, f); break;
            case "protest": Protest(town, f); break;
            case "rite": Rite(town, f); break;
            case "graves": Graves(town, f); break;
        }
    }

    private static bool BuildHall(Town town, FactionState f)
    {
        var def = town.Content.Building(Rules(town).HallBuilding);
        double cost = def.Cost + town.Market.Quote(Good.Timber, def.Timber);
        if (f.Purse < cost + Rules(town).HallReserve) return false;
        var lot = Shrines.CivicLot(town, def);
        if (lot == null) return false;
        town.Market.Buy(Good.Timber, def.Timber);
        f.Purse -= cost;
        var hall = town.AddBuilding(def, lot, null, f.Def.Hall, false);
        hall.LaborBudget = Construction.Hours(town, def) * Brain.ConstructionWage;
        hall.Cash = f.Purse;
        f.Purse = 0;
        f.HallId = hall.Id;
        town.Log(LedgerCategory.Faction, Importance.Notable, $"{Cap(f.Name)} {f.Verb("spends", "spend")} {Js.Str(Js.Round(cost))} gold of {f.Verb("its", "their")} dues to break ground on {hall.Name}.", f.Members.ToList());
        return true;
    }

    /// <summary>The member best at a job: highest ability modifier, plus level for heroes, then lowest id.</summary>
    private static Agent? Best(Town town, FactionState f, string ability) =>
        f.Members
            .Select(town.Agent)
            .Where(a => !a.IsAway)
            .OrderByDescending(a => Rng.Modifier(a.Abilities[ability]) + (a.Hero?.Level ?? 0))
            .ThenBy(a => a.Id)
            .FirstOrDefault();

    private static bool Vigilant(Town town) => town.Factions.Any(x => x.Active && x.VigilanceUntil > town.Day);

    /// <summary>
    /// What is left of a guild after a crackdown gets its own back: a bribed clerk robs the treasury, the stores of a
    /// civic building burn, or the mayor's door is marked. A stronger watch catches more of them in the act.
    /// </summary>
    private static void StrikeBack(Town town, FactionState f)
    {
        var r = Rules(town);
        var v = r.Vendetta;
        var rng = town.Rng;
        var agent = Best(town, f, Ability.Dex);
        if (agent == null) return;
        int roll = rng.D20();
        int bonus = Rng.Modifier(agent.Abilities.Dex) + (agent.Hero?.Cls.Sneak == true ? agent.Hero.Level + 2 : 0);
        double dc = r.TheftDc + Council.WatchStrength(town) * r.TheftDcPerWatch + (Vigilant(town) ? r.VigilanceDc : 0);
        var hall = Economy.Hall(town);
        int kind = rng.Range(0, 3);
        if (kind == 1 && hall == null) kind = 2;
        if (roll == 1 || roll + bonus < dc)
        {
            double fine = Math.Min(Math.Floor(Math.Max(0, agent.Gold)), r.Fine);
            agent.Gold -= fine;
            if (fine > 0) town.Book(TreasuryAccount.Fines, fine);
            f.Caught[agent.Id] = f.Caught.Get(agent.Id, 0) + 1;
            string caught = kind switch
            {
                0 => $"The watch catches {agent.Name} passing gold to a clerk of the counting-house (d20 {roll})",
                1 => $"The watch catches {agent.Name} behind {hall!.Name} at night with a tinderbox and a grudge (d20 {roll})",
                _ => $"The watch catches {agent.Name} with a knife at the mayor's door before dawn (d20 {roll})",
            };
            town.Log(
                LedgerCategory.Faction,
                Importance.Notable,
                $"{caught}. {agent.GivenName} is fined {Js.Str(fine)} gold and swears {f.Name} will not forget the raid.",
                [agent.Id]);
            return;
        }
        if (kind == 0 && town.Treasury > 0)
        {
            double take = Math.Floor(Math.Min(v.Cap, town.Treasury * v.Share));
            if (take < 1) return;
            town.Book(TreasuryAccount.Discrepancies, -take);
            agent.Gold += take;
            town.Log(LedgerCategory.Faction, Importance.Notable, $"A clerk in the counting-house is found to have been paid by {f.Name}. {Js.Str(take)} gold is missing from the treasury, and the guild sends word that this is for the raid.", [agent.Id]);
        }
        else if (kind == 1)
        {
            double damage = Math.Min(Math.Max(0, town.Treasury), rng.Range(v.Fire[0], v.Fire[1] + 1));
            town.Book(TreasuryAccount.Factions, -damage);
            town.Log(LedgerCategory.Faction, Importance.Notable, $"Fire takes the store-rooms behind {hall!.Name} in the night. Mending it costs {Js.Str(damage)} gold, and the guild's mark is chalked on the door.", [agent.Id]);
        }
        else
        {
            town.Mandate = Math.Max(0, town.Mandate - v.Threat);
            town.Log(LedgerCategory.Faction, Importance.Notable, $"The mayor wakes to find the mark of {f.Name} cut into the front door. Word of it goes round the taverns, and the council looks less sure of the mayor.", [agent.Id]);
        }
        f.Heat = Math.Min(100, f.Heat + 4);
    }

    private static void Theft(Town town, FactionState f)
    {
        var r = Rules(town);
        var rng = town.Rng;
        var thief = Best(town, f, Ability.Dex);
        if (thief == null) return;
        var target = town.Buildings
            .Where(b => b.Complete && b.Def.IsBusiness && !b.Def.Holy && b.Cash > r.TheftMinCash && (b.OwnerId == null || !f.Members.Contains(b.OwnerId.Value)))
            .OrderByDescending(b => b.Cash)
            .ThenBy(b => b.Id)
            .FirstOrDefault();
        if (target == null) return;
        int roll = rng.D20();
        int bonus = Rng.Modifier(thief.Abilities.Dex) + (thief.Hero?.Cls.Sneak == true ? thief.Hero.Level + 2 : 0);
        double dc = r.TheftDc + Council.WatchStrength(town) * r.TheftDcPerWatch + (Vigilant(town) ? r.VigilanceDc : 0);
        if (roll != 1 && roll + bonus >= dc)
        {
            double take = Math.Min(r.TheftCap, Math.Floor(target.Cash * (r.TheftShare[0] + rng.Next() * (r.TheftShare[1] - r.TheftShare[0]))));
            target.Cash -= take;
            thief.Gold += take / 2;
            AddCoffers(town, f, take / 2);
            f.Heat = Math.Min(100, f.Heat + 5);
            f.Standing -= 3;
            f.Thefts++;
            f.LastTheftDay = town.Day;
            town.Log(
                LedgerCategory.Faction,
                Importance.Notable,
                f.Known
                    ? $"{thief.Name} of {f.Name} slips into {target.Name} after dark (d20 {roll}) and leaves {Js.Str(take)} gold lighter."
                    : $"Someone gets into {target.Name} in the night and makes off with {Js.Str(take)} gold. The watch has no one to blame.",
                f.Known ? [thief.Id] : []);
            return;
        }
        if (roll != 1 && roll + bonus >= dc - 6)
        {
            town.Log(LedgerCategory.Faction, Importance.Routine, $"A shutter at {target.Name} is found forced in the morning, but nothing is missing.");
            return;
        }
        // Caught in the act.
        double fine = Math.Min(thief.Gold, r.Fine);
        thief.Gold -= fine;
        town.Book(TreasuryAccount.Fines, fine);
        f.Heat = Math.Min(100, f.Heat + 20);
        int times = f.Caught.Get(thief.Id, 0) + 1;
        f.Caught[thief.Id] = times;
        if (target.OwnerId != null) town.Agent(target.OwnerId.Value).ChangeOpinion(thief.Id, -40);
        if (times >= r.CaughtToExile)
        {
            Population.Depart(town, thief);
            town.Log(
                LedgerCategory.Faction,
                Importance.Notable,
                $"The watch catches {thief.Name} climbing out of {target.Name} (d20 {roll}), not for the first time. The council has {thief.GivenName} run out of {town.Name}, still saying nothing about who they work for.",
                [thief.Id]);
        }
        else
        {
            town.Log(
                LedgerCategory.Faction,
                Importance.Notable,
                $"The watch catches {thief.Name} climbing out of {target.Name} (d20 {roll}) and fines {thief.GivenName} {Js.ToFixed(fine, 0)} gold. {thief.GivenName} won't say who sent them.",
                [thief.Id]);
        }
        CheckExposure(town, f);
    }

    private static void Racket(Town town, FactionState f)
    {
        var r = Rules(town);
        var rng = town.Rng;
        var taverns = town.Taverns.Where(b => b.OwnerId == null || !f.Members.Contains(b.OwnerId.Value)).ToList();
        if (taverns.Count == 0) return;
        var tavern = taverns[rng.Range(0, taverns.Count)];
        var owner = tavern.OwnerId != null ? town.Agent(tavern.OwnerId.Value) : null;
        double pay = Math.Min(tavern.Cash, rng.Range(r.Racket[0], r.Racket[1] + 1));
        if (owner != null && owner.Has(Trait.HotTempered) && !f.Refused.Contains(owner.Id) && rng.Chance(0.5))
        {
            f.Refused.Add(owner.Id);
            double broken = Math.Min(tavern.Cash, pay * 2);
            tavern.Cash -= broken;
            f.Heat = Math.Min(100, f.Heat + 8);
            f.Standing -= 2;
            town.Log(LedgerCategory.Faction, Importance.Notable, $"{owner.Name} tells the men asking for protection money where to go. That night every window in {tavern.Name} is smashed ({Js.Str(Js.Round(broken))} gold of damage).", [owner.Id]);
            return;
        }
        if (pay < 1) return;
        tavern.Cash -= pay;
        AddCoffers(town, f, pay);
        f.Heat = Math.Min(100, f.Heat + 2);
        town.Log(LedgerCategory.Faction, Importance.Routine, $"The keeper of {tavern.Name} pays {Js.Str(Js.Round(pay))} gold to keep the windows whole, and doesn't say to whom.");
    }

    /// <summary>Goods brought in only where the market is short (price above base), so the guild relieves scarcity rather than undercutting the town's own makers.</summary>
    private static bool Relieve(Town town, IReadOnlyDictionary<string, double> goods)
    {
        bool any = false;
        foreach (var (name, qty) in goods.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var g = Enum.Parse<Good>(name);
            if (town.Market.Price(g) <= town.Market.BasePrice(g)) continue;
            town.Market.Seed(g, qty);
            any = true;
        }
        return any;
    }

    private static void Smuggle(Town town, FactionState f)
    {
        var r = Rules(town);
        if (!Relieve(town, r.Smuggle)) return;
        AddCoffers(town, f, r.SmuggleTake);
        f.Heat = Math.Min(100, f.Heat + 2);
        town.Log(LedgerCategory.Faction, Importance.Routine, "Barrels and crates with no excise mark turn up in the cellars overnight. What was dear is cheap this week, and nobody asks why.");
    }

    /// <summary>The thieves sell what they hear: a true word about a site the town knows little of.</summary>
    private static void Whisper(Town town, FactionState f)
    {
        var site = town.Sites
            .Where(s => !s.Cleared && s.Known != SiteKnowledge.Confirmed)
            .OrderBy(s => s.Def.Distance)
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (site == null || f.Standing < 0)
        {
            Smuggle(town, f);
            return;
        }
        string who = Rumors.Occupants(town.Content, site.Def);
        Rumors.Plant(
            town,
            site,
            "a fence",
            null,
            $"A fence who drinks with the Quiet Hand says {site.Def.Name}, {Rumors.Away(site.Def.Distance)}, is held by {who}",
            Rules(town).WhisperReliability,
            true,
            who,
            site.Def.Tier);
    }

    private static void Caravan(Town town, FactionState f)
    {
        var r = Rules(town);
        if (Coffers(town, f) < r.CaravanCost || !Relieve(town, r.Caravan)) return;
        AddCoffers(town, f, -r.CaravanCost);
        f.Standing += 1;
        town.Log(LedgerCategory.Faction, Importance.Routine, $"{Cap(f.Name)} {f.Verb("sends its", "send their")} own caravan down the river road; it comes back with what the market was short of.");
    }

    private static void Vigilance(Town town, FactionState f)
    {
        var r = Rules(town);
        var hand = Find(town, "quiet-hand");
        bool robbed = hand != null && hand.LastTheftDay != null && town.Day - hand.LastTheftDay.Value <= 10;
        if (!robbed || Coffers(town, f) < r.VigilanceCost || f.VigilanceUntil > town.Day)
        {
            Caravan(town, f);
            return;
        }
        AddCoffers(town, f, -r.VigilanceCost);
        f.VigilanceUntil = town.Day + r.VigilanceDays;
        if (hand!.Active) hand.Heat = Math.Min(100, hand.Heat + 10);
        town.Log(LedgerCategory.Faction, Importance.Notable, $"{Cap(f.Name)} {f.Verb("posts", "post")} a reward of {Js.Str(r.VigilanceCost)} gold for the thieves who have been robbing its members, and the watch doubles its night rounds.");
        if (hand.Active) CheckExposure(town, hand);
    }

    /// <summary>Fund a bounty from the hall's coffers: the lodge on the easiest job, the order on a deeper one.</summary>
    private static void Contract(Town town, FactionState f)
    {
        var r = Rules(town);
        var hall = Hall(town, f);
        if (hall == null || !hall.Complete || hall.Cash < r.ContractMin) return;
        if (town.Bounties.Any(b => b.FunderId == hall.Id && b.Status is BountyStatus.Open or BountyStatus.Taken)) return;
        bool deep = f.Lore.Kind == "arcane";
        // The mages send heroes for the best of a hoard, and pay the town in a spell; an errand goes where heroes will go.
        var errand = deep ? Errands.Pick(town) : null;
        var target = town.Sites
            .Where(s => s.Known != SiteKnowledge.Unheard && !s.ThoughtCleared && (!deep || s.BelievedTier >= 2))
            .Where(s => !Undercity.IsUnder(town, s) || Undercity.Stirring(town, s))
            .Where(s => !town.Bounties.Any(b => b.SiteId == s.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken))
            .OrderBy(s => deep && errand == null ? -s.BelievedTier : s.BelievedTier)
            .ThenBy(s => s.Def.Distance)
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (target == null) return;
        double reward = Math.Min(hall.Cash, Math.Max(r.ContractMin, Quests.SuggestedReward(Math.Max(1, target.BelievedTier)) * 0.6));
        reward = Js.Round(reward);
        var posted = Quests.PostBounty(town, target.Def.Id, reward, hall, errand != null ? "fetch" : "slay");
        if (posted == null) return;
        if (errand != null) posted.Errand = errand.Id;
        string why = errand != null ? $"for the best of its hoard, and promise{(f.Plural ? "" : "s")} the town {errand.Name} when it comes back" : deep ? "and asks only for first look at whatever relics come out" : "for any company with the nerve";
        town.Log(LedgerCategory.Faction, Importance.Notable, $"{Cap(f.Name)} {f.Verb("puts", "put")} up {Js.Str(reward)} gold from {f.Verb("its", "their")} own coffers on {target.Def.Name}, {why}.");
    }

    private static void CallHero(Town town, FactionState f)
    {
        var r = Rules(town);
        // The lodge sends for help only when a posted job is going begging.
        if (town.Heroes.Count >= Math.Max(r.HeroCap, City.HeroCap(town)) || Coffers(town, f) < r.RecruitHeroCost || town.Taverns.Count == 0) return;
        if (!town.Bounties.Any(b => b.Status == BountyStatus.Open)) return;
        var classes = town.Content.Classes;
        int roll = town.Rng.Range(0, classes.Sum(c => c.Weight));
        var cls = classes[0];
        foreach (var c in classes)
        {
            if (roll < c.Weight)
            {
                cls = c;
                break;
            }
            roll -= c.Weight;
        }
        AddCoffers(town, f, -r.RecruitHeroCost);
        var hero = Quests.CreateHero(town, cls, town.Rng.Chance(0.6) ? 1 : 2);
        Faith.Choose(town, hero);
        f.Members.Add(hero.Id);
        town.Log(LedgerCategory.Faction, Importance.Notable, $"{hero.Describe()}, arrives with a letter from another hall of {f.Name}, who paid for the road. ({hero.TraitList()})", [hero.Id]);
    }

    private static void Scry(Town town, FactionState f)
    {
        var site = town.Sites
            .Where(s => !s.Cleared && s.Known != SiteKnowledge.Unheard && Knowledge.ActiveScrying(town, s.Def.Id) == null && Watch.LiveOf(town, s) == null)
            .OrderByDescending(s => s.Def.Id == town.Content.Enemy.Outpost.Id ? 1 : 0)
            .ThenBy(s => s.Def.Distance)
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (site == null || Knowledge.Scryers(town).Count == 0) return;
        double cost = Knowledge.ScryCost(town, site.Def);
        if (Coffers(town, f) < cost) return;
        // The order pays the caster through the town's books, so the scrying is the town's to watch.
        AddCoffers(town, f, -cost);
        town.Book(TreasuryAccount.Factions, cost);
        if (Knowledge.CommissionScrying(town, site.Def.Id) == null)
        {
            town.Book(TreasuryAccount.Factions, -cost);
            AddCoffers(town, f, cost);
            return;
        }
        f.Standing += 2;
        town.Log(LedgerCategory.Faction, Importance.Routine, $"{Cap(f.Name)} {f.Verb("pays", "pay")} for the scrying and {f.Verb("keeps", "keep")} the notes.");
    }

    private static void Study(Town town, FactionState f)
    {
        if (town.Relics.Count == 0) return;
        var relic = town.Content.Library.Item(town.Relics[town.Rng.Range(0, town.Relics.Count)]);
        if (relic == null) return;
        double fee = 3 * town.Relics.Count;
        AddCoffers(town, f, fee);
        town.Log(LedgerCategory.Faction, Importance.Routine, $"Scholars of {f.Name} spend the day over {Library.A(Library.Prose(relic.Name))} and sell copies of their notes to visiting sages ({Js.Str(fee)} gold).");
    }

    private static void Patrol(Town town, FactionState f)
    {
        var knight = Best(town, f, Ability.Wis);
        if (knight == null) return;
        f.VigilanceUntil = Math.Max(f.VigilanceUntil, town.Day + Rules(town).VigilanceDays / 2);
        town.Log(LedgerCategory.Faction, Importance.Routine, $"{knight.Name} of {f.Name} walks the night streets with a lantern and a drawn sword.", [knight.Id]);
    }

    /// <summary>The knights hunt the secret faction the town most suspects.</summary>
    private static void Hunt(Town town, FactionState f)
    {
        var target = town.Factions
            .Where(x => x.Active && x.Secret && !x.Known && x.Heat >= 15 && !f.Lore.Allies.Contains(x.Id))
            .OrderByDescending(x => x.Heat)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        var hunter = Best(town, f, Ability.Wis);
        if (target == null || hunter == null)
        {
            Patrol(town, f);
            return;
        }
        int roll = town.Rng.D20();
        int bonus = Rng.Modifier(hunter.Abilities.Wis) + (hunter.Hero?.Level ?? 0);
        double dc = 15 - target.Heat / 10;
        if (roll + bonus >= dc)
        {
            target.Heat = Math.Min(100, target.Heat + 25);
            town.Log(
                LedgerCategory.Faction,
                Importance.Notable,
                $"{hunter.Name} of {f.Name} follows a hooded figure home from {(target.DenId != null ? town.Building(target.DenId.Value).Name : "the market")} (d20 {roll}) and comes back with a name.",
                [hunter.Id]);
            CheckExposure(town, target);
        }
        else town.Log(LedgerCategory.Faction, Importance.Routine, $"{hunter.Name} of {f.Name} spends the night watching a cellar door, and sees nothing.", [hunter.Id]);
    }

    private static void BlessFields(Town town, FactionState f)
    {
        if (town.Market.Price(Good.Food) <= town.Market.BasePrice(Good.Food)) return;
        town.Market.Seed(Good.Food, Rules(town).BlessFood);
        var druid = Best(town, f, Ability.Wis);
        town.Log(LedgerCategory.Faction, Importance.Routine, $"{(druid != null ? druid.Name : "A druid")} of {f.Name} walks the furrows at dusk singing to the soil. The harvest comes in heavy.", druid != null ? [druid.Id] : []);
    }

    private static void Protest(Town town, FactionState f)
    {
        var camps = town.Buildings.Where(b => b.Complete && b.Def.Id == "lumber_camp" && !b.Closed).ToList();
        if (camps.Count < Rules(town).ProtestCamps)
        {
            BlessFields(town, f);
            return;
        }
        foreach (var c in camps) c.Cash -= Math.Min(c.Cash, 5);
        f.Standing -= 3;
        town.Log(LedgerCategory.Faction, Importance.Notable, $"{Cap(f.Name)} {f.Verb("blocks", "block")} the road to the lumber camps: {camps.Count} camps, they say, is {camps.Count - Rules(town).ProtestCamps + 1} too many for the forest to forgive. The axes stand idle for a day.", f.Members.ToList());
    }

    private static void Rite(Town town, FactionState f)
    {
        var r = Rules(town);
        town.Faiths.Favor[f.Lore.Patron] = town.Faiths.Favor.Get(f.Lore.Patron, 0) + r.RiteFavor * Math.Sqrt(f.Members.Count);
        f.Heat = Math.Min(100, f.Heat + r.RiteHeat);
        string where = f.DenId != null ? town.Building(f.DenId.Value).Name : "the old mill";
        town.Log(LedgerCategory.Faction, Importance.Routine, f.Known ? $"{Cap(f.Name)} {f.Verb("sings", "sing")} in the cellar of {where} again." : $"Townsfolk near {where} swear they heard singing under the floor at midnight, slow and wordless.");
    }

    private static void Graves(Town town, FactionState f)
    {
        var r = Rules(town);
        town.Faiths.Favor[f.Lore.Patron] = town.Faiths.Favor.Get(f.Lore.Patron, 0) + r.RiteFavor / 2;
        f.Heat = Math.Min(100, f.Heat + r.GraveHeat);
        town.Log(LedgerCategory.Faction, Importance.Notable, "Two graves in the burying ground are found dug open at dawn, and the bodies are gone.");
        CheckExposure(town, f);
    }

    /// <summary>The Hollow King answers his choir: the dead walk to a crypt near town, a new site to clear. Returns the Ledger line.</summary>
    public static string? Waken(Town town)
    {
        var choir = town.Factions.FirstOrDefault(f => f.Active && f.Lore.Kind == "cult");
        var def = Rules(town).CryptSite;
        if (choir == null || def == null) return null;
        var existing = Quests.Site(town, def.Id);
        if (existing != null && !existing.Cleared) return null;
        if (existing == null)
        {
            existing = new SiteState(def);
            town.Sites.Add(existing);
        }
        else
        {
            existing.Cleared = false;
            existing.ThoughtCleared = false;
        }
        existing.Known = SiteKnowledge.Rumoured;
        existing.Believed = "the walking dead";
        existing.BelievedTier = def.Tier;
        choir.CryptId = def.Id;
        choir.CryptBroken = false;
        choir.Heat = Math.Min(100, choir.Heat + Rules(town).WakenHeat);
        CheckExposure(town, choir);
        return $"At midnight the dead climb out of {town.Name}'s burying ground and shamble west, into {def.Name}. Something under the hill is calling them, and it is not done.";
    }

    private static void CryptCleared(Town town)
    {
        foreach (var f in town.Factions)
        {
            if (f.CryptId == null || f.CryptBroken) continue;
            var site = Quests.Site(town, f.CryptId);
            if (site == null || !site.Cleared) continue;
            f.CryptBroken = true;
            if (!f.Active) continue;
            f.Heat = 100;
            bool wasKnown = f.Known;
            f.Known = true;
            if (!wasKnown)
                town.Log(
                    LedgerCategory.Faction,
                    Importance.Major,
                    $"Among the bones in {site.Def.Name} the heroes find the choir's roll of names: {Expedition.ListNames(f.Members.Select(id => town.Agent(id).Name).ToList())}. {Cap(f.Name)} {f.Verb("is", "are")} exposed.",
                    f.Members.ToList());
        }
    }

    // The mayor's levers --------------------------------------------------------------------------

    /// <summary>Send the watch against an exposed faction: members fined, the leader run out of town, its coffers seized.</summary>
    public static bool CrackDown(Town town, string id)
    {
        var r = Rules(town);
        var f = Find(town, id);
        if (f == null || !f.Active || !f.Known || town.Treasury < r.CrackdownCost) return false;
        town.Book(TreasuryAccount.Factions, -r.CrackdownCost);
        bool cult = f.Lore.Kind == "cult";
        var members = f.Members.Select(town.Agent).ToList();
        // The leader is run out of town, with any member the watch had caught before.
        var exiled = members.Take(1).Concat(members.Skip(1).Where(a => f.Caught.Get(a.Id, 0) > 0)).ToList();
        var rest = members.Except(exiled).ToList();
        double fines = 0;
        foreach (var a in rest)
        {
            double fine = Math.Min(a.Gold, r.Fine);
            a.Gold -= fine;
            fines += fine;
            // The cult's dupes recant: they keep no god for now, and the choir loses them.
            if (cult)
            {
                a.Faith = "";
                a.Devout = false;
            }
        }
        // The watch takes part of what the exiles carry; the rest leaves town with them.
        double purses = 0;
        if (f.Def.Shares)
            foreach (var a in exiled)
            {
                double take = Math.Floor(Math.Max(0, a.Gold) * r.Stash.Seize);
                a.Gold -= take;
                purses += take;
            }
        foreach (var a in exiled) Population.Depart(town, a);
        double seized = Math.Max(0, Coffers(town, f)) + purses;
        AddCoffers(town, f, -(seized - purses));
        town.Book(TreasuryAccount.Fines, fines + seized);
        f.Members.RemoveAll(i => exiled.Any(a => a.Id == i));
        // Half of those fined have had enough; a cult loses everyone.
        var quit = f.Members.Where((id, i) => cult || i % 2 == 1).ToList();
        f.Members.RemoveAll(quit.Contains);
        f.QuietUntil = town.Day + r.QuietDays;
        f.Heat = 0;
        f.Standing -= 20;
        town.Mandate = Math.Min(100, town.Mandate + r.CrackdownMandate);
        string where = f.DenId != null ? $"the cellar of {town.Building(f.DenId.Value).Name}" : f.HallId != null ? town.Building(f.HallId.Value).Name : "its meeting places";
        string names = Expedition.ListNames(exiled.Select(a => a.Name).ToList());
        var after = new List<string>();
        if (rest.Count > 0) after.Add(cult ? $"{rest.Count} more recant before the priests" : $"{rest.Count} more {(rest.Count == 1 ? "is" : "are")} fined");
        if (seized >= 1) after.Add($"{Js.Str(Js.Round(seized))} gold is seized");
        town.Log(
            LedgerCategory.Faction,
            Importance.Major,
            $"The watch raids {where}. {names} {(exiled.Count == 1 ? "is" : "are")} run out of {town.Name}" +
            (after.Count > 0 ? $", {string.Join(after.Count > 1 ? ", and " : "", after)}." : ".") +
            (f.Members.Count > 0 ? $" What is left of {f.Name} goes to ground." : $" {Cap(f.Name)} {f.Verb("is", "are")} broken."),
            members.Select(a => a.Id).ToList());
        if (f.Members.Count > 0 && f.Def.Vendetta) f.VendettaUntil = town.Day + r.Vendetta.Days;
        if (f.Members.Count > 0 && f.Secret) f.Known = false;
        else
        {
            f.Active = false;
            f.DormantUntil = town.Day + r.DormantDays;
        }
        // A mayor who stares the town's crooks down wins a little more trust.
        town.Mandate = Math.Min(100, town.Mandate + Sheet.Gain(town, "crackdown", Sheet.Decide(town, "crackdown")));
        return true;
    }

    /// <summary>Back an open (or exposed) faction with treasury gold: its coffers fill and its standing with the hall rises.</summary>
    public static bool Support(Town town, string id, double gold)
    {
        var f = Find(town, id);
        if (f == null || !f.Active || !f.Known || gold <= 0 || town.Treasury < gold) return false;
        town.Book(TreasuryAccount.Factions, -gold);
        AddCoffers(town, f, gold);
        f.Standing = Math.Min(100, f.Standing + gold / Rules(town).SupportPerStanding);
        town.Log(LedgerCategory.Faction, Importance.Notable, $"The council grants {f.Name} {Js.Str(gold)} gold from the treasury. Its members drink the mayor's health.", f.Members.ToList());
        return true;
    }

    /// <summary>The stand-in mayor: break the cult the moment it is known, and the thieves once they have been robbing the town.</summary>
    private static void Policy(Town town)
    {
        var r = Rules(town);
        foreach (var f in town.Factions)
        {
            if (!f.Active || !f.Secret || !f.Known) continue;
            bool robbing = f.LastTheftDay != null && town.Day - f.LastTheftDay.Value <= 10;
            if (f.Lore.Kind == "cult" || (robbing && town.Treasury >= r.CrackdownCost + 100)) CrackDown(town, f.Id);
        }
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
