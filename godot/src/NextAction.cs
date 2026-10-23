using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The Next button beside Build (N), after Civilization: one click goes to the next thing that needs the mayor, by the
/// same jump and glow trail as the goals card (<see cref="GoTo"/>, <see cref="Go(Alert)"/>). Urgent alerts first, then
/// decisions and the stewards' asks, then the charter goals or the what-next lines, then minor alerts; each click steps on through the queue.
/// </summary>
public partial class Main
{
    private Button _nextButton = null!;
    private string _nextLast = "";

    /// <summary>The button and its count, for the dock beside Build.</summary>
    private Control BuildNextButton()
    {
        _nextButton = DockButton("Next", "ui-up", "The next thing that needs you (N)");
        _nextButton.ToggleMode = false;
        _nextButton.Pressed += PressNext;
        return _nextButton;
    }

    /// <summary>Everything waiting on the mayor, most pressing first: its key, its words and where a click takes them.</summary>
    private List<(string Key, string Text, AlertLevel Level, Action Go)> NextQueue()
    {
        var t = _sim.Town;
        var q = new List<(string Key, string Text, AlertLevel Level, Action Go)>();
        var alerts = ShownAlerts.OrderByDescending(a => a.Kind.Level).ThenBy(a => a.Key, StringComparer.Ordinal).ToList();
        foreach (var a in alerts.Where(a => a.Kind.Level > AlertLevel.Minor)) q.Add(($"alert:{a.Key}", a.Text, a.Kind.Level, () => Go(a)));
        foreach (var a in t.Stewards.Asks.Take(AsksShown))
        {
            int id = a.Id;
            q.Add(($"ask:{id}", a.Title, AlertLevel.Decision, () =>
            {
                _askOpen = id;
                SyncAsks();
            }));
        }
        foreach (var g in Goals.Open(t).Where(g => g.Goes != "")) q.Add(($"goal:{g.Id}", g.Text, AlertLevel.Minor, () => GoTo(g.Goes, $"goal:{g.Id}")));
        foreach (var a in Ahead.Now(t).Aims.Where(a => a.Goes != "")) q.Add(($"aim:{a.Text}", a.Text, AlertLevel.Minor, () => GoTo(a.Goes, "aim")));
        foreach (var a in alerts.Where(a => a.Kind.Level == AlertLevel.Minor)) q.Add(($"alert:{a.Key}", a.Text, a.Kind.Level, () => Go(a)));
        return q;
    }

    /// <summary>Go to the thing after the one this button went to last (the first once that one is dealt with).</summary>
    private void PressNext()
    {
        var q = NextQueue();
        if (q.Count == 0)
        {
            Toast("Nothing needs you right now");
            return;
        }
        int i = q.FindIndex(x => x.Key == _nextLast);
        var pick = q[(i + 1) % q.Count];
        _nextLast = pick.Key;
        pick.Go();
        _dirty = true;
    }

    /// <summary>A few times a second: the count, the colour of the most pressing thing, and what a click does now.</summary>
    private void SyncNextButton()
    {
        var q = NextQueue();
        _nextButton.Text = q.Count > 0 ? $"Next · {q.Count}" : "Next";
        var top = q.Count > 0 ? q.Max(x => x.Level) : AlertLevel.Minor;
        var tint = q.Count == 0 ? Ui.Ink2 : top == AlertLevel.Urgent ? Ui.Bad : Ui.Gold;
        _nextButton.AddThemeColorOverride("icon_normal_color", tint);
        foreach (var c in new[] { "font_color", "font_hover_color" }) _nextButton.AddThemeColorOverride(c, q.Count == 0 ? Ui.Ink2 : tint);
        int i = q.FindIndex(x => x.Key == _nextLast);
        _nextButton.TooltipText = q.Count == 0 ? "Nothing needs you right now" : $"Next (N): {q[(i + 1) % q.Count].Text}";
    }
}
