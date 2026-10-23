using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Client;

/// <summary>
/// The armory's lower half: plain arms issued to the watch and the regiments, the forge's recipes and orders, and the
/// parts companies bring home (sold on or made into arms).
/// </summary>
public partial class Main
{
    private bool _forgeAll;

    /// <summary>The watch and each regiment: plain arms carried, what they add, and the one-click Arm and Recall.</summary>
    private void RenderKit(Pane p, Town t)
    {
        var units = new List<string> { Kit.Watch };
        units.AddRange(t.Army.Regiments.OrderBy(r => r.Id).Select(r => Kit.Regiment(r.Id)));
        units = units.Where(u => Kit.Men(t, u) > 0 || Kit.Of(t, u).Any()).ToList();
        if (units.Count == 0) return;
        int spare = t.Armory.Stock.Count(s => Kit.Fits(t, s.Item));
        p.Heading("Watch and soldiers");
        if (spare == 0 && !t.Armory.Issued.Any()) p.Note("Plain +1/+2/+3 arms from the forge or the wilds arm the watch and the regiments.");
        foreach (var u in units)
        {
            string unit = u;
            var held = Kit.Of(t, u).ToList();
            int men = Kit.Men(t, u);
            double pct = (Kit.Factor(t, u) - 1) * 100;
            string icon = u == Kit.Watch ? "order-guard" : "tab-army";
            string line = Ui.Chips(
                Ui.Chip(icon, Ui.Esc(Ui.Cap(Kit.Name(t, u))), null, $"{men} men"),
                Ui.Chip("item-weapon", $"{held.Count}/{men * t.Content.Enchant.Arms.PerMan}", null, "Plain pieces carried, of what its men can carry"),
                Ui.Chip("ui-up", $"+{Js.Str(Math.Round(pct))}%", pct > 0 ? Ui.Good : Ui.Ink2, u == Kit.Watch ? "Added to the watch's strength" : "Added to the regiment's strength"));
            p.Row(line,
                ("Arm", spare > 0 && t.Armory.Stock.Any(s => Kit.Refuse(t, s, u) == null), spare > 0, () => Act(() => _sim.ArmUnit(unit))),
                ("Recall", held.Count > 0, false, () => Act(() => { foreach (var s in Kit.Of(t, unit).ToList()) _sim.RecallArms(s.Id); })));
        }
    }

    /// <summary>"4 wolf fangs", "2 fine parts".</summary>
    private static string NeedText(Town t, string need, int n)
    {
        if (need.StartsWith("grade:", StringComparison.Ordinal))
            return $"{n} {(need == "grade:3" ? "rare" : "fine")} part{(n == 1 ? "" : "s")}";
        var tr = t.Content.Spoils.Trophy(need);
        return tr == null ? $"{n} {need}" : Spoils.Count(tr, n);
    }

    /// <summary>What the town's craftsmen can make now, and what's on their books.</summary>
    private void RenderForge(Pane p, Town t)
    {
        var r = t.Content.Spoils;
        // Recipes a shop in town could take, ready ones first.
        var open = r.Recipes.Where(x => Spoils.Shops(t, x).Count > 0 && t.City.Tier >= x.Tier && t.Content.Library.Item(x.Ref) != null)
            .Select(x => (Rec: x, No: Spoils.Refuse(t, x.Id)))
            .OrderBy(x => x.No == null ? 0 : x.No == "not enough gold" ? 1 : 2).ThenBy(x => x.Rec.Gold).ThenBy(x => x.Rec.Id, StringComparer.Ordinal).ToList();
        if (open.Count == 0 && t.Spoils.Orders.Count == 0) return;
        p.Heading("Forge");
        var shown = _forgeAll ? open : open.Where(x => x.No is null or "not enough gold").Take(8).ToList();
        if (shown.Count == 0) p.Note("Nothing the parts on hand can make yet.");
        foreach (var (rec, no) in shown)
        {
            var it = t.Content.Library.Item(rec.Ref)!;
            string id = rec.Id;
            string needs = string.Join(", ", rec.Needs.Select(n => NeedText(t, n.Key, n.Value)));
            double cost = Spoils.Cost(t, rec);
            string sub = Ui.Chips(Ui.Chip("gold", Js.Str(cost), t.Treasury >= cost ? null : Ui.Bad), needs != "" ? Ui.Chip("item-part", Ui.Esc(needs), no == "not enough parts" ? Ui.Bad : null) : "", Ui.Chip("ui-time", Ui.Days(rec.Days)));
            p.Row($"[b]{Powered(t.Content, it, Ui.Esc(Gear.Plainly(it)))}[/b] {Stars(it)}\n[font_size=12]{sub}{(no != null ? Ui.Dim($" · {Ui.Esc(no)}") : "")}[/font_size]",
                ($"fr{rec.Id}", () => ItemBadge(ItemIcon(t, it), RarityOf(it)), null),
                ("Make", no == null, no == null, () => Act(() => _sim.Craft(id))));
        }
        if (open.Count > shown.Count || _forgeAll)
            p.Buttons((_forgeAll ? "Fewer" : $"All {open.Count}", true, false, () => Act(() => _forgeAll = !_forgeAll)));
        foreach (var o in t.Spoils.Orders.OrderBy(o => o.DueTick).ThenBy(o => o.Id))
        {
            if (r.Recipe(o.RecipeId) is not { } rec || t.Content.Library.Item(rec.Ref) is not LibItem it) continue;
            int oid = o.Id;
            int days = Math.Max(1, (int)Math.Ceiling((o.DueTick - t.Tick) / (double)SimTime.HoursPerDay));
            p.Row($"{Ui.Img("forge", 14)} {Ui.Esc(Gear.Plainly(it))} {Ui.Dim($"· {Ui.Esc(t.Building(o.BuildingId)?.Name ?? "forge")} · {Ui.Days(days)} left")}",
                ("Cancel", true, false, () => Act(() => _sim.CancelCraft(oid))));
        }
    }

    /// <summary>Parts the town holds: what each is, what it's worth, and Sell.</summary>
    private void RenderParts(Pane p, Town t)
    {
        var r = t.Content.Spoils;
        var held = t.Spoils.Trophies.Where(x => x.Value > 0 && r.Trophy(x.Key) != null).Select(x => (Def: r.Trophy(x.Key)!, N: x.Value))
            .OrderByDescending(x => x.Def.Grade).ThenByDescending(x => x.Def.Value).ThenBy(x => x.Def.Id, StringComparer.Ordinal).ToList();
        if (held.Count == 0) return;
        p.Heading("Parts");
        foreach (var (d, n) in held)
        {
            string id = d.Id;
            int count = n;
            double each = Math.Floor(d.Value * r.SellShare);
            var uses = r.Recipes.Where(x => x.Needs.ContainsKey(d.Id) || x.Needs.Keys.Any(k => k.StartsWith("grade:", StringComparison.Ordinal) && int.TryParse(k[6..], out int g) && d.Grade >= g))
                .Select(x => t.Content.Library.Item(x.Ref)?.Name).Where(x => x != null).Distinct().Take(4).ToList();
            string tip = (d.Blurb != "" ? d.Blurb + "\n" : "") + (uses.Count > 0 ? $"Makes: {string.Join(", ", uses)}" : "Only for selling");
            string grade = d.Grade >= 3 ? Ui.C(Ui.Gold, " ◆◆◆") : d.Grade == 2 ? Ui.C(Ui.Gold, " ◆◆") : "";
            p.Row(Ui.Tip($"{Ui.Img("item-part", 14)} [b]{count}[/b] {Ui.Esc(d.Name)}{grade} {Ui.Dim($"· {Js.Str(each)}g each")}", tip),
                ("Sell all", true, false, () => Act(() => _sim.SellParts(id, count))));
        }
    }

    /// <summary>The picked item's powers, and the handout to the watch or a regiment for plain arms.</summary>
    private void PickedPowers(Pane p, Town t, StockItem s)
    {
        if (PowerTip(t.Content, s.Item) is var tip && tip != "") p.Text(Ui.Bullets(tip.Split('\n').Select(l => Ui.Esc(l.TrimStart('•', ' ')))), 13);
        if (!Kit.Fits(t, s.Item)) return;
        int id = s.Id;
        var units = new List<string> { Kit.Watch };
        units.AddRange(t.Army.Regiments.OrderBy(r => r.Id).Select(r => Kit.Regiment(r.Id)));
        var buttons = units.Where(u => Kit.Men(t, u) > 0).Take(4)
            .Select(u => (Label: $"To {Kit.Name(t, u).Replace("the ", "")}", Enabled: Kit.Refuse(t, s, u) == null, Primary: false, Press: (Action)(() => Act(() => _sim.IssueArms(id, u))))).ToArray();
        if (buttons.Length > 0) p.Buttons(buttons);
    }

    /// <summary>Relics the hall keeps and what each does for the town.</summary>
    private void RenderRelics(Pane p, Town t)
    {
        var kept = Regalia.InHall(t).ToList();
        if (kept.Count == 0) return;
        p.Heading("In the hall");
        foreach (var d in kept)
        {
            if (t.Content.Library.Item(d.Ref) is not LibItem it) continue;
            var does = (d.Service == null ? [] : Institutions.Describe(t, d.Service)).Concat(Regalia.Describe(t, d)).ToList();
            p.Row(Ui.Tip($"{Ui.Img("inst-hall-of-heroes", 14)} [b]{Ui.Esc(it.Name)}[/b] {Ui.Dim(Ui.Esc(string.Join(" · ", does)))}", it.Text));
        }
    }
}
