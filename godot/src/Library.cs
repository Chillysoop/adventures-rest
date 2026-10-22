using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Client;

/// <summary>
/// The library window: what the scholars study and what moves it, ideas taking shape and where each came from
/// (newcomers, trade, the wilds, books, wizards), the feed of finds, and the library's spellbooks and wizards.
/// Gold only greases the work; the window shows the people, books and finds that drive it.
/// </summary>
public partial class Main
{
    private static readonly string[] Fields = ["arcana", "crafts", "husbandry", "healing", "trade", "war", "lore"];

    private static readonly Dictionary<string, string> FieldIcons = new()
    {
        ["arcana"] = "lore", ["crafts"] = "tools", ["husbandry"] = "food", ["healing"] = "herbs",
        ["trade"] = "gold", ["war"] = "defence", ["lore"] = "books",
    };

    /// <summary>What each kind of spark is, for the hover on its icon.</summary>
    private static readonly Dictionary<string, string> SparkTips = new()
    {
        [Sparks.Newcomer] = "Townsfolk and newcomers", [Sparks.Trader] = "Trade", [Sparks.Built] = "Something built",
        [Sparks.Book] = "A spellbook", [Sparks.Magic] = "Wizards and magic", [Sparks.Hero] = "Heroes",
        [Sparks.Wilds] = "Explorers and the wilds", [Sparks.Slain] = "A monster slain", [Sparks.Ally] = "An ally",
        [Sparks.Known] = "What the town knows", [Sparks.Charter] = "The charter",
        [Sparks.Foe] = "A foe met or broken", [Sparks.Relic] = "A Skywright relic",
    };

    private void RenderLibrary()
    {
        var p = _panes[Tab.Library];
        var t = _sim.Town;
        var r = t.Content.Research;
        var st = t.Research;
        p.Begin();

        var rank = Research.Rank(t);
        int circle = Research.Circle(t);
        p.Card($"[font_size=17][b]{Ui.Esc(Ui.Cap(rank.Name))}[/b][/font_size]\n" + LibChips(
            ("lore", $"{st.Known.Count}/{st.Revealed.Count}", Ui.Good, "Discoveries made / ideas found"),
            ("compass", $"{r.Discoveries.Count - st.Revealed.Count} ?", null, "Ideas still hidden: the town and the wilds reveal them"),
            ("books", $"{st.Books.Count}", null, "Spellbooks"),
            ("tab-library", circle > 0 ? Research.Ordinal(circle) : "—", Ui.Gold, "Highest circle of town magic"),
            ("heroes", $"{Math.Round(Research.Power(t))}", null, "Library power: book circles + wizard levels")), Ui.Gold);

        // What the scholars are on, and what moves it.
        p.Heading("The scholars");
        double lore = Research.Lore(t);
        if (st.Study is string id)
        {
            var d = Research.Def(t, id);
            double need = Research.LoreCost(t, d), have = st.Progress.Get(id, 0);
            int days = lore > 0 ? (int)Math.Ceiling(Math.Max(0, need - have) / lore) : 0;
            p.Meter(d.Name, have / need, lore > 0 ? $"{Ui.Days(days)} · {Js.Str(Math.Round(lore, 1))}/day" : "no lore coming in");
            // The steps along the way, each a roll by the lead scholar when the lore reaches its mark.
            int passed = st.Steps.Get(id, 0);
            var stages = r.Stages;
            if (stages.Count > 0)
            {
                var marks = stages.Select((x, i) => Ui.Img(i < passed ? "ui-yes" : i == passed ? "d20" : "ui-time", 13)).ToList();
                string next = "";
                if (Research.NextStep(t, d) is { } stage)
                {
                    string skill = Research.StepSkill(t, d, stage);
                    var lead = Research.Lead(t, skill);
                    int bonus = lead != null ? Skills.Bonus(t, lead, skill) : Skills.WatchBonus(t, skill) + r.Clerks;
                    int dc = Research.StepDc(t, d, passed);
                    next = "  " + Ui.Tip(Ui.C(Ui.Ink2, $"{Ui.Esc(Ui.Cap(stage.Name))} at {Math.Round(stage.At * 100)}%: {Ui.Esc(Skills.Name(t, skill))} d20{Odds.Sign(bonus)} vs {dc}"),
                        $"{(lead != null ? lead.Name : "The town hall's clerks")} lead the work. A miss is a dead end that costs some lore; a natural 1 is a mishap; a natural 20 jumps ahead.");
                }
                p.Text($"{string.Join(" ", marks)}{next}", 12);
            }
        }
        else if (Research.Open(t).Count > 0) p.Text(Lb(Ui.C(Ui.Gold, "[b]Idle[/b] · pick an idea below"), "ui-warn"));
        if (st.Notes > 0) p.Text(Lb(Ui.C(Ui.Ink2, $"{Js.Str(Math.Round(st.Notes))} lore of notes waiting for the next study"), "books"), 13);

        // Lore a day by kind of source, one chip each, the sources it sums on hover.
        var moves = Research.Sources(t).GroupBy(s => SourceIcon(s.Why))
            .Select(g => (g.Key, "+" + Js.Str(Math.Round(g.Sum(s => s.Lore), 1)), (Godot.Color?)Ui.Good,
                string.Join("\n", g.Select(s => $"{Ui.Cap(s.Why)} +{Js.Str(Math.Round(s.Lore, 1))}")))).ToList();
        moves.Add(("gold", $"×{Js.Str(r.Funding[st.Funding].Lore)}", Ui.Gold, "Funding: gold only greases the work"));
        p.Text(LibChips(moves.ToArray()), 13);
        var finds = st.Feed.Where(n => n.Lore > 0 || n.Icon == "d20").Reverse().Take(4).ToList();
        if (finds.Count > 0)
            p.Text(string.Join("\n", finds.Select(n => Lb($"{Ui.Esc(Ui.Cap(n.Text))} {(n.Lore > 0 ? Ui.C(Ui.Good, "+" + Js.Str(n.Lore)) + " " : "")}{Ui.C(Ui.Ink2, $"day {n.Day}")}", n.Icon))), 13);

        var buttons = new List<(string, bool, bool, Action)>();
        for (int i = 0; i < r.Funding.Count; i++)
        {
            int level = i;
            double cost = Research.FundingCost(t, i);
            buttons.Add(($"×{Js.Str(r.Funding[i].Lore)}{(cost > 0 ? $" · {Js.Str(cost)}g" : "")}", st.Funding != i, st.Funding == i, () => Act(() => _sim.FundResearch(level))));
        }
        p.Buttons(buttons.ToArray());

        // Ideas taking shape: a spark holds, the idea isn't clear yet.
        if (st.Leads.Count > 0)
        {
            p.Heading("Taking shape");
            p.Text(string.Join("\n", st.Leads.OrderBy(l => l.Key, StringComparer.Ordinal).Select(l =>
            {
                var d = Research.Def(t, l.Key);
                int need = Math.Max(1, Research.BrewDays(t, d));
                return Lb($"{Ui.Img(FieldIcons[d.Field], 14)} ? {Ui.C(Ui.Ink2, Ui.Esc(l.Value.Why))} {Pips(l.Value.Days, need)}", l.Value.Icon);
            })), 13);
        }

        RenderSkyworks(p, t);

        // The tree: what this town could study, where each idea came from, what it still needs.
        p.Heading("Discoveries");
        if (st.Revealed.Count == 0)
            p.Note("No ideas yet. Newcomers, trade, buildings, books, explorers' finds and monsters your companies bring down each spark their own.");
        foreach (string field in Fields)
        {
            var defs = st.Revealed.Keys.Select(k => Research.Def(t, k)).Where(d => d.Field == field).OrderBy(d => Research.IsKnown(t, d.Id)).ThenBy(d => d.Rank).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();
            if (defs.Count == 0) continue;
            p.Text($"{Ui.Img(FieldIcons[field], 16)} [b]{Ui.Cap(field == "arcana" ? "town magic" : field)}[/b]", 13);
            foreach (var d in defs)
            {
                string did = d.Id;
                bool known = Research.IsKnown(t, d.Id);
                var missing = Research.Missing(t, d);
                string state = known ? Tag("known", Ui.Good)
                    : st.Study == d.Id ? Tag("studying", Ui.Gold)
                    : missing.Count > 0 ? Tag("needs " + string.Join(", ", missing))
                    : Tag($"{Js.Str(Research.LoreCost(t, d) - st.Progress.Get(d.Id, 0))} lore");
                string icon = st.RevealIcon.Get(d.Id, Sparks.Known);
                string body = $"{Ui.Img(icon, 15)} [b]{Ui.Esc(d.Name)}[/b]  {state}\n" +
                              Ui.C(Ui.Ink2, $"[font_size=12]{Effects(d)}\nfrom {Ui.Esc(st.Revealed[d.Id])}[/font_size]");
                body = $"[hint={Ui.Esc(d.Text).Replace("]", ")")}]{body}[/hint]";
                if (known || st.Study == d.Id || missing.Count > 0) p.Row(body);
                else p.Row(body, ("Study", true, st.Study == null, () => Act(() => _sim.StudyDiscovery(did))));
            }
        }

        // The feed: why each idea appeared and what moved the scholars, newest first.
        if (st.Feed.Count > 0)
        {
            p.Heading("News from the library");
            p.Text(string.Join("\n", st.Feed.AsEnumerable().Reverse().Take(10).Select(n =>
                Lb($"{Ui.C(Ui.Ink2, $"day {n.Day}")}  {Ui.Esc(Ui.Cap(n.Text))}{(n.Lore > 0 ? " " + Ui.C(Ui.Good, "+" + Js.Str(n.Lore)) : "")}", n.Icon))), 13);
        }

        p.Heading("Spellbooks");
        if (st.Books.Count == 0) p.Note("No spellbooks yet. They come home from delves, on caravans and from wizards' bastions.");
        foreach (var b in st.Books.OrderByDescending(b => b.Circle).ThenBy(b => b.Id).Take(8))
            p.Text(Lb($"[b]{Research.Ordinal(b.Circle)}[/b] {Ui.Esc(b.School)} {Ui.C(Ui.Ink2, $"· {Ui.Esc(b.From)}")}", "books"), 13);
        if (st.Books.Count > 8) p.Note($"and {st.Books.Count - 8} more.");

        p.Heading("Wizards in town");
        var wizards = t.Heroes.Where(a => a.Hero!.Cls.Id == "wizard").OrderByDescending(a => a.Hero!.Level).ThenBy(a => a.Id).ToList();
        if (wizards.Count == 0) p.Note("No wizards yet. A library with books enough draws them, each stronger than the last.");
        foreach (var a in wizards)
            p.Text(Lb($"[b]{Ui.Esc(a.Name)}[/b] {Ui.C(Ui.Ink2, $"lvl {a.Hero!.Level} · reads {Research.Ordinal(Math.Min(9, (a.Hero.Level + 1) / 2))} circle{(st.Drawn.Contains(a.Id) ? " · drawn by the library" : "")}{(a.IsAway ? " · away" : "")}")}", "lore"), 13);
        p.Commit();
    }

    /// <summary>
    /// The Skywright relics (where each lies, who holds it, the foe racing for them) and the Lofted City: its stages
    /// while it rises, then where it hovers and where it can sail. Shown once the town has heard of any of it.
    /// </summary>
    private void RenderSkyworks(Pane p, Town t)
    {
        var lr = t.Content.Lofted;
        var ls = t.Lofted;
        bool heard = (lr.Reveal != "" && Research.IsKnown(t, lr.Reveal)) || Lofted.Held(t) > 0 || ls.Relics.Any(r => r.Holder != "");
        var city = Lofted.City(t);
        if (!heard && city == null) return;

        p.Heading("Skywright relics");
        p.Text(string.Join("\n", ls.Relics.Select(rs =>
        {
            var def = Lofted.Relic(t, rs.Id);
            var site = Quests.Site(t, rs.SiteId);
            string where = site == null || site.Known == SiteKnowledge.Unheard ? "?" : Ui.Esc(site.Def.Name);
            string state = rs.Holder == "town" ? Ui.C(Ui.Good, "✓ home")
                : rs.Holder != "" ? Ui.C(Ui.Bad, $"⚔ {Ui.Esc(Lofted.HolderName(t, rs))}")
                : Ui.C(Ui.Ink2, where);
            string body = $"[b]{Ui.Esc(Ui.Cap(def?.Name ?? rs.Id))}[/b]  {state}";
            return $"[hint={Ui.Esc(def?.Text ?? "").Replace("]", ")")}]{Lb(body, rs.Holder == "town" ? Sparks.Relic : rs.Holder != "" ? "rivals" : "compass")}[/hint]";
        })), 13);
        if (Lofted.Rival(t) is { Alive: true } rival && ls.Relics.Any(r => r.Holder != "town"))
        {
            int held = ls.Relics.Count(r => r.Holder == $"menace:{rival.Id}");
            string keel = ls.Raised ? Ui.C(Ui.Bad, " · dark keel risen")
                : ls.RaiseDay is int day ? Ui.C(Ui.Bad, $" · dark keel in {Ui.Days(Math.Max(0, day - t.Day))}")
                : $" · keel at {lr.Race.RaiseAt}";
            p.Text(Lb($"Racing you: [b]{Ui.Esc(Ui.Cap(rival.Name))}[/b] {Ui.C(Ui.Ink2, $"{held} held{keel}")}", "rivals"), 13);
        }

        p.Heading("The Lofted City");
        if (city == null)
        {
            p.Note(Lofted.Locked(t, t.Content.Building("lofted_spire")) is { } why ? $"✗ {why}" : "Pick a size in the build menu (Government): spire, isle or city.");
            return;
        }
        if (!city.Complete)
        {
            int stage = GrandWorks.Current(city), n = city.Def.Stages.Count;
            p.Meter(Ui.Cap(city.Name), city.BuildProgress / city.Def.BuildHours, $"{stage + 1}/{n} · {city.Def.Stages[Math.Clamp(stage, 0, n - 1)].Name}");
            return;
        }
        string at = Lofted.Sailing(t)
            ? $"sailing · {Ui.Days((int)Math.Ceiling((ls.ArriveTick - t.Tick) / 24.0))}"
            : ls.RegionId is int rid ? $"over {Ui.Esc(t.Regions[rid].Name)}" : $"over {Ui.Esc(t.Name)}";
        p.Text(Lb($"[b]{Ui.Esc(Ui.Cap(city.Name))}[/b]  {Ui.C(Ui.Gold, at)}", Sparks.Relic), 14);
        var scope = lr.Scope(city.Def.Id);
        var sail = new List<(string, bool, bool, Action)>();
        if (ls.RegionId != null) sail.Add(("⌂ Home", Lofted.SailBlocked(t, -1) == null, true, () => Act(() => _sim.SailLofted(-1))));
        foreach (var g in t.Regions.Where(g => g.Known && g.Id != ls.RegionId).OrderByDescending(g => ls.Relics.Any(r => r.Holder == "" && Quests.Site(t, r.SiteId)?.RegionId == g.Id)).ThenByDescending(g => g.Days).ThenBy(g => g.Id).Take(6))
        {
            int gid = g.Id;
            sail.Add(($"⛵ {g.Name}", Lofted.SailBlocked(t, gid) == null, false, () => Act(() => _sim.SailLofted(gid))));
        }
        if (sail.Count > 0) p.Buttons(sail.ToArray());
        if (scope != null) p.Note($"{Js.Str(scope.SailGold)}g a day aloft · companies set out from under it");
    }

    /// <summary>For shots: every relic home, lofting known, and the city of this size standing (or rising some eighths) beside the town.</summary>
    private void StageLofted(string arg)
    {
        var t = _sim.Town;
        var parts = arg.Split(':');
        var d = t.Content.Buildings.FirstOrDefault(b => b.Id == parts[0] && b.Lofted);
        if (d == null) return;
        _sim.RunDays(1);
        foreach (var rs in t.Lofted.Relics) rs.Holder = "town";
        t.Research.Known.Add(t.Content.Lofted.Reveal);
        t.Research.Known.Add(t.Content.Lofted.Unlock);
        if (Planning.CivicLot(t, d) is not { } lot) return;
        bool rising = parts.Length > 1 && int.TryParse(parts[1], out _);
        var b = t.AddBuilding(d, lot, null, $"the {d.Name}", !rising);
        if (rising)
        {
            b.Stage = d.Stages.Count;
            b.BuildProgress = d.BuildHours * Math.Clamp(int.Parse(parts[1]), 0, 7) / 8.0;
        }
    }

    /// <summary>One point of a list, its icon first (with the source's meaning on hover).</summary>
    private static string Lb(string text, string icon) =>
        SparkTips.TryGetValue(icon, out string? tip) ? $"[hint={tip}]{Ui.Img(icon, 15)}[/hint] {text}" : $"{Ui.Img(icon, 15)} {text}";

    private static string LibChips(params (string Icon, string Value, Godot.Color? Color, string Tip)[] chips) =>
        string.Join("    ", chips.Select(c => $"[hint={c.Tip}]{Ui.Img(c.Icon, 16)} {(c.Color is Godot.Color col ? Ui.C(col, $"[b]{c.Value}[/b]") : $"[b]{c.Value}[/b]")}[/hint]"));

    /// <summary>Days a lead has held, as filled and empty pips.</summary>
    private static string Pips(int have, int need)
    {
        int n = Math.Min(8, need), full = (int)Math.Round(Math.Min(1.0, (double)have / need) * n);
        return Ui.C(Ui.Gold, new string('●', full)) + Ui.C(Ui.Ink2, new string('○', n - full));
    }

    /// <summary>The icon for a lore source, from its words (Research.Sources).</summary>
    private static string SourceIcon(string why) =>
        why.Contains("clerk") ? "mandate"
        : why.Contains("townsfolk") ? "population"
        : why.Contains("hedge") || why.Contains("wizard") ? "lore"
        : why.Contains("caster") ? "favour"
        : why.Contains("spellbook") ? "books"
        : why.Contains("already knows") ? "tab-library"
        : why.Contains(" the ") || why.Contains('×') ? "tab-build"
        : "tab-institutions";

    private static string Effects(DiscoveryDef d)
    {
        var e = new List<string>();
        foreach (var (good, f) in d.Yields) e.Add($"{good} {(f >= 1 ? "+" : "−")}{Math.Round(Math.Abs(f - 1) * 100)}%");
        if (d.Mood != 0) e.Add($"mood {(d.Mood > 0 ? "+" : "−")}{Js.Str(Math.Abs(d.Mood))}");
        if (d.Arrivals != 0) e.Add("more settlers");
        if (d.Build != 0) e.Add($"build +{Math.Round(d.Build * 100)}%");
        if (d.Gold != 0) e.Add($"{(d.Gold > 0 ? "+" : "−")}{Js.Str(Math.Abs(d.Gold))}g/day");
        if (d.Heroes != 0) e.Add($"+{d.Heroes} {(d.Heroes == 1 ? "hero" : "heroes")}");
        if (d.Defence != 0) e.Add($"defence +{Js.Str(d.Defence)}");
        if (d.Fireproof != 0) e.Add($"fires −{Math.Round(d.Fireproof * 100)}%");
        if (d.Ward != 0) e.Add($"undercity −{Math.Round(d.Ward * 100)}%");
        if (d.Xp != 0) e.Add("better-trained heroes");
        if (d.Scry != 0) e.Add($"scrying −{Math.Round(d.Scry * 100)}%");
        if (d.Lore != 0) e.Add($"+{Js.Str(d.Lore)} lore/day");
        return string.Join(" · ", e);
    }
}
