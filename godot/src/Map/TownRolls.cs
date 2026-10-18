using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// Tiny d20s over people's heads on the town map: when a townsperson or hero makes a check (haggling at a
/// stall, playing to the tavern, a guard staring down a drunk), a little die tumbles above them for half a
/// second and lands on the roll, with the skill and total beside it, then fades. They show only up close
/// and on screen, a handful at a time, so a great city's busy hour costs no more than a quiet one.
/// </summary>
public partial class TownRolls(MapView map) : Control
{
    /// <summary>Nearer than this the pops show; further out they are skipped.</summary>
    public const float ShowDistance = 120;
    private const int MaxShown = 6;
    private const float Tumble = 0.5f, Hold = 1.9f, Fade = 0.45f;

    private sealed class Pop
    {
        public int AgentId;
        public Vector2? Place;
        public string Skill = "";
        public int Die, Total;
        public StoryResult Result;
        public float Age;
        public Vector3 Axis;
    }

    private readonly List<Pop> _pops = [];

    /// <summary>
    /// Show a check someone in town just made. Dropped when zoomed out, when they aren't on screen, or when
    /// enough are already up (the newest wait for none: the oldest give way).
    /// </summary>
    public void Show(int agentId, string skill, int die, int modifier, StoryResult result) => Show(agentId, skill, die, modifier, result, null);

    /// <summary>The same, with where it happened on the ground (in front of a tavern, a shop) for when the person isn't drawn there.</summary>
    public void Show(int agentId, string skill, int die, int modifier, StoryResult result, Vector2? place)
    {
        if (map.Distance > ShowDistance || map.Strategic || !Where(agentId, place, out _)) return;
        _pops.RemoveAll(p => p.AgentId == agentId);
        if (_pops.Count >= MaxShown) _pops.RemoveAt(0);
        float h = Geo.Hash(agentId * 131 + die * 7 + _pops.Count);
        _pops.Add(new Pop
        {
            AgentId = agentId,
            Place = place,
            Skill = skill,
            Die = Math.Clamp(die, 1, 20),
            Total = die + modifier,
            Result = result,
            Axis = new Vector3(h - 0.5f, 0.7f, 0.5f - Geo.Hash(agentId * 17 + die)).Normalized(),
        });
        SetProcess(true);
    }

    /// <summary>Above the person's head on screen, if they are in town and in view; else over the place it happened, if given.</summary>
    private bool Where(int agentId, Vector2? place, out Vector2 screen)
    {
        screen = default;
        if (map.Crowd.People.Sprites.TryGetValue(agentId, out var s) && s.Visible) return map.OnScreen(map.Gen.At(s.P, 0.62f), out screen);
        return place is Vector2 p && map.OnScreen(map.Gen.At(p, 0.62f), out screen);
    }

    public override void _Process(double delta)
    {
        if (_pops.Count == 0)
        {
            SetProcess(false);
            QueueRedraw();
            return;
        }
        foreach (var p in _pops) p.Age += (float)Math.Min(delta, 0.1);
        _pops.RemoveAll(p => p.Age > Tumble + Hold + Fade);
        if (map.Distance > ShowDistance || map.Strategic) _pops.Clear();
        QueueRedraw();
    }

    private static readonly Color Body = new("9b2f2f"), Pill = new(0.08f, 0.06f, 0.04f, 0.82f);

    public override void _Draw()
    {
        // Smaller as the camera pulls back, but never too small to read.
        float zoom = Mathf.Clamp(60 / Mathf.Max(20, map.Distance), 0.7f, 1.3f);
        foreach (var p in _pops)
        {
            if (!Where(p.AgentId, p.Place, out var at)) continue;
            float alpha = p.Age < Tumble + Hold ? 1 : 1 - (p.Age - Tumble - Hold) / Fade;
            float u = Mathf.Min(1, p.Age / Tumble);
            float r = 10 * zoom;
            // Tossed up from the head and caught.
            float lift = (1 - u) * Mathf.Abs(Mathf.Sin(u * Mathf.Pi * 2)) * 14 * zoom + 6 * zoom;
            var c = at - new Vector2(0, lift + r);
            var turn = DiceRoll.Tumble(p.Die, p.Axis, 14 * (1 - u) * (1 - u));
            var body = u >= 1 && p.Result == StoryResult.Critical ? new Color("d9a420") : u >= 1 && p.Result == StoryResult.Fumble ? new Color("4c4040") : Body;
            if (u >= 1)
            {
                // The verdict beside the die: the skill and the total, gold for a natural 20, red for a 1.
                string text = p.Result switch
                {
                    StoryResult.Critical => $"{p.Skill} · NAT 20!",
                    StoryResult.Fumble => $"{p.Skill} · NAT 1",
                    _ => $"{p.Skill} {p.Total}",
                };
                var col = DiceRoll.WordColor(p.Result);
                int size = (int)(11 * zoom);
                var ts = Ui.Bold.GetStringSize(text, HorizontalAlignment.Left, -1, size);
                var box = new Rect2(c + new Vector2(r * 0.6f, -ts.Y / 2 - 3), new Vector2(ts.X + r * 0.8f + 10, ts.Y + 6));
                DrawRect(box, new Color(Pill, Pill.A * alpha));
                DrawString(Ui.Bold, box.Position + new Vector2(r * 0.8f + 4, 3 + Ui.Bold.GetAscent(size)), text, HorizontalAlignment.Left, -1, size, new Color(col, alpha));
                float pop = p.Age - Tumble < 0.15f ? 1 + 0.35f * (1 - (p.Age - Tumble) / 0.15f) : 1;
                r *= pop;
            }
            DiceRoll.DrawD20(this, c, r, turn, body, alpha, false);
            if (u >= 1)
            {
                // Too small for numbers on the faces: the roll sits on the die.
                string n = p.Die.ToString();
                int size = (int)(r * 1.0f);
                var ts = Ui.Display.GetStringSize(n, HorizontalAlignment.Left, -1, size);
                var pos = c + new Vector2(-ts.X / 2, Ui.Display.GetAscent(size) * 0.36f);
                DrawStringOutline(Ui.Display, pos, n, HorizontalAlignment.Left, -1, size, 3, new Color(0.1f, 0.05f, 0.04f, alpha));
                DrawString(Ui.Display, pos, n, HorizontalAlignment.Left, -1, size, new Color("f6e7c1", alpha));
            }
        }
    }
}
