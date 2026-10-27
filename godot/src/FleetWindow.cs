using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;
using Sky = CrownAndCompass.Sim.Systems.Sky;

namespace CrownAndCompass.Client;

/// <summary>
/// The fleet window (War group): the harbour's ships and the Sky Dock's airships, each with its hull, captain, job and
/// standing run, the corsairs' holds and the dues foreign ships pay. Merchant airship runs stay on Diplomacy's Trade page.
/// </summary>
public partial class Main
{
    private void RenderFleet()
    {
        var p = _panes[Tab.Fleet];
        var t = _sim.Town;
        p.Begin();
        StewardStrip(p, "fleet");
        var harbour = Fleet.Harbour(t);
        if (harbour == null && (!t.SkyFleet.Granted || Sky.Dock(t) == null))
        {
            p.Note(Fleet.Coastal(t) || Fleet.OnRiver(t) ? "The harbour rises with the town: its first boat comes with it." : "No harbour here · a Sky Dock at city charter flies airships.");
            p.Commit();
            return;
        }
        var holds = Fleet.Holds(t);
        double dues = Fleet.Dues(t);
        p.Card(Ui.Chips(
            Ui.Chip("inst-harbour", $"{Fleet.AtSea(t).Count()}/{Fleet.Berths(t)}", Ui.Ink, "Ships at the harbour / berths"),
            harbour != null ? Ui.Chip("population", $"{harbour.Workers.Count}/{Fleet.CrewNeeded(t)}", Fleet.Manned(t) < 1 ? Ui.Bad : Ui.Good, "Sailors at the harbour / hands the ships need") : "",
            dues > 0 ? Ui.Chip("gold", $"+{Js.Str(Js.Round(dues * 10) / 10)}/day", Ui.Good, "Dues from foreign ships at the quay") : "",
            Ui.Chip("ui-skull", $"{holds.Count}", holds.Count > 0 ? Ui.Bad : Ui.Good, holds.Count > 0 ? $"Corsair holds standing: {string.Join(", ", holds.Select(x => x.Def.Name))}. Clear them and the raids stop." : "No corsair hold stands")), Ui.Gold);
        FleetRuns(p, t);
        SkyFleet(p, t);
        p.Help(
            "Ships are crewed by sailors from the town. A captain is a townsperson: they learn with each voyage and can go down with the ship.",
            "A standing run sends a ship back to its port each time she is home, with the cargo that pays best there. Storms slow her; a fair wind brings her home sooner.",
            "Corsairs sail from their holds. Each hold makes their raids stronger; clear them all and only strays are left.");
        p.Commit();
    }

    private bool _skyMarines = true;
    private bool _seaMarines = true;

    /// <summary>The Sky Dock's airships (<see cref="Sky"/>): each one's hull, lift and job, and the flights it can take now.</summary>
    private void SkyFleet(Pane p, Town t)
    {
        if (!t.SkyFleet.Granted || Sky.Dock(t) == null) return;
        var r = Sky.Rules(t);
        p.Heading("Sky fleet", "ui-airship");
        var free = Sky.Marines(t).OrderByDescending(h => h.Hero!.Level).ThenBy(h => h.Id).ToList();
        p.Buttons(($"{(_skyMarines ? "✓" : "○")} Marines aboard ({free.Count} free)", true, _skyMarines, () => { _skyMarines = !_skyMarines; _dirty = true; }));
        // The fuel: the store, the traders' crystal this month, and the seams the companies found.
        double stock = Js.Round(Crystal.Stock(t) * 10) / 10;
        int sale = (int)Math.Min(6, Math.Floor(Crystal.ForSale(t)));
        string mines = string.Join(" ", Crystal.Seams(t).Where(x => x.Mined).Select(x => Ui.Chip("ui-lift", $"+{Js.Str(Js.Round(Crystal.Yield(t, x) * 10) / 10)}/day", x.Cleared ? Ui.Good : Ui.Bad, $"Crystal mine on {x.Def.Name}{(x.Cleared ? "" : ": retaken, yields nothing until cleared")}")));
        p.Row($"[font_size=12]{Ui.Chips(Ui.Chip("ui-lift", Js.Str(stock), stock < 4 ? Ui.Bad : null, "Lift-crystal in store: a charge burns a day of it for each day of lift" + (Research.Crystal(t) > 0 ? $", {Js.Str(Js.Round(Research.Crystal(t) * 100))}% less with what the town knows" : "")), mines)}[/font_size]",
            ($"Buy {sale} · {Js.Str(Crystal.Price(t, sale))}g", sale > 0 && Crystal.BuyRefused(t, sale) == null, false, () => Act(() => _sim.BuyCrystal(sale))));
        foreach (var seam in Crystal.Seams(t).Where(x => !x.Mined))
        {
            string sid = seam.Def.Id;
            p.Buttons(($"Mine {seam.Def.Name} · {Js.Str(r.Crystal.MineCost)}g", Crystal.MineRefused(t, seam) == null, true, () => Act(() => _sim.MineSeam(sid))));
        }
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
                if (s.Lift < d.Lift) acts.Add(($"Charge {Js.Str(Math.Floor(Sky.ChargeLift(t, s)))} · {Js.Str(Sky.ChargeCost(t, s))}g", Sky.ChargeLift(t, s) >= 1 && t.Treasury >= Sky.ChargeCost(t, s), false, () => Act(() => _sim.ChargeShip(id))));
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
        if (Fleet.Harbour(t) == null) return;
        p.Heading("Fleet");
        var ports = Fleet.Ports(t).ToList();
        var free = Voyages.Marines(t).OrderByDescending(h => h.Hero!.Level).ThenBy(h => h.Id).ToList();
        p.Buttons(($"{(_seaMarines ? "✓" : "○")} Marines aboard ({free.Count} free)", true, _seaMarines, () => { _seaMarines = !_seaMarines; _dirty = true; }));
        foreach (var s in Fleet.AtSea(t))
        {
            var d = Fleet.Def(t, s);
            int id = s.Id;
            var carrying = Voyages.Carrying(t, s);
            string state = s.Out is { } v
                ? $"at sea to {Ui.Esc(t.Settlements[v.SettlementId].Name)} · back in {Ui.Days(Math.Max(0, v.BackDay - t.Day))}"
                : s.Flight is { } f ? $"{Ui.Img(f.Job == "strike" ? "ui-ballista" : f.Job == "scout" ? "ui-eye" : "defence", 12)} {(f.Job == "strike" ? "bombard" : f.Job)} · back in {Ui.Days(Math.Max(0, f.BackDay - t.Day))}"
                : carrying != null ? $"{Ui.Img("heroes", 12)} carrying {Ui.Esc(carrying.Name)}"
                : $"{(Fleet.AtAnchor(t, s) ? "at anchor" : "moored")} · fishing {Ui.Img("food", 13)}";
            string guard = s.Guard == "watch" ? "Watch" : s.Guard == "hired" ? "Hired" : "No guard";
            string next = s.Guard == "" ? "watch" : s.Guard == "watch" ? "hired" : "";
            p.Row($"[font_size=13][b]{Ui.Esc(s.Name)}[/b] · {Ui.Esc(d.Name)}[/font_size]\n" +
                  $"[font_size=12]{Ui.Chips(Ui.Chip("defence", $"{Js.Str(Js.Round(s.Hull))}/{Js.Str(d.Hull)}", s.Hull < d.Hull * 0.5 ? Ui.Bad : null, "Hull"),
                      Ui.Chip("ui-ballista", $"{d.Guns}", null, "Guns"),
                      Ui.Chip("unit-sky-marines", $"{d.Marines}", null, "Marines it carries"),
                      s.Fights > 0 ? Ui.Chip("ui-skull", $"{s.Won}/{s.Fights}", s.Won * 2 >= s.Fights ? Ui.Good : Ui.Bad, "Fights won") : "")}[/font_size]\n" +
                  Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(s.Captain)} ({Ui.Esc(Fleet.ProwessName(t, s))}) · {state}[/font_size]"),
                ($"Patch {Js.Str(Fleet.RepairCost(t, s))}g", s.Out == null && s.Flight == null && carrying == null && s.Hull < d.Hull && t.Treasury >= Fleet.RepairCost(t, s), false, () => Act(() => _sim.RepairShip(id))),
                ($"{guard}", true, s.Guard != "", () => Act(() => _sim.SetShipGuard(id, next))));
            if (s.Route is int rp)
                p.Row(Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Img("ui-road", 12)} Standing run · {Ui.Esc(t.Settlements[rp].Name)}[/font_size]"),
                    ("Stop", true, false, () => Act(() => _sim.SetShipRoute(id, -1))));
            if (!Voyages.Moored(t, s)) continue;
            var marines = _seaMarines ? free.Take(d.Marines).Select(h => h.Id).ToList() : [];
            var jobs = new List<(string, bool, bool, Action)>();
            jobs.Add(($"Patrol · {Ui.Days(t.Content.Sea.Voyages.PatrolDays)}", Voyages.Refuse(t, s, "patrol", -1, "", marines) == null, false, () => Act(() => _sim.SailJob(id, "patrol", marines: marines))));
            var scout = t.Regions.Where(x => World.Sea(x) && !x.Known && Voyages.Refuse(t, s, "scout", x.Id, "", marines) == null).OrderBy(x => x.Days).ThenBy(x => x.Id).FirstOrDefault();
            if (scout != null)
            {
                int rid = scout.Id;
                jobs.Add(($"Scout {scout.Name} · {Ui.Days(Voyages.Days(t, s, scout))}", true, false, () => Act(() => _sim.SailJob(id, "scout", rid, marines: marines))));
            }
            var lair = t.Sites.Where(x => !x.Cleared && x.Known != SiteKnowledge.Unheard && x.RegionId is int rg && Voyages.Refuse(t, s, "strike", rg, x.Def.Id, marines) == null)
                .OrderBy(x => t.Regions[x.RegionId!.Value].Days).ThenBy(x => x.Def.Id, StringComparer.Ordinal).FirstOrDefault();
            if (lair != null)
            {
                int rg = lair.RegionId!.Value;
                string sid = lair.Def.Id;
                jobs.Add(($"Bombard {lair.Def.Name} · {Ui.Days(Voyages.Days(t, s, t.Regions[rg]))}", true, true, () => Act(() => _sim.SailJob(id, "strike", rg, sid, marines))));
            }
            p.Buttons([.. jobs]);
            // Its best run now: the port and cargo that clear the most a day.
            var best = ports.SelectMany(port => Goods.All.Where(g => Fleet.Refuse(t, s, port, g) == null).Select(g => (P: port, G: g, Gain: Fleet.Profit(t, s, port, g))))
                .OrderByDescending(x => x.Gain / Fleet.Days(t, s, x.P)).ThenBy(x => x.P.Id).ThenBy(x => x.G).FirstOrDefault();
            if (best.P == null) continue;
            int pid = best.P.Id;
            var cargo = best.G;
            p.Row(Ui.C(Ui.Ink2, $"[font_size=12]• {Ui.Esc(best.P.Name)} · {Ui.Days(Fleet.Days(t, s, best.P))} · {cargo.ToString().ToLowerInvariant()} · {Ui.Img("gold", 12)} ~{Js.Str(Js.Round(best.Gain))}[/font_size]"),
                ("Sail", true, best.Gain > 0, () => Act(() => _sim.SendShip(id, pid, cargo))),
                (s.Route == pid ? "Standing" : "Keep sailing", s.Route != pid && Fleet.RouteRefused(t, s, pid) == null, false, () => Act(() => _sim.SetShipRoute(id, pid))));
        }
        p.Buttons(t.Content.Sea.Ships.Select(d =>
        {
            string kind = d.Id;
            return ($"+ {d.Name} {Js.Str(Fleet.Cost(t, d))}g", Fleet.BuildBlocked(t, kind) == null, false, (Action)(() => Act(() => _sim.BuildShip(kind))));
        }).ToArray());
    }

}
