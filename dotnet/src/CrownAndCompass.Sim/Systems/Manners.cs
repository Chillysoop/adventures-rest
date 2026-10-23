namespace CrownAndCompass.Sim.Systems;

/// <summary>Something done for a faction now and then to warm it, shaped by one of its manners (manners.json overture).</summary>
public sealed record OvertureDef
{
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";

    /// <summary>The mayor's skill rolled for it, or "" for a sure thing.</summary>
    public string Skill { get; init; } = "";
    public int Dc { get; init; }

    /// <summary>Gold it costs, and the days before it can be done again for the same faction.</summary>
    public double Cost { get; init; }
    public int Every { get; init; } = 30;

    /// <summary>Their standing moved on a success, more on a natural 20, and on a failure.</summary>
    public double Win { get; init; }
    public double Crit { get; init; }
    public double Lose { get; init; }

    /// <summary>Story effects it has besides on a success (prosperity, security, food, mandate, reveal...).</summary>
    public Dictionary<string, double> Effects { get; init; } = [];

    /// <summary>The Ledger's line on a success and a failure ({name}: the faction, {town}).</summary>
    public string Won { get; init; } = "";
    public string Lost { get; init; } = "";
}

/// <summary>One side of a faction's personality: the skills it answers to, and what it lends a meeting and the diplomacy window.</summary>
public sealed record MannerDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";

    /// <summary>How to win them, in a breath, for the hover.</summary>
    public string Tip { get; init; } = "";

    /// <summary>What it does to the DC of each skill rolled against them: negative is easier.</summary>
    public Dictionary<string, int> Dc { get; init; } = [];

    /// <summary>How they come to a first meeting, for a lineage with no arrival of its own.</summary>
    public string Arrive { get; init; } = "";
    public StoryOption? Greet { get; init; }
    public StoryOption? Terms { get; init; }
    public StoryOption? Parley { get; init; }
    public OvertureDef? Overture { get; init; }
}

public sealed record MannerTexts
{
    public string People { get; init; } = "";
    public string Menace { get; init; } = "";
    public string Signs { get; init; } = "";
}

public sealed record MannerIgnored
{
    public StoryOutcome People { get; init; } = new();
    public StoryOutcome Menace { get; init; } = new();
    public StoryOutcome Signs { get; init; } = new();
}

/// <summary>Personalities from the data each faction already has (content/game/manners.json).</summary>
public sealed record MannerRules
{
    /// <summary>The manners shown and counted for one faction.</summary>
    public int Most { get; init; } = 3;

    /// <summary>The most manners move a DC either way.</summary>
    public int DcCap { get; init; } = 5;

    /// <summary>Lineage traits (Trait names), ruler temperaments, menace tempers and ambitions, and power likes, to manners.</summary>
    public Dictionary<string, string> Traits { get; init; } = [];
    public Dictionary<string, string> Tempers { get; init; } = [];
    public Dictionary<string, string> MenaceTempers { get; init; } = [];
    public Dictionary<string, string> Ambitions { get; init; } = [];
    public double AmbitionAt { get; init; } = 1.5;

    /// <summary>A power's likes at likesAt or more ("-key" for a dislike at -likesAt or less).</summary>
    public Dictionary<string, string> Likes { get; init; } = [];
    public double LikesAt { get; init; } = 20;
    public string Evil { get; init; } = "";

    /// <summary>A people sending this many troops is mighty; one starting this warm or this cold is so.</summary>
    public double MightyTroops { get; init; } = 35;
    public string Mighty { get; init; } = "";
    public double WarmFrom { get; init; } = 25;
    public string Warm { get; init; } = "";
    public double ColdBelow { get; init; } = -10;
    public string Cold { get; init; } = "";

    public MannerTexts Fallback { get; init; } = new();

    /// <summary>The second stage of a people's meeting, and the line under a herald's words: both tell the lineage's {motive}.</summary>
    public string Terms { get; init; } = "";
    public string Parley { get; init; } = "";

    /// <summary>Each lineage's opening line.</summary>
    public Dictionary<string, string> Arrivals { get; init; } = [];
    public StoryOption Closer { get; init; } = new();
    public StoryOption StandOff { get; init; } = new();
    public StoryOption Dismiss { get; init; } = new();
    public MannerIgnored Ignored { get; init; } = new();
    public IReadOnlyList<StoryOption> Signs { get; init; } = [];
    public IReadOnlyList<MannerDef> Manners { get; init; } = [];

    public MannerDef? Manner(string id) => Manners.FirstOrDefault(m => m.Id == id);
}

/// <summary>
/// Every faction's personality, worked out from what the game already knows of it, so a new people, power or enemy
/// gets one with no extra writing: a people's lineage traits, might and warmth plus its ruler's temperament; a power's
/// likes and whether it is evil; a menace's ambitions plus its kind's temper this campaign. The manners move the DC of
/// every roll made against them (first meetings, overtures, talking a holdout ally round), compose their first meeting
/// (<see cref="Compose"/>, at load) and offer the overtures in the Diplomacy window. This only reads the town, and the
/// overtures are the player's: the stand-in mayor never makes one.
/// </summary>
public static class Manners
{
    public static MannerRules Rules(Town town) => town.Content.Manners;

    public const string PeoplePrefix = "contact-people-";
    public const string MenacePrefix = "contact-menace-";
    public const string SignsPrefix = "contact-signs-";

    /// <summary>The option need that shows an option only to a faction with that manner.</summary>
    public const string Need = "manner:";

    // ---- Who they are ----

    private static void Add(MannerRules r, List<string> into, string? id)
    {
        if (id != null && id != "" && r.Manner(id) != null && !into.Contains(id)) into.Add(id);
    }

    private static void AddTraits(MannerRules r, List<string> into, HistoryLineage? l)
    {
        if (l == null) return;
        foreach (var t in l.Traits) Add(r, into, r.Traits.GetValueOrDefault(t));
    }

    /// <summary>A people's own manners, before its ruler: its lineage's traits, then whether it is mighty, warm or cold.</summary>
    public static List<string> OfPeople(MannerRules r, HistoryRules h, PeopleDef p)
    {
        var got = new List<string>();
        AddTraits(r, got, Annals.LineageOf(h, "people:" + p.Id));
        if (p.Boon.Troops >= r.MightyTroops) Add(r, got, r.Mighty);
        if (p.Start >= r.WarmFrom) Add(r, got, r.Warm);
        if (p.Start <= r.ColdBelow) Add(r, got, r.Cold);
        return got;
    }

    /// <summary>A power's manners: its lineage's traits, its strongest likes and dislikes, and evil.</summary>
    public static List<string> OfPower(MannerRules r, HistoryRules h, PowerDef d)
    {
        var got = new List<string>();
        AddTraits(r, got, Annals.LineageOf(h, "faction:" + d.Ref));
        foreach (var (key, w) in d.Likes.OrderByDescending(kv => Math.Abs(kv.Value)).ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (w >= r.LikesAt) Add(r, got, r.Likes.GetValueOrDefault(key));
            else if (w <= -r.LikesAt) Add(r, got, r.Likes.GetValueOrDefault("-" + key));
        }
        if (d.Evil) Add(r, got, r.Evil);
        return got;
    }

    /// <summary>A menace lineage's manners: its traits, then its ambitions, strongest first (the strongest always counts).</summary>
    public static List<string> OfLineage(MannerRules r, HistoryLineage l)
    {
        var got = new List<string>();
        AddTraits(r, got, l);
        var ambitions = l.Ambitions.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        for (int i = 0; i < ambitions.Count; i++)
            if (i == 0 || ambitions[i].Value >= r.AmbitionAt) Add(r, got, r.Ambitions.GetValueOrDefault(ambitions[i].Key));
        return got;
    }

    /// <summary>The manner a menace kind's temper this campaign gives it, if any.</summary>
    private static string? TemperOf(Town town, Menace m) =>
        Character.TemperOf(town, m.Kind) is { } t ? Rules(town).MenaceTempers.GetValueOrDefault(t.Id) : null;

    private static string? RulerOf(Town town, Settlement s) =>
        s.Reign != null ? Rules(town).Tempers.GetValueOrDefault(Reigns.TemperOf(town, s).Id) : null;

    private static HistoryLineage? LineageOf(Town town, Menace m) =>
        Annals.Lineage(town.Content.History, m.Lineage) ?? Annals.LineageOf(town.Content.History, "menace:" + m.Kind);

    /// <summary>
    /// A faction's own manners, and the one its ruler or this campaign's temper adds (null when it adds none new),
    /// for whoever a <see cref="Discovery"/> key names. Empty for keys that name nobody.
    /// </summary>
    private static (List<string> Own, string? Extra) Parts(Town town, string key)
    {
        var r = Rules(town);
        var h = town.Content.History;
        int colon = key.IndexOf(':');
        string kind = colon < 0 ? key : key[..colon], rest = colon < 0 ? "" : key[(colon + 1)..];
        (List<string>, string?) With(List<string> own, string? extra) =>
            (own, extra != null && r.Manner(extra) != null && !own.Contains(extra) ? extra : null);
        switch (kind)
        {
            case "people" when int.TryParse(rest, out int sid) && sid >= 0 && sid < town.Settlements.Count:
                return With(OfPeople(r, h, town.Settlements[sid].Kin), RulerOf(town, town.Settlements[sid]));
            case "power" when town.Content.Powers.Powers.FirstOrDefault(p => p.Id == rest) is { } d:
                return (OfPower(r, h, d), null);
            case "menace" when int.TryParse(rest, out int mid) && town.Saga.Menaces.FirstOrDefault(x => x.Id == mid) is { } m:
                return With(LineageOf(town, m) is { } l ? OfLineage(r, l) : [], TemperOf(town, m));
        }
        return ([], null);
    }

    /// <summary>Every manner of a faction, uncapped: its own, then what its ruler or temper adds.</summary>
    public static List<string> All(Town town, string key)
    {
        var (own, extra) = Parts(town, key);
        return extra == null ? own : [.. own, extra];
    }

    /// <summary>
    /// The manners shown for a faction and counted on its rolls: its own first, keeping room for what its ruler or
    /// this campaign's temper adds, since that is who the town deals with now.
    /// </summary>
    public static List<MannerDef> Of(Town town, string key)
    {
        var r = Rules(town);
        var (own, extra) = Parts(town, key);
        var shown = extra == null ? own.Take(r.Most) : own.Take(r.Most - 1).Append(extra);
        return shown.Select(id => r.Manner(id)!).ToList();
    }

    /// <summary>The Discovery key of whoever a story is about: its people, its power or its menace.</summary>
    public static string KeyOf(Story s) =>
        s.SettlementId is int sid ? "people:" + sid
        : s.Minor is string p ? "power:" + p
        : s.MenaceId is int m ? "menace:" + m
        : "";

    /// <summary>Whether whoever the story is about has the manner (an option needing "manner:id" shows only then).</summary>
    public static bool Has(Town town, Story s, string id) => All(town, KeyOf(s)).Contains(id);

    /// <summary>
    /// What their manners do to a roll of this skill against them, capped; with <paramref name="ruler"/>, a people's
    /// ruler's Insight (against lies) and Wisdom (against threats) and a menace's wits count too.
    /// </summary>
    public static int Lean(Town town, string key, string skill, bool ruler = true)
    {
        if (skill == "" || key == "") return 0;
        var r = Rules(town);
        int dc = Of(town, key).Sum(m => m.Dc.GetValueOrDefault(skill));
        if (ruler && key.StartsWith("people:", StringComparison.Ordinal) && int.TryParse(key.AsSpan(7), out int sid) && sid >= 0 && sid < town.Settlements.Count)
        {
            var t = Reigns.TemperOf(town, town.Settlements[sid]);
            if (skill is "deception" or "sleight-of-hand") dc += t.Insight / 2;
            if (skill == "intimidation") dc += t.Wis;
        }
        if (ruler && key.StartsWith("menace:", StringComparison.Ordinal) && int.TryParse(key.AsSpan(7), out int mid) && town.Saga.Menaces.FirstOrDefault(x => x.Id == mid) is { } m && skill is "deception" or "insight")
            dc += (Character.Score(town, m, "wits") - 10) / 2;
        return Math.Clamp(dc, -r.DcCap, r.DcCap);
    }

    /// <summary>Why a roll against them is harder or easier, for the hover ("Proud +3, ruler wary +2").</summary>
    public static string Why(Town town, string key, string skill)
    {
        var parts = Of(town, key).Where(m => m.Dc.GetValueOrDefault(skill) != 0)
            .Select(m => $"{m.Name} {(m.Dc[skill] > 0 ? "+" : "−")}{Math.Abs(m.Dc[skill])}").ToList();
        return string.Join(", ", parts);
    }

    // ---- First meetings ----

    /// <summary>A manner's option for a composed meeting, with "warm" turned into the effect it is for this kind of faction.</summary>
    private static StoryOption Fit(StoryOption o, string warm, string next = "", string need = "")
    {
        StoryOutcome Out(StoryOutcome x, bool goOn) => x with
        {
            Effects = x.Effects.ToDictionary(kv => kv.Key == "warm" ? warm : kv.Key, kv => kv.Value),
            Next = goOn && x.Next == "" ? next : x.Next,
        };
        return o with
        {
            Success = Out(o.Success, true),
            Failure = Out(o.Failure, false),
            Critical = o.Critical == null ? null : Out(o.Critical, true),
            Fumble = o.Fumble == null ? null : Out(o.Fumble, false),
            Partial = o.Partial == null ? null : Out(o.Partial, false),
            Needs = need == "" ? o.Needs : [.. o.Needs, need],
        };
    }

    /// <summary>Options from these manners (own ones always, the rest only for a faction that has them), then the last word.</summary>
    private static List<StoryOption> Options(MannerRules r, List<string> own, IEnumerable<string> maybe, Func<MannerDef, StoryOption?> pick, string warm, string next, StoryOption last)
    {
        var list = new List<StoryOption>();
        foreach (var id in own)
            if (pick(r.Manner(id)!) is { } o) list.Add(Fit(o, warm, next));
        foreach (var id in maybe.Where(x => !own.Contains(x)).Distinct().OrderBy(x => x, StringComparer.Ordinal))
            if (r.Manner(id) is { } m && pick(m) is { } o) list.Add(Fit(o, warm, next, Need + id));
        list.Add(Fit(last, warm));
        return list;
    }

    private static string Arrival(MannerRules r, HistoryLineage? l, string fallback, List<string> own, string people = "") =>
        people != "" && r.Arrivals.TryGetValue(people, out var mine) ? mine
        : l != null && r.Arrivals.TryGetValue(l.Id, out var a) ? a
        : fallback.Replace("{arrive}", own.Count > 0 ? r.Manner(own[0])!.Arrive : "to see who has settled so near");

    /// <summary>
    /// The first meetings composed from every people's and every menace lineage's manners: a people's riders (an
    /// opening option per manner, then what they want), a menace's herald (its words and a parley per manner) and,
    /// for beasts and the mindless, the signs it leaves. Options for a manner a ruler or temper may add are there too,
    /// shown only when the faction met has it.
    /// </summary>
    public static List<StoryDef> Compose(MannerRules r, HistoryRules h, WorldRules world, NeighbourRules neighbours)
    {
        var defs = new List<StoryDef>();
        if (r.Manners.Count == 0) return defs;
        var rulers = neighbours.Temperaments.Select(t => r.Tempers.GetValueOrDefault(t.Id)).OfType<string>().ToList();
        foreach (var p in world.Peoples)
        {
            var l = Annals.LineageOf(h, "people:" + p.Id);
            var own = OfPeople(r, h, p);
            defs.Add(new StoryDef
            {
                Id = PeoplePrefix + p.Id,
                Title = "Visitors from {settlement}",
                Stakes = "medium",
                Source = Powers.ContactSource,
                Needs = ["settlement"],
                RepeatDays = 100000,
                Stages =
                [
                    new StoryStage
                    {
                        Id = "start",
                        Text = Arrival(r, l, r.Fallback.People, own, p.Id),
                        Options = Options(r, own, rulers, m => m.Greet, "regard", "terms", r.Closer),
                        Ignored = Fit(new StoryOption { Success = r.Ignored.People }, "regard").Success,
                    },
                    new StoryStage
                    {
                        Id = "terms",
                        Text = r.Terms,
                        Options = Options(r, own, rulers, m => m.Terms, "regard", "", r.StandOff),
                        Ignored = new StoryOutcome { Text = "Nobody answers, and they go home." },
                    },
                ],
            });
        }
        foreach (var l in h.Lineages.Where(x => x.Subjects.Any(s => s.StartsWith("menace:", StringComparison.Ordinal) || s.StartsWith("foe:", StringComparison.Ordinal))))
        {
            var own = OfLineage(r, l);
            // A lineage of more than one kind can come under any of their tempers.
            var kinds = l.Subjects.Where(s => s.StartsWith("menace:", StringComparison.Ordinal)).Select(s => "menace:" + s[7..]).ToList();
            var tempers = h.Tempers.Where(t => kinds.Count == 0 || t.Subjects.Any(kinds.Contains)).Select(t => r.MenaceTempers.GetValueOrDefault(t.Id)).OfType<string>().ToList();
            bool beasts = l.Subjects.Contains("menace:beasts");
            defs.Add(new StoryDef
            {
                Id = MenacePrefix + l.Id,
                Title = "A herald of {menace}",
                Stakes = "medium",
                Source = Powers.ContactSource,
                Needs = ["menace"],
                RepeatDays = 100000,
                Stages =
                [
                    new StoryStage
                    {
                        Id = "start",
                        Text = Arrival(r, l, r.Fallback.Menace, own) + " " + r.Parley,
                        Options = Options(r, own, tempers, m => m.Parley, "dread", "", r.Dismiss),
                        Ignored = r.Ignored.Menace,
                    },
                ],
            });
            defs.Add(new StoryDef
            {
                Id = SignsPrefix + l.Id,
                Title = "Signs of {menace}",
                Stakes = "medium",
                Source = Powers.ContactSource,
                Needs = ["menace"],
                RepeatDays = 100000,
                Stages =
                [
                    new StoryStage
                    {
                        Id = "start",
                        Text = beasts && r.Arrivals.TryGetValue(l.Id, out var sign) ? sign : r.Fallback.Signs,
                        Options = [.. r.Signs],
                        Ignored = r.Ignored.Signs,
                    },
                ],
            });
        }
        return defs;
    }

    /// <summary>A people met for the first time: its own composed meeting, else the old shared one.</summary>
    public static StoryDef? PeopleContact(Town town, Settlement s) =>
        town.Content.Stories.Story(PeoplePrefix + s.Kin.Id) ?? town.Content.Stories.Story(Powers.ContactSource + "-people");

    /// <summary>Beasts and the mindless send no herald: the town finds their signs instead.</summary>
    public static bool Mindless(Menace m) => m.Kind == "beasts" || m.Cunning == "mindless";

    /// <summary>
    /// Daily: each menace met since the last look sends its herald, or leaves its signs, once. Meetings from a save
    /// made before this (or more than a day old) pass without a scene.
    /// </summary>
    public static void Daily(Town town)
    {
        var met = town.Discovered;
        for (int i = town.HeraldsSeen; i < met.Count; i++)
        {
            var c = met[i];
            if (town.Outcome != null || c.Day < town.Day - 1 || !c.Key.StartsWith("menace:", StringComparison.Ordinal)) continue;
            if (!int.TryParse(c.Key.AsSpan(7), out int mid) || town.Saga.Menaces.FirstOrDefault(x => x.Id == mid) is not { Alive: true } m) continue;
            Herald(town, m);
        }
        town.HeraldsSeen = met.Count;
    }

    /// <summary>Open a menace's first meeting now: its herald, or its signs. Returns the story, or null.</summary>
    public static Story? Herald(Town town, Menace m)
    {
        if (LineageOf(town, m) is not { } l) return null;
        if (town.Content.Stories.Story((Mindless(m) ? SignsPrefix : MenacePrefix) + l.Id) is not { } d) return null;
        return Stories.Contact(town, d, new Story { MenaceId = m.Id, SiteId = m.SeatId != "" ? m.SeatId : null });
    }

    // ---- Overtures ----

    /// <summary>The overtures a faction answers to: one per manner shown for it that has one.</summary>
    public static List<(MannerDef Manner, OvertureDef Overture)> Overtures(Town town, string key) =>
        Of(town, key).Where(m => m.Overture != null).Select(m => (m, m.Overture!)).ToList();

    /// <summary>The DC of an overture against this faction.</summary>
    public static int Dc(Town town, string key, OvertureDef o) => o.Dc + Lean(town, key, o.Skill);

    /// <summary>The faction's name as the Ledger says it.</summary>
    public static string Name(Town town, string key)
    {
        if (key.StartsWith("people:", StringComparison.Ordinal) && int.TryParse(key.AsSpan(7), out int sid) && sid >= 0 && sid < town.Settlements.Count)
            return town.Settlements[sid].People;
        if (key.StartsWith("power:", StringComparison.Ordinal) && town.Content.Powers.Powers.FirstOrDefault(p => p.Id == key[6..]) is { } d)
            return Saga.Sentences(Powers.Name(town, d));
        return "they";
    }

    private static string Slot(string key, string manner) => key + "/" + manner;

    /// <summary>The day an overture can next be made to them.</summary>
    public static int Ready(Town town, string key, string manner) => town.Overtures.Get(Slot(key, manner), 0);

    /// <summary>Why an overture can't be made now, or null.</summary>
    public static string? Refuse(Town town, string key, string manner)
    {
        if (town.Outcome != null) return "the game is over";
        var o = Overtures(town, key).FirstOrDefault(x => x.Manner.Id == manner).Overture;
        if (o == null) return "they don't answer to that";
        if (key.StartsWith("people:", StringComparison.Ordinal))
        {
            if (!int.TryParse(key.AsSpan(7), out int sid) || sid < 0 || sid >= town.Settlements.Count) return "nobody there";
            var s = town.Settlements[sid];
            if (s.Razed || !Neighbours.Known(town, s)) return "not met";
        }
        else if (key.StartsWith("power:", StringComparison.Ordinal))
        {
            if (Powers.Find(town, key[6..]) is not { } p || !Powers.Met(town, p)) return "not met";
            if (p.Side == PowerSide.Enemy) return "it serves the enemy";
        }
        else return "nobody there";
        int ready = Ready(town, key, manner);
        if (ready > town.Day) return $"again in {ready - town.Day} days";
        if (o.Cost > 0 && town.Treasury < o.Cost) return $"needs {Js.Str(o.Cost)} gold";
        return null;
    }

    /// <summary>
    /// Make an overture to a people or power the way one of its manners likes: pay its cost, roll the mayor's skill
    /// against the DC its manners set (kept as the last check so the client shows the die), and move its standing.
    /// Returns the roll, or null if it can't be made.
    /// </summary>
    public static StoryBeat? Overture(Town town, string key, string manner)
    {
        if (Refuse(town, key, manner) != null) return null;
        var o = Overtures(town, key).First(x => x.Manner.Id == manner).Overture;
        if (o.Cost > 0) town.Book(TreasuryAccount.Envoys, -o.Cost);
        town.Overtures[Slot(key, manner)] = town.Day + o.Every;
        StoryBeat beat;
        if (o.Skill == "") beat = new StoryBeat { Tick = town.Tick, Result = StoryResult.Certain };
        else
        {
            beat = Sheet.Roll(town, o.Skill, "", Dc(town, key, o), Sheet.HelpText(town, o.Skill), false, false);
            town.Stories.LastCheck = beat;
            town.Stories.Checks++;
        }
        beat.Option = o.Name;
        bool won = Sheet.Succeeded(beat);
        double shift = won ? o.Win + (beat.Result == StoryResult.Critical ? o.Crit : 0) : o.Lose;
        var effects = won ? new Dictionary<string, double>(o.Effects) : [];
        var story = new Story { Def = "overture:" + manner };
        if (key.StartsWith("people:", StringComparison.Ordinal))
        {
            var s = town.Settlements[int.Parse(key.AsSpan(7))];
            story.SettlementId = s.Id;
            effects["regard"] = shift;
            // A secret traded is a place near them worth knowing.
            if (effects.ContainsKey("reveal"))
                story.SiteId = town.Sites.Where(x => !x.Cleared && x.RegionId == s.RegionId && x.Known != SiteKnowledge.Confirmed)
                    .OrderBy(x => x.Known).ThenBy(x => x.Def.Id, StringComparer.Ordinal).FirstOrDefault()?.Def.Id;
        }
        else
        {
            story.Minor = key[6..];
            effects["minor"] = shift;
            effects.Remove("prosperity");
            effects.Remove("security");
            if (effects.ContainsKey("reveal"))
                story.SiteId = town.Sites.Where(x => !x.Cleared && x.RegionId != null && x.Known == SiteKnowledge.Unheard)
                    .OrderBy(x => town.Regions[x.RegionId!.Value].Days).ThenBy(x => x.Def.Id, StringComparer.Ordinal).FirstOrDefault()?.Def.Id;
        }
        string name = Name(town, key);
        string said = Stories.Apply(town, story, effects);
        string line = (won ? o.Won : o.Lost != "" ? o.Lost : o.Won).Replace("{name}", name).Replace("{town}", town.Name);
        beat.Text = line;
        string roll = o.Skill == "" ? "" : $" ({Negotiation.Said(town, beat)})";
        town.Log(LedgerCategory.Town, Importance.Notable, Saga.Sentences($"{o.Name}{roll}: {line}") + said);
        return beat;
    }
}
