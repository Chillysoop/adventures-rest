using CrownAndCompass.Sim.Agents;
using CrownAndCompass.Sim.Core;
using CrownAndCompass.Sim.World;

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
        foreach (var a in town.Agents)
        {
            if (a.Departed) continue;
            UpdateNeeds(town, a);
            var choice = Choose(town, a);
            Execute(town, a, choice);
            SampleMood(a);
        }
    }

    private static void UpdateNeeds(Town town, Agent a)
    {
        bool asleep = a.Activity == Activity.Sleeping;
        a.Hunger = Math.Min(100, a.Hunger + (asleep ? 1.0 : 3.0));
        if (!asleep)
            a.Fatigue = Math.Min(100, a.Fatigue + Math.Max(2.5, 4.0 - 0.3 * Dice.Modifier(a.Abilities.Con)));
        double lonely = a.Has(Trait.Gregarious) ? 2.5 : a.Has(Trait.Shy) ? 0.8 : 1.5;
        a.Loneliness = Math.Min(100, a.Loneliness + (asleep ? 0.3 : lonely));
    }

    private static Activity Choose(Town town, Agent a)
    {
        int hour = town.Hour;
        bool night = SimTime.IsNight(hour);
        var rng = town.Rng;

        // Sleep is sticky: once down for the night, stay down until rested or morning.
        if (a.Activity == Activity.Sleeping && a.Fatigue > 5 && (night || a.Fatigue > 40)) return Activity.Sleeping;

        double sleep = a.Fatigue / 100 * (night ? 2.0 : hour >= 21 ? 1.0 : 0.3) + (a.Fatigue > 90 ? 1 : 0);

        double mealCost = town.Market.Quote(Good.Food, 1);
        bool canEat = a.Gold >= mealCost;
        double eat = a.Hunger > 35 ? (canEat ? a.Hunger / 100 * 1.4 + (hour is 7 or 12 or 18 ? 0.2 : 0) : 0.05) : 0;

        double work = 0;
        if (a.JobId != null && SimTime.IsWorkHours(hour))
            work = (a.Has(Trait.Lazy) ? 0.55 : 0.85) + (a.Has(Trait.Greedy) ? 0.1 : 0) + (a.Has(Trait.Ambitious) ? 0.05 : 0);

        double drink = 0;
        if (SimTime.IsEvening(hour) && a.Gold >= 8 && town.Taverns.Any())
        {
            drink = 0.25 + a.Loneliness / 100 * 0.8
                    + (a.Has(Trait.Gregarious) ? 0.25 : 0) - (a.Has(Trait.Shy) ? 0.2 : 0)
                    - (a.Drinks >= 2 ? 0.4 : 0) - (a.Has(Trait.Greedy) ? 0.1 : 0);
        }

        double relax = 0.3;
        double wander = night ? 0 : 0.15;

        var options = new (Activity act, double score)[]
        {
            (Activity.Sleeping, sleep), (Activity.Eating, eat), (Activity.Working, work),
            (Activity.Drinking, drink), (Activity.Relaxing, relax), (Activity.Wandering, wander),
        };
        Activity best = Activity.Relaxing;
        double bestScore = double.MinValue;
        foreach (var (act, score) in options)
        {
            double s = score + rng.NextDouble() * 0.1;
            if (s > bestScore) { bestScore = s; best = act; }
        }
        return best;
    }

    private static void Execute(Town town, Agent a, Activity act)
    {
        a.Activity = act;
        switch (act)
        {
            case Activity.Sleeping:
                a.LocationId = a.HomeId;
                a.Fatigue = Math.Max(0, a.Fatigue - (a.HomeId != null ? 14 : 8));
                a.Drinks = 0;
                break;

            case Activity.Eating:
                double cost = town.Market.Buy(Good.Food, 1);
                a.Gold -= cost;
                a.Hunger = Math.Max(0, a.Hunger - 55);
                a.LocationId = a.HomeId;
                break;

            case Activity.Working:
                Work(town, a);
                break;

            case Activity.Drinking:
                Drink(town, a);
                break;

            case Activity.Relaxing:
                a.LocationId = a.HomeId;
                a.Fatigue = Math.Max(0, a.Fatigue - 1.5);
                if (a.HomeId is int h && town.Building(h).Residents.Count > 1)
                    a.Loneliness = Math.Max(0, a.Loneliness - (a.SpouseId != null ? 4 : 2));
                break;

            case Activity.Wandering:
                a.LocationId = null;
                a.Loneliness = Math.Max(0, a.Loneliness - 1);
                break;
        }
    }

    private static void Work(Town town, Agent a)
    {
        var b = town.Building(a.JobId!.Value);
        a.LocationId = b.Id;
        if (b.IsSite)
        {
            a.Activity = Activity.Building;
            b.BuildProgress += Math.Max(0.5, 1 + 0.1 * Dice.Modifier(a.Abilities.Str));
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

    private static void Drink(Town town, Agent a)
    {
        var tavern = town.Taverns
            .Where(t => PatronsAt(town, t) < t.Def.Capacity)
            .OrderByDescending(t => t.OwnerId is int o ? a.OpinionOf(o) : 0)
            .ThenBy(t => t.Id)
            .FirstOrDefault();
        if (tavern == null) { a.Activity = Activity.Relaxing; a.LocationId = a.HomeId; return; }

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
        double price = Math.Round(town.Market.Price(Good.Ale) * AleMarkup, 1);
        if (tavern.StockOf(Good.Ale) >= 1 && a.Gold >= price)
        {
            tavern.AddStock(Good.Ale, -1);
            a.Gold -= price;
            double tax = price * town.TaxRate;
            town.Treasury += tax;
            tavern.Cash += price - tax;
            tavern.RevenueToday += price;
            a.Drinks++;
        }
        a.Loneliness = Math.Max(0, a.Loneliness - 8);
    }

    public static int PatronsAt(Town town, Building b) =>
        town.Agents.Count(x => !x.Departed && x.Activity == Activity.Drinking && x.LocationId == b.Id);

    private static void SampleMood(Agent a)
    {
        double mood = 100 - 0.45 * a.Hunger - 0.25 * a.Fatigue - 0.25 * a.Loneliness
                      - (a.HomeId == null ? 20 : 0)
                      - (a.JobId == null && a.Ambition != Ambition.OpenBusiness ? 10 : 0);
        a.MoodToday += Math.Clamp(mood, 0, 100);
        a.MoodSamples++;
    }

    public static void Daily(Town town)
    {
        foreach (var a in town.Residents)
        {
            a.LastDayMood = a.MoodSamples > 0 ? a.MoodToday / a.MoodSamples : a.LastDayMood;
            a.MoodToday = 0; a.MoodSamples = 0;
            a.MiserableDays = a.LastDayMood < 35 ? a.MiserableDays + 1 : 0;
            a.DaysHomeless = a.HomeId == null ? a.DaysHomeless + 1 : 0;
            a.DaysUnemployed = a.JobId == null ? a.DaysUnemployed + 1 : 0;
        }
    }
}
