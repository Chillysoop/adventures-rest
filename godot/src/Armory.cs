using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The armory window, laid out to be read at a glance: a strip of counts, slot filters, the stock as a grid of item
/// cards (icon by kind, border by rarity, who it suits), the picked item's card with its handout buttons, then what
/// heroes and caravans offer, the sending stones out, and loot deals with the companies.
/// </summary>
public partial class Main
{
    private string _armorySlot = "";
    private int? _armoryPick;

    private static string Stars(LibItem it) => Ui.C(Ui.Gold, $"[font_size=11]{new string('◆', Math.Clamp(it.Tier, 1, 5))}[/font_size]");

    /// <summary>Rarity colours by tier, after the tabletop's: uncommon, rare, very rare, legendary, artifact.</summary>
    private static readonly Color[] Rarity = [new("9a9a8a"), new("6fbf5f"), new("5b8fe0"), new("a86ae0"), new("e09a3a"), new("e0503a")];

    private static Color RarityOf(LibItem it) => Rarity[Math.Clamp(it.Tier, 0, Rarity.Length - 1)];

    /// <summary>The icon for an item's kind (assets/icons/item-*.svg).</summary>
    private static string ItemIcon(Town t, LibItem it) =>
        Sim.Systems.Armory.IsStone(t, it) ? "item-stone"
        : Gear.SlotOf(it) switch
        {
            "weapon" => "item-weapon",
            "armor" => "item-armor",
            "shield" => "item-shield",
            _ => it.Category switch { "ring" => "item-ring", "wand" or "staff" or "rod" => "item-wand", _ => "item-trinket" },
        };

    /// <summary>The filter an item falls under: weapon, armor, shield, trinket or stone.</summary>
    private static string FilterOf(Town t, LibItem it) => Sim.Systems.Armory.IsStone(t, it) ? "stone" : Gear.SlotOf(it);

    private static readonly (string Key, string Icon, string Name)[] ArmoryFilters =
        [("", "tab-armory", "All"), ("weapon", "item-weapon", "Weapons"), ("armor", "item-armor", "Armour"), ("shield", "item-shield", "Shields"), ("trinket", "item-ring", "Charms"), ("stone", "item-stone", "Stones")];

    private void RenderArmory()
    {
        var p = _panes[Tab.Armory];
        var t = _sim.Town;
        var r = t.Content.Armory;
        p.Begin();
        var stock = t.Armory.Stock.OrderBy(s => Sim.Systems.Armory.IsStone(t, s.Item) ? 1 : 0).ThenByDescending(s => s.Item.Tier).ThenBy(s => s.Id).ToList();
        var carriers = t.Heroes.Where(h => h.Hero!.Stone).OrderBy(h => h.Hero!.PartyName ?? "", StringComparer.Ordinal).ThenBy(h => h.Id).ToList();
        var offers = t.Armory.Offers.OrderBy(o => o.Until).ThenBy(o => o.Id).ToList();
        var guard = Garrison.Defenders(t);

        // At a glance: what's held, offered and out, and the one-click handout to the guard.
        p.Card($"{Ui.Img("tab-armory", 16)} [b]{stock.Count}[/b] held    {Ui.Img("gold", 16)} [b]{offers.Count}[/b] offered    {Ui.Img("item-stone", 16)} [b]{carriers.Count}[/b] stones out    " +
               $"{Ui.Img("order-defend", 16)} [b]{guard.Count}[/b] on guard\n{Ui.C(Ui.Ink2, "[font_size=12]Gifts are worn at once, count as pay and win loyalty.[/font_size]")}");
        if (guard.Count > 0)
        {
            var ids = guard.Select(h => h.Id).ToList();
            p.Buttons(($"Arm the guard ({guard.Count})", guard.Any(h => Sim.Systems.Armory.BestFor(t, h) != null), true, () => Act(() => _sim.Arm(ids))));
        }

        p.Page("Items", "tab-armory");
        p.Heading("In the armory");
        var counts = ArmoryFilters.Select(f => f.Key == "" ? stock.Count : stock.Count(s => FilterOf(t, s.Item) == f.Key)).ToList();
        p.Add($"af{_armorySlot}|{string.Join(",", counts)}", () => FilterBar(counts));
        var shown = stock.Where(s => _armorySlot == "" || FilterOf(t, s.Item) == _armorySlot).ToList();
        if (stock.Count == 0) p.Note("Empty. Fills from loot terms, loot pacts and the offers below.");
        else if (shown.Count == 0) p.Note("None of this kind.");
        if (_armoryPick is int pk && shown.All(s => s.Id != pk)) _armoryPick = null;
        _armoryPick ??= shown.FirstOrDefault()?.Id;
        var cards = shown.Select(s =>
        {
            var best = Sim.Systems.Armory.Takers(t, s.Item).FirstOrDefault();
            string who = best.Hero == null ? "—" : Sim.Systems.Armory.IsStone(t, s.Item) ? best.Hero.GivenName : $"{best.Hero.GivenName} +{best.Use}";
            return (s.Id, Icon: ItemIcon(t, s.Item), Name: s.Name != "" ? s.Name : Gear.Plainly(s.Item), Color: RarityOf(s.Item), Who: who, Duty: best.Hero != null ? DutyIconName(best.Hero) : "");
        }).ToList();
        if (cards.Count > 0)
            p.Add($"ag{_armoryPick}|{string.Join(";", cards.Select(c => $"{c.Id},{c.Name},{c.Who},{c.Duty}"))}", () => ItemGrid(cards));
        if (shown.FirstOrDefault(s => s.Id == _armoryPick) is { } pick)
        {
            PickedItem(p, t, pick);
            PickedPowers(p, t, pick);
        }

        p.Heading("Offered to the town");
        foreach (var o in offers)
        {
            int id = o.Id;
            var taker = Sim.Systems.Armory.Takers(t, o.Item).FirstOrDefault(x => x.Hero.Id != o.HeroId);
            string seller = o.HeroId is int h ? AgentLink(t.Agent(h)) : Ui.Esc(Ui.Cap(o.Seller));
            string use = taker.Hero != null ? Ui.C(Ui.Good, $"{Ui.Esc(taker.Hero.GivenName)} +{taker.Use}") : Ui.C(Ui.Ink2, "no taker");
            int days = Math.Max(1, o.Until - t.Day);
            p.Row($"[b]{Ui.Esc(Gear.Plainly(o.Item))}[/b] {Stars(o.Item)}\n[font_size=12]{seller} · {use} · {Ui.C(Ui.Ink2, $"{Ui.Days(days)} left")}[/font_size]",
                ($"ai{o.Id}{ItemIcon(t, o.Item)}", () => ItemBadge(ItemIcon(t, o.Item), RarityOf(o.Item)), null),
                ($"Buy {Js.Str(o.Price)}g", t.Treasury >= o.Price, taker.Hero != null, () => Act(() => _sim.BuyLoot(id))));
        }

        p.Heading("Sending stones");
        if (carriers.Count == 0) p.Note($"None out. A stone lets a company send word home {r.StoneSlots} times a trip with no caster.");
        else
            p.Text(string.Join("\n", carriers.Select(h => $"{Ui.Img("item-stone", 14)} {AgentLink(h)}{(h.Hero!.PartyName is string n ? Ui.C(Ui.Ink2, $" · {Ui.Esc(n)}") : "")}{(h.IsAway ? Ui.C(Ui.Gold, " · on the road") : "")}")), 13);

        p.Heading("Loot deals");
        p.Note($"• [b]Loot terms[/b] on a bounty: about {Js.Str(Math.Round((r.Terms.Markup - 1) * 100))}% dearer, the hoard's magic comes here.\n• [b]Loot pact[/b]: every find comes here for a fee. Needs relation {Js.Str(r.Pact.MinRelation)}.");
        var names = Intel.MembersByCompany(t);
        foreach (var (name, members) in names)
        {
            var c = Intel.Find(t, name);
            double rel = Intel.Relation(t, name, members);
            var (word, color) = RelationWord(rel, t.Content.World.Intel.RefuseBelow);
            var home = members.Where(m => !m.IsAway).ToList();
            double purse = Js.Round(t.Content.Loyalty.Reward.PerLevel * home.Sum(m => m.Hero!.Level));
            string pact = c is { Pact: true } ? Tag($"Pact {Js.Str(Math.Round(c.PactFee * 100))}%{(c.Handed > 0 ? $" · {c.Handed} in" : "")}", Ui.Good) : "";
            string? no = Sim.Systems.Armory.PactRefusal(t, name);
            var buttons = new List<(string, bool, bool, Action)>();
            string n2 = name;
            if (c is { Pact: true }) buttons.Add(("End pact", true, false, () => Act(() => _sim.EndPact(n2))));
            else buttons.Add(($"Pact {Js.Str(Math.Round(Sim.Systems.Armory.PactFee(t, name) * 100))}%", c?.SulkUntil is not int u || t.Tick >= u, false, () => Act(() => _sim.OfferPact(n2))));
            if (home.Count > 0)
            {
                var ids = home.Select(m => m.Id).ToList();
                buttons.Add(("Arm", home.Any(m => Sim.Systems.Armory.BestFor(t, m) != null), false, () => Act(() => _sim.Arm(ids))));
                buttons.Add(($"Purse {Js.Str(purse)}g", t.Treasury >= purse, false, () => Act(() =>
                {
                    foreach (int hid in ids) _sim.RewardHero(hid, Js.Round(t.Content.Loyalty.Reward.PerLevel * t.Agent(hid).Hero!.Level));
                })));
            }
            string line = no != null && c is not { Pact: true } ? Ui.C(Ui.Ink2, $" · no pact: {Ui.Esc(no)}") : "";
            p.Row($"[b]The {Ui.Esc(name)}[/b]  {Tag(word, color)}  {pact}\n{Ui.C(Ui.Ink2, $"[font_size=12]{home.Count}/{members.Count} in town{line}[/font_size]")}", buttons.ToArray());
        }
        p.Page("Forge", "forge");
        RenderKit(p, t);
        RenderForge(p, t);
        RenderParts(p, t);
        p.Page("Hall", "ui-renown");
        RenderRelics(p, t);
        p.Page("Alchemy", "potions");
        RenderAlchemy(p);
        p.Commit();
    }

    /// <summary>The picked item's card: what it is, where it came from, who it suits, and the handout.</summary>
    private void PickedItem(Pane p, Town t, StockItem s)
    {
        int id = s.Id;
        var takers = Sim.Systems.Armory.Takers(t, s.Item);
        // The guard first, then the rest, each with what it would add.
        var all = takers.OrderBy(x => x.Hero.Hero!.Duty == HeroDuty.None ? 1 : 0).ToList();
        var items = all.Select(x => (DutyIconName(x.Hero), $"{x.Hero.GivenName} · {Ui.Cap(x.Hero.Hero!.Cls.Name)} {x.Hero.Hero.Level}{(Sim.Systems.Armory.IsStone(t, s.Item) ? "" : $" · +{x.Use}")}")).ToList();
        var hids = all.Select(x => x.Hero.Id).ToList();
        string suits = takers.Count == 0 ? Ui.C(Ui.Ink2, "No hero in town has a use for it now.")
            : string.Join("\n", takers.Take(3).Select(x => $"{DutyIcon(x.Hero)}{AgentLink(x.Hero)} {Ui.C(Ui.Ink2, $"{Ui.Esc(Ui.Cap(x.Hero.Hero!.Cls.Name))} {x.Hero.Hero.Level}")}{(Sim.Systems.Armory.IsStone(t, s.Item) ? "" : " " + Ui.C(Ui.Good, $"+{x.Use}"))}"));
        var color = RarityOf(s.Item);
        double sell = Sim.Systems.Armory.SellValue(t, s.Item);
        var buttons = new List<(string, bool, bool, Action)>();
        if (takers.Count > 0)
        {
            int best = takers[0].Hero.Id;
            buttons.Add(($"Give {takers[0].Hero.GivenName}", true, true, () => Act(() => _sim.Gift(id, best))));
        }
        buttons.Add(($"Sell {Js.Str(sell)}g", sell > 0, false, () => Act(() => _sim.SellStock(id))));
        p.Row($"[b]{Ui.Esc(s.Title)}[/b] {Stars(s.Item)}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Ui.Cap(s.From))} · day {s.Day}[/font_size]")}\n[font_size=13]{suits}[/font_size]",
            ($"ap{id}|{string.Join(",", items.Select(i => i.Item2))}", () => ItemBadge(ItemIcon(t, s.Item), color, 56), takers.Count > 1 ? () => Ui.Menu("Give to", "tab-armory", items, i => Act(() => _sim.Gift(id, hids[i]))) : null),
            buttons.ToArray());
        if (takers.Count > 1) p.Menus(("Give to", "tab-armory", items, i => Act(() => _sim.Gift(id, hids[i]))));
        else if (takers.Count == 0 && t.Heroes.Any(h => h.IsAway && Sim.Systems.Armory.Use(t, h, s.Item) > 0))
            p.Note("Whoever could use it is on the road. Give it when they're home.");
    }

    /// <summary>The slot filters: an icon toggle for each kind, with how many the armory holds.</summary>
    private Control FilterBar(List<int> counts)
    {
        var h = new HFlowContainer();
        h.AddThemeConstantOverride("h_separation", 6);
        h.AddThemeConstantOverride("v_separation", 6);
        for (int i = 0; i < ArmoryFilters.Length; i++)
        {
            var f = ArmoryFilters[i];
            if (i > 0 && counts[i] == 0) continue;
            var b = new Button { Text = $" {counts[i]}", Icon = Ui.Icon(f.Icon), ExpandIcon = false, TooltipText = f.Name, ToggleMode = true, ButtonPressed = _armorySlot == f.Key, FocusMode = Control.FocusModeEnum.None };
            b.AddThemeConstantOverride("icon_max_width", 18);
            string key = f.Key;
            b.Pressed += () => Act(() => _armorySlot = key);
            h.AddChild(b);
        }
        return h;
    }

    /// <summary>The stock as a grid of cards: the kind's icon in its rarity's colour, the name, and who it suits best.</summary>
    private Control ItemGrid(List<(int Id, string Icon, string Name, Color Color, string Who, string Duty)> cards)
    {
        var grid = new HFlowContainer();
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        foreach (var c in cards)
        {
            bool picked = c.Id == _armoryPick;
            var card = new Button { CustomMinimumSize = new Vector2(118, 112), FocusMode = Control.FocusModeEnum.None, TooltipText = c.Name, ClipContents = true };
            var look = Ui.Box(picked ? Ui.Panel2.Lightened(0.08f) : Ui.Panel2, picked ? Ui.Gold : c.Color, 8, picked ? 2 : 1);
            foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) card.AddThemeStyleboxOverride(state, state == "hover" ? Ui.Box(Ui.Panel2.Lightened(0.12f), c.Color, 8, 2) : look);
            var v = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            v.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, 6);
            v.AddThemeConstantOverride("separation", 2);
            var icon = new TextureRect { Texture = Ui.Icon(c.Icon), Modulate = c.Color, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(0, 40), MouseFilter = Control.MouseFilterEnum.Ignore };
            v.AddChild(icon);
            var name = Ui.Label(c.Name, Ui.Ink, null, 12);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            name.MaxLinesVisible = 2;
            name.MouseFilter = Control.MouseFilterEnum.Ignore;
            v.AddChild(name);
            var who = Ui.Rich($"[center]{(c.Duty != "" ? Ui.Img(c.Duty, 12) + " " : "")}{Ui.C(c.Who == "—" ? Ui.Ink2 : Ui.Good, Ui.Esc(c.Who))}[/center]", 11);
            who.MouseFilter = Control.MouseFilterEnum.Ignore;
            v.AddChild(who);
            card.AddChild(v);
            int id = c.Id;
            card.Pressed += () => Act(() => _armoryPick = id);
            grid.AddChild(card);
        }
        return grid;
    }

    /// <summary>An item's kind icon on a rarity-bordered tile, for rows.</summary>
    private static Control ItemBadge(string icon, Color color, int size = 40)
    {
        var tile = new PanelContainer { CustomMinimumSize = new Vector2(size, size), SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        tile.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel2, color, 6, 1, 4, 4));
        tile.AddChild(new TextureRect { Texture = Ui.Icon(icon), Modulate = color, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        return tile;
    }

    /// <summary>The icon of a hero's guard post, for menus: walls, streets, or none.</summary>
    private static string DutyIconName(Agent a) => a.Hero?.Duty switch
    {
        HeroDuty.Walls => "order-defend",
        HeroDuty.Streets => "order-guard",
        HeroDuty.Command => "army-command",
        _ => "",
    };

    /// <summary>The guard post's icon inline in rich text, or nothing.</summary>
    private static string DutyIcon(Agent a, int size = 14) => DutyIconName(a) is var n && n != "" ? Ui.Img(n, size) + " " : "";

    private static bool CanSend(Town t, Agent a)
    {
        var mates = a.Hero!.PartyName is string n ? Intel.Members(t, n) : [a];
        return mates.Any(m => m.Hero!.Stone || Knowledge.Sends(t, m));
    }
}
