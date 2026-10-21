namespace CrownAndCompass.Sim.Systems;

/// <summary>What a patron takes from a hero whose favour hits bottom.</summary>
public sealed record PatronPrice
{
    public string Text { get; init; } = "";

    /// <summary>Share of their hit points, dark gifts carried, loyalty to the town lost, share of their purse, and whether it leaves them restless.</summary>
    public double Wound { get; init; }
    public int Taint { get; init; }
    public double Loyalty { get; init; }
    public double Gold { get; init; }
    public int Restless { get; init; }
}

/// <summary>One power beyond the world that makes pacts with heroes (patrons.json "patrons").</summary>
public sealed record PatronDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>fiend, fey, star or celestial.</summary>
    public string Kind { get; init; } = "";
    public string Title { get; init; } = "";

    /// <summary>"blood spilled in its name": what it wants, in words.</summary>
    public string Wants { get; init; } = "";

    /// <summary>What it weighs at each judging, per unit of each deed since the last: kills, delves, renown, infamy, gold, lore.</summary>
    public IReadOnlyDictionary<string, double> Deeds { get; init; } = new Dictionary<string, double>();

    /// <summary>What each point of favour adds to the hero's attack, armour and skill (negative favour takes it away).</summary>
    public IReadOnlyDictionary<string, double> Boon { get; init; } = new Dictionary<string, double>();
    public string GiftText { get; init; } = "";
    public PatronPrice Price { get; init; } = new();

    /// <summary>The familiars it sends its warlocks; one is picked by the hero, without dice.</summary>
    public IReadOnlyList<string> Familiars { get; init; } = [];
}

public sealed record PatronBargain
{
    /// <summary>Chance a hero failing the veiled room is offered a way out; the favour they owe and the dark gifts it leaves.</summary>
    public double Chance { get; init; } = 0.45;
    public int Favour { get; init; } = -2;
    public int Taint { get; init; } = 1;

    /// <summary>The patron that offers an unsworn hero the way back.</summary>
    public string Fiend { get; init; } = "";
}

/// <summary>Ledger lines: {hero}, {heroName}, {patron}, {title}, {wants}, {favour}, {giftText}, {priceText}, {familiar}.</summary>
public sealed record PatronLines
{
    public string Sworn { get; init; } = "";
    public string Warms { get; init; } = "";
    public string Cools { get; init; } = "";
    public string Gift { get; init; } = "";
    public string Price { get; init; } = "";
    public string Broken { get; init; } = "";
    public string Familiar { get; init; } = "";
    public string Bargain { get; init; } = "";
}

/// <summary>The veiled room a fallen hero wakes in (Expedition): {name}, {rolls}, {patron}.</summary>
public sealed record VeilLines
{
    public string Wake { get; init; } = "";
    public string Pass { get; init; } = "";
    public string Fail { get; init; } = "";
    public string Offer { get; init; } = "";
    public string Take { get; init; } = "";
    public string Refuse { get; init; } = "";
}

/// <summary>Patrons and pacts (content/game/patrons.json).</summary>
public sealed record PatronRules
{
    public int JudgeEvery { get; init; } = 15;
    public double Pleased { get; init; } = 1;
    public double Idle { get; init; } = 0.25;
    public int Min { get; init; } = -5;
    public int Max { get; init; } = 5;
    public int Start { get; init; } = 1;
    public int AfterGift { get; init; } = 3;
    public int AfterPrice { get; init; } = -2;
    public int DesignAt { get; init; } = 4;

    /// <summary>The gift at the top of favour: heroXp is a share of what the hero needs for their next level.</summary>
    public IReadOnlyDictionary<string, double> Gift { get; init; } = new Dictionary<string, double>();

    /// <summary>What a familiar adds to its warlock's skill.</summary>
    public int Familiar { get; init; } = 1;
    public PatronBargain Bargain { get; init; } = new();
    public PatronLines Lines { get; init; } = new();
    public VeilLines Veil { get; init; } = new();
    public IReadOnlyList<PatronDef> Patrons { get; init; } = [];

    public PatronDef? Patron(string id) => Patrons.FirstOrDefault(p => p.Id == id);
}

/// <summary>
/// Patrons, after the warlock's pact: a fiend prince, a fey queen, a star that whispers in dreams and a fallen angel
/// who dices with fate. Every warlock comes to town sworn to one, with a familiar it sent; stories let a patron court
/// other heroes, and a hero dying on a delve may be bought back by one. A sworn hero's favour runs from -5 to +5 and
/// moves as the patron judges them every few days against what it wants (blood, songs, secrets, risks): favour lends
/// its boon to their attack, armour or skill, and turns it against them when it falls below nothing. At the top the
/// patron sends a gift; at the bottom it takes its price. Patrons have designs of their own on the town, which come
/// to the mayor as stories once their sworn heroes have pleased them enough. No dice: judging is by deeds, familiars
/// by the hero's id.
/// </summary>
public static class Patrons
{
    public static PatronRules Rules(Content c) => c.Patrons;

    public static PatronDef? Of(Content c, HeroSheet s) => s.Patron == "" ? null : Rules(c).Patron(s.Patron);

    /// <summary>The heroes in town sworn to a patron, in id order.</summary>
    public static List<Agent> Sworn(Town town, string patron) =>
        town.Heroes.Where(a => a.Hero!.Patron == patron).OrderBy(a => a.Id).ToList();

    /// <summary>How hard a patron pulls on the town: the favour its sworn heroes at home hold between them.</summary>
    public static int Pull(Town town, string patron) => Sworn(town, patron).Where(a => !a.IsAway).Sum(a => Math.Max(0, a.Hero!.Favour));

    /// <summary>The boon (or bane) favour lends to one of attack, ac or skill, rounded toward zero; a familiar adds to skill.</summary>
    public static int Boon(Content c, HeroSheet s, string kind)
    {
        if (Of(c, s) is not { } p) return 0;
        int b = p.Boon.TryGetValue(kind, out double w) ? (int)Math.Truncate(w * s.Favour) : 0;
        if (kind == "skill" && s.Familiar != "") b += Rules(c).Familiar;
        return b;
    }

    /// <summary>What a patron's deeds stand at for a hero now, by kind.</summary>
    private static double Deed(Town town, Agent a, string kind) => kind switch
    {
        "kills" => a.Hero!.Kills,
        "delves" => a.Hero!.Expeditions,
        "renown" => a.Hero!.Renown,
        "infamy" => a.Hero!.Infamy,
        "gold" => a.Gold,
        "lore" => town.Research.Known.Count + town.Research.Books.Count,
        _ => 0,
    };

    /// <summary>Swear a hero to a patron: favour starts at the start, its deeds are counted from now, a warlock gets a familiar.</summary>
    public static void Swear(Town town, Agent a, string patron, int? favour = null, bool quiet = false)
    {
        var r = Rules(town.Content);
        if (r.Patron(patron) is not { } p) return;
        var s = a.Hero!;
        s.Patron = p.Id;
        s.Favour = Math.Clamp(favour ?? r.Start, r.Min, r.Max);
        s.PactJudged = town.Day;
        Mark(town, a, p);
        if (!quiet) Log(town, a, r.Lines.Sworn, Importance.Notable);
        if (s.Cls.Id == "warlock" && s.Familiar == "" && p.Familiars.Count > 0)
        {
            s.Familiar = p.Familiars[(int)(Voices.Hash("familiar", a.Id) % (uint)p.Familiars.Count)];
            if (!quiet) Log(town, a, r.Lines.Familiar, Importance.Routine);
        }
    }

    /// <summary>The hero breaks their pact: the boon goes, and so does the familiar.</summary>
    public static void Break(Town town, Agent a)
    {
        var s = a.Hero!;
        if (s.Patron == "") return;
        Log(town, a, Rules(town.Content).Lines.Broken, Importance.Notable);
        s.Patron = "";
        s.Favour = 0;
        s.Familiar = "";
        s.PactRefused = true;
        s.PactMarks.Clear();
    }

    private static void Mark(Town town, Agent a, PatronDef p)
    {
        a.Hero!.PactMarks.Clear();
        foreach (var k in p.Deeds.Keys.OrderBy(k => k, StringComparer.Ordinal)) a.Hero.PactMarks[k] = Deed(town, a, k);
    }

    /// <summary>Move a hero's favour, with its gift at the top and its price at the bottom.</summary>
    public static void Shift(Town town, Agent a, int by)
    {
        var r = Rules(town.Content);
        var s = a.Hero!;
        if (by == 0 || Of(town.Content, s) is not { } p) return;
        s.Favour = Math.Clamp(s.Favour + by, r.Min, r.Max);
        if (s.Favour >= r.Max)
        {
            Log(town, a, r.Lines.Gift, Importance.Notable);
            if (r.Gift.TryGetValue("heroXp", out double share) && Quests.XpToNext(s.Level) is int next and > 0) Quests.GainXp(town, a, (int)(next * share));
            s.Favour = r.AfterGift;
        }
        else if (s.Favour <= r.Min)
        {
            Log(town, a, r.Lines.Price, Importance.Major);
            var pr = p.Price;
            if (pr.Wound > 0) s.Hp = Math.Max(1, s.Hp - (int)Math.Ceiling(s.MaxHp * pr.Wound));
            s.Taint += pr.Taint;
            if (pr.Loyalty > 0) s.Loyalty = Math.Max(0, s.Loyalty - pr.Loyalty);
            if (pr.Gold > 0) a.Gold -= Math.Floor(Math.Max(0, a.Gold) * pr.Gold);
            if (pr.Restless > 0) s.Restless = true;
            s.Favour = r.AfterPrice;
        }
        else Log(town, a, by > 0 ? r.Lines.Warms : r.Lines.Cools, Importance.Routine);
    }

    /// <summary>Daily: warlocks without a patron are sworn to one (by their id), and each sworn hero whose turn has come is judged.</summary>
    public static void Daily(Town town)
    {
        var r = Rules(town.Content);
        if (r.Patrons.Count == 0) return;
        foreach (var a in town.Heroes.OrderBy(a => a.Id))
        {
            var s = a.Hero!;
            if (s.Patron == "" && s.Cls.Id == "warlock" && !s.PactRefused)
                Swear(town, a, r.Patrons[(int)(Voices.Hash("patron", a.Id) % (uint)r.Patrons.Count)].Id, quiet: a.ArrivedTick == town.Tick);
            if (Of(town.Content, s) is not { } p || a.IsAway || town.Day - s.PactJudged < r.JudgeEvery) continue;
            s.PactJudged = town.Day;
            double score = 0;
            foreach (var (k, w) in p.Deeds) score += w * (Deed(town, a, k) - s.PactMarks.Get(k, Deed(town, a, k)));
            Mark(town, a, p);
            if (score >= r.Pleased - 1e-9) Shift(town, a, 1);
            else if (score < r.Idle) Shift(town, a, -1);
        }
    }

    /// <summary>
    /// A hero who failed the veiled room: the patron they are sworn to, or the fiend for the unsworn, may offer a way
    /// back (on the venture's dice). Returns the patron, or null if none offers or the hero refuses.
    /// </summary>
    public static PatronDef? Offer(Content c, Rng rng, HeroSheet s)
    {
        var b = Rules(c).Bargain;
        var p = Of(c, s) ?? Rules(c).Patron(b.Fiend);
        if (p == null || !rng.Chance(b.Chance)) return null;
        return p;
    }

    /// <summary>The bargains a party struck in the veiled room, sealed when it is home.</summary>
    public static void Home(Town town, ExpeditionResult result)
    {
        foreach (var (id, patron) in result.Bargains)
        {
            var a = town.Agent(id);
            if (a.Hero == null || a.Dead) continue;
            Bargained(a.Hero, patron, town.Content, town.Day);
            town.Log(LedgerCategory.Story, Importance.Major, Saga.Sentences(Fill(town, a, Rules(town.Content).Lines.Bargain)), [a.Id]);
        }
    }

    /// <summary>A bargain struck in the veiled room: sworn (or deeper in debt), and a dark gift.</summary>
    public static void Bargained(HeroSheet s, string patron, Content c, int day)
    {
        var r = Rules(c);
        if (r.Patron(patron) is not { } p) return;
        s.Taint += r.Bargain.Taint;
        if (s.Patron == p.Id) s.Favour = Math.Clamp(s.Favour + r.Bargain.Favour, r.Min, r.Max);
        else
        {
            s.Patron = p.Id;
            s.Favour = Math.Clamp(r.Bargain.Favour, r.Min, r.Max);
            s.PactJudged = day;
            s.PactMarks.Clear();
        }
    }

    /// <summary>"favour +3", for cards.</summary>
    public static string Favour(int f) => f > 0 ? $"+{f}" : f.ToString();

    public static string Fill(Town town, Agent a, string text)
    {
        var s = a.Hero!;
        var p = Of(town.Content, s);
        return text
            .Replace("{hero}", a.Name)
            .Replace("{heroName}", a.GivenName)
            .Replace("{patron}", p?.Name ?? "its patron")
            .Replace("{title}", p?.Title ?? "")
            .Replace("{wants}", p?.Wants ?? "")
            .Replace("{favour}", Favour(s.Favour))
            .Replace("{giftText}", p?.GiftText ?? "")
            .Replace("{priceText}", p?.Price.Text ?? "")
            .Replace("{familiar}", s.Familiar == "" ? "a familiar" : char.ToUpperInvariant(s.Familiar[0]) + s.Familiar[1..]);
    }

    private static void Log(Town town, Agent a, string line, Importance importance)
    {
        if (line == "") return;
        town.Log(LedgerCategory.Story, importance, Saga.Sentences(Fill(town, a, line)), [a.Id]);
    }
}
