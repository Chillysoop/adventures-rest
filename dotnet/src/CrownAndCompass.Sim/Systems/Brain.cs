using static CrownAndCompass.Sim.SimTime;

namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Hourly utility AI: every agent scores what it could do this hour from its needs, traits,
/// money and the time of day, then does the best-scoring thing.
/// </summary>
public static class Brain
{
    public const double ConstructionWage = 0.6;
    public const double AleMarkup = 2.0;

    public static void Hourly(Town town)
    {
        // Count the drinkers once and keep the count as each agent moves, rather than recounting the
        // whole town every time someone looks for a seat.
        var drinkers = new Dictionary<int, int>();
        foreach (var x in town.Agents)
            if (!x.Departed && x.Activity == Activity.Drinking && x.LocationId is int at) drinkers[at] = drinkers.GetValueOrDefault(at) + 1;
        town.HourTaverns = town.Taverns;
        town.Drinkers = drinkers;
        double festival = Council.FestivalMood(town) + Seasons.Mood(town);
        foreach (var a in town.Agents)
        {
            if (a.Departed || a.IsAway) continue;
            UpdateNeeds(a);
            int? was = a.Activity == Activity.Drinking ? a.LocationId : null;
            town.DrinkerWasAt = was;
            Execute(town, a, Choose(town, a));
            if (was is int w) drinkers[w]--;
            if (a.Activity == Activity.Drinking && a.LocationId is int now) drinkers[now] = drinkers.GetValueOrDefault(now) + 1;
            SampleMood(a, festival + (a.Activity == Activity.Praying ? town.Content.Shrines.PrayMood : 0) + Faith.Mood(town, a) + City.HomeMood(town, a) + Decor.Mood(town, a));
        }
        town.HourTaverns = null;
        town.Drinkers = null;
        town.DrinkerWasAt = null;
    }

    private static void UpdateNeeds(Agent a)
    {
        bool asleep = a.Activity == Activity.Sleeping;
        a.Hunger = Math.Min(100, a.Hunger + (asleep ? 1.0 : 3.0));
        if (!asleep) a.Fatigue = Math.Min(100, a.Fatigue + Math.Max(2.5, 4.0 - 0.3 * Rng.Modifier(a.Abilities.Con)));
        double lonely = a.Has(Trait.Gregarious) ? 2.5 : a.Has(Trait.Shy) ? 0.8 : 1.5;
        a.Loneliness = Math.Min(100, a.Loneliness + (asleep ? 0.3 : lonely));
    }

    private static Activity Choose(Town town, Agent a)
    {
        int hour = town.Hour;
        bool night = IsNight(hour);
        var rng = town.Rng;

        // Sleep is sticky: once down for the night, stay down until rested or morning.
        if (a.Activity == Activity.Sleeping && a.Fatigue > 5 && (night || a.Fatigue > 40)) return Activity.Sleeping;

        double sleep = (a.Fatigue / 100) * (night ? 2.0 : hour >= 21 ? 1.0 : 0.3) + (a.Fatigue > 90 ? 1 : 0);

        bool canEat = a.Gold >= town.Market.Quote(Good.Food, 1);
        double eat = a.Hunger > 35 ? (canEat ? (a.Hunger / 100) * 1.4 + (hour == 7 || hour == 12 || hour == 18 ? 0.2 : 0) : 0.05) : 0;

        double work = 0;
        if (a.JobId != null && IsWorkHours(hour))
            work = (a.Has(Trait.Lazy) ? 0.55 : 0.85) + (a.Has(Trait.Greedy) ? 0.1 : 0) + (a.Has(Trait.Ambitious) ? 0.05 : 0);

        double drink = 0;
        if (IsEvening(hour) && a.Gold >= 8 && (town.HourTaverns ?? town.Taverns).Count > 0)
        {
            drink =
                0.25 +
                (a.Loneliness / 100) * 0.8 +
                (a.Has(Trait.Gregarious) ? 0.25 : 0) -
                (a.Has(Trait.Shy) ? 0.2 : 0) -
                (a.Drinks >= 2 ? 0.4 : 0) -
                (a.Has(Trait.Greedy) ? 0.1 : 0);
        }

        const double relax = 0.3;
        double wander = night ? 0 : 0.15;
        double pray = Shrines.PrayScore(town, a, hour);

        (Activity, double)[] options = pray > 0
            ?
            [
                (Activity.Sleeping, sleep),
                (Activity.Eating, eat),
                (Activity.Working, work),
                (Activity.Drinking, drink),
                (Activity.Relaxing, relax),
                (Activity.Wandering, wander),
                (Activity.Praying, pray),
            ]
            :
            [
                (Activity.Sleeping, sleep),
                (Activity.Eating, eat),
                (Activity.Working, work),
                (Activity.Drinking, drink),
                (Activity.Relaxing, relax),
                (Activity.Wandering, wander),
            ];
        var best = Activity.Relaxing;
        double bestScore = double.NegativeInfinity;
        foreach (var (act, score) in options)
        {
            double s = score + rng.Next() * 0.1;
            if (s > bestScore)
            {
                bestScore = s;
                best = act;
            }
        }
        return best;
    }

    private static void Execute(Town town, Agent a, Activity act)
    {
        a.Activity = act;
        int? was = a.LocationId;
        Go(town, a, act);
        if (a.LocationId != was && a.LocationId != null) Streets.Trip(town, was, a.LocationId);
    }

    private static void Go(Town town, Agent a, Activity act)
    {
        switch (act)
        {
            case Activity.Sleeping:
                a.LocationId = a.HomeId;
                a.Fatigue = Math.Max(0, a.Fatigue - (a.HomeId != null ? 14 : 8));
                a.Drinks = 0;
                break;
            case Activity.Eating:
                a.Gold -= town.Market.Buy(Good.Food, 1);
                a.Hunger = Math.Max(0, a.Hunger - 55);
                a.LocationId = a.HomeId;
                break;
            case Activity.Working:
                DoWork(town, a);
                break;
            case Activity.Drinking:
                DoDrink(town, a);
                break;
            case Activity.Relaxing:
                a.LocationId = a.HomeId;
                a.Fatigue = Math.Max(0, a.Fatigue - 1.5);
                if (a.HomeId != null && town.Building(a.HomeId.Value).Residents.Count > 1)
                    a.Loneliness = Math.Max(0, a.Loneliness - (a.SpouseId != null ? 4 : 2));
                break;
            case Activity.Wandering:
                a.LocationId = null;
                a.Loneliness = Math.Max(0, a.Loneliness - 1);
                break;
            case Activity.Praying:
                Shrines.Pray(town, a);
                break;
        }
    }

    private static void DoWork(Town town, Agent a)
    {
        var b = town.Building(a.JobId!.Value);
        a.LocationId = b.Id;
        if (b.IsSite)
        {
            a.Activity = Activity.Building;
            b.Raise(Math.Max(0.5, 1 + 0.1 * Rng.Modifier(a.Abilities.Str)) * Seasons.Build(town));
            if (b.OwnerId != a.Id)
            {
                double pay = Math.Min(ConstructionWage, b.LaborBudget);
                b.LaborBudget -= pay;
                a.Gold += pay;
            }
        }
        else if (b.OwnerId != a.Id)
        {
            // Wages are paid by the hour out of the business account, which may go into debt.
            b.Cash -= b.Def.Wage;
            a.Gold += b.Def.Wage;
        }
    }

    private static void DoDrink(Town town, Agent a)
    {
        var tavern = (town.HourTaverns ?? town.Taverns)
            .Where(t => Seated(town, a, t) < t.Def.Capacity)
            .OrderByDescending(t => OpinionOfOwner(a, t))
            .ThenBy(t => t.Id)
            .FirstOrDefault();
        if (tavern == null)
        {
            a.Activity = Activity.Relaxing;
            a.LocationId = a.HomeId;
            return;
        }

        a.LocationId = tavern.Id;
        if (tavern.StockOf(Good.Ale) < 1)
        {
            double restock = Math.Min(10, Math.Max(0, tavern.Cash) / Math.Max(0.1, town.Market.Price(Good.Ale)));
            if (restock >= 1)
            {
                tavern.Cash -= town.Market.Buy(Good.Ale, Math.Floor(restock));
                tavern.AddStock(Good.Ale, Math.Floor(restock));
            }
        }
        double price = Js.Round(town.Market.Price(Good.Ale) * AleMarkup * 10) / 10;
        if (tavern.StockOf(Good.Ale) >= 1 && a.Gold >= price)
        {
            tavern.AddStock(Good.Ale, -1);
            a.Gold -= price;
            double tax = price * town.TaxRate;
            town.Book(TreasuryAccount.AleTax, tax);
            tavern.Cash += price - tax;
            tavern.RevenueToday += price;
            a.Drinks++;
        }
        a.Loneliness = Math.Max(0, a.Loneliness - 8);
    }

    private static int OpinionOfOwner(Agent a, Building b) => b.OwnerId != null ? a.OpinionOf(b.OwnerId.Value) : 0;

    /// <summary>Drinkers at the tavern as the agent looks for a seat, the agent among them if already there.</summary>
    private static int Seated(Town town, Agent a, Building t)
    {
        if (town.Drinkers == null) return PatronsAt(town, t);
        // The running count still holds the agent's last hour; this hour they are drinking wherever they stand.
        int others = town.Drinkers.GetValueOrDefault(t.Id) - (town.DrinkerWasAt == t.Id ? 1 : 0);
        return others + (a.LocationId == t.Id ? 1 : 0);
    }

    public static int PatronsAt(Town town, Building b)
    {
        int n = 0;
        foreach (var x in town.Agents) if (!x.Departed && x.Activity == Activity.Drinking && x.LocationId == b.Id) n++;
        return n;
    }

    private static void SampleMood(Agent a, double bonus)
    {
        double mood =
            100 +
            bonus -
            0.45 * a.Hunger -
            0.25 * a.Fatigue -
            0.25 * a.Loneliness -
            (a.HomeId == null && a.Hero?.Lodging != true ? 20 : 0) -
            (a.JobId == null && a.Ambition != Ambition.OpenBusiness && a.Hero == null ? 10 : 0);
        a.MoodToday += Math.Max(0, Math.Min(100, mood));
        a.MoodSamples++;
    }

    public static void Daily(Town town)
    {
        foreach (var a in town.Residents)
        {
            if (a.IsAway) continue;
            a.LastDayMood = a.MoodSamples > 0 ? a.MoodToday / a.MoodSamples : a.LastDayMood;
            a.MoodToday = 0;
            a.MoodSamples = 0;
            a.MiserableDays = a.LastDayMood < 35 ? a.MiserableDays + 1 : 0;
            a.DaysHomeless = a.HomeId == null ? a.DaysHomeless + 1 : 0;
            a.DaysUnemployed = a.JobId == null ? a.DaysUnemployed + 1 : 0;
        }
    }
}
