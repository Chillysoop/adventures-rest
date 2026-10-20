using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// Street life: small scenes at busy spots, read off what the town already knows. A preacher of the god at each
/// temple and shrine, a crier with the broadsheet's headline the days after it prints, a hawker outside each store,
/// a busker singing of the town's best hero on festival days, and a grumbler in a ward that is angry with the mayor.
/// Each is a figure with a speech bubble that changes every few seconds; they show only by day and up close.
/// Townsfolk in the grip of an inspiration or a break (<see cref="Moods"/>) say so over their own heads, in gold or in grey.
/// </summary>
public partial class Crowd
{
    private const float BubbleDistance = 32;
    /// <summary>Beyond this camera distance a bubble grows with it, so its words stay the same size on screen.</summary>
    private const float BubbleScaleFrom = 14;
    private const float LineSeconds = 6;
    private readonly List<Label3D> _bubbles = [];
    private List<Scene> _scenes = [];
    private List<(int Id, string Head, IReadOnlyList<string> Lines, bool Inspired)> _moods = [];
    private int _sceneTick = -1;

    private sealed record Scene(Vector2 P, Color Tunic, IReadOnlyList<string> Lines, int Salt);

    private static readonly Color Said = new("f3ead4"), Inspired = new("ffd66b"), Broken = new("b9c4d6");

    /// <summary>Townsfolk in the grip of an inspiration or a break, and what they say.</summary>
    private static List<(int, string, IReadOnlyList<string>, bool)> MoodLines(Town town)
    {
        var list = new List<(int, string, IReadOnlyList<string>, bool)>();
        foreach (var a in town.Agents)
        {
            if (a.Episode == "" || a.Departed || Moods.Episode(town, a) is not { } d || d.Emote.Count == 0) continue;
            bool inspired = Moods.IsInspiration(town, d);
            list.Add((a.Id, $"{a.GivenName}: {d.Name}{(inspired ? "!" : "")}", d.Emote, inspired));
        }
        return list;
    }

    private static readonly Color Robe = new("e6dcc0"), Crier = new("8a2a2a"), Apron = new("5a7a4a"), Motley = new("b0582a"), Grey = new("6a6a6a");

    /// <summary>The scenes the town supports right now.</summary>
    private static List<Scene> Scenes(Town town)
    {
        var list = new List<Scene>();
        if (town.Hour < 8 || town.Hour > 19) return list;
        foreach (var b in town.Buildings.Where(b => b.Complete && !b.Closed).OrderBy(b => b.Id))
        {
            var lot = town.Lot(b.LotId);
            var f = Geo.LotFront(lot);
            var at = Geo.LotPos(lot) + f * (lot.D / (float)Land.Res / 2 + 0.45f) + new Vector2(f.Y, -f.X) * 0.4f;
            if (b.Def.Id is "temple" or "shrine")
            {
                var god = Faith.Of(town, b);
                string name = god != null ? Faith.Prose(god) : town.Content.Shrines.Deity;
                list.Add(new(at, Robe, [$"Give thanks to {name}!", $"{Ui.Cap(name)} sees what you do in the dark!", $"Light a candle for {name} tonight!", "Repent, and be welcome!"], b.Id));
            }
            else if (b.Def.Id == "general_store")
            {
                var lines = new List<string> { $"Healing potions, {town.Content.Shrines.Potion.Price:0} gold!", "Rope! Lamp oil! Rations for the road!" };
                foreach (var w in town.Wares.Where(w => w.ShopId == b.Id).Take(2))
                    if (town.Content.Library.Item(w.ItemId) is { } item) lines.Add($"{item.Name}, hardly used!");
                list.Add(new(at, Apron, lines, b.Id));
            }
            else if (b.Def.Id == "town_hall" && Broadsheet.Latest(town) is { } issue && town.Day - issue.Day <= 2)
                list.Add(new(at, Crier, [$"{Broadsheet.Name(town)}! Issue {issue.Number}!", issue.Headline, "Read all about it!"], b.Id));
        }
        if (town.FestivalDays > 0)
        {
            var star = town.Heroes.Where(a => !a.Dead).OrderByDescending(a => a.Hero!.Renown).ThenBy(a => a.Id).FirstOrDefault();
            var song = new List<string> { "♪ Hey ho, the harvest's in! ♪", "♪ Drink to the walls and the watch upon them! ♪" };
            if (star != null) song.Add($"♪ Sing of {star.GivenName}, who never turned back! ♪");
            list.Add(new(Geo.Center + new Vector2(1.6f, 0.8f), Motley, song, 7));
        }
        foreach (var wardId in town.City.OpenWards)
        {
            if (Wards.Feeling(town, wardId) > -15) continue;
            var house = town.Buildings.Where(b => b.Complete && b.Def.Housing > 0 && City.WardOf(town, town.Lot(b.LotId)).Id == wardId).OrderBy(b => b.Id).FirstOrDefault();
            if (house == null) continue;
            var lot = town.Lot(house.LotId);
            var at = Geo.LotPos(lot) + Geo.LotFront(lot) * (lot.D / (float)Land.Res / 2 + 0.45f);
            string ward = City.Ward(town, wardId).Name;
            list.Add(new(at, Grey, [$"Nobody at the hall listens to {ward}!", "The mayor's never set foot down here.", "Mark my words, there'll be trouble."], house.Id));
        }
        return list;
    }

    /// <summary>Put the scenes' figures in the townsfolk batch and their bubbles over them.</summary>
    private void Street(Town town, float now)
    {
        var gen = map.Gen;
        // The scenes change with the hour, so they are worked out once an hour, not every frame.
        if (town.Tick != _sceneTick)
        {
            _scenes = Scenes(town);
            _moods = MoodLines(town);
            _sceneTick = town.Tick;
        }
        var scenes = _scenes;
        bool talk = map.Distance < BubbleDistance;
        var scale = Vector3.One * Mathf.Max(1, map.Distance / BubbleScaleFrom);
        int used = 0;
        foreach (var s in scenes)
        {
            float sway = Mathf.Sin(now * 1.3f + s.Salt) * 0.4f;
            Put(gen.At(s.P), sway + Mathf.Pi, s.Tunic, new Color("d9b48f"));
            if (!talk) continue;
            int beat = (int)((now + Geo.Hash(s.Salt) * LineSeconds) / LineSeconds);
            string line = s.Lines[(int)(Geo.Hash(s.Salt * 7 + beat) * s.Lines.Count) % s.Lines.Count];
            var bubble = Bubble(used++);
            bubble.Text = line;
            bubble.Modulate = Said;
            bubble.Position = gen.At(s.P, 0.62f);
            bubble.Scale = scale;
            bubble.Visible = true;
        }
        if (talk)
            foreach (var (id, head, lines, inspired) in _moods)
            {
                if (!People.Sprites.TryGetValue(id, out var s) || !s.Visible) continue;
                int beat = (int)((now + Geo.Hash(id) * LineSeconds) / LineSeconds);
                var bubble = Bubble(used++);
                bubble.Text = $"{head}\n\"{lines[(int)(Geo.Hash(id * 7 + beat) * lines.Count) % lines.Count]}\"";
                bubble.Modulate = inspired ? Inspired : Broken;
                bubble.Position = gen.At(s.P, 0.62f);
                bubble.Scale = scale;
                bubble.Visible = true;
            }
        for (int i = used; i < _bubbles.Count; i++) _bubbles[i].Visible = false;
    }

    private Label3D Bubble(int i)
    {
        while (_bubbles.Count <= i)
        {
            var l = new Label3D
            {
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = 0.0055f,
                FontSize = 30,
                OutlineSize = 8,
                Modulate = new Color("f3ead4"),
                OutlineModulate = new Color(0.08f, 0.06f, 0.04f, 0.9f),
                Width = 420,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                VerticalAlignment = VerticalAlignment.Bottom,
                NoDepthTest = true,
                RenderPriority = 10,
                OutlineRenderPriority = 9,
                Visible = false,
            };
            AddChild(l);
            _bubbles.Add(l);
        }
        return _bubbles[i];
    }
}
