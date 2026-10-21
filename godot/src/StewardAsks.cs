using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// What stewards who ask first want to do: small yes/no cards under the alert strip. Hover the title for why, click
/// it to open the card up; ✔ goes ahead, ✖ says no (the steward won't ask the same for a while). Areas are handed
/// over on the Settings window's Stewards tab.
/// </summary>
public partial class Main
{
    private VBoxContainer _asks = null!;
    private string _asksSig = "";
    private int? _askOpen;
    private const int AsksShown = 3;

    private Control BuildAsks()
    {
        _asks = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ShrinkCenter, Visible = false };
        _asks.AddThemeConstantOverride("separation", 4);
        return _asks;
    }

    private void SyncAsks()
    {
        List<StewardAsk> asks = _title.Visible ? [] : _sim.Town.Stewards.Asks;
        string sig = string.Join(",", asks.Select(a => a.Id)) + $"|{_askOpen}";
        if (sig == _asksSig) return;
        _asksSig = sig;
        Ui.Clear(_asks);
        foreach (var a in asks.Take(AsksShown)) _asks.AddChild(AskCard(a));
        if (asks.Count > AsksShown) _asks.AddChild(Ui.Label($"+{asks.Count - AsksShown} more", Ui.Ink2, Ui.Bold, 11));
        _asks.Visible = asks.Count > 0;
    }

    private Control AskCard(StewardAsk a)
    {
        var area = Stewards.Area(a.Area);
        bool open = _askOpen == a.Id;
        var card = new PanelContainer { CustomMinimumSize = new Vector2(360, 0), MouseFilter = MouseFilterEnum.Stop };
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.08f, 0.06f, 0.04f, 0.88f), Ui.Gold, 8, 1, 10, 6));
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 4);
        card.AddChild(col);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        col.AddChild(row);
        row.AddChild(new TextureRect { Texture = Ui.Icon(area.Icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(20, 20), SizeFlagsVertical = SizeFlags.ShrinkCenter, TooltipText = $"{area.Name} steward asks" });
        int id = a.Id;
        var title = new Button { Text = a.Title, Flat = true, Alignment = HorizontalAlignment.Left, ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, TooltipText = a.Why == "" ? area.Blurb : a.Why };
        title.AddThemeFontSizeOverride("font_size", 13);
        title.Pressed += () =>
        {
            _askOpen = open ? null : id;
            SyncAsks();
        };
        row.AddChild(title);
        row.AddChild(AskButton("✔", Ui.Good, "Go ahead", () => AnswerAsk(id, true)));
        row.AddChild(AskButton("✖", Ui.Bad, "No · not again for a while", () => AnswerAsk(id, false)));
        if (open)
        {
            var why = Ui.Label((a.Why == "" ? "" : a.Why + "\n") + $"• {area.Name}: Settings › Stewards", Ui.Ink2, Ui.Body, 12);
            why.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            col.AddChild(why);
        }
        return card;
    }

    private static Button AskButton(string text, Color color, string tip, Action then)
    {
        var b = new Button { Text = text, ThemeTypeVariation = "Speed", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(30, 0), TooltipText = tip };
        b.AddThemeColorOverride("font_color", color);
        b.Pressed += then;
        return b;
    }

    private void AnswerAsk(int id, bool yes)
    {
        if (yes && !_sim.AnswerSteward(id, true)) Toast("It can't be done any more");
        else if (!yes) _sim.AnswerSteward(id, false);
        if (_askOpen == id) _askOpen = null;
        _dirty = true;
        SyncAsks();
        GuideEvent("steward");
    }
}
