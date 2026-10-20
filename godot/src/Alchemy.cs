using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The alchemy bench, at the foot of the armory window: the herbs and monster parts to hand, a pot to put
/// two or three of them in and brew, the town's recipe book, and the potions on its shelf.
/// </summary>
public partial class Main
{
    /// <summary>Ingredients in the pot, by id, until brewed or cleared.</summary>
    private readonly List<string> _pot = [];

    private static string Props(AlchemyRules r, IEnumerable<KeyValuePair<string, int>> props) =>
        string.Join("  ", r.Properties.Select(p => (p, n: props.FirstOrDefault(x => x.Key == p.Id).Value)).Where(x => x.n > 0)
            .Select(x => Ui.C(new Color(x.p.Color), $"{x.p.Name} {x.n}")));

    private void RenderAlchemy(Pane p)
    {
        var t = _sim.Town;
        var r = t.Content.Alchemy;
        p.Heading("Alchemy bench");
        var benches = Sim.Systems.Alchemy.Benches(t);
        if (benches.Count == 0)
        {
            p.Note("Build an [b]Apothecary[/b] to brew potions from the town's herbs and the monster parts companies bring home.");
            return;
        }
        int left = Math.Max(0, Sim.Systems.Alchemy.Left(t));
        p.Note($"{string.Join(", ", benches.Select(b => b.Name).Distinct())} · {left} {(left == 1 ? "batch" : "batches")} left today. Put two or three ingredients in the pot: what they lend the brew decides what it makes.");

        // The pot ------------------------------------------------------------------------------------
        _pot.RemoveAll(id => r.Ingredient(id) == null);
        string inPot = _pot.Count == 0 ? Ui.C(Ui.Ink2, "The pot is empty.") : string.Join(" + ", _pot.Select(id => $"[b]{Ui.Esc(Ui.Cap(r.Ingredient(id)!.Name))}[/b]"));
        string makes = "";
        if (_pot.Count > 0)
        {
            var mix = Sim.Systems.Alchemy.Mix(t, _pot);
            makes = "\n" + Props(r, mix);
            if (_pot.Count >= 2)
            {
                var rec = Sim.Systems.Alchemy.Match(t, _pot);
                makes += "\n" + (rec == null ? Ui.C(Ui.Bad, "It will spoil: this mix reaches no recipe.")
                    : Sim.Systems.Alchemy.Known(t, rec.Id) is { } known ? Ui.C(Ui.Good, $"Makes {rec.Doses} doses of the [b]{Ui.Esc(known.Name)}[/b].")
                    : Ui.C(Ui.Gold, "Something the town has never brewed. It will be named in the book."));
            }
            var (herbs, cost) = Sim.Systems.Alchemy.Herbs(t, _pot);
            if (herbs > 0) makes += Ui.C(Ui.Ink2, $"\n[font_size=12]{Js.Str(herbs)} herbs from the market, {Js.Str(cost)}g[/font_size]");
        }
        string? no = Sim.Systems.Alchemy.Refuse(t, _pot);
        var pot = _pot.ToList();
        p.Row($"{inPot}{makes}{(no != null && _pot.Count > 0 ? Ui.C(Ui.Ink2, $"\n[font_size=12]Can't brew: {Ui.Esc(no)}[/font_size]") : "")}",
            ("Brew", no == null, true, () => Act(() =>
            {
                if (_sim.Brew(pot)) _pot.Clear();
            })),
            ("Empty", _pot.Count > 0, false, () => Act(() => _pot.Clear())));

        // Ingredients ----------------------------------------------------------------------------------
        var have = r.Ingredients.Select(i => (i, n: Sim.Systems.Alchemy.Stock(t, i))).Where(x => x.n > 0).ToList();
        if (have.Count == 0) p.Note("No herbs in the market and no monster parts in store. Herb gardens grow herbs; companies sell the alchemists what they cut from the lairs they clear.");
        else p.Note($"Herbs come from the market ({Js.Str(Math.Floor(t.Market.Stock(Good.Herbs)))} in stock, {Js.Str(r.Herbs)} a measure); monster parts from the lairs companies clear.");
        foreach (var (i, n) in have)
        {
            string id = i.Id;
            bool room = _pot.Count < r.Most && _pot.Count(x => x == id) < n;
            string from = i.Herb ? "herb" : $"part · {n} in store";
            p.Row($"[b]{Ui.Esc(Ui.Cap(i.Name))}[/b] {Ui.C(Ui.Ink2, $"[font_size=12]{from}[/font_size]")}  {Props(r, i.Props)}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(i.Blurb)}[/font_size]")}",
                ("Add", room, false, () => Act(() => _pot.Add(id))));
        }

        // The book and the shelf -----------------------------------------------------------------------
        p.Heading("Recipe book");
        if (t.Alchemy.Book.Count == 0) p.Note("The book is empty. The first batch of each recipe the town brews is written in it under a name of its own.");
        foreach (var e in t.Alchemy.Book)
        {
            var rec = r.Recipe(e.RecipeId);
            if (rec == null) continue;
            var ids = e.Ingredients.ToList();
            string how = string.Join(" + ", ids.Select(id => r.Ingredient(id)?.Name ?? id));
            int doses = t.Alchemy.Shelf.Get(rec.Id, 0);
            p.Row($"[b]{Ui.Esc(e.Name)}[/b]{(doses > 0 ? Ui.C(Ui.Good, $"  {doses} on the shelf") : "")}\n[font_size=12]{Ui.Esc(rec.Blurb)}[/font_size]\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Ui.Cap(how))} · day {e.Day}{(e.Brewer != "" ? $", {Ui.Esc(e.Brewer)}" : "")}[/font_size]")}",
                ("Brew again", Sim.Systems.Alchemy.Refuse(t, ids) == null, false, () => Act(() => _sim.Brew(ids))),
                ($"Sell {Js.Str(doses * rec.Value)}g", doses > 0, false, () => Act(() => _sim.SellPotions(rec.Id))));
        }
        if (t.Alchemy.Book.Count > 0)
            p.Note($"Heroes setting out take healing draughts from the shelf and the ward for where they are bound; a fever-breaker is kept for a plague-bringer's night visit. Selling takes every dose of a brew.");
    }
}
