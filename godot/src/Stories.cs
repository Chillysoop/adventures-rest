using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// Story events, Paradox style: a card pops up when something in town needs the mayor, and its choices are
/// d20 checks of the mayor's own sheet (<see cref="Stories"/>). The card shows each choice's skill, DC and
/// odds; picking one rolls the die on screen, then tells what came of it. "Later" leaves the story on the
/// alert strip until it runs its course. The Mayor window is the character sheet the rolls come from.
/// </summary>
public partial class Main
{
    private const double RollSeconds = 0.9;

    private PanelContainer _story = null!;
    private VBoxContainer _storyBox = null!;
    private int? _storyId;

    /// <summary>The newest story the player has been shown, so each new one pops up once.</summary>
    private int _storySeen;
    private bool _inspire;

    /// <summary>The roll on screen: the beat it shows and when the die was thrown.</summary>
    private StoryBeat? _beat;
    private double _beatAt;
    private Label? _dieFace, _dieOther;
    private Control? _beatResult;

    private void BuildStory()
    {
        _story = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(600, 0), ZIndex = 20, MouseFilter = MouseFilterEnum.Stop };
        var box = Glass(14, 24, 20);
        box.BorderColor = Ui.Gold;
        box.BorderWidthTop = 3;
        _story.AddThemeStyleboxOverride("panel", box);
        _storyBox = new VBoxContainer();
        _storyBox.AddThemeConstantOverride("separation", 10);
        _story.AddChild(_storyBox);
        AddChild(_story);
        _story.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.Minsize);
        _story.GrowHorizontal = GrowDirection.Both;
        _story.GrowVertical = GrowDirection.Both;
    }

    /// <summary>Show a story's card at its current stage.</summary>
    private void OpenStory(int id)
    {
        if (Stories.Find(_sim.Town, id) is not { } s) return;
        _storyId = id;
        _beat = null;
        _inspire = false;
        _storySeen = Math.Max(_storySeen, id);
        RenderStory(s);
        _story.Visible = true;
        MoveChild(_story, GetChildCount() - 1);
    }

    private void CloseStory()
    {
        _story.Visible = false;
        _storyId = null;
        _beat = null;
        _dirty = true;
    }

    /// <summary>A new story pops up on its own (while the player is playing, not watching the stand-in); high stakes pause the clock.</summary>
    private void SyncStories(double delta)
    {
        if (_story.Visible && _beat != null) AnimateRoll();
        if (_title.Visible || _menu.Visible || _story.Visible) return;
        var next = Stories.Pending(_sim.Town).FirstOrDefault(s => s.Id > _storySeen);
        if (next == null) return;
        _storySeen = next.Id;
        if (_opts.Smoke) return;
        OpenStory(next.Id);
        if (Stories.DefOf(_sim.Town, next).Stakes == "high" && _speed > 0 && !_opts.NoPause) SetSpeed(0);
    }

    private static (string Word, Color Color) StakesLook(string stakes) => stakes switch
    {
        "high" => ("HIGH STAKES", Ui.Bad),
        "medium" => ("RISING STAKES", Ui.Gold),
        _ => ("LOW STAKES", Ui.Ink2),
    };

    private void RenderStory(Story s)
    {
        foreach (var c in _storyBox.GetChildren()) c.Free();
        _dieFace = _dieOther = null;
        _beatResult = null;
        var t = _sim.Town;
        var d = Stories.DefOf(t, s);
        var (word, color) = StakesLook(d.Stakes);
        ((StyleBoxFlat)_story.GetThemeStylebox("panel")).BorderColor = color;

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        head.AddChild(new TextureRect { Texture = Ui.Icon("d20"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(30, 30), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        head.AddChild(Ui.Label(word, color, Ui.Bold, 11));
        head.AddChild(Ui.Label("·  " + d.Source.ToUpperInvariant(), Ui.Ink2, Ui.Bold, 11));
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        head.AddChild(spacer);
        if (!s.Done)
        {
            int days = Math.Max(1, (int)Math.Ceiling((s.DueTick - t.Tick) / 24.0));
            head.AddChild(Ui.Label($"Runs its course in {Ui.Days(days)}", Ui.Ink2, Ui.Medium, 11));
        }
        _storyBox.AddChild(head);

        var title = Ui.Label(Stories.Fill(t, s, d.Title), Ui.Ink, Ui.Display, 24);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _storyBox.AddChild(title);

        if (_beat != null)
        {
            RenderBeat(s, _beat);
            return;
        }
        var stage = Stories.StageOf(t, s);
        if (s.Beats.Count > 0) _storyBox.AddChild(Ui.Rich(Ui.C(Ui.Ink2, $"[i]{Ui.Esc(s.Beats[^1].Text)}[/i]"), 13));
        _storyBox.AddChild(Ui.Rich(LinkStory(s, Stories.Fill(t, s, stage.Text)), 15));

        var m = t.Stories.Mayor;
        for (int i = 0; i < stage.Options.Count; i++) _storyBox.AddChild(OptionRow(s, stage.Options[i], i));

        var foot = new HBoxContainer();
        foot.AddThemeConstantOverride("separation", 10);
        if (m.Inspiration > 0)
        {
            var inspire = new CheckBox { Text = $"Reroll with Inspiration if it fails ({m.Inspiration} left)", ButtonPressed = _inspire, FocusMode = FocusModeEnum.None, TooltipText = "If the d20 misses, spend one Inspiration to throw it again. It is only spent on a miss. Running the town well and natural 20s earn it." };
            inspire.Toggled += on =>
            {
                _inspire = on;
                RenderStory(s);
            };
            foot.AddChild(inspire);
        }
        var who = Ui.Rich(Ui.C(Ui.Ink2, $"[font_size=12]Rolling as [url=mayor]{Ui.C(Ui.Accent, Ui.Esc(m.Name))}[/url], level {m.Level} {Ui.Esc(Stories.Title(t).ToLowerInvariant())}[/font_size]"), 12);
        who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        who.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        foot.AddChild(who);
        var later = new Button { Text = "Later", TooltipText = "Leave it on the alert strip for now", FocusMode = FocusModeEnum.None };
        later.Pressed += CloseStory;
        foot.AddChild(later);
        _storyBox.AddChild(foot);
    }

    /// <summary>The hero a story is about, as a link.</summary>
    private string LinkStory(Story s, string text)
    {
        string esc = Ui.Esc(text);
        if (s.HeroId is int h)
        {
            var a = _sim.Town.Agent(h);
            int i = esc.IndexOf(a.Name, StringComparison.Ordinal);
            if (i >= 0) esc = $"{esc[..i]}{AgentLink(a)}{esc[(i + a.Name.Length)..]}";
        }
        return esc;
    }

    private Control OptionRow(Story s, StoryOption o, int index)
    {
        var t = _sim.Town;
        string? blocked = Stories.Blocked(t, s, o);
        string sub;
        if (!o.Rolls) sub = o.Cost > 0 ? $"A sure thing · costs {Js.Str(o.Cost)}g" : "A sure thing";
        else
        {
            var (mod, prof, ab) = Stories.Bonus(t, o.Skill, o.Ability);
            string name = o.Skill != "" ? t.Content.Stories.Skill(o.Skill)?.Name ?? o.Skill : ab.ToUpperInvariant();
            int bonus = mod + prof;
            string adv = Stories.Advantage(t, s, o);
            sub = $"{name} {(bonus >= 0 ? "+" : "−")}{Math.Abs(bonus)} ({ab.ToUpperInvariant()}{Edge(o.Skill)}) vs DC {o.Dc} · {Math.Round(Stories.Chance(t, s, o, _inspire) * 100)}%"
                + (adv != "" ? $" · advantage: {adv}" : "") + (o.Cost > 0 ? $" · costs {Js.Str(o.Cost)}g" : "");
        }
        if (blocked != null) sub = $"{Ui.Cap(blocked)}.";
        // The heroes in town who would like or resent the choice, by their tempers.
        var (pleased, angered) = Regard.Reactions(t, o.Tags);
        string Who(List<Agent> heroes) => string.Join(", ", heroes.Take(3).Select(h => h.GivenName)) + (heroes.Count > 3 ? $" +{heroes.Count - 3}" : "");
        var feel = new List<string>();
        if (pleased.Count > 0) feel.Add(Ui.C(Ui.Good, $"Pleases {Ui.Esc(Who(pleased))}"));
        if (angered.Count > 0) feel.Add(Ui.C(Ui.Bad, $"Angers {Ui.Esc(Who(angered))}"));
        var row = new Button { Disabled = blocked != null, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, feel.Count > 0 ? 72 : 54), MouseDefaultCursorShape = CursorShape.PointingHand };
        var normal = Ui.Box(new Color(Ui.Panel2, 0.9f), Ui.Line, 8, 1, 14, 8);
        var hover = Ui.Box(new Color(Ui.Gold, 0.14f), Ui.Gold, 8, 1, 14, 8);
        hover.BorderWidthLeft = 4;
        row.AddThemeStyleboxOverride("normal", normal);
        row.AddThemeStyleboxOverride("hover", hover);
        row.AddThemeStyleboxOverride("pressed", hover);
        row.AddThemeStyleboxOverride("disabled", Ui.Box(new Color(Ui.Panel, 0.6f), Ui.Line, 8, 1, 14, 8));
        var v = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        v.AddThemeConstantOverride("separation", 2);
        v.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        v.OffsetLeft = 14;
        v.OffsetTop = 8;
        var label = Ui.Label($"{index + 1}.  {Stories.Fill(t, s, o.Label)}", blocked != null ? Ui.Ink2 : Ui.Ink, Ui.Medium, 15);
        label.MouseFilter = MouseFilterEnum.Ignore;
        v.AddChild(label);
        var subl = Ui.Rich(Ui.Coined(Ui.C(Ui.Ink2, Ui.Esc(sub))), 12);
        subl.MouseFilter = MouseFilterEnum.Ignore;
        v.AddChild(subl);
        if (feel.Count > 0)
        {
            var feell = Ui.Rich(string.Join(Ui.C(Ui.Ink2, "  ·  "), feel), 12);
            feell.MouseFilter = MouseFilterEnum.Ignore;
            v.AddChild(feell);
        }
        row.AddChild(v);
        int id = s.Id;
        row.Pressed += () => Choose(id, index);
        return row;
    }

    /// <summary>What the mayor brings to a skill beyond the ability: training, expertise, a feat.</summary>
    private string Edge(string skill)
    {
        var t = _sim.Town;
        if (skill == "") return "";
        var bits = new List<string>();
        if (Stories.Expert(t, skill)) bits.Add("expert");
        else if (Stories.Proficient(t, skill)) bits.Add("proficient");
        foreach (var f in Sheet.Feats(t))
            if (f.Skills.GetValueOrDefault(skill) is int n and > 0) bits.Add($"{f.Name} +{n}");
        return bits.Count == 0 ? "" : ", " + string.Join(", ", bits);
    }

    /// <summary>Take an option: the sim rolls, and the card throws the die.</summary>
    private void Choose(int storyId, int option)
    {
        var beat = _sim.ResolveStory(storyId, option, _inspire);
        if (beat == null) return;
        GuideEvent("roll");
        _beat = beat;
        _beatAt = _clockReal;
        _dirty = true;
        if (Stories.Find(_sim.Town, storyId) is { } s) RenderStory(s);
    }

    private void RenderBeat(Story s, StoryBeat b)
    {
        var t = _sim.Town;
        bool rolled = b.Result is not (StoryResult.Certain or StoryResult.Ignored);
        if (rolled)
        {
            var dice = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            dice.AddThemeConstantOverride("separation", 18);
            dice.AddChild(Die(out _dieFace, 112));
            if (b.Other is int)
            {
                var other = Die(out _dieOther, 70);
                other.Modulate = new Color(1, 1, 1, 0.45f);
                dice.AddChild(other);
            }
            _storyBox.AddChild(dice);
        }
        var result = new VBoxContainer { Visible = !rolled };
        result.AddThemeConstantOverride("separation", 8);
        if (rolled)
        {
            var (word, color) = b.Result switch
            {
                StoryResult.Critical => ("NATURAL 20", Ui.Gold),
                StoryResult.Success => ("SUCCESS", Ui.Good),
                StoryResult.Partial => ("SUCCESS, AT A COST", Ui.Gold),
                StoryResult.Fumble => ("NATURAL 1", Ui.Bad),
                _ => ("FAILURE", Ui.Bad),
            };
            var w = Ui.Label(word, color, Ui.Display, 26);
            w.HorizontalAlignment = HorizontalAlignment.Center;
            result.AddChild(w);
            string skill = t.Content.Stories.Skill(b.Skill)?.Name ?? b.Skill.ToUpperInvariant();
            string ab = (t.Content.Stories.Skill(b.Skill)?.Ability ?? b.Skill).ToUpperInvariant();
            string math = $"{b.Die}{Signed(b.Modifier, ab)}{(b.Proficiency > 0 ? Signed(b.Proficiency, "training") : "")} = [b]{b.Total}[/b] against DC {b.Dc}";
            string adv = (b.Advantage != "" ? $"  ·  advantage: {Ui.Esc(b.Advantage)}" : "") + (b.Rerolled is int first ? $"  ·  rerolled a {first} with Inspiration" : "");
            result.AddChild(Ui.Rich($"[center]{Ui.C(Ui.Ink2, $"{Ui.Esc(skill)}: {math}{adv}")}[/center]", 13));
        }
        result.AddChild(Ui.Rich(LinkStory(s, b.Text), 15));
        var foot = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        var go = new Button { Text = s.Done ? "Close" : "Continue", ThemeTypeVariation = "Post", FocusMode = FocusModeEnum.None };
        int id = s.Id;
        go.Pressed += () =>
        {
            if (Stories.Find(_sim.Town, id) is { Done: false }) OpenStory(id);
            else CloseStory();
        };
        foot.AddChild(go);
        result.AddChild(foot);
        _storyBox.AddChild(result);
        _beatResult = result;
    }

    private static string Signed(int n, string what) => $" {(n >= 0 ? "+" : "−")} {Math.Abs(n)} ({what})";

    private static Control Die(out Label face, int size)
    {
        var holder = new Control { CustomMinimumSize = new Vector2(size, size) };
        var tex = new TextureRect { Texture = Ui.Icon("d20"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        holder.AddChild(tex);
        tex.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        face = Ui.Label("", Colors.White, Ui.Display, size * 3 / 8);
        face.HorizontalAlignment = HorizontalAlignment.Center;
        face.VerticalAlignment = VerticalAlignment.Center;
        face.AddThemeColorOverride("font_outline_color", new Color(0.11f, 0.09f, 0.07f));
        face.AddThemeConstantOverride("outline_size", Math.Max(4, size / 14));
        holder.AddChild(face);
        face.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        face.OffsetTop = size / 12f;
        return holder;
    }

    /// <summary>The die tumbles through faces, then lands on the roll and the result shows.</summary>
    private void AnimateRoll()
    {
        if (_beat == null || _dieFace == null || !IsInstanceValid(_dieFace)) return;
        double age = _clockReal - _beatAt;
        // Screenshots and the smoke test land the die at once.
        bool landed = age >= RollSeconds || _opts.Screenshot != null || _opts.Smoke;
        if (!landed)
        {
            int face = (int)(GD.Randi() % 20) + 1;
            _dieFace.Text = face.ToString();
            if (_dieOther != null) _dieOther.Text = ((int)(GD.Randi() % 20) + 1).ToString();
            _dieFace.GetParent<Control>().Rotation = (float)(Math.Sin(age * 30) * 0.25 * (1 - age / RollSeconds));
            return;
        }
        if (_beatResult is { Visible: false })
        {
            _dieFace.Text = _beat.Die.ToString();
            _dieFace.GetParent<Control>().Rotation = 0;
            if (_dieOther != null) _dieOther.Text = (_beat.Other ?? 0).ToString();
            _beatResult.Visible = true;
            _dieFace.AddThemeColorOverride("font_color", _beat.Result switch
            {
                StoryResult.Critical => Ui.Gold,
                StoryResult.Fumble => new Color("ffb0a4"),
                _ => Colors.White,
            });
        }
    }

    // ---------- The mayor's sheet ----------

    private void RenderMayor()
    {
        var p = _panes[Tab.Mayor];
        var t = _sim.Town;
        var r = t.Content.Stories;
        var m = t.Stories.Mayor;
        var bg = r.Background(m.Background);
        p.Begin();
        int? next = Stories.NextLevelXp(t);
        int from = r.Levels.Count >= m.Level ? r.Levels[m.Level - 1] : 0;
        p.Card($"[font_size=19][b]{Ui.Esc(m.Name)}[/b][/font_size]\n{Ui.C(Ui.Ink2, $"Level {m.Level} {Ui.Esc(Stories.Title(t).ToLowerInvariant())} of {Ui.Esc(t.Name)} · {Ui.Esc(bg.Name.ToLowerInvariant())}")}", Ui.Gold);
        if (next is int n) p.Meter("Experience", 100.0 * (m.Xp - from) / Math.Max(1, n - from), $"{m.Xp}/{n}");
        else p.Meter("Experience", 100, $"{m.Xp}");
        p.Note("You gain experience by seeing stories through. Each level lets you choose how you grow, and raises your proficiency bonus every four levels.");
        if (Sheet.Pending(t) > 0) LevelUp(p);

        p.Heading("Abilities");
        var a = Stories.Scores(t);
        string sig = $"ab{a}";
        p.Add(sig, () => AbilityRow(a));
        int prof = Stories.ProficiencyBonus(m.Level);
        int max = Sheet.InspirationMax(t);
        string pips = new string('●', Math.Min(m.Inspiration, max)) + new string('○', Math.Max(0, max - m.Inspiration));
        p.Text($"{Tag($"Proficiency +{prof}", Ui.Ink)}   {Tag($"Inspiration {pips}", m.Inspiration > 0 ? Ui.Gold : Ui.Ink2)}   {Ui.C(Ui.Ink2, $"[font_size=12]{m.Successes} won, {m.Failures} lost[/font_size]")}", 13);

        p.Heading("Inspiration");
        p.Note($"Spend one to reroll a failed d20 in a story. You hold up to {max}. Natural 20s earn it, and so does running the town well:");
        foreach (var d in r.Deeds)
        {
            var (have, need) = Sheet.Progress(t, d);
            string unit = d.Id == "content" ? $"days at {Js.Str(d.Threshold)}+ Mandate" : "";
            p.Text($"{Ui.C(Ui.Gold, "✦")} {Ui.Esc(d.Name)}  {Ui.C(Ui.Ink2, $"[font_size=12]{(need > 1 ? $"{Math.Max(0, have)} of {need}{(unit != "" ? " " + unit : "")}" : "each one")}[/font_size]")}", 13);
        }
        var feats = Sheet.Feats(t);
        if (feats.Count > 0)
        {
            p.Heading("Feats");
            foreach (var f in feats) p.Text($"[b]{Ui.Esc(f.Name)}[/b]  {Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(f.Text)}[/font_size]")}", 13);
        }

        p.Heading("Background");
        if (Stories.CanChooseBackground(t))
        {
            p.Note("Who were you before the charter? Your background sets your abilities, your two trained skills, and the kind of story you have advantage in. You can choose until you first step into a story.");
            foreach (var b in r.Backgrounds)
            {
                var skills = string.Join(", ", b.Skills.Select(k => r.Skill(k)?.Name ?? k));
                string id = b.Ref;
                bool current = b.Ref == m.Background;
                p.Row($"[b]{Ui.Esc(b.Name)}[/b]{(current ? "  " + Tag("now", Ui.Gold) : "")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(skills)} · best at {Ui.Esc(string.Join(", ", b.Abilities.Take(2).Select(x => x.ToUpperInvariant())))}. {Ui.Esc(b.Text)}[/font_size]")}",
                    ("Choose", true, current, () => Act(() => _sim.ChooseBackground(id))));
            }
        }
        else p.Text($"[b]{Ui.Esc(bg.Name)}[/b]  {Tag(bg.Feature, Ui.Gold)}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(bg.Text)}[/font_size]")}", 13);

        p.Heading("Skills");
        var lines = r.Skills.Select(sk =>
        {
            var (mod, pr, ab) = Stories.Bonus(t, sk.Id);
            int bonus = mod + pr;
            string dot = Stories.Expert(t, sk.Id) ? Ui.C(Ui.Gold, "◆") : Stories.Proficient(t, sk.Id) ? Ui.C(Ui.Gold, "●") : Ui.C(Ui.Line, "○");
            return $"{dot} {(pr > 0 ? $"[b]{Ui.Esc(sk.Name)}[/b]" : Ui.Esc(sk.Name))} {Ui.C(Ui.Ink2, $"[font_size=11]{ab.ToUpperInvariant()}[/font_size]")}  [b]{(bonus >= 0 ? "+" : "−")}{Math.Abs(bonus)}[/b]";
        }).ToList();
        int half = (lines.Count + 1) / 2;
        p.Add("sk" + string.Join("|", lines), () =>
        {
            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 16);
            grid.AddThemeConstantOverride("v_separation", 2);
            for (int i = 0; i < half; i++)
            {
                grid.AddChild(SkillCell(lines[i]));
                grid.AddChild(SkillCell(i + half < lines.Count ? lines[i + half] : ""));
            }
            return grid;
        });

        var open = Stories.Pending(t);
        p.Heading("Waiting on you");
        if (open.Count == 0) p.Note("No story needs you now. They come up as the town lives: in its taverns, its council, its heroes and its wars.");
        foreach (var s in open)
        {
            var d = Stories.DefOf(t, s);
            var (word, color) = StakesLook(d.Stakes);
            int days = Math.Max(1, (int)Math.Ceiling((s.DueTick - t.Tick) / 24.0));
            int id = s.Id;
            p.Row($"[b]{Ui.Esc(Stories.Fill(t, s, d.Title))}[/b]\n{Tag(word, color)} {Ui.C(Ui.Ink2, $"[font_size=12]· runs its course in {Ui.Days(days)}[/font_size]")}", ("Step in", true, true, () => OpenStory(id)));
        }
        var decrees = Decrees.Active(t).ToList();
        if (decrees.Count > 0)
        {
            p.Heading("Decrees in force");
            foreach (var d in decrees)
                p.Text($"[b]{Ui.Esc(d.Name)}[/b]  {Tag(Ui.Days(Decrees.DaysLeft(t, d.Id)) + " left", Ui.Ink2)}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(d.Text)}[/font_size]")}", 13);
        }
        var called = Callings.Holders(t).ToList();
        if (called.Count > 0)
        {
            p.Heading("Callings");
            p.Note("Parts heroes have taken up in the town through the stories of their class. Each lasts while its hero stays and stays loyal.");
            foreach (var (hero, c) in called)
                p.Text($"[b]{Ui.Esc(c.Name)}[/b]  {AgentLink(hero)} {Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(hero.Hero!.Cls.Name.ToLowerInvariant())}[/font_size]")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(c.Text)}[/font_size]")}", 13);
        }
        var remembered = t.Stories.Precedents.Keys.Select(id => r.Precedent(id)).OfType<PrecedentDef>().ToList();
        if (remembered.Count > 0)
        {
            p.Heading("The town remembers");
            p.Note("Hard choices are not forgotten. Later stories turn on them.");
            foreach (var pr in remembered)
                p.Text($"{Ui.C(Ui.Ink2, $"[font_size=11]Day {t.Stories.Precedents[pr.Id]}[/font_size]")}  {Ui.Esc(pr.Text)}", 13);
        }
        var past = t.Stories.All.Where(s => s.Done).OrderByDescending(s => s.Id).Take(8).ToList();
        if (past.Count > 0)
        {
            p.Heading("Deeds");
            foreach (var s in past)
            {
                var b = s.Beats[^1];
                var color = b.Result switch { StoryResult.Critical or StoryResult.Success or StoryResult.Certain => Ui.Good, StoryResult.Partial => Ui.Gold, StoryResult.Ignored => Ui.Ink2, _ => Ui.Bad };
                string what = b.Result == StoryResult.Ignored ? "let it be" : b.Result == StoryResult.Certain ? "settled" : Stories.Word(b.Result);
                p.Text($"{Ui.C(Ui.Ink2, $"[font_size=11]Day {b.Tick / 24 + 1}[/font_size]")}  {Ui.Esc(Stories.Fill(t, s, Stories.DefOf(t, s).Title))}  {Tag(what, color)}", 13);
            }
        }
        p.Commit();
    }

    /// <summary>The first ability picked for a +1/+1 improvement, until the second is.</summary>
    private string? _abilityPick;

    /// <summary>The level-up choice: two ability points or a feat at improvement levels, a skill or expertise at the others.</summary>
    private void LevelUp(Pane p)
    {
        var t = _sim.Town;
        var r = t.Content.Stories;
        var (level, improvement) = Sheet.NextAdvance(t);
        int pending = Sheet.Pending(t);
        p.Card($"[font_size=16][b]Level {level}: choose how you grow[/b][/font_size]{(pending > 1 ? Ui.C(Ui.Ink2, $"  ({pending} choices waiting)") : "")}\n{Ui.C(Ui.Ink2, improvement ? "Raise two abilities by 1 (or one by 2), or take a feat." : $"Learn a new skill{(level >= r.ExpertiseFrom ? ", or become expert in one you know (double proficiency)" : "")}.")}", Ui.Gold);
        if (improvement)
        {
            var scores = Stories.Scores(t);
            string sig = $"lvab{level}{_abilityPick}{scores}";
            p.Add(sig, () =>
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 6);
                foreach (var ab in Ability.All)
                {
                    string adv = _abilityPick == null ? "" : $"ability:{_abilityPick},{ab}";
                    bool ok = _abilityPick == null ? scores[ab] < 20 : Sheet.Refuse(t, adv) == null;
                    var b = new Button { Text = $"{ab.ToUpperInvariant()} {scores[ab]}{(_abilityPick == ab ? " +1" : "")}", Disabled = !ok, FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill, ThemeTypeVariation = _abilityPick == ab ? "Post" : "" };
                    string pick = ab;
                    b.Pressed += () =>
                    {
                        if (_abilityPick == null) _abilityPick = pick;
                        else
                        {
                            Act(() => _sim.AdvanceMayor($"ability:{_abilityPick},{pick}"));
                            _abilityPick = null;
                        }
                        _dirty = true;
                    };
                    row.AddChild(b);
                }
                return row;
            });
            if (_abilityPick != null) p.Note($"+1 {_abilityPick.ToUpperInvariant()}. Pick a second ability, or {_abilityPick.ToUpperInvariant()} again for +2.");
            foreach (var f in r.Feats)
            {
                string id = "feat:" + f.Id;
                bool ok = Sheet.Refuse(t, id) == null;
                p.Row($"[b]{Ui.Esc(f.Name)}[/b]\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(f.Text)}[/font_size]")}", ("Take", ok, false, () => Act(() => _sim.AdvanceMayor(id))));
            }
            return;
        }
        var choices = r.Skills
            .Select(sk => (Skill: sk, Advance: Stories.Proficient(t, sk.Id) ? "expertise:" + sk.Id : "skill:" + sk.Id))
            .Where(c => Sheet.Refuse(t, c.Advance) == null)
            .ToList();
        p.Add($"lvsk{level}{string.Join("|", choices.Select(c => c.Advance))}", () =>
        {
            var grid = new GridContainer { Columns = 3 };
            grid.AddThemeConstantOverride("h_separation", 6);
            grid.AddThemeConstantOverride("v_separation", 6);
            foreach (var (sk, adv) in choices)
            {
                bool expert = adv.StartsWith("expertise:", StringComparison.Ordinal);
                var b = new Button { Text = $"{(expert ? "◆ " : "+ ")}{sk.Name}", FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = expert ? $"Become expert in {sk.Name}: double proficiency." : $"Learn {sk.Name} ({sk.Ability.ToUpperInvariant()})." };
                string a = adv;
                b.Pressed += () => Act(() => _sim.AdvanceMayor(a));
                grid.AddChild(b);
            }
            return grid;
        });
    }

    /// <summary>The newest everyday-decision check shown, so each roll toasts once.</summary>
    private int _checksSeen = -1;

    /// <summary>A d20 rolled on one of the player's decisions shows as a toast.</summary>
    private void SyncChecks()
    {
        var st = _sim.Town.Stories;
        if (_checksSeen < 0 || _checksSeen > st.Checks) _checksSeen = st.Checks;
        if (st.Checks == _checksSeen || st.LastCheck is not { } b) return;
        _checksSeen = st.Checks;
        if (_sim.Options.AutoStories) return;
        var r = _sim.Town.Content.Stories;
        string skill = r.Skill(b.Skill)?.Name ?? b.Skill;
        Toast($"d20 {skill}: {b.Die} → {b.Total} vs DC {b.Dc}, {Stories.Word(b.Result)}.{(b.Text != "" ? " " + b.Text : "")}");
    }

    private static Control SkillCell(string bbcode)
    {
        var r = Ui.Rich(bbcode, 13);
        r.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return r;
    }

    private static Control AbilityRow(Abilities a)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        foreach (var (key, name) in new[] { (Ability.Str, "STR"), (Ability.Dex, "DEX"), (Ability.Con, "CON"), (Ability.Int, "INT"), (Ability.Wis, "WIS"), (Ability.Cha, "CHA") })
        {
            int score = a[key], mod = Rng.Modifier(score);
            var cell = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            cell.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel2, Ui.Line, 8, 1, 4, 6));
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", -2);
            foreach (var (text, color, font, size) in new[] { (name, Ui.Ink2, Ui.Bold, 10), ($"{(mod >= 0 ? "+" : "−")}{Math.Abs(mod)}", Ui.Ink, Ui.Display, 22), (score.ToString(), Ui.Ink2, Ui.Medium, 11) })
            {
                var l = Ui.Label(text, color, font, size);
                l.HorizontalAlignment = HorizontalAlignment.Center;
                v.AddChild(l);
            }
            cell.AddChild(v);
            row.AddChild(cell);
        }
        return row;
    }
}
