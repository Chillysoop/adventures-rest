using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The whole campaign at a glance, at the top of the Chronicles window (<see cref="Records"/>): pick a line (people,
/// gold, dread...) and see it from day one, with the big moments pinned on it. Hover a pin for what happened.
/// </summary>
public partial class Main
{
    private string _recordSeries = "people";

    /// <summary>Each pin kind's icon and colour.</summary>
    public static (string Icon, Color Color) PinLook(string kind) => kind switch
    {
        "siege" => ("warband", Ui.Bad),
        "charter" => ("decree", Ui.Gold),
        "fallen" => ("ui-skull", Ui.Ink2),
        "omen" => ("omen", new Color("a68ad8")),
        "sickness" => ("sickness", new Color("9cb85a")),
        "dark" => ("dark-company", new Color("a68ad8")),
        "victory" => ("ui-star", Ui.Good),
        _ => ("ui-info", Ui.Ink2),
    };

    private void RenderRecords(Pane p, Town t)
    {
        var weeks = t.Records.Weeks;
        if (weeks.Count < 2) return;
        p.Heading("The campaign", "records");
        var buttons = Records.Series.Select(s => (s.Label, true, s.Id == _recordSeries, (Action)(() => Act(() => _recordSeries = s.Id)))).ToArray();
        p.Buttons(buttons[..5]);
        p.Buttons(buttons[5..]);
        var series = Records.Series.FirstOrDefault(s => s.Id == _recordSeries);
        if (series.Of == null) series = Records.Series[0];
        var values = weeks.Select(w => (float)series.Of(w)).ToList();
        var days = weeks.Select(w => w.Day).ToList();
        var pins = t.Records.Pins.Select(x => (x.Day, x.Kind, x.Text)).ToList();
        string sig = $"rec{series.Id}{weeks.Count}:{pins.Count}";
        p.Add(sig, () => new RecordsChart(values, days, pins) { CustomMinimumSize = new Vector2(0, 180) });
        float now = values[^1], peak = values.Max();
        p.Text(Ui.Chips(Ui.Chip("ui-day", $"{Math.Round(now):N0}", Ui.Ink, "Now"), Ui.Chip("ui-up", $"{Math.Round(peak):N0}", Ui.Gold, "Highest"),
            Ui.Chip("warband", $"{pins.Count(x => x.Kind == "siege")}", Ui.Bad, "Sieges"), Ui.Chip("ui-skull", $"{pins.Count(x => x.Kind == "fallen")}", Ui.Ink2, "Heroes fallen")), 13);
    }
}

/// <summary>A campaign line over every week so far, with pins standing on it for its big moments.</summary>
public partial class RecordsChart(List<float> values, List<int> days, List<(int Day, string Kind, string Text)> pins) : Control
{
    private const float Left = 8, Bottom = 18, Top = 22;

    private float X(int day) => Left + (Size.X - Left * 2) * (days[^1] == days[0] ? 1 : (float)(day - days[0]) / (days[^1] - days[0]));

    public override void _Draw()
    {
        var size = Size;
        float lo = Math.Min(0, values.Min()), hi = values.Max();
        if (hi - lo < 1e-3f) hi = lo + 1;
        float h = size.Y - Bottom - Top;
        float Y(float v) => Top + h * (1 - (v - lo) / (hi - lo));
        DrawRect(new Rect2(0, 0, size), new Color(Ui.Panel, 0.6f));
        DrawLine(new Vector2(Left, size.Y - Bottom), new Vector2(size.X - Left, size.Y - Bottom), new Color(Ui.Line, 0.9f), 1);
        var pts = new Vector2[values.Count];
        for (int i = 0; i < values.Count; i++) pts[i] = new Vector2(X(days[i]), Y(values[i]));
        // The area under the line, then the line.
        var area = new Vector2[pts.Length + 2];
        pts.CopyTo(area, 0);
        area[^2] = new Vector2(pts[^1].X, size.Y - Bottom);
        area[^1] = new Vector2(pts[0].X, size.Y - Bottom);
        DrawColoredPolygon(area, new Color(Ui.Gold, 0.12f));
        DrawPolyline(pts, Ui.Gold, 2, true);
        foreach (var (day, kind, _) in pins)
        {
            var (_, color) = Main.PinLook(kind);
            float x = X(day);
            DrawLine(new Vector2(x, 6), new Vector2(x, size.Y - Bottom), new Color(color, 0.35f), 1);
            DrawCircle(new Vector2(x, 8), 4, color);
        }
        var font = Ui.Body;
        DrawString(font, new Vector2(Left, size.Y - 4), $"Day {days[0]}", HorizontalAlignment.Left, -1, 11, Ui.Ink2);
        DrawString(font, new Vector2(size.X - Left - 60, size.Y - 4), $"Day {days[^1]}", HorizontalAlignment.Right, 60, 11, Ui.Ink2);
    }

    public override string _GetTooltip(Vector2 at)
    {
        var near = pins.Where(x => Math.Abs(X(x.Day) - at.X) <= 5).Select(x => $"Day {x.Day}: {Pin(x.Kind)} {x.Text}").Take(4).ToList();
        if (near.Count > 0) return string.Join("\n", near);
        if (values.Count == 0) return "";
        int i = 0;
        for (int k = 1; k < days.Count; k++) if (Math.Abs(X(days[k]) - at.X) < Math.Abs(X(days[i]) - at.X)) i = k;
        return $"Day {days[i]}: {Math.Round(values[i]):N0}";
    }

    private static string Pin(string kind) => kind switch
    {
        "siege" => "siege by",
        "charter" => "charter of a",
        "fallen" => "fell:",
        "omen" => "omen:",
        "sickness" => "sickness at",
        "dark" => "the dark company rides:",
        "victory" => "broke",
        _ => "",
    };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        TooltipText = " ";
    }
}
