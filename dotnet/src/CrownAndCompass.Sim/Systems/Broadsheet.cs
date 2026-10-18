namespace CrownAndCompass.Sim.Systems;

/// <summary>A story the mayor can pay the printer to run on the front page (content/game/broadsheet.json).</summary>
public sealed record SlantDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";

    /// <summary>The front-page headline; {hero} is the hero cried up.</summary>
    public string Lead { get; init; } = "";

    /// <summary>What heroes make of it (<see cref="Regard"/>).</summary>
    public List<string> Tags { get; init; } = [];

    /// <summary>Approval it wins, less by this share for each issue in a row that ran it.</summary>
    public double Mandate { get; init; }
    public double Fade { get; init; }

    /// <summary>Chance the flattery is found out, and more for each issue in a row; approval lost when it is.</summary>
    public double Exposed { get; init; }
    public double ExposedPerIssue { get; init; }
    public double ExposedMandate { get; init; }

    /// <summary>A hero on the front page: renown they gain and how much they warm to the mayor.</summary>
    public int Renown { get; init; }
    public double Regard { get; init; }
    public bool NeedsHero { get; init; }

    /// <summary>Scrutiny it puts on the most active secret society.</summary>
    public double Heat { get; init; }
    public bool NeedsSecret { get; init; }
}

public sealed record BroadsheetRules
{
    /// <summary>The paper's name; {town} is the town's.</summary>
    public string Name { get; init; } = "The {town} Crier";

    /// <summary>Days between issues, and the townsfolk a printer needs before setting up.</summary>
    public int Every { get; init; } = 7;
    public int Population { get; init; } = 40;
    public int Stories { get; init; } = 4;

    /// <summary>A slant costs this, and this much more for every townsperson.</summary>
    public double Cost { get; init; } = 25;
    public double CostPerPop { get; init; } = 0.25;

    /// <summary>The heading each kind of news runs under.</summary>
    public Dictionary<string, string> Sections { get; init; } = [];
    public List<SlantDef> Slants { get; init; } = [];

    public SlantDef? Slant(string id) => Slants.FirstOrDefault(s => s.Id == id);
}

/// <summary>A piece in an issue: its heading and its copy.</summary>
public sealed class PressStory
{
    public string Section { get; set; } = "";
    public string Text { get; set; } = "";
    public List<int> Agents { get; set; } = [];
}

/// <summary>One issue of the broadsheet.</summary>
public sealed class PressIssue
{
    public int Number { get; set; }
    public int Day { get; set; }
    public string Headline { get; set; } = "";

    /// <summary>The slant the mayor paid for, if any, and whether it was found out.</summary>
    public string Slant { get; set; } = "";
    public bool Exposed { get; set; }
    public List<PressStory> Stories { get; set; } = [];

    /// <summary>"Bread 2.1 (dear) · Ale 1.0 · …": the market column.</summary>
    public string Prices { get; set; } = "";
}

public sealed class PressState
{
    public List<PressIssue> Issues { get; } = [];

    /// <summary>The slant paid for in the next issue ("" for none), and the hero it names.</summary>
    public string Slant { get; set; } = "";
    public int SlantHero { get; set; }

    /// <summary>Issues in a row that ran the same slant.</summary>
    public int Streak { get; set; }
    public string LastSlant { get; set; } = "";
}

/// <summary>
/// The town broadsheet. Once the town is big enough a printer sets up, and every week an issue goes on sale
/// with the week's news under its headings and the market prices. The mayor can pay for a slant: praise
/// that wins approval but can be found out, a hero cried up on the front page, or questions asked about the
/// secret societies. Heroes judge the mayor for what the paper is paid to say.
/// </summary>
public static class Broadsheet
{
    private static BroadsheetRules Rules(Town town) => town.Content.Broadsheet;

    public static string Name(Town town) => Rules(town).Name.Replace("{town}", town.Name);

    /// <summary>Whether the town has a printer yet.</summary>
    public static bool Printing(Town town) => town.Press.Issues.Count > 0 || town.Population >= Rules(town).Population;

    public static PressIssue? Latest(Town town) => town.Press.Issues.Count > 0 ? town.Press.Issues[^1] : null;

    /// <summary>Day the next issue goes on sale.</summary>
    public static int NextDay(Town town)
    {
        int every = Math.Max(1, Rules(town).Every);
        return (town.Day / every + 1) * every;
    }

    public static double Price(Town town) => Js.Round(Rules(town).Cost + Rules(town).CostPerPop * town.Population);

    /// <summary>Why this slant can't be bought now, or null.</summary>
    public static string? Blocked(Town town, SlantDef s, int? heroId = null)
    {
        if (!Printing(town)) return $"the town needs {Rules(town).Population} townsfolk before a printer sets up";
        if (town.Press.Slant != "") return "the next issue's front page is already bought";
        if (town.Treasury < Price(town)) return "the treasury can't pay the printer";
        if (s.NeedsHero && (heroId is not int id || id < 1 || id > town.Agents.Count || town.Agent(id) is not { Hero: not null, Departed: false, Dead: false }))
            return "name a hero in the town's service";
        if (s.NeedsSecret && Target(town) == null) return "nobody suspects a secret society yet";
        return null;
    }

    private static FactionState? Target(Town town) =>
        town.Factions.Where(f => f.Active && f.Secret && !f.Known && f.Members.Count > 0).OrderByDescending(f => f.Heat).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>The player's lever: pay for the next issue's front page. Returns why not, or null when bought.</summary>
    public static string? Buy(Town town, string slantId, int? heroId = null)
    {
        var s = Rules(town).Slant(slantId);
        if (s == null) return "no such story";
        if (Blocked(town, s, heroId) is string why) return why;
        town.Book(TreasuryAccount.Broadsheet, -Price(town));
        town.Press.Slant = s.Id;
        town.Press.SlantHero = heroId ?? 0;
        Regard.Judge(town, s.Tags, $"paid the printer to {Lower(s.Name)}", Name(town));
        return null;
    }

    private static string Lower(string s) => s.Length > 0 ? char.ToLowerInvariant(s[0]) + s[1..] : s;

    public static void Daily(Town town, bool autoMayor)
    {
        if (!Printing(town)) return;
        if (autoMayor) Auto(town);
        if (town.Day % Math.Max(1, Rules(town).Every) == 0) Print(town);
    }

    /// <summary>Stand-in mayor: flattery when approval sags, questions when a secret society runs hot, now and then a hero.</summary>
    private static void Auto(Town town)
    {
        if (town.Press.Slant != "" || town.Treasury < Price(town) * 10 || NextDay(town) - town.Day > 1) return;
        var r = Rules(town);
        var praise = r.Slant("praise");
        var expose = r.Slant("expose");
        var hero = r.Slant("hero");
        if (praise != null && town.Mandate < 45 && !(town.Press.LastSlant == praise.Id && town.Press.Streak >= 2)) Buy(town, praise.Id);
        else if (expose != null && Target(town) is { Heat: >= 15 }) Buy(town, expose.Id);
        else if (hero != null && town.Press.Issues.Count % 4 == 3)
        {
            var best = town.Heroes.Where(a => a.Hero!.AwayUntil == null).OrderByDescending(a => a.Hero!.Renown).ThenBy(a => a.Id).FirstOrDefault();
            if (best != null) Buy(town, hero.Id, best.Id);
        }
    }

    private static void Print(Town town)
    {
        var r = Rules(town);
        var p = town.Press;
        var issue = new PressIssue { Number = p.Issues.Count + 1, Day = town.Day };
        bool first = p.Issues.Count == 0;
        var slant = r.Slant(p.Slant);
        if (slant != null)
        {
            p.Streak = p.LastSlant == slant.Id ? p.Streak + 1 : 0;
            p.LastSlant = slant.Id;
            issue.Slant = slant.Id;
            issue.Headline = Run(town, slant, issue);
        }
        else
        {
            p.Streak = 0;
            p.LastSlant = "";
        }
        issue.Stories.AddRange(News(town));
        if (issue.Headline == "") issue.Headline = issue.Stories.Count > 0 ? issue.Stories[0].Text : "A quiet week in town";
        issue.Prices = Prices(town);
        p.Issues.Add(issue);
        p.Slant = "";
        p.SlantHero = 0;
        string paper = Name(town);
        town.Log(LedgerCategory.Town, first ? Importance.Major : Importance.Notable,
            first
                ? $"A printer sets up shop, and the first issue of {paper} goes on sale: \"{Headline(issue.Headline)}\""
                : $"Issue {issue.Number} of {paper} goes on sale: \"{Headline(issue.Headline)}\"");
    }

    /// <summary>The paid piece runs: what it does, and its headline.</summary>
    private static string Run(Town town, SlantDef s, PressIssue issue)
    {
        var p = town.Press;
        string lead = s.Lead;
        if (s.Mandate > 0)
        {
            double chance = s.Exposed + s.ExposedPerIssue * p.Streak;
            if (chance > 0 && town.Rng.Chance(Math.Min(0.95, chance)))
            {
                issue.Exposed = true;
                town.Mandate = Math.Max(0, town.Mandate - s.ExposedMandate);
                town.Log(LedgerCategory.Town, Importance.Notable, $"The printer's apprentice lets slip who paid for the praise in {Name(town)}. The joke goes round every tavern, at the mayor's expense.");
            }
            else town.Mandate = Math.Min(100, town.Mandate + s.Mandate * Math.Max(0, 1 - s.Fade * p.Streak));
        }
        if (s.NeedsHero && p.SlantHero > 0 && town.Agent(p.SlantHero) is { Hero: { } h } a && !a.Departed && !a.Dead)
        {
            h.Renown += s.Renown;
            Regard.Esteem(a, s.Regard);
            lead = lead.Replace("{hero}", a.Name);
            issue.Stories.Add(new PressStory { Section = "Front page", Text = $"{a.Name}, {h.Cls.Name.ToLowerInvariant()} of level {h.Level}, is the talk of the town.", Agents = [a.Id] });
        }
        if (s.Heat > 0 && Target(town) is { } f)
        {
            f.Heat = Math.Min(100, f.Heat + s.Heat);
            Factions.CheckExposure(town, f);
        }
        return lead.Replace("{hero}", "a hero");
    }

    /// <summary>A story's first sentence, cut short if it runs long.</summary>
    public static string Headline(string text)
    {
        int stop = text.IndexOf(". ", StringComparison.Ordinal);
        if (stop > 0) text = text[..stop];
        text = text.TrimEnd('.');
        return text.Length > 90 ? text[..text.LastIndexOf(' ', 87)] + "…" : text;
    }

    /// <summary>The week's news: the biggest stories first, a few at most, one per heading.</summary>
    private static List<PressStory> News(Town town)
    {
        var r = Rules(town);
        int since = town.Tick - Math.Max(1, r.Every) * SimTime.HoursPerDay;
        var picked = new List<PressStory>();
        var seen = new HashSet<string>();
        var week = town.Ledger.Entries
            .Where(e => e.Tick >= since && e.Importance >= Importance.Notable && r.Sections.ContainsKey(e.Category.ToString()))
            .OrderByDescending(e => e.Importance)
            .ThenByDescending(e => e.Tick)
            .ThenBy(e => e.Id);
        foreach (var e in week)
        {
            string section = r.Sections[e.Category.ToString()];
            if (!seen.Add(section)) continue;
            picked.Add(new PressStory { Section = section, Text = e.Text, Agents = e.Agents.ToList() });
            if (picked.Count >= r.Stories) break;
        }
        return picked;
    }

    private static string Prices(Town town)
    {
        var parts = new List<string>();
        foreach (var g in new[] { Good.Food, Good.Ale, Good.Timber, Good.Stone })
        {
            double now = town.Market.Price(g), usual = town.Market.BasePrice(g);
            string note = now >= usual * 1.3 ? " (dear)" : now <= usual * 0.75 ? " (cheap)" : "";
            parts.Add($"{g} {Js.ToFixed(now, 1)}{note}");
        }
        return string.Join(" · ", parts);
    }
}
