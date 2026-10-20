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
        // Count who is out where once and keep the count as each agent moves, rather than recounting the
        // whole town every time someone comes to a door.
        Crowds.Begin(town);
        double festival = Council.FestivalMood(town) + Seasons.Mood(town) + Decrees.Mood(town) + Institutions.Mood(town) + Taxes.Mood(town) + Tournaments.Mood(town);
        foreach (var a in town.Agents)
        {
            if (a.Departed || a.IsAway) continue;
            UpdateNeeds(a);
            Crowds.Leave(town, a);
            Execute(town, a, Choose(town, a));
            Crowds.Moved(town, a);
            SampleMood(a, festival + (a.Activity == Activity.Praying ? town.Content.Shrines.PrayMood : 0) + Faith.Mood(town, a) + City.HomeMood(town, a) + Decor.Mood(town, a) + Crowds.Mood(town, a));
        }
        Crowds.End(town);
    }

    private static void UpdateNeeds(Agent a)
    {
        bool asleep = a.Activity == Activity.Sleeping;
        a.Hunger = Math.Min(100, a.Hunger + (asleep ? 1.0 : 3.0));
        if (!asleep) a.Fatigue = Math.Min(100, a.Fatigue + Math.Max(2.5, 4.0 - 0.3 * Rng.Modifier(a.Abilities.Con)));
        double lonely = a.Has(Trait.Gregarious) ? 2.5 : a.Has(Trait.Shy) ? 0.8 : 1.5;
        a.Loneliness = Math.Min(100, a.Loneliness + (asleep ? 0.3 : lonely));
    }

    /// <param name="barred">An outing just found full, which the agent thinks again about.</param>
    private static Activity Choose(Town town, Agent a, Activity? barred = null)
    {
        int hour = town.Hour;
        bool night = IsNight(hour);
        var rng = town.Rng;

        // Sleep is sticky: once down for the night, stay down until rested or morning.
        if (a.Activity == Activity.Sleeping && a.Fatigue > 5 && (night || a.Fatigue > 40)) return Activity.Sleeping;

        // A break holds them: on a bender they drink the waking hours away, sulking they stay in (Moods).
        if (a.Episode != "" && !night && a.Fatigue < 90 && a.Hunger < 70 && Moods.Holds(town, a) is Activity held) return held;

        double sleep = (a.Fatigue / 100) * (night ? 2.0 : hour >= 21 ? 1.0 : 0.3) + (a.Fatigue > 90 ? 1 : 0);

        bool canEat = a.Gold >= town.Market.Quote(Good.Food, 1);
        double eat = a.Hunger > 35 ? (canEat ? (a.Hunger / 100) * 1.4 + (hour == 7 || hour == 12 || hour == 18 ? 0.2 : 0) : 0.05) : 0;

        double work = 0;
        if (a.JobId != null && IsWorkHours(hour) && !Taxes.Striking(town, a))
            work = (a.Has(Trait.Lazy) ? 0.55 : 0.85) + (a.Has(Trait.Greedy) ? 0.1 : 0) + (a.Has(Trait.Ambitious) ? 0.05 : 0);

        double drink = 0;
        if (IsEvening(hour) && a.Gold >= 8 && barred != Activity.Drinking && Crowds.PlacesOf(town, Crowds.Tavern).Count > 0)
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
        double pray = barred == Activity.Praying ? 0 : Shrines.PrayScore(town, a, hour);
        double visit = barred == Activity.Visiting ? 0 : Crowds.VisitScore(town, a, hour);

        var options = new List<(Activity, double)>(8)
        {
            (Activity.Sleeping, sleep),
            (Activity.Eating, eat),
            (Activity.Working, work),
            (Activity.Drinking, drink),
            (Activity.Relaxing, relax),
            (Activity.Wandering, wander),
        };
        if (pray > 0) options.Add((Activity.Praying, pray));
        if (visit > 0) options.Add((Activity.Visiting, visit));
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
        a.TurnedFrom = null;
        int? was = a.LocationId;
        a.Activity = act;
        // A full door: the agent thinks again about the hour, and does the next best thing.
        if (!Go(town, a, act))
        {
            var next = Choose(town, a, act);
            a.Activity = next;
            if (!Go(town, a, next))
            {
                a.Activity = Activity.Relaxing;
                Go(town, a, Activity.Relaxing);
            }
        }
        if (a.LocationId != was && a.LocationId != null) Streets.Trip(town, was, a.LocationId);
    }

    /// <summary>Spend the hour on the activity; false when it was an outing and every place near was full.</summary>
    private static bool Go(Town town, Agent a, Activity act)
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
                return DoDrink(town, a);
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
                return Shrines.Pray(town, a);
            case Activity.Visiting:
                return Crowds.Visit(town, a);
        }
        return true;
    }

    private static void DoWork(Town town, Agent a)
    {
        var b = town.Building(a.JobId!.Value);
        a.LocationId = b.Id;
        if (b.IsSite)
        {
            // Out of materials: the hour goes on carrying more onto the site.
            if (Construction.Short(town, b))
            {
                a.Activity = Activity.Hauling;
                Construction.Haul(town, b, Rng.Modifier(a.Abilities.Str));
            }
            else
            {
                a.Activity = Activity.Building;
                b.Raise(town, Math.Max(0.5, 1 + 0.1 * Rng.Modifier(a.Abilities.Str)) * Seasons.Build(town) * Decrees.Build(town) * Institutions.Build(town));
            }
            if (b.OwnerId != a.Id)
            {
                double pay = Math.Min(ConstructionWage, b.LaborBudget);
                b.LaborBudget -= pay;
                a.Gold += pay;
            }
        }
        else if (b.Def.Crews > 0)
        {
            // A builders' yard's builders go out to the sites, on the treasury's wage.
            town.Book(TreasuryAccount.Works, -b.Def.Wage);
            a.Gold += b.Def.Wage;
            if (Construction.SiteFor(town, b) is not { } site) return;
            a.LocationId = site.Id;
            if (Construction.Short(town, site))
            {
                a.Activity = Activity.Hauling;
                Construction.Haul(town, site, Rng.Modifier(a.Abilities.Str));
            }
            else
            {
                a.Activity = Activity.Building;
                site.Raise(town, Math.Max(0.5, 1 + 0.1 * Rng.Modifier(a.Abilities.Str)) * Seasons.Build(town) * Decrees.Build(town) * Institutions.Build(town));
            }
        }
        else if (b.OwnerId != a.Id)
        {
            // Wages are paid by the hour out of the business account, which may go into debt.
            b.Cash -= b.Def.Wage;
            a.Gold += b.Def.Wage;
        }
    }

    private static bool DoDrink(Town town, Agent a)
    {
        // Their local, though a keeper they like is worth a longer walk (and one they don't, a shorter); on to the next if it is full.
        var from = Crowds.From(town, a);
        double keeper = town.Content.Crowds.Keeper;
        var tavern = Crowds.Enter(town, a, Crowds.PlacesOf(town, Crowds.Tavern), t => Crowds.Distance(town, t, from) - OpinionOfOwner(a, t) * keeper);
        if (tavern == null) return false;

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
        return true;
    }

    private static int OpinionOfOwner(Agent a, Building b) => b.OwnerId != null ? a.OpinionOf(b.OwnerId.Value) : 0;

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
        Crowds.Daily(town);
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
