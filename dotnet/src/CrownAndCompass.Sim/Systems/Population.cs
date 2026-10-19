namespace CrownAndCompass.Sim.Systems;

public sealed record NewAgentOptions
{
    public string? Profession { get; init; }
    public string? Race { get; init; }
    public string? FamilyName { get; init; }
    public double? Gold { get; init; }
}

/// <summary>Who comes to town, who settles, and who gives up and leaves.</summary>
public static class Population
{
    private static readonly string[] Professions = ["farmer", "farmer", "woodcutter", "laborer", "laborer", "smith", "brewer", "barkeep", "merchant", "shopkeeper"];

    /// <summary>The business a newcomer of this trade dreams of opening, if the town has reached it.</summary>
    private static string? DreamFor(Town town, string profession) =>
        town.Content.Buildings.FirstOrDefault(d => d.Profession == profession && d.Buildable && d.IsBusiness && City.Unlocked(town, d))?.Id;

    /// <summary>A newcomer's trade: the frontier's usual mix, or one the town's own workshops call for.</summary>
    private static string PickProfession(Town town)
    {
        var rng = town.Rng;
        if (town.City.Tier == 0 || rng.Chance(0.5)) return rng.Pick(Professions);
        var trades = town.Content.Buildings
            .Where(d => d.Buildable && d.IsBusiness && d.Profession != null && City.Unlocked(town, d))
            .Select(d => d.Profession!)
            .Distinct()
            .ToList();
        return trades.Count > 0 ? rng.Pick(trades) : rng.Pick(Professions);
    }

    private static readonly (Trait, Trait)[] Opposites =
    [
        (Trait.Gregarious, Trait.Shy),
        (Trait.Greedy, Trait.Generous),
        (Trait.Ambitious, Trait.Lazy),
    ];

    public static Agent CreateAgent(Town town, NewAgentOptions? opts = null)
    {
        opts ??= new NewAgentOptions();
        var rng = town.Rng;
        var raceDef = opts.Race != null ? town.Content.Races.First(r => r.Id == opts.Race) : PickRace(town);
        string profession = opts.Profession ?? PickProfession(town);
        var traits = RollTraits(rng);
        bool entrepreneur = profession == "merchant" || traits.Contains(Trait.Ambitious);

        var a = new Agent { Id = town.NewAgentId() };
        int given = rng.Range(0, raceDef.Given.Count);
        a.FamilyName = opts.FamilyName ?? rng.Pick(raceDef.Family);
        // Two living townsfolk never share a full name (it read as one councillor holding two seats): take the
        // next given name on the list instead, which costs no extra dice.
        for (int k = 0; k < raceDef.Given.Count; k++)
        {
            string g = raceDef.Given[(given + k) % raceDef.Given.Count];
            if (town.Agents.Any(o => !o.Departed && !o.Dead && o.GivenName == g && o.FamilyName == a.FamilyName)) continue;
            given = (given + k) % raceDef.Given.Count;
            break;
        }
        a.GivenName = raceDef.Given[given];
        a.Race = raceDef.Name;
        a.Age = rng.Range(18, 56);
        a.Abilities = Abilities.Roll(rng);
        a.Traits = traits;
        a.Profession = profession;
        a.Gold = opts.Gold ?? (entrepreneur ? rng.Range(60, 170) : rng.Range(5, 35));
        a.Hunger = rng.Range(10, 40);
        a.Fatigue = rng.Range(10, 40);
        a.Loneliness = rng.Range(10, 50);
        a.ArrivedTick = town.Tick;
        a.Ambition = entrepreneur ? Ambition.OpenBusiness : Ambition.OwnHome;
        if (entrepreneur && DreamFor(town, profession) is { } dream) a.DreamBusiness = dream;
        town.Agents.Add(a);
        return a;
    }

    private static RaceDef PickRace(Town town)
    {
        var races = town.Content.Races;
        int total = races.Sum(r => r.Weight);
        int roll = town.Rng.Range(0, total);
        foreach (var r in races)
        {
            if (roll < r.Weight) return r;
            roll -= r.Weight;
        }
        return races[0];
    }

    /// <summary>Someone with these traits can't also have this one (shy and gregarious, say).</summary>
    public static bool Opposed(IReadOnlySet<Trait> traits, Trait t) =>
        Opposites.Any(o => (o.Item1 == t && traits.Contains(o.Item2)) || (o.Item2 == t && traits.Contains(o.Item1)));

    private static HashSet<Trait> RollTraits(Rng rng)
    {
        var traits = new HashSet<Trait>();
        int wanted = rng.Range(1, 4);
        for (int guard = 0; guard < 20 && traits.Count < wanted; guard++)
        {
            var t = rng.Pick(TraitOrder.All);
            if (Opposed(traits, t)) continue;
            traits.Add(t);
        }
        return traits;
    }

    /// <summary>One reason settlers come (positive) or stay away (negative), as it counts toward <see cref="Appeal"/>.</summary>
    public sealed record Pull(string Label, double Value);

    /// <summary>How strongly the town draws settlers today (0.05 to 1.6), and the reasons that add up to it.</summary>
    public sealed record Draw(double Total, List<Pull> Parts);

    /// <summary>
    /// The town's pull on settlers, read off the town with no dice: <see cref="DailyArrivals"/> rolls against its total,
    /// and the City window and alerts show its parts so the player can see why settlers come or don't.
    /// </summary>
    public static Draw Appeal(Town town)
    {
        int openJobs = 0;
        int openBeds = 0;
        int sites = 0;
        foreach (var b in town.Buildings)
        {
            if (b.Crew == 0) openJobs += b.OpenJobs;
            openBeds += b.OpenBeds;
            if (b.IsSite) sites++;
        }
        double foodPrice = town.Market.Price(Good.Food);
        var res = town.Workers;
        int homeless = res.Count(x => x.HomeId == null);
        int idle = res.Count(x => x.JobId == null);

        double jobs = 0.06 * openJobs, beds = 0.04 * openBeds, building = 0.05 * sites, bread = foodPrice > 1.8 ? 0.4 : 0;
        double roofless = 0.04 * homeless, workless = 0.06 * idle;
        double attraction = 0.3 + jobs + beds + building - bread - roofless - workless;
        // Word travels: a popular mayor draws settlers, high taxes turn them away.
        double mandate = (town.Mandate - 50) / 200, tax = (town.TaxRate - town.Content.Rules.TaxRate) * 5, decor = Decor.Draw(town), works = GrandWorks.Draw(town);
        double season = Seasons.Arrivals(town), decrees = Decrees.Arrivals(town), institutions = Institutions.Arrivals(town), unrest = Taxes.Attraction(town);
        attraction += mandate - tax + decor + works + season + decrees + institutions + unrest;
        // A young town is a chance at cheap land: the frontier draws settlers until the town is a proper village.
        double frontier = town.Content.Rules.FrontierDraw * Math.Max(0, 1 - town.Population / town.Content.Rules.FrontierSize);
        attraction += frontier;
        // Settlers want land to build on: with no residential lot zoned and for sale, fewer come.
        double land = Planning.VacantZoned(town, Zone.Residential) == 0 ? 0.15 : 0;
        if (land > 0) attraction -= land;
        attraction = Math.Max(0.05, Math.Min(1.6, attraction));

        var parts = new List<Pull> { new("Word on the road", 0.3) };
        void Add(string label, double v) { if (v != 0) parts.Add(new(label, v)); }
        Add(openJobs == 1 ? "1 job going" : $"{openJobs} jobs going", jobs);
        Add(openBeds == 1 ? "1 empty bed" : $"{openBeds} empty beds", beds);
        Add(sites == 1 ? "1 building site hiring" : $"{sites} building sites hiring", building);
        Add($"Dear bread ({foodPrice:0.00}g a loaf)", -bread);
        Add($"{homeless} homeless", -roofless);
        Add($"{idle} out of work", -workless);
        Add($"Your mandate ({Math.Round(town.Mandate)})", mandate);
        Add($"Taxes at {Math.Round(town.TaxRate * 100)}%", -tax);
        Add("Decorations", decor);
        Add("Grand works", works);
        Add($"The season ({Seasons.Current(town).Name.ToLowerInvariant()})", season);
        Add("Decrees in force", decrees);
        Add("The town's institutions", institutions);
        Add("Unrest over neglected services", unrest);
        Add("Cheap land in a young town", frontier);
        Add("No zoned home lots for sale", -land);
        return new(attraction, parts);
    }

    /// <summary>Settlers a week the town can expect at its current appeal (groups of one or two, about 1.3 each).</summary>
    public static double WeeklySettlers(Town town, double appeal) => 7 * Rolls(town) * appeal / 4 * 1.3;

    // Word of a bigger town travels further: the roads bring more hopefuls to a city than a thorp.
    private static int Rolls(Town town) => 4 + Math.Min(town.Population, 640) / 80;

    /// <summary>Daily: newcomers arrive if the town looks like a place worth living.</summary>
    public static void DailyArrivals(Town town)
    {
        var rng = town.Rng;
        double attraction = Appeal(town).Total;

        int rolls = Rolls(town);
        int groups = 0;
        for (int i = 0; i < rolls; i++) if (rng.Chance(attraction / 4)) groups++;

        for (int g = 0; g < groups; g++)
        {
            if (rng.Chance(0.3)) ArriveCouple(town);
            else ArriveSingle(town);
        }
    }

    /// <summary>
    /// Settlers a story sends: n come in (as singles), or, with n below zero, the unhappiest households
    /// (lowest mood, ties by id) leave. Returns how many people came or went.
    /// </summary>
    public static int Settle(Town town, int n)
    {
        if (n > 0)
        {
            for (int i = 0; i < n; i++) ArriveSingle(town);
            return n;
        }
        int gone = 0;
        foreach (var a in town.Workers.Where(a => a.Age >= 18).OrderBy(a => a.LastDayMood).ThenBy(a => a.Id).ToList())
        {
            if (gone >= -n) break;
            if (a.Departed) continue;
            Depart(town, a);
            gone++;
        }
        return -gone;
    }

    private static void ArriveSingle(Town town)
    {
        var a = CreateAgent(town);
        if (a.Ambition == Ambition.OpenBusiness)
        {
            string dream = a.DreamBusiness != null
                ? $"dreaming of opening {Agent.Article(town.Content.Building(a.DreamBusiness).Name)} {town.Content.Building(a.DreamBusiness).Name.ToLowerInvariant()}"
                : "looking for a business to buy into";
            town.Log(LedgerCategory.Arrival, Importance.Notable, $"{a.Describe()}, rides in with {Js.ToFixed(a.Gold, 0)} gold, {dream}.", [a.Id]);
        }
        else
        {
            town.Log(LedgerCategory.Arrival, Importance.Routine, $"{a.Describe()}, arrives looking for work. ({a.TraitList()})", [a.Id]);
        }
    }

    private static void ArriveCouple(Town town)
    {
        var a = CreateAgent(town);
        string raceId = town.Content.Races.First(r => r.Name == a.Race).Id;
        var b = CreateAgent(town, new NewAgentOptions { Race = raceId, FamilyName = a.FamilyName });
        a.SpouseId = b.Id;
        b.SpouseId = a.Id;
        a.ChangeOpinion(b.Id, 70);
        b.ChangeOpinion(a.Id, 70);
        double pooled = a.Gold + b.Gold;
        town.Log(
            LedgerCategory.Arrival,
            Importance.Routine,
            $"The {a.FamilyName} family arrives by cart: {a.GivenName} the {a.Profession} and {b.GivenName} the {b.Profession}, with {Js.ToFixed(pooled, 0)} gold between them.",
            [a.Id, b.Id]);
    }

    /// <summary>Daily: agents who have been miserable too long leave town, taking their spouse.</summary>
    public static void DailyDepartures(Town town)
    {
        foreach (var a in town.Residents)
        {
            if (a.Departed || a.MiserableDays < 4) continue;
            string reason = WorstComplaint(a);
            Depart(town, a);
            string withName = "";
            if (a.SpouseId != null && !town.Agent(a.SpouseId.Value).Departed)
            {
                var s = town.Agent(a.SpouseId.Value);
                Depart(town, s);
                withName = $" and {s.GivenName}";
            }
            string verb = withName == "" ? "packs up and leaves" : "pack up and leave";
            town.Log(LedgerCategory.Departure, Importance.Notable, $"{a.Name}{withName} {verb} {town.Name}, {reason}.", [a.Id]);
        }
    }

    private static string WorstComplaint(Agent a)
    {
        if (a.Hunger > 70) return "unable to afford a decent meal";
        if (a.DaysHomeless >= 3) return "tired of sleeping in the mud behind the stables";
        if (a.DaysUnemployed >= 4) return "having found no work worth the name";
        if (a.Loneliness > 70) return "without a single friend in town";
        return "worn down by frontier life";
    }

    public static void Depart(Town town, Agent a)
    {
        a.Departed = true;
        if (a.JobId != null) Remove(town.Building(a.JobId.Value).Workers, a.Id);
        if (a.HomeId != null) Remove(town.Building(a.HomeId.Value).Residents, a.Id);
        a.JobId = null;
        a.HomeId = null;
        a.LocationId = null;
        if (a.CourtingId != null)
        {
            town.Agent(a.CourtingId.Value).CourtingId = null;
            a.CourtingId = null;
        }

        // Property left behind reverts to the town; businesses go up for sale.
        foreach (var b in town.Buildings)
        {
            if (b.OwnerId != a.Id) continue;
            b.OwnerId = null;
            town.Lot(b.LotId).OwnerId = null;
            // A bastion passes to the next of its company (Bastions.Daily), or stands empty.
            if (b.Def.Jobs > 0 && !b.Def.Bastion) Economy.PutUpForSale(town, b);
        }
        foreach (var lot in town.Lots) if (lot.OwnerId == a.Id) lot.OwnerId = null;
        // Whoever leaves, by the road or the grave, leaves their factions too.
        foreach (var f in town.Factions) Remove(f.Members, a.Id);
    }

    public static void Remove(List<int> list, int id)
    {
        int i = list.IndexOf(id);
        if (i >= 0) list.RemoveAt(i);
    }
}
