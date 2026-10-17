using CrownAndCompass.Sim.Agents;
using CrownAndCompass.Sim.Core;
using CrownAndCompass.Sim.World;

namespace CrownAndCompass.Sim.Systems;

/// <summary>Who comes to town, who settles, and who gives up and leaves.</summary>
public static class Population
{
    private static readonly string[] Professions =
        { "farmer", "farmer", "woodcutter", "laborer", "laborer", "smith", "brewer", "barkeep", "merchant" };

    private static readonly Dictionary<string, string> DreamFor = new()
    {
        ["farmer"] = "farm", ["woodcutter"] = "lumber_camp", ["smith"] = "forge",
        ["brewer"] = "brewery", ["barkeep"] = "tavern",
    };

    private static readonly (Trait a, Trait b)[] Opposites =
    {
        (Trait.Gregarious, Trait.Shy), (Trait.Greedy, Trait.Generous), (Trait.Ambitious, Trait.Lazy),
    };

    public static Agent CreateAgent(Town town, string? profession = null, string? race = null, string? familyName = null, double? gold = null)
    {
        var rng = town.Rng;
        var raceDef = race != null ? town.Content.Races.First(r => r.Id == race) : PickRace(town);
        profession ??= rng.Pick(Professions);
        var traits = RollTraits(rng);
        bool entrepreneur = profession == "merchant" || (traits & Trait.Ambitious) != 0;

        var agent = new Agent
        {
            Id = town.NewAgentId(),
            GivenName = rng.Pick(raceDef.Given),
            FamilyName = familyName ?? rng.Pick(raceDef.Family),
            Race = raceDef.Name,
            Age = rng.Range(18, 56),
            Abilities = Abilities.Roll(rng),
            Traits = traits,
            Profession = profession,
            Gold = gold ?? (entrepreneur ? rng.Range(60, 170) : rng.Range(5, 35)),
            Hunger = rng.Range(10, 40),
            Fatigue = rng.Range(10, 40),
            Loneliness = rng.Range(10, 50),
            ArrivedTick = town.Tick,
        };
        agent.Ambition = entrepreneur ? Ambition.OpenBusiness : Ambition.OwnHome;
        if (entrepreneur && DreamFor.TryGetValue(profession, out var dream)) agent.DreamBusiness = dream;
        town.Agents.Add(agent);
        return agent;
    }

    private static RaceDef PickRace(Town town)
    {
        int total = town.Content.Races.Sum(r => r.Weight);
        int roll = town.Rng.Range(0, total);
        foreach (var r in town.Content.Races)
        {
            if (roll < r.Weight) return r;
            roll -= r.Weight;
        }
        return town.Content.Races[0];
    }

    private static Trait RollTraits(Rng rng)
    {
        var all = Enum.GetValues<Trait>().Where(t => t != Trait.None).ToArray();
        Trait traits = Trait.None;
        int wanted = rng.Range(1, 4);
        for (int guard = 0; guard < 20 && BitCount(traits) < wanted; guard++)
        {
            var t = rng.Pick(all);
            if (Opposites.Any(o => (o.a == t && (traits & o.b) != 0) || (o.b == t && (traits & o.a) != 0))) continue;
            traits |= t;
        }
        return traits;
    }

    private static int BitCount(Trait t) => System.Numerics.BitOperations.PopCount((uint)t);

    /// <summary>Daily: newcomers arrive if the town looks like a place worth living.</summary>
    public static void DailyArrivals(Town town)
    {
        var rng = town.Rng;
        int openJobs = town.Buildings.Sum(b => b.OpenJobs);
        int openBeds = town.Buildings.Sum(b => b.OpenBeds);
        int sites = town.Buildings.Count(b => b.IsSite);
        double foodPrice = town.Market.Price(Good.Food);
        int homeless = town.Residents.Count(x => x.HomeId == null);

        double attraction = 0.35 + 0.10 * openJobs + 0.04 * openBeds + 0.05 * sites
                            - (foodPrice > 1.8 ? 0.4 : 0) - 0.04 * homeless
                            - 0.04 * town.Residents.Count(a => a.JobId == null);
        attraction = Math.Clamp(attraction, 0.05, 2.5);

        int groups = 0;
        for (int i = 0; i < 4; i++)
            if (rng.Chance(attraction / 4)) groups++;

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
            string dream = a.DreamBusiness != null ? $"dreaming of opening a {town.Content.Building(a.DreamBusiness).Name.ToLowerInvariant()}" : "looking for a business to buy into";
            town.Log(LedgerCategory.Arrival, Importance.Notable,
                $"{a.Describe()}, rides in with {a.Gold:0} gold, {dream}.", a.Id);
        }
        else
        {
            town.Log(LedgerCategory.Arrival, Importance.Routine,
                $"{a.Describe()}, arrives looking for work. ({a.TraitList()})", a.Id);
        }
    }

    private static void ArriveCouple(Town town)
    {
        var a = CreateAgent(town);
        var raceId = town.Content.Races.First(r => r.Name == a.Race).Id;
        var b = CreateAgent(town, race: raceId, familyName: a.FamilyName);
        a.SpouseId = b.Id; b.SpouseId = a.Id;
        a.ChangeOpinion(b.Id, 70); b.ChangeOpinion(a.Id, 70);
        double pooled = a.Gold + b.Gold;
        town.Log(LedgerCategory.Arrival, Importance.Routine,
            $"The {a.FamilyName} family arrives by cart: {a.GivenName} the {a.Profession} and {b.GivenName} the {b.Profession}, with {pooled:0} gold between them.",
            a.Id, b.Id);
    }

    /// <summary>Daily: agents who have been miserable too long leave town, taking their spouse.</summary>
    public static void DailyDepartures(Town town)
    {
        foreach (var a in town.Residents.ToList())
        {
            if (a.Departed || a.MiserableDays < 4) continue;
            string reason = WorstComplaint(a);
            Depart(town, a);
            string with = "";
            if (a.SpouseId is int sid && !town.Agent(sid).Departed)
            {
                Depart(town, town.Agent(sid));
                with = $" and {town.Agent(sid).GivenName}";
            }
            town.Log(LedgerCategory.Departure, Importance.Notable,
                $"{a.Name}{with} {(with == "" ? "packs" : "pack")} up and {(with == "" ? "leaves" : "leave")} {town.Name}, {reason}.", a.Id);
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
        if (a.JobId is int j) town.Building(j).Workers.Remove(a.Id);
        if (a.HomeId is int h) town.Building(h).Residents.Remove(a.Id);
        a.JobId = null; a.HomeId = null;
        a.LocationId = null;
        if (a.CourtingId is int c) { town.Agent(c).CourtingId = null; a.CourtingId = null; }

        // Property left behind reverts to the town; businesses go up for sale.
        foreach (var b in town.Buildings.Where(b => b.OwnerId == a.Id))
        {
            b.OwnerId = null;
            town.Lot(b.LotId).OwnerId = null;
            if (b.Def.IsBusiness) Economy.PutUpForSale(town, b);
        }
        foreach (var lot in town.Lots.Where(l => l.OwnerId == a.Id)) lot.OwnerId = null;
    }
}
