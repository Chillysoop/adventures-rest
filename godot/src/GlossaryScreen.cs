using CrownAndCompass.Sim;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The glossary (content/game/glossary.json) as a book: every keyword by chapter down the left, with a search,
/// and the chosen entry on the right with the entries that mention it. Opened from the main menu, the game
/// menu, the top bar's book and F1. The same entries explain the gold keywords everywhere else.
/// </summary>
public partial class Main
{
    private Control _glossary = null!;
    private LineEdit _glossarySearch = null!;
    private VBoxContainer _glossaryList = null!;
    private VBoxContainer _glossaryPage = null!;
    private string? _glossaryId;
    private static readonly Texture2D Blank = ImageTexture.CreateFromImage(Image.CreateEmpty(20, 20, false, Image.Format.Rgba8));

    private void BuildGlossary()
    {
        _glossary = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Stop };
        _glossary.AddThemeStyleboxOverride("panel", Ui.Flat(Ui.Bg, 28, 20));
        AddChild(_glossary);
        _glossary.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 14);
        _glossary.AddChild(col);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 14);
        col.AddChild(head);
        head.AddChild(new TextureRect { Texture = Ui.Icon("glossary"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(40, 40), SizeFlagsVertical = SizeFlags.ShrinkCenter, Modulate = Ui.Gold });
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 2);
        head.AddChild(titles);
        titles.AddChild(Ui.Label("Glossary", Ui.Ink, Ui.Display, 32));
        titles.AddChild(Ui.Label("Every gold word in the game is in here. Hover one anywhere for its card, and click it to pin the card.", Ui.Ink2, Ui.Medium, 14));
        var close = new Button { Text = "Close", TooltipText = "Back (Esc)", SizeFlagsVertical = SizeFlags.ShrinkCenter, FocusMode = FocusModeEnum.None };
        close.Pressed += CloseGlossary;
        head.AddChild(close);

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 18);
        col.AddChild(body);

        var left = new VBoxContainer { CustomMinimumSize = new Vector2(330, 0) };
        left.AddThemeConstantOverride("separation", 10);
        body.AddChild(left);
        _glossarySearch = new LineEdit { PlaceholderText = "Search the glossary", ClearButtonEnabled = true };
        _glossarySearch.AddThemeStyleboxOverride("normal", Ui.Box(Ui.Panel, Ui.Line, 8, 1, 12, 8));
        _glossarySearch.AddThemeStyleboxOverride("focus", Ui.Box(Ui.Panel, Ui.Gold, 8, 1, 12, 8));
        _glossarySearch.TextChanged += _ => RenderGlossaryList();
        left.AddChild(_glossarySearch);
        var listScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var listPanel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        listPanel.AddThemeStyleboxOverride("panel", Castle.Oak(12, 12));
        listPanel.AddChild(listScroll);
        left.AddChild(listPanel);
        _glossaryList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _glossaryList.AddThemeConstantOverride("separation", 1);
        listScroll.AddChild(_glossaryList);

        var page = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeStyleboxOverride("panel", Castle.Oak(30, 26));
        body.AddChild(page);
        var pageScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        page.AddChild(pageScroll);
        _glossaryPage = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _glossaryPage.AddThemeConstantOverride("separation", 12);
        pageScroll.AddChild(_glossaryPage);
    }

    private void OpenGlossary(string? id = null)
    {
        HideHover();
        _glossary.Visible = true;
        _glossary.MoveToFront();
        _glossarySearch.Text = "";
        _glossaryId = id ?? _glossaryId ?? Glossary.Default().Entries[0].Id;
        RenderGlossaryList();
        RenderGlossaryPage();
        GuideEvent("glossary");
    }

    private void CloseGlossary() => _glossary.Visible = false;

    private void ToggleGlossary()
    {
        if (_glossary.Visible) CloseGlossary();
        else OpenGlossary();
    }

    private void RenderGlossaryList()
    {
        foreach (var c in _glossaryList.GetChildren()) c.QueueFree();
        var book = Glossary.Default();
        string q = _glossarySearch.Text.Trim().ToLowerInvariant();
        bool Matches(GlossaryEntry e) => q == "" || e.Term.ToLowerInvariant().Contains(q) || e.Aliases.Any(a => a.Contains(q)) || e.Text.ToLowerInvariant().Contains(q);
        foreach (var cat in book.Categories)
        {
            var entries = book.Entries.Where(e => e.Category == cat.Id && Matches(e)).OrderBy(e => e.Term, StringComparer.OrdinalIgnoreCase).ToList();
            if (entries.Count == 0) continue;
            var m = new MarginContainer();
            m.AddThemeConstantOverride("margin_left", 10);
            m.AddThemeConstantOverride("margin_top", 10);
            m.AddChild(Ui.Label(cat.Name.ToUpperInvariant(), Ui.Ink2, Ui.Bold, 12));
            _glossaryList.AddChild(m);
            foreach (var e in entries)
            {
                var b = new Button { Text = e.Term, Alignment = HorizontalAlignment.Left, ToggleMode = true, ButtonPressed = e.Id == _glossaryId, FocusMode = FocusModeEnum.None, ThemeTypeVariation = "Tab" };
                b.AddThemeFontSizeOverride("font_size", 14);
                b.AddThemeStyleboxOverride("normal", Ui.Flat(Colors.Transparent, 10, 5, 6));
                b.AddThemeStyleboxOverride("hover", Ui.Flat(Ui.Panel2, 10, 5, 6));
                b.AddThemeStyleboxOverride("pressed", Ui.Flat(new Color(Ui.Gold, 0.18f), 10, 5, 6));
                b.AddThemeStyleboxOverride("hover_pressed", Ui.Flat(new Color(Ui.Gold, 0.24f), 10, 5, 6));
                // Entries without an icon keep its room, so the names line up.
                b.Icon = e.Icon != "" ? Ui.Icon(e.Icon) : Blank;
                b.ExpandIcon = false;
                b.AddThemeConstantOverride("icon_max_width", 20);
                string id = e.Id;
                b.Pressed += () =>
                {
                    _glossaryId = id;
                    foreach (var other in _glossaryList.GetChildren().OfType<Button>()) other.SetPressedNoSignal(other == b);
                    RenderGlossaryPage();
                };
                _glossaryList.AddChild(b);
            }
        }
        if (_glossaryList.GetChildCount() == 0) _glossaryList.AddChild(Ui.Label("  Nothing matches.", Ui.Ink2, Ui.Medium, 14));
    }

    private void RenderGlossaryPage()
    {
        foreach (var c in _glossaryPage.GetChildren()) c.QueueFree();
        if (_glossaryId == null || Ui.Entry(_glossaryId) is not { } e) return;
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 14);
        if (e.Icon != "")
            head.AddChild(new TextureRect { Texture = Ui.Icon(e.Icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(56, 56), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        var names = new VBoxContainer();
        names.AddThemeConstantOverride("separation", 0);
        names.AddChild(Ui.Label(e.Term, Ui.Ink, Ui.Display, 30));
        names.AddChild(Ui.Label(CategoryName(e.Category).ToUpperInvariant(), Ui.Ink2, Ui.Bold, 12));
        head.AddChild(names);
        _glossaryPage.AddChild(head);
        _glossaryPage.AddChild(new ColorRect { Color = Ui.Line, CustomMinimumSize = new Vector2(0, 1) });
        _glossaryPage.AddChild(Ui.Rich(e.Text, 17, skip: e.Id));
        var also = e.Aliases.Where(a => !a.Equals(e.Term, StringComparison.OrdinalIgnoreCase)).ToList();
        if (also.Count > 0) _glossaryPage.AddChild(Ui.Rich(Ui.C(Ui.Ink2, $"Also written: {Ui.Esc(string.Join(", ", also))}"), 13, skip: e.Id));

        // The entries whose text links here: where else this idea comes up.
        var re = Ui.KeywordPattern;
        var mentions = Glossary.Default().Entries
            .Where(o => o.Id != e.Id && re.Matches(o.Text).Any(m => Ui.EntryForWord(m.Value)?.Id == e.Id))
            .OrderBy(o => o.Term, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (mentions.Count > 0)
        {
            var m = new MarginContainer();
            m.AddThemeConstantOverride("margin_top", 10);
            m.AddChild(Ui.Label("MENTIONED IN", Ui.Ink2, Ui.Bold, 12));
            _glossaryPage.AddChild(m);
            var flow = new HFlowContainer();
            flow.AddThemeConstantOverride("h_separation", 6);
            flow.AddThemeConstantOverride("v_separation", 6);
            foreach (var o in mentions)
            {
                var b = new Button { Text = o.Term, FocusMode = FocusModeEnum.None };
                if (o.Icon != "")
                {
                    b.Icon = Ui.Icon(o.Icon);
                    b.ExpandIcon = false;
                    b.AddThemeConstantOverride("icon_max_width", 18);
                }
                string id = o.Id;
                b.Pressed += () => OpenGlossary(id);
                flow.AddChild(b);
            }
            _glossaryPage.AddChild(flow);
        }
    }
}
