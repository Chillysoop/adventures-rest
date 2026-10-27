using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The Council window's politics: the form of government, town votes and policies on the Laws page, the open votes and
/// the moot in the Hall, and the Blocs page (the lens, each bloc, asks, situations and radicals). Everything reads
/// <see cref="Politics"/>.
/// </summary>
public partial class Main
{
    private BlocLens? _lens;

    /// <summary>The blocs a law change pleases and angers, as their icons, green and red, with the change's cost in a hover.</summary>
    private static string BlocChips(Town t, string law, string option)
    {
        var parts = new List<string>();
        foreach (var b in Politics.Blocs(t))
        {
            double v = Politics.Reaction(t, b, law, option);
            if (Math.Abs(v) < 0.01) continue;
            parts.Add(Ui.Tip($"{Ui.Img(b.Icon, 13)}{Ui.C(v > 0 ? Ui.Good : Ui.Bad, v > 0 ? "▲" : "▼")}", $"{Ui.Cap(b.Voice)} {(v > 0 ? "+" : "")}{Math.Round(v)}"));
        }
        return string.Join(" ", parts);
    }

    /// <summary>What a law in force does to the blocs, as icons.</summary>
    private static string InForce(Town t, string law)
    {
        string now = Politics.Current(t, law);
        var parts = new List<string>();
        foreach (var b in Politics.Blocs(t))
        {
            double v = Politics.Moves(t, b, law, now);
            if (Math.Abs(v) < 0.5) continue;
            parts.Add(Ui.Tip($"{Ui.Img(b.Icon, 13)}{Ui.C(v > 0 ? Ui.Good : Ui.Bad, v > 0 ? "▲" : "▼")}", $"{Ui.Cap(b.Voice)} {(v > 0 ? "+" : "")}{Math.Round(v)}"));
        }
        return string.Join(" ", parts);
    }

    /// <summary>A button's hover for a law change: what it costs or that it goes to the vote, and who it pleases and angers.</summary>
    private static string ChangeTip(Town t, string law, string option)
    {
        var lines = new List<string>();
        if (Politics.Blocked(t, law, option) is { } why && why != "already so") lines.Add(Ui.Cap(why));
        lines.Add(Politics.Voted(t, law) ? "Goes to a town vote" : $"Costs {Js.Str(Politics.Cost(t, law, option))} Mandate");
        var (pleased, angered) = Politics.Reactions(t, law, option);
        if (pleased.Count > 0) lines.Add("▲ " + Ui.Cap(Blocs.Voices(pleased)));
        if (angered.Count > 0) lines.Add("▼ " + Ui.Cap(Blocs.Voices(angered)));
        return string.Join("\n", lines);
    }

    /// <summary>Change a law's label on its button: with "(vote)" when the town decides it.</summary>
    private static string Label(Town t, string law, string name) => Politics.Voted(t, law) ? $"{name} (vote)" : name;

    /// <summary>The Hall's word on politics: town votes open, the coming moot, and the mayor's poll.</summary>
    private void RenderVotes(Pane p, Town t)
    {
        var open = t.Politics.Referenda.Where(x => x.Yes < 0).ToList();
        if (open.Count > 0)
        {
            p.Heading("Town votes", "vote-aye");
            foreach (var x in open)
            {
                double s = Politics.Support(t, x.Law, x.Option);
                p.Row($"{Ui.Img("vote-aye", 15)} [b]{Ui.Esc(Politics.Describe(t, x.Law, x.Option))}[/b]\n[font_size=12]{Ui.Chip("population", $"{Math.Round(s * 100)}% for", s > 0.5 ? Ui.Good : Ui.Bad, "How the town would vote today, bloc by bloc", 13)}   {Ui.Img("deadline", 13)} {Ui.Days(Math.Max(1, x.Due - t.Day))}   {BlocChips(t, x.Law, x.Option)}[/font_size]");
            }
        }
        if (Politics.NextMoot(t) is int day)
        {
            int days = Math.Max(1, day - t.Day);
            var f = Politics.Form(t);
            double poll = Politics.Poll(t);
            p.Chips(
                Ui.Chip("vote-aye", $"Moot {Ui.Days(days)}", days <= Politics.Rules(t).Moot.WarnDays ? Ui.Gold : Ui.Ink, "At the year's end every household votes" + (f.Seats == "elected" ? " for the council" : "") + (f.Mayor == "elected" ? " and for the mayor" : ""), 13),
                f.Mayor == "elected" ? Ui.Chip("face-crown", $"{Math.Round(poll * 100)}%", poll >= Politics.Rules(t).Moot.Keep ? Ui.Good : Ui.Bad, $"Your share of the vote if the moot were today: blocs' feeling by size, and your Mandate. Below {Math.Round(Politics.Rules(t).Moot.Keep * 100)}% you are turned out.", 13) : "");
        }
    }

    /// <summary>The Laws page's head: the form of government, the town votes and the policies.</summary>
    private void RenderGovernment(Pane p, Town t)
    {
        var r = Politics.Rules(t);
        var now = Politics.Form(t);
        p.Heading("Government", "tab-council");
        p.Card($"{Ui.Img(now.Icon, 18)} [b]{Ui.Esc(now.Name)}[/b]  {Tag(now.Kind, now.Liberty > 0 ? Ui.Good : now.Liberty < 0 ? Ui.Bad : Ui.Gold)}\n[font_size=12]{Ui.Esc(now.Text)}\n{InForce(t, "form")}[/font_size]", Ui.Gold);
        p.Text(string.Join("   ", r.Forms.Where(f => f.Id != now.Id).Select(f => Ui.Tip($"{Ui.Img(f.Icon, 14)} {Ui.Esc(f.Name)}", $"{f.Kind}\n{f.Text}"))), 12);
        p.TipButtons(r.Forms.Where(f => f.Id != now.Id).Select(f =>
        {
            string id = f.Id;
            return (Label(t, "form", f.Name), Politics.Blocked(t, "form", id) == null, false, (Action)(() => Act(() => _sim.SetGovernment(id))), $"{f.Kind}: {f.Text}\n{ChangeTip(t, "form", id)}");
        }).ToArray());

        if (now.AllowVotes)
        {
            p.Heading("Town votes", "vote-aye");
            var on = Politics.Votes(t);
            foreach (var m in r.Matters)
            {
                bool voted = on.Contains(m.Id);
                string id = m.Id;
                p.Row($"{Ui.Img(m.Icon, 15)} [b]{Ui.Esc(m.Name)}[/b]  {(voted ? Tag("The town votes", Ui.Good) : Tag("You decide", Ui.Ink2))}");
                p.TipButtons((voted ? "Take it back" : "Put to the town", Politics.VoteBlocked(t, id, !voted) == null, false, (Action)(() => Act(() => _sim.SetTownVote(id, !voted))),
                    $"{m.Text}\nCosts {Js.Str(r.MatterChange)} Mandate"));
            }
        }

        p.Heading("Policies", "decree");
        foreach (var pol in r.Policies)
        {
            var lv = Politics.Level(t, pol);
            string law = "policy:" + pol.Id, polId = pol.Id;
            double cost = lv.Gold + lv.PerResident * t.Population;
            p.Row($"{Ui.Img(pol.Icon, 15)} {Ui.Tip($"[b]{Ui.Esc(pol.Name)}[/b]", pol.Text)}  {Tag(lv.Name, lv.Id == pol.Levels[0].Id ? Ui.Ink2 : Ui.Gold)}\n[font_size=12]{(cost > 0 ? Ui.Chip("gold", $"−{Math.Round(cost)}g/day", Ui.Ink, "What it costs the treasury each day", 13) + "  " : "")}{InForce(t, law)}[/font_size]");
            p.TipButtons(pol.Levels.Where(l => l.Id != lv.Id).Select(l =>
            {
                string level = l.Id;
                return (Label(t, law, l.Name), Politics.Blocked(t, law, level) == null, false, (Action)(() => Act(() => _sim.SetPolicy(polId, level))), $"{l.Text}\n{ChangeTip(t, law, level)}");
            }).ToArray());
        }
        p.Note("Every law pleases some blocs and angers others. A change takes 10 days to be felt; turning it back soon costs double.");
    }

    /// <summary>The Blocs page: the lens, each bloc, what they ask, the situations in town, and any radicals.</summary>
    private void RenderBlocs(Pane p, Town t)
    {
        var r = Politics.Rules(t);
        _lens ??= new BlocLens();
        var lens = _lens;
        lens.Show(t);
        p.Add("lens", () => lens);
        p.Meter("Trust", t.Politics.Trust, $"{Math.Round(t.Politics.Trust)}", t.Politics.Trust < 35);

        // An ask waiting on the mayor's word, and promises given.
        if (Politics.Ask(t) is { } ask && Blocs.Def(t, ask.Bloc) is { } ab)
        {
            p.Heading("Asks", "vote-aye");
            p.Card($"{Ui.Img(ab.Icon, 16)} [b]{Ui.Esc(Ui.Cap(ab.Voice))}[/b] ask for [b]{Ui.Esc(Politics.Say(t, ask.Law, ask.Option))}[/b]\n[font_size=12]{Ui.Img("deadline", 13)} answer in {Ui.Days(Math.Max(1, r.Demands.AskDays - (t.Day - ask.Day)))}   {BlocChips(t, ask.Law, ask.Option)}[/font_size]", Ui.Gold);
            int id = ask.Id;
            p.TipButtons(
                ("Promise", true, true, (Action)(() => Act(() => _sim.AnswerBloc(id, true))), $"Do it within {r.Demands.DueDays} days: they warm a lot and trust rises. Break it and every bloc remembers."),
                ("Turn away", true, false, (Action)(() => Act(() => _sim.AnswerBloc(id, false))), "They cool a little."));
        }
        var promised = t.Politics.Demands.Where(d => d.Promised).ToList();
        if (promised.Count > 0)
            p.Bullets(promised.Select(d => $"{Ui.Img(Blocs.Def(t, d.Bloc)?.Icon ?? "vote-aye", 13)} {Ui.Esc(Politics.Say(t, d.Law, d.Option))}  {Ui.C(d.Due - t.Day <= 5 ? Ui.Bad : Ui.Ink2, Ui.Days(Math.Max(0, d.Due - t.Day)))}"), "ui-yes");

        p.Heading("Blocs", "population");
        foreach (var b in Politics.Blocs(t).OrderByDescending(b => Politics.Size(t, b)))
        {
            double f = Politics.Feeling(t, b.Id);
            var cell = Politics.CellOf(t, b.Id);
            var top = Politics.Pushes(t, b).OrderByDescending(x => Math.Abs(x.Points)).Take(4).ToList();
            string pushes = string.Join("  ", top.Select(x => Ui.Tip(Ui.C(x.Points > 0 ? Ui.Good : Ui.Bad, x.Points > 0 ? "▲" : "▼") + Ui.Img(LawIcon(t, x.Law), 12), $"{x.What} {(x.Points > 0 ? "+" : "")}{Math.Round(x.Points)}")));
            p.Row($"{Ui.Img(b.Icon, 16)} [b]{Ui.Esc(Ui.Cap(b.Voice))}[/b]  {Tag($"{Math.Round(f)}", f >= 15 ? Ui.Good : f <= -15 ? Ui.Bad : Ui.Gold)}{(cell != null ? "  " + Ui.Chip("suspicion", "Radicals", Ui.Bad, RadicalTip(cell), 13) : "")}\n[font_size=12]{Ui.Chip("population", $"{Math.Round(Politics.Size(t, b))}", Ui.Ink2, "Townsfolk in it, each by how much they belong", 13)}   {pushes}[/font_size]");
        }

        var on = r.Situations.Where(s => t.Politics.Situations.ContainsKey(s.Id)).ToList();
        if (on.Count > 0)
        {
            p.Heading("Situations", "unrest");
            foreach (var s in on)
            {
                double v = Politics.Reading(t, s.Reading);
                var hurt = Politics.Blocs(t).Where(b => s.Hurts.Any(b.Likes.Contains)).ToList();
                p.Row($"{Ui.Img(s.Icon, 15)} [b]{Ui.Esc(s.Name)}[/b]  {Ui.Chip("ui-time", $"{Ui.Days(Math.Max(1, t.Day - t.Politics.Situations[s.Id]))}", Ui.Ink2, "How long it has gone on", 13)}\n[font_size=12]{Ui.Tip(Ui.C(Ui.Bad, $"{v:0.#}"), $"{s.Fed}: {v:0.#}. It ends below {s.End:0.#}.")}   {string.Join(" ", hurt.Select(b => Ui.Tip($"{Ui.Img(b.Icon, 13)}{Ui.C(Ui.Bad, "▼")}", Ui.Cap(b.Voice))))}{(s.Unrest > 0 ? "   " + Ui.Chip("unrest", $"+{s.Unrest:0.#}/day", Ui.Bad, "Unrest a day while it lasts", 13) : "")}[/font_size]");
            }
        }
    }

    private string RadicalTip(Cell c)
    {
        string[] steps = ["slogans", "a riot", "sabotage", "an attempt on the mayor"];
        return $"Next: {steps[Math.Min(c.Stage, steps.Length - 1)]}. A strong watch breaks them up; win the bloc back and they melt away.";
    }

    /// <summary>The icon of a law key, or of a situation or trust (the lens and the bloc rows).</summary>
    internal static string LawIcon(Town t, string law)
    {
        var r = Politics.Rules(t);
        if (law == "trust") return "vote-aye";
        if (law.StartsWith("situation:", StringComparison.Ordinal)) return r.Situations.FirstOrDefault(s => s.Id == law[10..])?.Icon ?? "unrest";
        return Politics.Kind(law) switch
        {
            "tax" => "gold",
            "gate" => "defence",
            "service" => "army-law",
            "trial" => "trial",
            "policy" => r.Policy(law[7..])?.Icon ?? "decree",
            "form" => Politics.Form(t).Icon,
            _ => "decree",
        };
    }
}
