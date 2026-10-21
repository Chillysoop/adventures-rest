namespace CrownAndCompass.Sim.Systems;

/// <summary>A hero's moment for a short card: a level that brings something new, or a magic item they keep.</summary>
public sealed record HeroMoment(int Tick, int HeroId, string Kind, string Title, string Text, IReadOnlyList<string> Gains);

/// <summary>
/// The moments a table cheers between sessions: a hero reaching a level that brings a new spell or a new
/// feature, and a hero claiming a magic item with the story of where it came from. They are kept for the
/// client's cards (the newest few) and change nothing else; the words come from the library.
/// </summary>
public static class Moments
{
    public const string Level = "level";
    public const string Item = "item";
    public const string Mount = "mount";

    /// <summary>How many recent moments the town keeps for the cards.</summary>
    private const int Kept = 30;

    private static void Add(Town town, HeroMoment m)
    {
        town.Moments.Add(m);
        if (town.Moments.Count > Kept) town.Moments.RemoveRange(0, town.Moments.Count - Kept);
    }

    /// <summary>The highest spell level a caster of this kind can cast at a character level; 0 for none.</summary>
    public static int SpellLevel(string magic, int level) => magic switch
    {
        "full" => Math.Min(9, (level + 1) / 2),
        "pact" => Math.Min(5, (level + 1) / 2),
        "half" => level < 2 ? 0 : Math.Min(5, (level - 1) / 4 + 1),
        _ => 0,
    };

    /// <summary>
    /// A hero has gone up a level: if it brings something they can name (a feature of their class, a new
    /// circle of spells), it is a moment worth a card.
    /// </summary>
    public static void Leveled(Town town, Agent a, int level, int hpBefore)
    {
        var s = a.Hero!;
        var lib = town.Content.Library.Class(s.Cls.Id);
        var gains = new List<string>();
        foreach (var f in lib?.Features.Where(f => f.Level == level) ?? [])
        {
            if (f.Name.Contains("Subclass", StringComparison.OrdinalIgnoreCase)) gains.Add("Chooses the path their calling will take");
            else if (f.Name.Contains("Ability Score Improvement", StringComparison.OrdinalIgnoreCase)) gains.Add("A feat, or a sharper edge");
            else if (!f.Name.StartsWith("Core ", StringComparison.Ordinal) && f.Name != "Epic Boon") gains.Add(f.Name);
        }
        string magic = s.Cls.Magic != "" ? s.Cls.Magic : lib?.Magic ?? "";
        int circle = SpellLevel(magic, level);
        if (circle > SpellLevel(magic, level - 1))
        {
            var spells = town.Content.Library.Spells.Where(x => x.Level == circle && x.Classes.Contains(s.Cls.Id)).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
            string learns = spells.Count > 0 ? $": learns {Library.Prose(spells[(int)(Voices.Hash("spell", a.Id, level) % (uint)spells.Count)].Name)}" : "";
            gains.Insert(0, $"Spells of the {Ordinal(circle)} circle{learns}");
        }
        if (Expedition.Proficiency(level) > Expedition.Proficiency(level - 1)) gains.Add($"Proficiency +{Expedition.Proficiency(level)}");
        if (gains.Count == 0) return;
        gains.Add($"Hit points {hpBefore} → {s.MaxHp}");
        string fame = level switch
        {
            5 => "Bards in other towns have started singing about them.",
            11 => "Kings send letters to them by name.",
            17 => "The whole world knows the name.",
            _ => "",
        };
        Add(town, new HeroMoment(town.Tick, a.Id, Level, $"{a.Name} reaches level {level}", fame, gains));
    }

    /// <summary>A hero takes to the sky on a beast of their own: a griffon bought, an owl, an eagle that chose them.</summary>
    public static void Mounted(Town town, Agent a, MountDef m, string line) =>
        Add(town, new HeroMoment(town.Tick, a.Id, Mount, $"{a.Name} rides a {m.Name}", line, [$"Flies over {Passage.Describe(Passage.Parse(m.Ways))}"]));

    /// <summary>A hero keeps a magic item from a hoard: it carries the story of where it was found.</summary>
    public static void Claimed(Town town, Agent a, LibItem item, string party, SiteState site, IReadOnlyList<Agent> with)
    {
        var s = a.Hero!;
        if (!s.Magic.Values.Contains(item)) return;
        string boss = "";
        if (!string.IsNullOrEmpty(site.Def.Boss.Monster))
        {
            var def = town.Content.Monster(site.Def.Boss.Monster);
            boss = def.Named ? def.Name : $"the {def.Name}";
        }
        string others = Expedition.ListNames(with.Where(o => o.Id != a.Id).Select(o => o.GivenName).ToList());
        string tale = $"Taken from {site.Def.Name} on day {town.Day}" + (boss != "" && site.Cleared ? $", over the body of {boss}" : "") + (others != "" ? $", with {others} at their side." : ".");
        s.ItemTales[item.Id] = tale;
        string what = item.Rarity != "" ? $"{Ui(item.Rarity)} {item.Category}".Trim() : item.Category;
        Add(town, new HeroMoment(town.Tick, a.Id, Item, $"{a.GivenName} claims the {item.Name}", tale, what != "" ? [Cap(what)] : []));
    }

    private static string Ui(string rarity) => rarity.Replace('-', ' ');

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string Ordinal(int n) => n switch { 1 => "first", 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth", 6 => "sixth", 7 => "seventh", 8 => "eighth", _ => "ninth" };
}
