using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// A d20 thrown on screen: a solid twenty-sided die tumbles in, bounces and settles on the face the sim
/// rolled, then each bonus flies off it as a chip and adds to the running total, which lands against the
/// DC with the result. Advantage throws a second die that dims once it loses; Inspiration picks the die up
/// and throws it again; a natural 20 bursts gold and a natural 1 cracks. Every roll the player sees goes
/// through here: the full size on story, sending and discovery cards, a quick small one for the light checks
/// on everyday decisions. A click skips to the end. Drawn flat in 2D from a projected icosahedron, so it
/// costs a few dozen triangles and nothing once it has landed.
/// </summary>
public partial class DiceRoll : Control
{
    /// <summary>One thing added to the die, as its chip reads it.</summary>
    public readonly record struct Term(int Value, string Label);

    /// <summary>Raised once, as the result shows (at once when the roll was skipped or is instant).</summary>
    public event Action? Landed;

    public bool Done => _t >= _reveal;

    private readonly StoryBeat _b;
    private readonly bool _small;
    private readonly string _caption;
    private readonly bool _advantage;
    private readonly List<Term> _terms;
    private readonly double _k;
    private double _t;
    private bool _landedSent;

    // The timeline, in seconds from the throw.
    private readonly double _throw, _inspireAt, _settle, _chip0, _chipGap, _chipFly, _totalAt, _reveal, _end;

    // Layout.
    private readonly float _w, _h, _r, _restY, _diceH;
    private readonly Vector2 _restA, _restB;
    private readonly List<(Rect2 Rect, string Text, Color Color)> _chips = [];
    private Rect2 _totalChip, _dcChip, _wordChip;
    private readonly Vector2 _readout;
    private readonly float _bannerY;

    private readonly System.Random _rand;
    private readonly int _firstOther;
    private readonly Vector3 _axisA, _axisB, _axisA2, _axisB2;

    public DiceRoll(StoryBeat beat, IEnumerable<Term> terms, string caption, bool small, bool instant, bool advantage = true)
    {
        _b = beat;
        _small = small;
        _caption = caption;
        _advantage = advantage && beat.Other is int;
        _terms = terms.Where(x => x.Value != 0 || x.Label != "").ToList();
        // Whatever the caller left out still has to add up to the sim's total.
        int missing = beat.Total - beat.Die - _terms.Sum(x => x.Value);
        if (missing != 0) _terms.Add(new Term(missing, "other"));
        _k = small ? 0.55 : 1;
        _rand = new System.Random(beat.Tick * 7919 + beat.Die * 31 + beat.Dc * 3 + beat.Total);
        _firstOther = beat.Rerolled is int first ? 1 + _rand.Next(Math.Max(1, first)) : 0;
        _axisA = RandomAxis();
        _axisB = RandomAxis();
        _axisA2 = RandomAxis();
        _axisB2 = RandomAxis();

        _throw = 1.2 * _k;
        _inspireAt = beat.Rerolled is int ? _throw : -1;
        double lands = beat.Rerolled is int ? _throw * 2 + 0.75 * _k : _throw;
        _settle = lands;
        _chip0 = lands + 0.3 * _k;
        _chipGap = 0.42 * _k;
        _chipFly = 0.32 * _k;
        _totalAt = _chip0 + _chipGap * _terms.Count + _chipFly + 0.15 * _k;
        _reveal = _totalAt + 0.35 * _k;
        _end = _reveal + (beat.Result is StoryResult.Critical or StoryResult.Fumble ? 1.6 : 0.5);

        MouseFilter = MouseFilterEnum.Stop;
        TooltipText = "";
        if (small)
        {
            _r = 19;
            _h = 52;
            _diceH = _h;
            _restY = _h / 2;
            _restA = new Vector2(8 + _r, _restY);
            _restB = _restA + new Vector2(_r * 1.9f, 0);
            float x = (_advantage ? _restB.X : _restA.X) + _r + 14;
            const float ch = 26;
            float y = (_h - ch) / 2;
            _chips.Add((Chip(ref x, y, ch, beat.Die.ToString(), 14), beat.Die.ToString(), Ui.Ink));
            foreach (var term in _terms) _chips.Add((Chip(ref x, y, ch, TermText(term), 13), TermText(term), Ui.Ink));
            _totalChip = Chip(ref x, y, ch, $"= {beat.Total}", 15);
            _dcChip = Chip(ref x, y, ch, $"DC {beat.Dc}", 13);
            _wordChip = Chip(ref x, y, ch, Word(beat.Result), 13);
            _w = x;
            _readout = Vector2.Zero;
        }
        else
        {
            _r = 46;
            _diceH = 156;
            _restY = _diceH - _r - 14;
            const float ch = 32, gap = 8;
            // Chips wrap under the dice, centred.
            var texts = new List<(string Text, int Size)> { ($"d20  {beat.Die}", 15) };
            texts.AddRange(_terms.Select(x => (TermText(x), 15)));
            float maxRow = 540;
            var rows = new List<List<(float W, string Text)>> { new() };
            float rowW = 0;
            foreach (var (text, size) in texts)
            {
                float cw = Ui.Bold.GetStringSize(text, HorizontalAlignment.Left, -1, size).X + 26;
                if (rows[^1].Count > 0 && rowW + gap + cw > maxRow)
                {
                    rows.Add([]);
                    rowW = 0;
                }
                rowW += (rows[^1].Count > 0 ? gap : 0) + cw;
                rows[^1].Add((cw, text));
            }
            _w = Math.Max(520, rows.Max(r => r.Sum(c => c.W) + gap * (r.Count - 1)) + 20);
            float y = _diceH + 6;
            foreach (var row in rows)
            {
                float x = (_w - (row.Sum(c => c.W) + gap * (row.Count - 1))) / 2;
                foreach (var (cw, text) in row)
                {
                    _chips.Add((new Rect2(x, y, cw, ch), text, Ui.Ink));
                    x += cw + gap;
                }
                y += ch + gap;
            }
            _bannerY = y + 22;
            _h = _bannerY + 22;
            float cx = _w * 0.36f;
            _restA = new Vector2(_advantage ? cx - 52 : cx, _restY);
            _restB = new Vector2(cx + 62, _restY + _r * 0.28f);
            _readout = new Vector2(_w * 0.76f, 0);
        }
        CustomMinimumSize = new Vector2(_w, _h);
        if (instant) _t = _end;
    }

    /// <summary>What a mayor's roll adds: the ability, training, and each helper (a party over the stone, the Adventurers' Hall).</summary>
    public static List<Term> MayorTerms(Town t, StoryBeat b)
    {
        var sk = t.Content.Stories.Skill(b.Skill);
        string ab = (sk?.Ability ?? b.Skill).ToUpperInvariant();
        var terms = new List<Term> { new(b.Modifier, ab) };
        if (b.Proficiency != 0) terms.Add(new(b.Proficiency, "proficiency"));
        if (b.Help != 0) terms.AddRange(HelpTerms(b));
        return terms;
    }

    /// <summary>Each helper as its own term ("Brannoc +2, Adventurers' Hall +1"), or one if the list doesn't add up.</summary>
    private static List<Term> HelpTerms(StoryBeat b)
    {
        var list = new List<Term>();
        foreach (var part in b.Helpers.Split(", ", StringSplitOptions.RemoveEmptyEntries))
        {
            int sp = part.LastIndexOf(' ');
            if (sp < 0 || !int.TryParse(part[(sp + 1)..].Replace('−', '-'), out int v)) return [new(b.Help, "help")];
            list.Add(new(v, part[..sp]));
        }
        return list.Sum(x => x.Value) == b.Help ? list : [new(b.Help, b.Helpers != "" ? b.Helpers : "help")];
    }

    public static string Word(StoryResult r) => r switch
    {
        StoryResult.Critical => "NATURAL 20",
        StoryResult.Success => "SUCCESS",
        StoryResult.Partial => "SUCCESS, AT A COST",
        StoryResult.Fumble => "NATURAL 1",
        _ => "FAILURE",
    };

    public static Color WordColor(StoryResult r) => r switch
    {
        StoryResult.Critical => Ui.Gold,
        StoryResult.Success => Ui.Good,
        StoryResult.Partial => Ui.Gold,
        _ => Ui.Bad,
    };

    private static string TermText(Term x) => $"{(x.Value >= 0 ? "+" : "−")}{Math.Abs(x.Value)}  {x.Label}";

    private static Rect2 Chip(ref float x, float y, float h, string text, int size)
    {
        float w = Ui.Bold.GetStringSize(text, HorizontalAlignment.Left, -1, size).X + 18;
        var r = new Rect2(x, y, w, h);
        x += w + 6;
        return r;
    }

    private Vector3 RandomAxis() => new Vector3((float)_rand.NextDouble() - 0.5f, (float)_rand.NextDouble() - 0.5f, (float)_rand.NextDouble() - 0.5f).Normalized();

    /// <summary>Jump to the end: the die lands and the result shows.</summary>
    public void Skip()
    {
        if (_t < _reveal) _t = _reveal;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && !Done)
        {
            Skip();
            AcceptEvent();
        }
    }

    public override void _Process(double delta)
    {
        if (_t >= _end && _landedSent)
        {
            SetProcess(false);
            return;
        }
        _t += Math.Min(delta, 0.1);
        if (_t >= _reveal && !_landedSent)
        {
            _landedSent = true;
            Landed?.Invoke();
        }
        QueueRedraw();
    }

    // ---------- The die ----------

    private static readonly Vector3[] Verts;
    private static readonly (int A, int B, int C)[] Faces;
    private static readonly Vector3[] Normals, Ups;

    /// <summary>The number on each face; opposite faces add to 21, like a real d20.</summary>
    private static readonly int[] Numbers;

    static DiceRoll()
    {
        float p = (1 + Mathf.Sqrt(5)) / 2;
        var v = new List<Vector3>();
        foreach (float a in new[] { -1f, 1f })
            foreach (float b in new[] { -p, p })
            {
                v.Add(new Vector3(0, a, b));
                v.Add(new Vector3(a, b, 0));
                v.Add(new Vector3(b, 0, a));
            }
        Verts = v.Select(x => x.Normalized()).ToArray();
        float edge = 2 / Mathf.Sqrt(1 + p * p);
        bool Near(int i, int j) => Mathf.Abs(Verts[i].DistanceTo(Verts[j]) - edge) < 1e-3f;
        var faces = new List<(int, int, int)>();
        for (int i = 0; i < 12; i++)
            for (int j = i + 1; j < 12; j++)
                for (int k = j + 1; k < 12; k++)
                    if (Near(i, j) && Near(j, k) && Near(i, k)) faces.Add((i, j, k));
        Faces = faces.ToArray();
        Normals = Faces.Select(f => (Verts[f.Item1] + Verts[f.Item2] + Verts[f.Item3]).Normalized()).ToArray();
        Ups = Faces.Select((f, i) =>
        {
            var c = (Verts[f.Item1] + Verts[f.Item2] + Verts[f.Item3]) / 3;
            return (Verts[f.Item1] - c).Normalized();
        }).ToArray();
        Numbers = new int[Faces.Length];
        int[] order = [1, 7, 3, 9, 5, 2, 8, 4, 10, 6];
        int n = 0;
        for (int i = 0; i < Faces.Length; i++)
        {
            if (Numbers[i] != 0) continue;
            int opp = Array.FindIndex(Normals, x => x.Dot(Normals[i]) < -0.999f);
            Numbers[i] = order[n];
            Numbers[opp] = 21 - order[n];
            n++;
        }
    }

    /// <summary>The die's turn at rest with a face toward the viewer, its number upright.</summary>
    private static Basis Facing(int number)
    {
        int f = Math.Max(0, Array.IndexOf(Numbers, Math.Clamp(number, 1, 20)));
        var nrm = Normals[f];
        var up = Ups[f];
        return new Basis(up.Cross(nrm), up, nrm).Transposed();
    }

    private readonly record struct Pose(Vector2 At, Basis Turn, float Lift, float Alpha, float Scale);

    /// <summary>
    /// Where a die is: thrown in from the side (or hopped up again for a reroll), bouncing lower each time
    /// while it spins down onto its face, then rocking still.
    /// </summary>
    private Pose PoseAt(bool other)
    {
        var rest = other ? _restB : _restA;
        float scale = other ? 0.74f : 1;
        bool reroll = _b.Rerolled is int;
        double secondAt = _throw + 0.75 * _k;
        bool firstThrow = reroll && _t < secondAt;
        int value = other ? firstThrow ? _firstOther : _b.Other ?? 1 : firstThrow ? _b.Rerolled!.Value : _b.Die;
        double start = reroll && !firstThrow ? secondAt : 0;
        double u = Math.Clamp((_t - start) / _throw, 0, 1);
        Vector2 from = reroll && !firstThrow ? rest : other ? rest + new Vector2(_small ? 30 : 160, 0) : rest - new Vector2(_small ? 30 : 190, 0);
        float height = _small ? 14 : reroll && !firstThrow ? 46 : 58;
        float h = height * Mathf.Pow(1 - (float)u, 1.7f) * Mathf.Abs(Mathf.Cos(Mathf.Pi * 3 * (float)u));
        float ease = 1 - Mathf.Pow(1 - (float)u, 2.2f);
        var at = from.Lerp(rest, ease);
        var axis = other ? firstThrow || !reroll ? _axisB : _axisB2 : firstThrow || !reroll ? _axisA : _axisA2;
        float spin = (float)(22 * _throw * (1 - u) * (1 - u) / 2 / _k);
        var turn = Facing(value) * new Basis(axis, spin);
        double since = _t - start - _throw;
        if (since > 0) turn *= new Basis(Vector3.Right, (float)(0.13 * Math.Sin(since * 22) * Math.Exp(-since * 9)));
        float alpha = 1;
        if (other && _t > _settle) alpha = (float)Math.Max(0.38, 1 - (_t - _settle) / (0.4 * _k) * 0.62);
        if (other && !_advantage) alpha = 0;
        // A fumble shakes as it lands.
        if (!other && _b.Result == StoryResult.Fumble && _t > _reveal)
            at += new Vector2((float)(Math.Sin((_t - _reveal) * 70) * 5 * Math.Exp(-(_t - _reveal) * 5)), 0);
        return new Pose(at, turn, h, alpha, scale);
    }

    private static readonly StyleBoxFlat DieChipStyle = Ui.Box(new Color("9b2f2f"), new Color("3b0f0f"), 8, 1), TermChipStyle = Ui.Box(new Color(Ui.Panel2, 0.95f), Ui.Line, 8, 1);
    private static readonly Vector3 Light = new Vector3(-0.45f, 0.6f, 0.75f).Normalized();
    private static readonly Color Body = new("9b2f2f"), Edge = new("3b0f0f"), Ivory = new("f6e7c1"), GoldBody = new("d9a420"), Ash = new("4c4040");

    private void DrawDie(Pose p, bool kept)
    {
        if (p.Alpha <= 0) return;
        float r = _r * p.Scale;
        var c = p.At - new Vector2(0, p.Lift);
        // The shadow on the table shrinks and fades as the die flies higher.
        float shadow = 1 / (1 + p.Lift / 40);
        DrawSetTransform(p.At + new Vector2(0, r * 0.82f), 0, new Vector2(1, 0.28f));
        DrawCircle(Vector2.Zero, r * 0.95f * shadow, new Color(0, 0, 0, 0.32f * shadow * p.Alpha));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);

        var body = Body;
        if (kept && _t > _reveal)
        {
            float k = Mathf.Min(1, (float)((_t - _reveal) / 0.3));
            if (_b.Result == StoryResult.Critical) body = Body.Lerp(GoldBody, k);
            if (_b.Result == StoryResult.Fumble) body = Body.Lerp(Ash, k);
        }
        DrawD20(this, c, r, p.Turn, body, p.Alpha, true);
        if (kept && _b.Result == StoryResult.Fumble && _t > _reveal)
        {
            // A crack across the face.
            float k = Mathf.Min(1, (float)((_t - _reveal) / 0.25));
            var crack = new[] { new Vector2(-0.55f, -0.6f), new Vector2(-0.15f, -0.2f), new Vector2(-0.3f, 0.1f), new Vector2(0.12f, 0.35f), new Vector2(0.05f, 0.62f) }
                .Select(v => c + v * r).ToArray();
            int count = Math.Max(2, (int)Math.Ceiling(crack.Length * k));
            DrawPolyline(crack[..count], new Color(0.08f, 0.02f, 0.02f, 0.9f), Math.Max(1.5f, r / 18), true);
        }
    }

    /// <summary>A d20 drawn at c with radius r, turned by turn: its lit faces, edges and (big enough) numbers.</summary>
    public static void DrawD20(CanvasItem ci, Vector2 c, float r, Basis turn, Color body, float alpha, bool numbers)
    {
        var pts = new Vector2[3];
        for (int i = 0; i < Faces.Length; i++)
        {
            var n = turn * Normals[i];
            if (n.Z <= 0.02f) continue;
            var (a, b, d) = Faces[i];
            pts[0] = Project(c, r, turn * Verts[a]);
            pts[1] = Project(c, r, turn * Verts[b]);
            pts[2] = Project(c, r, turn * Verts[d]);
            float lit = 0.38f + 0.62f * Mathf.Max(0, n.Dot(Light));
            float spec = Mathf.Pow(Mathf.Max(0, n.Dot((Light + Vector3.Back).Normalized())), 24) * 0.35f;
            var col = new Color(body.R * lit + spec, body.G * lit + spec, body.B * lit + spec, alpha);
            ci.DrawColoredPolygon(pts, col);
            ci.DrawPolyline([pts[0], pts[1], pts[2], pts[0]], new Color(Edge, 0.85f * alpha), Math.Max(1, r / 34), true);
            if (!numbers || n.Z < 0.35f) continue;
            var ctr = (pts[0] + pts[1] + pts[2]) / 3;
            var up3 = turn * Ups[i];
            float angle = Mathf.Atan2(-up3.Y, up3.X) + Mathf.Pi / 2;
            int num = Numbers[i];
            string s = num is 6 or 9 ? num + "." : num.ToString();
            int size = Math.Max(8, (int)(r * 0.5f));
            var ink = new Color(Ivory, alpha * Mathf.Clamp((n.Z - 0.35f) * 3, 0, 1));
            var sz = Ui.Display.GetStringSize(s, HorizontalAlignment.Left, -1, size);
            ci.DrawSetTransform(ctr, angle, new Vector2(1, 1) * (0.55f + 0.45f * n.Z));
            ci.DrawString(Ui.Display, new Vector2(-sz.X / 2, Ui.Display.GetAscent(size) * 0.38f), s, HorizontalAlignment.Left, -1, size, ink);
            ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }

    /// <summary>The turn that shows a number face-up, spun down from a tumble of spin radians about axis.</summary>
    public static Basis Tumble(int number, Vector3 axis, float spin) => Facing(number) * new Basis(axis, spin);

    private static Vector2 Project(Vector2 c, float r, Vector3 v)
    {
        // A touch of perspective: nearer points sit a little further out.
        float f = 1 + v.Z * 0.12f;
        return c + new Vector2(v.X, -v.Y) * r * f;
    }

    // ---------- Drawing ----------

    public override void _Draw()
    {
        bool crit = _b.Result == StoryResult.Critical, fumble = _b.Result == StoryResult.Fumble;
        var a = PoseAt(false);
        if (crit && _t > _reveal) DrawBurst(a.At - new Vector2(0, a.Lift));
        if (!_small && _caption != "")
            DrawString(Ui.Bold, new Vector2(10, 16), _caption.ToUpperInvariant(), HorizontalAlignment.Left, _w - 20, 11, Ui.Ink2);
        if (_advantage)
        {
            var bp = PoseAt(true);
            DrawDie(bp, false);
            if (!_small && _t > _settle)
                DrawCentered(Ui.Bold, "ADVANTAGE", bp.At + new Vector2(0, _r * 0.74f + 22), 10, new Color(Ui.Ink2, bp.Alpha + 0.2f));
        }
        DrawDie(a, true);

        // Inspiration picks the die up for another throw.
        if (_inspireAt >= 0 && _t > _inspireAt && _t < _inspireAt + 0.75 * _k + 0.3)
        {
            float k = (float)((_t - _inspireAt) / (0.75 * _k + 0.3));
            var at = a.At - new Vector2(0, a.Lift);
            DrawArc(at, _r * (1.1f + k * 0.8f), 0, Mathf.Tau, 40, new Color(Ui.Gold, 1 - k), 3, true);
            if (!_small) DrawCentered(Ui.Bold, "INSPIRATION: THROW AGAIN", new Vector2(_restA.X, 34), 12, new Color(Ui.Gold, Mathf.Min(1, 3 * (1 - k))));
        }

        // Chips fly off the die to their places; the running total counts up as each one lands.
        int shown = _t >= _chip0 ? _b.Die : 0;
        for (int i = 0; i < _chips.Count; i++)
        {
            double at = _chip0 + (i == 0 ? -_chipFly : _chipGap * (i - 1));
            if (_t < at) continue;
            float k = (float)Math.Min(1, (_t - at) / _chipFly);
            if (i > 0 && k >= 1) shown += _terms[i - 1].Value;
            var (rect, text, color) = _chips[i];
            var origin = a.At - rect.Size / 2;
            var pos = origin.Lerp(rect.Position, 1 - Mathf.Pow(1 - k, 3));
            float s = 0.6f + 0.4f * k;
            bool dieChip = i == 0;
            var style = dieChip ? DieChipStyle : TermChipStyle;
            DrawSetTransform(pos + rect.Size * (1 - s) / 2, 0, new Vector2(s, s));
            DrawStyleBox(style, new Rect2(Vector2.Zero, rect.Size));
            int size = _small ? (dieChip ? 14 : 13) : 15;
            var ts = Ui.Bold.GetStringSize(text, HorizontalAlignment.Left, -1, size);
            var tcol = dieChip ? Ivory : i > 0 && _terms[i - 1].Value < 0 ? Ui.Bad : color;
            DrawString(Ui.Bold, new Vector2((rect.Size.X - ts.X) / 2, rect.Size.Y / 2 + Ui.Bold.GetAscent(size) * 0.36f), text, HorizontalAlignment.Left, -1, size, new Color(tcol, k));
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }

        bool final = _t >= _totalAt;
        var verdict = WordColor(_b.Result);
        if (_small)
        {
            if (final)
            {
                float k = (float)Math.Min(1, (_t - _totalAt) / (0.2 * _k));
                DrawChip(_totalChip, $"= {_b.Total}", 15, Ui.Gold, k);
                DrawChip(_dcChip, $"DC {_b.Dc}", 13, Ui.Ink2, k);
            }
            if (_t >= _reveal)
            {
                float k = (float)Math.Min(1, (_t - _reveal) / 0.2);
                DrawChip(_wordChip, Word(_b.Result), 13, verdict, k, true);
            }
            return;
        }

        // The readout: the running total, then its verdict against the DC.
        if (_t >= _chip0)
        {
            var rx = _readout.X;
            DrawCentered(Ui.Bold, "TOTAL", new Vector2(rx, 42), 11, Ui.Ink2);
            int lastLanded = _terms.Count == 0 ? 0 : (int)Math.Floor((_t - _chip0 - _chipFly) / _chipGap) + 1;
            double since = _terms.Count == 0 ? _t - _chip0 : _t - (_chip0 + _chipFly + _chipGap * Math.Clamp(lastLanded - 1, 0, _terms.Count - 1));
            float pop = since >= 0 && since < 0.18 ? 1 + 0.22f * (1 - (float)(since / 0.18)) : 1;
            var tcol = final ? (Sheet.Succeeded(_b) || _b.Result == StoryResult.Partial ? verdict : Ui.Bad) : Ui.Ink;
            if (final && _t - _totalAt < 0.25) pop = 1 + 0.3f * (1 - (float)((_t - _totalAt) / 0.25));
            DrawSetTransform(new Vector2(rx, 84), 0, new Vector2(pop, pop));
            DrawCentered(Ui.Display, (final ? _b.Total : shown).ToString(), Vector2.Zero, 52, tcol, outline: true);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            DrawCentered(Ui.Medium, $"against DC {_b.Dc}", new Vector2(rx, 122), 14, final ? Ui.Ink : Ui.Ink2);
        }
        if (_t >= _reveal)
        {
            float k = (float)Math.Min(1, (_t - _reveal) / 0.25);
            float s = 1 + 0.6f * (1 - k);
            var at = new Vector2(_w / 2, _bannerY);
            if (fumble) at += new Vector2((float)(Math.Sin((_t - _reveal) * 60) * 4 * Math.Exp(-(_t - _reveal) * 4)), 0);
            DrawSetTransform(at, 0, new Vector2(s, s));
            DrawCentered(Ui.Display, Word(_b.Result), Vector2.Zero, 28, new Color(verdict, k), outline: true);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }

    private void DrawChip(Rect2 r, string text, int size, Color color, float k, bool strong = false)
    {
        DrawStyleBox(Ui.Box(new Color(strong ? new Color(color, 0.2f) : Ui.Panel2, k), new Color(strong ? color : Ui.Line, k), 8, 1), r);
        var ts = Ui.Bold.GetStringSize(text, HorizontalAlignment.Left, -1, size);
        DrawString(Ui.Bold, new Vector2(r.Position.X + (r.Size.X - ts.X) / 2, r.Position.Y + r.Size.Y / 2 + Ui.Bold.GetAscent(size) * 0.36f), text, HorizontalAlignment.Left, -1, size, new Color(color, k));
    }

    private void DrawCentered(Font font, string text, Vector2 at, int size, Color color, bool outline = false)
    {
        var ts = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
        var pos = at + new Vector2(-ts.X / 2, font.GetAscent(size) * 0.36f);
        if (outline) DrawStringOutline(font, pos, text, HorizontalAlignment.Left, -1, size, Math.Max(4, size / 7), new Color(0.11f, 0.09f, 0.07f, color.A));
        DrawString(font, pos, text, HorizontalAlignment.Left, -1, size, color);
    }

    /// <summary>A natural 20: gold rays turn behind the die and sparks fly off it.</summary>
    private void DrawBurst(Vector2 c)
    {
        double s = _t - _reveal;
        float fade = (float)Math.Min(1, s / 0.3) * (float)Math.Max(0.35, 1 - Math.Max(0, s - 1.2) / 0.6);
        float reach = _r * (_small ? 1.7f : 2.4f);
        for (int i = 0; i < 12; i++)
        {
            float ang = (float)(i * Mathf.Tau / 12 + s * 0.6);
            var d1 = Vector2.FromAngle(ang - 0.09f) * reach;
            var d2 = Vector2.FromAngle(ang + 0.09f) * reach;
            DrawPolygon([c, c + d1, c + d2], [new Color(Ui.Gold, 0.45f * fade), new Color(Ui.Gold, 0), new Color(Ui.Gold, 0)]);
        }
        if (s > 1.2) return;
        var rand = new System.Random(_b.Tick + 20);
        for (int i = 0; i < 18; i++)
        {
            float ang = (float)(rand.NextDouble() * Mathf.Tau);
            float speed = (float)(0.6 + rand.NextDouble() * 0.8) * reach * 1.4f;
            float k = (float)(s / 1.2);
            var at = c + Vector2.FromAngle(ang) * speed * (1 - (1 - k) * (1 - k)) + new Vector2(0, 30 * k * k);
            DrawCircle(at, Math.Max(1.5f, _r / 18) * (1 - k * 0.5f), new Color(1, 0.86f, 0.4f, 1 - k));
        }
    }
}
