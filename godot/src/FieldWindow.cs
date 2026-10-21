using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The field in the army window: a foe on the road, who marches out to meet it and who stays on the walls (regiments,
/// allied hosts at the gate, heroes with goals), the order, the odds and the march; the host out on the road; and the
/// last field battle's report.
/// </summary>
public partial class Main
{
    private string _fieldOrder = "advance";

    private static string OrderIcon(string id) => id switch
    {
        "hold" => "order-defend",
        "flank" => "order-scout",
        "charge" => "order-strike",
        _ => "order-guard",
    };

    private void RenderField(Pane p, Town t)
    {
        var foes = Field.Foes(t);
        var s = t.Army.Sortie;
        var last = t.Army.Fields.LastOrDefault();
        bool recent = last != null && t.Tick - last.Tick < 10 * SimTime.HoursPerDay;
        if (foes.Count == 0 && s == null && !recent) return;
        p.Heading("Field");
        var rules = t.Content.Army.Field;
        var orders = rules.Orders.ToList();
        if (s != null)
        {
            var foe = Field.FoeOf(t, s.Foe);
            int men = s.Regiments.Sum(id => Army.Find(t, id)?.Soldiers.Count ?? 0);
            p.Row($"{Ui.Img("compass", 15)} [b]The host is out[/b]  ·  meets {Ui.Esc(foe?.Name ?? "the foe")} in [b]{Math.Max(0, s.MeetTick - t.Tick)}h[/b]\n" +
                  $"[font_size=12]{Ui.Img("tab-army", 13)} {men}   {Ui.Img("act-offer", 13)} {s.Allies.Count}   {Ui.Img("heroes", 13)} {s.Heroes.Count}[/font_size]",
                ("Call back", true, false, () => Act(() => _sim.RecallHost())));
            int at = Math.Max(0, orders.FindIndex(o => o.Id == s.Order));
            p.Choice("Order", orders.Select(o => o.Name).ToList(), at, i => Act(() => _sim.SetFieldOrder(orders[i].Id)));
        }
        else
            foreach (var f in foes)
            {
                int hours = f.ArrivalTick - t.Tick;
                double est = f.Key == "warband" ? Threat.EstimatedStrength(t) ?? f.Strength : f.Strength;
                double ours = Field.HostWorth(t, Field.Marching(t), Field.MarchingAllies(t), Field.GoalHeroes(t));
                double odds = ours / Math.Max(1, est);
                string tag = odds >= 1.5 ? Ui.C(Ui.Good, "strong") : odds >= 1 ? Ui.C(Ui.Gold, "even") : Ui.C(Ui.Bad, "outmatched");
                p.Row($"{Ui.Img("warband", 15)} [b]{Ui.Esc(Ui.Cap(f.Name))}[/b]  ~{Js.Str(Js.Round(est))}  ·  at the walls in [b]{hours}h[/b]\n" +
                      $"[font_size=12]Host {Ui.C(Ui.Ink, Js.Str(Js.Round(ours)))} · {tag}[/font_size]");
                p.Add($"fp{string.Join(",", t.Army.Regiments.Select(r => $"{r.Id}{r.Marches}{r.Soldiers.Count}"))}|{string.Join(",", t.Army.Calls.Select(c => $"{c.Id}{c.State}{c.Marches}"))}|{string.Join(",", t.Army.Goals.Select(g => $"{g.HeroId}{g.Goal}"))}",
                    () => MarchPicks(t));
                int at = Math.Max(0, orders.FindIndex(o => o.Id == _fieldOrder));
                p.Choice("Order", orders.Select(o => o.Name).ToList(), at, i => Act(() => _fieldOrder = orders[i].Id));
                p.Note(orders[at].Blurb);
                string? why = Field.MarchBlocked(t, f.Key);
                string key = f.Key;
                p.Buttons(($"March out{(why != null ? $" · {why}" : "")}", why == null, true, () => Act(() => _sim.MarchOut(key, _fieldOrder))));
            }
        if (recent && s == null) FieldReport(p, t, last!);
    }

    /// <summary>Who marches: each regiment and allied host as a toggle, each free hero with a goal menu.</summary>
    private Control MarchPicks(Town t)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 4);
        flow.AddThemeConstantOverride("v_separation", 4);
        foreach (var r in t.Army.Regiments.Where(r => r.Soldiers.Count > 0).OrderBy(r => r.Id))
        {
            var u = Army.Unit(t, r);
            var b = new Button { Icon = Ui.Icon($"unit-{u.Id}"), Text = $"{r.Soldiers.Count}", ExpandIcon = false, ToggleMode = true, ButtonPressed = r.Marches, FocusMode = Control.FocusModeEnum.None, TooltipText = $"{r.Name}\n{(r.Marches ? "Marches out" : "Stays on the walls")}  ·  {Math.Round(Field.Worth(t, r))} in the field" };
            b.AddThemeConstantOverride("icon_max_width", 18);
            b.AddThemeColorOverride("icon_normal_color", UnitColor(u).Lightened(0.2f));
            b.Modulate = r.Marches ? Colors.White : new Color(1, 1, 1, 0.55f);
            int id = r.Id;
            bool to = !r.Marches;
            b.Pressed += () => Act(() => _sim.SetMarches(id, to));
            flow.AddChild(b);
        }
        foreach (var c in t.Army.Calls.Where(c => c.State == CallState.Arrived).OrderBy(c => c.Id))
        {
            var st = AllyCalls.Of(t, c);
            var b = new Button { Icon = Ui.Icon("act-offer"), Text = $"{Js.Str(c.Strength)}", ExpandIcon = false, ToggleMode = true, ButtonPressed = c.Marches, FocusMode = Control.FocusModeEnum.None, TooltipText = $"{Ui.Cap(st.People)}\n{(c.Marches ? "Marches out" : "Stays at the gate")}" };
            b.AddThemeConstantOverride("icon_max_width", 18);
            b.Modulate = c.Marches ? Colors.White : new Color(1, 1, 1, 0.55f);
            int id = c.Id;
            bool to = !c.Marches;
            b.Pressed += () => Act(() => _sim.SetAllyMarches(id, to));
            flow.AddChild(b);
        }
        box.AddChild(flow);

        var goals = t.Content.Army.Field.Goals.ToList();
        var heroes = Field.FreeHeroes(t).Take(8).ToList();
        if (heroes.Count > 0)
        {
            var hf = new HFlowContainer();
            hf.AddThemeConstantOverride("h_separation", 4);
            hf.AddThemeConstantOverride("v_separation", 4);
            foreach (var h in heroes)
            {
                string goal = t.Army.Goals.FirstOrDefault(g => g.HeroId == h.Id)?.Goal ?? "";
                var items = new List<(string, string)> { ("", "Stays home") };
                items.AddRange(goals.Select(g => ("", g.Name)));
                string label = goal == "" ? h.GivenName : $"{h.GivenName}: {goals.FirstOrDefault(g => g.Id == goal)?.Name}";
                int id = h.Id;
                var m = Ui.Menu(label, "heroes", items, i => Act(() => _sim.SetFieldGoal(id, i == 0 ? "" : goals[i - 1].Id)));
                m.Modulate = goal == "" ? new Color(1, 1, 1, 0.6f) : Colors.White;
                m.TooltipText = $"{h.Name} · {Ui.Cap(h.Hero!.Cls.Name)} {h.Hero.Level}";
                hf.AddChild(m);
            }
            box.AddChild(hf);
        }
        return box;
    }

    /// <summary>The last field battle: how it ended, what it cost each side, and how the heroes' goals went.</summary>
    private void FieldReport(Pane p, Town t, FieldBattle b)
    {
        var (icon, col) = b.End switch
        {
            FieldEnd.Won or FieldEnd.Slain => ("order-strike", Ui.Good),
            FieldEnd.Lost => ("foe-unseen", Ui.Bad),
            _ => ("order-defend", Ui.Gold),
        };
        double theirs = b.Theirs.Start - (b.Theirs.Left.LastOrDefault());
        var goals = b.Goals.Select(g => $"{(g.Done ? Ui.C(Ui.Good, "✔") : Ui.C(Ui.Bad, "✘"))} {Ui.Esc(t.Agent(g.HeroId).GivenName)} · {Ui.Esc(Field.Goal(t, g.Goal)?.Name ?? g.Goal)}");
        p.Row($"{Ui.Img(icon, 15)} [b]{Ui.C(col, b.Lines[0])}[/b]\n" +
              $"[font_size=12]{Ui.Img("warband", 13)} −{Js.Str(Js.Round(theirs))}   {Ui.Img("tab-army", 13)} ✝{b.Fallen.Count}   {Ui.C(Ui.Ink2, $"Day {SimTime.DayOf(b.Tick)}")}" +
              (b.Goals.Count > 0 ? $"\n{string.Join("   ", goals)}" : "") + "[/font_size]",
            ("Watch", true, false, () => { _map.Crowd.WatchField(b); _map.LookAt(_map.Crowd.FieldSpot(t, b).At, 16); }));
    }
}
