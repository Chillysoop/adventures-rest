using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The siege on screen: a banner under the alert strip while one plays out on the walls ("Siege at the north wall ·
/// wave 2 of 3"), which takes the camera there when clicked. The camera goes there by itself when a siege begins in
/// play, and from its news in the feed or the Ledger. The playing itself is the map's (<see cref="Map.Crowd"/>).
/// </summary>
public partial class Main
{
    private Button _siegeBanner = null!;
    private (int Id, BattlePhase Phase, int Wave) _siegeShown = (-1, BattlePhase.March, -1);

    /// <summary>A siege that began since the last frame, to look at once the frame shows it playing.</summary>
    private int? _siegeJump;

    private void BuildSiegeBanner(VBoxContainer col)
    {
        _siegeBanner = new Button { Visible = false, SizeFlagsHorizontal = SizeFlags.ShrinkCenter, FocusMode = FocusModeEnum.None, TooltipText = "Look at the siege" };
        foreach (var state in new[] { "normal", "hover", "pressed" })
            _siegeBanner.AddThemeStyleboxOverride(state, Ui.Flat(new Color(0.42f, 0.08f, 0.05f, state == "hover" ? 0.95f : 0.85f), 16, 5, 99));
        _siegeBanner.AddThemeFontOverride("font", Ui.Bold);
        _siegeBanner.AddThemeFontSizeOverride("font_size", 14);
        _siegeBanner.AddThemeColorOverride("font_color", new Color("f6eedb"));
        _siegeBanner.AddThemeColorOverride("font_hover_color", new Color("ffffff"));
        _siegeBanner.Pressed += () =>
        {
            if (Battles.Playing(_sim.Town, _sim.Town.Tick + _map.HourFraction) is { } b) ShowSiege(b);
        };
        col.AddChild(_siegeBanner);
    }

    /// <summary>Keep the banner on the siege playing now; the text is rebuilt only when the siege moves on a stage.</summary>
    private void SyncSiege()
    {
        var t = _sim.Town;
        var b = Battles.Playing(t, t.Tick + _map.HourFraction);
        _siegeBanner.Visible = b != null && !_title.Visible;
        if (b == null)
        {
            _siegeJump = null;
            return;
        }
        var (phase, wave, _) = Battles.At(t, b, t.Tick + _map.HourFraction);
        if (_siegeShown != (b.Id, phase, wave))
        {
            _siegeShown = (b.Id, phase, wave);
            _siegeBanner.Text = $"⚔  {Battles.Headline(t, b, t.Tick + _map.HourFraction)}";
        }
        if (_siegeJump == b.Id)
        {
            _siegeJump = null;
            ShowSiege(b);
        }
    }

    /// <summary>A siege's news, written as it begins: the camera goes to the walls once the map is playing it.</summary>
    private void SiegeNews(LedgerEntry e)
    {
        if (e.Category == LedgerCategory.War && e.Importance == Importance.Major && SiegeOf(e) is { } b) _siegeJump = b.Id;
    }

    /// <summary>The siege a Ledger entry reports, if it is one the town still remembers.</summary>
    private Battle? SiegeOf(LedgerEntry e) =>
        e.Category == LedgerCategory.War ? _sim.Town.Battles.LastOrDefault(b => b.Tick == e.Tick) : null;

    /// <summary>Look at where a siege is now: the stretch of wall under attack, the heroes' fight with the leader, or the breach.</summary>
    private void ShowSiege(Battle b)
    {
        var (at, side) = _map.Crowd.SiegeFocus(_sim.Town, b);
        Look(at, _opts.Cam ?? 16);
        // From outside the wall, the host before it and the town behind.
        _map.Face(side);
        Select(null);
    }

    /// <summary>With --siege H: run on to the town's next siege and stop H hours into it, the camera on the walls.</summary>
    private void SiegeShot(float hours)
    {
        var t = _sim.Town;
        int before = t.Battles.Count > 0 ? t.Battles[^1].Id : 0;
        for (int h = 0; h < 600 * SimTime.HoursPerDay && (t.Battles.Count == 0 || t.Battles[^1].Id == before) && t.Outcome == null; h++) _sim.Step();
        if (t.Battles.Count == 0 || t.Battles[^1].Id == before) return;
        for (int h = 0; h < (int)hours; h++) _sim.Step();
        _acc = hours - (int)hours;
        _map.HourFraction = (float)_acc;
        var b = t.Battles[^1];
        _siegeJump = b.Id;
        GD.Print($"Siege day {SimTime.DayOf(b.Tick)}: {b.Name} at the {Garrison.Place(t, b.Approach)}, waves {string.Join(", ", b.Waves.Select(w => $"{Garrison.Place(t, w.Section)} {w.Size}{(w.Breached ? " breached" : "")}"))}; {b.End}");
    }
}
