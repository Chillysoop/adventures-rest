using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The bounty board, in its window and in the tray's drawer. It opens on who can ride today (each company ready in
/// town, and the free heroes), then the threats, one compact row each: the foe's face, what's posted, a coloured
/// pill per crew for how hard the place looks for those very heroes (click one to offer them the job by name), and
/// icon buttons whose tooltips say what they cost and do. The road, exploring and eyes in the wilds fold
/// away beneath, so the board itself never needs scrolling to.
/// </summary>
public partial class Main
{
    /// <summary>The bounty board's sections that are unfolded.</summary>
    private readonly HashSet<string> _boardOpen = ["road"];

    /// <summary>The last offer made from a crew's pill on the board, and its answer.</summary>
    private (string Company, string Site, JobAnswer Answer)? _boardAnswer;

    private const int PillsShown = 5;

    private void RenderBounties()
    {
        var p = _panes[Tab.Bounties];
        var t = _sim.Town;
        p.Begin();
        StewardStrip(p, "bounties");
        var crews = Roster.Crews(t);
        RenderRiders(p, t, crews);
        RenderBoard(p, t, crews, int.MaxValue, compact: false);

        int road = t.Parties.Count(x => !x.Returned);
        if (Fold(p, "road", "On the road", road == 0 ? "none" : road.ToString(), "Parties out on jobs. Ask for word or call them home by sending stone.")) RenderRoad(p, t);
        int out_ = t.Surveys.Count(Explore.Out);
        if (Fold(p, "explore", "Exploring", out_ > 0 ? $"{out_} out" : "", "Paint ground for a company to explore.\n• Explore over the corner map\n• Paid each trip until it's all seen")) RenderExplore(p, t);
        int patrols = t.Wilds.Count(z => z.Kind == Wilds.Patrol);
        if (Fold(p, "patrol", "Patrols", patrols > 0 ? patrols.ToString() : "", "Paint ground for riders to keep.\n• Fewer caravans lost on its roads\n• Raiders crossing it seen sooner", t.Wilds.Any(z => z.Kind == Wilds.Patrol && z.Unpaid) ? Ui.Bad : null)) RenderPatrols(p, t);
        int eyes = t.Scouts.Count(x => x.Out) + t.Mirrors.Count(m => m.Intact);
        if (Fold(p, "eyes", "Eyes in the wilds", eyes > 0 ? eyes.ToString() : "", "Scouts, hidden mirrors and the works that let the town see sites live.")) RenderEyes(p, t);
        p.Commit();
    }

    /// <summary>The bounty board in the tray's drawer: who can ride, the worst threats, and the road.</summary>
    private void TrayBounties(Pane p, Town t)
    {
        var crews = Roster.Crews(t);
        RenderRiders(p, t, crews);
        RenderBoard(p, t, crews, 5, compact: true);
        if (t.Parties.Any(x => !x.Returned))
        {
            p.Heading("On the road");
            RenderRoad(p, t);
        }
    }

    /// <summary>A section heading that folds: its title, a short count, and a tooltip saying what's in it. True while open.</summary>
    private bool Fold(Pane p, string id, string title, string count, string tip, Color? tint = null)
    {
        bool open = _boardOpen.Contains(id);
        p.Add($"F{id}{open}{count}{tint}", () =>
        {
            var m = new MarginContainer();
            m.AddThemeConstantOverride("margin_top", 6);
            var b = new Button { Flat = true, FocusMode = Control.FocusModeEnum.None, Alignment = HorizontalAlignment.Left, TooltipText = tip, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 8);
            var arrow = Ui.Label(open ? "▼" : "▶", Ui.Gold, Ui.Bold, 10);
            arrow.CustomMinimumSize = new Vector2(12, 0);
            row.AddChild(arrow);
            row.AddChild(Ui.Label(title.ToUpperInvariant(), Ui.Ink2, Ui.Bold, 12));
            row.AddChild(Ui.Label(count, tint ?? Ui.Ink2, Ui.Medium, 12));
            foreach (var ch in row.GetChildren()) ((Control)ch).MouseFilter = Control.MouseFilterEnum.Ignore;
            row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            b.AddChild(row);
            b.CustomMinimumSize = new Vector2(0, 26);
            b.Pressed += () => Act(() =>
            {
                if (!_boardOpen.Remove(id)) _boardOpen.Add(id);
            });
            m.AddChild(b);
            m.SetMeta("heading", title);
            return m;
        });
        return open;
    }

    /// <summary>A crew's short mark on the board: the initials of its name, or "Free" for the free heroes.</summary>
    private static string CrewMark(Crew c)
    {
        if (c.Name == "") return "Free";
        var words = c.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w is not ("The" or "the" or "of" or "and")).ToList();
        return words.Count switch
        {
            0 => c.Name[..Math.Min(2, c.Name.Length)],
            1 => words[0][..Math.Min(2, words[0].Length)],
            _ => string.Concat(words.Take(2).Select(w => char.ToUpperInvariant(w[0]))),
        };
    }

    private static string CrewName(Crew c) => c.Name == "" ? "Free heroes" : $"The {c.Name}";

    private static string Riders(Crew c) => string.Join("\n", c.Riders.Select(h => $"• {h.GivenName}, {h.Hero!.Cls.Name} {h.Hero.Level}{(h.Hero.PartyName != c.Name ? " (free hero)" : "")}"));

    /// <summary>Who can ride today, as chips: each crew's mark (the column its pills keep below), name, riders and levels.</summary>
    private void RenderRiders(Pane p, Town t, List<Crew> crews)
    {
        if (_boardAnswer is { } ans)
        {
            var site = Quests.Site(t, ans.Site);
            p.Card(ans.Answer.Accepted
                ? $"{Ui.Img("act-offer", 16)} [b]The {Ui.Esc(ans.Company)}[/b] {Ui.C(Ui.Good, "[b]take the job[/b]")}{(site != null ? $" on {SiteLink(site.Def)}" : "")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Ui.Cap(ans.Answer.Why))}.[/font_size]")}"
                : $"{Ui.Img("act-offer", 16)} [b]The {Ui.Esc(ans.Company)}[/b] {Ui.C(Ui.Bad, "[b]turn it down[/b]")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Ui.Cap(ans.Answer.Why))}.[/font_size]")}",
                ans.Answer.Accepted ? Ui.Good : Ui.Bad);
        }
        if (t.Taverns.Count == 0)
        {
            p.Card($"[b]{Ui.C(Ui.Bad, "No adventurers yet")}[/b]\n[font_size=12]• Heroes come once the town has a tavern[/font_size]", Ui.Bad);
            return;
        }
        if (crews.Count == 0)
        {
            var heroes = t.Heroes.Where(h => !h.Dead && !h.Departed).ToList();
            int road = heroes.Count(h => t.Parties.Any(x => !x.Returned && x.Members.Contains(h.Id)));
            int hurt = heroes.Count(h => !h.IsAway && !Quests.Available(h));
            int ready = heroes.Count(Quests.Available);
            var why = new List<string>();
            if (ready > 0) why.Add(ready < 3 ? $"Only {ready} ready: a party needs 3" : $"{ready} ready, but their companies are out on the road");
            if (road > 0) why.Add($"{road} on the road");
            if (hurt > 0) why.Add($"{hurt} resting their wounds");
            if (heroes.Count == 0) why.Add("no heroes on the town's books");
            p.Card($"[b]{Ui.C(Ui.Bad, "Nobody can ride today")}[/b]\n[font_size=12]{string.Join("\n", why.Select(w => $"• {w}"))}\n• Posted bounties wait {QuestsBountyDays} days for takers[/font_size]", Ui.Bad);
            return;
        }
        string sig = string.Join(";", crews.Take(PillsShown).Select(c => $"{c.Name}:{string.Join(",", c.Riders.Select(h => $"{h.Id}.{h.Hero!.Level}"))}:{c.Offerable}"));
        p.Add($"R{sig}", () =>
        {
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 4);
            var head = new HBoxContainer();
            head.AddThemeConstantOverride("separation", 6);
            head.AddChild(new TextureRect { Texture = Ui.Icon("heroes"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(14, 14), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            head.AddChild(Ui.Label("READY TO RIDE", Ui.Ink2, Ui.Bold, 12));
            v.AddChild(head);
            var flow = new HFlowContainer();
            flow.AddThemeConstantOverride("h_separation", 6);
            flow.AddThemeConstantOverride("v_separation", 6);
            foreach (var c in crews.Take(PillsShown))
            {
                var chip = new PanelContainer
                {
                    TooltipText = $"{CrewName(c)}\n{Riders(c)}\n{(c.Name == "" ? "They form a party for any bounty you post." : c.Offerable ? "Click a pill on a threat to offer them that job by name." : "Won't hear an offer by name now; may still take a posted bounty.")}",
                    MouseFilter = Control.MouseFilterEnum.Stop,
                };
                chip.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel2, Ui.Line, 6, 1, 6, 3));
                var h = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                h.AddThemeConstantOverride("separation", 6);
                h.AddChild(Mark(c, Ui.Ink, Ui.Gold));
                int levels = c.Riders.Sum(r => r.Hero!.Level);
                h.AddChild(Ui.Label(c.Name == "" ? "Free heroes" : c.Name, c.Offerable || c.Name == "" ? Ui.Ink : Ui.Ink2, Ui.Medium, 12));
                h.AddChild(Ui.Label($"{c.Riders.Count}× · Lv {levels}", Ui.Ink2, null, 11));
                foreach (var ch in h.GetChildren()) ((Control)ch).MouseFilter = Control.MouseFilterEnum.Ignore;
                chip.AddChild(h);
                flow.AddChild(chip);
            }
            v.AddChild(flow);
            return v;
        });
    }

    private const int QuestsBountyDays = 10;

    /// <summary>A crew's mark: its initials on a small plate, the key its pills on the threats repeat.</summary>
    private static Control Mark(Crew c, Color ink, Color edge)
    {
        var plate = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        plate.AddThemeStyleboxOverride("panel", Ui.Box(new Color("120f0b"), edge, 4, 1, 4, 0));
        var l = Ui.Label(CrewMark(c), ink, Ui.Bold, 11);
        l.MouseFilter = Control.MouseFilterEnum.Ignore;
        plate.AddChild(l);
        return plate;
    }

    /// <summary>The terms to post on, as icon toggles, and the threats: open ones first (named threats on top), the cleared in one line.</summary>
    private void RenderBoard(Pane p, Town t, List<Crew> crews, int limit, bool compact)
    {
        var chosen = t.Content.Bounties.Term(_terms);
        string termSig = string.Join(",", t.Content.Bounties.Terms.Select(x => x.Id)) + chosen.Id;
        var (pleased, angered) = Regard.Reactions(t, chosen.Tags);
        string Who(List<Agent> heroes) => string.Join(", ", heroes.Take(4).Select(h => h.GivenName)) + (heroes.Count > 4 ? $" +{heroes.Count - 4}" : "");
        string feel = (pleased.Count > 0 ? $"  {Ui.C(Ui.Good, $"♥ {Ui.Esc(Who(pleased))}")}" : "") + (angered.Count > 0 ? $"  {Ui.C(Ui.Bad, $"✖ {Ui.Esc(Who(angered))}")}" : "");
        p.Heading("Bounty board");
        p.Add($"K{termSig}{feel}", () =>
        {
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 4);
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 4);
            h.AddChild(Ui.Label("Terms", Ui.Ink2, null, 12));
            foreach (var term in t.Content.Bounties.Terms)
            {
                var (pl, an) = Regard.Reactions(t, term.Tags);
                string tip = $"{term.Name}  ·  pays {Math.Round(term.Pay * 100)}% of the asking\n{term.Text}"
                    + (pl.Count > 0 ? $"\n♥ Pleases {Who(pl)}" : "") + (an.Count > 0 ? $"\n✖ Angers {Who(an)}" : "")
                    + "\nEvery Post button below uses these terms.";
                string id = term.Id;
                var b = IconButton($"act-{term.Id}", "", tip, true, term.Id == chosen.Id, () => Act(() => _terms = id));
                if (term.Id == chosen.Id) b.Modulate = new Color(1.15f, 1.1f, 1);
                h.AddChild(b);
            }
            v.AddChild(h);
            v.AddChild(Ui.Rich($"[b]{Ui.Esc(chosen.Name)}[/b] {Ui.C(Ui.Ink2, $"[font_size=12]· {Ui.Esc(chosen.Text)}[/font_size]")}{(feel != "" ? $"\n[font_size=12]{feel}[/font_size]" : "")}", 13));
            return v;
        });

        foreach (var c in Captives.Held(t).OrderBy(c => c.Id))
        {
            if (Quests.Site(t, c.SiteId) is not SiteState held) continue;
            int left = Math.Max(1, (int)Math.Ceiling((c.DueTick - t.Tick) / 24.0));
            string names = string.Join(", ", c.Captives.Select(id => t.Agent(id).Name));
            p.Card($"{Ui.Img("act-rescue", 16)} [b]Held at {SiteLink(held.Def)}[/b]  {Tag(Ui.Days(left) + " left", Ui.Bad)}\n[font_size=12]• {Ui.Esc(names)}\n• Post a [b]Rescue[/b] there, or clear it[/font_size]", Ui.Bad);
        }

        // Threats first: a site an alert names (a war camp, a crisis's seat) or the warband's camp, then the rest of the open country.
        var named = Alerts.Standing(t).Where(a => a.SiteId != null).Select(a => a.SiteId!).ToHashSet();
        var open = t.Sites
            .Where(s => s.Known != SiteKnowledge.Unheard && !s.ThoughtCleared)
            .OrderBy(s => named.Contains(s.Def.Id) || s.Def.Id == t.Content.Enemy.Outpost.Id ? 0 : Askers(t, s).Count > 0 ? 1 : 2)
            .ThenBy(s => limit == int.MaxValue ? 0 : -s.BelievedTier)
            .ToList();
        var shown = open.Take(limit).ToList();
        if (open.Count == 0) p.Note("No threat the town knows of. Explore to find more.");
        var free = Watch.FreeScryingsLeft(t) > 0;
        var watchers = Watch.Watchers(t);
        foreach (var s in shown) BoardRow(p, t, s, chosen, crews, free, watchers, compact);
        if (open.Count > shown.Count) p.Note($"+{open.Count - shown.Count} more on the full board");

        var cleared = t.Sites.Where(s => s.Known != SiteKnowledge.Unheard && s.ThoughtCleared).ToList();
        if (cleared.Count > 0 && limit == int.MaxValue)
            p.Text($"{Ui.C(Ui.Good, "[b]✓ Cleared[/b]")}  [font_size=12]{string.Join(", ", cleared.Select(s => SiteLink(s.Def)))}[/font_size]", 13);
    }

    /// <summary>
    /// Everyone else who wants this place dealt with, as chips: a village's plea, a notable's job not yet posted, and the
    /// trouble it is to the town (which draws companies on its own). One board for all the work going.
    /// </summary>
    private static List<string> Askers(Town t, SiteState s)
    {
        var chips = new List<string>();
        foreach (var pl in t.Pleas.Where(x => x.SiteId == s.Def.Id && x.Status == PleaStatus.Open).OrderBy(x => x.Id))
            chips.Add(Ui.Chip("standing", Js.Str(Math.Round(pl.Reward)), Ui.Good, $"Plea from {t.Settlements[pl.SettlementId].Name}\n• Their reward, paid to whoever goes\n• Answering it pleases them", 12)
                + Ui.C(Ui.Good, $"[font_size=11] {Ui.Esc(t.Settlements[pl.SettlementId].Name)}[/font_size]"));
        foreach (var n in t.Notables.Where(n => !n.Gone && n.AskSite == s.Def.Id && Notables.AskOf(t, n) is { Quest: not "" }).OrderBy(n => n.Id))
            chips.Add(Ui.Chip("tab-notables", "", Ui.Gold, $"{Notables.NameOf(t, n)} {Notables.AskText(t, n, Notables.AskOf(t, n)!)}\n• Answer in the Notables tab to post it on their gold", 12)
                + Ui.C(Ui.Gold, $"[font_size=11] {Ui.Esc(Notables.NameOf(t, n))} asks[/font_size]"));
        string? trouble = Troubles.Of(t, s) switch
        {
            "host" => "the big bad's host gathers here",
            "raider" => "raided the town's roads lately",
            "burner" => "fell on a neighbour lately",
            _ => null,
        };
        if (trouble != null)
            chips.Add(Ui.Chip("threat", "", Ui.Bad, $"Trouble: {trouble}\n• Companies may go on their own\n• They earn renown for it", 12) + Ui.C(Ui.Bad, $"[font_size=11] {trouble}[/font_size]"));
        return chips;
    }

    /// <summary>One threat: face, name, what's posted or happening, a danger pill per crew, and its buttons.</summary>
    private void BoardRow(Pane p, Town t, SiteState s, TermDef chosen, List<Crew> crews, bool freeScry, List<Agent> watchers, bool compact)
    {
        var live = t.Bounties.FirstOrDefault(b => b.SiteId == s.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken);
        var party = live?.PartyId is int pid ? t.Parties.FirstOrDefault(x => x.Id == pid) : null;
        int reward = (int)Terms.Suggested(t, s, chosen);
        double withLoot = Sim.Systems.Armory.TermsReward(t, reward);
        string? blocked = Terms.Blocked(t, s, chosen);
        string sid = s.Def.Id;

        string status;
        if (party != null && !party.Returned) status = Tag($"⚔ The {Ui.Esc(party.Name)}, back day {SimTime.DayOf(party.ReturnTick)}", Ui.Gold);
        else if (live != null && live.Errand != "" && t.Content.Bounties.Errands.FirstOrDefault(x => x.Id == live.Errand) is { } boon)
            status = Tag($"Mages' errand · {Math.Round(live.Reward)}g + {Ui.Esc(boon.Name)}", Ui.Gold);
        else if (live != null)
            status = Tag($"Posted · {Math.Round(live.Reward)}g{(live.LootToTown ? " + loot to us" : "")} · {Ui.Days(Math.Max(1, (int)Math.Ceiling((live.ExpiresTick - t.Tick) / 24.0)))} left", Ui.Gold)
                + (t.Content.Bounties.Plots.FirstOrDefault(x => x.Id == live.Plot) is { } plot ? " " + Ui.C(Ui.Ink2, $"[font_size=11]{Ui.Esc(Ui.Cap(plot.Name))}[/font_size]") : "")
                + (crews.Count == 0 ? " " + Tag("· nobody free", Ui.Bad) : "");
        else if (s.Attempts > 0) status = Ui.Tip(Tag($"{s.Attempts} {(s.Attempts == 1 ? "try" : "tries")}", Ui.Ink2), "Companies and village bands that have set out against it, won or lost");
        else status = "";

        int bt = Math.Clamp(s.BelievedTier, 1, 3);
        string tier = new string('◆', bt) + new string('◇', 3 - bt);
        // A seat the Campaign window names reads as that, not as the rumour the town first heard of the place.
        var seated = t.Saga.Menaces.FirstOrDefault(m => m.Alive && m.SeatId == s.Def.Id);
        string rumour = seated != null
            ? Ui.C(Ui.Bad, $"[font_size=11]seat of {Ui.Esc(seated.Name)}{(seated.BigBad ? ", the big bad" : "")}{(s.Def.Tier > 0 ? $", tier {s.Def.Tier}" : "")}[/font_size]")
            : s.Known == SiteKnowledge.Rumoured ? Ui.C(Ui.Ink2, $"[font_size=11]rumour: {Ui.Esc(s.Believed)} ({Rumors.Trust(s.Trust)})[/font_size]") : "";
        int suggested = Notoriety.Suggested(t, s);
        string renown = "  " + Ui.Chip("ui-renown", suggested.ToString(), Ui.Ink2,
            suggested == 0 ? "Renown suggested: none. Any company can make its name here." : $"Renown suggested: {suggested}\n• A company with less can still go\n• Fewer foes there know its name, so more stand and fight", 12);
        string patron = live?.NotableId is int nid && Notables.Find(t, nid) is { } giver ? "  " + Ui.C(Ui.Gold, $"[font_size=11]for {Ui.Esc(Notables.NameOf(t, giver))}[/font_size]") : "";
        var askers = Askers(t, s);
        string asked = askers.Count == 0 ? "" : "\n" + string.Join("  ", askers);
        string text = $"{SiteLink(s.Def)} {Ui.C(Ui.Gold, $"[font_size=11]{tier}[/font_size]")}{renown}{patron}{(status != "" ? $"\n[font_size=12]{status}[/font_size]" : "")}{asked}{(rumour != "" ? $"\n{rumour}" : "")}";

        bool onRoad = party != null && !party.Returned;
        var pills = onRoad ? [] : crews.Take(PillsShown).Select(c => (Crew: c, Band: Assess.DangerFor(t, s, c.Riders, c.Name == "" ? null : c.Name))).ToList();
        var danger = Assess.Danger(t, s);
        bool canPost = live == null && blocked == null;
        bool scryable = Watch.LiveOf(t, s) == null;
        // The first rested caster's terms: the loyal scry free, others for gold, the disaffected for a favour.
        var offer = Knowledge.Offers(t, s.Def).FirstOrDefault();
        double scryCost = offer?.Gold ?? 0;
        bool scouting = Watch.ScoutAt(t, sid) != null;

        string sig = $"S{text}|{Foes.CreatureFace(s.Believed, s.Def.Kind)}{danger.Id}|{string.Join(",", pills.Select(x => x.Crew.Name + x.Band.Id + x.Crew.Offerable))}|{canPost}{blocked}{reward}{withLoot}{t.Treasury >= reward}{t.Treasury >= withLoot}|{scryable}{freeScry}{offer?.Caster.Id}{offer?.Price}{Math.Round(scryCost)}{t.Treasury >= scryCost}|{scouting}{watchers.Count}|{chosen.Id}{compact}";
        p.Add(sig, () =>
        {
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 6);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(Foes.Of(t, s, 36));
            var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            col.AddThemeConstantOverride("separation", 3);
            col.AddChild(Ui.Rich(text, 14));
            if (pills.Count > 0)
            {
                var ph = new HFlowContainer();
                ph.AddThemeConstantOverride("h_separation", 4);
                ph.AddThemeConstantOverride("v_separation", 4);
                foreach (var (c, band) in pills) ph.AddChild(Pill(t, s, c, band, chosen, reward));
                col.AddChild(ph);
            }
            else if (!onRoad && crews.Count == 0)
                col.AddChild(Foes.Bar("Danger", danger, Foes.Worse(danger), "How hard it looks for the town's strongest heroes, from what the town believes is there. A rumour can be wrong."));
            row.AddChild(col);

            var buttons = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            buttons.AddThemeConstantOverride("separation", 4);
            if (live == null)
            {
                string why = blocked != null ? $"\nCan't post: {Ui.Cap(blocked)}." : t.Treasury < reward ? $"\nThe treasury has only {Math.Floor(t.Treasury)}g." : "";
                string terms = chosen.Id;
                buttons.AddChild(IconButton($"act-{chosen.Id}", $"{reward}g",
                    $"Post a bounty: {chosen.Name}, {reward}g\n• {chosen.Text}\n• The gold leaves the treasury now\n• Heroes and companies choose whether to take it\n• Back to you if nobody does in {QuestsBountyDays} days{why}",
                    canPost && t.Treasury >= reward, true, () => Act(() =>
                    {
                        _boardAnswer = null;
                        _sim.PostBounty(sid, reward, terms);
                    })));
                if (!chosen.Item && !compact)
                    buttons.AddChild(IconButton("act-loot", $"{Js.Str(withLoot)}g",
                        $"Post it with the loot coming to us: {Js.Str(withLoot)}g\n• As above, but the magic they find goes to the town armory\n• Costs about a quarter more{why}",
                        canPost && t.Treasury >= withLoot, false, () => Act(() =>
                        {
                            _boardAnswer = null;
                            _sim.PostBounty(sid, withLoot, terms, lootToTown: true);
                        })));
            }
            if (scryable)
            {
                string scryTip = freeScry
                    ? $"Scry {s.Def.Name}: see it live for {t.Content.Knowledge.ScryHours} hours\n• Free today (the Scrying Pool)"
                    : offer != null
                        ? $"Scry {s.Def.Name}: see it live for {t.Content.Knowledge.ScryHours} hours\n• {offer.Caster.Name}, {offer.Caster.Hero!.Cls.Name}: {offer.Price switch { ScryPrice.Free => "free", ScryPrice.Gold => $"{Math.Round(offer.Gold)}g", _ => "as a favour" }}"
                        : Knowledge.Scryers(t).Count > 0
                            ? "Scry: see a site live\n• Every caster in town is resting from the last one"
                            : $"Scry: see a site live\n• Needs a {ScryClasses(t)} of level {t.Content.Knowledge.ScryMinLevel}+ in town";
                buttons.AddChild(IconButton("act-scry", "", scryTip, freeScry || offer != null && t.Treasury >= scryCost, false, () => Act(() => _sim.CommissionScrying(sid))));
                if (!scouting && !compact)
                {
                    var k = t.Content.Knowledge.Scouts;
                    string scoutTip = watchers.Count > 0
                        ? $"Scout {s.Def.Name}: {watchers[0].Name} watches it from cover\n• Up to {Ui.Days(k.WatchDays)}, {k.WagePerDay}g a day"
                        : "Scout: watch a site from cover\n• Needs a ranger, rogue, monk or cartographer in town and rested";
                    buttons.AddChild(IconButton("act-scout", "", scoutTip, watchers.Count > 0, false, () => Act(() => _sim.PostScout(sid))));
                }
            }
            row.AddChild(buttons);
            v.AddChild(row);
            v.AddChild(new ColorRect { Color = Ui.Line, CustomMinimumSize = new Vector2(0, 1) });
            return v;
        });
    }

    /// <summary>
    /// A crew's danger pill on a threat: its mark and a bar in the danger's colour. Hovering says who'd ride and how
    /// the company leans; clicking offers the company the job by name at the asking price, on the chosen terms.
    /// </summary>
    private Control Pill(Town t, SiteState s, Crew c, Banded band, TermDef chosen, int reward)
    {
        var fill = Foes.Worse(band);
        bool clickable = c.Offerable;
        var pill = new LazyTip
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = clickable ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow,
            TooltipText = CrewName(c),
        };
        var plain = Ui.Box(new Color(fill, 0.16f), new Color(fill, 0.7f), 6, 1, 5, 2);
        pill.AddThemeStyleboxOverride("panel", plain);
        if (clickable)
        {
            var lit = Ui.Box(new Color(fill, 0.32f), fill, 6, 1, 5, 2);
            pill.MouseEntered += () => pill.AddThemeStyleboxOverride("panel", lit);
            pill.MouseExited += () => pill.AddThemeStyleboxOverride("panel", plain);
        }
        var h = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        h.AddThemeConstantOverride("separation", 5);
        var mark = Ui.Label(CrewMark(c), Ui.Ink, Ui.Bold, 11);
        mark.MouseFilter = Control.MouseFilterEnum.Ignore;
        h.AddChild(mark);
        h.AddChild(new BandBar(band.Step + 1, band.Steps, fill, 26));
        var word = Ui.Label(band.Name, fill, Ui.Bold, 10);
        word.MouseFilter = Control.MouseFilterEnum.Ignore;
        h.AddChild(word);
        pill.AddChild(h);

        string sid = s.Def.Id, terms = chosen.Id, company = c.Name;
        pill.Tip = () =>
        {
            int levels = c.Riders.Select(r => r.Hero!.Level).OrderByDescending(l => l).Take(4).Sum();
            int shortBy = Notoriety.Short(t, c.Name == "" ? null : c.Name, s);
            string known = shortBy > 0 ? $"\n• Renown {Notoriety.RenownOf(t, c.Name == "" ? null : c.Name)}, it suggests {Notoriety.Suggested(t, s)}: more foes will stand and fight" : "";
            string head = $"{CrewName(c)}: {band.Name} for them\n{Riders(c)}\n• Levels {levels}, it wants about {Assess.Wants(t, s)}{known}";
            if (c.Name == "") return $"{head}\nThey'd form a party if you post a bounty.";
            if (!c.Offerable) return $"{head}\nWon't hear an offer: {Roster.Unavailable(t, c.Name)}.";
            var view = Roster.Weigh(t, c.Name, sid, reward, terms);
            if (view.Blocked != null) return $"{head}\nCan't offer it: {view.Blocked}.";
            var (lean, _) = LeaningWord(view.Leaning);
            string reasons = string.Join("", view.Reasons.Take(3).Select(r => $"\n{(r.Points >= 0 ? "+" : "−")} {r.Why}"));
            return $"{head}\nLeaning: {lean}{reasons}\nClick: offer them this job by name for {Js.Str(view.Reward)}g ({view.Terms.Name}). They answer at once and march if they take it.";
        };
        if (clickable)
            pill.GuiInput += e =>
            {
                if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
                Act(() =>
                {
                    var answer = _sim.OfferJob(company, sid, reward, terms);
                    _boardAnswer = answer != null ? (company, sid, answer) : null;
                });
            };
        return pill;
    }

    /// <summary>
    /// A button with an icon, an optional label (a price ending in "g" shows the coin), and a tooltip that says what
    /// it does and costs.
    /// </summary>
    private static Button IconButton(string icon, string label, string tip, bool enabled, bool primary, Action press)
    {
        var b = new Button
        {
            Disabled = !enabled,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = tip,
            ThemeTypeVariation = primary ? "Post" : "",
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        var h = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        h.AddThemeConstantOverride("separation", 4);
        var ink = enabled ? (primary ? Ui.Ink : Ui.Ink) : new Color(Ui.Ink2, 0.5f);
        h.AddChild(new TextureRect { Texture = Ui.Icon(icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(18, 18), Modulate = ink, MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
        if (label != "")
        {
            bool gold = label.EndsWith('g');
            h.AddChild(Ui.Label(gold ? label[..^1] : label, ink, Ui.Bold, 13));
            if (gold)
                h.AddChild(new TextureRect { Texture = Ui.Icon("gold"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(13, 13), Modulate = enabled ? Colors.White : new Color(1, 1, 1, 0.5f), MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
        }
        foreach (var ch in h.GetChildren()) ((Control)ch).MouseFilter = Control.MouseFilterEnum.Ignore;
        b.AddChild(h);
        h.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // The button sizes to its icon and label once they know their font.
        b.Ready += () => b.CustomMinimumSize = h.GetCombinedMinimumSize() + new Vector2(16, 8);
        b.Pressed += () => press();
        return b;
    }

    /// <summary>Parties out on the road, one line each, with sending-stone buttons.</summary>
    private void RenderRoad(Pane p, Town t)
    {
        foreach (var party in t.Parties.Where(x => !x.Returned))
        {
            var site = Quests.Site(t, party.SiteId)!;
            // The town only knows when it last reckoned them home, not where they are now.
            int due = SimTime.DayOf(party.Expected > 0 ? party.Expected : party.ReturnTick);
            bool overdue = Knowledge.Overdue(t).Contains(party);
            string rescue = Reaches.RescueRefused(t, party) ?? "";
            int slots = Knowledge.SlotsLeft(t, party);
            int id = party.Id;
            string members = string.Join(", ", party.Members.Select(m => AgentLink(t.Agent(m), t.Agent(m).GivenName)));
            string where = party.RecalledTick != null ? Ui.C(Ui.Gold, "called home") : $"→ {SiteLink(site.Def)}";
            // Who does what on the road and the skill it rolls, a bullet each.
            var crew = party.Members.Select(t.Agent).Where(a => !a.Dead).ToList();
            string jobs = string.Join("", Wayfaring.Assign(t, crew).Select(j => $"\n• {Ui.Esc(Wayfaring.Describe(t, j))}"));
            string text = $"[b]The {Ui.Esc(party.Name)}[/b] {where}  {(overdue ? Tag($"overdue since day {due}", Ui.Bad) : Tag($"back day {due}", Ui.Gold))}\n[font_size=12]{members}{Ui.C(Ui.Ink2, jobs)}[/font_size]";
            bool can = Knowledge.Slots(t, party) > 0;
            bool stone = Sim.Systems.Armory.Carries(t, party);
            string left = !can ? "No caster or sending stone with them: no way to reach them"
                : slots == 0 ? "No sendings left"
                : stone ? $"{slots} sending{(slots == 1 ? "" : "s")} left on the town's stone"
                : $"{slots} spell slot{(slots == 1 ? "" : "s")} left; each sending costs one{(party.Members.Any(m => t.Agent(m).Hero!.Cls.Healer) ? " (a healer's slot is a heal lost)" : "")}";
            bool canRecall = can && slots > 0 && Knowledge.CanRecall(t, party);
            bool aloft = overdue && site.Def.Aloft;
            p.Add($"P{text}{slots}{can}{canRecall}{aloft}{rescue}", () =>
            {
                var v = new VBoxContainer();
                v.AddThemeConstantOverride("separation", 6);
                var h = new HBoxContainer();
                h.AddThemeConstantOverride("separation", 6);
                var r = Ui.Rich(text, 13);
                r.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                h.AddChild(r);
                h.AddChild(IconButton("act-word", "", $"Ask for word: they send news of how it goes\n• {left}", can && slots > 0, false, () => Act(() => _sim.SendWord(id))));
                h.AddChild(IconButton("act-recall", "", $"Call them home: they turn back now\n• {left}", canRecall, false, () => Act(() => _sim.SendWord(id, recall: true))));
                if (aloft)
                    h.AddChild(IconButton("inst-sky-dock", "", $"Send an airship to look for them where they went\n• {(rescue == "" ? "Days of searching, none if the site is scried" : Ui.Cap(rescue))}", rescue == "", false, () => Act(() => _sim.RescueParty(id))));
                v.AddChild(h);
                v.AddChild(new ColorRect { Color = Ui.Line, CustomMinimumSize = new Vector2(0, 1) });
                return v;
            });
        }
    }

    /// <summary>Companies out exploring, the council's open offers, and the country a company could be sent to.</summary>
    private void RenderExplore(Pane p, Town t)
    {
        foreach (var s in t.Surveys.Where(Explore.Out).OrderBy(x => x.Id))
        {
            var region = t.Regions[s.RegionId];
            var at = s.AtFind is int fid ? Explore.FindOf(t, fid) : null;
            string state = s.Phase switch
            {
                SurveyPhase.Outbound when s.ZoneId != null => "On the road to the ground you painted",
                SurveyPhase.Outbound when s.SpotX is double sx && s.SpotY is double sy => $"On the road to the spot you marked {Explore.Bearing(sx, sy)}",
                SurveyPhase.Exploring when s.ZoneId is int zid && !s.Escorting => $"Walking the painted ground, {(Wilds.Of(t, zid) is { } wz ? Wilds.Unseen(wz) : 0)} left",
                SurveyPhase.Outbound => $"On the road to {Ui.Esc(region.Name)}",
                SurveyPhase.Exploring when s.SpotX != null && !s.Escorting => $"Looking round the spot you marked in {Ui.Esc(region.Name)}",
                SurveyPhase.Exploring => s.Escorting ? "Walking a caravan home" : $"Exploring {Ui.Esc(region.Name)}, {Ui.Days(Math.Max(1, (int)Math.Ceiling((s.ExploreUntil - t.Tick) / 24.0)))} left",
                SurveyPhase.Waiting when at != null => t.Tick >= at.ReportTick ? $"Waiting for word at {Ui.Esc(Explore.Title(t, at))}" : $"Stopped in {Ui.Esc(region.Name)}; a bird is on its way",
                SurveyPhase.Investigating when at != null && Explore.NextChapter(t, at) is { } ch => $"Investigating {Ui.Esc(Explore.Title(t, at))}: {Ui.Esc(ch.Title.ToLowerInvariant())}",
                _ => "Coming home",
            };
            string members = string.Join(", ", s.Members.Select(t.Agent).Where(a => !a.Dead).Select(a => AgentLink(a, a.GivenName)));
            int finds = s.Finds.Count(id => Explore.FindOf(t, id)!.Known);
            var buttons = new List<(string, bool, bool, Action)>();
            if (at is { State: FindState.Open } && t.Tick >= at.ReportTick)
            {
                int id = at.Id;
                buttons.Add(("Read their report", true, true, () => OpenFind(id)));
            }
            p.Row($"[b]The {Ui.Esc(s.Name)}[/b]  {Tag(finds == 1 ? "1 find" : $"{finds} finds", new Color("8cc084"))}\n[font_size=12]{state} · {members}[/font_size]", buttons.ToArray());
        }
        foreach (var c in t.Commissions.Where(c => c.Status == BountyStatus.Open))
        {
            string what = c.ZoneId is int czid && Wilds.Of(t, czid) is { } cz ? Ui.Esc(Ui.Cap(Wilds.Name(t, cz)))
                : c.SpotX is double sx && c.SpotY is double sy
                ? $"Spot marked {Explore.Bearing(sx, sy)}, {Js.Hypot(sx, sy):0.0} days out"
                : Ui.Esc(t.Regions[c.RegionId].Name);
            p.Row($"{what}  {Tag($"Offer posted · {Math.Round(c.Reward)}g", Ui.Gold)}\n{Ui.C(Ui.Ink2, "[font_size=12]Waiting for a company[/font_size]")}");
        }
        foreach (var z in Wilds.OfKind(t, Wilds.Explore).OrderBy(z => z.Id).ToList())
        {
            int zid = z.Id;
            var cells = z.Cells.Select(c => (c.X, c.Y)).ToList();
            p.Row($"{Ui.Esc(Ui.Cap(Wilds.Name(t, z)))}  {Tag($"{z.Cells.Count - Wilds.Unseen(z)} of {z.Cells.Count} walked", Map.Overlay.ExploreInk)}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Map.Overlay.WildsTag(t, z))}[/font_size]")}",
                ("Rub out", true, false, () => Act(() => _sim.EraseWilds(Wilds.Explore, cells))));
        }
        p.Row(Ui.C(Ui.Ink2, "[font_size=12]Paint ground on the map for a company to walk.[/font_size]"), ("Paint", true, true, () => ToggleBrush(Wilds.Explore)));
    }

    /// <summary>The painted patrols: what each costs, how it has fared, the horse riding it, and the brush.</summary>
    private void RenderPatrols(Pane p, Town t)
    {
        // The cart roads from far workings: what they lose, and how much of them patrols keep.
        foreach (var w in t.Content.Workings.Goods)
        {
            if (Workings.Nearest(t, w.Good) is not { } road || !Workings.Afar(t, road) || !t.Buildings.Any(b => b.Complete && w.Buildings.Contains(b.Def.Id))) continue;
            double lost = Workings.RaidChance(t, road), kept = Workings.Patrolled(t, road);
            int lairs = Workings.LairsBy(t, road);
            p.Row(Ui.Chips(
                Ui.Chip(w.Good == Good.Timber ? "timber" : "stone", $"{(w.Good == Good.Timber ? "Timber" : "Stone")} road · {Js.Str(Js.Round(road.Days * 10) / 10)}d", null, "Carts bring loads home from the far working every few days. Raiders can take them."),
                Ui.Chip("warband", $"{Js.Str(Js.Round(lost * 100))}% lost", lost >= 0.2 ? Ui.Bad : null, "Share of carts taken on the road: more for a long road, a dangerous land and lairs beside it."),
                Ui.Chip("order-patrol", $"{Js.Str(Js.Round(kept * 100))}% patrolled", null, "Paint patrols over the road to keep the carts safe."),
                lairs > 0 ? Ui.Chip("warband", $"{lairs} {(lairs == 1 ? "lair" : "lairs")} near", Ui.Bad, "Clear lairs beside the road and fewer carts are taken.") : ""),
                ("Patrol it", kept < 0.9, true, () => Act(() => _sim.PaintWilds(Wilds.Patrol, Wilds.RoadCells(t, (road.X, road.Y))))));
        }
        var horse = t.Army.Regiments.Where(r => r.Soldiers.Count > 0 && Army.Unit(t, r).Role == "horse").OrderBy(r => r.Id).ToList();
        foreach (var z in Wilds.OfKind(t, Wilds.Patrol).OrderBy(z => z.Id).ToList())
        {
            int zid = z.Id;
            var cells = z.Cells.Select(c => (c.X, c.Y)).ToList();
            var riders = Wilds.Riders(t, z);
            var next = horse.FirstOrDefault(r => r.WildsId == null && Wilds.SendBlocked(t, r, z) == null);
            string state = z.Unpaid ? Ui.C(Ui.Bad, "Unpaid: the riders went home") : $"Won {z.Won} · lost {z.Lost}";
            var buttons = new List<(string, bool, bool, Action)>();
            if (riders != null) buttons.Add(($"Call {riders.Name} home", true, false, () => Act(() => _sim.SendRiders(zid, null))));
            else buttons.Add((next != null ? $"Send {next.Name}" : "Send horse", next != null, false, () => { if (next != null) Act(() => _sim.SendRiders(zid, next.Id)); }));
            buttons.Add(("Rub out", true, false, () => Act(() => _sim.EraseWilds(Wilds.Patrol, cells))));
            p.Row($"{Ui.Esc(Ui.Cap(Wilds.Name(t, z)))}  {Tag($"{Wilds.Wage(t, z):0.#}g a day", Map.Overlay.PatrolInk)}\n[font_size=12]{(riders != null ? $"{Ui.Esc(riders.Name)} ride it · " : "")}{state}[/font_size]", buttons.ToArray());
        }
        p.Row(Ui.C(Ui.Ink2, "[font_size=12]Paint ground on the map for riders to keep.[/font_size]"), ("Paint", true, true, () => ToggleBrush(Wilds.Patrol)));
    }

    private static string ScryClasses(Town t) =>
        string.Join(" or ", t.Content.Library.Spell(t.Content.Knowledge.ScrySpell)?.Classes ?? []);

    /// <summary>Scouts in the field, mirrors at sites, and the works that see for the town.</summary>
    private void RenderEyes(Pane p, Town t)
    {
        var scouts = t.Scouts.Where(x => x.Out).ToList();
        var mirrors = t.Mirrors.Where(m => m.Intact).ToList();
        foreach (var sc in scouts)
        {
            var hero = t.Agent(sc.HeroId);
            var site = Quests.Site(t, sc.SiteId)!;
            string state = sc.Mirror
                ? (t.Tick < sc.ArriveTick ? $"carrying a mirror to {SiteLink(site.Def)}" : $"back from hiding a mirror at {SiteLink(site.Def)}")
                : t.Tick < sc.ArriveTick ? $"on the way to {SiteLink(site.Def)}"
                : t.Tick < sc.LeaveTick ? $"watching {SiteLink(site.Def)}, rations for {Ui.Days((int)Math.Ceiling((sc.LeaveTick - t.Tick) / 24.0))}"
                : $"coming home from {SiteLink(site.Def)}";
            int id = sc.Id;
            bool canRecall = !sc.Mirror && t.Tick < sc.LeaveTick;
            p.Row($"{AgentLink(hero)} the {Ui.Esc(hero.Hero!.Cls.Name)}\n{Ui.C(Ui.Ink2, $"[font_size=12]{state}[/font_size]")}",
                canRecall ? [("Call home", true, false, () => Act(() => _sim.RecallScout(id)))] : []);
        }
        foreach (var m in mirrors)
        {
            var site = Quests.Site(t, m.SiteId)!;
            p.Row($"Mirror at {SiteLink(site.Def)}  {Tag("live", Ui.Good)}\n{Ui.C(Ui.Ink2, $"[font_size=12]Hidden day {SimTime.DayOf(m.PlacedTick)}; shows the site until found[/font_size]")}");
        }
        foreach (var w in t.Content.Knowledge.Works)
        {
            var def = t.Content.Building(w.Building);
            var relic = t.Content.Library.Item(w.Relic);
            var b = Watch.WorkBuilding(t, w);
            string what = w.Reach > 0
                ? $"Every site within {Ui.Days(w.Reach)} live, always"
                : $"{(w.Scryings == 1 ? "One free scrying" : $"{w.Scryings} free scryings")} a day";
            string head = $"[b]{Ui.Esc(def.Name)}[/b]{(relic != null ? Ui.C(Ui.Ink2, $" · houses {Ui.Esc(Library.A(relic.Name))}") : "")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{what}[/font_size]")}";
            if (Watch.HasWork(t, w))
                p.Row($"{head}  {Tag(t.Relics.Contains(w.Relic) && b?.Complete != true ? "Relic in the hall" : "Built", Ui.Good)}");
            else if (b != null)
                p.Row($"{head}  {Tag($"Building, {Math.Floor(100 * b.BuildProgress / b.Def.BuildHours)}%", Ui.Gold)}");
            else if (t.Population < w.Population)
                p.Row($"{head}  {Tag($"Needs {w.Population} residents")}");
            else
            {
                double cost = Watch.WorkCost(t, w);
                string bid = w.Building;
                p.Row(head, ($"Build {Math.Round(cost)}g", t.Treasury >= cost, true, () => Act(() => _sim.BuildSightWork(bid))));
            }
        }
        // The town's first institution: a temple to its god, once there are people enough to fill it.
        var temple = t.Buildings.FirstOrDefault(b => b.Def.Id == t.Content.Shrines.Temple);
        string thead = $"[b]Temple of {Ui.Esc(Faith.TempleGod(t) is { } tg ? Faith.Prose(tg) : t.Content.Shrines.Deity)}[/b]\n{Ui.C(Ui.Ink2, $"[font_size=12]Heals and raises heroes, pays its own bounties · {Shrines.Followers(t).Count} {Ui.Esc(t.Content.Shrines.Devout)}[/font_size]")}";
        if (temple?.Complete == true) p.Row($"{thead}  {Tag($"Built · coffers {Math.Round(temple.Cash)}g", Ui.Good)}");
        else if (temple != null) p.Row($"{thead}  {Tag($"Building, {Math.Floor(100 * temple.BuildProgress / temple.Def.BuildHours)}%", Ui.Gold)}");
        else if (t.Population < t.Content.Shrines.TemplePopulation) p.Row($"{thead}  {Tag($"Needs {t.Content.Shrines.TemplePopulation} residents")}");
        else
        {
            double tcost = Shrines.TempleCost(t);
            p.Row(thead, ($"Build {Math.Round(tcost)}g", t.Treasury >= tcost, true, () => Act(() => _sim.BuildTemple())));
        }
    }
}

/// <summary>A panel whose tooltip is worked out only when hovered, for tips too dear to build for every row.</summary>
public partial class LazyTip : PanelContainer
{
    public Func<string>? Tip;

    public override string _GetTooltip(Vector2 atPosition) => Tip?.Invoke() ?? TooltipText;
}
