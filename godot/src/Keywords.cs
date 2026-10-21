using CrownAndCompass.Sim;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The glossary's cards: hover a keyword anywhere (a gold word in rich text, a figure on the top bar, a meter) for a
/// card explaining it; click it to pin the card, which stays until closed and can be dragged aside. Keywords inside a
/// pinned card work the same way, so one card leads to the next.
/// </summary>
public partial class Main : IKeywordCards
{
    private const float CardWidth = 330;
    private const int MaxPinned = 6;

    private CanvasLayer _kwLayer = null!;
    private Control _kwRoot = null!;
    private PanelContainer? _kwHover;
    private string? _kwHoverId;
    private double _kwHideIn = -1;
    private readonly List<PanelContainer> _kwPinned = [];

    private void BuildKeywords()
    {
        // Above every window, menu and screen.
        _kwLayer = new CanvasLayer { Layer = 20 };
        AddChild(_kwLayer);
        _kwRoot = new Control { MouseFilter = MouseFilterEnum.Ignore, Theme = Theme };
        _kwLayer.AddChild(_kwRoot);
        _kwRoot.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Ui.Keywords = this;
    }

    public void Hover(string id, Control from)
    {
        // A keyword inside the hover card itself is for clicking: the card stays put.
        if (_kwHover != null && _kwHover.IsAncestorOf(from)) return;
        ShowHover(id, null);
    }

    public void Show(string id, string? extra) => ShowHover(id, extra);

    private void ShowHover(string id, string? extra)
    {
        if (Ui.Entry(id) is not { } e) return;
        _kwHideIn = -1;
        if (_kwHoverId == id && _kwHover != null) return;
        _kwHover?.QueueFree();
        _kwHover = KeywordCard(e, pinned: false, extra);
        _kwHoverId = id;
        _kwRoot.AddChild(_kwHover);
        PlaceCard(_kwHover, GetViewport().GetMousePosition() + new Vector2(14, 18));
    }

    public void Unhover() => _kwHideIn = 0.18;

    public void Pin(string id) => Pin(id, null);

    public void Pin(string id, string? extra)
    {
        if (Ui.Entry(id) is not { } e) return;
        var at = _kwHover != null && _kwHoverId == id ? _kwHover.Position : GetViewport().GetMousePosition() + new Vector2(14, 18);
        HideHover();
        // Already pinned: bring it forward rather than pin it twice.
        if (_kwPinned.FirstOrDefault(p => (string)p.GetMeta("kw") == id) is { } open)
        {
            open.MoveToFront();
            return;
        }
        var card = KeywordCard(e, pinned: true, extra);
        _kwRoot.AddChild(card);
        PlaceCard(card, at);
        _kwPinned.Add(card);
        while (_kwPinned.Count > MaxPinned) Unpin(_kwPinned[0]);
        GuideEvent("pin");
    }

    private void Unpin(PanelContainer card)
    {
        _kwPinned.Remove(card);
        card.QueueFree();
    }

    private void UnpinAll()
    {
        foreach (var c in _kwPinned.ToList()) Unpin(c);
        HideHover();
    }

    private void HideHover()
    {
        _kwHover?.QueueFree();
        _kwHover = null;
        _kwHoverId = null;
        _kwHideIn = -1;
    }

    /// <summary>Let the hover card go once the mouse has left both the word and the card.</summary>
    private void SyncKeywords(double delta)
    {
        if (_kwHideIn < 0 || _kwHover == null) return;
        if (_kwHover.GetGlobalRect().HasPoint(GetViewport().GetMousePosition()))
        {
            _kwHideIn = 0.18;
            return;
        }
        _kwHideIn -= delta;
        if (_kwHideIn <= 0) HideHover();
    }

    private void PlaceCard(Control card, Vector2 at)
    {
        card.ResetSize();
        card.Position = at;
        // Once its text has wrapped, shrink it to fit and keep it on screen.
        Callable.From(() => FitCard(card)).CallDeferred();
        FitCard(card);
    }

    private void FitCard(Control card)
    {
        if (!IsInstanceValid(card)) return;
        card.Size = new Vector2(CardWidth, 0);
        card.ResetSize();
        var at = card.Position;
        var vp = GetViewportRect().Size;
        var size = card.Size;
        card.Position = new Vector2(Mathf.Clamp(at.X, 8, Math.Max(8, vp.X - size.X - 8)), Mathf.Clamp(at.Y, 8, Math.Max(8, vp.Y - size.Y - 8)));
    }

    private PanelContainer KeywordCard(GlossaryEntry e, bool pinned, string? extra)
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(CardWidth, 0), MouseFilter = MouseFilterEnum.Stop };
        card.SetMeta("kw", e.Id);
        card.AddThemeStyleboxOverride("panel", Castle.Oak(18, 14));
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 6);
        card.AddChild(v);

        var head = new HBoxContainer { MouseDefaultCursorShape = pinned ? CursorShape.Move : CursorShape.Arrow };
        head.AddThemeConstantOverride("separation", 8);
        if (e.Icon != "")
            head.AddChild(new TextureRect { Texture = Ui.Icon(e.Icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(26, 26), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
        var names = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        names.AddThemeConstantOverride("separation", -2);
        names.AddChild(Ui.Label(e.Term, Ui.Ink, Ui.Display, 17));
        names.AddChild(Ui.Label(CategoryName(e.Category).ToUpperInvariant(), Ui.Ink2, Ui.Bold, 11));
        foreach (var n in names.GetChildren().OfType<Control>()) n.MouseFilter = MouseFilterEnum.Ignore;
        head.AddChild(names);
        if (pinned)
        {
            var x = new Button { Text = "✕", ThemeTypeVariation = "Speed", TooltipText = "Unpin (or right-click the card)", FocusMode = FocusModeEnum.None, SizeFlagsVertical = SizeFlags.ShrinkBegin };
            x.Pressed += () => Unpin(card);
            head.AddChild(x);
            head.GuiInput += ev =>
            {
                if (ev is InputEventMouseMotion mm && (mm.ButtonMask & MouseButtonMask.Left) != 0)
                {
                    var vp = GetViewportRect().Size;
                    card.Position = (card.Position + mm.Relative).Clamp(new Vector2(-card.Size.X + 60, 0), vp - new Vector2(60, 30));
                }
            };
            card.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) Unpin(card);
                else if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) card.MoveToFront();
            };
        }
        v.AddChild(head);
        // A pinned card is marked by a gold rule under its name.
        v.AddChild(new ColorRect { Color = pinned ? Ui.Gold : Ui.Line, CustomMinimumSize = new Vector2(0, pinned ? 2 : 1), MouseFilter = MouseFilterEnum.Ignore });

        // Rich text that fits its content needs its width up front, or the card sizes itself to a one-word column.
        float inner = CardWidth - 36;
        var text = Ui.Rich(Ui.Points(e.Text), 14, skip: e.Id);
        text.CustomMinimumSize = new Vector2(inner, 0);
        v.AddChild(text);
        if (extra is { Length: > 0 })
        {
            var ex = Ui.Rich(Ui.C(Ui.Gold, extra), 13);
            ex.CustomMinimumSize = new Vector2(inner, 0);
            v.AddChild(ex);
        }

        var foot = new HBoxContainer();
        foot.AddThemeConstantOverride("separation", 8);
        if (pinned)
        {
            var more = new Button { Text = "Open in the glossary", FocusMode = FocusModeEnum.None };
            more.AddThemeFontSizeOverride("font_size", 12);
            more.Pressed += () => OpenGlossary(e.Id);
            foot.AddChild(more);
        }
        else foot.AddChild(Ui.Label("Click to pin this card", Ui.Ink2, Ui.Medium, 12));
        v.AddChild(foot);
        return card;
    }

    private static string CategoryName(string id) => Glossary.Default().Categories.FirstOrDefault(c => c.Id == id)?.Name ?? id;
}
