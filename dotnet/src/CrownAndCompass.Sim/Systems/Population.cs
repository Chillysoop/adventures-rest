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
    private static readonly string[] Professions = ["farmer", "farmer", "woodcutter", "laborer", "laborer", "smith", "brewer", "barkeep", "merchant"];

    private static readonly Dictionary<string, string> DreamFor = new()
    {
        ["farmer"] = "farm",
        ["woodcutter"] = "lumber_camp",
        ["smith"] = "forge",
        ["brewer"] = "brewery",
        ["barkeep"] = "tavern",
    };

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
        string profession = opts.Profession ?? rng.Pick(Professions);
        var traits = RollTraits(rng);
        bool entrepreneur = profession == "merchant" || traits.Contains(Trait.Ambitious);

        var a = new Agent { Id = town.NewAgentId() };
        a.GivenName = rng.Pick(raceDef.Given);
        a.FamilyName = opts.FamilyName ?? rng.Pick(raceDef.Family);
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
        if (entrepreneur && DreamFor.TryGetValue(profession, out var dream)) a.DreamBusiness = dream;
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

    private static HashSet<Trait> RollTraits(Rng rng)
    {
        var traits = new HashSet<Trait>();
        int wanted = rng.Range(1, 4);
        for (int guard = 0; guard < 20 && traits.Count < wanted; guard++)
        {
            var t = rng.Pick(TraitOrder.All);
            if (Opposites.Any(o => (o.Item1 == t && traits.Contains(o.Item2)) || (o.Item2 == t && traits.Contains(o.Item1)))) continue;
            traits.Add(t);
        }
        return traits;
    }

    /// <summary>Daily: newcomers arrive if the town looks like a place worth living.</summary>
    public static void DailyArrivals(Town town)
    {
        var rng = town.Rng;
        int openJobs = 0;
        int openBeds = 0;
        int sites = 0;
        foreach (var b in town.Buildings)
        {
            openJobs += b.OpenJobs;
            openBeds += b.OpenBeds;
            if (b.IsSite) sites++;
        }
        double foodPrice = town.Market.Price(Good.Food);
        var res = town.Workers;
        int homeless = res.Count(x => x.HomeId == null);
        int idle = res.Count(x => x.JobId == null);

        double attraction = 0.3 + 0.06 * openJobs + 0.04 * openBeds + 0.05 * sites - (foodPrice > 1.8 ? 0.4 : 0) - 0.04 * homeless - 0.06 * idle;
        // Word travels: a popular mayor draws settlers, high taxes turn them away.
        attraction += (town.Mandate - 50) / 200 - (town.TaxRate - town.Content.Rules.TaxRate) * 5;
        attraction = Math.Max(0.05, Math.Min(2.5, attraction));

        int groups = 0;
        for (int i = 0; i < 4; i++) if (rng.Chance(attraction / 4)) groups++;

        for (int g = 0; g < groups; g++)
        {
            if (rng.Chance(0.3)) ArriveCouple(town);
            else ArriveSingle(town);
        }
    }

    private static void ArriveSingle(Town town)
    {
        var a = CreateAgent(town);
        if (a.Ambition == Ambition.OpenBusiness)
        {
            string dream = a.DreamBusiness != null
                ? $"dreaming of opening a {town.Content.Building(a.DreamBusiness).Name.ToLowerInvariant()}"
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
            if (b.Def.Jobs > 0) Economy.PutUpForSale(town, b);
        }
        foreach (var lot in town.Lots) if (lot.OwnerId == a.Id) lot.OwnerId = null;
    }

    public static void Remove(List<int> list, int id)
    {
        int i = list.IndexOf(id);
        if (i >= 0) list.RemoveAt(i);
    }
}
