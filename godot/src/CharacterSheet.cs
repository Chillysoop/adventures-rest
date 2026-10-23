using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Client;

public partial class Main
{
    /// <summary>What each kind of everyday check was about, for the sheet's latest rolls.</summary>
    private static readonly Dictionary<string, string> CheckKinds = new()
    {
        ["talk"] = "tavern talk",
        ["brawl"] = "a brawl",
        ["calm"] = "keeping the peace",
        ["tend"] = "seeing to bruises",
        ["haggle"] = "haggling over supplies",
        ["craft"] = "a day at the trade",
        ["perform"] = "a song at the tavern",
        ["pickpocket"] = "a hand in a purse",
        ["steal"] = "the town strongbox",
        ["intimidate"] = "facing a brawler down",
        ["bargain"] = "haggling over gear",
        ["tempted"] = "a dark whisper",
        ["betray"] = "a whisper at the gate",
        ["research"] = "a step in the scholars' work",
    };

    private int _rollsSeen = -1;

    /// <summary>Frames until --rolls replays the latest checks (once the crowd is on the map), or 0.</summary>
    private int _rollsReplay;

    /// <summary>
    /// Pop a little d20 over each townsperson or hero who rolled a check since the last frame. Idle tavern talk
    /// only shows on a natural 20 or 1, so the few dice on screen go to haggles, songs, brawls and thieves.
    /// </summary>
    private void SyncTownRolls()
    {
        var t = _sim.Town;
        if (_rollsSeen < 0 || _rollsSeen > t.CheckSerial) _rollsSeen = t.CheckSerial;
        if (_rollsReplay > 0 && --_rollsReplay == 0) _rollsSeen = Math.Max(0, t.CheckSerial - 24);
        if (t.CheckSerial == _rollsSeen) return;
        foreach (var c in t.Checks)
        {
            if (c.Serial <= _rollsSeen || c.AgentId is not int id) continue;
            if (c.Kind == "talk" && !c.Critical && !c.Fumble) continue;
            var result = c.Critical ? StoryResult.Critical : c.Fumble ? StoryResult.Fumble : c.Success ? StoryResult.Success : StoryResult.Failure;
            _map.Rolls.Show(id, Skills.Name(t, c.Skill), c.Die, c.Bonus, result, c.BuildingId is int b ? Front(t, b, id) : null);
        }
        _rollsSeen = t.CheckSerial;
    }

    /// <summary>A spot in the lane in front of a building, spread by who it is, for a roll whose roller isn't drawn there.</summary>
    private static Godot.Vector2 Front(Town t, int buildingId, int agentId)
    {
        var lot = t.Lot(t.Building(buildingId).LotId);
        var f = Geo.LotFront(lot);
        float half = lot.W / (float)Land.Res / 2;
        return Geo.LotPos(lot) + f * (lot.D / (float)Land.Res / 2 + 0.25f) + new Godot.Vector2(f.Y, -f.X) * ((Geo.Hash(agentId * 13 + 1) - 0.5f) * 2 * half);
    }

    /// <summary>The portrait glyph for a class (assets/icons/face-*.svg).</summary>
    private static string ClassGlyph(string cls) => cls switch
    {
        "fighter" => "crossed-swords",
        "barbarian" => "axe-swing",
        "paladin" => "templar-shield",
        "warden" => "oak",
        "rogue" => "cloak-dagger",
        "ranger" => "bowman",
        "cartographer" => "treasure-map",
        "monk" => "fist",
        "wizard" => "pointy-hat",
        "sorcerer" => "fireball",
        "warlock" => "warlock-eye",
        "reliquarian" => "relic-blade",
        "bard" => "lyre",
        "cleric" => "holy-symbol",
        "druid" => "oak-leaf",
        "hedge-witch" => "cauldron",
        _ => "visored-helm",
    };

    // Plainly: the icon already says what it is, so glossary words in the line get no second one.
    private static string Line(string icon, string text, Godot.Color? color = null) => $"{Ui.Img(icon, 14)} {Ui.Plainly(color is Godot.Color c ? Ui.C(c, text) : text)}";

    private static string Signed(int n) => n >= 0 ? $"+{n}" : $"−{-n}";

    /// <summary>A hero's character sheet: who they are, their numbers, what they fight with and carry, and their story.</summary>
    private SheetSpec HeroSheetSpec(Town t, Agent a)
    {
        var hs = a.Hero!;
        var cls = hs.Cls;
        var c = t.Content;
        string bg = hs.Background != "" && c.Library.Background(hs.Background) is { } b ? $" · {Ui.Esc(b.Name.ToLowerInvariant())}" : "";
        var badge = new List<string>();
        if (cls.Card != "") badge.Add($"{Ui.Img("role-" + cls.Card, 15)} {Tag(Ui.Cap(cls.Card), Ui.Gold)}");
        if (hs.PartyName != null) badge.Add(Ui.C(Ui.Ink2, $"of the {Ui.Esc(hs.PartyName)}"));
        if (hs.Knight) badge.Add(Tag("Knight", Ui.Gold));
        if (hs.Champion) badge.Add(Tag("Champion", Ui.Gold));
        double xp = -1;
        string xpText = "max level";
        if (Quests.XpBand(hs.Level) is var (from, to))
        {
            xp = (hs.Xp - from) / (double)Math.Max(1, to - from);
            xpText = $"{hs.Xp:#,0} / {to:#,0} XP";
        }

        int prof = Skills.HeroProficiency(hs.Level);
        var vitals = new List<SheetStat>
        {
            new("ui-shield", "ARMOUR", Gear.Ac(c, a).ToString(), "Armour class: what a foe must roll to hit"),
            new("ui-heart", "HIT POINTS", $"{hs.Hp}/{hs.MaxHp}", "", hs.MaxHp > 0 ? hs.Hp / (double)hs.MaxHp : 0),
            new("item-weapon", "ATTACK", Signed(Gear.Attack(c, a)), "Added to every attack roll"),
            new("d20", "PROFICIENCY", Signed(prof), "Added to trained skills; half to the rest"),
            new("ui-skull", "FOES SLAIN", hs.Kills.ToString(), $"{hs.Expeditions} expeditions"),
        };

        var fights = new List<string> { Line("item-weapon", $"{Worn(c, hs, "weapon", Ui.Esc(Ui.Cap(Piece(hs, hs.Weapon, "weapon") is var w && w != "" ? w : "bare hands")))} {Ui.Dim(Signed(Gear.Attack(c, a)))}") };
        if (cls.Sneak) fights.Add(Line("role-striker", "Sneak attack"));
        foreach (var (icon, label, tip) in Prowess.Sheet(c, hs)) fights.Add(Ui.Tip(Line(icon, Ui.Esc(label)), tip));
        if (cls.Role != "") fights.Add(Ui.Dim(Ui.Esc(Ui.Cap(cls.Role) + (cls.Magic != "" ? $", {cls.Magic} caster" : ""))));

        var kit = new List<string>();
        if (Piece(hs, hs.Armor, "armor") is var armor && armor != "") kit.Add(Line("item-armor", Worn(c, hs, "armor", Ui.Esc(Ui.Cap(armor)))));
        if (Piece(hs, hs.Shield, "shield") is var shield && shield != "") kit.Add(Line("item-shield", Worn(c, hs, "shield", Ui.Esc(Ui.Cap(shield)))));
        var charms = Gear.Charmed(hs).ToList();
        foreach (var charm in charms) kit.Add(Line(charm.Category == "ring" ? "item-ring" : "item-trinket", Powered(c, charm, Ui.C(Ui.Gold, Ui.Esc(charm.Name)))));
        if (charms.Count > 0) kit.Add(Ui.Tip(Ui.Dim($"Attuned {charms.Count}/{c.Enchant.Attune}"), "Magic charms a hero can wear at once"));
        if (hs.Potions > 0) kit.Add(Line("potions", $"Healing potion ×{hs.Potions}"));
        if (hs.Scrolls > 0) kit.Add(Line("sheet-spell", $"Spell scroll ×{hs.Scrolls}"));
        if (hs.Copied > 0) kit.Add(Line("books", $"{hs.Copied} copied into the spellbook"));
        if (hs.Stone) kit.Add(Line("item-stone", "Town sending stone"));
        if (hs.Mount != "" && c.Passage.Mount(hs.Mount) is { } md) kit.Add(Line("sheet-mount", Ui.Esc(Ui.Cap(md.Name))));
        var (alone, carries) = Passage.Of(t, a);
        if (Passage.Describe(alone) is var crosses && crosses != "") kit.Add(Line("ui-road", Ui.Esc($"Crosses {crosses}")));
        if (Passage.Describe(carries & ~alone) is var more && more != "") kit.Add(Line("ui-road", Ui.Esc($"Takes the party over {more}")));

        var features = new List<string>();
        if (Patrons.Of(c, hs) is { } pd)
        {
            var fc = hs.Favour >= 3 ? Ui.Good : hs.Favour <= -3 ? Ui.Bad : Ui.Ink2;
            var boons = new[] { "attack", "ac", "skill" }.Select(k => (k, Patrons.Boon(c, hs, k))).Where(x => x.Item2 != 0).Select(x => $"{(x.k == "ac" ? "AC" : x.k)} {Signed(x.Item2)}");
            features.Add(Ui.Tip(Line("sheet-patron", $"{Ui.Esc(Ui.Cap(pd.Name))} {Ui.C(fc, $"favour {Patrons.Favour(hs.Favour)}")} {Ui.Dim(string.Join(", ", boons))}"), $"{Ui.Cap(pd.Title)}. It wants {pd.Wants}."));
        }
        if (hs.Familiar != "") features.Add(Line("sheet-familiar", Ui.Esc(Ui.Cap(hs.Familiar))));
        if (Curses.Of(c, hs) is { } curse)
        {
            var mods = new[] { ("attack", "attack"), ("ac", "AC"), ("damage", "damage"), ("skill", "skill") }.Select(x => (x.Item2, Curses.Mod(c, hs, x.Item1))).Where(x => x.Item2 != 0).Select(x => $"{x.Item1} {Signed(x.Item2)}");
            features.Add(Ui.Tip(Line("sheet-curse", $"Cursed: {Ui.Esc(Ui.Cap(curse.Name))} {string.Join(", ", mods)}", Ui.Bad), $"{curse.Text} Stuck until lifted."));
        }
        foreach (var cond in Conditions.Of(t, a)) features.Add(Ui.Tip(Line(cond.Icon, Ui.Esc(cond.Name), cond.Edge > 0 ? Ui.Good : Ui.Bad), cond.Text));
        if (hs.Taint > 0) features.Add(Line("ui-skull", hs.Taint == 1 ? "A gift from the dark" : $"{hs.Taint} gifts from the dark", Ui.Bad));
        if (hs.Holding != "") features.Add(Line("ui-house", $"Owns {Ui.Esc(hs.Holding)}"));
        if (hs.StakeId is int sid) features.Add(Line("ui-trade", $"{Js.Str(hs.Stake)}g stake in {BuildingLink(t.Building(sid))}"));

        var story = new List<string>();
        if (Annals.Past(t, a) is string past && past != "") story.Add(Ui.Dim(Ui.Esc(past)));
        story.Add(Line("ui-person", Ui.Esc(Ui.Cap(a.TraitList()))));
        if (Taverns.Persona(t, a) is { } persona) story.Add(Line("ale", $"In their cups: {Ui.Esc(persona.Name)}{(Taverns.Drunk(t, a) != null ? Ui.Dim(", tonight") : "")}"));
        if (Taverns.Owing(t, a) is { } blood) story.Add(Line("gold", $"Owes blood money for {Ui.Esc(blood.Victim)}", Ui.Bad));

        return new SheetSpec
        {
            Name = Loyalty.Styled(t, a),
            Subtitle = $"Level {hs.Level} {Ui.Esc(a.Race.ToLowerInvariant())} {Ui.Esc(cls.Name.ToLowerInvariant())}{bg}",
            Badge = string.Join("  ", badge),
            Glyph = ClassGlyph(cls.Id),
            Tint = Palette.ClassColor(cls.Id),
            Ring = Notoriety.FameTier(t, hs) >= 2 ? Ui.Gold : Notoriety.InfamyTier(t, hs) >= 2 ? Ui.Bad : new Godot.Color("5a5347"),
            Crowned = hs.Knight || hs.Champion,
            Xp = xp,
            XpText = xpText,
            Vitals = vitals,
            Abilities = a.Abilities,
            Skills = Skills.All(t).Select(sk => new SheetSkill(sk.Name, Skills.Bonus(t, a, sk.Id), Skills.Trained(t, a, sk.Id), Skills.Expert(t, a, sk.Id))).ToList(),
            SkillsNote = $"● trained {Signed(prof)} · ◆ expert",
            Right = [new("Attacks & spells", "item-weapon", fights), new("Equipment", "sheet-pack", kit), new("Features", "ui-star", features)],
            Wide = [new("Story", "lore", story)],
        };
    }

    /// <summary>A worn piece's name with its powers, if the slot holds magic.</summary>
    private static string Worn(Content c, HeroSheet s, string slot, string name) =>
        s.Magic.TryGetValue(slot, out var it) ? Powered(c, it, name) : name;

    /// <summary>A worn piece by name: its magic version when it has one ("+1 longsword"), else the plain gear.</summary>
    private static string Piece(HeroSheet s, LibGear? g, string slot) =>
        s.Magic.TryGetValue(slot, out var it) ? Gear.Label(s, it) : g == null ? "" : Library.Prose(g.Name);

    /// <summary>
    /// Anyone's character sheet: ability scores, proficiency, all eighteen skills with their bonus (trained ones
    /// marked, expert ones doubly), and the last few d20s they rolled around town.
    /// </summary>
    private void InspectSheet(Pane p, Town t, Agent a)
    {
        InspectConditions(p, t, a);
        if (a.Hero != null)
        {
            InspectRolls(p, t, a);
            return;
        }
        p.Heading("Character sheet");
        p.Add($"ab{a.Abilities}", () => AbilityRow(a.Abilities));
        var r = t.Content.Skills.Townsfolk;
        int practised = Skills.Practised(t, a);
        string grade = practised >= r.PracticeMax ? "masterful" : practised > 0 ? "practised" : "learning";
        int toNext = practised >= r.PracticeMax ? 0 : r.PracticeStep - a.Practice % Math.Max(1, r.PracticeStep);
        p.Text($"{Tag($"Trade +{r.Proficiency + practised}", Ui.Ink)}  {Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Ui.Cap(a.Profession))}, {grade}{(toNext > 0 ? $" ({toNext} good turns at the trade to the next step)" : "")}. Untrained at everything else.[/font_size]")}", 13);
        var cells = new System.Text.StringBuilder("[table=2]");
        foreach (var s in Skills.All(t))
        {
            bool trained = Skills.Trained(t, a, s.Id), expert = Skills.Expert(t, a, s.Id);
            int bonus = Skills.Bonus(t, a, s.Id);
            string mark = expert ? "◆" : trained ? "●" : "○";
            string line = $"{mark} {Ui.Esc(s.Name)} {(trained ? "[b]" : "")}{Skills.Sign(bonus)}{(trained ? "[/b]" : "")}";
            cells.Append("[cell]").Append(Ui.C(trained ? Ui.Ink : Ui.Ink2, $"[font_size=12]{line}[/font_size]")).Append("[/cell]");
        }
        cells.Append("[/table]");
        p.Text(cells.ToString(), 12);
        InspectRolls(p, t, a);
    }

    /// <summary>The conditions they are under (conditions.json), each an icon with what it does on hover.</summary>
    private static void InspectConditions(Pane p, Town t, Agent a)
    {
        var list = Conditions.Of(t, a);
        if (list.Count == 0) return;
        p.Chips([.. list.Select(d => Ui.Chip(d.Icon, d.Name, d.Edge > 0 ? Ui.Good : Ui.Bad, d.Text))]);
    }

    /// <summary>The last few d20s they rolled around town.</summary>
    private static void InspectRolls(Pane p, Town t, Agent a)
    {
        var rolls = Skills.Recent(t, a.Id).Take(5).ToList();
        if (rolls.Count == 0) return;
        p.Heading("Latest rolls");
        foreach (var c in rolls)
        {
            string die = (c.Critical ? Ui.C(Ui.Gold, "[b]20[/b]") : c.Fumble ? Ui.C(Ui.Bad, "[b]1[/b]") : c.Die.ToString())
                + (c.Other is int o ? Ui.Tip(Ui.C(Ui.Ink2, $" ({o})"), $"{(o <= c.Die ? "Advantage" : "Disadvantage")}: {c.Edge}") : "");
            string result = c.Success ? Ui.C(Ui.Good, "success") : Ui.C(Ui.Bad, "failure");
            string what = CheckKinds.GetValueOrDefault(c.Kind, c.Kind);
            p.Text($"{Ui.C(Ui.Ink2, $"[font_size=11]Day {c.Tick / 24 + 1}, {c.Tick % 24:00}:00[/font_size]")}  {Ui.Esc(Skills.Name(t, c.Skill))}: d20 {die} {Skills.Sign(c.Bonus)} = {c.Total} vs {c.Dc}, {result} {Ui.C(Ui.Ink2, $"[font_size=11]· {Ui.Esc(what)}{(c.Why != "" ? $": {Ui.Esc(c.Why)}" : "")}[/font_size]")}", 12);
        }
    }
}
