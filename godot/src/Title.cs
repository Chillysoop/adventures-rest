using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The main menu the game opens on: a town turning slowly behind the title, and Continue, Start, Start
/// with tutorial, Load, History, Glossary, Settings and Quit. Starting a town first asks who will tell its story: the storytellers of
/// content/game/storytellers.json, the game's difficulty. The game menu (Esc) can bring it back.
/// </summary>
public partial class Main
{
    private Control _title = null!;
    private Button _continue = null!;
    private Label _continueAbout = null!;
    private readonly List<Control> _hiddenByTitle = [];
    private bool _menuLoadOnly;
    private VBoxContainer _titleMenu = null!, _tellers = null!;
    private bool _tellTutorial;

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
        TitleButton(col, "Settings", "Clock stops, autosave, camera, display, keys", primary: false, () => OpenSettings());
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
            TitleButton(_tellers, $"{st.Title}  ·  {st.Name}", st.Text, primary: id == rules.Default, () => StartTown(_tellTutorial, storyteller: id));
            var about = Ui.Label($"{st.Character} {st.Text}", Ui.Ink2, Ui.Body, 13);
            about.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            about.CustomMinimumSize = new Vector2(520, 0);
            _tellers.AddChild(about);
            _tellers.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        }
        BuildCabinet(_tellers);
        TitleButton(_tellers, "Back", "Back to the main menu", primary: false, () => PickStoryteller(null));
    }

    /// <summary>Show the storytellers to start a town under (with or without the tutorial), or with null go back to the menu.</summary>
    private void PickStoryteller(bool? tutorial)
    {
        _tellTutorial = tutorial == true;
        _titleMenu.Visible = tutorial == null;
        _tellers.Visible = tutorial != null;
        if (tutorial != null) FillCabinet();
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
        // A town opens paused, the clock waiting on the player (Space or a speed).
        SetSpeed(0);
    }

    private void StartTown(bool tutorial, int? seed = null, bool autoplay = false, string? storyteller = null)
    {
        NewTown(seed ?? (int)(GD.Randi() % 9999) + 1, autoplay, storyteller);
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
