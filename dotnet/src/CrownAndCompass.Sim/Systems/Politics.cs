namespace CrownAndCompass.Sim.Systems;

/// <summary>One level of a policy (politics.json "policies"): its tags, its daily cost and what it does.</summary>
public sealed record PolicyLevel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
    public List<string> Tags { get; init; } = [];

    /// <summary>Gold a day from the treasury, plus per resident.</summary>
    public double Gold { get; init; }
    public double PerResident { get; init; }

    /// <summary>Share of would-be offenders it keeps home, mood for everyone, the temple god's favour a day, and mood for the poor.</summary>
    public double Deter { get; init; }
    public double Mood { get; init; }
    public double Favour { get; init; }
    public double Relief { get; init; }
}

/// <summary>A law with levels (curfew, tithe, dole), set like a slider.</summary>
public sealed record PolicyDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
    public string Text { get; init; } = "";
    public List<PolicyLevel> Levels { get; init; } = [];

    public PolicyLevel? Level(string id) => Levels.FirstOrDefault(l => l.Id == id);
}

/// <summary>A form of government: who seats the council, how the mayor holds office, what goes to the town vote.</summary>
public sealed record FormDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";

    /// <summary>"Democratic", "Chartered" or "Authoritarian", and how free the town is, -2 to 2.</summary>
    public string Kind { get; init; } = "";
    public int Liberty { get; init; }
    public string Text { get; init; } = "";
    public List<string> Tags { get; init; } = [];

    /// <summary>"appointed" (the ablest), "elected" (the moot), "gentry" (the richest) or "none" (no council).</summary>
    public string Seats { get; init; } = "appointed";

    /// <summary>Blocs with no vote at the moot.</summary>
    public List<string> Barred { get; init; } = [];

    /// <summary>"chartered", "elected" (stands at the moot) or "crowned".</summary>
    public string Mayor { get; init; } = "chartered";

    /// <summary>Matters it puts to the town vote, and whether the mayor may put any.</summary>
    public List<string> Votes { get; init; } = [];
    public bool AllowVotes { get; init; } = true;

    /// <summary>Times the Mandate a decree costs, and a law change; times the radicals' growth; Mandate a day; times the watch's strength; times the dole's cost.</summary>
    public double DecreeTimes { get; init; } = 1;
    public double LawTimes { get; init; } = 1;
    public double Radicals { get; init; } = 1;
    public double Mandate { get; init; }
    public double Watch { get; init; } = 1;
    public double Dole { get; init; } = 1;

    public bool Elects => Seats == "elected" || Mayor == "elected";
}

/// <summary>A matter that can be put to the town vote, and the laws it covers (by kind: "tax", "punish", "policy"...).</summary>
public sealed record MatterDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
    public string Text { get; init; } = "";
    public List<string> Laws { get; init; } = [];
}

public sealed record TaxLean
{
    public double Neutral { get; init; } = 0.15;
    public double Per { get; init; } = 0.05;
    public List<string> Low { get; init; } = [];
    public List<string> High { get; init; } = [];
}

public sealed record VoteRules
{
    public int Days { get; init; } = 3;
    public double Per { get; init; } = 0.05;
    public double Feeling { get; init; } = 0.003;
    public double Won { get; init; } = 2;
    public double Lost { get; init; } = 1;
}

public sealed record MootRules
{
    public int Candidates { get; init; } = 3;
    public double MandateShare { get; init; } = 0.5;
    public double Keep { get; init; } = 0.5;
    public int WarnDays { get; init; } = 10;
    public double Loyalty { get; init; } = 0.4;
    public double Won { get; init; } = 6;
}

public sealed record CouncillorRules
{
    public double Least { get; init; } = 0.2;
    public double Loyalty { get; init; } = 0.2;
    public double Lean { get; init; } = 1.5;
}

/// <summary>A lasting state of the town that starts when a reading crosses Start and ends below End.</summary>
public sealed record SituationDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";

    /// <summary>crime, homeless, bread, sick, strike or unrest.</summary>
    public string Reading { get; init; } = "";
    public double Start { get; init; }
    public double End { get; init; }

    /// <summary>Blocs that like these tags are hurt while it lasts.</summary>
    public List<string> Hurts { get; init; } = [];

    /// <summary>Unrest a day while it lasts.</summary>
    public double Unrest { get; init; }

    /// <summary>What the reading is, for its card; and the Ledger lines ({town}).</summary>
    public string Fed { get; init; } = "";
    public string Began { get; init; } = "";
    public string Ended { get; init; } = "";
}

public sealed record TrustRules
{
    public double Start { get; init; } = 50;
    public double Ease { get; init; } = 0.01;
    public double Weight { get; init; } = 0.3;
}

public sealed record DemandRules
{
    public int Every { get; init; } = 15;
    public int Again { get; init; } = 60;
    public double AskBelow { get; init; } = -15;
    public int AskDays { get; init; } = 5;
    public int DueDays { get; init; } = 20;
    public double Promise { get; init; } = 4;
    public double Kept { get; init; } = 25;
    public double KeptTrust { get; init; } = 8;
    public double Broken { get; init; } = 20;
    public double BrokenTrust { get; init; } = 15;
    public double Refused { get; init; } = 4;
    public string Asks { get; init; } = "";
    public string KeptLine { get; init; } = "";
    public string BrokenLine { get; init; } = "";
}

public sealed record RadicalRules
{
    public double Furious { get; init; } = -50;
    public double Calm { get; init; } = -20;
    public int FormDays { get; init; } = 10;
    public int CalmDays { get; init; } = 7;
    public int Every { get; init; } = 8;
    public double Catch { get; init; } = 0.6;
    public double Graffiti { get; init; } = 3;
    public double Riot { get; init; } = 12;
    public double Sabotage { get; init; } = 0.06;
    public double SabotageMost { get; init; } = 150;
    public string Formed { get; init; } = "";
    public List<string> Acts { get; init; } = [];
    public string Foiled { get; init; } = "";
    public string Missed { get; init; } = "";
    public string Faded { get; init; } = "";
}

public sealed record PoliticsRules
{
    /// <summary>Feeling per tag a law in force carries, and the most laws can move a bloc either way.</summary>
    public double Step { get; init; } = 6;
    public double Most { get; init; } = 30;

    /// <summary>Mandate a law change costs, per point for taxes; doubled per change within FlipDays; days a change phases in over.</summary>
    public double Change { get; init; } = 4;
    public double TaxChange { get; init; } = 1;
    public double TaxMost { get; init; } = 8;
    public int FlipDays { get; init; } = 30;
    public double FlipTimes { get; init; } = 2;
    public int PhaseDays { get; init; } = 10;

    /// <summary>Tags of the laws the town already had: "gate:shut", "punish:stocks", "trial:zone-of-truth".</summary>
    public Dictionary<string, List<string>> Laws { get; init; } = [];

    /// <summary>How an ask names a change ("gate:shut", "policy:curfew:dusk", "tax" with {rate}, "punish" with {punishment} and {crime}).</summary>
    public Dictionary<string, string> Say { get; init; } = [];
    public TaxLean Taxes { get; init; } = new();
    public List<PolicyDef> Policies { get; init; } = [];
    public List<FormDef> Forms { get; init; } = [];

    /// <summary>Mandate a change of government costs, and days before it may change again.</summary>
    public double FormChange { get; init; } = 25;
    public int FormGapDays { get; init; } = 60;
    public List<MatterDef> Matters { get; init; } = [];

    /// <summary>Mandate putting a matter to the town vote, or taking it back, costs.</summary>
    public double MatterChange { get; init; } = 6;
    public VoteRules Vote { get; init; } = new();
    public MootRules Moot { get; init; } = new();
    public CouncillorRules Councillors { get; init; } = new();

    /// <summary>Tags of each council motion, for the councillors' blocs.</summary>
    public Dictionary<string, List<string>> Motions { get; init; } = [];
    public List<SituationDef> Situations { get; init; } = [];
    public TrustRules Trust { get; init; } = new();
    public DemandRules Demands { get; init; } = new();
    public RadicalRules Radicals { get; init; } = new();

    public PolicyDef? Policy(string id) => Policies.FirstOrDefault(p => p.Id == id);
    public FormDef? Form(string id) => Forms.FirstOrDefault(f => f.Id == id);
    public MatterDef? Matter(string id) => Matters.FirstOrDefault(m => m.Id == id);
}

/// <summary>The last change to a law: what it was before, when, and how many changes ran close together.</summary>
public sealed class LawChange
{
    public string From { get; set; } = "";
    public int Day { get; set; }
    public int Times { get; set; }

    /// <summary>For taxes, which way the last change went.</summary>
    public int Dir { get; set; }
}

/// <summary>A law change put to the town vote.</summary>
public sealed class Referendum
{
    public int Id { get; set; }
    public string Law { get; set; } = "";
    public string Option { get; set; } = "";
    public int Day { get; set; }
    public int Due { get; set; }

    /// <summary>The share that voted for it, once counted (-1 while open).</summary>
    public double Yes { get; set; } = -1;
}

/// <summary>A bloc's ask of the mayor: a law change, and whether the mayor promised it and by when.</summary>
public sealed class Demand
{
    public int Id { get; set; }
    public string Bloc { get; set; } = "";
    public string Law { get; set; } = "";
    public string Option { get; set; } = "";
    public int Day { get; set; }
    public bool Promised { get; set; }
    public int Due { get; set; }
}

/// <summary>Radicals sprung from a furious bloc: how far they have gone and when they act next.</summary>
public sealed class Cell
{
    public string Bloc { get; set; } = "";
    public int Stage { get; set; }
    public int Formed { get; set; }
    public int Next { get; set; }
}

public sealed class PoliticsState
{
    public string Form { get; set; } = "charter";
    public int FormDay { get; set; }

    /// <summary>Policy id to level id; a policy not here is at its first level.</summary>
    public Dictionary<string, string> Policies { get; } = new();

    /// <summary>Matters put to the town vote beyond the form's own; and the form's the mayor took back.</summary>
    public List<string> Votes { get; } = [];
    public List<string> Unvoted { get; } = [];
    public Dictionary<string, LawChange> Changes { get; } = new();
    public List<Referendum> Referenda { get; } = [];
    public int NextReferendum { get; set; } = 1;

    /// <summary>Situations in force, and the day each began.</summary>
    public Dictionary<string, int> Situations { get; } = new();
    public double Trust { get; set; } = 50;
    public List<Demand> Demands { get; } = [];
    public int NextDemand { get; set; } = 1;
    public int AskDay { get; set; }

    /// <summary>The day each ask ("bloc|law|option") was last turned away.</summary>
    public Dictionary<string, int> Refused { get; } = new();

    /// <summary>Days each bloc has been furious, or calm while it has a cell.</summary>
    public Dictionary<string, int> Furious { get; } = new();
    public Dictionary<string, int> Calm { get; } = new();
    public List<Cell> Cells { get; } = [];

    /// <summary>Moots held, and what the mayor polled at the last.</summary>
    public int Moots { get; set; }
    public int MootDay { get; set; }
    public double Share { get; set; } = -1;

    [field: NotSaved] public int CacheDay { get; set; } = -1;
    [field: NotSaved] public Dictionary<string, double> Standing { get; } = new();
    [field: NotSaved] public Dictionary<string, double> Sizes { get; } = new();
}

/// <summary>
/// Town politics after Democracy 4. Every law in force carries tags, and each bloc warms or cools toward the mayor by
/// how it likes them (<see cref="Standing"/>), phasing in over days after a change; changing a law costs Mandate, more
/// for flip-flopping. The town's form of government (a charter, a free town, a commune, a crown, iron rule) says who
/// seats the council, whether the mayor stands at the yearly moot, what goes to a town vote and how dear a decree is.
/// Situations (a crime wave, slums, bread riots) start and end on the town's numbers and hurt the blocs that care. A
/// bloc that feels the worst asks the mayor for a change, which the mayor can promise; kept or broken, the town's trust
/// follows. A bloc furious too long breeds radicals, who escalate to an attempt on the mayor. Its rolls use its own
/// dice, never <see cref="Town.Rng"/>.
/// </summary>
public static class Politics
{
    public static PoliticsRules Rules(Town town) => town.Content.Politics;

    public static FormDef Form(Town town) => Rules(town).Form(town.Politics.Form) ?? Rules(town).Forms.FirstOrDefault() ?? new FormDef();

    /// <summary>The blocs that speak, from the blocs.json list.</summary>
    public static List<BlocDef> Blocs(Town town) => Systems.Blocs.Speaking(town);

    // ---------- Laws ----------

    /// <summary>A policy's level in force.</summary>
    public static PolicyLevel Level(Town town, PolicyDef p) => p.Level(town.Politics.Policies.GetValueOrDefault(p.Id, "")) ?? p.Levels[0];

    public static PolicyLevel? Level(Town town, string policy) => Rules(town).Policy(policy) is { } p ? Level(town, p) : null;

    /// <summary>Every law key the town has: tax, gate, service, trial, punish:&lt;crime&gt;, policy:&lt;id&gt;, form.</summary>
    public static IEnumerable<string> Keys(Town town)
    {
        yield return "tax";
        yield return "gate";
        yield return "service";
        yield return "trial";
        foreach (var c in town.Content.Laws.Crimes) yield return "punish:" + c.Id;
        foreach (var p in Rules(town).Policies) yield return "policy:" + p.Id;
        yield return "form";
    }

    /// <summary>What a law stands at now ("shut", "stocks", "0.15").</summary>
    public static string Current(Town town, string law) => law switch
    {
        "tax" => Js.Str(Js.Round(town.TaxRate * 100) / 100),
        "gate" => town.Frontier.Gate,
        "service" => town.Army.Service.ToString().ToLowerInvariant(),
        "trial" => town.TrialMagic,
        "form" => town.Politics.Form,
        _ when law.StartsWith("punish:", StringComparison.Ordinal) => Laws.For(town, law[7..]).Id,
        _ when law.StartsWith("policy:", StringComparison.Ordinal) => Level(town, law[7..])?.Id ?? "",
        _ => "",
    };

    /// <summary>The kind of a law key, for the matters ("punish", "policy", "tax"...).</summary>
    public static string Kind(string law) => law.Contains(':') ? law[..law.IndexOf(':')] : law;

    /// <summary>The tags a law carries at an option, each counted (a tax far from the usual counts its tags more than once, in part).</summary>
    public static List<(string Tag, double Weight)> Tags(Town town, string law, string option)
    {
        var r = Rules(town);
        var list = new List<(string, double)>();
        void Add(IEnumerable<string>? tags, double w)
        {
            if (tags != null && w != 0) foreach (var t in tags) list.Add((t, w));
        }
        switch (Kind(law))
        {
            case "tax":
                double rate = double.TryParse(option, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : town.TaxRate;
                double s = Math.Clamp((r.Taxes.Neutral - rate) / Math.Max(0.001, r.Taxes.Per), -2, 2);
                Add(s > 0 ? r.Taxes.Low : r.Taxes.High, Math.Abs(s));
                break;
            case "policy":
                Add(r.Policy(law[7..])?.Level(option)?.Tags, 1);
                break;
            case "form":
                Add(r.Form(option)?.Tags, 1);
                break;
            case "punish":
                Add(r.Laws.GetValueOrDefault("punish:" + option), 1);
                break;
            default:
                Add(r.Laws.GetValueOrDefault(law + ":" + option), 1);
                break;
        }
        return list;
    }

    /// <summary>How a law at an option moves a bloc.</summary>
    public static double Moves(Town town, BlocDef b, string law, string option)
    {
        double sum = 0;
        foreach (var (tag, w) in Tags(town, law, option))
            sum += w * ((b.Likes.Contains(tag) ? 1 : 0) - (b.Dislikes.Contains(tag) ? 1 : 0));
        return sum * Rules(town).Step;
    }

    /// <summary>How a change of a law moves a bloc: the new option's pull less the old's.</summary>
    public static double Reaction(Town town, BlocDef b, string law, string option) =>
        Moves(town, b, law, option) - Moves(town, b, law, Current(town, law));

    /// <summary>The speaking blocs a change pleases and angers.</summary>
    public static (List<BlocDef> Pleased, List<BlocDef> Angered) Reactions(Town town, string law, string option)
    {
        var pleased = new List<BlocDef>();
        var angered = new List<BlocDef>();
        foreach (var b in Blocs(town))
        {
            double v = Reaction(town, b, law, option);
            if (v > 0.01) pleased.Add(b);
            else if (v < -0.01) angered.Add(b);
        }
        return (pleased, angered);
    }

    /// <summary>The share of a law's last change in effect, 0 to 1.</summary>
    public static double Phase(Town town, string law) =>
        town.Politics.Changes.TryGetValue(law, out var c) ? Math.Clamp((town.Day - c.Day) / (double)Math.Max(1, Rules(town).PhaseDays), 0, 1) : 1;

    /// <summary>
    /// How the laws in force, the situations, the form and the town's trust move a bloc: added to its feeling. Kept for the
    /// hour, and again after each change.
    /// </summary>
    public static double Standing(Town town, string blocId)
    {
        Refresh(town);
        return town.Politics.Standing.GetValueOrDefault(blocId, 0);
    }

    /// <summary>What the laws (and the rest) do to a bloc, each with its share, for the lens: ("Curfew: at dusk", -12).</summary>
    public static List<(string Law, string What, double Points)> Pushes(Town town, BlocDef b)
    {
        var r = Rules(town);
        var list = new List<(string, string, double)>();
        foreach (var law in Keys(town))
        {
            string now = Current(town, law);
            double phase = Phase(town, law), v = Moves(town, b, law, now) * phase;
            if (phase < 1 && town.Politics.Changes.TryGetValue(law, out var c)) v += Moves(town, b, law, c.From) * (1 - phase);
            if (Math.Abs(v) >= 0.5) list.Add((law, Describe(town, law, now), v));
        }
        foreach (var s in r.Situations)
        {
            if (!town.Politics.Situations.ContainsKey(s.Id)) continue;
            double v = -r.Step * s.Hurts.Count(b.Likes.Contains);
            if (v != 0) list.Add(("situation:" + s.Id, s.Name, v));
        }
        double trust = (town.Politics.Trust - 50) * r.Trust.Weight;
        if (Math.Abs(trust) >= 0.5) list.Add(("trust", "Trust in your word", trust));
        return list;
    }

    private static void Refresh(Town town)
    {
        var p = town.Politics;
        if (p.CacheDay == town.Tick) return;
        p.CacheDay = town.Tick;
        p.Standing.Clear();
        p.Sizes.Clear();
        var r = Rules(town);
        foreach (var b in town.Content.Blocs.Blocs)
        {
            p.Sizes[b.Id] = Systems.Blocs.Size(town, b);
            double laws = 0, rest = 0;
            foreach (var (law, _, v) in Pushes(town, b))
                if (law.StartsWith("situation:", StringComparison.Ordinal) || law == "trust") rest += v;
                else laws += v;
            p.Standing[b.Id] = Math.Clamp(laws, -r.Most, r.Most) + rest;
        }
    }

    /// <summary>Forget the kept standing: a law changed by another hand (the council's motions).</summary>
    public static void Changed(Town town) => town.Politics.CacheDay = -1;

    /// <summary>A bloc's size, kept with the hour's standing.</summary>
    public static double Size(Town town, BlocDef b)
    {
        Refresh(town);
        return town.Politics.Sizes.GetValueOrDefault(b.Id, 0);
    }

    /// <summary>How a bloc feels about the mayor, choices and laws together, -100 to 100.</summary>
    public static double Feeling(Town town, string blocId) => Math.Clamp(Systems.Blocs.Feeling(town, blocId) + Standing(town, blocId), -100, 100);

    /// <summary>"Curfew: at dusk", "Gate: shut", "Taxes: 15%".</summary>
    public static string Describe(Town town, string law, string option)
    {
        var r = Rules(town);
        return Kind(law) switch
        {
            "tax" => $"Taxes: {Js.Str(Js.Round((double.TryParse(option, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0) * 100))}%",
            "gate" => $"Gate: {option}",
            "service" => option == "conscription" ? "Levy: conscription" : "Levy: volunteers",
            "trial" => option == "" ? "Trials: as of old" : $"Trials: {town.Content.Library.Spell(option)?.Name ?? option}",
            "form" => $"Government: {r.Form(option)?.Name ?? option}",
            "punish" => $"{town.Content.Laws.Crimes.FirstOrDefault(c => c.Id == law[7..])?.Name ?? law[7..]}: {town.Content.Laws.Punishment(option)?.Name.ToLowerInvariant() ?? option}",
            "policy" => r.Policy(law[7..]) is { } pd ? $"{pd.Name}: {(pd.Level(option)?.Name ?? option).ToLowerInvariant()}" : law,
            _ => law,
        };
    }

    /// <summary>How an ask names a change: "the gates shut", "a curfew at dusk", "taxes at 12%".</summary>
    public static string Say(Town town, string law, string option)
    {
        var say = Rules(town).Say;
        string kind = Kind(law);
        if (kind == "tax" && say.TryGetValue("tax", out var t))
            return t.Replace("{rate}", $"{Js.Str(Js.Round(ParseRate(option) * 100))}%");
        if (kind == "punish" && say.TryGetValue("punish", out var p))
            return p.Replace("{punishment}", (town.Content.Laws.Punishment(option)?.Name ?? option).ToLowerInvariant())
                .Replace("{crime}", (town.Content.Laws.Crimes.FirstOrDefault(c => c.Id == law[7..])?.Name ?? law[7..]).ToLowerInvariant());
        return say.TryGetValue(law + ":" + option, out var s) ? s : Describe(town, law, option).ToLowerInvariant();
    }

    // ---------- Changing a law ----------

    /// <summary>The matters on the town vote: the form's, less any taken back, and any the mayor added.</summary>
    public static List<string> Votes(Town town)
    {
        var f = Form(town);
        if (!f.AllowVotes) return [];
        return f.Votes.Where(v => !town.Politics.Unvoted.Contains(v)).Concat(town.Politics.Votes.Where(v => !f.Votes.Contains(v))).Distinct().ToList();
    }

    /// <summary>Whether a change to this law goes to the town vote.</summary>
    public static bool Voted(Town town, string law)
    {
        string kind = Kind(law);
        return Votes(town).Any(id => Rules(town).Matter(id)?.Laws.Contains(kind) == true);
    }

    /// <summary>The referendum open on a law, if any.</summary>
    public static Referendum? Open(Town town, string law) => town.Politics.Referenda.FirstOrDefault(x => x.Law == law && x.Yes < 0);

    /// <summary>Mandate a change to this law costs now.</summary>
    public static double Cost(Town town, string law, string option)
    {
        var r = Rules(town);
        double baseCost = law switch
        {
            "form" => r.FormChange,
            "tax" => Math.Min(r.TaxMost, r.TaxChange * Math.Max(1, Math.Abs(Js.Round((ParseRate(option) - town.TaxRate) * 100)))),
            _ => r.Change,
        };
        if (law != "form") baseCost *= Form(town).LawTimes;
        if (town.Politics.Changes.TryGetValue(law, out var c) && town.Day - c.Day < r.FlipDays)
            baseCost *= Math.Pow(r.FlipTimes, c.Times + (Flips(town, law, option, c) ? 1 : 0));
        return Js.Round(baseCost);
    }

    /// <summary>Whether a change turns a law back soon after its last: any change but taxes stepped on the same way.</summary>
    private static bool Flips(Town town, string law, string option, LawChange c) =>
        law != "tax" || Math.Sign(ParseRate(option) - town.TaxRate) != c.Dir;

    private static double ParseRate(string option) =>
        double.TryParse(option, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

    /// <summary>Why this change can't be made now, or null.</summary>
    public static string? Blocked(Town town, string law, string option)
    {
        if (option == Current(town, law)) return "already so";
        if (Open(town, law) != null) return "the town is voting on it";
        var r = Rules(town);
        switch (Kind(law))
        {
            case "tax":
                double rate = ParseRate(option);
                if (rate < town.Content.Taxes.Min - 1e-9 || rate > town.Content.Taxes.Max + 1e-9) return "out of bounds";
                break;
            case "gate":
                if (Frontier.GateBlocked(town, option) is { } g) return g;
                break;
            case "service":
                if (option != "volunteers" && option != "conscription") return "no such law";
                break;
            case "trial":
                if (option != "" && (town.Content.Laws.Trial.Of(option) is not { } t || Laws.TruthBlocked(town, t) is not null)) return "can't be held";
                break;
            case "punish":
                if (town.Content.Laws.Punishment(option) == null) return "no such punishment";
                break;
            case "policy":
                if (r.Policy(law[7..])?.Level(option) == null) return "no such level";
                break;
            case "form":
                if (r.Form(option) == null) return "no such form";
                if (town.Day - town.Politics.FormDay < r.FormGapDays && town.Politics.FormDay > 0) return $"next in {r.FormGapDays - (town.Day - town.Politics.FormDay)} days";
                break;
            default:
                return "no such law";
        }
        if (!Voted(town, law) && town.Mandate < Cost(town, law, option)) return "not enough Mandate";
        return null;
    }

    /// <summary>
    /// The mayor changes a law: at once, for its Mandate, or by putting it to the town vote if the form says so. Returns
    /// whether it was done or put to the vote.
    /// </summary>
    public static bool Change(Town town, string law, string option)
    {
        if (Blocked(town, law, option) != null) return false;
        if (Voted(town, law))
        {
            var r = Rules(town);
            var p = town.Politics;
            p.Referenda.Add(new Referendum { Id = p.NextReferendum++, Law = law, Option = option, Day = town.Day, Due = town.Day + r.Vote.Days });
            town.Log(LedgerCategory.Council, Importance.Notable, $"The mayor puts it to the town: {Describe(town, law, option).ToLowerInvariant()}. The vote is counted in {r.Vote.Days} days.");
            return true;
        }
        double cost = Cost(town, law, option);
        string was = Current(town, law);
        if (!Apply(town, law, option)) return false;
        town.Mandate = Math.Clamp(town.Mandate - cost, 0, 100);
        Record(town, law, was);
        Said(town, law, option, was);
        return true;
    }

    private static void Record(Town town, string law, string was)
    {
        var p = town.Politics;
        var r = Rules(town);
        // Taxes stepped the same way again are one change; turning back is a flip.
        int dir = law == "tax" ? Math.Sign(town.TaxRate - ParseRate(was)) : 0;
        if (p.Changes.TryGetValue(law, out var c) && town.Day - c.Day < r.FlipDays)
        {
            if (law != "tax" || dir != c.Dir) c.Times++;
        }
        else p.Changes[law] = c = new LawChange();
        c.Dir = dir;
        c.From = was;
        c.Day = town.Day;
        p.CacheDay = -1;
    }

    /// <summary>The Ledger names the blocs a change pleased and angered.</summary>
    private static void Said(Town town, string law, string option, string was)
    {
        var pleased = new List<BlocDef>();
        var angered = new List<BlocDef>();
        foreach (var b in Blocs(town))
        {
            double v = Moves(town, b, law, option) - Moves(town, b, law, was);
            if (v > 0.01) pleased.Add(b);
            else if (v < -0.01) angered.Add(b);
        }
        var parts = new List<string>();
        if (pleased.Count > 0) parts.Add($"{Systems.Blocs.Voices(pleased)} approve");
        if (angered.Count > 0) parts.Add($"{Systems.Blocs.Voices(angered)} object");
        if (parts.Count > 0) town.Log(LedgerCategory.Town, Importance.Routine, $"{Describe(town, law, option)}: {string.Join("; ", parts)}.");
    }

    /// <summary>Make the change itself, through the system that owns the law.</summary>
    private static bool Apply(Town town, string law, string option)
    {
        switch (Kind(law))
        {
            case "tax": return Taxes.Set(town, ParseRate(option));
            case "gate": return Frontier.SetGate(town, option);
            case "service": return Army.SetService(town, option == "conscription" ? ServiceLaw.Conscription : ServiceLaw.Volunteers);
            case "trial": return Laws.SetTrialMagic(town, option);
            case "punish": return Laws.Set(town, law[7..], option);
            case "policy": return SetLevel(town, law[7..], option);
            case "form": return SetForm(town, option);
            default: return false;
        }
    }

    private static bool SetLevel(Town town, string policy, string level)
    {
        var p = Rules(town).Policy(policy);
        var l = p?.Level(level);
        if (p == null || l == null || Level(town, p).Id == l.Id) return false;
        town.Politics.Policies[p.Id] = l.Id;
        town.Log(LedgerCategory.Council, Importance.Notable, $"{p.Name}: {l.Name.ToLowerInvariant()}. {l.Text}");
        return true;
    }

    private static bool SetForm(Town town, string id)
    {
        var f = Rules(town).Form(id);
        if (f == null || f.Id == town.Politics.Form) return false;
        var p = town.Politics;
        p.Form = f.Id;
        p.FormDay = Math.Max(1, town.Day);
        p.Unvoted.Clear();
        p.Votes.Clear();
        if (!f.AllowVotes) p.Referenda.RemoveAll(x => x.Yes < 0);
        if (f.Seats == "none")
            foreach (var m in town.Motions.Where(m => m.Status == MotionStatus.Pending)) m.Status = MotionStatus.Failed;
        town.Log(LedgerCategory.Council, Importance.Major, $"{town.Name} is a {f.Name.ToLowerInvariant()} from today. {f.Text}");
        if (f.Seats == "none")
            foreach (var s in town.Seats.Where(s => s.HolderId != null))
            {
                town.Log(LedgerCategory.Council, Importance.Notable, $"{town.Agent(s.HolderId!.Value).Name} is sent home: there is no council any more.", [s.HolderId.Value]);
                s.HolderId = null;
            }
        return true;
    }

    /// <summary>Put a matter to the town vote, or take it back; costs Mandate either way.</summary>
    public static string? VoteBlocked(Town town, string matter, bool on)
    {
        if (Rules(town).Matter(matter) == null) return "no such matter";
        if (!Form(town).AllowVotes) return $"a {Form(town).Name.ToLowerInvariant()} holds no votes";
        if (Votes(town).Contains(matter) == on) return "already so";
        if (town.Mandate < Rules(town).MatterChange) return "not enough Mandate";
        return null;
    }

    public static bool SetVote(Town town, string matter, bool on)
    {
        if (VoteBlocked(town, matter, on) != null) return false;
        var p = town.Politics;
        bool form = Form(town).Votes.Contains(matter);
        if (on)
        {
            if (form) p.Unvoted.Remove(matter);
            else p.Votes.Add(matter);
        }
        else if (form) p.Unvoted.Add(matter);
        else p.Votes.Remove(matter);
        town.Mandate = Math.Clamp(town.Mandate - Rules(town).MatterChange, 0, 100);
        var m = Rules(town).Matter(matter)!;
        town.Log(LedgerCategory.Council, Importance.Notable, on
            ? $"From today the town votes on {m.Name.ToLowerInvariant()}: the mayor can't change them without the people."
            : $"The mayor takes {m.Name.ToLowerInvariant()} back from the town vote.");
        Systems.Blocs.Judge(town, on ? ["freedom"] : ["order"], on ? "Putting it to the people" : "Taking it from the people");
        return true;
    }

    /// <summary>The share of the town for a change: each bloc by its size, from how the change moves it and how it feels.</summary>
    public static double Support(Town town, string law, string option)
    {
        var v = Rules(town).Vote;
        double yes = 0, all = 0;
        foreach (var b in Blocs(town))
        {
            if (Form(town).Barred.Contains(b.Id)) continue;
            double size = Size(town, b);
            double s = Math.Clamp(0.5 + Reaction(town, b, law, option) * v.Per + Feeling(town, b.Id) * v.Feeling, 0.05, 0.95);
            yes += s * size;
            all += size;
        }
        return all <= 0 ? 0.5 : yes / all;
    }

    private static void Count(Town town)
    {
        var v = Rules(town).Vote;
        foreach (var x in town.Politics.Referenda.Where(x => x.Yes < 0 && town.Day >= x.Due).ToList())
        {
            x.Yes = Support(town, x.Law, x.Option);
            bool passed = x.Yes > 0.5 && Blocked(town, x.Law, x.Option) is null or "not enough Mandate";
            string what = Describe(town, x.Law, x.Option).ToLowerInvariant();
            string share = $"{Js.Str(Js.Round(x.Yes * 100))}%";
            if (passed)
            {
                string was = Current(town, x.Law);
                if (Apply(town, x.Law, x.Option))
                {
                    Record(town, x.Law, was);
                    town.Mandate = Math.Clamp(town.Mandate + v.Won, 0, 100);
                    town.Log(LedgerCategory.Council, Importance.Notable, $"The town votes yes, {share}: {what}.");
                    continue;
                }
            }
            town.Mandate = Math.Clamp(town.Mandate - v.Lost, 0, 100);
            town.Log(LedgerCategory.Council, Importance.Notable, $"The town votes no, {share} for: {what}. Things stay as they are.");
        }
        if (town.Politics.Referenda.Count > 12) town.Politics.Referenda.RemoveRange(0, town.Politics.Referenda.Count - 12);
    }

    // ---------- The council ----------

    /// <summary>Whether the town has a council at all.</summary>
    public static bool HasCouncil(Town town) => Form(town).Seats != "none";

    /// <summary>Times the Mandate a decree overruling the council costs.</summary>
    public static double DecreeTimes(Town town) => Form(town).DecreeTimes;

    /// <summary>Mandate a day the form adds.</summary>
    public static double Mandate(Town town) => Form(town).Mandate;

    /// <summary>Times the watch's strength.</summary>
    public static double WatchFactor(Town town) => Form(town).Watch;

    /// <summary>The blocs a councillor speaks for: every bloc they belong to by enough, and at least two.</summary>
    public static List<(BlocDef Bloc, double Degree)> SeatBlocs(Town town, Agent a)
    {
        double least = Rules(town).Councillors.Least;
        var all = town.Content.Blocs.Blocs.Select(b => (Bloc: b, Degree: Systems.Blocs.Degree(town, b, a))).Where(x => x.Degree > 0)
            .OrderByDescending(x => x.Degree).ToList();
        var list = all.Where(x => x.Degree >= least).ToList();
        foreach (var x in all)
        {
            if (list.Count >= 2) break;
            if (!list.Contains(x)) list.Add(x);
        }
        // Everyone who keeps a house speaks for the householders at least.
        if (list.Count < 2 && town.Content.Blocs.Of("householders") is { } h && list.All(x => x.Bloc.Id != h.Id)) list.Add((h, 0.1));
        return list;
    }

    /// <summary>Loyalty a councillor gains or loses a day from how their blocs feel about the mayor.</summary>
    public static double SeatLoyalty(Town town, Agent a)
    {
        var blocs = SeatBlocs(town, a);
        if (blocs.Count == 0) return 0;
        double sum = 0, w = 0;
        foreach (var (b, d) in blocs)
        {
            sum += Feeling(town, b.Id) * d;
            w += d;
        }
        return w <= 0 ? 0 : sum / w / 100 * Rules(town).Councillors.Loyalty;
    }

    /// <summary>How a councillor's blocs lean them on a motion: added to their lean.</summary>
    public static double Lean(Town town, Agent a, string motion)
    {
        if (!Rules(town).Motions.TryGetValue(motion, out var tags) || tags.Count == 0) return 0;
        double sum = 0;
        foreach (var (b, d) in SeatBlocs(town, a))
            sum += d * (tags.Count(b.Likes.Contains) - tags.Count(b.Dislikes.Contains));
        return sum * Rules(town).Councillors.Lean * 2.5;
    }

    /// <summary>Who the town would seat, from candidates (ablest first): the ablest; the richest under a crown; the moot's choice where it elects.</summary>
    public static Agent? Seat(Town town, List<Agent> candidates)
    {
        if (candidates.Count == 0) return null;
        var f = Form(town);
        if (f.Seats == "gentry") return candidates.OrderByDescending(a => a.Gold).ThenBy(a => a.Id).First();
        if (f.Seats != "elected") return candidates[0];
        var pool = candidates.Take(Rules(town).Moot.Candidates).ToList();
        return pool.OrderByDescending(a => Votes(town, a)).ThenBy(a => pool.IndexOf(a)).First();
    }

    /// <summary>The votes a candidate draws at the moot: each bloc's size by how much they belong to it.</summary>
    public static double Votes(Town town, Agent a)
    {
        var f = Form(town);
        double sum = 0;
        foreach (var b in Blocs(town))
            if (!f.Barred.Contains(b.Id)) sum += Size(town, b) * Systems.Blocs.Degree(town, b, a);
        return sum;
    }

    /// <summary>A new councillor's loyalty: from how their blocs feel about the mayor, where the town elects them.</summary>
    public static double StartLoyalty(Town town, Agent a, double rolled)
    {
        if (Form(town).Seats != "elected") return rolled;
        var blocs = SeatBlocs(town, a);
        double mean = blocs.Count == 0 ? 0 : blocs.Average(x => Feeling(town, x.Bloc.Id));
        return Math.Clamp(50 + mean * Rules(town).Moot.Loyalty, 5, 95);
    }

    /// <summary>The share of the town, by bloc size, whose blocs feel well of the mayor (at or above 0).</summary>
    public static double Content(Town town)
    {
        double yes = 0, all = 0;
        foreach (var b in Blocs(town))
        {
            double size = Size(town, b);
            if (Feeling(town, b.Id) >= 0) yes += size;
            all += size;
        }
        return all <= 0 ? 0 : yes / all;
    }

    // ---------- The moot ----------

    /// <summary>The day of the next moot, or null where the form elects nobody.</summary>
    public static int? NextMoot(Town town)
    {
        if (!Form(town).Elects) return null;
        int year = (int)Seasons.YearLength(town);
        return (town.Day / year + 1) * year;
    }

    /// <summary>The share the mayor would poll today: the blocs' feeling, by size, and the Mandate.</summary>
    public static double Poll(Town town)
    {
        var m = Rules(town).Moot;
        double yes = 0, all = 0;
        foreach (var b in Blocs(town))
        {
            if (Form(town).Barred.Contains(b.Id)) continue;
            double size = Size(town, b);
            yes += size * (Feeling(town, b.Id) + 100) / 200;
            all += size;
        }
        double blocs = all <= 0 ? 0.5 : yes / all;
        return (1 - m.MandateShare) * blocs + m.MandateShare * town.Mandate / 100;
    }

    private static void Moot(Town town)
    {
        var f = Form(town);
        var p = town.Politics;
        if (!f.Elects || NextMoot(town) is not int day || town.Day + 1 != day || p.MootDay == town.Day) return;
        p.MootDay = town.Day;
        p.Moots++;
        town.Log(LedgerCategory.Council, Importance.Major, $"The year's end: {town.Name} holds its moot, and every household has its say.");
        if (f.Seats == "elected")
        {
            var seated = new HashSet<int>();
            foreach (var s in town.Seats)
            {
                if (town.Population < s.Def.UnlockPopulation) continue;
                var cand = Council.Candidates(town, s, seated);
                var winner = Seat(town, cand);
                if (winner == null) continue;
                seated.Add(winner.Id);
                if (s.HolderId == winner.Id)
                {
                    town.Log(LedgerCategory.Council, Importance.Notable, $"{winner.Name} keeps the seat of {s.Def.Title}.", [winner.Id]);
                    continue;
                }
                s.HolderId = winner.Id;
                s.Loyalty = StartLoyalty(town, winner, 50);
                s.LowLoyaltyDays = 0;
                var blocs = SeatBlocs(town, winner).Select(x => x.Bloc).ToList();
                town.Log(LedgerCategory.Council, Importance.Notable, $"The moot seats {winner.Name} as {s.Def.Title}, for {Systems.Blocs.Voices(blocs)}.", [winner.Id]);
            }
        }
        if (f.Mayor == "elected" && !Mayoralty.Dead(town))
        {
            double share = Poll(town);
            p.Share = share;
            string pct = $"{Js.Str(Js.Round(share * 100))}%";
            if (share >= Rules(town).Moot.Keep)
            {
                town.Mandate = Math.Clamp(town.Mandate + Rules(town).Moot.Won, 0, 100);
                town.Log(LedgerCategory.Council, Importance.Major, $"Mayor {town.Stories.Mayor.Name} wins the moot with {pct} of the vote.");
            }
            else Mayoralty.Unseat(town, $"lost the moot with {pct} of the vote");
        }
    }

    // ---------- Policies at work ----------

    /// <summary>Times the chance an offender tries a crime, for the curfew.</summary>
    public static double Deter(Town town) => 1 - Math.Clamp(Js.Sum(Rules(town).Policies, p => Level(town, p).Deter), 0, 0.9);

    /// <summary>Mood for everyone from the policies in force.</summary>
    public static double Mood(Town town) => Js.Sum(Rules(town).Policies, p => Level(town, p).Mood);

    /// <summary>Mood for a poor townsperson from the dole.</summary>
    public static double Relief(Town town, Agent a) =>
        a.Hero == null && a.Gold < 20 ? Js.Sum(Rules(town).Policies, p => Level(town, p).Relief) : 0;

    /// <summary>What the policies cost the treasury a day.</summary>
    public static double Upkeep(Town town)
    {
        double pop = town.Population, sum = 0;
        foreach (var p in Rules(town).Policies)
        {
            var l = Level(town, p);
            sum += (l.Gold + l.PerResident * pop) * (p.Id == "dole" ? Form(town).Dole : 1);
        }
        return Js.Round(sum);
    }

    private static void Policies(Town town)
    {
        double cost = Upkeep(town);
        if (cost > 0) town.Book(TreasuryAccount.Laws, -Math.Min(cost, Math.Max(0, town.Treasury)));
        double favour = Js.Sum(Rules(town).Policies, p => Level(town, p).Favour);
        if (favour > 0 && Faith.TempleGod(town) is { } god) town.Faiths.Favor[god.Id] = town.Faiths.Favor.Get(god.Id, 0) + favour;
    }

    // ---------- Situations ----------

    /// <summary>A situation's reading today.</summary>
    public static double Reading(Town town, string reading)
    {
        switch (reading)
        {
            case "crime":
            {
                // Brawls this week per hundred workers (a hamlet counts as a hundred).
                int n = 0, since = town.Tick - 7 * SimTime.HoursPerDay;
                var e = town.Ledger.Entries;
                for (int i = e.Count - 1; i >= 0 && e[i].Tick >= since; i--)
                    if (e[i].Category == LedgerCategory.Brawl) n++;
                return n * 100.0 / Math.Max(100, town.Workers.Count);
            }
            case "homeless":
            {
                var w = town.Workers;
                return w.Count == 0 ? 0 : 100.0 * w.Count(a => a.Homeless) / w.Count;
            }
            case "bread": return town.Market.Price(Good.Food) / Math.Max(0.01, town.Market.BasePrice(Good.Food));
            case "sick": return town.Population == 0 ? 0 : 100.0 * Sickness.Abed(town) / town.Population;
            case "strike": return town.Guilds.Count(g => Guilds.OnStrike(town, g));
            case "unrest": return town.Taxes.Unrest;
            default: return 0;
        }
    }

    private static void Situations(Town town)
    {
        var p = town.Politics;
        foreach (var s in Rules(town).Situations)
        {
            double v = Reading(town, s.Reading);
            bool on = p.Situations.ContainsKey(s.Id);
            if (!on && v >= s.Start && town.Population >= 40)
            {
                p.Situations[s.Id] = town.Day;
                p.CacheDay = -1;
                town.Log(LedgerCategory.Town, Importance.Notable, s.Began.Replace("{town}", town.Name));
            }
            else if (on && v <= s.End)
            {
                p.Situations.Remove(s.Id);
                p.CacheDay = -1;
                town.Log(LedgerCategory.Town, Importance.Notable, s.Ended.Replace("{town}", town.Name));
            }
            if (p.Situations.ContainsKey(s.Id) && s.Unrest > 0) town.Taxes.Unrest = Math.Clamp(town.Taxes.Unrest + s.Unrest, 0, 100);
        }
    }

    // ---------- Asks and promises ----------

    /// <summary>The change a bloc would like most, if any: (law, option).</summary>
    public static (string Law, string Option)? Wish(Town town, BlocDef b)
    {
        (string, string)? best = null;
        double most = 0.5;
        int again = Rules(town).Demands.Again;
        foreach (var (law, option) in Options(town))
        {
            if (Blocked(town, law, option) is { } why && why != "not enough Mandate") continue;
            if (town.Politics.Refused.TryGetValue($"{b.Id}|{law}|{option}", out int no) && town.Day - no < again) continue;
            double v = Reaction(town, b, law, option);
            if (v > most)
            {
                most = v;
                best = (law, option);
            }
        }
        return best;
    }

    /// <summary>Every change the mayor could make: each other option of each law, and taxes a few points either way.</summary>
    private static IEnumerable<(string, string)> Options(Town town)
    {
        double step = 0.03;
        string R(double r) => Js.Str(Js.Round(Math.Clamp(r, town.Content.Taxes.Min, town.Content.Taxes.Max) * 100) / 100);
        yield return ("tax", R(town.TaxRate - step));
        yield return ("tax", R(town.TaxRate + step));
        foreach (var g in Frontier.Rules(town).Gates) yield return ("gate", g.Id);
        yield return ("service", "volunteers");
        yield return ("service", "conscription");
        foreach (var c in town.Content.Laws.Crimes)
            foreach (var pun in town.Content.Laws.Punishments) yield return ("punish:" + c.Id, pun.Id);
        foreach (var pol in Rules(town).Policies)
            foreach (var l in pol.Levels) yield return ("policy:" + pol.Id, l.Id);
    }

    public static Demand? Ask(Town town) => town.Politics.Demands.FirstOrDefault(d => !d.Promised);

    private static void Asks(Town town, bool auto)
    {
        var r = Rules(town).Demands;
        var p = town.Politics;
        foreach (var k in p.Refused.Where(kv => town.Day - kv.Value >= r.Again).Select(kv => kv.Key).ToList()) p.Refused.Remove(k);
        // An ask left unanswered lapses as a refusal; a stand-in mayor lets them lapse.
        foreach (var d in p.Demands.Where(d => !d.Promised && town.Day - d.Day >= r.AskDays).ToList()) Answer(town, d.Id, false);
        foreach (var d in p.Demands.Where(d => d.Promised).ToList())
        {
            var b = Systems.Blocs.Def(town, d.Bloc);
            string what = Say(town, d.Law, d.Option), voice = b?.Voice ?? d.Bloc;
            if (Current(town, d.Law) == d.Option)
            {
                p.Demands.Remove(d);
                Systems.Blocs.Nudge(town, d.Bloc, r.Kept);
                p.Trust = Math.Clamp(p.Trust + r.KeptTrust, 0, 100);
                p.CacheDay = -1;
                town.Log(LedgerCategory.Council, Importance.Notable, Fill(r.KeptLine, town, voice, what));
            }
            else if (town.Day >= d.Due)
            {
                p.Demands.Remove(d);
                Systems.Blocs.Nudge(town, d.Bloc, -r.Broken);
                p.Trust = Math.Clamp(p.Trust - r.BrokenTrust, 0, 100);
                p.CacheDay = -1;
                town.Log(LedgerCategory.Council, Importance.Major, Fill(r.BrokenLine, town, voice, what));
            }
        }
        if (town.Day - p.AskDay < r.Every || Ask(town) != null || town.Population < 15) return;
        var worst = Blocs(town).Where(b => p.Demands.All(d => d.Bloc != b.Id)).Select(b => (b, f: Feeling(town, b.Id)))
            .Where(x => x.f < r.AskBelow).OrderBy(x => x.f).Select(x => x.b).FirstOrDefault();
        if (worst == null || Wish(town, worst) is not { } wish) return;
        var (law, option) = wish;
        p.AskDay = town.Day;
        p.Demands.Add(new Demand { Id = p.NextDemand++, Bloc = worst.Id, Law = law, Option = option, Day = town.Day });
        town.Log(LedgerCategory.Council, auto ? Importance.Routine : Importance.Notable, Fill(r.Asks, town, worst.Voice, Say(town, law, option)));
    }

    private static string Fill(string text, Town town, string voice, string change) =>
        Cap(text.Replace("{voice}", voice).Replace("{change}", change).Replace("{town}", town.Name));

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Promise a bloc its ask within the days, or refuse it.</summary>
    public static bool Answer(Town town, int demandId, bool promise)
    {
        var r = Rules(town).Demands;
        var p = town.Politics;
        var d = p.Demands.FirstOrDefault(x => x.Id == demandId && !x.Promised);
        if (d == null) return false;
        string voice = Systems.Blocs.Def(town, d.Bloc)?.Voice ?? d.Bloc;
        if (promise)
        {
            d.Promised = true;
            d.Due = town.Day + r.DueDays;
            Systems.Blocs.Nudge(town, d.Bloc, r.Promise);
            town.Log(LedgerCategory.Council, Importance.Notable, $"The mayor gives {voice} their word: {Say(town, d.Law, d.Option)}, within {r.DueDays} days.");
        }
        else
        {
            p.Demands.Remove(d);
            p.Refused[$"{d.Bloc}|{d.Law}|{d.Option}"] = town.Day;
            Systems.Blocs.Nudge(town, d.Bloc, -r.Refused);
            town.Log(LedgerCategory.Council, Importance.Routine, $"The mayor turns {voice} away.");
        }
        return true;
    }

    // ---------- Radicals ----------

    /// <summary>The politics' own dice for one roll, apart from the town's stream.</summary>
    private static Rng Dice(Town town, string salt, int n) =>
        new(unchecked(town.Seed * 7919 + town.Day * 104729 + (int)Voices.Hash(salt) * 31 + n * 17 + 11));

    public static Cell? CellOf(Town town, string bloc) => town.Politics.Cells.FirstOrDefault(c => c.Bloc == bloc);

    private static void Radicals(Town town)
    {
        var r = Rules(town).Radicals;
        var p = town.Politics;
        double grow = Form(town).Radicals;
        foreach (var b in town.Content.Blocs.Blocs)
        {
            double f = Feeling(town, b.Id);
            var cell = CellOf(town, b.Id);
            if (cell == null)
            {
                p.Furious[b.Id] = f <= r.Furious && Size(town, b) >= town.Content.Blocs.Least ? p.Furious.GetValueOrDefault(b.Id, 0) + 1 : 0;
                if (grow > 0 && p.Furious[b.Id] >= r.FormDays / grow)
                {
                    p.Cells.Add(new Cell { Bloc = b.Id, Formed = town.Day, Next = town.Day + r.Every });
                    p.Furious[b.Id] = 0;
                    town.Log(LedgerCategory.Town, Importance.Major, Cap(r.Formed.Replace("{voice}", b.Voice)));
                }
                continue;
            }
            p.Calm[b.Id] = f > r.Calm ? p.Calm.GetValueOrDefault(b.Id, 0) + 1 : 0;
            if (p.Calm[b.Id] >= r.CalmDays)
            {
                p.Cells.Remove(cell);
                p.Calm[b.Id] = 0;
                town.Log(LedgerCategory.Town, Importance.Notable, Cap(r.Faded.Replace("{voice}", b.Voice)));
                continue;
            }
            if (town.Day < cell.Next) continue;
            cell.Next = town.Day + r.Every;
            var dice = Dice(town, b.Id, cell.Stage);
            if (dice.Chance(Council.WatchStrength(town) * r.Catch))
            {
                p.Cells.Remove(cell);
                town.Log(LedgerCategory.Town, Importance.Notable, Cap(r.Foiled.Replace("{voice}", b.Voice)));
                continue;
            }
            Act(town, b, cell, dice);
        }
    }

    private static void Act(Town town, BlocDef b, Cell cell, Rng dice)
    {
        var r = Rules(town).Radicals;
        int stage = Math.Min(cell.Stage, Math.Max(0, r.Acts.Count - 1));
        string line = r.Acts.Count > 0 ? r.Acts[stage].Replace("{voice}", b.Voice) : "";
        switch (stage)
        {
            case 0:
                town.Mandate = Math.Clamp(town.Mandate - r.Graffiti, 0, 100);
                break;
            case 1:
            {
                town.Taxes.Unrest = Math.Clamp(town.Taxes.Unrest + r.Riot, 0, 100);
                var targets = town.Buildings.Where(x => !x.IsSite && !x.Wrecked && x.Def.Housing == 0 && x.Def.Id != "town_hall").OrderBy(x => x.Id).ToList();
                var hit = targets.Count > 0 ? dice.Pick(targets) : null;
                if (hit != null) Blows.Wreck(town, hit, 0.3);
                line = line.Replace("{building}", hit != null ? $"the {hit.Def.Name.ToLowerInvariant()}" : "a market stall");
                break;
            }
            case 2:
            {
                double gold = Math.Floor(Math.Min(r.SabotageMost, Math.Max(0, town.Treasury) * r.Sabotage));
                if (gold > 0) town.Book(TreasuryAccount.Discrepancies, -gold);
                line = line.Replace("{gold}", Js.Str(gold));
                break;
            }
            default:
                if (!Mayoralty.Exposed(town)) line = r.Missed.Replace("{voice}", b.Voice);
                else
                {
                    town.Log(LedgerCategory.Town, Importance.Major, Cap(line));
                    Mayoralty.Knifed(town, new Culprit($"a radical from {b.Voice}", "", ""));
                    line = "";
                }
                break;
        }
        if (line != "") town.Log(LedgerCategory.Town, Importance.Major, Cap(line));
        cell.Stage++;
    }

    // ---------- The day ----------

    public static void Init(Town town, string form = "")
    {
        town.Politics.Trust = Rules(town).Trust.Start;
        if (Rules(town).Form(form) is { } f) town.Politics.Form = f.Id;
    }

    public static void Daily(Town town, bool auto)
    {
        var p = town.Politics;
        p.CacheDay = -1;
        p.Trust += (50 - p.Trust) * Rules(town).Trust.Ease;
        // Taxes and laws the council or the stand-in changed today count from now.
        Policies(town);
        Count(town);
        Situations(town);
        Asks(town, auto);
        Radicals(town);
        Moot(town);
    }
}
