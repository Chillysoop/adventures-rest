using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The main menu the game opens on: a town turning slowly behind the title, and Continue, Start, Start
/// with tutorial, Load, History, Glossary, Settings and Quit. Starting a town first asks who will tell its story: the storytellers of
/// content/game/storytellers.json, the game's difficulty; then shows the mayor's sheet, rolled, to reroll
/// and give a background. The game menu (Esc) can bring it back.
/// </summary>
public partial class Main
{
    private Control _title = null!;
    private Button _continue = null!;
    private Label _continueAbout = null!;
    private readonly List<Control> _hiddenByTitle = [];
    private bool _menuLoadOnly;
    private VBoxContainer _titleMenu = null!, _tellers = null!, _mayorSetup = null!;
    private bool _tellTutorial;
    private string? _tellerPick;

    /// <summary>The mayor rolled for the new town: rerolled with a new seed, the background picked on it.</summary>
    private MayorRoll _mayorRoll = new();

    /// <summary>New game's Hardcore toggle: the mayor's death ends the campaign.</summary>
    private bool _hardcore;

    private void BuildTitle()
    {
        _title = new Control { Visible = false, MouseFilter = MouseFilterEnum.Stop };
        AddChild(_title);
        _title.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // A dark wash from the left, fading out over the town.
        var shade = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient { Colors = [new Color(0.05f, 0.04f, 0.03f, 0.94f), new Color(0.05f, 0.04f, 0.03f, 0.75f), new Color(0.05f, 0.04f, 0.03f, 0)], Offsets = [0, 0.45f, 1] },
                FillTo = new Vector2(1, 0),
                Width = 256,
                Height = 4,
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _title.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide);
        shade.OffsetRight = 900;

        var pad = new MarginContainer();
        pad.AddThemeConstantOverride("margin_left", 96);
        _title.AddChild(pad);
        pad.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide);
        pad.OffsetRight = 640;
        var stack = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        pad.AddChild(stack);
        var col = _titleMenu = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        col.AddThemeConstantOverride("separation", 10);
        stack.AddChild(col);

        var name = Ui.Label("Crown & Compass", Ui.Ink, Ui.Display, 58);
        col.AddChild(name);
        var rule = new ColorRect { Color = Ui.Gold, CustomMinimumSize = new Vector2(120, 3), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        col.AddChild(rule);
        col.AddChild(Ui.Label("A town of heroes at the edge of the wilds", Ui.Ink2, Ui.Medium, 17));
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 30) });

        _continue = TitleButton(col, "Continue", "Pick up the newest save", primary: true, () => Continue());
        _continueAbout = Ui.Label("", Ui.Ink2, Ui.Body, 12);
        _continueAbout.CustomMinimumSize = new Vector2(0, 0);
        col.AddChild(_continueAbout);
        TitleButton(col, "Start a new town", "A fresh town on a random seed", primary: false, () => PickStoryteller(tutorial: false));
        TitleButton(col, "Start with tutorial", "A fresh town, with a guide to the basics", primary: false, () => PickStoryteller(tutorial: true));
        TitleButton(col, "Load a town", "Pick a save slot", primary: false, OpenLoad);
        TitleButton(col, "History of the world", "The ages, empires and peoples before your town", primary: false, () => OpenHistory());
        TitleButton(col, "Glossary", "Every keyword in the game, explained (F1)", primary: false, () => OpenGlossary());
        TitleButton(col, "Settings", "Which events stop the clock", primary: false, OpenSettings);
        TitleButton(col, "Quit", "Close the game", primary: false, () => GetTree().Quit());

        // Who tells the new town's story: one button each, with their character and what playing under them is like.
        _tellers = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Visible = false };
        _tellers.AddThemeConstantOverride("separation", 6);
        stack.AddChild(_tellers);
        _tellers.AddChild(Ui.Label("Who tells your town's story?", Ui.Ink, Ui.Display, 30));
        _tellers.AddChild(Ui.Label("The storyteller sets how hard the campaign pushes. You can't change it later.", Ui.Ink2, Ui.Body, 13));
        _tellers.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
        var rules = Sim.Content.Default().Storytellers;
        foreach (var st in rules.Storytellers)
        {
            string id = st.Id;
            TitleButton(_tellers, $"{st.Title}  ·  {st.Name}", st.Text, primary: id == rules.Default, () => PickMayor(id));
            var about = Ui.Label($"{st.Character} {st.Text}", Ui.Ink2, Ui.Body, 13);
            about.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            about.CustomMinimumSize = new Vector2(520, 0);
            _tellers.AddChild(about);
            _tellers.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        }
        TitleButton(_tellers, "Back", "Back to the main menu", primary: false, () => PickStoryteller(null));

        // Then the mayor: a rolled sheet to reroll and a background to pick, before the charter is signed.
        _mayorSetup = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Visible = false };
        _mayorSetup.AddThemeConstantOverride("separation", 8);
        stack.AddChild(_mayorSetup);
    }

    /// <summary>After the storyteller, the mayor: a fresh roll, kept until Begin or Back.</summary>
    private void PickMayor(string storyteller)
    {
        _tellerPick = storyteller;
        _hardcore = _opts.Hardcore;
        _mayorRoll = Stories.RollMayor(Sim.Content.Default(), (int)(GD.Randi() & 0x7fffffff));
        _tellers.Visible = false;
        _titleMenu.Visible = false;
        _mayorSetup.Visible = true;
        RenderMayorSetup();
    }

    private void RerollMayor()
    {
        _mayorRoll = Stories.RollMayor(Sim.Content.Default(), (int)(GD.Randi() & 0x7fffffff), _mayorRoll.Background);
        RenderMayorSetup();
    }

    private void PickMayorBackground(string id)
    {
        _mayorRoll = _mayorRoll with { Background = id };
        RenderMayorSetup();
    }

    /// <summary>The mayor's sheet as rolled: name, six abilities with the background's +2 and +1, the backgrounds to pick from.</summary>
    private void RenderMayorSetup()
    {
        foreach (var c in _mayorSetup.GetChildren()) c.QueueFree();
        var r = Sim.Content.Default().Stories;
        var bg = r.Background(_mayorRoll.Background);
        var col = _mayorSetup;
        col.AddChild(Ui.Label("Your mayor", Ui.Ink2, Ui.Bold, 13));
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 14);
        head.AddChild(Ui.Label(_mayorRoll.Name, Ui.Ink, Ui.Display, 34));
        var reroll = new Button { Text = "Reroll", Icon = Ui.Icon("reroll"), ExpandIcon = true, TooltipText = "Roll new scores and a new name (4d6, lowest dropped)", CustomMinimumSize = new Vector2(120, 40), SizeFlagsVertical = SizeFlags.ShrinkCenter, FocusMode = FocusModeEnum.None };
        reroll.AddThemeConstantOverride("icon_max_width", 22);
        Ui.GoldButton(reroll);
        reroll.Pressed += RerollMayor;
        head.AddChild(reroll);
        col.AddChild(head);
        var rolls = Ui.Rich($"{Ui.Img("d20", 14)} {Ui.C(Ui.Ink2, string.Join("  ", _mayorRoll.Scores))}   {Ui.C(Ui.Ink2, $"total {_mayorRoll.Scores.Sum()}")}", 13);
        rolls.CustomMinimumSize = new Vector2(520, 0);
        col.AddChild(rolls);
        var abilities = AbilityRow(Stories.Deal(r, bg.Ref, _mayorRoll.Scores));
        abilities.CustomMinimumSize = new Vector2(520, 0);
        col.AddChild(abilities);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });

        col.AddChild(Ui.Label("Background", Ui.Ink2, Ui.Bold, 13));
        var grid = new GridContainer { Columns = 2, CustomMinimumSize = new Vector2(520, 0) };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        foreach (var b in r.Backgrounds)
        {
            string id = b.Ref;
            bool on = id == bg.Ref;
            var btn = new Button { Text = b.Name, TooltipText = b.Text, ToggleMode = true, ButtonPressed = on, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 34), FocusMode = FocusModeEnum.None, ThemeTypeVariation = on ? "Post" : "" };
            Ui.GoldButton(btn);
            btn.Pressed += () => PickMayorBackground(id);
            grid.AddChild(btn);
        }
        col.AddChild(grid);
        // The picked background in brief: its two skills, its best abilities and the stories it has advantage in.
        string skills = string.Join("   ", bg.Skills.Select(k => $"{Ui.C(Ui.Gold, "●")} {Ui.Esc(r.Skill(k)?.Name ?? k)}"));
        var about = Ui.Rich($"{skills}\n{Ui.C(Ui.Gold, "▲")} {string.Join(" ", bg.Abilities.Take(2).Select(a => a.ToUpperInvariant()))}   {Ui.Img("d20", 13)} {Ui.C(Ui.Ink2, $"advantage in {Ui.Esc(bg.Favoured)} stories")}", 13);
        about.CustomMinimumSize = new Vector2(520, 0);
        col.AddChild(about);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        var hard = new CheckButton { Text = "Hardcore", ButtonPressed = _hardcore, TooltipText = "Your mayor dies, you lose. No raising, no successor.", FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        hard.Icon = Ui.Icon("face-death-skull");
        hard.AddThemeConstantOverride("icon_max_width", 22);
        hard.Toggled += on => _hardcore = on;
        col.AddChild(hard);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        TitleButton(col, "Begin", "Sign the charter and start the town", primary: true, () => StartTown(_tellTutorial, storyteller: _tellerPick, mayor: _mayorRoll, hardcore: _hardcore));
        TitleButton(col, "Back", "Back to the storytellers", primary: false, () => PickStoryteller(_tellTutorial));
    }

    /// <summary>Show the storytellers to start a town under (with or without the tutorial), or with null go back to the menu.</summary>
    private void PickStoryteller(bool? tutorial)
    {
        _tellTutorial = tutorial == true;
        _titleMenu.Visible = tutorial == null;
        _tellers.Visible = tutorial != null;
        _mayorSetup.Visible = false;
    }

    private static Button TitleButton(VBoxContainer col, string text, string tip, bool primary, Action pressed)
    {
        var b = new Button { Text = text, TooltipText = tip, CustomMinimumSize = new Vector2(320, 50), SizeFlagsHorizontal = SizeFlags.ShrinkBegin, Alignment = HorizontalAlignment.Left, FocusMode = FocusModeEnum.None };
        b.AddThemeFontOverride("font", Ui.Display);
        b.AddThemeFontSizeOverride("font_size", 20);
        // Oak planks with iron studs, and the red banner for the first choice (Castle.cs).
        b.AddThemeStyleboxOverride("normal", Castle.Button("normal", primary, 22, 8));
        b.AddThemeStyleboxOverride("hover", Castle.Button("hover", primary, 22, 8));
        b.AddThemeStyleboxOverride("pressed", Castle.Button("pressed", primary, 22, 8));
        b.AddThemeStyleboxOverride("disabled", Castle.Button("disabled", false, 22, 8));
        b.AddThemeColorOverride("font_color", Ui.Ink);
        b.AddThemeColorOverride("font_hover_color", Colors.White);
        b.AddThemeColorOverride("font_disabled_color", new Color(Ui.Ink2, 0.5f));
        b.Pressed += pressed;
        col.AddChild(b);
        return b;
    }

    /// <summary>Show the main menu over the town as it stands, with the HUD put away and the clock stopped.</summary>
    private void ShowTitle()
    {
        if (_title.Visible) return;
        _hiddenByTitle.Clear();
        foreach (var c in GetChildren().OfType<Control>())
            if (c != _map && c != _title && c != _menu && c != _guideCard && c != _outline && c != _toast.GetParent() && c.Visible)
            {
                c.Visible = false;
                _hiddenByTitle.Add(c);
            }
        if (_menu.Visible) CloseMenu();
        SetSpeed(0);
        _title.Visible = true;
        PickStoryteller(null);
        var newest = NewestSave();
        _continue.Disabled = newest == null;
        _continueAbout.Text = newest is var (slot, h) ? $"{SlotTitle(slot)}: {Describe(h)}" : "No saved towns yet";
        _map.LookAt(Geo.Center, 28);
    }

    private void HideTitle()
    {
        if (!_title.Visible) return;
        _title.Visible = false;
        foreach (var c in _hiddenByTitle) c.Visible = true;
        _hiddenByTitle.Clear();
        _map.Recenter();
        SetSpeed(1);
    }

    private void StartTown(bool tutorial, int? seed = null, bool autoplay = false, string? storyteller = null, MayorRoll? mayor = null, bool hardcore = false)
    {
        NewTown(seed ?? (int)(GD.Randi() % 9999) + 1, autoplay, storyteller, mayor, hardcore);
        // Begin at first light rather than in the dark of the first midnight.
        for (int i = 0; i < 7; i++) _sim.Step();
        HideTitle();
        if (tutorial)
        {
            StartTutorial();
            // The first lesson is starting the clock.
            SetSpeed(0);
        }
        else OfferNewSteps();
    }

    private void OpenLoad()
    {
        _menuLoadOnly = true;
        OpenMenu();
    }

    private static (string Slot, SaveHeader Header)? NewestSave()
    {
        if (!Directory.Exists(SaveDir)) return null;
        foreach (var f in new DirectoryInfo(SaveDir).GetFiles("*.save").OrderByDescending(f => f.LastWriteTimeUtc))
        {
            string slot = Path.GetFileNameWithoutExtension(f.Name);
            if (Header(slot) is SaveHeader h) return (slot, h);
        }
        return null;
    }
}
