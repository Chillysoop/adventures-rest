namespace CrownAndCompass.Sim.Systems;

/// <summary>Where a tavern keeper came from, and what it gives their house (taverns.json "pasts").</summary>
public sealed record TavernPast
{
    public string Id { get; init; } = "";

    /// <summary>"a retired adventurer", and the one line of their story.</summary>
    public string Name { get; init; } = "";
    public string Story { get; init; } = "";

    /// <summary>heroes, calm, ale, perform, watch or warm, and how much.</summary>
    public string Perk { get; init; } = "";
    public double Value { get; init; }
    public string PerkText { get; init; } = "";
}

/// <summary>What makes a tavern odd, and the crowd it draws (taverns.json "quirks").</summary>
public sealed record TavernQuirk
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";

    /// <summary>Traits of the townsfolk it draws, and whether it draws heroes; they walk Draw tiles further to it.</summary>
    public List<string> Traits { get; init; } = [];
    public bool Heroes { get; init; }
    public double Draw { get; init; }

    /// <summary>Added to talk rolls there, times the chance of a brawl, added to songs.</summary>
    public int Talk { get; init; }
    public double Brawl { get; init; } = 1;
    public int Perform { get; init; }
}

/// <summary>The kind of drunk someone becomes (taverns.json "personas").</summary>
public sealed record DrunkPersona
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Drinks in an evening before it shows.</summary>
    public int At { get; init; } = 3;
    public int Talk { get; init; }
    public double Brawl { get; init; } = 1;

    /// <summary>{name}, {tavern}, {slang:plain}.</summary>
    public List<string> Lines { get; init; } = [];
}

/// <summary>One plain word and the ways a town might say it instead.</summary>
public sealed record SlangWord
{
    public string Plain { get; init; } = "";
    public List<string> Words { get; init; } = [];
}

public sealed record BloodRules
{
    public int Drinks { get; init; } = 3;

    /// <summary>Per mille of drunk heroes' natural 20s on a townsperson that kill.</summary>
    public int Chance { get; init; } = 400;

    /// <summary>{tavern}, {hero}, {victim}, {victimName}, {kin}.</summary>
    public string Line { get; init; } = "";
    public string Settled { get; init; } = "";
    public string Feud { get; init; } = "";
}

public sealed record TavernRules
{
    public List<TavernPast> Pasts { get; init; } = [];
    public List<TavernQuirk> Quirks { get; init; } = [];
    public List<DrunkPersona> Personas { get; init; } = [];
    public int PersonaEvery { get; init; } = 10;
    public List<SlangWord> Slang { get; init; } = [];
    public BloodRules Blood { get; init; } = new();

    public TavernPast? Past(string id) => Pasts.FirstOrDefault(p => p.Id == id);
}

/// <summary>A townsperson a drunken hero killed in a brawl, and what their kin were given for it.</summary>
public sealed class BloodDebt
{
    public int HeroId { get; init; }
    public int VictimId { get; init; }
    public string Victim { get; init; } = "";

    /// <summary>Their spouse, or the eldest of their family left in town; null when nobody is left to ask.</summary>
    public int? KinId { get; init; }
    public string Kin { get; init; } = "";
    public int Day { get; init; }

    /// <summary>"" until the town hall rules: "paid" (weregild), "feud" (the kin swore one).</summary>
    public string Settled { get; set; } = "";
}

/// <summary>
/// Taverns with a character (GDD rule C11, the tavern is the town's living room). Every keeper has a past with a perk
/// (an old sergeant breaks up brawls, a smuggler pours cheap ale, a retired adventurer keeps heroes fond of the town)
/// and asks favours of the town as stories; every tavern has a quirk that draws its own crowd and changes the evening;
/// every drinker turns into their own kind of drunk; each town has its own slang for a few things, heard in tavern
/// talk and rumours only; and a drunk hero can kill a townsperson in a brawl, leaving kin who want blood money.
/// No dice of its own: who is what comes from stable hashes, so it never moves the town's random stream.
/// </summary>
public static class Taverns
{
    public static TavernRules Rules(Town town) => town.Content.Taverns;

    /// <summary>Who keeps a tavern: its owner, else its first worker.</summary>
    public static Agent? Keeper(Town town, Building b)
    {
        if (b.OwnerId is int o && town.Agent(o) is { Departed: false } owner) return owner;
        return b.Workers.OrderBy(id => id).Select(town.Agent).FirstOrDefault(w => !w.Departed);
    }

    public static TavernPast? Past(Town town, Building b)
    {
        var r = Rules(town);
        if (r.Pasts.Count == 0 || Keeper(town, b) is not { } k) return null;
        return r.Pasts[(int)(Voices.Hash("keeper-past", k.Id) % (uint)r.Pasts.Count)];
    }

    public static TavernQuirk? Quirk(Town town, Building b)
    {
        var r = Rules(town);
        if (r.Quirks.Count == 0 || !town.Taverns.Contains(b)) return null;
        return r.Quirks[(int)(Voices.Hash("tavern-quirk", b.Id) % (uint)r.Quirks.Count)];
    }

    /// <summary>The perk's value at this tavern, or the neutral value when its keeper's past gives another.</summary>
    private static double Perk(Town town, Building b, string perk, double neutral) =>
        Past(town, b) is { } p && p.Perk == perk ? p.Value : neutral;

    public static DrunkPersona? Persona(Town town, Agent a)
    {
        var r = Rules(town);
        return r.Personas.Count == 0 ? null : r.Personas[(int)(Voices.Hash("persona", a.Id) % (uint)r.Personas.Count)];
    }

    /// <summary>Their persona, once they have had enough to show it.</summary>
    public static DrunkPersona? Drunk(Town town, Agent a) => Persona(town, a) is { } p && a.Drinks >= p.At ? p : null;

    /// <summary>Tiles further someone will walk to a tavern whose quirk draws their kind.</summary>
    public static double Draw(Town town, Agent a, Building b)
    {
        if (Quirk(town, b) is not { } q) return 0;
        bool drawn = q.Heroes && a.Hero != null || q.Traits.Any(t => Enum.TryParse<Trait>(t, out var tr) && a.Has(tr));
        return drawn ? q.Draw : 0;
    }

    /// <summary>Added to a talk roll at a tavern: its quirk and the talker's drunk persona.</summary>
    public static int Talk(Town town, Building b, Agent a) => (Quirk(town, b)?.Talk ?? 0) + (Drunk(town, a)?.Talk ?? 0);

    /// <summary>Times the chance a grudge turns into a brawl.</summary>
    public static double Brawl(Town town, Building b, Agent a) => (Quirk(town, b)?.Brawl ?? 1) * (Drunk(town, a)?.Brawl ?? 1);

    public static int Perform(Town town, Building b) => (Quirk(town, b)?.Perform ?? 0) + (int)Perk(town, b, "perform", 0);

    public static int Calm(Town town, Building b) => (int)Perk(town, b, "calm", 0);

    public static double Ale(Town town, Building b) => Perk(town, b, "ale", 1);

    public static double Warm(Town town, Building b) => Perk(town, b, "warm", 0);

    /// <summary>What the town's informer-keepers add to the watch.</summary>
    public static double Watch(Town town) => Js.Sum(town.Taverns, b => Perk(town, b, "watch", 0));

    // ---------- slang ----------

    /// <summary>How this town says a plain word ("the watch" might be "the lanterns"), or the word itself.</summary>
    public static string Say(Town town, string plain)
    {
        var w = Rules(town).Slang.FirstOrDefault(s => s.Plain == plain);
        if (w == null || w.Words.Count == 0) return plain;
        return w.Words[(int)(Voices.Hash("slang:" + town.Name, Rules(town).Slang.IndexOf(w)) % (uint)w.Words.Count)];
    }

    /// <summary>Tavern talk in the town's own words: every plain word the slang knows, said its way (talk and rumours only, never prices).</summary>
    public static string Slang(Town town, string text)
    {
        foreach (var w in Rules(town).Slang)
        {
            if (w.Plain == "" || !text.Contains(w.Plain, StringComparison.OrdinalIgnoreCase)) continue;
            string said = Say(town, w.Plain);
            text = System.Text.RegularExpressions.Regex.Replace(text, $@"\b{System.Text.RegularExpressions.Regex.Escape(w.Plain)}\b",
                m => char.IsUpper(m.Value[0]) ? Stories.Cap(said) : said, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return text;
    }

    // ---------- the evening ----------

    /// <summary>An evening hour at a tavern: heroes warm to a keeper who was one of them, and now and then someone's drunk self shows.</summary>
    public static void Hour(Town town, Building tavern, List<Agent> patrons)
    {
        double fond = Perk(town, tavern, "heroes", 0);
        if (fond > 0)
            foreach (var h in patrons)
                if (h.Hero != null) h.Hero.Loyalty = Math.Min(100, h.Hero.Loyalty + fond);
        var r = Rules(town);
        if (r.PersonaEvery <= 0 || Voices.Hash("drunk", town.Tick, tavern.Id) % (uint)r.PersonaEvery != 0) return;
        foreach (var p in patrons.OrderBy(p => p.Id))
        {
            if (Drunk(town, p) is not { Lines.Count: > 0 } d) continue;
            string line = d.Lines[(int)(Voices.Hash("drunk-line", town.Tick, p.Id) % (uint)d.Lines.Count)];
            line = System.Text.RegularExpressions.Regex.Replace(line, @"\{slang:([^}]+)\}", m => Say(town, m.Groups[1].Value));
            town.Log(LedgerCategory.Social, Importance.Routine, Stories.Cap(line.Replace("{name}", p.Name).Replace("{tavern}", tavern.Name)), [p.Id]);
            return;
        }
    }

    // ---------- blood money ----------

    /// <summary>
    /// After a brawl: a drunk hero who rolled a natural 20 on a townsperson may have hit them once too hard. They die,
    /// and their kin want blood money. Returns whether it came to that.
    /// </summary>
    public static bool Bloodshed(Town town, Building tavern, Agent winner, Agent loser, int die)
    {
        var b = Rules(town).Blood;
        if (die != 20 || winner.Hero == null || loser.Hero != null || winner.Drinks < b.Drinks) return false;
        if (Voices.Hash("blood", town.Tick, winner.Id, loser.Id) % 1000 >= (uint)b.Chance) return false;
        var kin = loser.SpouseId is int sp && town.Agent(sp) is { Departed: false } spouse ? spouse
            : town.Residents.Where(a => a.Id != loser.Id && a.Hero == null && a.FamilyName == loser.FamilyName).OrderBy(a => a.Id).FirstOrDefault();
        string kinName = kin != null ? $"{kin.Name} and the {loser.FamilyName} family" : $"The {loser.FamilyName} family";
        Threat.Kill(town, loser);
        var debt = new BloodDebt { HeroId = winner.Id, VictimId = loser.Id, Victim = loser.Name, KinId = kin?.Id, Kin = kinName, Day = town.Day };
        town.BloodDebts.Add(debt);
        town.Log(LedgerCategory.Brawl, Importance.Major, Fill(b.Line, town, tavern, debt), [winner.Id, loser.Id]);
        return true;
    }

    /// <summary>The oldest blood debt a hero in town still owes, or null.</summary>
    public static BloodDebt? Owing(Town town, Agent hero) => town.BloodDebts.FirstOrDefault(d => d.HeroId == hero.Id && d.Settled == "");

    /// <summary>Close a hero's blood debt: paid (the kin are mollified) or feud (they swear one on the hero).</summary>
    public static void Settle(Town town, Agent hero, bool paid)
    {
        if (Owing(town, hero) is not { } d) return;
        d.Settled = paid ? "paid" : "feud";
        var b = Rules(town).Blood;
        if (d.KinId is int k && town.Agent(k) is { Departed: false } kin)
        {
            kin.ChangeOpinion(hero.Id, paid ? 20 : -60);
            if (!paid)
                foreach (var f in town.Residents.Where(a => a.Hero == null && a.FamilyName == kin.FamilyName && a.Id != kin.Id))
                    f.ChangeOpinion(hero.Id, -40);
        }
        town.Log(LedgerCategory.Social, paid ? Importance.Notable : Importance.Major, Fill(paid ? b.Settled : b.Feud, town, null, d), [hero.Id]);
    }

    private static string Fill(string line, Town town, Building? tavern, BloodDebt d) =>
        Stories.Cap(line
            .Replace("{tavern}", tavern?.Name ?? "the tavern")
            .Replace("{hero}", town.Agent(d.HeroId).Name)
            .Replace("{victimName}", town.Agent(d.VictimId).GivenName)
            .Replace("{victim}", d.Victim)
            .Replace("{kin}", d.Kin));
}
