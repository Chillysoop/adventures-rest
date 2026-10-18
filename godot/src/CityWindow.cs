using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The city window: what the town is (thorp to great city), what the next charter asks for, the
/// wards that can be laid out, and how the houses fare (the goods and services that let them grow
/// a storey). Charters and wards are the player's to claim; everything else here is read-only.
/// </summary>
public partial class Main
{
    private void RenderCity()
    {
        var p = _panes[Tab.City];
        var t = _sim.Town;
        var tier = City.Tier(t);
        p.Begin();
        p.Card($"[font_size=17][b]{Ui.Esc(tier.Name)}[/b][/font_size]  {Tag($"about {City.Souls(t):N0} souls")}  {Tag($"{t.Population} households")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(tier.Blurb)}[/font_size]")}");

        RenderSeason(p, t);
        RenderSettlers(t, p);

        var next = City.NextTier(t);
        if (next != null)
        {
            p.Heading($"The charter of a {next.Name.ToLowerInvariant()}");
            p.Text(Check(t.Population >= next.Residents, $"{next.Residents} households (now {t.Population})"), 13);
            foreach (string id in next.Requires)
            {
                var d = t.Content.Building(id);
                bool has = City.Has(t, id);
                bool rising = !has && t.Buildings.Any(b => b.Def.Id == id && b.IsSite);
                string place = has ? "" : rising ? Ui.C(Ui.Ink2, " (rising)") : "";
                if (!has && !rising && Planning.IsGovernment(t, d))
                    p.Row(Check(false, $"a {d.Name.ToLowerInvariant()}") + place, ("Place", Planning.Blocked(t, d) == null, false, () => PlaceFromCity(id)));
                else p.Text(Check(has, $"a {d.Name.ToLowerInvariant()}" + (has || rising || d.Civic ? "" : Ui.C(Ui.Ink2, " (townsfolk build these on zoned land)"))) + place, 13);
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
            p.Text(Check(t.Treasury >= next.Cost, $"{next.Cost:N0} gold for the charter"), 13);
            var missing = City.Missing(t);
            p.Buttons(($"Claim the charter of a {next.Name.ToLowerInvariant()}", missing.Count == 0, true, () => { _sim.RaiseCharter(); _dirty = true; }));
            p.Note(Ui.Esc(next.Blurb));
        }
        else p.Note("The greatest charter there is. From here the city only grows richer and its houses grander.");

        p.Heading("Wards");
        foreach (var w in t.Content.City.Wards)
        {
            bool open = t.City.OpenWards.Contains(w.Id);
            string? why = City.WardBlocked(t, w.Id);
            string where = w.Ring == 0 ? "the heart of town" : $"{(w.Outer ? "outer" : "inner")} {w.Side}";
            string status = open ? Tag("laid out", Ui.Good) : City.Reached(t, w.Tier) ? Tag($"{w.Cost:N0}g") : Tag($"needs {City.TierName(t, w.Tier).ToLowerInvariant()}");
            string favour = w.Favours != Zone.None ? $" Favours {Planning.ZoneName(w.Favours)}." : "";
            string text = $"[b]{Ui.Esc(w.Name)}[/b]  {status}  {Ui.C(Ui.Ink2, $"[font_size=11]{where}[/font_size]")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(w.Blurb)}{favour}[/font_size]")}";
            var id = w.Id;
            if (open) p.Row(text, ("Show", true, false, () => LookAtWard(id)));
            else p.Row(text, ("Show", true, false, () => LookAtWard(id)), ("Lay out", why == null, true, () => { _sim.OpenWard(id); _dirty = true; }));
        }

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
            string needs = h.Goods.Count + h.Services.Count == 0 ? "Wants nothing but a roof."
                : wants == "" ? "Has all it wants." : $"Short of {wants}.";
            string grow = h.Next != "" ? $" Grows into a {t.Content.Building(h.Next).Name.ToLowerInvariant()} when its family has what that wants." : "";
            p.Row($"[b]{Ui.Esc(def.Name)}[/b]  {Tag($"{homes.Count}")}  {Ui.C(Ui.Ink2, $"[font_size=11]{people} living · {h.Tax:0.##}g hearth tax a day[/font_size]")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{needs}{grow}[/font_size]")}");
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
        p.Commit();
    }

    /// <summary>The season: what it does to the town, the winter stores, and the visitors it brings.</summary>
    private void RenderSeason(Pane p, Town t)
    {
        var s = Seasons.Current(t);
        var st = t.Seasons;
        string festival = Seasons.Festival(t) && s.Festival != null ? "  " + Tag(Ui.Cap(s.Festival.Name), Ui.Good) : "";
        p.Heading($"{s.Name}, year {Seasons.Year(t)}");
        p.Text($"{Tag($"day {Seasons.DayOf(t)} of {Seasons.Rules(t).Days}")}{festival}  {Ui.C(Ui.Ink2, $"[font_size=12]{Seasons.Next(t).Name} in {Ui.Days(Seasons.DaysLeft(t))}[/font_size]")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(s.Blurb)}[/font_size]")}", 13);
        var effects = new List<string>();
        double food = s.YieldOf(Good.Food);
        if (Math.Abs(food - 1) > 0.05) effects.Add($"fields yield {Math.Round(food * 100)}%");
        foreach (var (g, y) in s.Yields)
            if (g != Good.Food && Math.Abs(y - 1) > 0.15) effects.Add($"{g.ToString().ToLowerInvariant()} {Math.Round(y * 100)}%");
        if (Math.Abs(s.Build - 1) > 0.05) effects.Add($"building {Math.Round(s.Build * 100)}% as fast");
        if (s.Mood != 0) effects.Add($"mood {(s.Mood > 0 ? "+" : "")}{s.Mood}");
        effects.Add($"caravans charge {s.ImportPremium:0.#}× for what the town lacks");
        if (s.MarchDays > 0) effects.Add($"armies march {Ui.Days(s.MarchDays)} slower");
        var busy = s.Menace.Where(kv => kv.Value >= 1.25).Select(kv => kv.Key).ToList();
        var quiet = s.Menace.Where(kv => kv.Value <= 0.7).Select(kv => kv.Key).ToList();
        if (s.Warband >= 1.25) busy.Insert(0, "the warband");
        if (s.Warband <= 0.7) quiet.Insert(0, "the warband");
        if (busy.Count > 0) effects.Add($"{Expedition.ListNames(busy)} on the move");
        if (quiet.Count > 0) effects.Add($"{Expedition.ListNames(quiet)} lie low");
        p.Note(Ui.Cap(string.Join(" · ", effects)) + ".");

        double cap = Seasons.Stores(t);
        double full = Seasons.StoresFull(t);
        bool holding = s.Hold;
        p.Meter("Winter stores", Math.Min(1, full) * 100, $"{Math.Round(Math.Min(t.Market.Stock(Good.Food), cap)):N0} / {cap:N0}", holding && full < 0.5);
        var (more, cost) = Seasons.LayInQuote(t);
        string why = !holding ? "Stores are laid in from autumn, when the harvest comes in and the cellars hold it back from the barges."
            : more <= 0 ? "The cellars and granaries are full." : $"Buy {more:N0} loads of grain off the traders for the winter.";
        if (holding && more > 0) p.Row(Ui.C(Ui.Ink2, $"[font_size=12]{why}[/font_size]"), ($"Lay in stores · {cost:N0}g", t.Treasury >= cost, true, () => { _sim.LayInStores(); _dirty = true; }));
        else p.Note(why + " Granaries hold more.");
        p.Text($"{Tag($"{st.Tourists} visitors")}  {Ui.C(Ui.Ink2, $"[font_size=12]spent {st.TouristGold:N0}g yesterday and {st.SeasonGold:N0}g this season at the taverns and shops[/font_size]")}", 13);
    }

    /// <summary>Why settlers come or stay away: each reason with its sign, as the arrivals roll counts it.</summary>
    private static void RenderSettlers(Town t, Pane p)
    {
        var draw = Population.Appeal(t);
        double week = Population.WeeklySettlers(t, draw.Total);
        p.Heading("Why settlers come or don't");
        foreach (var part in draw.Parts)
            p.Text($"{Ui.C(part.Value >= 0 ? Ui.Good : Ui.Bad, $"{(part.Value >= 0 ? "+" : "−")}{Math.Abs(part.Value):0.00}")}  {Ui.Esc(part.Label)}", 13);
        p.Note($"Draw {draw.Total:0.00} of at most 1.60: about {Math.Round(week)} newcomers a week. Taxes above {Math.Round(t.Content.Rules.TaxRate * 100)}% and idle hands cost the most; open jobs and empty beds help.");
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
            string text = $"[b]{Ui.Esc(d.Name)}[/b]  {status}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(GrandWorks.Bonus(d))}. Upkeep {d.Upkeep:0.#}g a day.[/font_size]")}";
            var id = d.Id;
            if (b == null) p.Row(text, ($"Begin · {GrandWorks.StageCost(t, d, 0):N0}g", why == null, true, () => PlaceFromCity(id)));
            else p.Row(text, ("Show", true, false, () => LookAtBuilding(b)));
            if (b is { IsSite: true })
            {
                p.Meter("Built", b.BuildProgress / d.BuildHours * 100, $"{Math.Round(b.BuildProgress / d.BuildHours * 100)}%");
                if (GrandWorks.Waiting(b))
                {
                    double cost = GrandWorks.StageCost(t, d, b.Stage);
                    p.Note($"Next: {d.Stages[b.Stage].Name}, {cost:N0}g with its goods and wages. It starts on its own once the treasury holds that and {GrandWorks.Reserve:N0}g to spare.");
                }
                else p.Note($"Building {d.Stages[Math.Max(0, b.Stage - 1)].Name}.");
            }
        }
        p.Note("Great projects that take seasons to raise. Each stage draws its gold and goods when the last is done, and the finished work helps the whole town for good.");
    }

    private void LookAtBuilding(Building b)
    {
        var lot = _sim.Town.Lot(b.LotId);
        Look(new Vector2(Geo.Margin + (float)lot.X, Geo.Margin + (float)lot.Y), 30);
    }

    private static string? Previous(Town t, string housingId) => City.Previous(t, housingId)?.Id;

    private static string Check(bool ok, string text) => (ok ? Ui.C(Ui.Good, "✓ ") : Ui.C(Ui.Ink2, "○ ")) + text;

    /// <summary>Open the build bar on the government group with this building picked.</summary>
    private void PlaceFromCity(string id)
    {
        OpenPlan("Government", id, null);
        _dirty = true;
    }

    private void LookAtWard(string wardId)
    {
        var t = _sim.Town;
        var lots = t.Lots.Where(l => City.WardOf(t, l).Id == wardId).ToList();
        if (lots.Count == 0) return;
        var c = new Vector2((float)lots.Average(l => l.X), (float)lots.Average(l => l.Y));
        Look(new Vector2(Geo.Margin + c.X + 0.5f, Geo.Margin + c.Y + 0.5f), 40);
    }
}
