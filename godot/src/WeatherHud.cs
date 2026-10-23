using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The sky over the town on the top bar: an icon and the heat beside the clock. Hovering it shows the weather now, the
/// wind and the days ahead the town can read (<see cref="Weather.Forecast"/>), and when a storm is coming. A storm
/// breaking over the town stops the clock (clock stop "storm", off unless the player turns it on).
/// </summary>
public partial class Main
{
    private HBoxContainer _weatherChip = null!;
    private TextureRect _weatherIcon = null!;
    private Label _weatherHeat = null!;
    private PanelContainer _weatherTip = null!;
    private RichTextLabel _weatherTipText = null!;
    private string _weatherShown = "";
    private int _weatherStep = -1;

    private Button _weatherButton = null!;

    /// <summary>The weather layer's toggle by the corner map: off by default, so the map stays clean.</summary>
    private Button BuildWeatherButton()
    {
        _weatherButton = new Button { Icon = Ui.Icon("weather-layer"), ToggleMode = true, TooltipText = "Weather layer: cloud, rain, storms and wind over the campaign map, where the town can see", FocusMode = FocusModeEnum.None, ExpandIcon = true, CustomMinimumSize = new Vector2(34, 0) };
        _weatherButton.Toggled += on =>
        {
            _map.WeatherLayer = on;
            if (on) GuideEvent("weather-layer");
        };
        return _weatherButton;
    }

    private Control BuildWeatherChip()
    {
        _weatherChip = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.Help };
        _weatherChip.AddThemeConstantOverride("separation", 3);
        _weatherIcon = Ui.IconRect("weather-clear", 22);
        _weatherChip.AddChild(_weatherIcon);
        _weatherHeat = Ui.Label("", Ui.Ink, Ui.Bold, 14);
        _weatherHeat.MouseFilter = MouseFilterEnum.Ignore;
        _weatherChip.AddChild(_weatherHeat);
        _weatherChip.MouseEntered += ShowWeatherTip;
        _weatherChip.MouseExited += () => _weatherTip.Visible = false;

        _weatherTip = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 60, CustomMinimumSize = new Vector2(250, 0) };
        _weatherTip.AddThemeStyleboxOverride("panel", Castle.Oak(14, 12));
        _weatherTipText = Ui.Rich("", 13);
        _weatherTipText.MouseFilter = MouseFilterEnum.Ignore;
        _weatherTipText.CustomMinimumSize = new Vector2(226, 0);
        _weatherTip.AddChild(_weatherTipText);
        AddChild(_weatherTip);
        return _weatherChip;
    }

    private static string Degrees(double heat) => $"{Math.Round(heat):0}°";

    /// <summary>The colour of a step: calm ink, the grey and foul days dimmer, storms gold and the great storm red.</summary>
    private static Color StepColor(int step) => step switch
    {
        >= Weather.Tempest => Ui.Bad,
        Weather.Storm => Ui.Gold,
        _ => Ui.Ink,
    };

    /// <summary>On every header render: the chip follows the sky, and a storm breaking over the town may stop the clock.</summary>
    private void RenderWeather(Town t)
    {
        var w = Weather.Here(t);
        var kind = Weather.Kind(t, w.Kind);
        string key = $"{kind.Id}:{Math.Round(w.Heat)}";
        if (key != _weatherShown)
        {
            _weatherShown = key;
            _weatherIcon.Texture = Ui.Icon(kind.Icon);
            _weatherHeat.Text = Degrees(w.Heat);
            _weatherHeat.AddThemeColorOverride("font_color", StepColor(w.Step));
        }
        if (_weatherStep >= 0 && w.Step >= Weather.Storm && _weatherStep < Weather.Storm) StopClock("storm");
        _weatherStep = w.Step;
        if (_weatherTip.Visible) FillWeatherTip();
    }

    private void ShowWeatherTip()
    {
        FillWeatherTip();
        _weatherTip.Visible = true;
        _weatherTip.MoveToFront();
        GuideEvent("weather");
    }

    private void FillWeatherTip()
    {
        var t = _sim.Town;
        var w = Weather.Here(t);
        var kind = Weather.Kind(t, w.Kind);
        var sb = new System.Text.StringBuilder();
        sb.Append(Ui.Head(kind.Name, kind.Icon, StepColor(w.Step)));
        sb.Append('\n').Append(Ui.Dim(kind.Text));
        if (w.By != "") sb.Append('\n').Append(Ui.Bullet(Ui.C(Ui.Bad, $"Worked by {w.By}"), "ui-warn"));
        sb.Append('\n').Append(Ui.Chips(
            Ui.Chip("weather-hot", Degrees(w.Heat), null, "Heat"),
            Ui.Chip("weather-wind", w.Wind < 0.12 ? "still" : $"from {Weather.FromWord(w.WindBearing)}", w.Wind >= Weather.Rules(t).Gale ? Ui.Gold : null, w.Wind >= Weather.Rules(t).Gale ? "A gale" : w.Wind >= 0.5 ? "A strong wind" : "A breeze")));
        var (snow, wet) = Weather.Ground(t, 0, 0, t.Day);
        if (snow >= 0.3) sb.Append('\n').Append(Ui.Bullet("Snow on the roads: marches slower", "weather-snow"));
        else if (wet >= 0.6) sb.Append('\n').Append(Ui.Bullet("Wet ground, rivers high", "weather-rain"));
        if (Weather.StormIn(t) is int hours && w.Step < Weather.Storm)
        {
            var ahead = Weather.Here(t, t.Tick + hours);
            var coming = Weather.Kind(t, ahead.Kind);
            string from = ahead.By != "" ? $", sent by {ahead.By}" : "";
            sb.Append('\n').Append(Ui.Bullet(Ui.C(Ui.Gold, $"{coming.Name} in {(hours < 24 ? $"{Math.Max(1, hours)}h" : Ui.Days((hours + 12) / 24))}{from}"), "ui-warn"));
        }
        var days = Weather.Forecast(t);
        sb.Append($"\n\n{Ui.Head("Days ahead", "ui-day", Ui.KeywordColor, 13)}");
        string[] names = ["Today", "Tomorrow"];
        for (int i = 0; i < days.Count; i++)
        {
            var d = days[i];
            var k = Weather.Kind(t, d.Kind);
            string label = i < names.Length ? names[i] : $"Day {t.Day + i}";
            sb.Append('\n').Append(Ui.Bullet($"{label}  {Ui.Chip(k.Icon, k.Name, StepColor(d.Step), k.Text, 14)}"));
        }
        sb.Append('\n').Append(Ui.Dim(Weather.Lead(t) > Weather.Rules(t).Lead ? "A druid or ranger reads the sky" : "A druid or ranger at home reads a day further"));
        Ui.SetRich(_weatherTipText, sb.ToString());
        _weatherTip.ResetSize();
        var chip = _weatherChip.GetGlobalRect();
        float x = Mathf.Clamp(chip.Position.X - 8, Gap, GetViewportRect().Size.X - _weatherTip.Size.X - Gap);
        _weatherTip.Position = new Vector2(x, TopBar + 4);
    }
}
