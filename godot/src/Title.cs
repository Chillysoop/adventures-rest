using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The main menu the game opens on: a town turning slowly behind the title, and Continue, Start, Start
/// with tutorial, Load, History, Glossary, Settings and Quit. Starting a town first asks who will tell its story: the storytellers of
/// content/game/storytellers.json, the game's difficulty; then where to found it, on a map of the new world with a pin for
/// every kind of site (content/game/founding.json, <see cref="FoundingMap"/>); then shows the mayor's sheet, rolled, to reroll and give a background. The game menu
/// (Esc) can bring it back.
/// </summary>
public partial class Main
{
    private Control _title = null!;
    private Button _continue = null!;
    private Label _continueAbout = null!;
    private readonly List<Control> _hiddenByTitle = [];
    private bool _menuLoadOnly;
    private VBoxContainer _titleMenu = null!, _tellers = null!, _sites = null!, _mayorSetup = null!;
    private bool _tellTutorial;
    private string? _tellerPick;

    /// <summary>The site picked on New game, where on the map (days from the world's heart), and the seed of the world it was picked in.</summary>
    private string? _foundingPick;
    private (double X, double Y)? _homePick;
    private int? _seedPick;

    /// <summary>New game's map, what it says of the spot under the pointer, and the kinds of town the picked spot takes.</summary>
    private FoundingMap _siteMap = null!;
    private RichTextLabel _siteInfo = null!;
    private VBoxContainer _siteKinds = null!;
    private Town? _sitePreview;

    /// <summary>The mayor rolled for the new town: rerolled with a new seed, the background picked on it.</summary>
    private MayorRoll _mayorRoll = new();

    /// <summary>New game's Hardcore toggle: the mayor's death ends the campaign.</summary>
    private bool _hardcore;

    /// <summary>The name New game's town will take, as typed or rolled.</summary>
    private string _townPick = "";

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
            TitleButton(_tellers, $"{st.Title}  ·  {st.Name}", st.Text, primary: id == rules.Default, () => PickSite(id));
            var about = Ui.Label($"{st.Character} {st.Text}", Ui.Ink2, Ui.Body, 13);
            about.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            about.CustomMinimumSize = new Vector2(520, 0);
            _tellers.AddChild(about);
            _tellers.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        }
        BuildCabinet(_tellers);
        TitleButton(_tellers, "Back", "Back to the main menu", primary: false, () => PickStoryteller(null));

        // Where to found it: the new world's map beside the column, filled for the seed when a storyteller is picked.
        _sites = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Visible = false };
        _sites.AddThemeConstantOverride("separation", 6);
        stack.AddChild(_sites);
        _siteMap = new FoundingMap { Visible = false };
        _title.AddChild(_siteMap);
        _siteMap.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _siteMap.OffsetLeft = 700;
        _siteMap.OffsetRight = -40;
        _siteMap.OffsetTop = 40;
        _siteMap.OffsetBottom = -40;
        _siteMap.Picked += (x, y) => ShowSiteInfo(x, y);

        // Then the mayor: a rolled sheet to reroll and a background to pick, before the charter is signed.
        _mayorSetup = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Visible = false };
        _mayorSetup.AddThemeConstantOverride("separation", 8);
        stack.AddChild(_mayorSetup);
    }

    /// <summary>
    /// Where to found the town: the new world's map, a pin for every kind of site in its heartland, and anywhere else in
    /// the gold ring that suits a kind. The pins passed over stay on the map, for neighbours or foes to take.
    /// </summary>
    private void PickSite(string storyteller, int? seed = null)
    {
        _tellerPick = storyteller;
        int s = seed ?? _opts.Seed ?? (int)(GD.Randi() % 9999) + 1;
        if (_seedPick != s) _homePick = _opts.Home;
        _seedPick = s;
        foreach (var c in _sites.GetChildren()) c.QueueFree();
        _sites.AddChild(Ui.Label("Where do you found your town?", Ui.Ink, Ui.Display, 30));
        var how = Ui.Rich(Ui.Bullets([
            $"{Ui.Img("found-here", 16)} Click a pin, or anywhere in the gold ring",
            $"{Ui.Img("threat", 16)} Farther from the middle, harder lairs lie closer"]));
        how.FitContent = true;
        how.CustomMinimumSize = new Vector2(520, 0);
        _sites.AddChild(how);
        _sites.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        _siteInfo = Ui.Rich("");
        _siteInfo.FitContent = true;
        _siteInfo.CustomMinimumSize = new Vector2(520, 0);
        _sites.AddChild(_siteInfo);
        _siteKinds = new VBoxContainer();
        _siteKinds.AddThemeConstantOverride("separation", 6);
        _sites.AddChild(_siteKinds);
        _sites.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        TitleButton(_sites, "New world", "Roll another world to found the town in", primary: false, () => { _homePick = null; PickSite(storyteller, (int)(GD.Randi() % 9999) + 1); });
        TitleButton(_sites, "Back", "Back to the storytellers", primary: false, () => PickStoryteller(_tellTutorial));
        _sitePreview = new Simulation(new SimulationOptions { Seed = s }).Town;
        _siteMap.Show(_sitePreview, _homePick, _opts.SiteHover);
        var (hx, hy) = _homePick ?? (0, 0);
        ShowSiteInfo(hx, hy);
        _titleMenu.Visible = _tellers.Visible = _mayorSetup.Visible = false;
        _sites.Visible = _siteMap.Visible = true;
    }

    /// <summary>
    /// The spot picked on New game's map: its country and danger, and each kind of town it takes with what that kind does,
    /// each a button that founds it there.
    /// </summary>
    private void ShowSiteInfo(double x, double y)
    {
        if (_sitePreview is not { } t) return;
        _homePick = (x, y);
        _siteMap.Pick(_homePick);
        var lines = new List<string>();
        var region = Explore.RegionAt(t, x, y);
        if (region != null)
        {
            var lv = region.Levels;
            var danger = region.Ring switch { 0 => (Ui.Good, "calm"), 1 => (Ui.Gold, "wild"), 2 => (Ui.Accent, "dangerous"), _ => (Ui.Bad, "deadly") };
            lines.Add($"[b]{Ui.Esc(region.Name)}[/b]  {Ui.Dim(Ui.Esc(region.Theme.Name))}");
            lines.Add(Ui.Chips(
                Ui.Chip("threat", $"Lv {lv[0]}–{lv[^1]}", danger.Item1, $"Lairs here are built for heroes of level {lv[0]} to {lv[^1]}: {danger.Item2} country"),
                Ui.Chip("ui-road", $"{Js.Str(Js.Round(Js.Hypot(x, y) * 10) / 10)} days out", null, "Days' march from the middle of the world")));
        }
        foreach (var c in _siteKinds.GetChildren()) c.QueueFree();
        var kinds = Founding.KindsAt(t, x, y);
        if (Founding.Blocked(t, x, y) is { } why) lines.Add(Ui.C(Ui.Bad, $"{Ui.Img("ui-no", 15)} No town here: {Ui.Esc(why)}"));
        else
            foreach (var k in kinds)
            {
                var chips = Founding.Effects(k);
                lines.Add($"{Ui.Img($"found-{k.Id}", 18)} {Ui.Tip($"[b]{Ui.Esc(k.Name)}[/b]", k.Blurb)}  {(chips.Count == 0 ? Ui.Dim("no bonus, no drawback") : Ui.Chips(chips.Select(c => Ui.Chip(c.Icon, c.Text)).ToArray()))}");
            }
        _siteInfo.Text = Ui.Dress(string.Join("\n", lines));
        if (kinds.Count == 0 || Founding.Blocked(t, x, y) != null) return;
        // The pin's own kind first, when the spot is a pin.
        var pin = Founding.Spots(t).FirstOrDefault(p => Js.Hypot(p.X - x, p.Y - y) < 0.05);
        foreach (var k in kinds.OrderBy(k => k.Id == pin?.Kind ? 0 : 1))
        {
            string id = k.Id;
            var (hx, hy) = (x, y);
            TitleButton(_siteKinds, $"Found a {k.Name.ToLowerInvariant()} here", $"{k.Blurb}\n{Founding.Summary(k)}", primary: id == (pin?.Kind ?? kinds[0].Id), () =>
            {
                _foundingPick = id;
                _homePick = (hx, hy);
                PickMayor(_tellerPick ?? "rising");
            });
        }
    }

    /// <summary>After the site, the mayor: a fresh roll, kept until Begin or Back.</summary>
    private void PickMayor(string storyteller)
    {
        _tellerPick = storyteller;
        _hardcore = _opts.Hardcore;
        if (_townPick == "") RollTownName();
        _mayorRoll = Stories.RollMayor(Sim.Content.Default(), (int)(GD.Randi() & 0x7fffffff));
        _tellers.Visible = _sites.Visible = _siteMap.Visible = false;
        _titleMenu.Visible = false;
        _mayorSetup.Visible = true;
        RenderMayorSetup();
    }

    /// <summary>A name off founding.json's list, never the one already showing.</summary>
    private void RollTownName()
    {
        var names = Sim.Content.Default().Founding.Names.Where(n => n != _townPick).ToList();
        if (names.Count > 0) _townPick = names[(int)(GD.Randi() % (uint)names.Count)];
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
        // The town's name first: typed, or rolled from founding.json's list.
        col.AddChild(Ui.Label("Your town", Ui.Ink2, Ui.Bold, 13));
        var named = new HBoxContainer();
        named.AddThemeConstantOverride("separation", 14);
        var nameBox = new LineEdit
        {
            Text = _townPick, PlaceholderText = "Name your town", MaxLength = Sim.Content.Default().Founding.NameMost,
            CustomMinimumSize = new Vector2(320, 44), TooltipText = "What the town is called in the Ledger, the songs and the history books",
        };
        nameBox.AddThemeFontOverride("font", Ui.Display);
        nameBox.AddThemeFontSizeOverride("font_size", 24);
        nameBox.TextChanged += text => _townPick = text;
        named.AddChild(nameBox);
        var rollName = new Button { Text = "Roll a name", TooltipText = "A name from the old maps", CustomMinimumSize = new Vector2(120, 40), SizeFlagsVertical = SizeFlags.ShrinkCenter, FocusMode = FocusModeEnum.None };
        Ui.GoldButton(rollName);
        rollName.Pressed += () => { RollTownName(); nameBox.Text = _townPick; };
        named.AddChild(rollName);
        col.AddChild(named);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });

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
        TitleButton(col, "Begin", "Sign the charter and start the town", primary: true, () => StartTown(_tellTutorial, _seedPick, storyteller: _tellerPick, founding: _foundingPick, mayor: _mayorRoll, hardcore: _hardcore, townName: _townPick, home: _homePick));
        TitleButton(col, "Back", "Back to the sites", primary: false, () => PickSite(_tellerPick ?? "rising", _seedPick));
    }

    /// <summary>Show the storytellers to start a town under (with or without the tutorial), or with null go back to the menu.</summary>
    private void PickStoryteller(bool? tutorial)
    {
        _tellTutorial = tutorial == true;
        _titleMenu.Visible = tutorial == null;
        _tellers.Visible = tutorial != null;
        _sites.Visible = _siteMap.Visible = _mayorSetup.Visible = false;
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

    private void StartTown(bool tutorial, int? seed = null, bool autoplay = false, string? storyteller = null, string? founding = null, MayorRoll? mayor = null, bool hardcore = false, string? townName = null, (double X, double Y)? home = null)
    {
        NewTown(seed ?? (int)(GD.Randi() % 9999) + 1, autoplay, storyteller, founding, mayor, hardcore, townName, home);
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
