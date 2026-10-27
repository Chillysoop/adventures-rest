namespace CrownAndCompass.Sim.Systems;

/// <summary>One moment heroes speak at: who speaks (the hero it is about, someone else, or anyone), how often, and the lines by voice.</summary>
public sealed record VoiceMomentDef
{
    public string By { get; init; } = "any";
    public double Chance { get; init; } = 0.5;
    public Dictionary<string, List<string>> Lines { get; init; } = [];
}

/// <summary>A running joke: after enough of the same mishap a hero earns a nickname, and the party teases them with it.</summary>
public sealed record JokeDef
{
    public int After { get; init; } = 2;
    public List<string> Names { get; init; } = [];
    public string Named { get; init; } = "";
    public List<string> Tease { get; init; } = [];
}

public sealed record TableVoiceDef
{
    public List<string> Cheer { get; init; } = [];
    public List<string> Groan { get; init; } = [];
    public List<string> Told { get; init; } = [];
    public List<string> Sour { get; init; } = [];
}

public sealed record FuneralDef
{
    public List<string> Places { get; init; } = [];
    public string Nowhere { get; init; } = "";
    public string Rite { get; init; } = "";
    public string Wake { get; init; } = "";
    public Dictionary<string, List<string>> Words { get; init; } = [];
    public string Vow { get; init; } = "";
    public string VowWake { get; init; } = "";
    public string Kept { get; init; } = "";
    public string KeptBy { get; init; } = "";
}

/// <summary>content/game/voices.json.</summary>
public sealed record VoiceRules
{
    public Dictionary<string, VoiceMomentDef> Moments { get; init; } = [];
    public Dictionary<string, List<string>> Variants { get; init; } = [];
    public Dictionary<string, JokeDef> Jokes { get; init; } = [];
    public TableVoiceDef Table { get; init; } = new();
    public FuneralDef Funeral { get; init; } = new();

    /// <summary>How much more a hero wants a bounty on the place they swore to see cleared.</summary>
    public double VowPull { get; init; } = 0.4;
}

/// <summary>A mishap or a feat on a delve that may grow into a running joke, carried home with the party.</summary>
public sealed record Gag(int HeroId, string Kind);

/// <summary>
/// Heroes talking in their own voices: on the road and in the dark (the chronicles), at the table when the
/// mayor rolls (the story cards), and over the graves of the fallen. Who speaks and what they say is picked
/// by a hash of the moment, never the town's dice, so the words never change how anything turns out.
/// </summary>
public static class Voices
{
    private static VoiceRules Rules(Content content) => content.Voices;

    /// <summary>A stable number for a moment: FNV-1a over its parts.</summary>
    public static uint Hash(string key, params int[] parts)
    {
        uint h = 2166136261;
        foreach (char ch in key)
        {
            h ^= ch;
            h *= 16777619;
        }
        foreach (int p in parts)
        {
            for (int k = 0; k < 4; k++)
            {
                h ^= (uint)(p >> (8 * k)) & 0xff;
                h *= 16777619;
            }
        }
        return h;
    }

    private static T Pick<T>(IReadOnlyList<T> list, uint h) => list[(int)(h % (uint)list.Count)];

    private static bool Roll(double chance, uint h) => chance >= 1 || (h >> 8) % 10000 < chance * 10000;

    /// <summary>A hero's voices, first to last: their traits, then their temper, then anyone's.</summary>
    public static IEnumerable<string> VoicesOf(Agent a)
    {
        foreach (var t in Enum.GetValues<Trait>()) if (a.Has(t)) yield return t.ToString().ToLowerInvariant();
        if (a.Hero is { Temper: not "" } s) yield return s.Temper;
        yield return "any";
    }

    /// <summary>The first lines a hero's voice has for a set of lines, or null if only "any" would.</summary>
    private static List<string>? LinesFor(Agent a, Dictionary<string, List<string>> lines, bool any)
    {
        foreach (var v in VoicesOf(a))
        {
            if (v == "any" && !any) return null;
            if (lines.TryGetValue(v, out var l) && l.Count > 0) return l;
        }
        return null;
    }

    private static string Fill(string line, Agent speaker, Agent? about, string site) => line
        .Replace("{name}", speaker.GivenName, StringComparison.Ordinal)
        .Replace("{who}", about?.GivenName ?? "", StringComparison.Ordinal)
        .Replace("{nick}", about?.Hero?.Nickname ?? "", StringComparison.Ordinal)
        .Replace("{site}", Trim(site), StringComparison.Ordinal);

    /// <summary>"the Field of Spears" reads "the Field of Spears" after "to"; "the {site}" lines want it bare.</summary>
    private static string Trim(string site) => site.StartsWith("the ", StringComparison.Ordinal) ? site[4..] : site;

    /// <summary>
    /// What somebody says at a moment, or null when nobody speaks. The speaker is the hero it is about
    /// ("self"), one of the others ("other"), or anyone; a voice with lines of its own for the moment is
    /// heard before anyone falling back on the common lines.
    /// </summary>
    public static (string Text, int Speaker)? Say(Content content, string moment, IReadOnlyList<Agent> standing, Agent? about, string site, uint seed)
    {
        if (!Rules(content).Moments.TryGetValue(moment, out var m)) return null;
        uint h = Hash(moment, (int)seed, about?.Id ?? 0);
        if (!Roll(m.Chance, h)) return null;
        var pool = m.By switch
        {
            "self" => about == null ? [] : [about],
            "other" => standing.Where(a => a.Id != about?.Id).ToList(),
            _ => standing.ToList(),
        };
        if (pool.Count == 0) return null;
        // Those with something of their own to say first; then anyone.
        var own = pool.Where(a => LinesFor(a, m.Lines, false) != null).ToList();
        var speaker = Pick(own.Count > 0 ? own : pool, h >> 3);
        var lines = LinesFor(speaker, m.Lines, true);
        return lines == null ? null : (Fill(Pick(lines, h >> 11), speaker, about, site), speaker.Id);
    }

    /// <summary>The party teasing a hero by the nickname their running joke earned.</summary>
    public static (string Text, int Speaker)? Tease(Content content, string kind, IReadOnlyList<Agent> standing, Agent about, uint seed)
    {
        if (about.Hero is not { Nickname: not "" } s || s.NicknameFor != kind || !Rules(content).Jokes.TryGetValue(kind, out var j) || j.Tease.Count == 0) return null;
        var others = standing.Where(a => a.Id != about.Id).ToList();
        if (others.Count == 0) return null;
        uint h = Hash("tease" + kind, (int)seed, about.Id);
        if (!Roll(0.6, h)) return null;
        var speaker = Pick(others, h >> 3);
        return (Fill(Pick(j.Tease, h >> 7), speaker, about, ""), speaker.Id);
    }

    /// <summary>A line said a few different ways, so a long chronicle doesn't read the same line twenty times.</summary>
    public static string Variant(Content content, string key, string fallback, uint seed, params (string Key, string Value)[] fill)
    {
        string line = Rules(content).Variants.TryGetValue(key, out var v) && v.Count > 0 ? Pick(v, Hash(key, (int)seed)) : fallback;
        foreach (var (k, val) in fill)
        {
            line = line.Replace("{" + k + "}", val, StringComparison.Ordinal);
            if (k.Length > 0) line = line.Replace("{" + char.ToUpperInvariant(k[0]) + k[1..] + "}", val.Length > 0 ? char.ToUpperInvariant(val[0]) + val[1..] : val, StringComparison.Ordinal);
        }
        return line;
    }

    // The table -------------------------------------------------------------------------------------

    /// <summary>
    /// How the people a story choice touched take the roll, in a line under the result: those it pleased
    /// cheer a success and groan at a failure; those it angered grumble at a success and look like they knew
    /// it at a failure. Empty when nobody cares.
    /// </summary>
    public static string Table(Town town, IReadOnlyCollection<string> tags, StoryResult result)
    {
        if (tags.Count == 0 || result is StoryResult.Certain or StoryResult.Ignored) return "";
        var (heroes, angry) = Regard.Reactions(town, tags);
        var (blocs, angryBlocs) = Blocs.Reactions(town, tags);
        var pleased = heroes.Select(h => h.GivenName).Take(3).Concat(blocs.Select(b => b.Voice)).ToList();
        var angered = angry.Select(h => h.GivenName).Take(3).Concat(angryBlocs.Select(b => b.Voice)).ToList();
        bool won = result is StoryResult.Critical or StoryResult.Success or StoryResult.Partial;
        var t = Rules(town.Content).Table;
        uint h = Hash("table", town.Tick, (int)result);
        var said = new List<string>();
        if (pleased.Count > 0 && (won ? t.Cheer : t.Groan) is { Count: > 0 } a) said.Add(Pick(a, h).Replace("{names}", Expedition.ListNames(pleased), StringComparison.Ordinal));
        if (angered.Count > 0 && (won ? t.Sour : t.Told) is { Count: > 0 } b) said.Add(Pick(b, h >> 5).Replace("{names}", Expedition.ListNames(angered), StringComparison.Ordinal));
        return string.Join(" ", said);
    }

    // Running jokes ---------------------------------------------------------------------------------

    /// <summary>The party is home: count each hero's mishaps, and a hero who keeps doing the same thing earns a name for it.</summary>
    public static void Jokes(Town town, string party, IReadOnlyList<Gag> gags, IReadOnlyList<Agent> survivors)
    {
        foreach (var g in gags)
        {
            if (survivors.FirstOrDefault(a => a.Id == g.HeroId) is not { Hero: { } s } a) continue;
            s.Gags[g.Kind] = s.Gags.Get(g.Kind, 0) + 1;
            if (s.Nickname != "" || !Rules(town.Content).Jokes.TryGetValue(g.Kind, out var j) || j.Names.Count == 0 || s.Gags.Get(g.Kind, 0) < j.After) continue;
            s.Nickname = Pick(j.Names, Hash("nick" + g.Kind, a.Id));
            s.NicknameFor = g.Kind;
            town.Log(LedgerCategory.Quest, Importance.Notable,
                j.Named.Replace("{party}", party, StringComparison.Ordinal).Replace("{name}", a.GivenName, StringComparison.Ordinal).Replace("{nick}", s.Nickname, StringComparison.Ordinal), [a.Id]);
        }
    }

    // The fallen ------------------------------------------------------------------------------------

    /// <summary>
    /// The fallen are buried at the temple or shrine, each by the friend who loved them best (or, when nobody
    /// could carry them home, mourned together at a wake in the tavern), and if the place that killed them
    /// still stands, a friend swears to see it cleared.
    /// </summary>
    public static void Funeral(Town town, IReadOnlyList<Agent> dead, IReadOnlyList<Agent> mourners, string siteId, string siteName, bool body)
    {
        var f = Rules(town.Content).Funeral;
        if (dead.Count == 0 || f.Rite == "") return;
        var spoke = new List<int>();
        Agent? Speaker(Agent d)
        {
            bool Ok(Agent a) => !dead.Any(x => x.Id == a.Id) && !a.Dead && !a.Departed;
            var pool = mourners.Where(Ok).ToList();
            if (pool.Count == 0) pool = town.Heroes.Where(a => Ok(a) && !a.IsAway).ToList();
            // Each friend speaks once if there are friends enough.
            return pool.OrderBy(a => spoke.Contains(a.Id)).ThenByDescending(a => a.OpinionOf(d.Id)).ThenBy(a => a.Id).FirstOrDefault();
        }
        var temple = town.Buildings.Where(b => b.Complete && (b.Def.Id == town.Content.Shrines.Temple || b.Def.Id == "shrine")).OrderByDescending(b => b.Def.Id == town.Content.Shrines.Temple).ThenBy(b => b.Id).FirstOrDefault();
        string Called(Agent d) => d.Hero is { Nickname: not "" } ds ? $" (\"{ds.Nickname}\" to their friends)" : "";
        var site = Quests.Site(town, siteId);
        bool vowed = false;
        string Vow(Agent speaker, Agent d)
        {
            if (vowed || speaker.Hero is not { Vow: null } || site == null || site.Cleared || speaker.OpinionOf(d.Id) < Social.FriendThreshold) return "";
            vowed = true;
            speaker.Hero.Vow = siteId;
            speaker.Hero.VowFor = d.Name;
            return " " + (body ? f.Vow : f.VowWake).Replace("{speaker}", speaker.GivenName, StringComparison.Ordinal).Replace("{name}", d.GivenName, StringComparison.Ordinal).Replace("{site}", siteName, StringComparison.Ordinal);
        }
        string Words(Agent speaker, Agent d) =>
            LinesFor(speaker, f.Words, true) is { Count: > 0 } words ? " " + Fill(Pick(words, Hash("words", d.Id, speaker.Id)), speaker, d, siteName) : "";

        if (!body)
        {
            // One wake for everyone the road took, spoken for by whoever loved the first of them best.
            var first = dead[0];
            if (Speaker(first) is not { } speaker) return;
            string names = Expedition.ListNames(dead.Select(d => d.Name + Called(d)).ToList());
            string text = f.Wake.Replace("{town}", town.Name, StringComparison.Ordinal).Replace("{name}", names, StringComparison.Ordinal)
                .Replace("{epithet}", "", StringComparison.Ordinal).Replace("{speaker}", speaker.GivenName, StringComparison.Ordinal)
                .Replace("{them}", dead.Count == 1 ? "them" : "each of them", StringComparison.Ordinal);
            text += Words(speaker, first) + Vow(speaker, first);
            town.Log(LedgerCategory.Quest, Importance.Notable, text, [.. dead.Select(d => d.Id), speaker.Id]);
            return;
        }
        foreach (var d in dead)
        {
            if (Speaker(d) is not { } speaker) continue;
            spoke.Add(speaker.Id);
            string place = temple != null && f.Places.Count > 0 ? Pick(f.Places, Hash("grave", d.Id)).Replace("{place}", temple.Name, StringComparison.Ordinal) : f.Nowhere;
            string text = f.Rite.Replace("{place}", place, StringComparison.Ordinal).Replace("{town}", town.Name, StringComparison.Ordinal)
                .Replace("{name}", d.Name, StringComparison.Ordinal).Replace("{epithet}", Called(d), StringComparison.Ordinal)
                .Replace("{speaker}", speaker.GivenName, StringComparison.Ordinal);
            text += Words(speaker, d) + Vow(speaker, d);
            town.Log(LedgerCategory.Quest, Importance.Notable, text, [d.Id, speaker.Id]);
        }
    }

    /// <summary>Vows on places cleared some other way (a siege broken, a lair burnt out) are let go.</summary>
    public static void Forsworn(Town town)
    {
        foreach (var a in town.Agents)
        {
            if (a.Departed || a.Hero is not { Vow: string id }) continue;
            var site = Quests.Site(town, id);
            if (site == null || site.Cleared) Cleared(town, id, site?.Def.Name ?? "the place", []);
        }
    }

    /// <summary>A site has fallen: every hero who swore to see it cleared lets the vow go, with honour if they were there.</summary>
    public static void Cleared(Town town, string siteId, string siteName, IReadOnlyList<Agent> clearedBy)
    {
        var f = Rules(town.Content).Funeral;
        foreach (var a in town.Heroes.Where(a => a.Hero!.Vow == siteId).OrderBy(a => a.Id).ToList())
        {
            var s = a.Hero!;
            bool there = clearedBy.Any(c => c.Id == a.Id);
            string text = (there ? f.Kept : f.KeptBy).Replace("{speaker}", a.GivenName, StringComparison.Ordinal)
                .Replace("{name}", s.VowFor, StringComparison.Ordinal).Replace("{site}", siteName, StringComparison.Ordinal);
            if (text != "") town.Log(LedgerCategory.Quest, there ? Importance.Major : Importance.Notable, text, [a.Id]);
            if (there) Notoriety.Deed(town, a, "vow", town.Content.Notoriety.FameDeeds.Vow);
            s.Vow = null;
            s.VowFor = "";
        }
    }
}
