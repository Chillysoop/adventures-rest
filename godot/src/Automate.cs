using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Client;

/// <summary>
/// Council › Automate: the lesser council. Every area of business a steward can take on, each with its titled
/// lesser councillor once handed over (<see cref="Stewards"/>): You / Asks / Steward per area, presets that hand
/// over many at once, a monthly gold cap per steward, the armory's levers one by one, and the stewards' report.
/// An area a steward runs raises no alerts; what it did is in the report. Each window also shows its own areas'
/// switches at the top (<see cref="StewardStrip"/>).
/// </summary>
public partial class Main
{
    /// <summary>Presets: a name, its icon, its hover and what it does.</summary>
    private static readonly (string Name, string Icon, string Tip, Func<StewardArea, StewardMode> Mode)[] Presets =
    [
        ("All mine", "ui-person", "You run every area", _ => StewardMode.Player),
        ("Advisors", "ui-info", "Every steward asks you first", a => a.CanAsk ? StewardMode.Ask : StewardMode.Player),
        ("Run the city", "tab-city", "Stewards run the town at home; you run heroes, war and the realm",
            a => a.Id is "planning" or "research" or "council" or "guilds" or "order" or "stories" ? StewardMode.Steward : StewardMode.Player),
        ("All stewards", "tab-automate", "Stewards run every area; war stays yours", _ => StewardMode.Steward),
    ];

    /// <summary>Monthly caps a steward can be given, in gold; 0 is none.</summary>
    private static readonly int[] Caps = [0, 100, 300, 1000];

    private void RenderAutomate()
    {
        var p = _panes[Tab.Automate];
        var t = _sim.Town;
        p.Begin();
        int handed = Stewards.Areas.Count(a => Stewards.Hand(t, _sim.Options, a.Id));
        double spent = Stewards.Areas.Sum(a => Stewards.SpentThisMonth(t, a.Id));
        p.Card($"{Ui.Img("tab-automate", 22)} [font_size=18][b]{handed} of {Stewards.Areas.Length}[/b][/font_size] {Ui.C(Ui.Ink2, "handed over")}\n" +
               Ui.Chips(Ui.Chip("gold", $"{Js.Str(Js.Round(spent))}", Ui.Gold, "Spent by stewards this month"),
                   t.Stewards.Asks.Count > 0 ? Ui.Chip("ui-info", $"{t.Stewards.Asks.Count}", Ui.Gold, "Asks waiting") : ""), Ui.Gold);

        p.Page("Council", "tab-council");
        p.Heading("Presets", "tab-automate");
        p.TipButtons(Presets.Select(x => (x.Name, true, Stewards.Areas.All(a => Stewards.Mode(t, _sim.Options, a.Id) == x.Mode(a)), (Action)(() => Act(() =>
        {
            foreach (var a in Stewards.Areas) _sim.SetSteward(a.Id, x.Mode(a));
            GuideEvent("steward");
        })), x.Tip)).ToArray());
        p.Heading("Lesser council", "tab-council");
        foreach (var a in Stewards.Areas) StewardRow(p, t, a, true);
        p.Help("Hand an area to a steward and a townsperson takes a lesser seat to run it, titled for the work.",
            "Steward: they act, and the area's alerts leave the rail. Read what they did on the Report page.",
            "Asks: they ask first, a small ✔ / ✖ card. ✔ all answers every ask at once.",
            "War, the army and the last ride stay yours.");

        p.Page("Budgets", "gold");
        p.Heading("Gold a month", "gold");
        foreach (var a in Stewards.Areas.Where(a => Stewards.Hand(t, _sim.Options, a.Id)))
        {
            string id = a.Id;
            double cap = t.Stewards.Caps.GetValueOrDefault(id), used = Stewards.SpentThisMonth(t, id);
            p.Row($"{Ui.Img(a.Icon, 16)} [b]{a.Title}[/b]  {Ui.Chip("gold", cap > 0 ? $"{Js.Str(Js.Round(used))}/{Js.Str(cap)}" : Js.Str(Js.Round(used)), cap > 0 && used >= cap ? Ui.Bad : null, "Spent this month / cap")}",
                Caps.Select(c => (c == 0 ? "∞" : Js.Str(c), true, (int)cap == c, (Action)(() => Act(() => _sim.SetStewardCap(id, c))))).ToArray());
        }
        var armory = Stewards.Area("armory");
        if (Stewards.Hand(t, _sim.Options, "armory"))
        {
            p.Heading("Quartermaster's levers", "tab-armory");
            p.Row($"{Ui.Img("tab-armory", 16)} [b]Buy · Gift · Sell[/b]", armory.Kinds.Select(k => (Ui.Cap(k), true, Stewards.LeverOn(t, "armory", k),
                (Action)(() => Act(() => _sim.SetStewardLever("armory", k, !Stewards.LeverOn(t, "armory", k)))))).ToArray());
        }

        p.Page("Report", "records");
        p.Heading("What the stewards did", "records");
        foreach (var d in Enumerable.Reverse(t.Stewards.Report).Take(40))
        {
            var a = Stewards.Areas.FirstOrDefault(x => x.Id == d.Area);
            p.Text($"{Ui.Img(a?.Icon ?? "tab-automate", 14)} {Ui.Dim($"day {d.Day}")}  {Ui.Esc(d.Text)}", 13);
        }
        p.Commit();
    }

    /// <summary>One area's row: its icon, its lesser councillor's title and name, and You / Asks / Steward.</summary>
    private void StewardRow(Pane p, Town t, StewardArea a, bool blurb)
    {
        string id = a.Id;
        var now = Stewards.Mode(t, _sim.Options, id);
        var holder = Stewards.Hand(t, _sim.Options, id) ? Stewards.Holder(t, id) : null;
        string who = holder != null ? Ui.Dim(Ui.Esc(holder.Name)) : Ui.Dim(Ui.Esc(a.Name));
        string head = Ui.Tip($"{Ui.Img(a.Icon, 16)} [b]{a.Title}[/b]  {who}", a.Blurb);
        if (blurb) head += $"\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(a.Blurb)}[/font_size]")}";
        var modes = a.CanAsk ? new[] { (StewardMode.Player, "You"), (StewardMode.Ask, "Asks"), (StewardMode.Steward, "Steward") } : [(StewardMode.Player, "You"), (StewardMode.Steward, "Steward")];
        p.Row(head, modes.Select(m => (m.Item2, true, now == m.Item1, (Action)(() => Act(() =>
        {
            _sim.SetSteward(id, m.Item1);
            GuideEvent("steward");
        })))).ToArray());
    }

    /// <summary>The switches for the areas a window answers, at its top: who runs it, and You / Asks / Steward.</summary>
    private void StewardStrip(Pane p, params string[] areas)
    {
        var t = _sim.Town;
        foreach (string id in areas) StewardRow(p, t, Stewards.Area(id), false);
    }
}
