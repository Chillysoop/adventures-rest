using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The town broadsheet, read from the Ledger window: the latest issue laid out like a front page, and the
/// stories the mayor can pay the printer to run in the next one.
/// </summary>
public partial class Main
{
    private bool _paperShown;
    private VBoxContainer _paperBox = null!;
    private Pane _paper = null!;

    private void BuildPaper(VBoxContainer box)
    {
        _paperBox = new VBoxContainer { Visible = false };
        _paperBox.AddThemeConstantOverride("separation", 6);
        box.AddChild(_paperBox);
        _paper = new Pane(_paperBox);
    }

    private void ShowPaper(bool on)
    {
        _paperShown = on;
        _paperBox.Visible = on;
        _ledgerText.Visible = !on;
        _paper.Invalidate();
        if (on) GuideEvent("tab:Broadsheet");
    }

    /// <summary>The hero a front page would cry up: the one in the inspector, else the town's most renowned.</summary>
    private Agent? PaperHero()
    {
        var t = _sim.Town;
        if (_map.Selection is { Kind: "agent" } sel && sel.Id >= 1 && sel.Id <= t.Agents.Count && t.Agent(sel.Id) is { Hero: not null, Departed: false, Dead: false } a) return a;
        return t.Heroes.OrderByDescending(h => h.Hero!.Renown).ThenBy(h => h.Id).FirstOrDefault();
    }

    private void RenderPaper()
    {
        var t = _sim.Town;
        var p = _paper;
        p.Begin();
        var issue = Broadsheet.Latest(t);
        string paper = Broadsheet.Name(t);
        if (issue == null)
        {
            p.Heading(paper);
            p.Note(Broadsheet.Printing(t)
                ? $"The first issue goes on sale on day {Broadsheet.NextDay(t)}."
                : $"No printer has set up yet. One will once {t.Content.Broadsheet.Population} townsfolk live here.");
        }
        else
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"[center]{Ui.C(Ui.Ink2, $"[font_size=11]ISSUE {issue.Number} · DAY {issue.Day} · ONE COPPER[/font_size]")}\n");
            sb.Append($"[font_size=24][b]{Ui.Esc(paper)}[/b][/font_size]\n");
            sb.Append($"{Ui.C(Ui.Line, "━━━━━━━━━━━━━━━━━━━━━━━━━━━━")}\n");
            sb.Append($"[font_size=18][b]{Ui.Esc(Broadsheet.Headline(issue.Headline))}[/b][/font_size][/center]\n");
            if (issue.Exposed) sb.Append(Ui.C(Ui.Bad, "[font_size=12]Found out: the whole town knows who paid for this one.[/font_size]\n"));
            else if (issue.Slant != "") sb.Append(Ui.C(Ui.Ink2, "[font_size=12]Paid for by the mayor's office.[/font_size]\n"));
            foreach (var s in issue.Stories)
                sb.Append($"\n{Ui.C(Ui.Gold, $"[font_size=10][b]{s.Section.ToUpperInvariant()}[/b][/font_size]")}\n{Ui.Esc(s.Text)}\n");
            sb.Append($"\n{Ui.C(Ui.Gold, "[font_size=10][b]MARKET PRICES[/b][/font_size]")}\n{Ui.C(Ui.Ink2, issue.Prices)}\n");
            p.Card(sb.ToString());
        }

        if (!Broadsheet.Printing(t))
        {
            p.Commit();
            return;
        }
        p.Heading("Next issue's front page");
        var bought = t.Content.Broadsheet.Slant(t.Press.Slant);
        if (bought != null)
        {
            string who = bought.NeedsHero && t.Press.SlantHero > 0 ? $" ({t.Agent(t.Press.SlantHero).Name})" : "";
            p.Note($"Paid for: {bought.Name}{who}. It runs on day {Broadsheet.NextDay(t)}.");
            p.Commit();
            return;
        }
        double price = Broadsheet.Price(t);
        var hero = PaperHero();
        foreach (var s in t.Content.Broadsheet.Slants)
        {
            int? heroId = s.NeedsHero ? hero?.Id : null;
            string? why = Broadsheet.Blocked(t, s, heroId);
            string label = s.NeedsHero && hero != null ? $"Cry up {hero.GivenName} · {Js.Str(price)}g" : $"{s.Name} · {Js.Str(price)}g";
            var (pleased, angered) = Regard.Reactions(t, s.Tags);
            string mood = pleased.Count + angered.Count == 0 ? ""
                : "\n" + string.Join(" · ", new[]
                {
                    pleased.Count > 0 ? Ui.C(Ui.Good, $"Pleases {string.Join(", ", pleased.Take(3).Select(a => a.GivenName))}{(pleased.Count > 3 ? $" +{pleased.Count - 3}" : "")}") : "",
                    angered.Count > 0 ? Ui.C(Ui.Bad, $"Angers {string.Join(", ", angered.Take(3).Select(a => a.GivenName))}{(angered.Count > 3 ? $" +{angered.Count - 3}" : "")}") : "",
                }.Where(x => x != ""));
            string id = s.Id;
            p.Row($"[b]{Ui.Esc(s.Name)}[/b]\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(s.Text)}[/font_size]")}{mood}" + (why != null ? $"\n{Ui.C(Ui.Ink2, $"[font_size=11]Not now: {Ui.Esc(why)}[/font_size]")}" : ""),
                (label, why == null, true, () => Act(() => _sim.SlantBroadsheet(id, heroId))));
        }
        if (hero != null) p.Note("To cry up someone else, select that hero first.");
        p.Commit();
    }
}
