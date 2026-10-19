using CrownAndCompass.Client.Map;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The player's markers on the campaign map: a palette of kinds over the corner map, a list to fly to
/// any of them, and a small card to name, change or remove one. Clicking a marker opens its card,
/// dragging moves it, right-click or Delete removes it, and Shift-click places several in a row.
/// </summary>
public partial class Main
{
    private HBoxContainer _markerBar = null!;
    private readonly Dictionary<MarkerKind, Button> _markerButtons = [];
    private MenuButton _markerList = null!;
    private PanelContainer _markerCard = null!;
    private LineEdit _markerName = null!;
    private Label _markerTitle = null!;
    private readonly Dictionary<MarkerKind, Button> _markerKindButtons = [];
    private Marker? _editing;

    /// <summary>A marker's pictogram as a control, for buttons.</summary>
    private sealed partial class MarkerIcon(MarkerKind kind) : Control
    {
        public override void _Draw() => Overlay.MarkerGlyph(this, kind, Size / 2, 1, new Color("f6eedb"));
    }

    private static Button MarkerButton(MarkerKind k, string tip)
    {
        var b = new Button { ToggleMode = true, TooltipText = tip, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(34, 32) };
        var c = Overlay.MarkerColor(k);
        b.AddThemeStyleboxOverride("normal", Ui.Box(c.Darkened(0.45f), c.Darkened(0.1f), 8, 1, 6, 4));
        b.AddThemeStyleboxOverride("hover", Ui.Box(c.Darkened(0.25f), Ui.Ink, 8, 1, 6, 4));
        b.AddThemeStyleboxOverride("pressed", Ui.Box(c, Ui.Ink, 8, 2, 6, 4));
        b.AddThemeStyleboxOverride("hover_pressed", Ui.Box(c, Ui.Ink, 8, 2, 6, 4));
        var icon = new MarkerIcon(k) { MouseFilter = Control.MouseFilterEnum.Ignore };
        b.AddChild(icon);
        icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return b;
    }

    private Control BuildMarkerBar()
    {
        _markerBar = new HBoxContainer { Visible = false, Alignment = BoxContainer.AlignmentMode.End };
        _markerBar.AddThemeConstantOverride("separation", 4);
        foreach (var k in Overlay.MarkerKinds)
        {
            var b = MarkerButton(k, $"{Overlay.MarkerName(k)}: click the map to place it (Shift-click to place several)");
            b.Pressed += () =>
            {
                _map.Placing = b.ButtonPressed ? k : null;
                CloseMarker();
                SyncMarkers();
            };
            _markerBar.AddChild(b);
            _markerButtons[k] = b;
        }
        _markerList = new MenuButton { Text = "≡", TooltipText = "Every marker: pick one to look at it", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(34, 32), Flat = false };
        _markerList.AddThemeStyleboxOverride("normal", Ui.Box(new Color(0.09f, 0.075f, 0.055f, 0.96f), Ui.Line, 8, 1, 6, 4));
        _markerList.AddThemeStyleboxOverride("hover", Ui.Box(new Color(0.16f, 0.13f, 0.1f, 0.96f), Ui.Ink, 8, 1, 6, 4));
        _markerList.AboutToPopup += FillMarkerList;
        _markerList.GetPopup().IndexPressed += i =>
        {
            var marks = SortedMarkers();
            if (i < 0 || i >= marks.Count) return;
            _map.LookAt(marks[(int)i].P);
            OpenMarker(marks[(int)i]);
        };
        _markerBar.AddChild(_markerList);
        BuildMarkerCard();
        _map.MarkerOpened += OpenMarker;
        _map.Pressed += CloseMarker;
        return _markerBar;
    }

    /// <summary>Markers for the list: by kind in palette order, then by name, then by when they were placed.</summary>
    private List<Marker> SortedMarkers() => _map.Markers
        .Select((m, i) => (m, i))
        .OrderBy(x => Array.IndexOf(Overlay.MarkerKinds, x.m.Kind))
        .ThenBy(x => x.m.Label == "" ? 1 : 0)
        .ThenBy(x => x.m.Label, StringComparer.OrdinalIgnoreCase)
        .ThenBy(x => x.i)
        .Select(x => x.m)
        .ToList();

    private void FillMarkerList()
    {
        var popup = _markerList.GetPopup();
        popup.Clear();
        var marks = SortedMarkers();
        if (marks.Count == 0)
        {
            popup.AddItem("No markers yet: pick a kind and click the map");
            popup.SetItemDisabled(0, true);
            return;
        }
        foreach (var m in marks)
            popup.AddItem($"{Overlay.MarkerName(m.Kind)}{(m.Label == "" ? "" : $": {m.Label}")}   (day {m.Day})");
    }

    private void BuildMarkerCard()
    {
        _markerCard = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(268, 0) };
        _markerCard.AddThemeStyleboxOverride("panel", Castle.Oak(16, 14));
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        _markerCard.AddChild(box);
        _markerTitle = Ui.Label("Marker", Ui.Ink, Ui.Display, 15);
        box.AddChild(_markerTitle);
        _markerName = new LineEdit { PlaceholderText = "Name it (optional)", MaxLength = 40, ClearButtonEnabled = true };
        _markerName.TextChanged += t =>
        {
            if (_editing != null) _editing.Label = t.Trim();
        };
        _markerName.TextSubmitted += _ => CloseMarker();
        box.AddChild(_markerName);
        var kinds = new HBoxContainer();
        kinds.AddThemeConstantOverride("separation", 3);
        foreach (var k in Overlay.MarkerKinds)
        {
            var b = MarkerButton(k, Overlay.MarkerName(k));
            b.CustomMinimumSize = new Vector2(32, 30);
            b.Pressed += () =>
            {
                if (_editing == null) return;
                _editing.Kind = k;
                SyncMarkerCard();
            };
            kinds.AddChild(b);
            _markerKindButtons[k] = b;
        }
        box.AddChild(kinds);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var remove = new Button { Text = "Remove", FocusMode = Control.FocusModeEnum.None, TooltipText = "Take this marker off the map (Delete)" };
        remove.Pressed += () =>
        {
            if (_editing != null) _map.Markers.Remove(_editing);
            CloseMarker();
        };
        row.AddChild(remove);
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var done = new Button { Text = "Done", FocusMode = Control.FocusModeEnum.None };
        done.Pressed += CloseMarker;
        row.AddChild(done);
        box.AddChild(row);
        AddChild(_markerCard);
    }

    private void OpenMarker(Marker m)
    {
        _editing = m;
        _markerName.Text = m.Label;
        _markerCard.Visible = true;
        SyncMarkerCard();
        PlaceMarkerCard();
        _markerName.GrabFocus();
        _markerName.SelectAll();
    }

    private void CloseMarker()
    {
        if (_editing == null) return;
        _editing = null;
        _markerCard.Visible = false;
        _markerName.ReleaseFocus();
    }

    private void SyncMarkerCard()
    {
        if (_editing == null) return;
        _markerTitle.Text = $"{Overlay.MarkerName(_editing.Kind)} · placed day {_editing.Day}";
        _markerTitle.AddThemeColorOverride("font_color", Overlay.MarkerColor(_editing.Kind).Lightened(0.35f));
        foreach (var (k, b) in _markerKindButtons) b.SetPressedNoSignal(_editing.Kind == k);
    }

    /// <summary>Keep the card beside its marker as the camera moves, inside the screen.</summary>
    private void PlaceMarkerCard()
    {
        if (_editing == null) return;
        var at = _map.ToScreen(_editing.P);
        var size = _markerCard.GetCombinedMinimumSize();
        var pos = at + new Vector2(22, -size.Y / 2 - 22);
        if (pos.X + size.X > Size.X - Gap) pos.X = at.X - size.X - 22;
        pos = pos.Clamp(new Vector2(Gap, TopBar + Gap), Size - size - new Vector2(Gap, Gap + DockHeight + Gap));
        _markerCard.Position = pos;
        _markerCard.Size = size;
    }

    private void ToggleStrategic()
    {
        _map.ToggleStrategic();
        _strategicButton.ButtonPressed = _map.Strategic;
        _markerBar.Visible = _map.Strategic;
        if (!_map.Strategic)
        {
            _map.ClearMarkerTools();
            CloseMarker();
        }
        _help.Text = _map.Strategic ? StrategicHelp : MapHelp;
        SyncMarkers();
        if (_map.Strategic) GuideEvent("strategic");
    }

    private const string MapHelp = "Scroll to zoom · drag to pan · Q/E rotate · M campaign map · double right-click to recenter";
    private const string StrategicHelp = "Pick a marker kind and click to place it · click a marker to name it · drag to move · right-click or Delete removes";

    /// <summary>Every frame: the palette shows the tool in hand, and the card follows its marker or closes with it.</summary>
    private void SyncMarkers()
    {
        foreach (var (k, b) in _markerButtons) b.SetPressedNoSignal(_map.Placing == k);
        if (_editing != null && (!_map.Strategic || !_map.Markers.Contains(_editing))) CloseMarker();
        PlaceMarkerCard();
    }

    /// <summary>Marker keys: Escape puts the tool or the card down, Delete removes the marker under the cursor.</summary>
    private bool MarkerKey(Key key)
    {
        if (key == Key.Escape && _editing != null) CloseMarker();
        else if (key == Key.Escape && _map.Placing != null) _map.Placing = null;
        else if (key == Key.Delete && _map.Strategic && (_map.HoverMarker ?? _editing) is Marker m)
        {
            _map.Markers.Remove(m);
            CloseMarker();
        }
        else return false;
        return true;
    }
}
