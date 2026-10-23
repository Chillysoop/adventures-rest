using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using CrownAndCompass.Client.Map;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The notables window: the influential people who aren't heroes. Each notable has a medallion ringed in how they feel
/// about the mayor, a five-step regard bar, the news they pass on and what they give, the favour they ask, and one button
/// for each way of winning them over (with its odds). Below them, the other people of note the town already deals with
/// (institution keepers, guild masters, councillors, friends in the wilds), each a click from their own window.
/// </summary>
public partial class Main
{
    private static Color BandColor(int band) => band switch
    {
        0 => Ui.Bad,
        1 => Ui.Gold,
        2 => Ui.Ink,
        _ => Ui.Good,
    };

    private static Control NotableFace(string icon, int band, bool outsider, float size = 48) =>
        new Portrait(size) { Icon = icon, Tint = outsider ? new Color("8d7bc4") : new Color("c9a35a"), Ring = BandColor(band).Darkened(0.2f) };

    private static string Signed(double v) => $"{(v >= 0 ? "+" : "−")}{Js.Str(Math.Abs(Math.Round(v)))}";

    /// <summary>What a set of a notable's daily effects comes to, as short coloured bits.</summary>
    private static IEnumerable<string> Gives(Dictionary<string, double> effects)
    {
        if (effects.TryGetValue("gold", out double g) && g != 0) yield return Ui.C(g > 0 ? Ui.Good : Ui.Bad, $"{Ui.Img("gold", 13)}{Signed(g)}/day");
        if (effects.TryGetValue("mandate", out double m) && m != 0) yield return Ui.C(m > 0 ? Ui.Good : Ui.Bad, $"{Ui.Img("mandate", 13)}{(m > 0 ? "+" : "−")}{Js.Str(Math.Abs(m * 30))}/month");
    }

    private void RenderNotables()
    {
        var p = _panes[Tab.Notables];
        var t = _sim.Town;
        var r = t.Content.Notables;
        p.Begin();
        var active = Notables.Active(t);
        int warm = active.Count(n => Notables.Band(t, n) >= Notables.Warm);
        int asking = active.Count(n => n.Ask != "");
        p.Card($"{Ui.Img("np-shaking-hands", 22)} [font_size=18][b]{active.Count}[/b][/font_size] {Ui.C(Ui.Ink2, "notables")}\n" +
               $"[font_size=13]{Ui.C(Ui.Good, $"● {warm} warm")}" +
               (asking > 0 ? $"   {Ui.C(Ui.Gold, $"◆ {asking} asking")}" : "") +
               $"   {Ui.Img("np-conversation", 14)} {active.Sum(n => n.Tips)} tips[/font_size]", Ui.Gold);
        if (active.Count == 0) p.Text(Ui.C(Ui.Ink2, "Nobody of note yet. They rise as the town grows."), 13);

        foreach (var n in active.OrderBy(n => n.Ask == "" ? 1 : 0).ThenByDescending(n => n.Regard).ThenBy(n => n.Id))
        {
            var d = Notables.DefOf(t, n);
            int band = Notables.Band(t, n);
            int id = n.Id;
            string who = Notables.NameOf(t, n);
            var hears = r.Hears.GetValueOrDefault(d.Hears);
            var gives = new List<string>();
            if (band == Notables.Friend) gives.AddRange(Gives(d.Friend));
            else if (band >= Notables.Warm) gives.AddRange(Gives(d.Warm));
            else if (band == Notables.Bitter) gives.AddRange(Gives(d.Bitter));
            if (d.Boon != "" && t.Content.Library.Item(d.Boon) is { } boon)
                gives.Add(n.BoonGiven ? Ui.C(Ui.Good, $"★ {Ui.Esc(boon.Name)}") : Ui.C(Ui.Ink2, $"☆ {Ui.Esc(boon.Name)} at Friend"));
            string ear = hears == null ? "" : band >= Notables.Warm
                ? $"{Ui.Img(hears.Icon, 13)} {Ui.C(Ui.Good, Ui.Esc(hears.Name))}{(n.Tips > 0 ? $" ×{n.Tips}" : "")}"
                : $"{Ui.Img(hears.Icon, 13)} {Ui.C(Ui.Ink2, Ui.Esc(hears.Name) + " at Warm")}";
            // A guild's master carries the guild: their mood shows beside the guild's standing.
            var guild = Notables.GuildOf(t, n);
            string title = guild != null ? $"Master of {Guilds.Name(t, guild)}" : d.Title;
            if (guild != null)
            {
                double lean = band == Notables.Friend ? 2 * r.Guild : band >= Notables.Warm ? r.Guild : band == Notables.Bitter ? -r.Guild : 0;
                gives.Insert(0, Ui.Chip("tab-council", $"{Math.Round(guild.Standing)}{(lean != 0 ? $" {(lean > 0 ? "▲" : "▼")}" : "")}", lean > 0 ? Ui.Good : lean < 0 ? Ui.Bad : Ui.Ink2,
                    lean > 0 ? "Guild standing: they talk the members round, so it climbs each day" : lean < 0 ? "Guild standing: they stir the members up against you" : "Guild standing: win them to Warm and the guild warms to you too", 13));
            }
            string text = $"[font_size=16][b]{Ui.Esc(who)}[/b][/font_size]  {Ui.C(Ui.Ink2, Ui.Esc(Ui.Cap(title)))}\n" +
                          $"[font_size=13]{Ui.C(BandColor(band), $"[b]{Ui.Esc(Notables.BandName(t, n))}[/b] {Signed(n.Regard)}")}" +
                          (n.Favours > 0 ? $"   {Ui.C(Ui.Good, $"✔{n.Favours}")}" : "") + $"\n{ear}" +
                          (gives.Count > 0 ? "\n" + string.Join("   ", gives) : "") + "[/font_size]";
            var lead = () => NotableFace(d.Face, band, d.Outsider);
            var under = () => (Control)Foes.Bar("", band + 1, 5, Notables.BandName(t, n), BandColor(band), "How they feel about you");
            p.Row(text, ($"{id}:{band}:{d.Face}", lead, under),
                ("Show", n.AgentId != null, false, () => { if (n.AgentId is int a) ShowAgent(a); }));

            if (Notables.AskOf(t, n) is { } ask)
            {
                string? blocked = Notables.AnswerBlocked(t, n);
                var questSite = ask.Quest != "" ? Quests.Site(t, n.AskSite) : null;
                string help = questSite != null ? "Post the job"
                    : ask.Parts.Count > 0 ? (blocked == null ? "Give it" : "None in stores")
                    : ask.Gold > 0 ? $"Help · {Js.Str(ask.Gold)}g"
                    : $"{Ui.Cap(t.Content.Stories.Skill(ask.Skill)?.Name ?? ask.Skill)} · {Math.Round(Notables.Chance(t, ask.Skill, ask.Dc) * 100)}%";
                // A quest goes on the bounty board on their gold; the regard comes when a company sees it done.
                string quest = questSite == null ? "" : "\n" + Ui.Bullets([
                    Ui.Chip("gold", Js.Str(Notables.QuestReward(t, questSite)), Ui.Gold, "The reward, from their own purse", 12) + " theirs, on the board",
                    Ui.Chip("ui-renown", Notoriety.Suggested(t, questSite).ToString(), Ui.Ink2, "Renown suggested for the company that takes it", 12) + " renown suggested",
                    "Regard when a company sees it done"]);
                p.Row($"[font_size=13]{Ui.C(Ui.Gold, "◆")} {Ui.Esc(Ui.Cap(Notables.AskText(t, n, ask)))}  {Ui.C(Ui.Good, $"+{Js.Str(ask.Gain)}")}  {Ui.C(Ui.Ink2, $"{Math.Max(1, n.AskUntil - t.Day)}d")}{quest}[/font_size]",
                    (help, blocked == null, true, () => Act(() => _sim.AnswerNotable(id, true))),
                    ("Refuse", true, false, () => Act(() => _sim.AnswerNotable(id, false))));
            }

            int wait = Notables.VisitIn(t, n);
            var visits = new List<(string, bool, bool, Action)>();
            foreach (var ap in d.Approaches)
            {
                if (r.Approach(ap) is not { } a) continue;
                string apId = ap;
                string label = wait > 0 ? $"{a.Name} · {wait}d" : $"{a.Name} · {Math.Round(Notables.Chance(t, a.Skill, Notables.DcOf(t, n, a)) * 100)}%";
                visits.Add((label, Notables.VisitBlocked(t, n, apId) == null, false, () => Act(() => _sim.VisitNotable(id, apId))));
            }
            visits.Add(($"Gift · {Js.Str(r.Gift)}g", Notables.VisitBlocked(t, n, "gift") == null, false, () => Act(() => _sim.VisitNotable(id, "gift"))));
            p.Buttons(visits.ToArray());
            foreach (var m in n.Memories.TakeLast(2).Reverse())
                p.Text(Ui.C(Ui.Ink2, "· " + Ui.Esc(m)), 12);
        }

        // The rest of the town's people of note, each answered in their own window.
        var others = new List<(string Text, Tab Tab)>();
        foreach (var s in t.Institutions.Where(s => !s.Shut))
            others.Add(($"{Ui.Img("tab-institutions", 14)} [b]{Ui.Esc(Institutions.KeeperName(t, s))}[/b] {Ui.C(Ui.Ink2, Ui.Esc(Institutions.DefOf(t, s).Name))}  {Ui.C(FeelingColor(s.Regard), Signed(s.Regard))}", Tab.Institutions));
        foreach (var g in t.Guilds.Where(g => g.MasterId != null))
            others.Add(($"{Ui.Img("tab-council", 14)} [b]{Ui.Esc(t.Agent(g.MasterId!.Value).Name)}[/b] {Ui.C(Ui.Ink2, Ui.Esc(Guilds.Name(t, g)))}  {Ui.C(g.Standing >= 50 ? Ui.Good : g.Standing >= 25 ? Ui.Gold : Ui.Bad, Js.Str(Math.Round(g.Standing)))}", Tab.Council));
        foreach (var seat in t.Seats.Where(s => s.HolderId != null))
            others.Add(($"{Ui.Img("mandate", 14)} [b]{Ui.Esc(t.Agent(seat.HolderId!.Value).Name)}[/b] {Ui.C(Ui.Ink2, Ui.Esc(seat.Def.Title))}  {Ui.C(seat.Loyalty >= 50 ? Ui.Good : seat.Loyalty >= 30 ? Ui.Gold : Ui.Bad, Js.Str(Math.Round(seat.Loyalty)))}", Tab.Council));
        foreach (var c in t.Neighbours.Cast.Where(c => !c.Gone).OrderByDescending(c => c.Regard).ThenBy(c => c.Id).Take(6))
            others.Add(($"{Ui.Img("compass", 14)} [b]{Ui.Esc(c.Name)}[/b] {Ui.C(Ui.Ink2, Ui.Esc(t.Settlements[c.SettlementId].Name))}  {Ui.C(FeelingColor(c.Regard), Signed(c.Regard))}", Tab.Diplomacy));
        if (others.Count > 0) p.Heading("Also of note");
        foreach (var (text, tab) in others)
        {
            var go = tab;
            p.Row($"[font_size=13]{text}[/font_size]", ("Open", true, false, () => SetTab(go)));
        }
        p.Commit();
    }

    /// <summary>--befriend: a friend, the warm, the cool and the bitter, twelve days on, one asking a favour.</summary>
    private void StageNotables()
    {
        var t = _sim.Town;
        double[] regard = [100, 70, 35, 5, -30, -70];
        var list = Notables.Active(t);
        for (int i = 0; i < list.Count; i++) list[i].Regard = regard[i % regard.Length];
        _sim.RunDays(12);
        if (list.FirstOrDefault(n => !n.Gone && n.Ask == "" && Notables.Band(t, n) >= Notables.Warm) is { } asker)
        {
            // A quest if they hand one out and a place is open for it, so shots show the job.
            var asks = Notables.DefOf(t, asker).Asks;
            var site = t.Sites.FirstOrDefault(x => x.Known != SiteKnowledge.Unheard && !x.ThoughtCleared && !t.Bounties.Any(b => b.SiteId == x.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken));
            var ask = asks.FirstOrDefault(a => a.Quest != "" && site != null && asker.AgentId != null) ?? asks[0];
            asker.Ask = ask.Id;
            asker.AskSite = ask.Quest != "" ? site!.Def.Id : "";
            asker.AskUntil = t.Day + 4;
        }
        _dirty = true;
    }

    private void ShowAgent(int id)
    {
        if (_map.Crowd.People.Sprites.TryGetValue(id, out var sp) && sp.Visible) Look(sp.P, 22);
        Select(Selection.Agent(id));
    }
}
