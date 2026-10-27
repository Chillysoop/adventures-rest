using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The showdown: a company before the master of a meaningful place (<see cref="Showdowns"/>). Talk first
/// (persuade, deceive, intimidate), then the fight on a grid in initiative order. Every hero starts under the
/// mayor's command; the face by each name in the order hands one back to their own judgment. Click a square to
/// move, an action on the bar then a square to aim it; right-click lets go of the action. Auto plays it out.
/// Stops the clock ("showdown" in Settings).
/// </summary>
public partial class Main
{
    private PanelContainer _duel = null!;
    private VBoxContainer _duelBox = null!;
    private DuelGrid _duelGrid = null!;
    private Expedition.Venture.Showdown? _duelFight;
    private Expedition.Venture.Showdown? _duelSeen;
    private string? _duelAim;

    private void BuildShowdown()
    {
        _duel = new PanelContainer { Visible = false, ZIndex = 21, MouseFilter = MouseFilterEnum.Stop };
        _duel.AddThemeStyleboxOverride("panel", Castle.Oak(22, 18));
        _duelBox = new VBoxContainer();
        _duelBox.AddThemeConstantOverride("separation", 8);
        _duel.AddChild(_duelBox);
        AddChild(_duel);
        _duel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.Minsize);
        _duel.GrowHorizontal = GrowDirection.Both;
        _duel.GrowVertical = GrowDirection.Both;
        _duel.MinimumSizeChanged += () => Ui.Refit(_duel);
    }

    /// <summary>A showdown waiting on the mayor opens the card once and stops the clock.</summary>
    private void SyncShowdown()
    {
        var f = _sim.Showdown;
        if (f == null || f == _duelSeen || _duel.Visible) return;
        if (_title.Visible || _menu.Visible || (_opts.Smoke && _opts.Showdown == null)) return;
        OpenShowdown(f);
        StopClock("showdown");
    }

    /// <summary>For shots: --showdown talk|fight|over stages one for the town's best heroes.</summary>
    private void StageShowdown(string phase)
    {
        if (Showdowns.Stage(_sim.Town, null, phase) is { } live) OpenShowdown(live.Fight);
    }

    private void OpenShowdown(Expedition.Venture.Showdown f)
    {
        _duelFight = f;
        _duelSeen = f;
        _duelAim = null;
        RenderShowdown();
        _duel.Visible = true;
        _duel.MoveToFront();
    }

    private void CloseShowdown()
    {
        _duel.Visible = false;
        _duelFight = null;
        _duelAim = null;
    }

    private static readonly Dictionary<string, string> MarkIcons = new()
    {
        ["held"] = "spell-hold", ["hindered"] = "cond-exhausted", ["raging"] = "act-bonus", ["dodging"] = "act-dodge",
        ["grappled"] = "spell-hold", ["swallowed"] = "cond-wounded",
    };

    private void RenderShowdown()
    {
        var f = _duelFight;
        if (f == null) return;
        Ui.Clear(_duelBox);

        // The head: whose place, and where the fight stands.
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        head.AddChild(Ui.IconRect("showdown", 26));
        head.AddChild(Ui.Label("SHOWDOWN", Ui.Gold, Ui.Bold, 11));
        head.AddChild(Ui.Label(Ui.Cap(f.Boss) + " · " + Ui.Cap(f.SiteName), Ui.Ink, Ui.Display, 20));
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        string state = f.Phase switch
        {
            "talk" => "Parley",
            "fight" => $"Round {f.Round}",
            _ => f.Won ? (f.Talked ? "Talked down" : "Victory") : "Retreat",
        };
        head.AddChild(Ui.Label(state, f.Phase == "over" ? (f.Won ? Ui.Good : Ui.Bad) : Ui.Ink2, Ui.Bold, 14));
        if (f.Phase != "over")
        {
            var auto = DuelButton("Auto", "act-auto", "Let the party play it out on its own");
            auto.Pressed += () => { _sim.ShowdownAuto(); RenderShowdown(); };
            head.AddChild(auto);
        }
        else
        {
            var done = DuelButton("Done", "ui-yes", "Back to the town");
            Ui.GoldButton(done);
            done.Pressed += CloseShowdown;
            head.AddChild(done);
        }
        _duelBox.AddChild(head);

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 14);
        _duelGrid = new DuelGrid(f, GlyphOf) { CustomMinimumSize = new Vector2(f.W * 62, f.H * 62), Aim = _duelAim };
        _duelGrid.Clicked += DuelClick;
        body.AddChild(_duelGrid);
        body.AddChild(DuelSide(f));
        _duelBox.AddChild(body);
        if (f.Phase == "fight") _duelBox.AddChild(DuelBar(f));
    }

    private static Button DuelButton(string text, string icon, string tip)
    {
        var b = new Button { Text = text, Icon = Ui.Icon(icon), TooltipText = tip, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
        b.AddThemeConstantOverride("icon_max_width", 20);
        return b;
    }

    private static Button DuelIcon(string icon, string tip, int size = 30)
    {
        var b = new Button { Icon = Ui.Icon(icon), IconAlignment = HorizontalAlignment.Center, TooltipText = tip, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand, CustomMinimumSize = new Vector2(size, size) };
        b.AddThemeConstantOverride("icon_max_width", size - 12);
        b.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        return b;
    }

    private string GlyphOf(GridUnit u) => u.Hero ? ClassGlyph(u.Kind) : Foes.CreatureFace(u.Name, "") is var face && face != "help" ? face : u.Boss ? "dread-skull" : "death-skull";

    /// <summary>The right column: the talk while it lasts, the order of turns with who is commanded, and the fight's last lines.</summary>
    private Control DuelSide(Expedition.Venture.Showdown f)
    {
        var side = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        side.AddThemeConstantOverride("separation", 8);
        if (f.Phase == "talk")
        {
            var rules = Showdowns.Rules(_sim.Town).Talk;
            string pips = string.Concat(Enumerable.Range(0, rules.Wins).Select(i => i < f.Wins ? Ui.C(Ui.Good, "●") : Ui.C(Ui.Line, "●")))
                + "   " + string.Concat(Enumerable.Range(0, rules.Losses).Select(i => i < f.Losses ? Ui.C(Ui.Bad, "✕") : Ui.C(Ui.Line, "✕")));
            side.AddChild(Ui.Rich(Ui.Tip($"{Ui.Img("act-parley", 16)} {pips}", $"{rules.Wins} wins settle it; {rules.Losses} losses and it attacks"), 16));
            foreach (var o in f.TalkOptions())
            {
                var hero = _sim.Town.Agent(o.HeroId);
                int need = Math.Clamp(o.Dc - o.Bonus, 2, 20);
                int odds = (int)Math.Round((21 - need) / 20.0 * 100);
                var b = DuelButton($"{o.Name}   {hero.GivenName} {(o.Bonus >= 0 ? "+" : "")}{o.Bonus} vs {o.Dc}", o.Icon, $"{odds}% a try · d20 + {o.Bonus} against DC {o.Dc}");
                b.Alignment = HorizontalAlignment.Left;
                string way = o.Id;
                int who = o.HeroId;
                b.Pressed += () => { _sim.ShowdownTalk(way, who); RenderShowdown(); };
                side.AddChild(b);
            }
            var attack = DuelButton("Attack", "act-attack", "Draw weapons: the party strikes first if nobody has spoken");
            attack.Pressed += () => { _sim.ShowdownTalk("attack"); RenderShowdown(); };
            side.AddChild(attack);
        }
        else
        {
            var units = f.Units().ToDictionary(u => u.Id);
            foreach (int id in f.Order())
            {
                var u = units[id];
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 6);
                bool now = u.Marks.Contains("turn");
                var tint = u.Hero ? Palette.ClassColor(u.Kind) : Ui.Bad;
                row.AddChild(new Portrait(30) { Glyph = GlyphOf(u), Tint = tint, Ring = now ? Ui.Gold : new Color("5a5347"), Crowned = u.Boss });
                string marks = string.Concat(u.Marks.Where(MarkIcons.ContainsKey).Select(m => Ui.Img(MarkIcons[m], 13)));
                var name = Ui.Rich($"{(now ? Ui.C(Ui.Gold, "▶ ") : "")}{Ui.Esc(u.Name)}  {Ui.C(u.Hp * 2 < u.MaxHp ? Ui.Bad : Ui.Ink2, $"{u.Hp}/{u.MaxHp}")} {marks}", 13);
                name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(name);
                if (u.Hero)
                {
                    bool led = f.Commanded.Contains(u.AgentId);
                    var toggle = DuelIcon(led ? "act-attack" : "act-auto", led ? "Commanded: click to let them fight on their own" : "On their own: click to take command");
                    int agent = u.AgentId;
                    toggle.Pressed += () => { _sim.ShowdownCommand(agent, !led); RenderShowdown(); };
                    row.AddChild(toggle);
                }
                side.AddChild(row);
            }
        }
        var log = Ui.Rich(string.Join("\n", f.Lines.TakeLast(f.Phase == "talk" ? 6 : 9).Select(l => Ui.C(Ui.Ink2, Ui.Esc(l)))), 12);
        log.FitContent = true;
        log.CustomMinimumSize = new Vector2(360, 0);
        side.AddChild(log);
        return side;
    }

    /// <summary>The hotbar of the commanded hero whose turn it is: their attack, cantrip, every spell they have slots for, item powers, then the plain actions.</summary>
    private Control DuelBar(Expedition.Venture.Showdown f)
    {
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 4);
        int hero = f.Waiting;
        if (hero == 0) return bar;
        var a = _sim.Town.Agent(hero);
        var turn = f.Turn;
        var who = Ui.Rich($"[b]{Ui.Esc(a.GivenName)}[/b]\n{Ui.Chip("act-dash", $"{turn.Move}", null, "Squares of movement left")}  {Ui.Chip("act-attack", turn.Acted ? "—" : "1", null, "Action")}  {Ui.Chip("act-bonus", turn.Bonus ? "—" : "1", null, "Bonus action")}", 13);
        who.CustomMinimumSize = new Vector2(150, 0);
        who.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        bar.AddChild(who);
        foreach (var act in f.Actions())
        {
            var b = DuelIcon(act.Icon, $"{Ui.Cap(act.Name)}{(act.Range > 1 ? $" · reach {act.Range}" : "")}{(act.Area > 0 ? $" · radius {act.Area}" : "")}{(act.Tip != "" ? "\n" + act.Tip : "")}", 46);
            b.Disabled = !act.Ready;
            if (_duelAim == act.Id) Ui.GoldButton(b);
            string id = act.Id;
            string aim = act.Aim;
            b.Pressed += () =>
            {
                if (aim == "self")
                {
                    _duelAim = null;
                    _sim.ShowdownAct(id);
                }
                else _duelAim = _duelAim == id ? null : id;
                RenderShowdown();
            };
            bar.AddChild(b);
        }
        return bar;
    }

    private void DuelClick(int x, int y, bool right)
    {
        var f = _duelFight;
        if (f == null || f.Phase != "fight") return;
        if (right) _duelAim = null;
        else if (_duelAim is string aim)
        {
            if (_sim.ShowdownAct(aim, x, y)) _duelAim = null;
        }
        else _sim.ShowdownMove(x, y);
        RenderShowdown();
    }
}

/// <summary>The showdown's room drawn as a battle map: stone floor, its ground, where the mover can go, what the aimed action reaches, and everyone on it.</summary>
public partial class DuelGrid : Control
{
    private static readonly Color Floor = new("3a3328");
    private static readonly Color Floor2 = new("332d23");
    private static readonly Color Grout = new("241f18");

    private readonly Expedition.Venture.Showdown _f;
    private readonly Func<GridUnit, string> _glyph;
    private Vector2I _hover = new(-1, -1);
    public string? Aim;
    public event Action<int, int, bool>? Clicked;

    public DuelGrid(Expedition.Venture.Showdown f, Func<GridUnit, string> glyph)
    {
        _f = f;
        _glyph = glyph;
        MouseFilter = MouseFilterEnum.Stop;
    }

    private float Cell => Mathf.Min(Size.X / _f.W, Size.Y / _f.H);

    private Vector2I At(Vector2 p) => new((int)(p.X / Cell), (int)(p.Y / Cell));

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion m)
        {
            var c = At(m.Position);
            if (c != _hover)
            {
                _hover = c;
                QueueRedraw();
            }
        }
        if (e is InputEventMouseButton { Pressed: true } b && b.ButtonIndex is MouseButton.Left or MouseButton.Right)
        {
            var c = At(b.Position);
            if (c.X >= 0 && c.Y >= 0 && c.X < _f.W && c.Y < _f.H) Clicked?.Invoke(c.X, c.Y, b.ButtonIndex == MouseButton.Right);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        float s = Cell;
        var units = _f.Units();
        var mover = units.FirstOrDefault(u => u.Marks.Contains("turn"));
        var act = Aim != null ? _f.Actions().FirstOrDefault(a => a.Id == Aim) : null;
        var moves = Aim == null ? _f.Moves().ToHashSet() : [];
        for (int y = 0; y < _f.H; y++)
            for (int x = 0; x < _f.W; x++)
            {
                var r = new Rect2(x * s, y * s, s, s);
                DrawRect(r, (x + y) % 2 == 0 ? Floor : Floor2);
                string g = _f.Ground[y * _f.W + x];
                switch (g)
                {
                    case "pillar":
                        DrawCircle(r.GetCenter(), s * 0.42f, new Color("1d1712"));
                        DrawCircle(r.GetCenter() + new Vector2(-s * 0.06f, -s * 0.06f), s * 0.34f, new Color("6b6152"));
                        break;
                    case "wall":
                        DrawRect(r, new Color("1d1712"));
                        DrawRect(r.Grow(-s * 0.1f), new Color("4a4236"));
                        break;
                    case "chasm":
                        DrawRect(r, new Color("0b0907"));
                        DrawRect(r, new Color("5a5040"), false, 1.5f);
                        break;
                    case "water":
                        DrawRect(r, new Color(0.18f, 0.32f, 0.45f, 0.85f));
                        DrawLine(r.Position + new Vector2(s * 0.2f, s * 0.45f), r.Position + new Vector2(s * 0.8f, s * 0.45f), new Color("8fb8d4"), 1.5f);
                        break;
                    case "rubble":
                        foreach (var (dx, dy, rr) in new[] { (0.3f, 0.35f, 0.12f), (0.65f, 0.3f, 0.09f), (0.5f, 0.7f, 0.14f) })
                            DrawCircle(r.Position + new Vector2(s * dx, s * dy), s * rr, new Color("6e604c"));
                        break;
                    case "web":
                        DrawLine(r.Position, r.End, new Color(1, 1, 1, 0.45f), 1);
                        DrawLine(new Vector2(r.End.X, r.Position.Y), new Vector2(r.Position.X, r.End.Y), new Color(1, 1, 1, 0.45f), 1);
                        DrawArc(r.GetCenter(), s * 0.25f, 0, Mathf.Tau, 12, new Color(1, 1, 1, 0.4f), 1);
                        break;
                }
                if (moves.Contains((x, y))) DrawRect(r.Grow(-2), new Color(Ui.Gold, 0.18f));
                if (act != null && mover != null && act.Range > 0 && Math.Max(Math.Abs(x - mover.X), Math.Abs(y - mover.Y)) <= act.Range)
                    DrawRect(r.Grow(-1), new Color(Ui.Bad, 0.07f));
                if (act is { Area: > 0 } && _hover.X >= 0 && Math.Max(Math.Abs(x - _hover.X), Math.Abs(y - _hover.Y)) <= act.Area)
                    DrawRect(r.Grow(-1), new Color(Ui.Bad, 0.22f));
                DrawRect(r, Grout, false, 1);
            }
        if (_hover.X >= 0 && _hover.X < _f.W && _hover.Y < _f.H) DrawRect(new Rect2(_hover.X * s, _hover.Y * s, s, s).Grow(-1), new Color(Ui.Ink, 0.5f), false, 2);
        foreach (var u in units.OrderBy(u => u.Down ? 0 : 1))
        {
            var c = new Vector2((u.X + 0.5f) * s, (u.Y + 0.5f) * s);
            float r = s * (u.Boss ? 0.46f : 0.38f);
            var tint = u.Hero ? Palette.ClassColor(u.Kind) : new Color("b5523b");
            float fade = u.Down ? 0.35f : 1;
            if (u.Marks.Contains("turn")) DrawArc(c, r + 4, 0, Mathf.Tau, 32, Ui.Gold, 3);
            DrawCircle(c, r, new Color("1d1712", fade));
            DrawCircle(c, r * 0.9f, new Color(u.Hero ? new Color("7fb0d4") : Ui.Bad, fade));
            DrawCircle(c, r * 0.78f, new Color(tint.Darkened(0.6f), fade));
            var face = Ui.Icon("face-" + _glyph(u));
            float g = r * 1.2f;
            DrawTextureRect(face, new Rect2(c - new Vector2(g / 2, g / 2), new Vector2(g, g)), false, new Color(tint.Lerp(Ui.Ink, 0.5f), fade));
            if (u.Boss) DrawTextureRect(Ui.Icon("face-crown"), new Rect2(c + new Vector2(-r * 0.4f, -r * 1.25f), new Vector2(r * 0.8f, r * 0.5f)), false, Ui.Gold);
            if (u.Down)
            {
                DrawLine(c - new Vector2(r * 0.6f, r * 0.6f), c + new Vector2(r * 0.6f, r * 0.6f), Ui.Bad, 3);
                DrawLine(c + new Vector2(-r * 0.6f, r * 0.6f), c + new Vector2(r * 0.6f, -r * 0.6f), Ui.Bad, 3);
                continue;
            }
            // Hit points under the token.
            float w = s * 0.8f, frac = u.MaxHp > 0 ? Math.Clamp(u.Hp / (float)u.MaxHp, 0, 1) : 0;
            var bar = new Rect2(c.X - w / 2, (u.Y + 1) * s - 7, w, 5);
            DrawRect(bar, new Color("120f0b"));
            DrawRect(new Rect2(bar.Position, new Vector2(w * frac, 5)), frac < 0.5f ? Ui.Bad : Ui.Good);
            int k = 0;
            foreach (var m in u.Marks.Where(m => m != "turn"))
            {
                DrawCircle(new Vector2(u.X * s + 7 + k * 10, u.Y * s + 7), 4, m is "held" or "grappled" or "swallowed" ? new Color("b28ad8") : Ui.Gold);
                k++;
            }
        }
    }

    public override void _Process(double delta) => QueueRedraw();
}
