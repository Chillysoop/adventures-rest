using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// An enemy's portrait: a game-icons face on an iron medallion tinted by its kind, its ring in the colour of how
/// dangerous it is, a crown over the big bad, and a question mark where the town only has rumours. Each leader
/// keeps the same face all campaign (picked from its kind by its name).
/// </summary>
public partial class Portrait : Control
{
    public string Glyph = "help";

    /// <summary>A whole icon to draw instead of a face glyph (a notable's trade).</summary>
    public string? Icon;
    public Color Tint = Ui.Ink2;
    public Color Ring = new("5a5347");
    public bool Crowned;

    /// <summary>Only rumoured: the face is dimmed behind a question mark.</summary>
    public bool Rumoured;

    public Portrait(float size)
    {
        CustomMinimumSize = new Vector2(size, size);
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        float s = Mathf.Min(Size.X, Size.Y);
        var c = new Vector2(Size.X / 2, s / 2 + (Crowned ? s * 0.06f : 0));
        float r = s / 2 * (Crowned ? 0.88f : 1);
        // Iron rim, the threat ring, then the field: the kind's colour, lit from above.
        DrawCircle(c, r, new Color("1d1712"));
        DrawCircle(c, r * 0.94f, Ring);
        DrawCircle(c, r * 0.8f, new Color("1d1712"));
        DrawCircle(c, r * 0.76f, Tint.Darkened(0.72f));
        DrawCircle(c + new Vector2(0, -r * 0.16f), r * 0.56f, new Color(Tint.Darkened(0.45f), 0.55f));
        var face = Ui.Icon(Icon ?? "face-" + Glyph);
        float g = r * 1.12f;
        var light = Tint.Lerp(Ui.Ink, 0.55f);
        DrawTextureRect(face, new Rect2(c - new Vector2(g / 2, g / 2), new Vector2(g, g)), false, Rumoured ? new Color(light, 0.55f) : light);
        if (Rumoured)
        {
            // Only tavern talk: a question mark on a seal at its shoulder.
            var at = c + new Vector2(r * 0.68f, r * 0.68f);
            float q = r * 0.36f;
            DrawCircle(at, q, new Color("1d1712"));
            DrawCircle(at, q * 0.82f, Ui.Panel2);
            DrawTextureRect(Ui.Icon("face-help"), new Rect2(at - new Vector2(q * 0.75f, q * 0.75f), new Vector2(q * 1.5f, q * 1.5f)), false, Ui.Gold);
        }
        if (Crowned)
        {
            float k = s * 0.42f;
            DrawTextureRect(Ui.Icon("face-crown"), new Rect2(new Vector2(Size.X / 2 - k / 2, -k * 0.18f), new Vector2(k, k * 0.62f)), false, Ui.Gold);
        }
    }
}

/// <summary>
/// A short bar of segments for a band on one of the assessment ladders (<see cref="Assess"/>): as many lit as the
/// band's step, in a colour that runs from good to bad, so the count reads even without the colour.
/// </summary>
public partial class BandBar : Control
{
    public int Lit;
    public int Of = 4;
    public Color Fill = Ui.Gold;

    public BandBar(int lit, int of, Color fill, float width = 54)
    {
        Lit = lit;
        Of = of;
        Fill = fill;
        CustomMinimumSize = new Vector2(width, 9);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        float gap = 2, w = (Size.X - gap * (Of - 1)) / Of;
        for (int i = 0; i < Of; i++)
        {
            var rect = new Rect2(i * (w + gap), 0, w, Size.Y);
            DrawStyleBox(Ui.Box(i < Lit ? Fill : new Color("120f0b"), i < Lit ? Fill.Darkened(0.35f) : Ui.Line, 2), rect);
        }
    }
}

/// <summary>Faces, colours and bars for enemies, odds and stakes.</summary>
public static class Foes
{
    /// <summary>Each kind's colour (as its glossary icon) and the faces its leaders wear.</summary>
    private static readonly Dictionary<string, (Color Tint, string[] Faces)> Kinds = new()
    {
        ["tribe"] = (new Color("9cb85a"), ["orc-head", "ogre", "troll", "goblin-head", "brutal-helm", "horned-helm", "giant"]),
        ["undead"] = (new Color("cfcab4"), ["shambling-zombie", "death-skull", "vampire-dracula", "floating-ghost", "crowned-skull", "dread-skull"]),
        ["schemers"] = (new Color("a68ad8"), ["hooded-assassin", "hooded-figure", "spy", "domino-mask", "iron-mask", "witch-face"]),
        ["beasts"] = (new Color("c4955f"), ["wolf-head", "bear-head", "direwolf", "spider-face", "boar", "harpy", "werewolf"]),
        ["dragon"] = (new Color("e8673f"), ["dragon-head", "spiked-dragon-head", "wyvern", "sea-dragon", "hydra"]),
        ["cult"] = (new Color("d0566a"), ["cultist", "warlock-hood", "tentacles-skull", "devil-mask", "all-seeing-eye", "imp"]),
        ["rivals"] = (new Color("d9b25a"), ["black-knight-helm", "visored-helm", "bandit", "overlord-helm", "crested-helmet"]),
        ["warband"] = (new Color("c8553d"), ["orc-head"]),
    };

    /// <summary>Words in what a site is believed to hold, and the face that shows them; the first match wins.</summary>
    private static readonly (string Word, string Face)[] Creatures =
    [
        ("hobgoblin", "goblin-head"), ("goblin", "goblin-head"), ("kobold", "goblin-head"), ("orc", "orc-head"), ("ogre", "ogre"),
        ("troll", "troll"), ("giant", "giant"), ("cyclops", "cyclops"), ("minotaur", "minotaur"),
        ("skeleton", "death-skull"), ("lich", "crowned-skull"), ("zombie", "shambling-zombie"), ("ghoul", "shambling-zombie"),
        ("ghast", "shambling-zombie"), ("mummy", "shambling-zombie"), ("vampire", "vampire-dracula"), ("wight", "dread-skull"),
        ("wraith", "floating-ghost"), ("ghost", "floating-ghost"), ("specter", "floating-ghost"), ("spectre", "floating-ghost"), ("banshee", "floating-ghost"),
        ("spider", "spider-face"), ("werewolf", "werewolf"), ("wolf", "wolf-head"), ("worg", "direwolf"), ("bear", "bear-head"), ("boar", "boar"),
        ("rat", "rat"), ("bat", "evil-bat"), ("snake", "snake"), ("serpent", "snake"), ("scorpion", "scorpion"), ("harpy", "harpy"),
        ("toad", "frog"), ("frog", "frog"), ("hag", "witch-face"), ("witch", "witch-face"),
        ("wyvern", "wyvern"), ("hydra", "hydra"), ("dragon", "dragon-head"), ("drake", "spiked-dragon-head"), ("wyrm", "dragon-head"),
        ("cult", "cultist"), ("fanatic", "cultist"), ("necromancer", "warlock-hood"), ("mage", "warlock-hood"), ("warlock", "warlock-hood"),
        ("demon", "devil-mask"), ("devil", "devil-mask"), ("fiend", "devil-mask"), ("imp", "imp"), ("eye", "all-seeing-eye"),
        ("bandit", "bandit"), ("brigand", "bandit"), ("thug", "bandit"), ("assassin", "hooded-assassin"), ("spy", "spy"), ("knight", "black-knight-helm"),
        ("gargoyle", "gargoyle"), ("golem", "golem-head"), ("construct", "golem-head"), ("kraken", "kraken-tentacle"), ("octopus", "kraken-tentacle"),
        ("ooze", "goo-skull"), ("slime", "goo-skull"), ("jelly", "goo-skull"), ("pudding", "goo-skull"), ("mimic", "mimic-chest"),
        ("owl", "barn-owl"), ("griffon", "eagle-head"), ("griffin", "eagle-head"), ("hippogriff", "eagle-head"), ("eagle", "eagle-head"), ("roc", "eagle-head"),
        ("raven", "raven"), ("crow", "raven"), ("vulture", "vulture"), ("stirge", "amber-mosquito"), ("crab", "crab"), ("shark", "shark-jaws"),
        ("grindylow", "kraken-tentacle"), ("merrow", "fish-monster"), ("sahuagin", "fish-monster"), ("glimmerkin", "fish-monster"), ("lizard", "lizardman"),
        ("troglodyte", "lizardman"), ("blight", "evil-tree"), ("treant", "evil-tree"), ("dryad", "fairy"), ("sprite", "fairy"), ("pixie", "fairy"), ("fey", "fairy"),
        ("duergar", "dwarf-face"), ("dwarf", "dwarf-face"), ("drow", "woman-elf-face"), ("elf", "woman-elf-face"), ("merfolk", "mermaid"), ("centaur", "centaur"),
        ("worm", "worm-mouth"), ("ankheg", "flying-beetle"), ("beetle", "flying-beetle"), ("wasp", "wasp-sting"), ("nightmare", "horse-head"), ("horse", "horse-head"),
        ("unicorn", "unicorn"), ("sphinx", "greek-sphinx"), ("medusa", "medusa-head"), ("gorgon", "medusa-head"), ("gnoll", "hyena-head"), ("hyena", "hyena-head"),
        ("lion", "lion"), ("tiger", "tiger-head"), ("panther", "saber-toothed-cat-head"), ("lynx", "saber-toothed-cat-head"),
        ("salamander", "fire"), ("elemental", "fire"), ("mephit", "imp"),
    ];

    /// <summary>A name's stable number (string hashes differ between runs in .NET).</summary>
    private static int Seed(string s)
    {
        int h = 17;
        foreach (char ch in s) h = unchecked(h * 31 + ch);
        return h & 0x7fffffff;
    }

    public static Color Tint(string kind) => Kinds.TryGetValue(kind, out var k) ? k.Tint : Ui.Ink2;

    /// <summary>The face a leader wears: one of its kind's, always the same for the same name.</summary>
    public static string Face(string kind, string name)
    {
        if (!Kinds.TryGetValue(kind, out var k)) return "help";
        return k.Faces[(int)(Geo.Hash(Seed(name)) * k.Faces.Length) % k.Faces.Length];
    }

    /// <summary>
    /// The face for what a site is believed to hold, else its kind's first. The creature is the last word that
    /// starts with one of the names ("giant owls" are owls), the longer name where two fit the same word.
    /// </summary>
    public static string CreatureFace(string believed, string kind)
    {
        string b = believed.ToLowerInvariant();
        string? best = null;
        int end = -1, len = 0;
        foreach (var (word, face) in Creatures)
        {
            // Only where a word starts with it: "rat" in "rats", not in "pirates".
            int i = b.Length;
            while ((i = i == 0 ? -1 : b.LastIndexOf(word, i - 1, StringComparison.Ordinal)) > 0 && char.IsLetter(b[i - 1])) { }
            if (i < 0) continue;
            int e = i + word.Length;
            // "rats" ends with the word plus a plural: count the letters of the word it sits in.
            while (e < b.Length && char.IsLetter(b[e])) e++;
            if (e > end || e == end && word.Length > len) (best, end, len) = (face, e, word.Length);
        }
        return best ?? (Kinds.TryGetValue(kind, out var k) ? k.Faces[0] : "help");
    }

    /// <summary>The colour of a step on a ladder, from good (0) to bad (1): green, olive, gold, orange, red.</summary>
    public static Color Scale(double bad)
    {
        Color[] stops = [new("6cbf7a"), new("a9c45a"), new("e8c33a"), new("e8913a"), new("e0604e")];
        double x = Math.Clamp(bad, 0, 1) * (stops.Length - 1);
        int i = Math.Min(stops.Length - 2, (int)x);
        return stops[i].Lerp(stops[i + 1], (float)(x - i));
    }

    /// <summary>A band's colour, where the top of the ladder is the worst (threat, danger).</summary>
    public static Color Worse(Banded b) => Scale(b.Steps <= 1 ? 0 : (double)b.Step / (b.Steps - 1));

    /// <summary>A band's colour, where the top of the ladder is the best (odds).</summary>
    public static Color Better(Banded b) => Scale(b.Steps <= 1 ? 0 : 1 - (double)b.Step / (b.Steps - 1));

    public static Portrait Of(Town t, Menace m, float size)
    {
        var threat = Assess.Threat(t, m);
        return new Portrait(size) { Glyph = Face(m.Kind, m.Leader), Tint = Tint(m.Kind), Ring = Worse(threat), Crowned = m.BigBad || m.Stage >= MenaceStage.Overlord };
    }

    public static Portrait Of(Town t, SiteState s, float size)
    {
        string kind = s.Known == SiteKnowledge.Unheard ? "" : s.Def.Kind;
        return new Portrait(size)
        {
            Glyph = CreatureFace(s.Believed, kind),
            Tint = Tint(kind),
            Ring = s.ThoughtCleared ? new Color("5a5347") : Worse(Assess.Danger(t, s)),
            Rumoured = s.Known == SiteKnowledge.Rumoured && !s.ThoughtCleared,
        };
    }

    public static Portrait Warband(Town t, float size) =>
        new(size) { Glyph = "orc-head", Tint = Tint("warband"), Ring = Assess.WarbandThreat(t) is Banded b ? Worse(b) : new Color("5a5347") };

    /// <summary>A labelled bar: "Threat ▮▮▮▯ Serious". A ladder's lowest band lights one segment.</summary>
    public static HBoxContainer Bar(string label, Banded b, Color fill, string? tip = null, float width = 54) => Bar(label, b.Step + 1, b.Steps, b.Name, fill, tip, width);

    /// <summary>A stakes bar: its lowest band ("none") lights nothing.</summary>
    public static HBoxContainer Stakes(string label, Banded b, Color fill, string? tip = null) => Bar(label, b.Step, b.Steps - 1, b.Name, b.Step == 0 ? Ui.Ink2 : fill, tip, 33);

    public static HBoxContainer Bar(string label, int lit, int of, string name, Color fill, string? tip = null, float width = 54)
    {
        var h = new HBoxContainer { MouseFilter = tip == null ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Pass, TooltipText = tip ?? "" };
        h.AddThemeConstantOverride("separation", 6);
        if (label != "") h.AddChild(Ui.Label(label, Ui.Ink2, Ui.Medium, 11));
        h.AddChild(new BandBar(lit, of, fill, width));
        h.AddChild(Ui.Label(name, fill, Ui.Bold, 11));
        foreach (var c in h.GetChildren()) ((Control)c).MouseFilter = Control.MouseFilterEnum.Ignore;
        return h;
    }

    /// <summary>A menace's threat bar.</summary>
    public static HBoxContainer Threat(Town t, Menace m) =>
        Bar("Threat", Assess.Threat(t, m), Worse(Assess.Threat(t, m)), "How its strength compares with the town's defence, as far as the town can judge. Deadly: it can take the walls.");
}
