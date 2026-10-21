using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The institutions window: a strip of slots, one per institution (lit when open, dim when only heard of, "?" while
/// undiscovered), then each open one with its keeper, what it does as icon bullets, its funding and the mayor's
/// choice, then the ones the town has heard of with what they still need.
/// </summary>
public partial class Main
{
    private static Color FeelingColor(double regard) => regard >= 25 ? Ui.Good : regard >= -10 ? Ui.Ink : regard >= -40 ? Ui.Gold : Ui.Bad;

    /// <summary>A service as one coloured bullet a line, each with its icon.</summary>
    private static string Bullets(Town t, ServiceDef d, double k = 1, int size = 13) => Bullets(Institutions.Lines(t, d, k), size);

    private static string Bullets(IEnumerable<(string Icon, string Text, bool Good)> lines, int size = 13) =>
        string.Join("\n", lines.DistinctBy(l => l.Text).Select(l => $"[font_size={size}]{Ui.Img(l.Icon, size + 2)} {Ui.C(l.Good ? Ui.Good : Ui.Bad, Ui.Esc(Ui.Cap(l.Text)))}[/font_size]"));

    /// <summary>The institution's icon, dimmed for one not yet open.</summary>
    private static Control InstitutionIcon(InstitutionDef d, bool open, int size = 40)
    {
        var r = new TextureRect
        {
            Texture = Ui.Icon(d.Icon == "" ? "tab-institutions" : d.Icon),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(size, size),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            Modulate = open ? Colors.White : new Color(0.55f, 0.55f, 0.55f, 0.8f),
        };
        return r;
    }

    private void RenderInstitutions()
    {
        var p = _panes[Tab.Institutions];
        var t = _sim.Town;
        var r = t.Content.Institutions;
        p.Begin();
        double daily = Js.Sum(t.Institutions.Where(s => !s.Shut), s => Institutions.Cost(t, Institutions.Level(t, s)));
        int seen = r.Institutions.Count(d => Institutions.Seen(t, d.Id));
        p.Card($"{Ui.Img("tab-institutions", 22)} [font_size=18][b]{t.Institutions.Count} of {r.Institutions.Count}[/b][/font_size] {Ui.C(Ui.Ink2, "institutions")}\n" +
               $"[font_size=13]{Ui.C(Ui.Good, $"● {t.Institutions.Count} open")}   {Ui.C(Ui.Gold, $"● {seen - t.Institutions.Count} heard of")}   {Ui.C(Ui.Ink2, $"? {r.Institutions.Count - seen} undiscovered")}" +
               (daily > 0 ? $"   {Ui.Img("gold", 14)}{Js.Str(Js.Round(daily))}/day" : "") + "[/font_size]", Ui.Gold);

        // The slots: every institution has one, in the content's order, so the shape of what's left shows.
        var slots = r.Institutions.Select(d => (d, Institutions.Find(t, d.Id) != null, Institutions.Seen(t, d.Id))).ToList();
        p.Add("slots" + string.Join("", slots.Select(x => x.Item2 ? "o" : x.Item3 ? "s" : "h")), () =>
        {
            var flow = new HFlowContainer();
            flow.AddThemeConstantOverride("h_separation", 6);
            flow.AddThemeConstantOverride("v_separation", 6);
            foreach (var (d, open, known) in slots)
            {
                var slot = new PanelContainer
                {
                    CustomMinimumSize = new Vector2(48, 48),
                    TooltipText = known ? $"{Ui.Cap(d.Name)}{(open ? "" : " (heard of)")}" : "Undiscovered",
                };
                slot.AddThemeStyleboxOverride("panel", Ui.Box(open ? Ui.Panel2 : Ui.Panel, open ? Ui.Gold : known ? Ui.Line : Ui.Panel2, 6, open ? 2 : 1, 4, 4));
                if (known) slot.AddChild(InstitutionIcon(d, open, 36));
                else
                {
                    var q = Ui.Label("?", Ui.Ink2, Ui.Bold, 20);
                    q.HorizontalAlignment = HorizontalAlignment.Center;
                    q.VerticalAlignment = VerticalAlignment.Center;
                    slot.AddChild(q);
                }
                flow.AddChild(slot);
            }
            return flow;
        });

        foreach (var s in t.Institutions)
        {
            var d = Institutions.DefOf(t, s);
            string id = d.Id;
            double strength = Institutions.Strength(t, s);
            string at = s.BuildingId is int bid ? t.Building(bid).Name : "";
            var lead = () => InstitutionIcon(d, !s.Shut);
            var service = s.Shut ? Ui.C(Ui.Bad, "[font_size=13]✘ Shut: no roof[/font_size]")
                : Bullets(Institutions.Parts(t, s).SelectMany(x => Institutions.Lines(t, x, strength)));
            p.Row($"[font_size=16][b]{Ui.Esc(Ui.Cap(d.Name))}[/b][/font_size]\n" +
                  $"[font_size=13]{Ui.Esc(Institutions.KeeperName(t, s))}  {Tag(Ui.Cap(Institutions.Feeling(s.Regard)), FeelingColor(s.Regard))}" +
                  (s.Answered + s.Ignored > 0 ? $"  {Ui.C(Ui.Good, $"✔{s.Answered}")} {Ui.C(Ui.Bad, $"✘{s.Ignored}")}" : "") + "[/font_size]\n" + service,
                (d.Id + s.Shut, lead, null),
                ("Show", s.BuildingId != null, false, () => { if (s.BuildingId is int b) LookAtBuilding(t.Building(b)); }));

            // Funding: one button a level, the current one lit.
            var levels = new List<(string, bool, bool, Action)>();
            for (int i = 0; i < r.Patronage.Count; i++)
            {
                var lv = r.Patronage[i];
                int level = i;
                double cost = Institutions.Cost(t, lv);
                levels.Add(($"{lv.Name}{(cost > 0 ? $" · {Js.Str(cost)}g" : "")}", i != s.Patronage && !s.Shut, i == s.Patronage, () => Act(() => _sim.FundInstitution(id, level))));
            }
            p.Buttons(levels.ToArray());

            if (d.Choice is { } c)
            {
                string? blocked = Institutions.ChoiceBlocked(t, s);
                var chosen = Institutions.Chosen(t, s);
                p.Text(chosen == null
                    ? Ui.C(Ui.Gold, $"◆ [b]Choose: {Ui.Esc(Ui.Cap(c.Label))}[/b]")
                    : $"{Ui.C(Ui.Gold, "◆")} {Ui.C(Ui.Ink2, Ui.Esc(Ui.Cap(c.Label)) + ":")} [b]{Ui.Esc(chosen.Name)}[/b]" + (blocked != null ? Ui.C(Ui.Ink2, $"  ({Ui.Esc(blocked)})") : ""), 13);
                foreach (var o in c.Options)
                {
                    if (o.Id == s.Choice) continue;
                    string opt = o.Id;
                    p.Row($"[font_size=13][b]{Ui.Esc(Ui.Cap(o.Name))}[/b][/font_size]\n" + Bullets(t, o.Service, 1, 12),
                        (chosen == null ? "Choose" : "Change", blocked == null && !s.Shut, chosen == null, () => Act(() => _sim.ChooseInstitution(id, opt))));
                }
            }
            foreach (var f in s.Fixtures)
                if (r.Fixture(f) is { } fd)
                    p.Text($"{Ui.C(Ui.Gold, "★")} [b]{Ui.Esc(Ui.Cap(fd.Name))}[/b]", 12);
        }

        var heard = r.Institutions.Where(d => Institutions.Seen(t, d.Id) && Institutions.Find(t, d.Id) == null).ToList();
        if (heard.Count > 0)
        {
            p.Heading("Heard of");
            foreach (var d in heard)
            {
                var bd = t.Content.Buildings.FirstOrDefault(b => b.Id == d.Building);
                var needs = new List<string>();
                string Need(bool ok, string text) => $"{(ok ? Ui.C(Ui.Good, "✔") : Ui.C(Ui.Bad, "✘"))} {Ui.Esc(text)}";
                string tier = Institutions.Charter(t, d);
                if (tier != "") needs.Add(Need(City.Reached(t, tier), City.TierName(t, tier)));
                (string, bool, bool, Action)[] buttons = [];
                if (d.Adopt)
                {
                    bool has = t.Buildings.Any(b => b.Def.Id == d.Building && b.Complete && !b.Closed);
                    needs.Add(Need(has, Ui.Cap(bd?.Name ?? d.Building)));
                }
                else if (t.Buildings.Any(b => b.Def.Id == d.Building && b.IsSite))
                    needs.Add($"{Ui.Img("tools", 14)} {Ui.C(Ui.Gold, "Rising")}");
                else
                {
                    string? why = bd == null ? "not in this build" : Planning.Blocked(t, bd);
                    if (bd != null) needs.Add($"{Ui.Img("gold", 14)}{Js.Str(Planning.Cost(t, bd))}");
                    if (why != null && !why.StartsWith("needs the town to be", StringComparison.Ordinal)) needs.Add(Need(false, Ui.Cap(why)));
                    string bid = d.Building;
                    buttons = [("Place", why == null, true, () => PlaceFromCity(bid))];
                }
                string from = t.InstitutionsSeen.Get(d.Id, "");
                p.Row($"[font_size=15][b]{Ui.Esc(Ui.Cap(d.Name))}[/b][/font_size]" + (from != "" ? Ui.C(Ui.Ink2, $"  [font_size=11]{Ui.Img("lore", 12)} {Ui.Esc(from)}[/font_size]") : "") + "\n" +
                      Bullets(t, d.Service, 1, 12) + "\n" +
                      $"[font_size=13]{string.Join("   ", needs)}[/font_size]",
                    (d.Id, () => InstitutionIcon(d, false), null), buttons);
            }
        }
        if (seen < r.Institutions.Count)
            p.Note($"? {r.Institutions.Count - seen} more to discover\n• heroes and folk who settle\n• crafts and trade\n• sites cleared\n• a bigger charter");
        p.Commit();
    }
}
