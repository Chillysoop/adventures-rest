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
    private Control _speedsBox = null!, _menuButton = null!, _dock = null!, _miniFrame = null!;

    private void BuildGuide()
    {
        _guide = ReadGuide();
        _outline = new Panel { MouseFilter = MouseFilterEnum.Ignore, Visible = false, ZIndex = 5 };
        var ring = Ui.Box(new Color(Ui.Gold, 0.06f), Ui.Gold, 12, 3);
        ring.ShadowColor = new Color(Ui.Gold, 0.45f);
        ring.ShadowSize = 10;
        _outline.AddThemeStyleboxOverride("panel", ring);
        AddChild(_outline);

        _guideCard = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(340, 0), ZIndex = 5 };
        var box = Glass(12, 18, 14);
        box.BorderColor = Ui.Gold;
        box.BorderWidthLeft = 3;
        _guideCard.AddThemeStyleboxOverride("panel", box);
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
        if (s.When.StartsWith("alert:", StringComparison.Ordinal)) return _shownAlerts.Any(a => a.Key.StartsWith(s.When[6..], StringComparison.Ordinal));
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
        _guideCount.Text = _guideNew ? $"NEW IN THIS BUILD · {left} LEFT" : $"TUTORIAL · {i + 1} OF {Book.Steps.Count}";
        _guideTitle.Text = s.Title;
        _guideText.Text = s.Text;
        bool waits = s.Until != "next";
        _guideNext.Text = waits ? "Skip" : i == Book.Steps.Count - 1 ? "Finish" : "Next";
        _guideNext.ThemeTypeVariation = waits ? "" : "Post";
        _guideNext.Disabled = false;
    }

    private Control? GuideTarget(string point)
    {
        Control? c = point switch
        {
            "speeds" => _speedsBox,
            "topbar" => _stats,
            "clock" => _clock,
            "threat" => _threatPill,
            "alerts" => _alertRow.GetChildCount() > 0 ? _alertRow : null,
            "feed" => _feed.GetChildCount() > 0 ? _feed : null,
            "build" => _buildBar.Visible ? null : _buildToggle,
            "buildbar" => _buildBar.Visible ? _buildBar : _buildToggle,
            "strategic" => _strategicButton,
            "minimap" => _miniFrame,
            "markers" => _markerBar,
            "menu" => _menuButton,
            "window" => _window,
            "inspector" => _inspector,
            "dock" => _dock,
            _ when point.StartsWith("tab:", StringComparison.Ordinal) && Enum.TryParse<Tab>(point[4..], out var tab) && _tabButtons.TryGetValue(tab, out var b) => _window.Visible && _windowTab == tab ? null : b,
            _ => null,
        };
        return c != null && c.IsVisibleInTree() ? c : null;
    }
}
