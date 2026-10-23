using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// Kitting a company out where the player looks at it: its party as portrait tiles that open each member's
/// character sheet, and the armory's gifts, the arm-all button and a purse right beside them. The hero sheet gets
/// the same "Give" drop-down for one hero.
/// </summary>
public partial class Main
{
    /// <summary>The company's members as a row of tiles: portrait, name, class and level, hit points, what they carry. A click opens the sheet.</summary>
    private void PartyStrip(Pane p, Town t, List<Agent> members)
    {
        var tiles = members.Select(m =>
        {
            var s = m.Hero!;
            var marks = new List<string>();
            if (s.Stone) marks.Add(Ui.Img("item-stone", 13));
            if (s.Magic.Count > 0) marks.Add($"{Ui.Img("item-ring", 13)}{s.Magic.Count}");
            if (s.Duty != HeroDuty.None) marks.Add(Ui.Img(DutyIconName(m), 13));
            string state = m.IsAway ? Ui.C(Ui.Gold, s.AwayUntil is int back ? $"away · day {SimTime.DayOf(back)}" : "away") : "";
            return (m.Id, Name: m.GivenName, Line: $"{Ui.Cap(s.Cls.Name)} {s.Level}", Hp: s.MaxHp > 0 ? s.Hp / (double)s.MaxHp : 0, Marks: string.Join(" ", marks), State: state,
                Glyph: ClassGlyph(s.Cls.Id), Tint: Palette.ClassColor(s.Cls.Id), Ring: Notoriety.FameTier(t, s) >= 2 ? Ui.Gold : new Color("5a5347"), Crowned: s.Knight || s.Champion);
        }).ToList();
        p.Add($"ps|{string.Join(";", tiles.Select(x => $"{x.Id},{x.Line},{Math.Round(x.Hp * 20)},{x.Marks},{x.State},{x.Crowned}"))}", () =>
        {
            var h = new HFlowContainer();
            h.AddThemeConstantOverride("h_separation", 8);
            h.AddThemeConstantOverride("v_separation", 8);
            foreach (var x in tiles)
            {
                var card = new Button { FocusMode = Control.FocusModeEnum.None, TooltipText = $"{x.Name}: open the character sheet" };
                var look = Ui.Box(Ui.Panel2, x.Tint.Darkened(0.2f), 8, 1);
                foreach (var st in new[] { "normal", "pressed", "focus" }) card.AddThemeStyleboxOverride(st, look);
                card.AddThemeStyleboxOverride("hover", Ui.Box(Ui.Panel2.Lightened(0.12f), Ui.Gold, 8, 2));
                var v = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                v.AddThemeConstantOverride("separation", 2);
                var face = new Portrait(44) { Glyph = x.Glyph, Tint = x.Tint, Ring = x.Ring, Crowned = x.Crowned, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
                v.AddChild(face);
                var name = Ui.Label(x.Name, Ui.Ink, Ui.Bold, 12);
                name.HorizontalAlignment = HorizontalAlignment.Center;
                name.ClipText = true;
                name.MouseFilter = Control.MouseFilterEnum.Ignore;
                v.AddChild(name);
                var under = Ui.Rich($"[center]{Ui.C(Ui.Ink2, Ui.Esc(x.Line))}{(x.Marks != "" ? "  " + x.Marks : "")}{(x.State != "" ? $"\n{x.State}" : "")}[/center]", 11);
                under.MouseFilter = Control.MouseFilterEnum.Ignore;
                v.AddChild(under);
                var hp = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0, Value = x.Hp, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 5), MouseFilter = Control.MouseFilterEnum.Ignore };
                var fill = x.Hp < 0.7 ? Ui.Bad : Ui.Good;
                hp.AddThemeStyleboxOverride("background", Ui.Box(new Color("120f0b"), Ui.Line, 2));
                hp.AddThemeStyleboxOverride("fill", Ui.Box(fill, fill.Darkened(0.3f), 2));
                v.AddChild(hp);
                var pad = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                foreach (var side in new[] { "left", "right", "top", "bottom" }) pad.AddThemeConstantOverride($"margin_{side}", 6);
                pad.AddChild(v);
                card.AddChild(pad);
                pad.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                card.CustomMinimumSize = new Vector2(112, x.State != "" ? 132 : 118);
                int id = x.Id;
                card.Pressed += () =>
                {
                    OnLink($"agent:{id}");
                    GuideEvent("sheet");
                };
                h.AddChild(card);
            }
            return h;
        });
    }

    /// <summary>Every armory item someone in the group could take now, as "item → hero (+gain)", for a Give drop-down.</summary>
    private static List<(int Stock, int Hero, string Icon, string Text)> Gifts(Town t, List<Agent> heroes)
    {
        var list = new List<(int, int, string, string)>();
        foreach (var s in t.Armory.Stock.OrderBy(x => Sim.Systems.Armory.IsStone(t, x.Item) ? 1 : 0).ThenByDescending(x => x.Item.Tier).ThenBy(x => x.Id))
        {
            var who = heroes.Where(h => Sim.Systems.Armory.Refuse(t, h, s.Item) == null)
                .OrderByDescending(h => Sim.Systems.Armory.Use(t, h, s.Item)).ThenByDescending(h => h.Hero!.Level).ThenBy(h => h.Id).ToList();
            bool stone = Sim.Systems.Armory.IsStone(t, s.Item);
            string name = s.Name != "" ? s.Name : Gear.Plainly(s.Item);
            foreach (var h in heroes.Count == 1 ? who : who.Take(2))
                list.Add((s.Id, h.Id, ItemIcon(t, s.Item), heroes.Count == 1 ? $"{name}{(stone ? "" : $"  +{Sim.Systems.Armory.Use(t, h, s.Item)}")}" : $"{name} → {h.GivenName}{(stone ? "" : $"  +{Sim.Systems.Armory.Use(t, h, s.Item)}")}"));
        }
        return list;
    }

    /// <summary>A purse for each member in town: the loyalty reward's gold per level.</summary>
    private static double Purse(Town t, IEnumerable<Agent> heroes) => heroes.Sum(m => Js.Round(t.Content.Loyalty.Reward.PerLevel * m.Hero!.Level));

    /// <summary>The company's kit and coin: give a chosen armory item, arm everyone with the best, send a purse.</summary>
    private void OutfitCompany(Pane p, Town t, List<Agent> members)
    {
        var home = members.Where(m => !m.IsAway && !m.Dead).ToList();
        if (home.Count == 0)
        {
            int back = members.Select(m => m.Hero!.AwayUntil ?? 0).DefaultIfEmpty(0).Max();
            p.Note(back > 0 ? $"All away until day {SimTime.DayOf(back)}. Gear and purses wait till they're home." : "All away. Gear and purses wait till they're home.");
            return;
        }
        var ids = home.Select(m => m.Id).ToList();
        var gifts = Gifts(t, home);
        double purse = Purse(t, home);
        bool arm = home.Any(m => Sim.Systems.Armory.BestFor(t, m) != null);
        int kinds = gifts.Select(g => g.Stock).Distinct().Count();
        string away = home.Count < members.Count ? $"{members.Count - home.Count} away" : "";
        p.Add($"oc|{string.Join("/", gifts.Select(g => g.Text))}|{arm}|{purse}|{t.Treasury >= purse}|{away}", () =>
        {
            var h = new HFlowContainer();
            h.AddThemeConstantOverride("h_separation", 8);
            h.AddThemeConstantOverride("v_separation", 6);
            var give = Ui.Menu($"Give ({kinds})", "tab-armory", gifts.Select(g => (g.Icon, g.Text)).ToList(), i => Act(() =>
            {
                _sim.Gift(gifts[i].Stock, gifts[i].Hero);
                GuideEvent("outfit");
            }));
            give.TooltipText = gifts.Count == 0 ? "The armory holds nothing they'd use" : "Pick an armory item and who gets it.\nWorn at once; counts as pay and wins loyalty.";
            h.AddChild(give);
            void Add(string label, string tip, bool on, bool primary, Action press)
            {
                var btn = new Button { Text = label, Disabled = !on, ThemeTypeVariation = primary ? "Post" : "", TooltipText = tip };
                Ui.GoldButton(btn);
                btn.Pressed += press;
                h.AddChild(btn);
            }
            Add("Arm all", "The armory's best for each member in town", arm, arm, () => Act(() =>
            {
                _sim.Arm(ids);
                GuideEvent("outfit");
            }));
            Add($"Purse {Js.Str(purse)}g", "Gold for each member in town, by level: wins loyalty", t.Treasury >= purse, false, () => Act(() =>
            {
                foreach (var m in home) _sim.RewardHero(m.Id, Js.Round(t.Content.Loyalty.Reward.PerLevel * m.Hero!.Level));
                GuideEvent("outfit");
            }));
            if (away != "") h.AddChild(Ui.Rich(Ui.Chip("ui-road", away, Ui.Gold, "Members on the road get nothing until they're home"), 12));
            return h;
        });
    }

    /// <summary>The hero sheet's drop-down of armory items this hero would take.</summary>
    private void GiveMenu(Pane p, Town t, Agent a)
    {
        if (a.IsAway || a.Dead || a.Departed) return;
        var gifts = Gifts(t, [a]);
        if (gifts.Count == 0) return;
        p.Menus(($"Give ({gifts.Count})", "tab-armory", gifts.Select(g => (g.Icon, g.Text)).ToList(), i => Act(() =>
        {
            _sim.Gift(gifts[i].Stock, gifts[i].Hero);
            GuideEvent("outfit");
        })));
    }
}
