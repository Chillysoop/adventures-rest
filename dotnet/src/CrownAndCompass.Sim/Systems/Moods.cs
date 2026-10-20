namespace CrownAndCompass.Sim.Systems;

/// <summary>Numbers for townsfolk's inspirations and breaks (content/game/moods.json).</summary>
public sealed record MoodRules
{
    /// <summary>A day's mood at or over this, for this many days in a row, may bring an inspiration.</summary>
    public double High { get; init; } = 82;
    public int HighDays { get; init; } = 4;

    /// <summary>A day's mood under this, for this many days in a row, may bring a break.</summary>
    public double Low { get; init; } = 52;
    public int LowDays { get; init; } = 2;

    /// <summary>Daily chance for each townsperson in the streak (before the town and their traits).</summary>
    public double Inspire { get; init; } = 0.006;
    public double Break { get; init; } = 0.05;

    /// <summary>A well-run town: inspirations × (1 + (mandate − pivot) × per), breaks divided by it; unrest adds to breaks and takes from inspirations.</summary>
    public double MandatePivot { get; init; } = 60;
    public double MandatePer { get; init; } = 0.015;
    public double UnrestPer { get; init; } = 0.01;

    /// <summary>Days before the same person has another, the most of each kind in a day, and days between Notable lines of each kind.</summary>
    public int Cooldown { get; init; } = 30;
    public int PerDay { get; init; } = 1;
    public int NotableGap { get; init; } = 3;

    /// <summary>Moments each person's inspector keeps, and hours the bubble shows over an episode with no days of its own.</summary>
    public int Keep { get; init; } = 4;
    public int ShowHours { get; init; } = 14;

    /// <summary>Multipliers on the chances by trait (trait names as in <see cref="Trait"/>).</summary>
    public Dictionary<string, double> InspireTraits { get; init; } = [];
    public Dictionary<string, double> BreakTraits { get; init; } = [];
    public BladeNames Blade { get; init; } = new();
    public List<MoodDef> Inspirations { get; init; } = [];
    public List<MoodDef> Breaks { get; init; } = [];

    public MoodDef? Of(string id) => Inspirations.FirstOrDefault(d => d.Id == id) ?? Breaks.FirstOrDefault(d => d.Id == id);
}

/// <summary>A forged blade's name, first half and second ("Ember" + "fang").</summary>
public sealed record BladeNames
{
    public List<string> First { get; init; } = [];
    public List<string> Last { get; init; } = [];
}

/// <summary>One inspiration or break: who it comes to, what it does and what the town hears of it.</summary>
public sealed record MoodDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>What it does: blade, feast, decor, song, insight, harvest, zeal; bender, tantrum, brawl, sulk, walkout.</summary>
    public string Effect { get; init; } = "";

    /// <summary>The trades it comes to (an inspiration); none for any trade.</summary>
    public List<string> Professions { get; init; } = [];

    /// <summary>A break's weight among the others, and its multipliers by trait.</summary>
    public double Weight { get; init; } = 1;
    public Dictionary<string, double> Traits { get; init; } = [];

    /// <summary>How long it holds them (days, else hours), which is how long its bubble shows.</summary>
    public int Days { get; init; }
    public int Hours { get; init; }

    /// <summary>A blade: the library item, and the better one a masterful smith makes.</summary>
    public string Item { get; init; } = "";
    public string Master { get; init; } = "";

    /// <summary>A feast: mood to its ward for its days. A song or good cheer: mandate, and a song's loyalty to the hero it is of.</summary>
    public double Mood { get; init; }
    public double Mandate { get; init; }
    public double Loyalty { get; init; }

    /// <summary>A flourish: the decoration carved.</summary>
    public string Decor { get; init; } = "";

    /// <summary>An insight: days of the scholars' lore it is worth.</summary>
    public double LoreDays { get; init; }

    /// <summary>A harvest: the good and how much goes to market.</summary>
    public Good Good { get; init; } = Good.Food;
    public double Qty { get; init; }

    /// <summary>Good cheer: loneliness taken from them and their household.</summary>
    public double Lonely { get; init; }

    /// <summary>A bender: ale drunk from the tavern's cellar. A tantrum: gold of damage, and the share of a stock spoiled.</summary>
    public double Ale { get; init; }
    public int[] Gold { get; init; } = [5, 15];
    public double Stock { get; init; }

    /// <summary>A walk-out: days of low mood it takes.</summary>
    public int MinDays { get; init; }

    /// <summary>What they say over their head while it holds them, the line their inspector keeps, and the Ledger's.</summary>
    public List<string> Emote { get; init; } = [];
    public string Moment { get; init; } = "";
    public string Text { get; init; } = "";
}

/// <summary>The town's side of inspirations and breaks.</summary>
public sealed class MoodState
{
    /// <summary>The day the Ledger last told of an inspiration, and of a break, as news.</summary>
    public int NotedInspired { get; set; } = -99;
    public int NotedBroke { get; set; } = -99;

    /// <summary>The kind of inspiration last told as news.</summary>
    public string NotedKind { get; set; } = "";

    public int Inspirations { get; set; }
    public int Breaks { get; set; }

    /// <summary>Wards feasting, and the tick each feast's cheer runs out.</summary>
    public OrderedMap<string, int> Feasts { get; } = new();

    /// <summary>Names given to forged blades, so no two share one.</summary>
    public List<string> Blades { get; } = [];
}

/// <summary>
/// Inspirations and breaks, after the way colonists in RimWorld snap one way or the other. A townsperson whose mood
/// has stayed high for days may be struck by an inspiration that fits their trade: a smith forges a named blade for the
/// armory, a barkeep throws a feast for the ward, a mason carves a column, a herald's song lifts the mayor and a hero,
/// a scholar's insight speeds the research, a farmer brings in a bumper harvest. One whose mood stays low may break: a
/// bender at the tavern, a tantrum at work, a brawl, sulking at home, or after a long low walking out of town for good.
/// A well-run town (high mandate, no unrest) sees more of the first and fewer of the second. None of them stops the clock.
/// </summary>
public static class Moods
{
    public static MoodRules Rules(Town town) => town.Content.Moods;

    /// <summary>The episode holding them now, or null.</summary>
    public static MoodDef? Episode(Town town, Agent a) =>
        a.Episode != "" && town.Tick < a.EpisodeUntil ? Rules(town).Of(a.Episode) : null;

    public static bool IsInspiration(Town town, MoodDef d) => Rules(town).Inspirations.Contains(d);

    /// <summary>
    /// The dice for one person's moods today: seeded from the town's seed, the hour and the person, so they are as
    /// deterministic as the town's own (and need no saving), but rolling them leaves the town's stream where it was. A
    /// campaign plays the same with or without the townsfolk's moods until one of them actually does something.
    /// </summary>
    private static Rng Dice(Town town, Agent a) => new(unchecked(town.Seed * 7919 + town.Tick * 104729 + a.Id * 31 + a.Moments.Count * 7 + 13));

    /// <summary>Mood a ward's feast gives its households this hour.</summary>
    public static double Feast(Town town, string wardId)
    {
        if (town.Moods.Feasts.Count == 0 || !town.Moods.Feasts.TryGetValue(wardId, out int until) || town.Tick >= until) return 0;
        return Rules(town).Inspirations.FirstOrDefault(d => d.Effect == "feast")?.Mood ?? 0;
    }

    /// <summary>What a break makes of their hour: a bender keeps them at the tavern by day, sulking at home; null for no say.</summary>
    public static Activity? Holds(Town town, Agent a)
    {
        if (a.Episode == "" || town.Tick >= a.EpisodeUntil) return null;
        return Rules(town).Of(a.Episode)?.Effect switch
        {
            "bender" => town.Taverns.Count > 0 ? Activity.Drinking : Activity.Relaxing,
            "sulk" => Activity.Relaxing,
            _ => null,
        };
    }

    private static double TraitFactor(Agent a, Dictionary<string, double> traits)
    {
        double f = 1;
        foreach (var t in TraitOrder.All)
            if (a.Has(t) && traits.TryGetValue(t.ToString(), out double m)) f *= m;
        return f;
    }

    /// <summary>How well the town is run, as a multiplier on inspirations (breaks are divided by it).</summary>
    public static double WellRun(Town town)
    {
        var r = Rules(town);
        return Math.Clamp(1 + (town.Mandate - r.MandatePivot) * r.MandatePer, 0.4, 1.6);
    }

    public static void Daily(Town town)
    {
        var r = Rules(town);
        var ms = town.Moods;
        foreach (var w in ms.Feasts.Keys.ToList())
            if (town.Tick >= ms.Feasts[w]) ms.Feasts.Remove(w);
        if (r.Inspirations.Count == 0 || town.Outcome != null) return;
        var st = Storyteller.Of(town);
        double well = WellRun(town), unrest = Math.Clamp(town.Taxes.Unrest * r.UnrestPer, 0, 1);
        int inspired = 0, broke = 0;
        foreach (var a in town.Workers)
        {
            if (a.Episode != "" && town.Tick >= a.EpisodeUntil) a.Episode = "";
            a.HighDays = a.LastDayMood >= r.High ? a.HighDays + 1 : 0;
            a.LowDays = a.LastDayMood < r.Low ? a.LowDays + 1 : 0;
            if (a.Departed || a.Age < 18 || a.Episode != "" || town.Day - a.EpisodeDay < r.Cooldown) continue;
            if (a.HighDays >= r.HighDays && inspired < r.PerDay)
            {
                var dice = Dice(town, a);
                if (dice.Chance(r.Inspire * well * (1 - unrest) * st.Inspire * TraitFactor(a, r.InspireTraits)) && Inspire(town, a, null, dice)) inspired++;
            }
            else if (a.LowDays >= r.LowDays && broke < r.PerDay)
            {
                var dice = Dice(town, a);
                if (dice.Chance(r.Break / well * (1 + unrest) * st.Breaks * TraitFactor(a, r.BreakTraits)) && Break(town, a, null, dice)) broke++;
            }
        }
    }

    // Inspirations -------------------------------------------------------------------------------

    /// <summary>The trade they work at: their workplace's, else the one they came to town with.</summary>
    private static string Trade(Town town, Agent a) =>
        a.JobId is int j && town.Building(j).Def.Profession is string p ? p : a.Profession;

    /// <summary>Strike them with the inspiration that fits their trade (good cheer if nothing else does). Tests call it directly.</summary>
    public static bool Inspire(Town town, Agent a, string? id = null, Rng? dice = null)
    {
        var r = Rules(town);
        dice ??= Dice(town, a);
        string trade = Trade(town, a);
        var fits = id != null
            ? r.Inspirations.Where(d => d.Id == id).ToList()
            : r.Inspirations.Where(d => d.Professions.Contains(trade)).ToList();
        foreach (var d in fits.Concat(r.Inspirations.Where(d => d.Professions.Count == 0)))
        {
            var said = Apply(town, a, d, dice);
            if (said == null) continue;
            Begin(town, a, d, said.Value.Moment);
            // News a few days apart, and never the same kind twice running: the rest go to the full Ledger.
            var imp = town.Day - town.Moods.NotedInspired >= r.NotableGap && d.Professions.Count > 0 && d.Id != town.Moods.NotedKind ? Importance.Notable : Importance.Routine;
            if (imp == Importance.Notable)
            {
                town.Moods.NotedInspired = town.Day;
                town.Moods.NotedKind = d.Id;
            }
            town.Moods.Inspirations++;
            town.Log(LedgerCategory.Social, imp, said.Value.Text, [a.Id]);
            return true;
        }
        return false;
    }

    /// <summary>Do what the inspiration does; the moment and the Ledger's line, or null if it can't here.</summary>
    private static (string Moment, string Text)? Apply(Town town, Agent a, MoodDef d, Rng dice)
    {
        var work = a.JobId is int j ? town.Building(j) : null;
        string ward = Ward(town, a) is { } w ? City.Ward(town, w).Name : town.Name;
        var vars = new List<(string, string)> { ("{work}", work?.Name ?? "the workshop"), ("{ward}", ward) };
        switch (d.Effect)
        {
            case "blade":
            {
                var it = town.Content.Library.Item(Skills.Practised(town, a) >= town.Content.Skills.Townsfolk.PracticeMax && d.Master != "" ? d.Master : d.Item);
                if (it == null || work == null) return null;
                string blade = BladeName(town, dice);
                town.Moods.Blades.Add(blade);
                var s = Armory.Add(town, it, $"forged by {a.Name} in a fit of inspiration");
                s.Name = blade;
                vars.Add(("{blade}", blade));
                break;
            }
            case "feast":
            {
                if (Ward(town, a) is not string w2) return null;
                town.Moods.Feasts[w2] = town.Tick + Math.Max(1, d.Days) * SimTime.HoursPerDay;
                break;
            }
            case "decor":
            {
                if (work == null) return null;
                var def = Decor.Rules(town).Piece(d.Decor);
                var (x, y) = Decor.PosOf(town, work);
                if (Decor.Blocked(town, def) != null || Decor.Spot(town, def, x, y, spareLand: true, within: 3) is not (double px, double py)) return null;
                double cost = Decor.Cost(town, def);
                if (Decor.Place(town, def.Id, px, py, dice.Next() * Math.PI * 2, quiet: true) == null) return null;
                // The mason gives the work; the town pays nothing.
                town.Book(TreasuryAccount.Decorations, cost);
                break;
            }
            case "song":
            {
                var hero = town.Heroes.Where(h => !h.Dead).OrderByDescending(h => h.Hero!.Renown).ThenBy(h => h.Id).FirstOrDefault();
                town.Mandate = Math.Min(100, town.Mandate + d.Mandate);
                if (hero != null) Loyalty.Sway(hero, d.Loyalty);
                vars.Add(("{hero}", hero?.Name ?? $"the founders of {town.Name}"));
                break;
            }
            case "insight":
            {
                if (town.Research.Study is not string study) return null;
                var st = town.Research;
                st.Progress[study] = st.Progress.Get(study, 0) + d.LoreDays * Research.Lore(town);
                string name = Research.Def(town, study).Name;
                vars.Add(("{study}", name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..]));
                break;
            }
            case "harvest":
            {
                if (work == null) return null;
                double gold = town.Market.Sell(d.Good, d.Qty);
                if (work.OwnerId == null || work.OwnerId == a.Id) a.Gold += gold;
                else work.Cash += gold;
                vars.Add(("{qty}", Js.Str(d.Qty)));
                break;
            }
            case "zeal":
            {
                town.Mandate = Math.Min(100, town.Mandate + d.Mandate);
                a.Loneliness = Math.Max(0, a.Loneliness - d.Lonely);
                if (a.SpouseId is int sp) town.Agent(sp).Loneliness = Math.Max(0, town.Agent(sp).Loneliness - d.Lonely);
                break;
            }
            default:
                return null;
        }
        return (Fill(town, a, d.Moment, vars), Fill(town, a, d.Text, vars));
    }

    /// <summary>A name for a forged blade that no other in town has.</summary>
    private static string BladeName(Town town, Rng dice)
    {
        var b = Rules(town).Blade;
        for (int i = 0; i < 20; i++)
        {
            string name = dice.Pick(b.First) + dice.Pick(b.Last);
            if (!town.Moods.Blades.Contains(name)) return name;
        }
        return $"{dice.Pick(b.First)}{dice.Pick(b.Last)} the {Ordinal(town.Moods.Blades.Count + 1)}";
    }

    private static string Ordinal(int n) => n switch { 2 => "Second", 3 => "Third", 4 => "Fourth", 5 => "Fifth", _ => $"{n}th" };

    // Breaks -------------------------------------------------------------------------------------

    /// <summary>Break them, weighing the breaks that can happen here by their traits. Tests call it directly with an id.</summary>
    public static bool Break(Town town, Agent a, string? id = null, Rng? dice = null)
    {
        var r = Rules(town);
        dice ??= Dice(town, a);
        var fits = r.Breaks.Where(d => (id == null || d.Id == id) && Can(town, a, d)).ToList();
        if (fits.Count == 0) return false;
        double total = Js.Sum(fits, d => d.Weight * TraitFactor(a, d.Traits));
        double roll = dice.Next() * total;
        var pick = fits[^1];
        foreach (var d in fits)
        {
            roll -= d.Weight * TraitFactor(a, d.Traits);
            if (roll < 0)
            {
                pick = d;
                break;
            }
        }
        var said = Snap(town, a, pick, dice);
        if (said == null) return false;
        Begin(town, a, pick, said.Value.Moment);
        town.Moods.Breaks++;
        if (said.Value.Text != "")
        {
            // A walk-out is always news; the others are news a few days apart.
            var imp = pick.Effect == "walkout" || town.Day - town.Moods.NotedBroke >= r.NotableGap ? Importance.Notable : Importance.Routine;
            if (imp == Importance.Notable) town.Moods.NotedBroke = town.Day;
            town.Log(pick.Effect == "walkout" ? LedgerCategory.Departure : LedgerCategory.Social, imp, said.Value.Text, [a.Id]);
        }
        return true;
    }

    private static bool Can(Town town, Agent a, MoodDef d) => d.Effect switch
    {
        "bender" or "brawl" => town.Taverns.Count > 0,
        "tantrum" => a.JobId != null || a.HomeId != null,
        "sulk" => a.HomeId != null,
        "walkout" => a.LowDays >= d.MinDays,
        _ => false,
    };

    private static (string Moment, string Text)? Snap(Town town, Agent a, MoodDef d, Rng rng)
    {
        var work = a.JobId is int j ? town.Building(j) : null;
        var home = a.HomeId is int h ? town.Building(h) : null;
        var vars = new List<(string, string)>
        {
            ("{work}", work?.Name ?? home?.Name ?? "the house"),
            ("{home}", home?.Name ?? "the stables"),
            ("{instead}", work != null ? $" instead of turning up at {work.Name}" : ""),
            ("{short}", work != null ? $", and {work.Name} goes a pair of hands short" : ""),
        };
        switch (d.Effect)
        {
            case "bender":
            {
                var tavern = town.Taverns.OrderByDescending(t => t.StockOf(Good.Ale)).ThenBy(t => t.Id).First();
                double ale = Math.Min(d.Ale, tavern.StockOf(Good.Ale));
                tavern.AddStock(Good.Ale, -ale);
                double paid = Math.Min(a.Gold, ale * town.Market.Price(Good.Ale));
                a.Gold -= paid;
                tavern.Cash += paid;
                vars.Add(("{tavern}", tavern.Name));
                break;
            }
            case "tantrum":
            {
                var b = work ?? home!;
                double gold = rng.Range(d.Gold[0], d.Gold[1] + 1);
                var spoiled = b.Stock.Keys.Where(g => b.StockOf(g) >= 2).OrderByDescending(g => b.StockOf(g)).ThenBy(g => g).FirstOrDefault();
                string damage;
                double lost = b.Stock.Count > 0 ? Math.Floor(b.StockOf(spoiled) * d.Stock) : 0;
                if (lost >= 1)
                {
                    b.AddStock(spoiled, -lost);
                    damage = $"{Js.Str(lost)} {spoiled.ToString().ToLowerInvariant()} ruined and {Js.Str(gold)} gold of broken tools";
                }
                else damage = $"{Js.Str(gold)} gold of broken pots and shutters";
                if (b.Def.IsBusiness && b.OwnerId != a.Id) b.Cash -= gold;
                else a.Gold = Math.Max(0, a.Gold - gold);
                vars[0] = ("{work}", b.Name);
                vars.Add(("{damage}", damage));
                break;
            }
            case "brawl":
            {
                var tavern = town.Taverns.OrderBy(t => t.Id).First();
                var foe = town.Residents
                    .Where(x => x.Id != a.Id && !x.IsAway && x.Age >= 18 && x.Hero == null)
                    .OrderBy(x => a.OpinionOf(x.Id))
                    .ThenBy(x => x.Id)
                    .FirstOrDefault();
                if (foe == null) return null;
                vars.Add(("{tavern}", tavern.Name));
                Social.Brawl(town, tavern, a, foe);
                break;
            }
            case "sulk":
                break;
            case "walkout":
            {
                string with = "";
                Population.Depart(town, a);
                if (a.SpouseId is int sp && !town.Agent(sp).Departed)
                {
                    Population.Depart(town, town.Agent(sp));
                    with = $", and {town.Agent(sp).GivenName} goes too";
                }
                vars.Add(("{with}", with));
                vars.Add(("{tools}", work != null ? "the keys of " + work.Name : "everything"));
                break;
            }
            default:
                return null;
        }
        return (Fill(town, a, d.Moment, vars), Fill(town, a, d.Text, vars));
    }

    // Shared -------------------------------------------------------------------------------------

    /// <summary>The ward they live in (or work in), or null.</summary>
    private static string? Ward(Town town, Agent a)
    {
        int? at = a.HomeId ?? a.JobId;
        return at is int b ? City.WardOf(town, town.Lot(town.Building(b).LotId)).Id : null;
    }

    private static void Begin(Town town, Agent a, MoodDef d, string moment)
    {
        var r = Rules(town);
        a.Episode = d.Id;
        a.EpisodeUntil = town.Tick + (d.Days > 0 ? d.Days * SimTime.HoursPerDay : d.Hours > 0 ? d.Hours : r.ShowHours);
        a.EpisodeDay = town.Day;
        a.HighDays = 0;
        a.LowDays = 0;
        a.Moments.Add($"Day {town.Day}: {moment}");
        while (a.Moments.Count > r.Keep) a.Moments.RemoveAt(0);
    }

    private static string Fill(Town town, Agent a, string text, List<(string Key, string Value)> vars)
    {
        foreach (var (k, v) in vars) text = text.Replace(k, v);
        return text.Replace("{name}", a.Name).Replace("{given}", a.GivenName).Replace("{town}", town.Name);
    }
}
