using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The top bar's resources, each with where it is heading over the last month (30 days, a season): the treasury's
/// net, days of bread left, the households, Mandate and unrest. A resource heading for trouble gets a red outline.
/// Hovering one shows its month in full (the books by account, a line of the month, when it runs out at this rate
/// and the one thing to do about it); clicking opens the window that fixes it. The numbers are <see cref="Trends"/>'.
/// </summary>
public partial class Main
{
    private sealed record Stat(string Key, string Icon, string Value, string Trend, Color TrendColor, bool Trouble, Tab Window);

    private readonly List<(PanelContainer Card, Label K, Label V, Label Trend)> _statCards = [];
    private readonly List<Stat> _statsNow = [];
    private PanelContainer _statTip = null!;
    private RichTextLabel _statTipText = null!;
    private Sparkline _statSpark = null!;
    private int _statHover = -1;

    private static string PerMonth(double n) => $"{(n >= 0 ? "+" : "−")}{Math.Abs(Math.Round(n)):N0}/mo";

    private List<Stat> Stats(Town t)
    {
        var r = Trends.Rules(t);
        int away = t.Heroes.Count(a => a.IsAway);
        double net = Trends.Net(t);
        double pop = Trends.Change(t, d => d.Households);
        double mandate = Trends.Change(t, d => d.Mandate);
        double unrest = Trends.Change(t, d => d.Unrest);
        int homeless = t.Workers.Count(a => a.HomeId == null);
        int plots = Planning.VacantZoned(t, Zone.Residential);
        bool importing = Trends.FoodTrouble(t);
        string food = Trends.FoodDays(t) is double fd ? $"{fd:N0} {(fd == 1 ? "day" : "days")}" : "—";
        double stock = Trends.Change(t, d => d.Food);
        bool young = Trends.Span(t) < 2;
        string Steady(double change, double eps, string up, string down, string flat) => young ? "" : change > eps ? up : change < -eps ? down : flat;
        return
        [
            new("Households", "population", $"{t.Population:N0}", young ? "" : PerMonth(pop), pop > 0.5 ? Ui.Good : pop < -0.5 ? Ui.Bad : Ui.Ink2, false, Tab.City),
            new("Heroes", "heroes", t.Heroes.Count.ToString(), away > 0 ? $"{away} out" : "", Ui.Ink2, false, Tab.Bounties),
            new("Treasury", Ui.Money(Math.Floor(t.Treasury)).Icon, Ui.Money(Math.Floor(t.Treasury)).Figure, young ? "" : PerMonth(net), net >= 0 ? Ui.Good : Ui.Bad, Trends.MoneyTrouble(t), Tab.Council),
            new("Food", "bread", food, importing ? "importing" : Steady(stock, 3, "▲ stocking", "▼ drawing", "steady"), importing || stock < -3 ? Ui.Bad : stock > 3 ? Ui.Good : Ui.Ink2, Trends.FoodTrouble(t), Tab.City),
            new("Mandate", "mandate", $"{Math.Round(t.Mandate)}", Steady(mandate, 1, PerMonth(mandate), PerMonth(mandate), "steady"), mandate > 1 ? Ui.Good : mandate < -1 ? Ui.Bad : Ui.Ink2, Trends.MandateTrouble(t), Tab.Council),
            new("Unrest", "suspicion", $"{Math.Round(t.Taxes.Unrest)}", t.Taxes.Unrest < 1 && unrest <= 0 ? "calm" : Steady(unrest, 1, "rising", "easing", "steady"), unrest > 1 ? Ui.Bad : unrest < -1 ? Ui.Good : Ui.Ink2, Trends.UnrestTrouble(t), Tab.Council),
            homeless > 0
                ? new("Homes", "homeless", $"{homeless}", "homeless", Ui.Bad, true, Tab.City)
                : new("Homes", "homeless", $"{plots}", "free", plots == 0 ? Ui.Gold : Ui.Ink2, false, Tab.City),
        ];
    }

    private void RenderStats(Town t)
    {
        var stats = Stats(t);
        _statsNow.Clear();
        _statsNow.AddRange(stats);
        if (_statCards.Count == 0)
        {
            BuildStatTip();
            for (int i = 0; i < stats.Count; i++)
            {
                int slot = i;
                var card = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand };
                var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
                h.AddThemeConstantOverride("separation", 5);
                card.AddChild(h);
                h.AddChild(new TextureRect { Texture = Ui.Icon(stats[i].Icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(22, 22), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
                var v = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
                v.AddThemeConstantOverride("separation", -3);
                var k = Ui.Label("", Ui.Ink2, Ui.Medium, 10);
                var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
                row.AddThemeConstantOverride("separation", 5);
                var val = Ui.Label("", Ui.Ink, Ui.Bold, 15);
                var trend = Ui.Label("", Ui.Ink2, Ui.Bold, 10);
                trend.SizeFlagsVertical = SizeFlags.ShrinkEnd;
                foreach (var l in new[] { k, val, trend }) l.MouseFilter = MouseFilterEnum.Ignore;
                row.AddChild(val);
                row.AddChild(trend);
                v.AddChild(k);
                v.AddChild(row);
                h.AddChild(v);
                card.MouseEntered += () => ShowStatTip(slot);
                card.MouseExited += () => { if (_statHover == slot) HideStatTip(); };
                card.GuiInput += e =>
                {
                    if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && slot < _statsNow.Count) OpenWindow(_statsNow[slot].Window);
                };
                _stats.AddChild(card);
                _statCards.Add((card, k, val, trend));
            }
        }
        for (int i = 0; i < stats.Count && i < _statCards.Count; i++)
        {
            var (card, k, val, trend) = _statCards[i];
            var st = stats[i];
            k.Text = st.Key.ToUpperInvariant();
            val.Text = st.Value;
            trend.Text = st.Trend;
            trend.Visible = st.Trend != "";
            trend.AddThemeColorOverride("font_color", st.TrendColor);
            var box = Ui.Flat(st.Trouble ? new Color(Ui.Bad, 0.16f) : Colors.Transparent, 4, 1, 6);
            if (st.Trouble)
            {
                box.BorderColor = Ui.Bad;
                box.SetBorderWidthAll(1);
            }
            card.AddThemeStyleboxOverride("panel", box);
        }
        if (_statHover >= 0) FillStatTip(_statHover);
    }

    private void BuildStatTip()
    {
        _statTip = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 60, CustomMinimumSize = new Vector2(330, 0) };
        _statTip.AddThemeStyleboxOverride("panel", Castle.Oak(14, 12));
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 6);
        _statTip.AddChild(box);
        _statTipText = Ui.Rich("", 13);
        _statTipText.MouseFilter = MouseFilterEnum.Ignore;
        _statTipText.CustomMinimumSize = new Vector2(306, 0);
        box.AddChild(_statTipText);
        _statSpark = new Sparkline { CustomMinimumSize = new Vector2(306, 40), MouseFilter = MouseFilterEnum.Ignore };
        box.AddChild(_statSpark);
        box.MoveChild(_statSpark, 0);
        AddChild(_statTip);
    }

    private void ShowStatTip(int i)
    {
        _statHover = i;
        FillStatTip(i);
        _statTip.Visible = true;
        _statTip.MoveToFront();
        GuideEvent("resource");
    }

    private void HideStatTip()
    {
        _statHover = -1;
        _statTip.Visible = false;
    }

    private void FillStatTip(int i)
    {
        if (i >= _statsNow.Count) return;
        var t = _sim.Town;
        var st = _statsNow[i];
        var (text, series, color) = StatDetail(t, st);
        Ui.SetRich(_statTipText, text);
        _statSpark.Values = series;
        _statSpark.Line = color;
        _statSpark.Visible = series.Count >= 2;
        _statTip.ResetSize();
        var card = _statCards[i].Card.GetGlobalRect();
        float x = Mathf.Clamp(card.Position.X - 8, Gap, GetViewportRect().Size.X - _statTip.Size.X - Gap);
        _statTip.Position = new Vector2(x, TopBar + 4);
    }

    /// <summary>A resource's month in full: what moved it, where it is heading, and what to do about it.</summary>
    private (string Text, List<float> Series, Color Line) StatDetail(Town t, Stat st)
    {
        var r = Trends.Rules(t);
        var days = t.Trends.Days;
        string Head(string s) => $"[font_size=15][b]{s}[/b][/font_size]";
        string Dim(string s) => Ui.C(Ui.Ink2, s);
        string Advice(string s) => "\n" + Ui.C(Ui.Gold, s);
        string span = days.Count >= r.Days ? "the last 30 days" : $"the {days.Count} {(days.Count == 1 ? "day" : "days")} so far, at that pace for a month";
        switch (st.Key)
        {
            case "Treasury":
            {
                double net = Trends.Net(t);
                var sb = new System.Text.StringBuilder();
                sb.Append(Head(net >= 0 ? $"Treasury: earning {Math.Round(net):N0} gold a month" : $"Treasury: losing {Math.Abs(Math.Round(net)):N0} gold a month"));
                sb.Append('\n').Append(Dim(Trends.Runway(t) is double run
                    ? run < 1 ? "The coffers are empty." : $"At this rate the coffers are empty in about {Ui.Days((int)run)}."
                    : $"{Math.Floor(t.Treasury):N0} gold in hand, and growing."));
                var books = Trends.Books(t);
                if (books.Count > 0)
                {
                    sb.Append("\n\n");
                    var shown = books.Count > 9 ? books.Take(5).Concat(books.TakeLast(4)).ToList() : books;
                    foreach (var (acc, amt) in shown)
                        sb.Append($"{Ui.Esc(acc)}{(acc == TreasuryAccount.SalesTax || acc == TreasuryAccount.HearthTax ? Dim($" (rate {Math.Round(t.TaxRate * 100)}%)") : "")}   {Ui.C(amt >= 0 ? Ui.Good : Ui.Bad, $"[b]{(amt >= 0 ? "+" : "−")}{Math.Abs(amt):N0}[/b]")}\n");
                    if (shown.Count < books.Count) sb.Append(Dim($"and {books.Count - shown.Count} smaller\n"));
                    sb.Append($"[b]Net over {span}[/b]   {Ui.C(net >= 0 ? Ui.Good : Ui.Bad, $"[b]{(net >= 0 ? "+" : "−")}{Math.Abs(Math.Round(net)):N0}[/b]")}");
                }
                if (net < 0)
                {
                    var worst = books.LastOrDefault();
                    sb.Append(Advice(worst.Account == TreasuryAccount.Bounties ? "Post fewer or cheaper bounties, or raise taxes in the Council." : "Raise taxes in the Council, or open trade routes in Diplomacy."));
                }
                else sb.Append(Advice("Click to open the Council and its taxes."));
                return (sb.ToString(), days.Select(d => (float)d.Treasury).ToList(), net >= 0 ? Ui.Good : Ui.Bad);
            }
            case "Food":
            {
                double eaten = Trends.Eaten(t), imported = Trends.Imported(t);
                string text = Head(Trends.FoodDays(t) is double fd ? $"Food: {fd:N0} days of bread in store" : "Food: the market's stores")
                    + "\n" + Dim($"{Math.Round(t.Market.Stock(Good.Food)):N0} loads in the market; the town eats about {Math.Round(eaten):N0} a day.")
                    + (imported >= 1 ? "\n" + Ui.C(Ui.Bad, $"The caravans are bringing in {Math.Round(imported):N0} loads a day at a steep price: the farms can't keep up.") : "")
                    + "\n" + Dim($"A loaf costs {t.Market.Price(Good.Food):0.00} gold.")
                    + Advice(Trends.FoodTrouble(t) ? "Paint more farmland (B), or lay in winter stores from the City window." : "Click to open the City window and its winter stores.");
                return (text, days.Select(d => (float)d.Food).ToList(), Trends.FoodTrouble(t) ? Ui.Bad : Ui.Good);
            }
            case "Households":
            {
                double pop = Trends.Change(t, d => d.Households);
                string text = Head($"Households: {(pop >= 0 ? "+" : "−")}{Math.Abs(Math.Round(pop)):N0} a month")
                    + "\n" + Dim($"{t.Population:N0} households, about {City.Souls(t):N0} souls. {(days.Count > 0 && days[^1].Homeless > 0 ? $"{days[^1].Homeless} without a roof." : "Everyone has a roof.")}")
                    + Advice(pop < 0 ? "Families are leaving: the City window shows what keeps settlers away." : "Click to open the City window and its next charter.");
                return (text, days.Select(d => (float)d.Households).ToList(), pop >= 0 ? Ui.Good : Ui.Bad);
            }
            case "Mandate":
            {
                double m = Trends.Change(t, d => d.Mandate);
                string text = Head($"Mandate: {Math.Round(t.Mandate)} of 100, {(Math.Abs(m) < 1 ? "steady" : $"{(m >= 0 ? "+" : "−")}{Math.Abs(Math.Round(m))} a month")}")
                    + "\n" + Dim("How far the townsfolk back you. It follows their mood, and falls with hunger, high taxes, unrest and decrees.")
                    + Advice(Trends.MandateTrouble(t) ? "It is falling: lower taxes, sign the motions the town wants, or feed it." : "Click to open the Council.");
                return (text, days.Select(d => (float)d.Mandate).ToList(), m >= 0 ? Ui.Good : Ui.Bad);
            }
            case "Unrest":
            {
                double income = Taxes.Income(t), services = Taxes.Services(t);
                string text = Head($"Unrest: {Math.Round(t.Taxes.Unrest)} of 100")
                    + "\n" + Dim($"Services cost {services:N0}g a day; taxes, rents and trade bring {income:N0}g.")
                    + (Taxes.Funding(t) < 1 ? "\n" + Ui.C(Ui.Bad, "Services go short, so unrest climbs.") : "")
                    + Advice(Taxes.Funding(t) < 1 || t.Taxes.Unrest >= 1 ? "Raise taxes in the Council, or find more income." : "The town's services are paid for.");
                return (text, days.Select(d => (float)d.Unrest).ToList(), Ui.Bad);
            }
            case "Homes":
            {
                int homeless = t.Workers.Count(a => a.HomeId == null);
                int plots = Planning.VacantZoned(t, Zone.Residential);
                string text = Head(homeless > 0 ? $"Homes: {homeless} homeless" : $"Homes: {plots} residential {(plots == 1 ? "plot" : "plots")} free")
                    + "\n" + Dim("Townsfolk build their own homes on residential land.")
                    + Advice(homeless > 0 || plots == 0 ? "Paint more residential land from the Build bar (B)." : "Room to grow.");
                return (text, days.Select(d => (float)d.Homeless).ToList(), Ui.Bad);
            }
            default:
            {
                int away = t.Heroes.Count(a => a.IsAway);
                return (Head($"Heroes: {t.Heroes.Count}") + "\n" + Dim($"{t.Heroes.Count - away} in town, {away} on the road.") + Advice("Click to open the bounty board."), [], Ui.Gold);
            }
        }
    }
}

/// <summary>A small line of a month's values, for the top bar's hover card.</summary>
public partial class Sparkline : Control
{
    private List<float> _values = [];

    public List<float> Values
    {
        get => _values;
        set
        {
            _values = value;
            QueueRedraw();
        }
    }

    public Color Line { get; set; } = Ui.Good;

    public override void _Draw()
    {
        var size = Size;
        DrawLine(new Vector2(0, size.Y - 1), new Vector2(size.X, size.Y - 1), new Color(Ui.Line, 0.8f), 1);
        if (Values.Count < 2) return;
        float lo = Values.Min(), hi = Values.Max();
        if (hi - lo < 1e-3f) hi = lo + 1;
        var pts = new Vector2[Values.Count];
        for (int i = 0; i < Values.Count; i++)
            pts[i] = new Vector2(size.X * i / (Values.Count - 1), 3 + (size.Y - 8) * (1 - (Values[i] - lo) / (hi - lo)));
        DrawPolyline(pts, Line, 2, true);
    }
}
