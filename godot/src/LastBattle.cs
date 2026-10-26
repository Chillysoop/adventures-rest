using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The last battle: the moments the campaign turns on get the big card, on the first-meeting frame with the big bad's
/// own picture. The champions riding for its seat (the mayor's call, <see cref="Saga.CallLastRide"/>), its host setting
/// out for the walls, and its fall. Each stops the clock ("last-battle" in Settings).
/// </summary>
public partial class Main
{
    /// <summary>What the card has shown: the last ride's party, the host's marches, -1 until the first look after a load.</summary>
    private int _rideSeen = -1, _marchSeen = -1;

    private void SyncLastBattle()
    {
        var t = _sim.Town;
        var big = t.Saga.Menaces.FirstOrDefault(m => m.BigBad);
        if (big == null) return;
        var ride = t.Parties.Where(p => p.SiteId == big.SeatId).Select(p => p.Id).DefaultIfEmpty(0).Max();
        int marches = big.ArrivalTick != null && Horde.Known(t, big) ? big.Marches : big.Marches - 1;
        if (_rideSeen < 0 || _marchSeen < 0)
        {
            (_rideSeen, _marchSeen) = (ride, marches);
            return;
        }
        if (_intro.Visible || _opts.Smoke || _title.Visible || _menu.Visible) return;
        if (ride > _rideSeen && big.Alive)
        {
            _rideSeen = ride;
            OpenLastBattle(big, "ride");
            StopClock("last-battle");
        }
        else if (marches > _marchSeen && big.Alive)
        {
            _marchSeen = marches;
            OpenLastBattle(big, "march");
            StopClock("last-battle");
        }
    }

    /// <summary>For shots: --last-battle ride|march|ready shows the card for the town's big bad.</summary>
    private void StageLastBattle(string moment)
    {
        if (_sim.Town.Saga.Menaces.FirstOrDefault(m => m.BigBad) is { } big) OpenLastBattle(big, moment);
    }

    private void OpenLastBattle(Menace m, string moment)
    {
        var t = _sim.Town;
        var d = Intros.Def(t, m.Lineage);
        if (d == null) return;
        _introArt.Show(d, Intros.Scene(t, d));
        _introShown = null;
        Ui.Clear(_introBox);
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        head.AddChild(new Crest(d, CrestSize));
        var names = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(NamesWidth, 0) };
        names.AddThemeConstantOverride("separation", 0);
        names.AddChild(Sized(Ui.Rich(Ui.C(Ui.Bad, "[font_size=11][b]THE LAST BATTLE[/b][/font_size]") + Ui.Dim($"[font_size=11]   day {t.Day}[/font_size]"), 11), NamesWidth));
        var title = Ui.Label(Ui.Cap(m.Leader != "" ? m.Leader : m.Name), Ui.Ink, Ui.Display, 26);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        names.AddChild(Sized(title, NamesWidth));
        string line = moment switch
        {
            "march" => $"The host of {m.Name} is on the march for the {Garrison.Place(t, m.Approach)}.",
            "ready" => $"Your champions are strong enough to face {m.Leader} at {m.SeatName}.",
            _ => $"Your champions ride for {m.SeatName}. Everything turns on this.",
        };
        names.AddChild(Sized(Ui.Rich(Ui.C(Ui.Ink2, $"[i]{Ui.Esc(line)}[/i]"), 14), NamesWidth));
        head.AddChild(names);
        _introBox.AddChild(head);

        var points = new List<string>();
        if (moment == "march")
        {
            points.Add(Ui.Bullet("Call the watch to arms on that side", "defence"));
            points.Add(Ui.Bullet("Keep your heroes home, or meet it on the road with the army", "heroes"));
            points.Add(Ui.Bullet("Kill it at the walls and the war is won", "ui-renown"));
        }
        else
        {
            var party = t.Parties.Where(p => p.SiteId == m.SeatId && !p.Returned).OrderByDescending(p => p.Id).FirstOrDefault();
            var riders = party != null ? party.Members.Select(t.Agent).ToList() : Saga.Champions(t, out _, out _) ?? [];
            foreach (var h in riders) points.Add(Ui.Bullet($"[b]{Ui.Esc(h.GivenName)}[/b] {Ui.Dim($"{Ui.Esc(Ui.Cap(h.Hero!.Cls.Name))} {h.Hero.Level}")}", "heroes"));
            points.Add(Ui.Bullet($"{Ui.Esc(m.SeatName)} · level {m.Level}", "threat"));
        }
        _introBox.AddChild(Sized(Ui.Rich(string.Join("\n", points), 14), TextWidth));

        var foot = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        foot.AddThemeConstantOverride("separation", 8);
        var window = moment == "march" ? Tab.Defence : Tab.War;
        var open = new Button { Text = moment == "march" ? "Defence" : "War", Icon = Ui.Icon(moment == "march" ? "tab-defence" : "tab-war"), FocusMode = FocusModeEnum.None, ExpandIcon = true };
        open.AddThemeConstantOverride("icon_max_width", 18);
        open.Pressed += () =>
        {
            CloseIntro();
            OpenWindow(window);
        };
        foot.AddChild(open);
        var onward = new Button { Text = "Onward", FocusMode = FocusModeEnum.None };
        onward.AddThemeStyleboxOverride("normal", Castle.Button("normal", true));
        onward.AddThemeStyleboxOverride("hover", Castle.Button("hover", true));
        onward.Pressed += CloseIntro;
        foot.AddChild(onward);
        _introBox.AddChild(foot);
        _intro.Visible = true;
        _intro.MoveToFront();
        Fit(_intro);
    }
}
