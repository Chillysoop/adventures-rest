using System.Text.Json;
using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The tutorial: a card on the right that walks through content/game/tutorial.json one step at a time,
/// outlining what the step is about and moving on when the player does it. Which steps a player has
/// seen is kept across towns and builds (user://tutorial.json), so when a later build adds steps for
/// new features, a player who has done the tutorial gets just those, as "new in this build".
/// </summary>
public partial class Main
{
    private sealed class GuideState
    {
        public bool Active { get; set; }
        public List<string> Seen { get; set; } = [];
    }

    private static readonly TutorialBook Book = Tutorial.Default();
    private static string GuidePath => ProjectSettings.GlobalizePath("user://tutorial.json");

    private GuideState _guide = new();
    private bool _guideNew;
    private TutorialStep? _step;
    private int _stepBase;
    private double _stepDoneAt = -1, _nextGuideCheck;

    private PanelContainer _guideCard = null!;
    private Label _guideCount = null!, _guideTitle = null!;
    private RichTextLabel _guideText = null!;
    private Button _guideNext = null!;
    private Panel _outline = null!;
    private Control _speedsBox = null!, _menuButton = null!, _glossaryButton = null!, _dock = null!, _miniFrame = null!;

    private void BuildGuide()
    {
        _guide = ReadGuide();
        _outline = new Panel { MouseFilter = MouseFilterEnum.Ignore, Visible = false, ZIndex = 5 };
        var ring = Ui.Box(new Color(Ui.Gold, 0.06f), Ui.Gold, 12, 3);
        ring.ShadowColor = new Color(Ui.Gold, 0.45f);
        ring.ShadowSize = 10;
        _outline.AddThemeStyleboxOverride("panel", ring);
        AddChild(_outline);
        BuildGoalPointer();

        _guideCard = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(340, 0), ZIndex = 5 };
        _guideCard.AddThemeStyleboxOverride("panel", Castle.Oak(20, 16));
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        _guideCard.AddChild(v);
        _guideCount = Ui.Label("", Ui.Gold, Ui.Bold, 10);
        v.AddChild(_guideCount);
        _guideTitle = Ui.Label("", Ui.Ink, Ui.Display, 19);
        v.AddChild(_guideTitle);
        _guideText = Ui.Rich("", 14);
        v.AddChild(_guideText);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var end = new Button { Text = "End tutorial", TooltipText = "Stop the tips; start them again from the game menu", FocusMode = FocusModeEnum.None };
        end.AddThemeFontSizeOverride("font_size", 12);
        end.Pressed += EndTutorial;
        row.AddChild(end);
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _guideNext = new Button { Text = "Next", ThemeTypeVariation = "Post", FocusMode = FocusModeEnum.None };
        _guideNext.Pressed += () => GuideEvent("next", force: true);
        row.AddChild(_guideNext);
        v.AddChild(row);
        AddChild(_guideCard);
        _guideCard.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterRight, LayoutPresetMode.Minsize, Gap);
        _guideCard.GrowHorizontal = GrowDirection.Begin;
        _guideCard.GrowVertical = GrowDirection.Both;
    }

    private static GuideState ReadGuide()
    {
        try
        {
            return File.Exists(GuidePath) ? JsonSerializer.Deserialize<GuideState>(File.ReadAllText(GuidePath)) ?? new() : new();
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            return new();
        }
    }

    private void WriteGuide()
    {
        if (_opts.Screenshot != null) return;
        try
        {
            File.WriteAllText(GuidePath, JsonSerializer.Serialize(_guide));
        }
        catch (IOException e)
        {
            GD.PrintErr($"Tutorial progress: {e.Message}");
        }
    }

    /// <summary>Run the whole tutorial from the start, or from the step with this id.</summary>
    private void StartTutorial(string? from = null)
    {
        int at = from == null ? 0 : Math.Max(0, Book.Steps.FindIndex(s => s.Id == from));
        _guide = new GuideState { Active = true, Seen = Book.Steps.Take(at).Select(s => s.Id).ToList() };
        _guideNew = false;
        _step = null;
        WriteGuide();
    }

    /// <summary>On a plain start: someone who has done the tutorial before gets the steps added since.</summary>
    private void OfferNewSteps()
    {
        if (_guide.Active || _guide.Seen.Count == 0 || Book.Steps.All(s => _guide.Seen.Contains(s.Id))) return;
        _guide.Active = true;
        _guideNew = true;
        _step = null;
    }

    private void EndTutorial()
    {
        // Ended early counts as seen, so "new in this build" only ever shows what is really new.
        _guide = new GuideState { Seen = Book.Steps.Select(s => s.Id).ToList() };
        _step = null;
        WriteGuide();
    }

    /// <summary>The player did something; finish the step if it was waiting for that.</summary>
    private void GuideEvent(string what, bool force = false)
    {
        if (_step == null || _stepDoneAt >= 0) return;
        if (force || _step.Until == what || (_step.Until == "select" && what.StartsWith("select:", StringComparison.Ordinal))) StepDone();
    }

    private void StepDone()
    {
        if (_step == null) return;
        if (!_guide.Seen.Contains(_step.Id)) _guide.Seen.Add(_step.Id);
        WriteGuide();
        _stepDoneAt = _clockReal;
        _guideCount.Text = "✓  DONE";
        _guideNext.Disabled = true;
    }

    private bool StepReady(TutorialStep s)
    {
        var t = _sim.Town;
        if (s.When == "") return true;
        if (s.When.StartsWith("day:", StringComparison.Ordinal)) return int.TryParse(s.When[4..], out int d) && t.Day >= d;
        if (s.When.StartsWith("alert:", StringComparison.Ordinal)) return ShownAlerts.Any(a => a.Key.StartsWith(s.When[6..], StringComparison.Ordinal));
        return Tutorial.Measure(t, s.When) > 0;
    }

    /// <summary>Every frame: pick the step to show, watch for it to be done, and keep the outline on its target.</summary>
    private void SyncGuide()
    {
        bool blocked = _menu.Visible || _title.Visible || _endOverlay.Visible;
        if (_stepDoneAt >= 0 && _clockReal - _stepDoneAt > 0.9)
        {
            _stepDoneAt = -1;
            _step = null;
        }
        // Reading the town for steps is cheap but needn't happen every frame.
        bool check = _clockReal >= _nextGuideCheck;
        if (check) _nextGuideCheck = _clockReal + 0.25;
        if (_guide.Active && _step == null && !blocked && check)
        {
            var unseen = Book.Steps.Where(s => !_guide.Seen.Contains(s.Id)).ToList();
            if (unseen.Count == 0)
            {
                _guide.Active = false;
                WriteGuide();
            }
            else if (unseen.FirstOrDefault(StepReady) is TutorialStep next) ShowStep(next, unseen.Count);
        }
        if (check && _step != null && _stepDoneAt < 0 && Tutorial.Measure(_sim.Town, _step.Until) is int now && now != _stepBase) StepDone();
        _guideCard.Visible = _guide.Active && _step != null && !blocked;
        var target = _guideCard.Visible && _stepDoneAt < 0 ? GuideTarget(_step!.Point) : null;
        _outline.Visible = target != null;
        if (target == null) return;
        var r = target.GetGlobalRect().Grow(5);
        _outline.Position = r.Position;
        _outline.Size = r.Size;
        _outline.Modulate = new Color(1, 1, 1, 0.55f + 0.45f * (float)Math.Abs(Math.Sin(_clockReal * 2.6)));
    }

    private void ShowStep(TutorialStep s, int left)
    {
        _step = s;
        _stepBase = Tutorial.Measure(_sim.Town, s.Until) ?? 0;
        int i = Book.Steps.IndexOf(s);
        // The tour runs up to "done"; the steps after it are tips that wait for their moment, so they aren't counted.
        int tour = Book.Steps.FindIndex(x => x.Id == "done") is int d and >= 0 ? d + 1 : Book.Steps.Count;
        _guideCount.Text = _guideNew ? $"NEW IN THIS BUILD · {left} LEFT" : i < tour ? $"TUTORIAL · {i + 1} OF {tour}" : "TIP";
        _guideTitle.Text = s.Title;
        // A sentence a line, so a step can be skimmed.
        Ui.SetRich(_guideText, Ui.Points(s.Text));
        // A step whose action the player already took on their own ("count:Surveys" with a survey out) only
        // needs reading: it would otherwise wait for a second one and hold back every step after it.
        bool already = s.Until.StartsWith("count:", StringComparison.Ordinal) && _stepBase > 0;
        bool waits = s.Until != "next" && !already;
        _guideNext.Text = waits ? "Skip" : i == tour - 1 ? "Finish" : i >= tour ? "Got it" : "Next";
        _guideNext.ThemeTypeVariation = waits ? "" : "Post";
        _guideNext.Disabled = false;
    }

    // ---------- The goal pointer: the charter goal to work on next, and the button that does it ----------

    private Panel _goalRing = null!;
    private Label _goalArrow = null!;
    private Control? _goalTarget;
    private string _goalPoint = "";
    private double _nextGoalPoint;

    private void BuildGoalPointer()
    {
        _goalRing = new Panel { MouseFilter = MouseFilterEnum.Ignore, Visible = false, ZIndex = 5 };
        var ring = Ui.Box(new Color(Ui.Gold, 0.04f), Ui.Gold, 10, 2);
        ring.ShadowColor = new Color(Ui.Gold, 0.35f);
        ring.ShadowSize = 8;
        _goalRing.AddThemeStyleboxOverride("panel", ring);
        AddChild(_goalRing);
        _goalArrow = Ui.Label("▼", Ui.Gold, Ui.Bold, 22);
        _goalArrow.MouseFilter = MouseFilterEnum.Ignore;
        _goalArrow.Visible = false;
        _goalArrow.ZIndex = 5;
        _goalArrow.AddThemeConstantOverride("outline_size", 6);
        _goalArrow.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
        AddChild(_goalArrow);
    }

    /// <summary>
    /// Every frame, after the goals card: ring the button the next charter goal needs (the Build button, then the brush;
    /// a dock tab) with a bobbing arrow beside it. It gives way to the tutorial, whose own outline would be a second
    /// thing to look at, and to windows and menus.
    /// </summary>
    private void SyncGoalPointer(bool check)
    {
        bool tutorial = _guideCard.Visible || _outline.Visible;
        if (check || _goalTarget == null)
        {
            var next = _goalsCard.Visible && !tutorial ? CrownAndCompass.Sim.Systems.Goals.Next(_sim.Town) : null;
            _goalPoint = next?.Point
                ?? (_goalsCard.Visible && !tutorial && _sim.Town.Goals.Closed ? CrownAndCompass.Sim.Systems.Ahead.Now(_sim.Town).Aims.FirstOrDefault(a => a.Point != "")?.Point : null) ?? "";
            _goalTarget = _goalPoint == "" ? null : GuideTarget(_goalPoint);
        }
        var target = _goalsCard.Visible && !tutorial && _goalTarget != null && _goalTarget.IsVisibleInTree() ? _goalTarget : null;
        _goalRing.Visible = _goalArrow.Visible = target != null;
        if (target == null) return;
        var r = target.GetGlobalRect().Grow(4);
        _goalRing.Position = r.Position;
        _goalRing.Size = r.Size;
        float pulse = (float)Math.Abs(Math.Sin(_clockReal * 2.6));
        _goalRing.Modulate = new Color(1, 1, 1, 0.5f + 0.5f * pulse);
        float bob = 4 + 6 * pulse;
        var size = _goalArrow.GetMinimumSize();
        // Rail tabs sit at the left edge: point at them from the right. Anything else: from above, or below near the top.
        if (r.Position.X < RailWidth + Gap)
        {
            _goalArrow.Text = "◀";
            _goalArrow.Position = new Vector2(r.End.X + bob, r.GetCenter().Y - size.Y / 2);
        }
        else if (r.Position.Y > size.Y + 16)
        {
            _goalArrow.Text = "▼";
            _goalArrow.Position = new Vector2(r.GetCenter().X - size.X / 2, r.Position.Y - size.Y - bob);
        }
        else
        {
            _goalArrow.Text = "▲";
            _goalArrow.Position = new Vector2(r.GetCenter().X - size.X / 2, r.End.Y + bob);
        }
        _goalArrow.ResetSize();
    }

    /// <summary>
    /// Where a step about an alert points: the rail button (or the Events tray) holding the alert its "when" names,
    /// else the one holding the most pressing alert.
    /// </summary>
    private Control? AlertAnchor()
    {
        var shown = ShownAlerts.ToList();
        string? want = _step?.When is { } w && w.StartsWith("alert:", StringComparison.Ordinal) ? w[6..] : null;
        var a = (want != null ? shown.FirstOrDefault(x => x.Key.StartsWith(want, StringComparison.Ordinal)) : null)
            ?? shown.OrderByDescending(x => x.Kind.Level).ThenBy(x => x.Key, StringComparer.Ordinal).FirstOrDefault();
        if (a == null) return null;
        if (RailOf(a) is Tab tab) return TabAnchor(tab) ?? RailButtonOf(tab);
        return _trayButtons.TryGetValue("Events", out var events) ? events.Button : null;
    }

    /// <summary>
    /// A window ("Council") or one of its pages ("Council:Laws"): its rail button or tab until the window is open,
    /// then the page's button until that page is showing.
    /// </summary>
    private Control? PageAnchor(string spec)
    {
        var parts = spec.Split(':', 2);
        if (!Enum.TryParse<Tab>(parts[0], out var tab) || tab == Tab.Inspect) return null;
        var a = TabAnchor(tab);
        if (a != null || parts.Length < 2) return a;
        var pane = _panes[tab];
        if (pane.Selected == parts[1]) return null;
        return pane.Box.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.HasMeta("page") && (string)b.GetMeta("page") == parts[1]);
    }

    private Control? GuideTarget(string point)
    {
        Control? c = point switch
        {
            "speeds" => _speedsBox,
            "topbar" => _stats,
            "clock" => _clock,
            "threat" => _threatPill,
            "alerts" => AlertAnchor(),
            "feed" => _feed.GetChildCount() > 0 ? _feed : null,
            "build" => _buildBar.Visible ? null : _buildToggle,
            "buildbar" => _buildBar.Visible ? _buildBar : _buildToggle,
            "strategic" => _strategicButton,
            "lens" => _lensBar.Visible ? _lensBar : _lensButton,
            "minimap" => _miniFrame,
            "markers" => _markerBar,
            "menu" => _menuButton,
            "glossary" => _glossaryButton,
            "window" => _window,
            "inspector" => _inspector,
            "dock" or "rail" => _rail,
            "tray" => _trayColumn,
            _ when point.StartsWith("tray:", StringComparison.Ordinal) && _trayButtons.TryGetValue(point[5..], out var tb) => _trayOpen == point[5..] ? _drawer : tb.Button,
            "siege" => _siegeBanner,
            "goals" => _goalsCard,
            _ when point.StartsWith("zone:", StringComparison.Ordinal) || point.StartsWith("place:", StringComparison.Ordinal) => BuildTarget(point),
            _ when point.StartsWith("tab:", StringComparison.Ordinal) => PageAnchor(point[4..]),
            _ => null,
        };
        return c != null && c.IsVisibleInTree() ? c : null;
    }
}
