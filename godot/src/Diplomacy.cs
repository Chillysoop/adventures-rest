using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;
using Sky = CrownAndCompass.Sim.Systems.Sky;

namespace CrownAndCompass.Client;

/// <summary>
/// The diplomacy window: every people the town has heard of out in the wilds, what they think of the
/// town and what an alliance would bring, envoys with gifts, and the trade routes the town runs to them.
/// </summary>
public partial class Main
{
    private static (string Word, Color Color) StandingWord(Settlement s, double ally) =>
        s.Razed ? ("Gone", Ui.Ink2)
        : s.Allied ? ("Allied", Ui.Good)
        : s.Standing >= ally * 0.5 ? ("Friendly", Ui.Good)
        : s.Standing >= 10 ? ("Cordial", Ui.Ink)
        : s.Standing >= -20 ? ("Wary", Ui.Gold)
        : ("Hostile", Ui.Bad);

    /// <summary>Its band on the standing bar, 1 (hostile) to 5 (allied).</summary>
    private static int StandingBand(Settlement s, double ally) =>
        s.Allied ? 5 : s.Standing >= ally * 0.5 ? 4 : s.Standing >= 10 ? 3 : s.Standing >= -20 ? 2 : 1;

    /// <summary>A settlement's own life: its leader, how it is doing, its band, its trouble and what the town has built there.</summary>
    private static string Life(Town t, Settlement s)
    {
        var lead = Neighbours.LeaderOf(t, s);
        string Level(double v) => v >= 70 ? "high" : v >= 45 ? "fair" : v >= 25 ? "low" : "dire";
        Color Shade(double v) => v >= 45 ? Ui.Good : v >= 25 ? Ui.Gold : Ui.Bad;
        string ruler = s.Reign != null ? Reigns.Describe(t, s) : $"{s.Leader}, their {lead.Title}";
        string lines = $"[font_size=12]Led by {Ui.C(Reigns.Puppet(s) ? Ui.Bad : Ui.Ink, Ui.Esc(ruler))} · prosperity {Ui.C(Shade(s.Prosperity), Level(s.Prosperity))} · security {Ui.C(Shade(s.Security), Level(s.Security))}";
        if (s.Reign != null)
        {
            string temper = Reigns.Puppet(s) ? "" : Ui.Cap(Reigns.TemperOf(t, s).Text) + ".";
            string was = Reigns.Succession(t, s);
            if (temper != "" || was != "") lines += "\n" + Ui.C(Ui.Ink2, Ui.Esc($"{temper}{(temper != "" && was != "" ? " " : "")}{(was != "" ? was + "." : "")}"));
        }
        if (Shadows.Shadowed(s) && Shadows.Over(t, s) is { } dark)
        {
            var sh = s.Shadow!;
            string form = sh.Form switch
            {
                "dread" => "lives in dread of",
                "tribute" => "pays tribute to",
                "cult" => "sings the hymns of",
                _ => "is ruled for",
            };
            string from = sh.SourceId is string src && Quests.Site(t, src) is { } srcSite ? $"; clear {Ui.Esc(srcSite.Def.Name)} to loosen its grip" : "";
            lines += "\n" + Ui.C(sh.Form == "dread" ? Ui.Gold : Ui.Bad, $"In shadow: {Ui.Esc(s.Name)} {form} {Ui.Esc(dark.Name)} ({Math.Round(sh.Depth)}% deep){from}.");
            if (sh.RallyHeroId is int hid)
                lines += "\n" + Ui.C(Ui.Ink2, $"{Ui.Esc(t.Agent(hid).GivenName)} is {(sh.Rallied ? "on the way home" : "on the way to rally them")}.");
        }
        if (s.Band is { } b)
            lines += b.Members > 0
                ? $"\n{Ui.Esc(Ui.Cap(b.Name))}: {b.Members} strong, level {b.Level}, {b.Wins} {(b.Wins == 1 ? "win" : "wins")}{(b.Home ? "" : ", out on a job")}"
                : $"\n{Ui.Esc(Ui.Cap(b.Name))} were broken; {Ui.Esc(s.Name)} is raising another";
        if (s.Band?.CaptiveAt is string at && Quests.Site(t, at) is { } held)
            lines += $"\n{Ui.C(Ui.Bad, $"Their captain {Ui.Esc(s.Band.CaptiveName ?? "")} is held at {Ui.Esc(held.Def.Name)}: clear it to bring them home.")}";
        if (Neighbours.TroubleOf(t, s) is { } tr)
        {
            string where = s.Trouble!.SiteId is string sid && Quests.Site(t, sid) is { } site ? $", from {Ui.Esc(site.Def.Name)}" : "";
            lines += $"\n{Ui.C(s.Trouble.Severity >= 60 ? Ui.Bad : Ui.Gold, $"Trouble: {Ui.Esc(tr.Name)}{where} ({Math.Round(s.Trouble.Severity)}% to the worst)")}";
        }
        if (Plots.Tolled(t, s) is { } toll) lines += $"\n{Ui.C(Ui.Bad, $"{Ui.Esc(Ui.Cap(toll.Name))} holds the road here and tolls it.")}";
        var built = s.Projects.Select(id => t.Content.Neighbours.Project(id)?.Name ?? id).ToList();
        if (s.Works is string w && t.Content.Neighbours.Project(w) is { } wp) built.Add($"{wp.Name} (building, {Ui.Days(Math.Max(0, s.WorksDoneDay - t.Day))})");
        if (built.Count > 0) lines += $"\nBuilt by {Ui.Esc(t.Name)}: {Ui.Esc(string.Join(", ", built))}";
        return lines + "[/font_size]";
    }

    /// <summary>A score on a character sheet, coloured by how strong it is.</summary>
    /// <summary>The icon for each score slot, in character.json's slots.</summary>
    private static readonly Dictionary<string, string> ScoreIcons = new()
    {
        ["might"] = "defence", ["wealth"] = "gold", ["diplomacy"] = "standing", ["health"] = "health",
        ["lore"] = "books", ["faith"] = "faith", ["wits"] = "wits", ["reach"] = "population",
        ["dread"] = "dread", ["will"] = "will",
    };

    /// <summary>A score as its icon and number; the name shows on hover.</summary>
    internal static string ScoreTag(Score x) =>
        $"[hint={x.Name}]{(ScoreIcons.TryGetValue(x.Slot, out var icon) ? Ui.Img(icon, 14) : Ui.Esc(x.Name))} " +
        Ui.C(x.Value >= 14 ? Ui.Good : x.Value <= 7 ? Ui.Bad : Ui.Ink, $"[b]{x.Value}[/b]") + "[/hint]";

    /// <summary>A settlement's character sheet: size and purpose, its six scores, then customs, secret and a saying as bullets.</summary>
    private static string CharacterSheet(Town t, Settlement s)
    {
        string size = Character.SizeOf(t, s)?.Name ?? s.Kin.Kind;
        string purpose = Character.PurposeOf(t, s) is { } pu ? $" · {Ui.Esc(pu.Name)}" : "";
        string lines = $"[font_size=12]{Tag(Ui.Cap(size))}{purpose}\n{string.Join("   ", Character.Sheet(t, s).Select(ScoreTag))}";
        foreach (var c in Character.CustomsOf(t, s)) lines += "\n• " + Ui.C(Ui.Ink2, Ui.Esc(Character.Fill(s, c.Text)));
        if (Character.SecretOf(t, s) is { } secret)
            lines += "\n• " + (Character.SecretKnown(t, s)
                ? Ui.C(Ui.Gold, $"Secret: {Ui.Esc(Ui.Cap(Character.Fill(s, secret.Text)))}.")
                : Ui.C(Ui.Ink2, "A secret, told only to friends."));
        if (Character.Proverb(t, s, s.Id + t.Day / 30) is string saying) lines += "\n" + Ui.C(Ui.Ink2, $"[i]{Ui.Esc(saying)}[/i]");
        return lines + "[/font_size]";
    }

    /// <summary>The people there who remember the town, and what they remember.</summary>
    private static string Cast(Town t, Settlement s)
    {
        var cast = Neighbours.CastOf(t, s).Where(c => c.Memories.Count > 0).OrderByDescending(c => c.Regard).ThenBy(c => c.Id).Take(3).ToList();
        if (cast.Count == 0) return Ui.C(Ui.Ink2, "[font_size=11]No one there owes the town anything yet.[/font_size]");
        return string.Join("\n", cast.Select(c =>
            Ui.C(Ui.Ink2, $"[font_size=11]{Ui.Esc(c.Name)}, {Ui.Esc(c.Role == "leader" ? Neighbours.LeaderOf(t, s).Title : c.Role)}{(c.Regard >= t.Content.Neighbours.Cast.Regard ? ", a friend" : "")}: remembers {Ui.Esc(c.Memories[^1])}[/font_size]")));
    }

    /// <summary>The people picked for the airship's next run in the Diplomacy window (-1: the best paying).</summary>
    private int _skyPort = -1;

    /// <summary>The sky dock's airship: where it is, and the run to send it on (<see cref="Skyship"/>).</summary>
    private void SkyRuns(Pane p, Town t)
    {
        if (Institutions.Airships(t) == 0) return;
        var sky = t.Trade.Sky;
        var r = t.Content.Trade.Sky;
        p.Heading("The airship");
        string status;
        if (sky.Out is { } run)
            status = $"Bound for [b]{Ui.Esc(t.Settlements[run.SettlementId].Name)}[/b] with {Js.Str(Js.Round(run.Qty))} {Ui.Esc(run.Cargo.ToString().ToLowerInvariant())} · home in {Ui.Days(Math.Max(0, run.BackDay - t.Day))}{(run.Stormed ? ", held back by storms" : "")}";
        else if (t.Parties.FirstOrDefault(x => x.Airship && !x.Returned) is { } party)
            status = $"Out carrying [b]{Ui.Esc(party.Name)}[/b] to their job: it can't trade until it brings them home";
        else status = "At anchor over the Sky Dock, its hold empty";
        p.Row($"[font_size=13]{status}[/font_size]\n" +
              Ui.C(Ui.Ink2, $"[font_size=12]Hold {Js.Str(Js.Round(Skyship.Hold(t)))} goods · {sky.Runs} {(sky.Runs == 1 ? "run" : "runs")} flown · sold {Js.Str(Js.Round(sky.Earned))}g, spent {Js.Str(Js.Round(sky.Spent))}g[/font_size]") +
              (sky.LastLine != "" ? "\n" + Ui.C(Ui.Ink2, $"[font_size=11]Last run: {Ui.Esc(sky.LastLine)}[/font_size]") : ""));
        if (Passage.AirshipsFree(t) <= 0) return;
        var ports = Skyship.Ports(t).ToList();
        if (ports.Count == 0)
        {
            p.Note("No people the town knows of will let it moor. Explorers and envoys find the airship somewhere to fly.");
            return;
        }
        var cargo = Goods.All.Where(g => Skyship.Spare(t, g) >= r.MinCargo).ToList();
        if (cargo.Count == 0)
        {
            p.Note("The town has nothing to spare for the hold: the market keeps back what the town needs. Surplus builds up as the makers outrun the households.");
            return;
        }
        // Each people's best cargo, so the picker can say what they pay best for.
        var best = ports.ToDictionary(s => s.Id, s => cargo.OrderByDescending(g => Skyship.Profit(t, s, g)).ThenBy(g => g).First());
        if (ports.All(s => s.Id != _skyPort))
            _skyPort = ports.OrderByDescending(s => Skyship.Profit(t, s, best[s.Id]) / Skyship.FlightDays(t, s)).ThenBy(s => s.Id).First().Id;
        p.Text(Ui.C(Ui.Ink2, "Where to? Each people pays its own price, more far out and for what they want, and less the more you have sold them lately."), 12);
        // Three to a row so a long list of peoples wraps inside the window.
        foreach (var row in ports.Chunk(3))
            p.Buttons(row.Select(s =>
            {
                int sid = s.Id;
                return ($"{s.Name} · {Skyship.FlightDays(t, s)}d · {best[s.Id]}", true, sid == _skyPort, (Action)(() => { _skyPort = sid; _dirty = true; }));
            }).ToArray());
        var port = t.Settlements[_skyPort];
        var region = t.Regions[port.RegionId];
        double danger = Skyship.AmbushChance(t, port);
        p.Text($"[b]{Ui.Esc(port.Name)}[/b] " + Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(port.People)} in {Ui.Esc(region.Name)} · {Ui.Days(Skyship.FlightDays(t, port))} there and back by air, {Ui.Days(Trade.TripDays(t, port))} by road · fit-out {Js.Str(Skyship.FitOut(t, port))}g" +
               (danger > 0 ? $" · fliers {Math.Round(danger * 100)}%" : "") + "[/font_size]"), 13);
        foreach (var g in cargo.OrderByDescending(g => Skyship.Profit(t, port, g)).ThenBy(g => g))
        {
            double qty = Skyship.Spare(t, g), gold = Skyship.Quote(t, port, g, qty), profit = gold - Skyship.FitOut(t, port);
            string back = Skyship.Bring(t, port, g) is { } b ? $"Brings back {b.Good.ToString().ToLowerInvariant()} at {Js.ToFixed(b.Price, 1)}g" : "Brings back gold only";
            string? no = Skyship.Refuse(t, port, g);
            var cg = g;
            int sid = port.Id;
            p.Row($"[font_size=13][b]{g}[/b] · {Js.Str(Js.Round(qty))} to spare · {Js.ToFixed(Skyship.Price(t, port, g), 1)}g each there (home {Js.ToFixed(t.Market.BasePrice(g), 1)}g)[/font_size]\n" +
                  Ui.C(profit > 0 ? Ui.Good : Ui.Bad, $"[font_size=12]Fetches about {Js.Str(Js.Round(gold))}g, clears {(profit >= 0 ? "" : "−")}{Js.Str(Math.Abs(Js.Round(profit)))}g after the fit-out[/font_size]") + "\n" +
                  Ui.C(Ui.Ink2, $"[font_size=11]{Ui.Esc(back)}{(no != null ? $" · {Ui.Esc(no)}" : "")}[/font_size]"),
                ("Send", no == null, profit > 0, () => Act(() => _sim.SendSkyship(sid, cg))));
        }
    }

    private bool _skyMarines = true;

    /// <summary>The Sky Dock's airships (<see cref="Sky"/>): each one's hull, lift and job, and the flights it can take now.</summary>
    private void SkyFleet(Pane p, Town t)
    {
        if (!t.SkyFleet.Granted || Sky.Dock(t) == null) return;
        var r = Sky.Rules(t);
        p.Heading("Sky fleet", "ui-airship");
        var free = Sky.Marines(t).OrderByDescending(h => h.Hero!.Level).ThenBy(h => h.Id).ToList();
        p.Buttons(($"{(_skyMarines ? "✓" : "○")} Marines aboard ({free.Count} free)", true, _skyMarines, () => { _skyMarines = !_skyMarines; _dirty = true; }));
        foreach (var s in Sky.Ships(t))
        {
            var d = Fleet.Def(t, s);
            int id = s.Id;
            string state = s.Flight is { } f
                ? $"{Ui.Img(f.Job == "strike" ? "ui-ballista" : f.Job == "scout" ? "ui-eye" : "defence", 12)} {f.Job} · back in {Ui.Days(Math.Max(0, f.BackDay - t.Day))}"
                : s.DownedRegion is int dr ? Ui.C(Ui.Bad, $"down in {Ui.Esc(t.Regions[dr].Name)}{(s.SalvageDay is int sd ? $" · riggers in {Ui.Days(Math.Max(0, sd - t.Day))}" : "")}")
                : Skyship.Flying(t, s.Id) ? "on a merchant run" : "at the mast";
            int won = s.Won, fights = s.Fights;
            var acts = new List<(string, bool, bool, Action)>();
            if (s.DownedRegion != null)
                acts.Add(($"Salvage {Js.Str(r.Salvage)}g", s.SalvageDay == null && t.Treasury >= r.Salvage, true, () => Act(() => _sim.SalvageShip(id))));
            else if (Sky.Moored(t, s))
            {
                if (s.Lift < d.Lift) acts.Add(($"Charge {Js.Str(Sky.ChargeCost(t, s))}g", t.Treasury >= Sky.ChargeCost(t, s), false, () => Act(() => _sim.ChargeShip(id))));
                if (s.Hull < d.Hull) acts.Add(($"Patch {Js.Str(Fleet.RepairCost(t, s))}g", t.Treasury >= Fleet.RepairCost(t, s), false, () => Act(() => _sim.RepairShip(id))));
            }
            p.Row($"[font_size=13][b]{Ui.Esc(Ui.Cap(s.Name))}[/b] · {Ui.Esc(d.Name)}[/font_size]\n" +
                  $"[font_size=12]{Ui.Chips(Ui.Chip("defence", $"{Js.Str(Js.Round(s.Hull))}/{Js.Str(d.Hull)}", s.Hull < d.Hull * 0.5 ? Ui.Bad : null, "Hull"),
                      Ui.Chip("ui-lift", $"{s.Lift}/{d.Lift}", s.Lift < d.Lift * 0.5 ? Ui.Gold : null, "Lift-crystal: a day aloft each"),
                      Ui.Chip("ui-ballista", $"{d.Guns}", null, "Ballistae"),
                      Ui.Chip("unit-sky-marines", $"{d.Marines}", null, "Marines it carries"),
                      fights > 0 ? Ui.Chip("ui-skull", $"{won}/{fights}", won * 2 >= fights ? Ui.Good : Ui.Bad, "Fights won") : "")}[/font_size]\n" +
                  Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(s.Captain)} ({Ui.Esc(Fleet.ProwessName(t, s))}) · {state}[/font_size]"),
                [.. acts]);
            if (!Sky.Moored(t, s) || Skyship.Flying(t, s.Id)) continue;
            var marines = _skyMarines ? free.Take(d.Marines).Select(h => h.Id).ToList() : [];
            var jobs = new List<(string, bool, bool, Action)>();
            jobs.Add(($"Patrol · {Ui.Days(r.PatrolDays)}", Sky.Refuse(t, s, "patrol", -1, "", marines) == null, false, () => Act(() => _sim.FlyShip(id, "patrol", marines: marines))));
            // Scout the nearest land not yet charted; strike the nearest lair the town has heard of.
            var scout = t.Regions.Where(x => x.Ring > 0 && !x.Known && Sky.Refuse(t, s, "scout", x.Id, "", marines) == null).OrderBy(x => x.Days).ThenBy(x => x.Id).FirstOrDefault();
            if (scout != null)
            {
                int rid = scout.Id;
                jobs.Add(($"Scout {scout.Name} · {Ui.Days(Sky.Days(t, s, scout))}", true, false, () => Act(() => _sim.FlyShip(id, "scout", rid, marines: marines))));
            }
            var lair = t.Sites.Where(x => !x.Cleared && x.Known != SiteKnowledge.Unheard && x.RegionId is int rg && Sky.Refuse(t, s, "strike", rg, x.Def.Id, marines) == null)
                .OrderBy(x => t.Regions[x.RegionId!.Value].Days).ThenBy(x => x.Def.Id, StringComparer.Ordinal).FirstOrDefault();
            if (lair != null)
            {
                int rg = lair.RegionId!.Value;
                string sid = lair.Def.Id;
                jobs.Add(($"Strike {lair.Def.Name} · {Ui.Days(Sky.Days(t, s, t.Regions[rg]))}", true, true, () => Act(() => _sim.FlyShip(id, "strike", rg, sid, marines))));
            }
            p.Buttons([.. jobs]);
        }
        var last = t.SkyFleet.Battles.LastOrDefault();
        if (last != null)
            p.Text(Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Img("ui-skull", 12)} Last fight: {Ui.Esc(r.Foes.FirstOrDefault(k => k.Id == last.Foe)?.Name ?? last.Foe)} · {last.Outcome}[/font_size]"));
        // The kinds the charter allows now, and the next one up to aim for.
        var kinds = r.Ships.Where((d, i) => i == 0 || r.Ships[i - 1].Tier == "" || City.Reached(t, r.Ships[i - 1].Tier)).ToList();
        p.Buttons(kinds.Select(d =>
        {
            string kind = d.Id;
            return ($"+ {d.Name} {Js.Str(Fleet.Cost(t, d))}g", Sky.BuildBlocked(t, kind) == null, false, (Action)(() => Act(() => _sim.BuildShip(kind))));
        }).ToArray());
    }

    /// <summary>The harbour's ships: each one's state, its best run, and new hulls to lay down (<see cref="Fleet"/>).</summary>
    private void FleetRuns(Pane p, Town t)
    {
        if (!Fleet.Coastal(t) || Fleet.Harbour(t) == null) return;
        p.Heading("Fleet");
        var ports = Fleet.Ports(t).ToList();
        foreach (var s in Fleet.AtSea(t))
        {
            var d = Fleet.Def(t, s);
            int id = s.Id;
            string state = s.Out is { } v
                ? $"at sea to {Ui.Esc(t.Settlements[v.SettlementId].Name)} · back in {Ui.Days(Math.Max(0, v.BackDay - t.Day))}"
                : $"moored · fishing {Ui.Img("food", 13)}";
            p.Row($"[font_size=13][b]{Ui.Esc(s.Name)}[/b] · {Ui.Esc(d.Name)}[/font_size]\n" +
                  Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(s.Captain)} ({Ui.Esc(Fleet.ProwessName(t, s))}) · {Ui.Img("defence", 12)} hull {Js.Str(Js.Round(s.Hull))}/{Js.Str(d.Hull)} · {state}[/font_size]"),
                ($"Patch {Js.Str(Fleet.RepairCost(t, s))}g", s.Out == null && s.Hull < d.Hull && t.Treasury >= Fleet.RepairCost(t, s), false, () => Act(() => _sim.RepairShip(id))));
            if (s.Out != null) continue;
            // Its best run now: the port and cargo that clear the most a day.
            var best = ports.SelectMany(port => Goods.All.Where(g => Fleet.Refuse(t, s, port, g) == null).Select(g => (P: port, G: g, Gain: Fleet.Profit(t, s, port, g))))
                .OrderByDescending(x => x.Gain / Fleet.Days(t, s, x.P)).ThenBy(x => x.P.Id).ThenBy(x => x.G).FirstOrDefault();
            if (best.P == null) continue;
            int pid = best.P.Id;
            var cargo = best.G;
            p.Row(Ui.C(Ui.Ink2, $"[font_size=12]• {Ui.Esc(best.P.Name)} · {Ui.Days(Fleet.Days(t, s, best.P))} · {cargo.ToString().ToLowerInvariant()} · {Ui.Img("gold", 12)} ~{Js.Str(Js.Round(best.Gain))}[/font_size]"),
                ("Sail", true, best.Gain > 0, () => Act(() => _sim.SendShip(id, pid, cargo))));
        }
        p.Buttons(t.Content.Sea.Ships.Select(d =>
        {
            string kind = d.Id;
            return ($"+ {d.Name} {Js.Str(Fleet.Cost(t, d))}g", Fleet.BuildBlocked(t, kind) == null, false, (Action)(() => Act(() => _sim.BuildShip(kind))));
        }).ToArray());
    }

    private void RenderDiplomacy()
    {
        var p = _panes[Tab.Diplomacy];
        var t = _sim.Town;
        var w = t.Content.World.Standing;
        var tr = t.Content.Trade;
        p.Begin();
        var known = t.Settlements.Where(s => Neighbours.Known(t, s)).OrderBy(s => s.Razed).ThenByDescending(s => Shadows.Shadowed(s)).ThenByDescending(s => s.Allied).ThenByDescending(s => s.Standing).ThenBy(s => s.Id).ToList();
        int allies = known.Count(s => s.Allied);
        var open = Trade.Open(t).OrderBy(r => r.Id).ToList();
        p.Card(Ui.Chips(Ui.Chip("population", $"{known.Count}", Ui.Ink, "Peoples known"), Ui.Chip("standing", $"{allies}", Ui.Good, "Allied"), Ui.Chip("ui-trade", $"{open.Count}/{Trade.Cap(t)}", Ui.Gold, $"Trade routes running; the {City.Tier(t).Name.ToLowerInvariant()}'s charter allows {Trade.Cap(t)}")), Ui.Gold);

        RenderPowers(p, t);

        p.Heading("Trade routes");
        if (open.Count == 0)
            p.Note(Trade.Cap(t) == 0
                ? "A hamlet's charter lets the town run its first trade route. Caravans sell the town's surplus to a people that wants it and bring back what the town is short of."
                : $"No caravans on the road. Any people at {Js.Str(tr.MinStanding)} standing or better will trade: open a route below.");
        foreach (var r in open)
        {
            var s = Trade.SettlementOf(t, r);
            int days = Math.Max(0, r.NextDay - t.Day);
            double net = r.Earned - r.Spent;
            int id = r.Id;
            string ex = r.Export.ToString().ToLowerInvariant(), im = r.Import.ToString().ToLowerInvariant();
            double risk = Trade.RaidChance(t, s);
            p.Row($"[b]{Ui.Esc(s.Name)}[/b]  {Ui.Dim($"[font_size=12]{Ui.Esc(s.People)}[/font_size]")}\n" +
                  $"{Ui.Tip($"{Ui.Img(ex, 16)} → {Ui.Img(im, 16)}", $"{r.Export} out, {r.Import} back")}  {Ui.Chip("ui-time", Ui.Days(days), Ui.Ink2, "Next caravan home")}  {Ui.Chip("ui-skull", $"{Math.Round(risk * 100)}%", risk >= 0.2 ? Ui.Bad : risk > 0 ? Ui.Gold : Ui.Good, "Road danger")}\n" +
                  $"[font_size=12]{Ui.Chips(Ui.Chip("ui-trade", $"{r.Wagons}", null, "Wagons"), Ui.Chip("ui-road", $"{r.Caravans}", null, "Runs"), r.Raided > 0 ? Ui.Chip("ui-wound", $"{r.Raided}", Ui.Bad, "Waylaid") : "", Ui.Chip("gold", $"{Js.Str(Js.Round(r.Earned - r.Spent))}", r.Earned >= r.Spent ? Ui.Good : Ui.Bad, $"Net: sold {Js.Str(Js.Round(r.Earned))}g, bought {Js.Str(Js.Round(r.Spent))}g"))}[/font_size]",
                Trade.WagonCost(t, r) is double wagon
                    ? [($"Add a wagon · {Js.Str(wagon)}g", t.Treasury >= wagon, true, () => Act(() => _sim.AddWagon(id))), ("Close", true, false, () => Act(() => _sim.CloseTradeRoute(id)))]
                    : [("Close", true, false, () => Act(() => _sim.CloseTradeRoute(id)))]);
        }

        SkyRuns(p, t);
        SkyFleet(p, t);
        FleetRuns(p, t);

        p.Heading("Peoples of the wilds");
        if (known.Count == 0) p.Note("No peoples met yet · their riders come as word spreads.");
        foreach (var s in known)
        {
            var region = t.Regions[s.RegionId];
            var (word, color) = StandingWord(s, w.Ally);
            int id = s.Id;
            var trade = s.Kin.Trade;
            string Icons(IEnumerable<Good> gs) => string.Concat(gs.Select(g => Ui.Tip(Ui.Img(g.ToString().ToLowerInvariant(), 16), g.ToString())));
            var offers = Trade.Offers(t, s);
            string goods = offers.Count == 0 ? Ui.Dim("No trade")
                : $"{Ui.Dim("want")} {Icons(trade.Wants)}   {Ui.Dim("sell")} {Icons(offers)}";
            int pleas = t.Pleas.Count(x => x.SettlementId == s.Id && x.Status is PleaStatus.Open or PleaStatus.Taken);
            string boon = s.Kin.Boon.Text != "" ? Ui.Cap(s.Kin.Boon.Text.Replace("{S}", s.Name)) + (s.Kin.Boon.Troops > 0 ? ", and their warriors come if the walls are attacked." : ".") : "";
            string head = $"[b]{Ui.Esc(s.Name)}[/b]{(s.Razed ? "  " + Tag(word, color) : "")}" +
                          $"{(pleas > 0 ? "  " + Tag(pleas == 1 ? "1 plea" : $"{pleas} pleas", Ui.Gold) : "")}\n" +
                          $"[font_size=12]{Ui.Tip(Ui.Dim($"{Ui.Esc(Ui.Cap(s.People))} {Ui.Img("ui-info", 12)}"), $"{Ui.Cap(s.People)}, {Agent.Article(s.Kin.Kind)} {s.Kin.Kind} in {region.Name}")}  {Ui.Chip("ui-road", Ui.Days(region.Days), Ui.Ink2, "Out")}[/font_size]" +
                          (s.Razed ? "" : $"\n[font_size=12]{goods}[/font_size]") +
                          (s.Razed || boon == "" ? "" : "\n" + $"[font_size=12]{Ui.Bullet(Ui.Tip(s.Allied ? Ui.C(Ui.Good, "Ally's boon") : Ui.Dim($"Boon at {Js.Str(w.Ally)}"), boon), "ui-star")}[/font_size]");
            if (s.Razed)
            {
                p.Row(head);
                continue;
            }
            head += "\n" + CharacterSheet(t, s) + "\n" + Life(t, s);
            var buttons = new List<(string, bool, bool, Action)>();
            if (!s.Allied || Shadows.Shadowed(s)) buttons.Add(($"Envoy · {Js.Str(w.EnvoyCost)}g · {DecisionOdds(t, "envoy")}", t.Treasury >= w.EnvoyCost, false, () => Act(() => _sim.SendEnvoy(id))));
            if (Shadows.Shadowed(s))
                buttons.Add((Shadows.Rallier(t) is { } speaker && s.Shadow!.RallyHeroId == null ? $"Send {speaker.GivenName} to rally them" : "Rally", Shadows.RallyRefused(t, s) == null, true, () => Act(() => _sim.RallySettlement(id))));
            if (Neighbours.TroubleOf(t, s) is { Kind: "need", Aid: { } aid })
                buttons.Add(($"Aid · {Js.Str(aid.Gold)}g, {Js.Str(aid.Qty)} {aid.Good.ToString().ToLowerInvariant()}", Neighbours.AidRefused(t, s) == null, true, () => Act(() => _sim.AidSettlement(id))));
            if (Conquest.Held(t, s) == null)
                buttons.Add(($"Buy charter · {Js.Str(Conquest.CharterPrice(t, s))}g", Conquest.BuyBlocked(t, s) == null, false, () => Act(() => _sim.BuySettlement(id))));
            else head += $"\n[font_size=12]{Ui.Chip("face-crown", "Sworn to you", Ui.Good, "The town holds its charter")}[/font_size]";
            if (!open.Any(r => r.SettlementId == s.Id) && offers.Count > 0)
            {
                string? no = Trade.Refuse(t, s);
                var (export, import) = Trade.Suggest(t, s);
                buttons.Add(no == null
                    ? ($"Trade {export} for {import} · {Js.Str(Trade.OpenCost(t, s))}g", true, true, () => Act(() => _sim.OpenTradeRoute(id, export, import)))
                    : ("Trade", false, false, () => { }));
                if (no != null) head += "\n" + Ui.C(Ui.Ink2, $"[font_size=11]No route yet: {Ui.Esc(no)}.[/font_size]");
            }
            int band = StandingBand(s, w.Ally);
            double standing = s.Standing;
            p.Row(head, ($"st{band}{word}{Math.Round(standing)}", null, () => Foes.Standing(band, word, standing)), buttons.ToArray());
            var build = Neighbours.ProjectsFor(t, s).Where(x => !s.Projects.Contains(x.Id)).ToList();
            if (s.Works == null && build.Count > 0)
                p.Buttons(build.Select(x =>
                {
                    string pid = x.Id;
                    return ($"{x.Name} · {Js.Str(x.Cost)}g", Neighbours.ProjectRefused(t, s, x) == null, false, (Action)(() => Act(() => _sim.FundProject(id, pid))));
                }).ToArray());
            p.Row(Cast(t, s));
        }
        p.Note("Each settlement has its own leader, its own band of adventurers and its own troubles. Aid meets a need; a project there lasts (a watchtower keeps it safe, a bridge or patrol keeps its road open, a waystation lets your parties rest). The people you help remember it.");
        p.Note("Rulers age, die and are overthrown, and each heir has a temperament of their own that warms or cools relations. A risen enemy's shadow spreads over the settlements near it: dread, then tribute (no trade, families fleeing to you), then a cult or a puppet ruler. Clear its site nearest them, send an envoy, or send a hero to rally them.");
        p.Note($"Envoys carry gifts, and each one pleases a little less than the last. Clearing trouble near a people, answering their pleas and every caravan that gets through all raise standing. At {Js.Str(w.Ally)} they ally with the town.");
        p.Commit();
    }
}
