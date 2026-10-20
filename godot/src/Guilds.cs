using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Client;

public partial class Main
{
    /// <summary>The Council window's trade guilds: master, hall, standing, the seat each leans on, its demand or strike, and the levers.</summary>
    private void RenderGuilds(Pane p, Town t)
    {
        var r = Guilds.Rules(t);
        p.Heading("Guilds");
        if (t.Guilds.Count == 0)
        {
            p.Note($"Once {r.Population} households live here and enough workers share a trade, they form a guild that leans on a councillor and makes demands of you.");
            return;
        }
        foreach (var g in t.Guilds)
        {
            var d = Guilds.Def(t, g);
            string id = g.Id;
            bool strike = Guilds.OnStrike(t, g);
            string mood = strike ? "On strike" : g.Standing >= r.Pleased ? "Pleased" : g.Standing >= r.Angry ? "Content" : "Angry";
            var color = strike || g.Standing < r.Angry ? Ui.Bad : g.Standing >= r.Pleased ? Ui.Good : Ui.Ink2;
            string master = g.MasterId is int m ? AgentLink(t.Agent(m)) : "no master";
            string hall = g.HallId is int h ? BuildingLink(t.Building(h)) : "the taverns";
            var seat = Council.FindSeat(t, g.Seat);
            string backs = Council.Holder(t, g.Seat) is { } holder ? $"Backs {AgentLink(holder)} the {Ui.Esc(seat!.Def.Title)}" : $"Waits for a {Ui.Esc(seat?.Def.Title ?? "councillor")}";
            int members = Guilds.Members(t, g).Count;
            p.Row($"[b]{Ui.Esc(Ui.Cap(d.Name))}[/b]  {Tag(mood, color)}\n{Ui.C(Ui.Ink2, $"[font_size=12]Master {master} at {hall} · {members} members · standing {Math.Round(g.Standing)} · purse {Math.Round(g.Purse)}g\n{backs}. {Ui.Esc(Ui.Cap(d.Trade))}.[/font_size]")}");
            if (strike)
            {
                p.Card($"[b]On strike for {Ui.Days(g.StrikeUntil - t.Day)}.[/b] {Ui.C(Ui.Ink2, Ui.Esc(d.Strike))}", Ui.Bad);
                string? breakWhy = Guilds.BreakBlocked(t, g), payWhy = Guilds.PayBlocked(t, g);
                p.Buttons(
                    ($"Pay them off, {Js.Str(Guilds.PayCost(t, g))}g", payWhy == null, true, () => Act(() => _sim.PayOffGuild(id))),
                    ($"Break the strike, −{Js.Str(r.BreakMandate)} Mandate", breakWhy == null, false, () => Act(() => _sim.BreakStrike(id))));
                if (breakWhy != null) p.Note($"Breaking it {breakWhy}.");
                continue;
            }
            if (Guilds.Want(t, g) is not { } w) continue;
            string? why = Guilds.MeetBlocked(t, g);
            double? cost = Guilds.MeetCost(t, g);
            p.Card($"[b]Wants {Ui.Esc(Guilds.WantText(t, g, w))}.[/b] {Ui.C(Ui.Ink2, $"Due in {Ui.Days(Math.Max(1, g.Due - t.Day))}. Left alone, its standing falls by {Js.Str(r.Lapsed)}; at {Js.Str(r.StrikeAt)} it strikes.")}", Ui.Gold);
            p.Buttons(
                (w.Kind == "zone" ? "Meet it (paint the land)" : cost is > 0 ? $"Meet it, {Js.Str(cost.Value)}g" : "Meet it", why == null, true, () => Act(() => _sim.MeetGuildDemand(id))),
                ($"Pay them off, {Js.Str(Guilds.PayCost(t, g))}g", Guilds.PayBlocked(t, g) == null, false, () => Act(() => _sim.PayOffGuild(id))));
            if (why != null) p.Note(Ui.Cap(why) + ".");
        }
    }

    /// <summary>
    /// --guilds [strike]: the Council window open on the guilds; with strike, the guild with the most members walks out.
    /// Staging only: the town it plays never sees this.
    /// </summary>
    private void StageGuilds(bool strike)
    {
        var t = _sim.Town;
        if (strike && t.Guilds.OrderByDescending(g => Guilds.Members(t, g).Count).ThenBy(g => g.Id, StringComparer.Ordinal).FirstOrDefault() is { } walkout)
            Guilds.Strike(t, walkout);
        SetTab(Tab.Council);
    }
}
