namespace CrownAndCompass.Sim.Systems;

/// <summary>Something an ally asks of the town (accords.json asks).</summary>
public sealed record AllyAskDef
{
    public string Id { get; init; } = "";
    public double Gold { get; init; }
    public double PerProsperity { get; init; }
    public double Qty { get; init; }
    public double Win { get; init; }
    public double Lose { get; init; }
    public string Text { get; init; } = "";
}

/// <summary>A promise a people asked of the town at its first meeting (accords.json promises).</summary>
public sealed record PromiseDef
{
    public string Id { get; init; } = "";

    /// <summary>aid, peace, mark, festival, toll, letters or tribute (see accords.json).</summary>
    public string Kind { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";

    /// <summary>The decor piece a mark raises.</summary>
    public string Decor { get; init; } = "";

    /// <summary>Days a festival promise has to be kept in, or 0 for none.</summary>
    public int DueDays { get; init; }

    /// <summary>Gold a toll pays them a month for each trade route, or a tribute asks.</summary>
    public double Gold { get; init; }
}

public sealed record ParleyTruce
{
    public string Skill { get; init; } = "persuasion";
    public int Dc { get; init; } = 14;
    public double Gold { get; init; }
    public double PerPower { get; init; }
    public int Days { get; init; } = 60;
    public int Every { get; init; } = 90;
}

public sealed record ParleyRansom
{
    public double PerCaptive { get; init; } = 50;
    public int Every { get; init; } = 10;
}

public sealed record ParleyTurn
{
    public string Skill { get; init; } = "deception";
    public int Dc { get; init; } = 15;
    public double Clash { get; init; } = 0.18;
    public int Every { get; init; } = 75;
}

public sealed record ParleyRules
{
    public ParleyTruce Truce { get; init; } = new();
    public ParleyRansom Ransom { get; init; } = new();
    public ParleyTurn Turn { get; init; } = new();
    public Dictionary<string, string> Texts { get; init; } = [];
}

/// <summary>Rivalries, allies' asks and drift, the promises of first meetings and parleys with menaces (content/game/accords.json).</summary>
public sealed record AccordRules
{
    public IReadOnlyList<string[]> Rivals { get; init; } = [];
    public double RivalShare { get; init; } = 0.3;
    public double RivalAllied { get; init; } = 8;

    /// <summary>Rivalry cools a people no lower than this: a grudge, not a war.</summary>
    public double RivalFloor { get; init; } = -30;
    public double AllyDrift { get; init; } = 0.08;
    public double LeaveBelow { get; init; } = 40;
    public int[] AskEvery { get; init; } = [45, 75];
    public int AnswerDays { get; init; } = 12;
    public int ClearDays { get; init; } = 60;
    public IReadOnlyList<AllyAskDef> Asks { get; init; } = [];
    public Dictionary<string, string> AskTexts { get; init; } = [];
    public IReadOnlyList<PromiseDef> Promises { get; init; } = [];
    public double Kept { get; init; } = 6;
    public double Broken { get; init; } = -18;
    public double Kin { get; init; } = -6;
    public int Every { get; init; } = 30;
    public Dictionary<string, string> PromiseTexts { get; init; } = [];
    public ParleyRules Parley { get; init; } = new();

    public PromiseDef? Promise(string id) => Promises.FirstOrDefault(p => p.Id == id);
    public AllyAskDef? Ask(string id) => Asks.FirstOrDefault(a => a.Id == id);
}

public enum PromiseState
{
    Standing,
    Called,
    Kept,
    Broken,
}

/// <summary>The town's word to a people, given at a first meeting.</summary>
public sealed class Promise
{
    public int Id { get; init; }
    public int SettlementId { get; init; }
    public string Def { get; init; } = "";
    public int Day { get; init; }
    public PromiseState State { get; set; }

    /// <summary>When it was called on (aid), and the day it is due (festival) or next pays (toll, letters, tribute).</summary>
    public int CalledDay { get; set; }
    public int NextDay { get; set; }

    /// <summary>The decoration a mark raised, once it stands.</summary>
    public int? DecorId { get; set; }

    /// <summary>How often it has been kept.</summary>
    public int Kept { get; set; }
}

/// <summary>An ally's ask, waiting on the mayor or, for a site, promised and waiting on the heroes.</summary>
public sealed class AllyAsk
{
    public int Id { get; init; }
    public int SettlementId { get; init; }
    public string Kind { get; init; } = "";
    public double Gold { get; init; }
    public Good Good { get; init; }
    public double Qty { get; init; }
    public string? SiteId { get; init; }
    public int Until { get; set; }

    /// <summary>A tribute promised at a first meeting, asked again: refusing it breaks the promise.</summary>
    public int? PromiseId { get; init; }

    /// <summary>The mayor said yes to a site: it must be cleared by <see cref="Until"/>.</summary>
    public bool Taken { get; set; }
    public bool Done { get; set; }
}

public sealed class AccordState
{
    public List<Promise> Promises { get; } = [];
    public List<AllyAsk> Asks { get; } = [];

    /// <summary>Each settlement's next ask, by its id.</summary>
    public OrderedMap<int, int> NextAsk { get; } = new();

    /// <summary>Each menace's next parley of each kind, by "id/kind".</summary>
    public OrderedMap<string, int> NextParley { get; } = new();

    /// <summary>Each menace's truce, by its id: the day it ends.</summary>
    public OrderedMap<int, int> Truces { get; } = new();

    /// <summary>Whether a festival was on yesterday, to see one begin.</summary>
    public bool Festival { get; set; }
}

/// <summary>
/// What keeps the peoples' friendship alive after first contact, and words with the town's enemies. Peoples with old
/// grudges cool when the town warms their rival. Allies drift back toward where they began and ask things now and then;
/// one left too long leaves. The terms a people asks at its first meeting are promises the town keeps or breaks. And a
/// menace the town knows can be parleyed with: a truce, a ransom, or whispers that turn it on its neighbour. Its own
/// dice (<see cref="Dice"/>) leave the main stream alone.
/// </summary>
public static class Accords
{
    public static AccordRules Rules(Town town) => town.Content.Accords;

    private static Rng Dice(Town town, int salt) => new(unchecked((int)Voices.Hash("accords", town.Seed, town.Day, salt)));

    private static string Cap(string s) => Saga.Cap(s);

    // ---- Rivals ----

    /// <summary>Whether two peoples (world.json ids) bear each other a grudge.</summary>
    public static bool Rivals(Town town, string a, string b) =>
        a != b && Rules(town).Rivals.Any(g => g.Length == 2 && (g[0] == a && g[1] == b || g[0] == b && g[1] == a));

    /// <summary>The peoples a people bears a grudge against, as known settlements standing.</summary>
    public static IEnumerable<Settlement> RivalsOf(Town town, Settlement s) =>
        town.Settlements.Where(x => !x.Razed && x.Id != s.Id && Rivals(town, x.Kin.Id, s.Kin.Id));

    /// <summary>The town warmed a people: its rivals take it badly (<see cref="World.Shift"/>).</summary>
    internal static void Warmed(Town town, Settlement s, double gain, bool allied)
    {
        var r = Rules(town);
        foreach (var x in RivalsOf(town, s).Where(x => Neighbours.Known(town, x) && x.Standing > r.RivalFloor).OrderBy(x => x.Id))
            x.Standing = Math.Max(r.RivalFloor, x.Standing - gain * r.RivalShare - (allied ? r.RivalAllied : 0));
    }

    // ---- The day ----

    public static void Daily(Town town, bool autoMayor)
    {
        if (town.Settlements.Count == 0 || town.Outcome != null) return;
        Leave(town);
        Ask(town);
        Lapse(town);
        Promises(town);
        if (autoMayor) StandIn(town);
        foreach (var (id, day) in town.Accords.Truces.ToList())
            if (day <= town.Day) town.Accords.Truces.Remove(id);
    }

    /// <summary>An ally left too long, or wronged too often, walks away from the alliance.</summary>
    private static void Leave(Town town)
    {
        var r = Rules(town);
        foreach (var s in town.Settlements.Where(x => x.Allied && !x.Razed && x.Standing < r.LeaveBelow).OrderBy(x => x.Id))
        {
            s.Allied = false;
            town.Log(LedgerCategory.Town, Importance.Major, Text(town, r.AskTexts.GetValueOrDefault("left", ""), s));
        }
    }

    // ---- Allies' asks ----

    /// <summary>The asks waiting on the mayor's word.</summary>
    public static IEnumerable<AllyAsk> Waiting(Town town) => town.Accords.Asks.Where(a => !a.Done && !a.Taken);

    /// <summary>Asks the mayor said yes to that wait on the heroes.</summary>
    public static IEnumerable<AllyAsk> Promised(Town town) => town.Accords.Asks.Where(a => !a.Done && a.Taken);

    public static AllyAsk? AskById(Town town, int id) => town.Accords.Asks.FirstOrDefault(a => a.Id == id);

    private static void Ask(Town town)
    {
        var r = Rules(town);
        if (r.Asks.Count == 0) return;
        foreach (var s in town.Settlements.Where(x => x.Allied && !x.Razed && Neighbours.Known(town, x) && !Shadows.Shadowed(x)).OrderBy(x => x.Id))
        {
            var dice = Dice(town, s.Id);
            if (!town.Accords.NextAsk.TryGetValue(s.Id, out int next))
            {
                town.Accords.NextAsk[s.Id] = town.Day + dice.Range(r.AskEvery[0], r.AskEvery[1] + 1);
                continue;
            }
            if (town.Day < next) continue;
            town.Accords.NextAsk[s.Id] = town.Day + dice.Range(r.AskEvery[0], r.AskEvery[1] + 1);
            if (town.Accords.Asks.Any(a => !a.Done && a.SettlementId == s.Id)) continue;
            var kinds = r.Asks.Where(k => Fits(town, s, k)).ToList();
            if (kinds.Count == 0) continue;
            Open(town, s, kinds[dice.Range(0, kinds.Count)], null);
        }
    }

    private static bool Fits(Town town, Settlement s, AllyAskDef k) => k.Id switch
    {
        "goods" => s.Kin.Trade.Wants.Count > 0,
        "clear" => SiteFor(town, s) != null,
        _ => true,
    };

    /// <summary>A known site troubling an ally's roads the town's heroes could take on.</summary>
    private static SiteState? SiteFor(Town town, Settlement s)
    {
        var home = town.Regions[s.RegionId];
        int strongest = town.Heroes.Select(h => h.Hero!.Level).DefaultIfEmpty(0).Max();
        return town.Sites
            .Where(x => !x.Cleared && x.Known != SiteKnowledge.Unheard && x.RegionId is int rid && town.Regions[rid].Ring == home.Ring && x.Def.Tier <= strongest + 1)
            .Where(x => Menaces.At(town, x.Def.Id) is not { BigBad: true })
            .Where(x => !town.Accords.Asks.Any(a => !a.Done && a.SiteId == x.Def.Id))
            .OrderBy(x => x.RegionId == home.Id ? 0 : 1)
            .ThenBy(x => World.AngleBetween(town.Regions[x.RegionId!.Value].Bearing, home.Bearing))
            .ThenBy(x => x.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static AllyAsk Open(Town town, Settlement s, AllyAskDef k, Promise? promise)
    {
        var r = Rules(town);
        var good = s.Kin.Trade.Wants.Count > 0 ? s.Kin.Trade.Wants[0] : Good.Food;
        double gold = promise != null ? Rules(town).Promise(promise.Def)?.Gold ?? k.Gold : Js.Round(k.Gold + s.Prosperity * k.PerProsperity);
        var a = new AllyAsk
        {
            Id = town.Accords.Asks.Count + 1,
            SettlementId = s.Id,
            Kind = k.Id,
            Gold = k.Id == "gift" ? gold : 0,
            Good = good,
            Qty = k.Qty,
            SiteId = k.Id == "clear" ? SiteFor(town, s)?.Def.Id : null,
            Until = town.Day + r.AnswerDays,
            PromiseId = promise?.Id,
        };
        town.Accords.Asks.Add(a);
        town.Log(LedgerCategory.Town, Importance.Notable, AskText(town, a, k.Text));
        return a;
    }

    /// <summary>What the ask is, in a few words, for its row.</summary>
    public static string Describe(Town town, AllyAsk a) => a.Kind switch
    {
        "gift" => $"{Js.Str(a.Gold)} gold",
        "goods" => $"{Js.Str(a.Qty)} {a.Good.ToString().ToLowerInvariant()}",
        "clear" => Quests.Site(town, a.SiteId!)?.Def.Name ?? "a lair",
        _ => a.Kind,
    };

    private static string AskText(Town town, AllyAsk a, string text)
    {
        var s = town.Settlements[a.SettlementId];
        return Cap(Text(town, text, s)
            .Replace("{gold}", Js.Str(a.Gold))
            .Replace("{qty}", Js.Str(a.Qty))
            .Replace("{good}", a.Good.ToString().ToLowerInvariant())
            .Replace("{site}", a.SiteId != null ? Quests.Site(town, a.SiteId)?.Def.Name ?? "" : ""));
    }

    private static string Text(Town town, string text, Settlement s) =>
        text.Replace("{settlement}", s.Name).Replace("{people}", Cap(s.People)).Replace("{leader}", s.Leader != "" ? s.Leader : "the elders")
            .Replace("{town}", town.Name).Replace("{kin}", s.Kin.People);

    /// <summary>Why the town can't say yes to this ask now, or null.</summary>
    public static string? AnswerBlocked(Town town, AllyAsk a)
    {
        if (a.Done || a.Taken) return "answered";
        if (a.Kind == "gift" && town.Treasury < a.Gold) return $"needs {Js.Str(a.Gold)} gold";
        if (a.Kind == "goods" && town.Market.Stock(a.Good) < a.Qty) return $"the market has {Js.Str(Math.Floor(town.Market.Stock(a.Good)))} {a.Good.ToString().ToLowerInvariant()}";
        return null;
    }

    /// <summary>The mayor's word on an ally's ask: yes pays it (or promises the site), no costs standing.</summary>
    public static bool Answer(Town town, int askId, bool yes)
    {
        var a = AskById(town, askId);
        if (a == null || a.Done || a.Taken) return false;
        var s = town.Settlements[a.SettlementId];
        var r = Rules(town);
        var k = r.Ask(a.Kind);
        if (k == null) return false;
        if (!yes)
        {
            Close(town, a, s, k.Lose, r.AskTexts.GetValueOrDefault("no", ""), Importance.Notable);
            if (a.PromiseId is int pid) Break(town, town.Accords.Promises[pid - 1]);
            return true;
        }
        if (AnswerBlocked(town, a) != null) return false;
        if (Stewards.Intercept(town, "realm", new StewardAsk { Area = "realm", Kind = "ally", Title = $"Answer {s.Name} · {Describe(town, a)}", Why = "• An ally asks\n• A yes keeps the friendship warm", I1 = a.Id, Cost = a.Gold })) return false;
        switch (a.Kind)
        {
            case "gift":
                town.Book(TreasuryAccount.Region, -a.Gold);
                break;
            case "goods":
                town.Market.Seed(a.Good, -a.Qty);
                break;
            case "clear":
                a.Taken = true;
                a.Until = town.Day + r.ClearDays;
                town.Log(LedgerCategory.Town, Importance.Notable, Cap($"{town.Name} promises {s.Name} it will deal with {Describe(town, a)}."));
                return true;
        }
        Close(town, a, s, k.Win, r.AskTexts.GetValueOrDefault("yes", ""), Importance.Routine);
        if (a.PromiseId is int kp) KeptOnce(town, town.Accords.Promises[kp - 1]);
        return true;
    }

    private static void Close(Town town, AllyAsk a, Settlement s, double shift, string text, Importance importance)
    {
        a.Done = true;
        World.Shift(town, s, shift);
        if (text != "") town.Log(LedgerCategory.Town, importance, AskText(town, a, text));
    }

    /// <summary>Asks nobody answered, and sites promised and cleared or not.</summary>
    private static void Lapse(Town town)
    {
        var r = Rules(town);
        foreach (var a in town.Accords.Asks.Where(a => !a.Done).ToList())
        {
            var s = town.Settlements[a.SettlementId];
            var k = r.Ask(a.Kind);
            if (k == null || s.Razed)
            {
                a.Done = true;
                continue;
            }
            if (a.Taken && a.SiteId != null && Quests.Site(town, a.SiteId) is { Cleared: true })
            {
                Close(town, a, s, k.Win, r.AskTexts.GetValueOrDefault("cleared", ""), Importance.Notable);
                continue;
            }
            if (town.Day < a.Until) continue;
            if (a.Taken) Close(town, a, s, k.Lose * 1.5, r.AskTexts.GetValueOrDefault("failed", ""), Importance.Notable);
            else
            {
                Close(town, a, s, k.Lose, r.AskTexts.GetValueOrDefault("lapsed", ""), Importance.Notable);
                if (a.PromiseId is int pid) Break(town, town.Accords.Promises[pid - 1]);
            }
        }
    }

    // ---- Promises ----

    public static PromiseDef? DefOf(Town town, Promise p) => Rules(town).Promise(p.Def);

    /// <summary>The town's word to a people, kept or not yet tested.</summary>
    public static IEnumerable<Promise> To(Town town, Settlement s) => town.Accords.Promises.Where(p => p.SettlementId == s.Id);

    /// <summary>The promise a first meeting's terms make (story effect promise:id).</summary>
    public static string Give(Town town, Settlement s, string id)
    {
        var d = Rules(town).Promise(id);
        if (d == null || s.Razed || To(town, s).Any(p => p.Def == id && p.State != PromiseState.Broken)) return "";
        var p = new Promise { Id = town.Accords.Promises.Count + 1, SettlementId = s.Id, Def = id, Day = town.Day };
        p.NextDay = d.Kind == "festival" && d.DueDays > 0 ? town.Day + d.DueDays : town.Day + Rules(town).Every;
        town.Accords.Promises.Add(p);
        if (d.Kind == "mark") Mark(town, p, d);
        return $"promised: {d.Name.ToLowerInvariant()}";
    }

    private static string Promised(Town town, string text, Promise p, PromiseDef d) =>
        Cap(Text(town, text, town.Settlements[p.SettlementId]).Replace("{promise}", d.Name.ToLowerInvariant()));

    private static void Promises(Town town)
    {
        var r = Rules(town);
        bool festival = Seasons.Festival(town);
        bool began = festival && !town.Accords.Festival;
        town.Accords.Festival = festival;
        foreach (var p in town.Accords.Promises.Where(p => p.State is PromiseState.Standing or PromiseState.Called).ToList())
        {
            var s = town.Settlements[p.SettlementId];
            var d = DefOf(town, p);
            if (d == null || s.Razed) continue;
            switch (d.Kind)
            {
                case "aid":
                    if (p.State == PromiseState.Standing && s.Trouble != null)
                    {
                        p.State = PromiseState.Called;
                        p.CalledDay = town.Day;
                        town.Log(LedgerCategory.Town, Importance.Notable, Promised(town, r.PromiseTexts.GetValueOrDefault("called", ""), p, d));
                    }
                    break;
                case "mark":
                    if (p.DecorId is not int did) Mark(town, p, d);
                    else if (town.Decor.Pieces.All(x => x.Id != did)) Break(town, p);
                    break;
                case "festival":
                    if (began) KeptOnce(town, p);
                    else if (d.DueDays > 0 && p.Kept == 0 && town.Day >= p.NextDay) Break(town, p);
                    break;
                case "toll":
                    if (town.Day < p.NextDay) break;
                    p.NextDay = town.Day + r.Every;
                    int routes = Trade.Open(town).Count();
                    if (routes == 0) break;
                    double gold = d.Gold * routes;
                    if (town.Treasury < gold)
                    {
                        Break(town, p);
                        break;
                    }
                    town.Book(TreasuryAccount.Region, -gold);
                    town.Log(LedgerCategory.Town, Importance.Routine, Cap(Text(town, r.PromiseTexts.GetValueOrDefault("toll", ""), s).Replace("{gold}", Js.Str(gold))));
                    KeptOnce(town, p, quiet: true);
                    break;
                case "letters":
                    if (town.Day < p.NextDay) break;
                    p.NextDay = town.Day + r.Every;
                    if (Letter(town, s) is { } site)
                    {
                        town.Log(LedgerCategory.Town, Importance.Routine, Cap(Text(town, r.PromiseTexts.GetValueOrDefault("letters", ""), s).Replace("{site}", site.Def.Name)));
                        KeptOnce(town, p, quiet: true);
                    }
                    break;
                case "tribute":
                    if (town.Day < p.NextDay) break;
                    p.NextDay = town.Day + r.Every * 2;
                    if (!town.Accords.Asks.Any(a => !a.Done && a.SettlementId == s.Id) && r.Ask("gift") is { } gift) Open(town, s, gift, p);
                    break;
            }
        }
    }

    /// <summary>A people's trouble ended: a promise of aid called on is kept if the town helped, else it stands for next time.</summary>
    internal static void TroubleEnded(Town town, Settlement s, bool helped, bool failed)
    {
        foreach (var p in To(town, s).Where(p => p.State == PromiseState.Called).ToList())
        {
            if (helped) KeptOnce(town, p);
            else if (failed) Break(town, p);
            else p.State = PromiseState.Standing;
        }
    }

    /// <summary>The town marches on a people: every word it gave them is broken.</summary>
    internal static void Betrayed(Town town, Settlement s)
    {
        foreach (var p in To(town, s).Where(p => p.State is PromiseState.Standing or PromiseState.Called).ToList()) Break(town, p);
    }

    private static void KeptOnce(Town town, Promise p, bool quiet = false)
    {
        var r = Rules(town);
        var d = DefOf(town, p)!;
        var s = town.Settlements[p.SettlementId];
        p.Kept++;
        // A promise of aid stands again for the next trouble; the rest go on as they were.
        p.State = PromiseState.Standing;
        World.Shift(town, s, r.Kept);
        if (!quiet) town.Log(LedgerCategory.Town, Importance.Notable, Promised(town, r.PromiseTexts.GetValueOrDefault("kept", ""), p, d));
    }

    private static void Break(Town town, Promise p)
    {
        if (p.State is PromiseState.Broken) return;
        var r = Rules(town);
        var d = DefOf(town, p)!;
        var s = town.Settlements[p.SettlementId];
        p.State = PromiseState.Broken;
        World.Shift(town, s, r.Broken);
        foreach (var x in town.Settlements.Where(x => x.Id != s.Id && !x.Razed && x.Kin.Id == s.Kin.Id).OrderBy(x => x.Id))
            World.Shift(town, x, r.Kin);
        town.Log(LedgerCategory.Town, Importance.Major, Promised(town, r.PromiseTexts.GetValueOrDefault("broken", ""), p, d));
    }

    /// <summary>A mark goes up in town: the shrine or sign promised, near the heart.</summary>
    private static void Mark(Town town, Promise p, PromiseDef d)
    {
        var def = Decor.Rules(town).Pieces.FirstOrDefault(x => x.Id == d.Decor);
        if (def == null || town.Buildings.Count == 0) return;
        var (hx, hy) = Decor.Heart;
        if (Decor.Spot(town, def, hx, hy, spareLand: true, within: 6) is not { } at) return;
        if (Decor.Place(town, def.Id, at.X, at.Y, 0, quiet: true) is not { } piece) return;
        p.DecorId = piece.Id;
        town.Log(LedgerCategory.Town, Importance.Notable, Cap(Text(town, Rules(town).PromiseTexts.GetValueOrDefault("mark", ""), town.Settlements[p.SettlementId]).Replace("{piece}", Decor.Describe(town, piece))));
    }

    /// <summary>A letter from a people: a site near them the town has not had sure word of.</summary>
    private static SiteState? Letter(Town town, Settlement s)
    {
        var site = town.Sites.Where(x => !x.Cleared && x.RegionId == s.RegionId && x.Known != SiteKnowledge.Confirmed && !(x.Known == SiteKnowledge.Rumoured && x.Trust >= 70))
            .OrderBy(x => x.Def.Tier).ThenBy(x => x.Def.Id, StringComparer.Ordinal).FirstOrDefault();
        if (site == null) return null;
        Rumors.Hear(town, site, Rumors.Occupants(town.Content, site.Def), site.Def.Tier, 70);
        return site;
    }

    // ---- The stand-in ----

    /// <summary>The Envoy steward answers asks it can afford, keeping a reserve.</summary>
    private static void StandIn(Town town)
    {
        foreach (var a in Waiting(town).ToList())
        {
            if (AnswerBlocked(town, a) != null || town.Treasury < a.Gold + 200) continue;
            if (a.Kind == "goods" && town.Market.Stock(a.Good) < a.Qty * 2) continue;
            Answer(town, a.Id, true);
        }
    }

    // ---- Parley ----

    public const string Truce = "truce", Ransom = "ransom", Turn = "turn";

    /// <summary>Whether a menace keeps a truce with the town today.</summary>
    public static bool InTruce(Town town, Menace m) => town.Accords.Truces.TryGetValue(m.Id, out int until) && until > town.Day;

    public static int TruceLeft(Town town, Menace m) => town.Accords.Truces.TryGetValue(m.Id, out int until) ? Math.Max(0, until - town.Day) : 0;

    public static double TruceCost(Town town, Menace m) => Js.Round(Rules(town).Parley.Truce.Gold + m.Power * Rules(town).Parley.Truce.PerPower);

    /// <summary>The captives a menace holds, at its seat and its holdings.</summary>
    public static List<Captivity> Held(Town town, Menace m) =>
        Captives.Held(town).Where(c => c.SiteId == m.SeatId || m.Holdings.Contains(c.SiteId)).OrderBy(c => c.Id).ToList();

    public static double RansomCost(Town town, Menace m) => Js.Round(Held(town, m).Sum(c => c.Captives.Count) * Rules(town).Parley.Ransom.PerCaptive);

    /// <summary>The menace a turned one falls on: the nearest other menace alive, not the big bad.</summary>
    public static Menace? TurnOn(Town town, Menace m)
    {
        var seat = Quests.Site(town, m.SeatId);
        var home = seat != null ? World.RegionOf(town, seat) : null;
        if (home == null) return null;
        return Menaces.Alive(town)
            .Where(o => o != m && !o.BigBad && o.Origin != Menaces.FromWarband && Quests.Site(town, o.SeatId) is { } os && World.RegionOf(town, os) != null)
            .Select(o => (Menace: o, Region: World.RegionOf(town, Quests.Site(town, o.SeatId)!)!))
            .Where(x => Math.Abs(x.Region.Ring - home.Ring) <= 1)
            .OrderBy(x => World.AngleBetween(x.Region.Bearing, home.Bearing))
            .ThenBy(x => x.Menace.Id)
            .Select(x => x.Menace)
            .FirstOrDefault();
    }

    public static string Key(Menace m) => "menace:" + m.Id;

    public static int Dc(Town town, Menace m, string kind)
    {
        var p = Rules(town).Parley;
        return kind == Truce ? p.Truce.Dc + Manners.Lean(town, Key(m), p.Truce.Skill) : p.Turn.Dc + Manners.Lean(town, Key(m), p.Turn.Skill);
    }

    public static string SkillOf(Town town, string kind) => kind == Truce ? Rules(town).Parley.Truce.Skill : kind == Turn ? Rules(town).Parley.Turn.Skill : "";

    private static int Every(Town town, string kind) => kind switch
    {
        Truce => Rules(town).Parley.Truce.Every,
        Ransom => Rules(town).Parley.Ransom.Every,
        _ => Rules(town).Parley.Turn.Every,
    };

    /// <summary>Why the town can't parley so with a menace now, or null.</summary>
    public static string? ParleyBlocked(Town town, Menace m, string kind)
    {
        if (town.Outcome != null) return "the game is over";
        if (!m.Alive || m.Origin == Menaces.FromWarband || !Discovery.Known(town, m)) return "no one to talk to";
        if (town.Accords.NextParley.TryGetValue($"{m.Id}/{kind}", out int next) && next > town.Day) return $"again in {next - town.Day} days";
        switch (kind)
        {
            case Truce:
                if (m.BigBad) return "the big bad makes no truce";
                if (m.ArrivalTick != null) return "its host is on the march";
                if (InTruce(town, m)) return $"a truce holds {TruceLeft(town, m)} more days";
                if (town.Treasury < TruceCost(town, m)) return $"needs {Js.Str(TruceCost(town, m))} gold";
                return null;
            case Ransom:
                if (Held(town, m).Count == 0) return "it holds none of the town's people";
                if (town.Treasury < RansomCost(town, m)) return $"needs {Js.Str(RansomCost(town, m))} gold";
                return null;
            case Turn:
                if (m.BigBad) return "the big bad listens to no one";
                if (TurnOn(town, m) == null) return "no rival near enough";
                return null;
        }
        return "they don't answer to that";
    }

    /// <summary>
    /// Words with a menace: buy a truce (a roll), ransom its captives, or turn it on its neighbour (a roll). Returns the
    /// roll (a sure one for a ransom), or null if it can't be made.
    /// </summary>
    public static StoryBeat? Parley(Town town, int menaceId, string kind)
    {
        var m = town.Saga.Menaces.FirstOrDefault(x => x.Id == menaceId);
        if (m == null || ParleyBlocked(town, m, kind) != null) return null;
        var p = Rules(town).Parley;
        town.Accords.NextParley[$"{m.Id}/{kind}"] = town.Day + Every(town, kind);
        string T(string id) => p.Texts.GetValueOrDefault(id, "").Replace("{leader}", m.Leader).Replace("{menace}", m.Name).Replace("{town}", town.Name);
        StoryBeat beat;
        if (kind == Ransom)
        {
            var held = Held(town, m);
            double gold = RansomCost(town, m);
            int count = held.Sum(c => c.Captives.Count);
            town.Book(TreasuryAccount.Tribute, -gold);
            foreach (var c in held) Captives.Free(town, c, null);
            beat = new StoryBeat { Tick = town.Tick, Result = StoryResult.Certain, Option = "Ransom", Text = Cap(T("ransom").Replace("{gold}", Js.Str(gold)).Replace("{count}", count.ToString())) };
            town.Log(LedgerCategory.War, Importance.Notable, beat.Text);
            return beat;
        }
        string skill = SkillOf(town, kind);
        beat = Sheet.Roll(town, skill, "", Dc(town, m, kind), Sheet.HelpText(town, skill), false, false);
        town.Stories.LastCheck = beat;
        town.Stories.Checks++;
        bool won = Sheet.Succeeded(beat);
        beat.Option = kind == Truce ? "Truce" : "Turn";
        if (kind == Truce)
        {
            if (won)
            {
                double gold = TruceCost(town, m);
                town.Book(TreasuryAccount.Tribute, -gold);
                town.Accords.Truces[m.Id] = town.Day + p.Truce.Days;
                m.DemandDueTick = null;
                beat.Text = Cap(T("truce").Replace("{gold}", Js.Str(gold)).Replace("{days}", p.Truce.Days.ToString()));
            }
            else beat.Text = Cap(T("truceFailed"));
        }
        else
        {
            var other = TurnOn(town, m)!;
            if (won)
            {
                double clash = p.Turn.Clash;
                double hit = Math.Min(m.Power, other.Power) * clash;
                m.Power = Math.Max(0, m.Power - hit);
                other.Power = Math.Max(0, other.Power - hit);
                beat.Text = Cap(T("turn").Replace("{other}", other.Name));
            }
            else beat.Text = Cap(T("turnFailed"));
        }
        town.Log(LedgerCategory.War, won ? Importance.Major : Importance.Notable, Saga.Sentences($"{beat.Option} ({Negotiation.Said(town, beat)}): {beat.Text}"));
        return beat;
    }
}
