using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The city window: what the town is (thorp to great city), what the next charter asks for, the
/// neighbourhoods and blocs the town has grown, and how the houses fare (the goods and services that let them grow
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
        StewardStrip(p, "planning");
        p.Card($"{Ui.Tip(Ui.Head(Ui.Esc(tier.Name), "tab-city", Ui.KeywordColor, 17), tier.Blurb)}\n{Ui.Chips(Ui.Chip("population", $"{t.Population}", null, "Households"), Ui.Chip("ui-person", $"~{City.Souls(t):N0}", null, "Souls"))}");

        p.Page("Growth", "ui-up");
        RenderSeason(p, t);
        RenderRefugees(p, t);
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
        RenderGrandWorks(p, t);

        p.Page("Neighbourhoods", "ui-house");
        RenderNeighbourhoods(p, t);
        RenderBlocs(p, t);

        RenderDensity(p, t);
        RenderUnder(p, t);

        p.Page("Homes", "population");
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
            // The contentment bar hangs under its own house's row, above the rule.
            bool meter = homes.Count > 0 && (h.Goods.Count > 0 || h.Services.Count > 0);
            bool low = content < t.Content.City.DowngradeBelow;
            string pct = $"{Math.Round(content * 100)}%";
            p.Row($"[b]{Ui.Esc(def.Name)}[/b]  {Ui.Chips(Ui.Chip("ui-house", $"{homes.Count}", null, "Homes"), Ui.Chip("ui-person", $"{people}", null, "Living"), Ui.Chip("gold", $"{h.Tax:0.##}", Ui.Gold, "Hearth tax a day, each"))}\n[font_size=12]{needs}{grow}[/font_size]",
                meter ? ($"content{pct}{low}", null, () => Pane.MeterLine("Content", content * 100, pct, low)) : null);
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
        RenderSickness(p, t);
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
        if (Math.Abs(food - 1) > 0.05) effects.Add(Ui.Tip($"{Ui.Img("food", 16)} {Pct(food)} {W("fields")}", "Fields yield"));
        foreach (var (g, y) in s.Yields)
            if (g != Good.Food && Math.Abs(y - 1) > 0.15) effects.Add(Ui.Tip($"{Ui.Img(g.ToString().ToLowerInvariant(), 16)} {Pct(y)} {W(g.ToString().ToLowerInvariant())}", $"{g} output"));
        if (Math.Abs(s.Build - 1) > 0.05) effects.Add(Ui.Tip($"{Ui.Img("tools", 16)} {Pct(s.Build)} {W("building")}", "Building speed"));
        if (s.Mood != 0) effects.Add(Ui.Tip($"{Ui.Img("mandate", 16)} {Ui.Signed(s.Mood)} {W("mood")}", "Mood"));
        effects.Add(Ui.Tip($"{Ui.Img("ui-trade", 16)} {Ui.C(Ui.Gold, $"[b]{s.ImportPremium:0.#}×[/b]")} {W("imports")}", "Caravans charge this much for what the town lacks"));
        if (s.MarchDays > 0) effects.Add(Ui.Tip($"{Ui.Img("ui-road", 16)} {Ui.C(Ui.Bad, $"[b]+{Ui.Days(s.MarchDays).Replace(' ', '\u00a0')}[/b]")} {W("marches")}", "Armies march slower"));
        var busy = s.Menace.Where(kv => kv.Value >= 1.25).Select(kv => kv.Key).ToList();
        var quiet = s.Menace.Where(kv => kv.Value <= 0.7).Select(kv => kv.Key).ToList();
        if (s.Warband >= 1.25) busy.Insert(0, "the warband");
        if (s.Warband <= 0.7) quiet.Insert(0, "the warband");
        if (busy.Count > 0) effects.Add(Ui.Tip($"{Ui.Img("threat", 16)} {Ui.C(Ui.Bad, "[b]▲[/b]")} {W("foes")}", $"On the move: {Expedition.ListNames(busy)}"));
        if (quiet.Count > 0) effects.Add(Ui.Tip($"{Ui.Img("threat", 16)} {Ui.C(Ui.Good, "[b]▼[/b]")} {W("foes")}", $"Lie low: {Expedition.ListNames(quiet)}"));
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

    /// <summary>A one-word label after a chip's figure, so the row reads without hovering.</summary>
    private static string W(string word) => Ui.Dim($"[font_size=11]{word}[/font_size]");

    /// <summary>What an omen does, as chips: green for a boon, red for a trial.</summary>
    public static List<string> OmenEffects(OmenDef o)
    {
        var e = new List<string>();
        string Pct(double y) => Ui.C(y >= 1 ? Ui.Good : Ui.Bad, $"[b]{Math.Round(y * 100)}%[/b]");
        foreach (var (g, y) in o.Yields) e.Add(Ui.Tip($"{Ui.Img(g.ToString().ToLowerInvariant(), 14)} {Pct(y)} {W(g.ToString().ToLowerInvariant())}", $"{g} output"));
        if (o.Mood != 0) e.Add(Ui.Tip($"{Ui.Img("mandate", 14)} {Ui.Signed(o.Mood)} {W("mood")}", "Mood"));
        if (Math.Abs(o.Build - 1) > 0.01) e.Add(Ui.Tip($"{Ui.Img("tools", 14)} {Pct(o.Build)} {W("building")}", "Building speed"));
        if (o.Arrivals != 0) e.Add(Ui.Tip($"{Ui.Img("population", 14)} {Ui.C(o.Arrivals > 0 ? Ui.Good : Ui.Bad, o.Arrivals > 0 ? "[b]▲[/b]" : "[b]▼[/b]")} {W("settlers")}", "Settlers"));
        if (o.Heroes != 0) e.Add(Ui.Tip($"{Ui.Img("heroes", 14)} {Ui.C(o.Heroes > 0 ? Ui.Good : Ui.Bad, o.Heroes > 0 ? "[b]▲[/b]" : "[b]▼[/b]")} {W("heroes")}", "Heroes walking in"));
        if (Math.Abs(o.Tourists - 1) > 0.01) e.Add(Ui.Tip($"{Ui.Img("ui-person", 14)} {Pct(o.Tourists)} {W("visitors")}", "Visitors"));
        if (o.Menace > 1.01 || o.Warband > 1.01) e.Add(Ui.Tip($"{Ui.Img("threat", 14)} {Ui.C(Ui.Bad, "[b]▲[/b]")} {W("foes")}", "Enemies on the move"));
        else if (o.Menace < 0.99 || o.Warband < 0.99) e.Add(Ui.Tip($"{Ui.Img("threat", 14)} {Ui.C(Ui.Good, "[b]▼[/b]")} {W("foes")}", "Enemies lie low"));
        if (o.MarchDays != 0) e.Add(Ui.Tip($"{Ui.Img("ui-road", 14)} {Ui.C(o.MarchDays > 0 ? Ui.Good : Ui.Bad, $"[b]{(o.MarchDays > 0 ? "+" : "")}{Ui.Days(o.MarchDays).Replace(' ', '\u00a0')}[/b]")} {W("marches")}", "Armies march"));
        if (Math.Abs(o.Sick - 1) > 0.01) e.Add(Ui.Tip($"{Ui.Img("sickness", 14)} {Pct(o.Sick)} {W("sickness")}", "Sickness spreads"));
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

    /// <summary>The refugee camp by the road: who waits, who found their feet, who was turned away, and the town's haven name.</summary>
    private void RenderRefugees(Pane p, Town t)
    {
        var st = t.Refuge;
        var camp = Refugees.Camped(t).ToList();
        if (camp.Count == 0 && st.Sheltered == 0 && st.TurnedAway == 0) return;
        var r = t.Content.Refugees;
        p.Heading("Refugees", "homeless");
        string title = Refugees.Title(t);
        if (title != "") p.Text(Ui.Chip("ui-renown", Ui.Cap(title), Ui.Gold, "Haven fame: more refugees choose the town, and settlers come sooner"), 13);
        int soonest = camp.Count == 0 ? 0 : camp.Min(a => a.StayUntil) - t.Day;
        p.Text(Ui.Chips(
            Ui.Chip("homeless", $"{camp.Count}", null, "In the camp by the road"),
            Ui.Chip("ui-house", $"{st.Settled}", Ui.Good, "Settled for good"),
            Ui.Chip("ui-person", $"{st.WentHome}", Ui.Ink2, "Went home to rebuild"),
            Ui.Chip("ui-skull", $"{st.TurnedAway + st.GaveUp}", Ui.Bad, "Turned away or gave up waiting")), 13);
        foreach (var l in st.Lost.Where(l => !l.Retaken).OrderBy(l => l.Day))
            if (Quests.Site(t, l.SiteId) is { } lostTo)
                p.Text(Ui.Tip($"{Ui.Img("ui-house", 14)} {Ui.Esc(t.Settlements[l.From].Name)} {Ui.C(Ui.Bad, "←")} {Ui.Esc(lostTo.Def.Name)}",
                    "Their lost home. Clear the site and those still in the camp go home and rebuild; the village comes back as a friend."), 13);
        if (camp.Count > 0)
            p.Text(string.Join("\n", new[] {
                Ui.Tip($"{Ui.Img("ui-time", 14)} Soonest leaves in {Ui.Days(Math.Max(0, soonest))}", $"They wait {r.Patience.Least} days, a day more for each day homes or workplaces are going up, up to {r.Patience.Most}."),
                Ui.Tip($"{Ui.Img("food", 14)} The council feeds those with no coin", "Rations come out of the treasury; with an empty treasury they go hungry and leave sooner."),
                camp.Count >= r.CrowdAt
                    ? Ui.Tip(Ui.C(Ui.Bad, $"{Ui.Img("sickness", 14)} Crowded: grumbling, fever risk"), "A temple, shrine or bathhouse and full bellies keep it down. Homes and work empty the camp.")
                    : "" }.Where(x => x != "")), 13);
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

    /// <summary>Neighbourhoods: each formed where buildings cluster, with its people, rent, land price and mood.</summary>
    private void RenderNeighbourhoods(Pane p, Town t)
    {
        if (t.City.Quarters.Count == 0) return;
        p.Heading("Neighbourhoods");
        p.Help("Nothing is laid out ahead: build anywhere your zones are painted.",
            "Where buildings cluster, a neighbourhood forms and names itself after what it is.",
            "What stands round a home sets its rent, land price and mood:",
            "• A few shops near: dearer, cheerier. A whole market: noisy",
            "• No shop within reach: a long walk, grumbling",
            "• Workshops next door: cheap and gloomy. Nobody wants them, everybody needs them",
            "• Temples and fine houses raise a street; tenements lower it",
            "Manors need dear ground, tenements cheap ground, fisheries the river, a cathedral holy ground.",
            "Arcane works close together make a scholars' quarter that adds lore.");
        foreach (var q in t.City.Quarters)
        {
            var st = Neighbourhood.Stats(t, q);
            if (st.Buildings == 0) continue;
            p.Row($"{Ui.Tip($"[b]{Ui.Esc(q.Name)}[/b] {Ui.Img("ui-info", 13)}", QuarterTip(st))}\n[font_size=12]{QuarterChips(st)}[/font_size]", ("Show", true, false, () => LookAtQuarter(q.Id)));
        }
        int scholars = Neighbourhood.Scholars(t), least = Neighbourhood.Rules(t).Scholars.Least;
        if (scholars > 0)
            p.Text(Ui.Bullet(Ui.Tip(scholars >= least ? Ui.C(Ui.Good, $"Scholars' quarter: +{Js.Str(Neighbourhood.Rules(t).Scholars.Lore)} lore a day") : Ui.Dim($"Scholars' quarter: {scholars} / {least} arcane works together"),
                "A library, mage tower, alchemists' quarter, observatory or apothecary within a few streets of each other."), "books"), 13);
    }

    /// <summary>A neighbourhood's figures as chips, for the City window and the map card.</summary>
    public static string QuarterChips(QuarterStats st)
    {
        var mc = st.Mood >= 0.5 ? Ui.Good : st.Mood <= -0.5 ? Ui.Bad : Ui.Ink2;
        var lc = st.Land >= 1.15 ? Ui.Gold : st.Land <= 0.9 ? Ui.Bad : Ui.Ink2;
        return Ui.Chips(
            Ui.Chip(CharacterIcon(st.Character), Ui.Cap(CharacterName(st.Character)), Ui.Ink2, "What it is now"),
            st.Residents > 0 ? Ui.Chip("ui-person", $"{st.Residents}", null, "Living here") : "",
            Ui.Chip("gold", $"×{st.Rent:0.00}", lc, "Rent, by what stands round the homes"),
            Ui.Chip("ui-heart", $"{(st.Mood >= 0 ? "+" : "−")}{Math.Abs(st.Mood):0.0}", mc, "Mood its homes carry"));
    }

    /// <summary>The hover on a neighbourhood: when it formed, what's there and what the ground is worth.</summary>
    public static string QuarterTip(QuarterStats st) =>
        $"Formed day {st.Quarter.Day}, as {CharacterName(st.Quarter.Character)}.\n{st.Buildings} buildings: {st.Homes} homes, {st.Shops} shops, {st.Workshops} workshops.\nLand ×{st.Land:0.00} · rent ×{st.Rent:0.00}";

    public static string CharacterName(string character) => character switch
    {
        "market" => "market street",
        "workshops" => "workshops",
        "farms" => "farmsteads",
        "holy" => "temple close",
        "rich" => "well-off",
        "poor" => "poor",
        "scholars" => "scholars",
        "river" => "riverside",
        _ => "homes",
    };

    public static string CharacterIcon(string character) => character switch
    {
        "market" => "ui-trade",
        "workshops" => "tools",
        "farms" => "bread",
        "holy" => "ui-holy",
        "rich" => "regalia-circlet",
        "poor" => "homeless",
        "scholars" => "books",
        "river" => "inst-harbour",
        _ => "ui-house",
    };

    /// <summary>The town's blocs: who speaks for which townsfolk, what they care about, and how they feel about the mayor.</summary>
    private static void RenderBlocs(Pane p, Town t)
    {
        var speaking = Blocs.Speaking(t);
        if (speaking.Count == 0) return;
        p.Heading("Blocs");
        p.Help("Townsfolk band into blocs by where they work, the house they live in and their faith, wherever in town they live.",
            "Each person belongs to several, some more than others.",
            "Every choice shows the blocs it pleases or angers. Warm blocs: cheerier folk, houses grow sooner. Sour ones grumble in the street.");
        foreach (var b in speaking)
        {
            double f = Blocs.Feeling(t, b.Id);
            var fc = f >= 15 ? Ui.Good : f <= -15 ? Ui.Bad : Ui.Ink2;
            string cares = Regard.Describe(t, new TemperDef { Likes = b.Likes, Dislikes = b.Dislikes });
            p.Text($"{Ui.Img(b.Icon == "" ? "ui-person" : b.Icon, 16)} {Ui.Tip($"[b]{Ui.Esc(Ui.Cap(b.Voice))}[/b]", $"{Ui.Cap(b.Voice)} {cares.Replace("; dislikes ", "; they dislike ").Replace("likes ", "like ")}.")}   [font_size=12]{Ui.Chips(
                Ui.Chip("population", $"{Math.Round(Blocs.Size(t, b))}", null, "Members, by how much each belongs"),
                Ui.Chip("standing", $"{Ui.Cap(Blocs.Describe(f))} {(f >= 0 ? "+" : "−")}{Math.Abs(Math.Round(f))}", fc, "How they feel about you"))}[/font_size]", 13);
        }
    }

    private void LookAtQuarter(int id)
    {
        var t = _sim.Town;
        if (Neighbourhood.Find(t, id) is not { } q) return;
        _map.Lens.Show(true, Lens.Mode.Neighbourhoods);
        Look(new Vector2(Geo.Margin + (float)q.X, Geo.Margin + (float)q.Y), 40);
    }
}
