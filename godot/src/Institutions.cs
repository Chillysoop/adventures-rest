using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The institutions window: each of the town's institutions with the keeper who runs it and how they feel
/// about the town, what it does for the town, the council's funding and the choice the mayor makes about
/// it, and the lasting features stories have left there. Below, the ones the town could still raise.
/// </summary>
public partial class Main
{
    private static Color FeelingColor(double regard) => regard >= 25 ? Ui.Good : regard >= -10 ? Ui.Ink : regard >= -40 ? Ui.Gold : Ui.Bad;

    private void RenderInstitutions()
    {
        var p = _panes[Tab.Institutions];
        var t = _sim.Town;
        var r = t.Content.Institutions;
        p.Begin();
        double daily = Js.Sum(t.Institutions.Where(s => !s.Shut), s => Institutions.Cost(t, Institutions.Level(t, s)));
        p.Card($"[font_size=17][b]{t.Institutions.Count} of {r.Institutions.Count} institutions[/b][/font_size]\n" +
               Ui.C(Ui.Ink2, t.Institutions.Count == 0
                   ? "A town grows around its institutions. Each has a keeper, does something for the whole town every day, and asks for something now and then."
                   : $"The council's patronage costs {Js.Str(Js.Round(daily))} gold a day. Generous funding buys more of each one's service and its keeper's goodwill."), Ui.Gold);

        foreach (var s in t.Institutions)
        {
            var d = Institutions.DefOf(t, s);
            var k = Institutions.Keeper(t, s);
            string id = d.Id;
            double strength = Institutions.Strength(t, s);
            string feeling = Institutions.Feeling(s.Regard);
            string at = s.BuildingId is int bid ? t.Building(bid).Name : "";
            p.Heading(Ui.Cap(d.Name));
            var service = Institutions.Parts(t, s).SelectMany(x => Institutions.Describe(t, x, strength)).Distinct().ToList();
            p.Row($"[b]{Ui.Esc(Institutions.KeeperName(t, s))}[/b]  {Tag(Ui.Cap(feeling), FeelingColor(s.Regard))} {Ui.C(Ui.Ink2, $"[font_size=12]regard {(s.Regard >= 0 ? "+" : "−")}{Math.Abs(Math.Round(s.Regard))}[/font_size]")}" +
                  (s.Shut ? "  " + Tag("Shut", Ui.Bad) : "") + "\n" +
                  Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(k.Text)}[/font_size]") + "\n" +
                  $"[font_size=13]{(s.Shut ? "Shut until it has a roof again." : Ui.Esc(Ui.Cap(string.Join(" · ", service))))}[/font_size]" +
                  (s.Answered + s.Ignored > 0 ? "\n" + Ui.C(Ui.Ink2, $"[font_size=11]Requests answered {s.Answered}, left unanswered {s.Ignored}{(at != "" ? $" · at {Ui.Esc(at)}" : "")}[/font_size]") : at != "" ? "\n" + Ui.C(Ui.Ink2, $"[font_size=11]At {Ui.Esc(at)}[/font_size]") : ""),
                ("Show", s.BuildingId != null, false, () => { if (s.BuildingId is int b) LookAtBuilding(t.Building(b)); }));

            // Funding: one button a level, the current one lit.
            var levels = new List<(string, bool, bool, Action)>();
            for (int i = 0; i < r.Patronage.Count; i++)
            {
                var lv = r.Patronage[i];
                int level = i;
                double cost = Institutions.Cost(t, lv);
                levels.Add(($"{lv.Name}{(cost > 0 ? $" · {Js.Str(cost)}g/day" : "")}", i != s.Patronage && !s.Shut, i == s.Patronage, () => Act(() => _sim.FundInstitution(id, level))));
            }
            p.Buttons(levels.ToArray());

            if (d.Choice is { } c)
            {
                string? blocked = Institutions.ChoiceBlocked(t, s);
                var chosen = Institutions.Chosen(t, s);
                p.Text(chosen == null
                    ? Ui.C(Ui.Gold, $"[b]Decide {Ui.Esc(c.Label)}.[/b] ") + Ui.C(Ui.Ink2, Ui.Esc(c.Text))
                    : $"{Ui.C(Ui.Ink2, Ui.Esc(Ui.Cap(c.Label)) + ":")} [b]{Ui.Esc(chosen.Name)}[/b]. {Ui.C(Ui.Ink2, Ui.Esc(chosen.Text))}" +
                      (blocked != null ? Ui.C(Ui.Ink2, $" ({Ui.Esc(blocked)})") : Ui.C(Ui.Ink2, $" Changing it costs {Institutions.KeeperGiven(t, s)} some goodwill.")), 12);
                foreach (var o in c.Options)
                {
                    if (o.Id == s.Choice) continue;
                    string opt = o.Id;
                    var gives = Institutions.Describe(t, o.Service);
                    p.Row($"[font_size=13][b]{Ui.Esc(Ui.Cap(o.Name))}[/b][/font_size]\n" + Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Ui.Cap(string.Join(" · ", gives)))}[/font_size]"),
                        (chosen == null ? "Choose" : "Change", blocked == null && !s.Shut, chosen == null, () => Act(() => _sim.ChooseInstitution(id, opt))));
                }
            }
            foreach (var f in s.Fixtures)
                if (r.Fixture(f) is { } fd)
                    p.Text($"{Ui.C(Ui.Gold, "◆")} [b]{Ui.Esc(Ui.Cap(fd.Name))}[/b] {Ui.C(Ui.Ink2, Ui.Esc(fd.Text))}", 12);
        }

        var missing = r.Institutions.Where(d => Institutions.Find(t, d.Id) == null).ToList();
        if (missing.Count > 0)
        {
            p.Heading("Still to come");
            foreach (var d in missing)
            {
                var bd = t.Content.Buildings.FirstOrDefault(b => b.Id == d.Building);
                string how;
                (string, bool, bool, Action)[] buttons = [];
                if (d.Adopt)
                    how = d.Building == t.Content.Shrines.Temple ? "Forms around the town's temple once it stands." : $"Forms around the town's oldest {Ui.Esc(bd?.Name.ToLowerInvariant() ?? d.Building)} once the town is a {Ui.Esc(t.Content.City.Tiers.FirstOrDefault(x => x.Id == d.Tier)?.Name.ToLowerInvariant() ?? "hamlet")}.";
                else
                {
                    string? why = bd == null ? "not in this build" : Planning.Blocked(t, bd);
                    bool rising = t.Buildings.Any(b => b.Def.Id == d.Building && b.IsSite);
                    how = rising ? "Its builders are at work." : why == null ? $"Raise it from the Government build bar: {Js.Str(Planning.Cost(t, bd!))} gold." : $"Raise it from the Government build bar ({Ui.Esc(why)}).";
                    string bid = d.Building;
                    if (!rising) buttons = [("Place", why == null, true, () => PlaceFromCity(bid))];
                }
                p.Row($"[b]{Ui.Esc(Ui.Cap(d.Name))}[/b]\n" + Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(d.Blurb)}[/font_size]") + $"\n[font_size=12]{how}[/font_size]", buttons);
            }
        }
        p.Note("Keepers bring requests as events now and then. Answering them, and funding them well, wins them over; ignoring them sours them, and a bitter keeper gives the town little.");
        p.Commit();
    }
}
