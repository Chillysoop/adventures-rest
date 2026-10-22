using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The city window: what the town is (thorp to great city), what the next charter asks for, the
/// wards the town has grown, and how the houses fare (the goods and services that let them grow
/// a storey). Charters are the player's to claim; everything else here is read-only.
/// </summary>
public partial class Main
{
    private void RenderCity()
    {
        var p = _panes[Tab.City];
        var t = _sim.Town;
        var tier = City.Tier(t);
        p.Begin();
        p.Card($"{Ui.Tip(Ui.Head(Ui.Esc(tier.Name), "tab-city", Ui.KeywordColor, 17), tier.Blurb)}\n{Ui.Chips(Ui.Chip("population", $"{t.Population}", null, "Households"), Ui.Chip("ui-person", $"~{City.Souls(t):N0}", null, "Souls"))}");

        RenderSeason(p, t);
        RenderSickness(p, t);
        RenderSettlers(t, p);

        var next = City.NextTier(t);
        if (next != null)
        {
            p.Heading($"Charter: {next.Name}", "decree");
            p.Text(Check(t.Population >= next.Residents, $"{Ui.Img("population", 15)} {t.Population} / {next.Residents} households"), 13);
            foreach (string id in next.Requires)
            {
                var d = t.Content.Building(id);
                bool has = City.Has(t, id);
                bool rising = !has && t.Buildings.Any(b => b.Def.Id == id && b.IsSite);
                string place = has ? "" : rising ? Ui.C(Ui.Ink2, " (rising)") : "";
                if (!has && !rising && Planning.IsGovernment(t, d))
                    p.Row(Check(false, $"a {d.Name.ToLowerInvariant()}") + place, ("Place", Planning.Blocked(t, d) == null, false, () => PlaceFromCity(id)));
                else p.Text(Check(has, has || rising || d.Civic ? $"a {d.Name.ToLowerInvariant()}" : Ui.Tip($"a {d.Name.ToLowerInvariant()} {Ui.Img("ui-info", 13)}", "Townsfolk build these on zoned land")) + place, 13);
            }
            if (next.Walls != "")
            {
                var work = next.Walls == "CityWall" ? DefenseWork.CityWall : DefenseWork.StoneWall;
                bool up = City.WallsStand(t, next.Walls);
                string name = next.Walls == "CityWall" ? "the city wall" : "stone walls";
                string? why = Threat.BuildBlocked(t, work);
                if (up) p.Text(Check(true, name), 13);
                else p.Row(Check(false, name) + (why == "under way" ? Ui.C(Ui.Ink2, " (rising)") : ""), ($"Build · {Threat.BuildCost(t, work)}g", why == null, false, () => { _sim.BuildDefense(work); _dirty = true; }));
            }
            p.Text(Check(t.Treasury >= next.Cost, $"{next.Cost:N0}g"), 13);
            var missing = City.Missing(t);
            p.Buttons(($"Claim: {next.Name}", missing.Count == 0, true, () => { _sim.RaiseCharter(); _dirty = true; }));
            p.Note(Ui.Esc(next.Blurb));
        }
        else p.Note("The greatest charter there is. From here the city only grows richer and its houses grander.");

        p.Heading("Wards");
        p.Note("Wards aren't laid out ahead. Once a few buildings stand in a quarter round the old town, it grows into the ward that suits what was built there first.");
        foreach (var id0 in t.City.OpenWards)
        {
            var w = City.Ward(t, id0);
            string where = w.Ring == 0 ? "the heart of town" : City.SectorOf(t, w.Id) is var (_, side) ? $"{(w.Outer ? "outside the walls" : "round the old town")}, {side}" : "";
            string about = w.Blurb;
            var chips = new List<string> { Ui.Chip("ui-house", Ui.Cap(where), Ui.Ink2, "Where") };
            if (w.Favours != Zone.None) chips.Add(Ui.Chip("ui-star", Ui.Cap(Planning.ZoneName(w.Favours)), Ui.Gold, "Favours"));
            if (t.Content.Wards.Of(w.Id) is { } temper)
            {
                // Who speaks for the ward, what it cares about, and how it feels about the mayor now.
                double f = Wards.Feeling(t, w.Id);
                var fc = f >= 15 ? Ui.Good : f <= -15 ? Ui.Bad : Ui.Ink2;
                string cares = Regard.Describe(t, new TemperDef { Likes = temper.Likes, Dislikes = temper.Dislikes });
                about += $"\n{Ui.Cap(temper.Voice)} {cares.Replace("; dislikes ", "; they dislike ").Replace("likes ", "like ")}.";
                chips.Add(Ui.Chip("standing", $"{Ui.Cap(Wards.Describe(f))} {(f >= 0 ? "+" : "−")}{Math.Abs(Math.Round(f))}", fc, "How the ward feels about you"));
            }
            string text = $"{Ui.Tip($"[b]{Ui.Esc(w.Name)}[/b] {Ui.Img("ui-info", 13)}", about)}\n[font_size=12]{Ui.Chips(chips.ToArray())}[/font_size]";
            var id = w.Id;
            p.Row(text, ("Show", true, false, () => LookAtWard(id)));
        }
        var yet = t.Content.City.Wards.Where(w => !t.City.OpenWards.Contains(w.Id)).ToList();
        if (yet.Count > 0)
        {
            p.Text(Ui.Dim("[b]Could still grow[/b]"), 13);
            p.Text(string.Join("\n", yet.Select(w =>
            {
                string draws = string.Join(", ", w.Draws.OrderByDescending(d => d.Value).ThenBy(d => d.Key, StringComparer.Ordinal).Select(d => DrawName(t, d.Key)));
                bool now = City.Reached(t, w.Tier) && City.RingOpen(t, w.Ring);
                string when = !City.Reached(t, w.Tier) ? City.TierName(t, w.Tier) : !City.RingOpen(t, w.Ring) ? $"land {(w.Outer ? "beyond the walls" : "round the old town")} opens" : "now";
                return Ui.Bullet(Ui.Tip($"{Ui.Esc(w.Name)}  {Ui.Dim($"[font_size=12]{Ui.Esc(when)}[/font_size]")}", $"{(w.Outer ? "Outside the walls" : "Round the old town")}, where you build mostly {draws}."), now ? "ui-yes" : "ui-lock");
            })), 13);
        }

        RenderDensity(p, t);
        RenderGrandWorks(p, t);

        p.Heading("Houses");
        foreach (var h in t.Content.City.Housing)
        {
            var homes = t.Buildings.Where(b => b.Def.Id == h.Id && b.Complete).ToList();
            var def = t.Content.Building(h.Id);
            if (homes.Count == 0 && !City.Unlocked(t, def)) continue;
            int people = homes.Sum(b => b.Residents.Count);
            double content = homes.Count == 0 ? 0 : homes.Average(b => b.Contentment);
            string wants = City.Wants(t, h);
            string needs = h.Goods.Count + h.Services.Count == 0 ? Ui.Bullet(Ui.Dim("Just a roof"), "ui-house")
                : wants == "" ? Ui.Bullet(Ui.C(Ui.Good, "Has all it wants"), "ui-yes") : Ui.Bullet(Ui.C(Ui.Bad, $"Short of {Ui.Esc(wants)}"), "ui-warn");
            string grow = h.Next != "" ? "\n" + Ui.Bullet(Ui.Tip(Ui.Dim($"→ {t.Content.Building(h.Next).Name}{(h.Dense != "" ? $" / {t.Content.Building(h.Dense).Name}" : "")}"), $"Grows into a {t.Content.Building(h.Next).Name.ToLowerInvariant()}{(h.Dense != "" ? $" (a {t.Content.Building(h.Dense).Name.ToLowerInvariant()} on high-density land)" : "")} when its family has what that wants."), "ui-up") : "";
            p.Row($"[b]{Ui.Esc(def.Name)}[/b]  {Ui.Chips(Ui.Chip("ui-house", $"{homes.Count}", null, "Homes"), Ui.Chip("ui-person", $"{people}", null, "Living"), Ui.Chip("gold", $"{h.Tax:0.##}", Ui.Gold, "Hearth tax a day, each"))}\n[font_size=12]{needs}{grow}[/font_size]");
            if (homes.Count > 0 && (h.Goods.Count > 0 || h.Services.Count > 0)) p.Meter("Content", content * 100, $"{Math.Round(content * 100)}%", content < t.Content.City.DowngradeBelow);
        }

        p.Heading("Services");
        foreach (var (s, cov) in t.City.Coverage)
        {
            bool wanted = t.Content.City.Housing.Any(h => h.Services.Contains(s) && t.Buildings.Any(b => b.Def.Id == h.Id || b.Def.Id == Previous(t, h.Id)));
            if (cov <= 0 && !wanted) continue;
            p.Meter(Ui.Cap(City.ServiceName(t, s)), cov * 100, $"{Math.Round(cov * 100)}%", cov < 0.6);
        }
        p.Note("The share of townsfolk each kind of building can serve. Taverns, shrines, market halls, bathhouses, theatres and libraries each serve so many; houses that want one and can't reach it stop growing.");

        if (t.City.Wanted.Count > 0)
        {
            p.Heading("Household goods");
            foreach (var (g, want) in t.City.Wanted)
            {
                if (want <= 0) continue;
                double got = t.City.Supplied.Get(g, 0) / want;
                p.Meter(g.ToString(), got * 100, $"{Math.Round(got * 100)}%", got < 0.6);
            }
            p.Note("How much of what the houses want to buy the market could sell them yesterday. Workshops open where these run short.");
        }
        RenderUnder(p, t);
        p.Commit();
    }

    /// <summary>The undercity: each level under the town, how restless it is, and the watch's patrols.</summary>
    private void RenderUnder(Pane p, Town t)
    {
        var r = t.Content.Undercity;
        p.Heading("Under the streets");
        foreach (var d in r.Levels)
        {
            var l = Undercity.Of(t, d.Id);
            if (l == null)
            {
                var tier = t.Content.City.Tiers.FirstOrDefault(x => x.Id == d.Tier);
                p.Text(Ui.Bullet(Ui.Dim($"{Ui.Esc(Ui.Cap(d.Name))}: {Ui.Esc(tier?.Name ?? d.Tier)}"), "ui-lock"), 13);
                continue;
            }
            var site = Undercity.Site(t, l);
            string mood = Undercity.Mood(t, l);
            var color = mood == "stirring" ? Ui.Bad : mood == "restless" ? Ui.Gold : Ui.Good;
            string state = site.Cleared
                ? Ui.Chips(Ui.Chip("ui-yes", $"{l.Cleared}", Ui.Good, "Times cleared"), Ui.Chip("ui-time", Ui.Days(Math.Max(0, (l.RefillDay ?? t.Day) - t.Day)), Ui.Ink2, "Until the dark fills again"))
                : Ui.Chips(Ui.Chip("dread", $"{Math.Round(l.Stir)}/{Js.Str(r.RiseAt)}", color, "Stir: at the top, something comes up"), Ui.Chip("ui-up", $"{Js.Str(Math.Round(Undercity.Rate(t, l), 2))}/day", Ui.Ink2, "Rising"), l.Rose > 0 ? Ui.Chip("ui-skull", $"{l.Rose}", Ui.Bad, "Times it has risen") : "");
            string head = $"[b]{Ui.Esc(Ui.Cap(d.Name))}[/b]  {Tag(mood, color)}  {Ui.Dim($"[font_size=12]tier {d.Site.Tier}[/font_size]")}\n[font_size=12]{state}[/font_size]";
            string id = d.Id;
            if (site.Cleared && !l.Patrol) p.Row(head);
            else p.Row(head, l.Patrol
                ? ("Call off the patrol", true, false, () => Act(() => _sim.PatrolUndercity(id, false)))
                : ($"Patrol · {Js.Str(r.PatrolCost)}g a day", true, false, () => Act(() => _sim.PatrolUndercity(id, true))));
        }
        p.Note($"Each charter opens a deeper level. Left alone a level stirs (faster with secret societies and more people above), and at {Js.Str(r.RiseAt)} something comes up into the streets. Post a bounty on it, or patrol at night to slow it.");
    }

    /// <summary>Zoning densities: which the charter allows, and how many households live at each.</summary>
    private static void RenderDensity(Pane p, Town t)
    {
        p.Heading("Density");
        var homes = t.Buildings.Where(b => b.Def.Housing > 0 && b.Complete).ToList();
        for (int level = 0; level < t.Content.City.Densities.Count; level++)
        {
            var d = t.Content.City.Densities[level];
            int lvl = level;
            int people = homes.Where(b => Density.At(t, t.Lot(b.LotId)) == lvl).Sum(b => b.Residents.Count);
            string status = Density.Blocked(t, level) == null ? Tag("unlocked", Ui.Good) : Tag($"needs {City.TierName(t, d.Tier).ToLowerInvariant()}");
            p.Row($"{Ui.Tip($"[b]{Ui.Esc(d.Name)}[/b] {Ui.Img("ui-info", 13)}", d.Blurb)}  {status}  {Ui.Chip("ui-person", $"{people}", null, "Living")}");
        }
        p.Note("Paint dense homes and shops from the build bar's zones, over open ground or streets already built. Old walled cores grow tall first; let lower sprawl spread outside the walls.");
    }

    /// <summary>The season: what it does to the town, the winter stores, and the visitors it brings.</summary>
    private void RenderSeason(Pane p, Town t)
    {
        var s = Seasons.Current(t);
        var st = t.Seasons;
        string festival = Seasons.Festival(t) && s.Festival != null ? "  " + Tag(Ui.Cap(s.Festival.Name), Ui.Good) : "";
        p.Heading($"{s.Name}, year {Seasons.Year(t)}", "ui-day");
        p.Text($"{Ui.Tip(Ui.Chips(Ui.Chip("ui-day", $"{Seasons.DayOf(t)}/{Seasons.Rules(t).Days}", null, "Day of the season"), Ui.Chip("ui-time", $"{Seasons.Next(t).Name} in {Ui.Days(Seasons.DaysLeft(t))}", Ui.Ink2)), s.Blurb)}{festival}", 13);
        // The season's effects as chips: an icon, the figure in green or red, the meaning on hover.
        var effects = new List<string>();
        string Pct(double y) => Ui.C(y >= 1 ? Ui.Good : Ui.Bad, $"[b]{Math.Round(y * 100)}%[/b]");
        double food = s.YieldOf(Good.Food);
        if (Math.Abs(food - 1) > 0.05) effects.Add(Ui.Tip($"{Ui.Img("food", 16)} {Pct(food)}", "Fields yield"));
        foreach (var (g, y) in s.Yields)
            if (g != Good.Food && Math.Abs(y - 1) > 0.15) effects.Add(Ui.Tip($"{Ui.Img(g.ToString().ToLowerInvariant(), 16)} {Pct(y)}", $"{g} output"));
        if (Math.Abs(s.Build - 1) > 0.05) effects.Add(Ui.Tip($"{Ui.Img("tools", 16)} {Pct(s.Build)}", "Building speed"));
        if (s.Mood != 0) effects.Add(Ui.Tip($"{Ui.Img("mandate", 16)} {Ui.Signed(s.Mood)}", "Mood"));
        effects.Add(Ui.Tip($"{Ui.Img("ui-trade", 16)} {Ui.C(Ui.Gold, $"[b]{s.ImportPremium:0.#}×[/b]")}", "Caravans charge this much for what the town lacks"));
        if (s.MarchDays > 0) effects.Add(Ui.Tip($"{Ui.Img("ui-road", 16)} {Ui.C(Ui.Bad, $"[b]+{Ui.Days(s.MarchDays)}[/b]")}", "Armies march slower"));
        var busy = s.Menace.Where(kv => kv.Value >= 1.25).Select(kv => kv.Key).ToList();
        var quiet = s.Menace.Where(kv => kv.Value <= 0.7).Select(kv => kv.Key).ToList();
        if (s.Warband >= 1.25) busy.Insert(0, "the warband");
        if (s.Warband <= 0.7) quiet.Insert(0, "the warband");
        if (busy.Count > 0) effects.Add(Ui.Tip($"{Ui.Img("threat", 16)} {Ui.C(Ui.Bad, "[b]▲[/b]")}", $"On the move: {Expedition.ListNames(busy)}"));
        if (quiet.Count > 0) effects.Add(Ui.Tip($"{Ui.Img("threat", 16)} {Ui.C(Ui.Good, "[b]▼[/b]")}", $"Lie low: {Expedition.ListNames(quiet)}"));
        p.Text(Ui.Chips(effects.ToArray()), 13);
        RenderOmen(p, t);

        double cap = Seasons.Stores(t);
        double full = Seasons.StoresFull(t);
        bool holding = s.Hold;
        p.Meter("Winter stores", Math.Min(1, full) * 100, $"{Math.Round(Math.Min(t.Market.Stock(Good.Food), cap)):N0} / {cap:N0}", holding && full < 0.5);
        var (more, cost) = Seasons.LayInQuote(t);
        string why = !holding ? "Laid in from autumn. Granaries hold more."
            : more <= 0 ? "Cellars and granaries full." : $"Buy {more:N0} loads of grain for the winter.";
        if (holding && more > 0) p.Row(Ui.C(Ui.Ink2, $"[font_size=12]{why}[/font_size]"), ($"Lay in stores · {cost:N0}g", t.Treasury >= cost, true, () => { _sim.LayInStores(); _dirty = true; }));
        else p.Note(why);
        p.Text(Ui.Chips(Ui.Chip("ui-person", $"{st.Tourists}", null, "Visitors"), Ui.Chip("gold", $"{st.TouristGold:N0}", Ui.Gold, "Visitors spent yesterday"), Ui.Chip("ui-day", $"{st.SeasonGold:N0}g", Ui.Gold, "Visitors spent this season")), 13);
    }

    /// <summary>This year's omen: what the seers foretold, when it falls and what it does.</summary>
    private static void RenderOmen(Pane p, Town t)
    {
        if (Omens.Foretold(t) is not { } o) return;
        bool now = Omens.Now(t) == o;
        int? until = Omens.DaysUntil(t);
        var color = o.Good ? Ui.Good : Ui.Bad;
        string when = now ? Ui.Chip("ui-time", "now", color, "In its season now") : until is int d && d > 0 ? Ui.Chip("ui-time", $"in {Ui.Days(d)}", Ui.Ink2, "When it falls") : Ui.Chip("ui-time", "passed", Ui.Ink2);
        string effects = string.Join(" ", OmenEffects(o));
        p.Text($"{Ui.Tip($"{Ui.Img(o.Icon, 18)} [b]{Ui.Esc(o.Name)}[/b]", $"The seers foretold it for this year. {o.Text}")}  {when}  {effects}", 13);
    }

    /// <summary>What an omen does, as chips: green for a boon, red for a trial.</summary>
    public static List<string> OmenEffects(OmenDef o)
    {
        var e = new List<string>();
        string Pct(double y) => Ui.C(y >= 1 ? Ui.Good : Ui.Bad, $"[b]{Math.Round(y * 100)}%[/b]");
        foreach (var (g, y) in o.Yields) e.Add(Ui.Tip($"{Ui.Img(g.ToString().ToLowerInvariant(), 14)} {Pct(y)}", $"{g} output"));
        if (o.Mood != 0) e.Add(Ui.Tip($"{Ui.Img("mandate", 14)} {Ui.Signed(o.Mood)}", "Mood"));
        if (Math.Abs(o.Build - 1) > 0.01) e.Add(Ui.Tip($"{Ui.Img("tools", 14)} {Pct(o.Build)}", "Building speed"));
        if (o.Arrivals != 0) e.Add(Ui.Tip($"{Ui.Img("population", 14)} {Ui.C(o.Arrivals > 0 ? Ui.Good : Ui.Bad, o.Arrivals > 0 ? "[b]▲[/b]" : "[b]▼[/b]")}", "Settlers"));
        if (o.Heroes != 0) e.Add(Ui.Tip($"{Ui.Img("heroes", 14)} {Ui.C(o.Heroes > 0 ? Ui.Good : Ui.Bad, o.Heroes > 0 ? "[b]▲[/b]" : "[b]▼[/b]")}", "Heroes walking in"));
        if (Math.Abs(o.Tourists - 1) > 0.01) e.Add(Ui.Tip($"{Ui.Img("ui-person", 14)} {Pct(o.Tourists)}", "Visitors"));
        if (o.Menace > 1.01 || o.Warband > 1.01) e.Add(Ui.Tip($"{Ui.Img("threat", 14)} {Ui.C(Ui.Bad, "[b]▲[/b]")}", "Enemies on the move"));
        else if (o.Menace < 0.99 || o.Warband < 0.99) e.Add(Ui.Tip($"{Ui.Img("threat", 14)} {Ui.C(Ui.Good, "[b]▼[/b]")}", "Enemies lie low"));
        if (o.MarchDays != 0) e.Add(Ui.Tip($"{Ui.Img("ui-road", 14)} {Ui.C(o.MarchDays > 0 ? Ui.Good : Ui.Bad, $"[b]{(o.MarchDays > 0 ? "+" : "")}{Ui.Days(o.MarchDays)}[/b]")}", "Armies march"));
        if (Math.Abs(o.Sick - 1) > 0.01) e.Add(Ui.Tip($"{Ui.Img("sickness", 14)} {Pct(o.Sick)}", "Sickness spreads"));
        return e;
    }

    /// <summary>Sickness in the houses: how many, who brought it, and the council's two answers.</summary>
    private void RenderSickness(Pane p, Town t)
    {
        if (!Sickness.Outbreak(t)) return;
        var sick = Sickness.Sick(t);
        var r = t.Content.Sickness;
        p.Heading("Sickness", "sickness");
        p.Text(Ui.Chips(Ui.Chip("sick-house", $"{sick.Count}", Ui.Bad, "Sick houses"), Ui.Chip("ui-person", $"{Sickness.Abed(t)}", Ui.Bad, "Abed"),
            Ui.Chip("tend", $"−{Math.Round(Sickness.Healers(t) * 100)}%", Ui.Good, "Healers and clean water slow the spread")), 13);
        int until = t.Tick + r.Tend.Days * SimTime.HoursPerDay;
        bool tended = sick.All(b => b.SickUntil <= until);
        double cost = Sickness.TendCost(t);
        p.Row(Ui.Tip($"{Ui.Img("tend", 16)} Healers door to door", $"Every sick house is up within {Ui.Days(r.Tend.Days)}."),
            (tended ? "Tended" : $"Tend · {Js.Str(cost)}g", !tended && t.Treasury >= cost, true, () => Act(() => _sim.TendTheSick())));
        bool q = t.Sickness.Quarantine;
        p.Row(Ui.Tip($"{Ui.Img("quarantine", 16)} Close the sick lanes", $"Spreads {Math.Round((1 - r.Quarantine.Spread) * 100)}% slower; everyone's mood {Ui.Signed(r.Quarantine.Mood)}."),
            (q ? "Open" : "Close", true, !q, () => Act(() => _sim.Quarantine(!q))));
        var shown = sick.OrderBy(b => b.Id).First();
        p.Buttons(("Show", true, false, () => LookAtBuilding(shown)));
    }

    /// <summary>Why settlers come or stay away: each reason with its sign, as the arrivals roll counts it.</summary>
    private static void RenderSettlers(Town t, Pane p)
    {
        var draw = Population.Appeal(t);
        double week = Population.WeeklySettlers(t, draw.Total);
        p.Heading("Why settlers come or don't");
        foreach (var part in draw.Parts)
            p.Text($"{Ui.Signed(part.Value, "0.00")}  {Ui.Esc(part.Label)}", 13);
        p.Text(Ui.Tip(Ui.Chips(Ui.Chip("population", $"~{Math.Round(week)}/week", Ui.Good, "Newcomers"), Ui.Dim($"draw {draw.Total:0.00} / 1.60 {Ui.Img("ui-info", 13)}")),
            $"Taxes above {Math.Round(t.Content.Rules.TaxRate * 100)}% and idle hands cost the most; open jobs and empty beds help."), 13);
    }

    /// <summary>The grand works: what each costs and does, which the charter allows, and how far the rising one has got.</summary>
    private void RenderGrandWorks(Pane p, Town t)
    {
        p.Heading("Grand works");
        foreach (var d in GrandWorks.All(t).OrderBy(d => City.TierIndex(t, d.Tier)).ThenBy(d => d.Id, StringComparer.Ordinal))
        {
            var b = GrandWorks.Of(t, d.Id);
            string? why = Planning.Blocked(t, d);
            string status = b is { Complete: true } ? Tag("stands", Ui.Good)
                : b != null ? Tag($"stage {Math.Min(b.Stage, d.Stages.Count)} of {d.Stages.Count}", Ui.Gold)
                : City.Reached(t, d.Tier) ? Tag($"about {GrandWorks.TotalCost(t, d):N0}g in all")
                : Tag($"needs {City.TierName(t, d.Tier).ToLowerInvariant()}");
            string text = $"[b]{Ui.Esc(d.Name)}[/b]  {status}\n[font_size=12]{Ui.Bullet(Ui.Esc(GrandWorks.Bonus(d)), "ui-star")}  {Ui.Chip("gold", $"−{d.Upkeep:0.#}", Ui.Bad, "Upkeep a day", 14)}[/font_size]";
            var id = d.Id;
            if (b == null) p.Row(text, ($"Begin · {GrandWorks.StageCost(t, d, 0):N0}g", why == null, true, () => PlaceFromCity(id)));
            else p.Row(text, ("Show", true, false, () => LookAtBuilding(b)));
            if (b is { IsSite: true })
            {
                p.Meter("Built", b.BuildProgress / d.BuildHours * 100, $"{Math.Round(b.BuildProgress / d.BuildHours * 100)}%");
                if (GrandWorks.Waiting(b))
                {
                    double cost = GrandWorks.StageCost(t, d, b.Stage);
                    p.Note($"Next: {d.Stages[b.Stage].Name}, {cost:N0}g. Starts on its own once the treasury holds that and {GrandWorks.Reserve:N0}g to spare.");
                }
                else p.Note($"Building {d.Stages[Math.Max(0, b.Stage - 1)].Name}.");
            }
        }
        p.Note("Great projects that take seasons. Each stage draws its gold and goods when the last is done, and the finished work helps the whole town for good.");
    }

    private void LookAtBuilding(Building b)
    {
        var lot = _sim.Town.Lot(b.LotId);
        Look(new Vector2(Geo.Margin + (float)lot.X, Geo.Margin + (float)lot.Y), 30);
    }

    private static string? Previous(Town t, string housingId) => City.Previous(t, housingId)?.Id;

    private static string Check(bool ok, string text) => Ui.Img(ok ? "ui-yes" : "ui-no", 15) + " " + (ok ? text : Ui.Dim(text));

    /// <summary>Open the build bar on the government group with this building picked.</summary>
    private void PlaceFromCity(string id)
    {
        OpenPlan("Government", id, null);
        _dirty = true;
    }

    /// <summary>What a ward draws on, as the City window names it.</summary>
    private static string DrawName(Town t, string key) => key switch
    {
        "Holy" => "shrines and temples",
        "Fine" => "fine houses",
        "Residential" => "homes",
        "Commercial" => "shops",
        "Industry" => "workshops",
        "Farmland" => "farms",
        _ => t.Content.Buildings.FirstOrDefault(b => b.Id == key) is { } d ? d.Name.ToLowerInvariant() + "s" : key,
    };

    private void LookAtWard(string wardId)
    {
        var t = _sim.Town;
        var lots = t.Lots.Where(l => City.WardOf(t, l).Id == wardId).ToList();
        if (lots.Count == 0) return;
        var c = new Vector2((float)lots.Average(l => l.X), (float)lots.Average(l => l.Y));
        Look(new Vector2(Geo.Margin + c.X + 0.5f, Geo.Margin + c.Y + 0.5f), 40);
    }
}
