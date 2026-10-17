using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

public static class Palette
{
    public static readonly Color Grass = new("8fae6b");
    public static readonly Color GrassDark = new("7b9a5a");
    public static readonly Color Forest = new("4f7543");
    public static readonly Color ForestDark = new("3c5c34");
    public static readonly Color River = new("6d9fbf");
    public static readonly Color RiverLight = new("9cc3da");
    public static readonly Color Dirt = new("c8ab7e");
    public static readonly Color DirtDark = new("b0915f");
    public static readonly Color Plaza = new("d8cbb0");
    public static readonly Color Residential = new("a8bf84");
    public static readonly Color Commercial = new("c9b98f");
    public static readonly Color Farmland = new("b9a76a");
    public static readonly Color Furrow = new("9d8a4f");
    public static readonly Color Wheat = new("d9bf62");
    public static readonly Color Wall = new("efe2c6");
    public static readonly Color WallShade = new("d4c3a0");
    public static readonly Color RoofHouse = new("b5583f");
    public static readonly Color RoofTavern = new("8e3b2e");
    public static readonly Color RoofHall = new("4f5f8a");
    public static readonly Color RoofForge = new("5b5550");
    public static readonly Color RoofBrewery = new("7a5a34");
    public static readonly Color Stone = new("9a9389");
    public static readonly Color Timber = new("8a6239");
    public static readonly Color Ink = new("2c2620");
    public static readonly Color Gold = new("e8b93a");
    public static readonly Color Window = new("ffd27a");
    public static readonly Color Shadow = new(0, 0, 0, 0.25f);

    public static Color ClassColor(string cls) => cls switch
    {
        "fighter" => new("c0392b"),
        "barbarian" => new("d35400"),
        "rogue" => new("5b4a6e"),
        "ranger" => new("2e7d32"),
        "wizard" => new("3949ab"),
        "cleric" => new("f5f0e1"),
        "paladin" => new("e6c35c"),
        "warden" => new("7d6b4f"),
        "monk" => new("e08a3c"),
        "cartographer" => new("4fa3a5"),
        "sorcerer" => new("b03a7a"),
        "warlock" => new("4a2a5e"),
        "reliquarian" => new("a8a39a"),
        "druid" => new("6f9a3a"),
        "bard" => new("d46a9a"),
        "hedge-witch" => new("5e7d4a"),
        _ => new("dddddd"),
    };

    public static Color RaceColor(string race) => race switch
    {
        "human" => new("e9c9a0"),
        "dwarf" => new("c98b5e"),
        "elf" => new("d9e8b8"),
        "halfling" => new("f0b98a"),
        "gnome" => new("c4b0e0"),
        "half-orc" => new("9fbf7a"),
        _ => new("b9d0d8"),
    };
}

/// <summary>Grass, river and forest: drawn once, redrawn only when the sites move as the town grows.</summary>
public partial class TerrainLayer(MapView map) : Layer(map)
{
    public override void _Draw()
    {
        if (Map.Sim == null) return;
        var g = Map.Geo;
        const float W = Geo.World;
        Rect(-W * 2, -W * 2, W * 5, W * 5, Palette.Grass);
        // Mottled grass so the field does not read as a flat fill.
        var mottle = Palette.GrassDark with { A = 0.35f };
        for (int i = 0; i < 420; i++)
        {
            var p = new Vector2(-8 + Geo.Hash(i * 11 + 5) * (W + 16), -8 + Geo.Hash(i * 11 + 6) * (W + 16));
            Ellipse(p, 0.5f + Geo.Hash(i) * 0.6f, 0.3f + Geo.Hash(i + 1) * 0.3f, mottle, 12);
        }
        var river = g.River.ToArray();
        DrawPolyline(river, Palette.DirtDark, 1.25f, true);
        DrawPolyline(river, Palette.River, 1.0f, true);
        DrawPolyline(river, Palette.RiverLight, 0.35f, true);
        var shadow = new Color(0, 0, 0, 0.2f);
        foreach (var (p, r) in g.Trees)
        {
            if (g.Pins.Values.Any(s => Mathf.Abs(s.X - p.X) < 1.5f && Mathf.Abs(s.Y - p.Y) < 1.3f)) continue;
            Ellipse(p + new Vector2(0.06f, r * 0.7f), r * 0.9f, r * 0.4f, shadow, 12);
            Circle(p, r, Palette.ForestDark);
            Circle(p - new Vector2(r * 0.25f, r * 0.25f), r * 0.65f, Palette.Forest);
        }
    }
}

/// <summary>Lots, the palisade and the buildings: redrawn once per in-game hour.</summary>
public partial class TownLayer(MapView map) : Layer(map)
{
    public override void _Draw()
    {
        if (Map.Sim == null) return;
        var town = Town;
        DrawLots(town);
        DrawPalisade(town);
        foreach (var b in town.Buildings.OrderBy(b => b.LotId))
        {
            var p = Geo.LotPos(town.Lot(b.LotId));
            if (b.IsSite) DrawConstruction(b, p.X, p.Y);
            else DrawBuilding(b, p.X, p.Y);
        }
    }

    private void DrawLots(Town town)
    {
        // Zoned lots sit on packed dirt; neighbouring lots merge into streets.
        foreach (var l in town.Lots)
        {
            if (l.Zone is Zone.None or Zone.Farmland) continue;
            var p = Geo.LotPos(l);
            Rect(p.X - 0.04f, p.Y - 0.04f, 1.08f, 1.08f, Palette.Dirt);
        }
        foreach (var l in town.Lots)
        {
            if (l.Zone == Zone.None) continue;
            var p = Geo.LotPos(l);
            float inset = l.Zone == Zone.Farmland ? 0.04f : 0.13f;
            var c = l.Zone switch
            {
                Zone.Civic => Palette.Plaza,
                Zone.Commercial => Palette.Commercial,
                Zone.Farmland => Palette.Farmland,
                _ => Palette.Residential,
            };
            DrawRect(new Rect2(p.X + inset, p.Y + inset, 1 - inset * 2, 1 - inset * 2), c);
            if (l.Zone == Zone.Farmland)
                for (int i = 1; i < 6; i++)
                    DrawLine(new Vector2(p.X + 0.1f, p.Y + i / 6f), new Vector2(p.X + 0.9f, p.Y + i / 6f), Palette.Furrow, 0.035f, true);
        }
    }

    /// <summary>Bounds of the walled town: every zoned lot except the farms, which lie outside the wall.</summary>
    public static Rect2? WallBounds(Town town)
    {
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (var l in town.Lots)
        {
            if (l.Zone is Zone.None or Zone.Farmland) continue;
            x0 = Mathf.Min(x0, l.X);
            y0 = Mathf.Min(y0, l.Y);
            x1 = Mathf.Max(x1, l.X + 1);
            y1 = Mathf.Max(y1, l.Y + 1);
        }
        if (x0 == float.MaxValue) return null;
        return new Rect2(Geo.Margin + x0 - 0.35f, Geo.Margin + y0 - 0.35f, x1 - x0 + 0.7f, y1 - y0 + 0.7f);
    }

    private void DrawPalisade(Town town)
    {
        var d = town.Defenses;
        if (d.Palisade == PalisadeState.None || WallBounds(town) is not Rect2 b) return;
        float total = town.Content.Enemy.Palisade.Days * SimTime.HoursPerDay;
        float done = d.Palisade == PalisadeState.Building && d.PalisadeReadyTick is int ready ? 1 - (ready - town.Tick) / total : 1;
        // Sharpened stakes along the perimeter, laid clockwise so a rising wall reads as progress.
        float w = b.Size.X, h = b.Size.Y, perim = 2 * (w + h);
        int stakes = (int)(perim / 0.16f);
        int shown = (int)(stakes * Mathf.Clamp(done, 0, 1));
        float bx0 = b.Position.X, by0 = b.Position.Y, bx1 = b.End.X, by1 = b.End.Y;
        for (int i = 0; i < shown; i++)
        {
            float t = (float)i / stakes * perim;
            float x, y;
            if (t < w) (x, y) = (bx0 + t, by0);
            else if ((t -= w) < h) (x, y) = (bx1, by0 + t);
            else if ((t -= h) < w) (x, y) = (bx1 - t, by1);
            else (x, y) = (bx0, by1 - (t - w));
            Rect(x - 0.04f, y + 0.02f, 0.12f, 0.08f, new Color(0.16f, 0.1f, 0.04f, 0.3f));
            Poly(i % 2 == 1 ? new Color("8a6239") : new Color("7a5530"),
                new(x - 0.06f, y + 0.06f), new(x - 0.06f, y - 0.14f), new(x, y - 0.24f), new(x + 0.06f, y - 0.14f), new(x + 0.06f, y + 0.06f));
        }
        Vector2[] corners = [new(bx0, by0), new(bx1, by0), new(bx1, by1), new(bx0, by1)];
        for (int i = 0; i < Math.Min(4, d.Towers); i++)
        {
            var (x, y) = (corners[i].X, corners[i].Y);
            Rect(x - 0.18f, y - 0.08f, 0.42f, 0.3f, new Color(0.16f, 0.1f, 0.04f, 0.3f));
            Rect(x - 0.2f, y - 0.5f, 0.4f, 0.55f, new Color("6b4a2a"));
            Poly(new Color("9b3d2a"), new(x - 0.28f, y - 0.48f), new(x, y - 0.8f), new(x + 0.28f, y - 0.48f));
        }
    }

    private void DrawConstruction(Building b, float x, float y)
    {
        float p = Mathf.Min(1, (float)(b.BuildProgress / b.Def.BuildHours));
        Rect(x + 0.18f, y + 0.3f, 0.64f, 0.52f, Palette.Stone);
        float h = 0.52f * p;
        Rect(x + 0.22f, y + 0.82f - h, 0.56f, h, Palette.Timber);
        var scaffold = new Color("6b4a2a");
        foreach (float sx in new[] { 0.18f, 0.5f, 0.82f }) DrawLine(new(x + sx, y + 0.86f), new(x + sx, y + 0.2f), scaffold, 0.035f, true);
        DrawLine(new(x + 0.14f, y + 0.42f), new(x + 0.86f, y + 0.42f), scaffold, 0.035f, true);
        DrawLine(new(x + 0.14f, y + 0.62f), new(x + 0.86f, y + 0.62f), scaffold, 0.035f, true);
        DrawLine(new(x + 0.18f, y + 0.86f), new(x + 0.5f, y + 0.42f), scaffold, 0.035f, true);
        Rect(x + 0.15f, y + 0.06f, 0.7f, 0.08f, new Color(0, 0, 0, 0.35f));
        Rect(x + 0.15f, y + 0.06f, 0.7f * p, 0.08f, Palette.Gold);
    }

    private void House(float bx, float by, float w, float h, Color roof, float roofH = 0.55f)
    {
        Rect(bx + 0.06f, by + 0.06f, w, h, new Color(0.16f, 0.12f, 0.08f, 0.22f));
        float rh = h * roofH;
        Rect(bx, by + rh, w, h - rh, Palette.Wall);
        Rect(bx, by + h - 0.05f, w, 0.05f, Palette.WallShade);
        Poly(roof, new(bx - 0.04f, by + rh + 0.02f), new(bx + 0.1f, by), new(bx + w - 0.1f, by), new(bx + w + 0.04f, by + rh + 0.02f));
        DrawLine(new(bx + 0.1f, by + rh * 0.45f), new(bx + w - 0.1f, by + rh * 0.45f), new Color(0, 0, 0, 0.25f), 0.025f, true);
        Rect(bx + w / 2 - 0.05f, by + h - 0.18f, 0.1f, 0.16f, new Color("6b4a2a"));
    }

    private void DrawBuilding(Building b, float x, float y)
    {
        switch (b.Def.Id)
        {
            case "tavern":
                House(x + 0.1f, y + 0.16f, 0.8f, 0.68f, Palette.RoofTavern, 0.5f);
                Circle(new(x + 0.82f, y + 0.62f), 0.07f, Palette.Gold);
                break;
            case "town_hall":
                House(x + 0.08f, y + 0.24f, 0.84f, 0.62f, Palette.RoofHall, 0.5f);
                Rect(x + 0.4f, y + 0.04f, 0.2f, 0.3f, Palette.Wall);
                Poly(Palette.RoofHall, new(x + 0.36f, y + 0.08f), new(x + 0.5f, y - 0.1f), new(x + 0.64f, y + 0.08f));
                DrawLine(new(x + 0.5f, y - 0.1f), new(x + 0.5f, y - 0.3f), Palette.Ink, 0.025f, true);
                Rect(x + 0.5f, y - 0.3f, 0.16f, 0.09f, new Color("c0392b"));
                break;
            case "farm":
                for (int i = 0; i < 4; i++) Rect(x + 0.08f, y + 0.12f + i * 0.13f, 0.5f, 0.07f, Palette.Wheat);
                House(x + 0.6f, y + 0.4f, 0.32f, 0.42f, new Color("9b3d2a"));
                break;
            case "lumber_camp":
                foreach (var (sx, sy) in new[] { (0.2f, 0.2f), (0.35f, 0.12f), (0.15f, 0.4f) }) Rect(x + sx, y + sy, 0.08f, 0.08f, Palette.Timber);
                for (int i = 0; i < 3; i++) Rect(x + 0.12f, y + 0.58f + i * 0.08f, 0.4f, 0.07f, i % 2 == 1 ? new Color("a5784a") : Palette.Timber);
                House(x + 0.58f, y + 0.3f, 0.34f, 0.5f, Palette.Forest, 0.6f);
                break;
            case "forge":
                House(x + 0.16f, y + 0.24f, 0.68f, 0.58f, Palette.RoofForge, 0.5f);
                Rect(x + 0.62f, y + 0.06f, 0.12f, 0.26f, Palette.Stone);
                Circle(new(x + 0.68f, y + 0.06f), 0.06f, new Color(1, 0.47f, 0.16f, 0.8f));
                break;
            case "brewery":
                House(x + 0.14f, y + 0.2f, 0.62f, 0.62f, Palette.RoofBrewery, 0.5f);
                foreach (float by in new[] { 0.42f, 0.62f }) Ellipse(new(x + 0.84f, y + by), 0.08f, 0.09f, new Color("8a5a2b"), 12);
                break;
            case "scrying_pool":
                // A round stone basin of still water under a little shrine roof.
                Ellipse(new(x + 0.52f, y + 0.66f), 0.38f, 0.24f, new Color(0.16f, 0.12f, 0.08f, 0.22f), 20);
                Ellipse(new(x + 0.5f, y + 0.6f), 0.38f, 0.25f, Palette.Stone, 20);
                Ellipse(new(x + 0.5f, y + 0.58f), 0.29f, 0.17f, new Color("4b6f9c"), 20);
                Ellipse(new(x + 0.44f, y + 0.55f), 0.12f, 0.05f, new Color("b48cff", 0.7f), 14);
                foreach (float px in new[] { 0.16f, 0.84f }) Rect(x + px - 0.03f, y + 0.18f, 0.06f, 0.42f, Palette.Timber);
                Poly(Palette.RoofHall, new(x + 0.08f, y + 0.22f), new(x + 0.5f, y + 0.02f), new(x + 0.92f, y + 0.22f));
                break;
            case "sleepless_spire":
                // A tall stone tower with a crystal eye that never closes.
                Rect(x + 0.32f, y + 0.84f, 0.44f, 0.08f, new Color(0.16f, 0.12f, 0.08f, 0.25f));
                Poly(Palette.Stone, new(x + 0.3f, y + 0.88f), new(x + 0.38f, y - 0.2f), new(x + 0.62f, y - 0.2f), new(x + 0.7f, y + 0.88f));
                Poly(new Color("837c72"), new(x + 0.5f, y + 0.88f), new(x + 0.5f, y - 0.2f), new(x + 0.62f, y - 0.2f), new(x + 0.7f, y + 0.88f));
                Rect(x + 0.44f, y + 0.3f, 0.12f, 0.16f, Palette.Window);
                Poly(Palette.RoofHall, new(x + 0.32f, y - 0.18f), new(x + 0.5f, y - 0.62f), new(x + 0.68f, y - 0.18f));
                Circle(new(x + 0.5f, y - 0.66f), 0.11f, new Color("b48cff"));
                Circle(new(x + 0.5f, y - 0.66f), 0.05f, new Color("fff6d8"));
                break;
            // Stand-ins until the art pass draws them properly.
            case "shrine":
                Rect(x + 0.3f, y + 0.5f, 0.4f, 0.3f, Palette.Stone);
                Poly(Palette.RoofHall, new(x + 0.24f, y + 0.52f), new(x + 0.5f, y + 0.3f), new(x + 0.76f, y + 0.52f));
                Circle(new(x + 0.5f, y + 0.64f), 0.07f, Palette.Gold);
                break;
            case "temple":
                House(x + 0.1f, y + 0.26f, 0.8f, 0.6f, Palette.RoofHall, 0.45f);
                foreach (float px in new[] { 0.2f, 0.4f, 0.6f, 0.8f }) Rect(x + px - 0.03f, y + 0.56f, 0.06f, 0.28f, Palette.Wall);
                Circle(new(x + 0.5f, y + 0.12f), 0.1f, Palette.Gold);
                break;
            case "general_store":
                House(x + 0.16f, y + 0.24f, 0.68f, 0.58f, new Color("6a7a3a"), 0.5f);
                foreach (float px in new[] { 0.22f, 0.4f }) Rect(x + px, y + 0.72f, 0.12f, 0.1f, Palette.Timber);
                break;
            default:
                House(x + 0.2f, y + 0.22f, 0.6f, 0.58f, Palette.RoofHouse);
                break;
        }
        if (b.Closed || b.ForSale)
        {
            Rect(x + 0.08f, y + 0.08f, 0.84f, 0.84f, new Color(0.31f, 0.31f, 0.31f, 0.35f));
            Rect(x + 0.06f, y + 0.72f, 0.3f, 0.16f, new Color("f2e6cc"));
            Rect(x + 0.1f, y + 0.78f, 0.22f, 0.04f, Palette.Ink);
        }
    }
}

public sealed class Sprite
{
    public Vector2 P;
    public bool Visible;
}

/// <summary>Townsfolk and heroes in town, easing toward wherever the simulation says they are.</summary>
public partial class AgentLayer(MapView map) : Layer(map)
{
    public readonly Dictionary<int, Sprite> Sprites = [];

    public void Reset() => Sprites.Clear();

    private Vector2? Target(Agent a)
    {
        if (a.Departed || a.Dead || a.IsAway || a.Activity == Activity.Sleeping) return null;
        float j1 = Geo.Hash(a.Id * 13 + 1), j2 = Geo.Hash(a.Id * 13 + 2);
        if (a.LocationId is int loc)
        {
            var lot = Town.Lot(Town.Building(loc).LotId);
            // Gather along the street in front of the building.
            return new Vector2(Geo.Margin + lot.X + 0.15f + j1 * 0.7f, Geo.Margin + lot.Y + 0.86f + (j2 - 0.5f) * 0.12f);
        }
        // Wanderers drift around the square, picking a new spot every few hours.
        int k = Town.Tick / 3;
        float ang = Geo.Hash(a.Id * 31 + k) * Mathf.Tau;
        float r = 1 + Geo.Hash(a.Id * 17 + k) * 3.2f;
        return Geo.Center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
    }

    public void Advance(float dt)
    {
        float k = 1 - Mathf.Exp(-dt * 4);
        foreach (var a in Town.Agents)
        {
            var tgt = Target(a);
            Sprites.TryGetValue(a.Id, out var s);
            if (tgt == null)
            {
                if (s != null) s.Visible = false;
                continue;
            }
            if (s == null) Sprites[a.Id] = s = new Sprite { P = Geo.Center };
            s.Visible = true;
            s.P += (tgt.Value - s.P) * k;
        }
    }

    public override void _Draw()
    {
        if (Map.Sim == null) return;
        var town = Town;
        var councillors = town.Seats.Where(s => s.HolderId != null).Select(s => s.HolderId!.Value).ToHashSet();
        var order = town.Agents.Where(a => Sprites.TryGetValue(a.Id, out var s) && s.Visible).OrderBy(a => Sprites[a.Id].P.Y).ThenBy(a => a.Id);
        foreach (var a in order)
        {
            var p = Sprites[a.Id].P;
            Ellipse(p + new Vector2(0, 0.07f), 0.12f, 0.05f, Palette.Shadow, 10);
            float r = a.Hero != null ? 0.11f : 0.09f;
            var head = p - new Vector2(0, 0.04f);
            if (a.Hero != null)
            {
                Circle(head, r + 0.03f, Palette.Gold);
                Circle(head, r, Palette.ClassColor(a.Hero.Cls.Id));
            }
            else
            {
                Circle(head, r + 0.022f, a.Activity == Activity.Drinking ? new Color("e0a04a") : new Color(0.12f, 0.08f, 0.04f, 0.6f));
                Circle(head, r, Palette.RaceColor(a.Race));
            }
            if (councillors.Contains(a.Id))
                Poly(Palette.Gold, new(p.X - 0.08f, p.Y - 0.16f), new(p.X - 0.08f, p.Y - 0.26f), new(p.X - 0.04f, p.Y - 0.2f), new(p.X, p.Y - 0.28f),
                    new(p.X + 0.04f, p.Y - 0.2f), new(p.X + 0.08f, p.Y - 0.26f), new(p.X + 0.08f, p.Y - 0.16f));
        }
    }
}

/// <summary>Day and night: a cool wash after dusk, warm light spilling from windows on top.</summary>
public partial class NightLayer(MapView map) : Layer(map)
{
    public override void _Draw()
    {
        if (Map.Sim == null) return;
        var town = Town;
        const float W = Geo.World;
        if (town.Outcome == Outcome.Defeat) Rect(-W * 2, -W * 2, W * 5, W * 5, new Color(0.35f, 0.08f, 0.04f, 0.28f));
        float night = Geo.Nightness(town, Map.HourFraction);
        if (night <= 0) return;
        Rect(-W * 2, -W * 2, W * 5, W * 5, new Color(0.08f, 0.11f, 0.25f, 0.5f * night));
        var occupied = new HashSet<int>();
        foreach (var a in town.Agents) if (!a.Departed && a.LocationId is int l && a.Activity != Activity.Sleeping) occupied.Add(l);
        foreach (var b in town.Buildings)
        {
            if (!b.Complete) continue;
            if (!occupied.Contains(b.Id) && !b.Def.Venue && b.Def.Id != "forge") continue;
            var lot = town.Lot(b.LotId);
            var c = new Vector2(Geo.Margin + lot.X + 0.5f, Geo.Margin + lot.Y + 0.6f);
            float glow = b.Def.Venue ? 0.9f : 0.55f;
            for (int i = 3; i >= 1; i--) Circle(c, glow * i / 3, new Color(1, 0.78f, 0.43f, 0.07f * night));
            Rect(c.X - 0.2f, c.Y - 0.02f, 0.08f, 0.08f, Palette.Window);
            Rect(c.X + 0.12f, c.Y - 0.02f, 0.08f, 0.08f, Palette.Window);
        }
    }
}

/// <summary>The fog of war, a soft texture over the world.</summary>
public partial class FogLayer : Layer
{
    public FogLayer(MapView map) : base(map) => TextureFilter = TextureFilterEnum.Linear;

    public override void _Draw()
    {
        if (Map.Sim == null) return;
        const float W = Geo.World;
        var c = new Color("1b1711", Fog.UnknownAlpha);
        float x0 = Fog.X0, x1 = Fog.X0 + Fog.Span;
        // Beyond the fog texture everything is unknown.
        Rect(-W * 3, -W * 3, W * 7, x0 + W * 3, c);
        Rect(-W * 3, x1, W * 7, W * 4, c);
        Rect(-W * 3, x0, x0 + W * 3, x1 - x0, c);
        Rect(x1, x0, W * 4, x1 - x0, c);
        DrawTextureRect(Map.Fog.Texture, new Rect2(x0, x0, Fog.Span, Fog.Span), false);
    }
}

/// <summary>Sites, parties on the road, the warband, scryings and the selection.</summary>
public partial class OverlayLayer(MapView map) : Layer(map)
{
    public override void _Draw()
    {
        if (Map.Sim == null) return;
        var town = Town;
        DrawRoads(town);
        DrawSpire(town);
        DrawSites(town);
        DrawParties(town);
        DrawScouts(town);
        DrawWarband(town);
        DrawSelection(town);
    }

    /// <summary>Tracks to the sites that reports describe; the fog hides the rest.</summary>
    private void DrawRoads(Town town)
    {
        foreach (var r in Map.Fog.Reports)
        {
            if (!Map.Geo.Pins.TryGetValue(r.SiteId, out var pin)) continue;
            var end = Geo.Center + (pin - Geo.Center) * r.Reach;
            DrawDashedLine(Geo.Center, end, new Color("6b4a2a", 0.55f), 0.06f, 0.18f);
        }
    }

    private void DrawSites(Town town)
    {
        var fog = Map.Fog;
        foreach (var site in town.Sites)
        {
            // Nobody has so much as heard of it: nothing on the map, unless it is in plain sight.
            if (site.Known == SiteKnowledge.Unheard) continue;
            if (!Map.Geo.Pins.TryGetValue(site.Def.Id, out var pin)) continue;
            var sight = fog.SightOf(site.Def.Id, pin);
            var bounty = town.Bounties.FirstOrDefault(b => b.SiteId == site.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken);
            float r = 0.42f + Math.Min(site.Def.Tier, 5) * 0.06f;
            if (sight == Sight.Rumoured)
            {
                // A name from tavern gossip: a dashed ring and a question mark, nothing more.
                DrawArc(pin, r, 0, Mathf.Tau, 32, new Color("efe6d2", 0.5f), 0.05f, true);
                Circle(pin, r * 0.55f, new Color("efe6d2", 0.12f));
                if (bounty != null) Ring(pin, r + 0.12f, Palette.Gold, 0.07f);
                continue;
            }
            bool enemy = site.Def.Id == town.Content.Enemy.Outpost.Id;
            // A scouted site shows what the last report said; only a live view shows it now.
            bool cleared = sight == Sight.Live ? site.Cleared : fog.LastReport(site.Def.Id)?.Outcome == ExpeditionOutcome.Cleared;
            Ellipse(pin + new Vector2(0.05f, 0.1f), r, r * 0.8f, new Color(0, 0, 0, 0.3f), 24);
            var fill = cleared ? new Color("6f8f5b") : enemy ? new Color("8e2a1f") : site.Def.Tier >= 3 ? new Color("4a2c3a") : site.Def.Tier == 2 ? new Color("5a3d2e") : new Color("5e5236");
            Circle(pin, r, fill);
            Ring(pin, r, cleared ? new Color("e8f0d8") : bounty != null ? Palette.Gold : new Color("1d1712"), bounty != null ? 0.09f : 0.05f);
            var glyph = cleared ? new Color("e8f0d8") : new Color("efe6d2");
            if (cleared)
            {
                Rect(pin.X - 0.03f, pin.Y - 0.26f, 0.05f, 0.5f, glyph);
                Poly(glyph, new(pin.X + 0.02f, pin.Y - 0.26f), new(pin.X + 0.26f, pin.Y - 0.16f), new(pin.X + 0.02f, pin.Y - 0.06f));
            }
            else
            {
                Circle(pin - new Vector2(0, 0.04f), 0.17f, glyph);
                Rect(pin.X - 0.1f, pin.Y + 0.06f, 0.2f, 0.12f, glyph);
                Circle(new(pin.X - 0.065f, pin.Y - 0.04f), 0.045f, new Color("1d1712"));
                Circle(new(pin.X + 0.065f, pin.Y - 0.04f), 0.045f, new Color("1d1712"));
            }
            for (int i = 0; i < Math.Min(site.Def.Tier, 5); i++)
                Circle(new(pin.X - (Math.Min(site.Def.Tier, 5) - 1) * 0.08f + i * 0.16f, pin.Y + r + 0.12f), 0.055f, Palette.Gold);
            float now = (float)Time.GetTicksMsec() / 1000f;
            switch (Watch.LiveOf(town, site))
            {
                case LiveSource.Scrying:
                    // The scrying eye: a slow violet shimmer around the site.
                    Ring(pin, 2.4f + Mathf.Sin(now * 2) * 0.06f, new Color("b48cff", 0.55f), 0.08f);
                    break;
                case LiveSource.Mirror:
                    Ring(pin, 2.4f, new Color("cfe3f0", 0.45f), 0.06f);
                    break;
                case LiveSource.Scout:
                    DrawArc(pin, 2.4f, 0, Mathf.Tau, 48, new Color("8cc084", 0.5f), 0.06f, true);
                    break;
                case LiveSource.Outpost:
                    // The town's banner over a held site.
                    Ring(pin, 2.4f, new Color("e8c36a", 0.5f), 0.07f);
                    Poly(Palette.Gold, pin + new Vector2(-0.05f, -r - 0.9f), pin + new Vector2(0.4f, -r - 0.75f), pin + new Vector2(-0.05f, -r - 0.6f));
                    break;
            }
            if (Watch.MirrorAt(town, site.Def.Id) != null)
            {
                // The hidden mirror: a silver lozenge tucked beside the site.
                var m = pin + new Vector2(r + 0.18f, -r * 0.4f);
                Poly(new Color("1d1712"), m + new Vector2(0, -0.2f), m + new Vector2(0.13f, 0), m + new Vector2(0, 0.2f), m + new Vector2(-0.13f, 0));
                Poly(new Color("dfe8ee"), m + new Vector2(0, -0.15f), m + new Vector2(0.09f, 0), m + new Vector2(0, 0.15f), m + new Vector2(-0.09f, 0));
                Poly(new Color("9fb8c8"), m + new Vector2(0, -0.15f), m + new Vector2(0.09f, 0), m + new Vector2(0, 0.02f));
            }
        }
    }

    /// <summary>
    /// A party marches out under its banner, and once past the town's light it is gone into the
    /// fog. It is seen again only on its way home, or under a scrying.
    /// </summary>
    private void DrawParties(Town town)
    {
        foreach (var p in town.Parties)
        {
            if (p.Returned || !Map.Geo.Pins.TryGetValue(p.SiteId, out var pin)) continue;
            float leg = Geo.PartyLeg(town, p, Map.HourFraction);
            var pos = Geo.Center + (pin - Geo.Center) * leg;
            float fogA = Map.Fog.AlphaAt(pos);
            if (fogA > 0.45f) continue;
            float fade = 1 - Mathf.Clamp(fogA / 0.45f, 0, 1);
            // Drawn a little larger than townsfolk so a company on the road reads at a glance.
            DrawSetTransform(pos, 0, new Vector2(1.6f, 1.6f));
            Ellipse(new Vector2(0, 0.2f), 0.32f, 0.1f, Palette.Shadow with { A = 0.25f * fade }, 14);
            Rect(-0.02f, -0.5f, 0.04f, 0.55f, Palette.Ink with { A = fade });
            Poly(Palette.Gold with { A = fade }, new(0.02f, -0.5f), new(0.34f, -0.4f), new(0.02f, -0.28f));
            for (int i = 0; i < p.Members.Count; i++)
            {
                var a = town.Agent(p.Members[i]);
                var mp = new Vector2(-0.2f + i % 3 * 0.16f, 0.12f + i / 3 * 0.14f);
                Circle(mp, 0.075f, Palette.Gold with { A = fade });
                Circle(mp, 0.06f, Palette.ClassColor(a.Hero?.Cls.Id ?? "") with { A = fade });
            }
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }

    /// <summary>The Sleepless Spire's reach: a faint golden boundary around everything it watches.</summary>
    private void DrawSpire(Town town)
    {
        int reach = Watch.Reach(town);
        if (reach <= 0) return;
        float r = Map.Geo.TownRadius + 1.3f + reach * 1.1f + 2.2f;
        DrawArc(Geo.Center, r, 0, Mathf.Tau, 128, new Color("e8b93a", 0.35f), 0.08f, true);
    }

    /// <summary>
    /// Scouts slip out alone and are lost to the fog like parties; one watching a site is drawn
    /// crouched at its edge, since the site is in sight while they stay.
    /// </summary>
    private void DrawScouts(Town town)
    {
        foreach (var s in town.Scouts)
        {
            if (!s.Out || !Map.Geo.Pins.TryGetValue(s.SiteId, out var pin)) continue;
            float leg = (float)Watch.ScoutLeg(town, s, Map.HourFraction);
            var site = Quests.Site(town, s.SiteId)!;
            float r = 0.42f + Math.Min(site.Def.Tier, 5) * 0.06f;
            var dir = (pin - Geo.Center).Normalized();
            var pos = leg >= 1 ? pin - dir * (r + 0.45f) + new Vector2(dir.Y, -dir.X) * 0.3f : Geo.Center + (pin - Geo.Center) * leg;
            float fogA = Map.Fog.AlphaAt(pos);
            if (fogA > 0.45f) continue;
            float fade = 1 - Mathf.Clamp(fogA / 0.45f, 0, 1);
            var a = town.Agent(s.HeroId);
            DrawSetTransform(pos, 0, new Vector2(1.5f, 1.5f));
            Ellipse(new Vector2(0, 0.1f), 0.16f, 0.06f, Palette.Shadow with { A = 0.25f * fade }, 12);
            // A hooded figure in a green cloak.
            Poly(new Color("3f5f3a") with { A = fade }, new(-0.11f, 0.1f), new(0, -0.16f), new(0.11f, 0.1f));
            Circle(new Vector2(0, -0.12f), 0.065f, Palette.ClassColor(a.Hero?.Cls.Id ?? "") with { A = fade });
            if (s.Mirror) Poly(new Color("dfe8ee") with { A = fade }, new(0.1f, -0.06f), new(0.17f, 0.02f), new(0.1f, 0.1f), new(0.03f, 0.02f));
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }

    /// <summary>The warband on the march, closing on the town; seen only once it is inside the light.</summary>
    private void DrawWarband(Town town)
    {
        var en = town.Enemy;
        if (en.Stage != ThreatStage.Marching || en.ArrivalTick is not int arrival) return;
        float total = town.Content.Enemy.MarchDays * SimTime.HoursPerDay;
        float t = Mathf.Clamp(1 - (arrival - town.Tick - Map.HourFraction) / total, 0, 1);
        var from = Map.Geo.Pins.TryGetValue(town.Content.Enemy.Outpost.Id, out var camp) ? camp : Geo.Center + new Vector2(9, -9);
        var pos = from + (Geo.Center - from) * t * 0.75f;
        if (!Map.Fog.IsLive(pos)) return;
        int n = Math.Min(60, (int)Math.Round(en.Strength / 6));
        for (int i = 0; i < n; i++)
        {
            var o = new Vector2((Geo.Hash(i * 5 + 1) - 0.5f) * 1.6f, (Geo.Hash(i * 5 + 2) - 0.5f) * 1.1f);
            Ellipse(pos + o + new Vector2(0, 0.05f), 0.08f, 0.035f, new Color(0, 0, 0, 0.3f), 8);
            Circle(pos + o - new Vector2(0, 0.03f), 0.065f, i % 7 == 0 ? new Color("3a1a14") : new Color("6a2a1f"));
        }
        Rect(pos.X - 0.02f, pos.Y - 0.95f, 0.045f, 0.9f, Palette.Ink);
        Poly(new Color("c0392b"), new(pos.X + 0.025f, pos.Y - 0.95f), new(pos.X + 0.5f, pos.Y - 0.8f), new(pos.X + 0.025f, pos.Y - 0.62f));
    }

    private void DrawSelection(Town town)
    {
        var sel = Map.Selection;
        if (sel == null) return;
        var c = new Color("fff6d8");
        if (sel.Kind == "building")
        {
            var lot = town.Lot(town.Building(sel.Id).LotId);
            var p = Geo.LotPos(lot);
            DrawRect(new Rect2(p.X + 0.03f, p.Y + 0.03f, 0.94f, 0.94f), c, false, 0.06f);
        }
        else if (sel.Kind == "agent")
        {
            if (Map.Agents.Sprites.TryGetValue(sel.Id, out var s) && s.Visible) Ring(s.P - new Vector2(0, 0.04f), 0.2f, c, 0.05f);
        }
        else if (Map.Geo.Pins.TryGetValue(sel.SiteId, out var pin)) Ring(pin, 0.85f, c, 0.06f);
    }
}

/// <summary>Names and figures in screen space, so text stays sharp at any zoom.</summary>
public partial class LabelLayer(MapView map) : Control
{
    private readonly MapView _map = map;

    public override void _Draw()
    {
        if (_map.Sim == null) return;
        var town = _map.Sim.Town;
        var fog = _map.Fog;
        float scale = _map.Zoom;
        int size = (int)Mathf.Clamp(scale * 0.36f, 11, 16);
        foreach (var site in town.Sites)
        {
            if (site.Known == SiteKnowledge.Unheard) continue;
            if (!_map.Geo.Pins.TryGetValue(site.Def.Id, out var pin)) continue;
            var sight = fog.SightOf(site.Def.Id, pin);
            float r = 0.42f + Math.Min(site.Def.Tier, 5) * 0.06f;
            var at = _map.ToScreen(pin + new Vector2(0, r + 0.25f));
            string name = Ui.Cap(site.Def.Name.StartsWith("the ") ? site.Def.Name[4..] : site.Def.Name);
            Text(at + new Vector2(0, size), name, Ui.Display, size, sight == Sight.Rumoured ? new Color("efe6d2", 0.7f) : new Color("f6eedb"));
            string sub = sight switch
            {
                Sight.Live => Watch.LiveOf(town, site) switch
                {
                    LiveSource.Scrying when Knowledge.ActiveScrying(town, site.Def.Id) is Scrying s => $"Scrying, {s.EndTick - town.Tick}h left",
                    LiveSource.Scout => "Scout watching",
                    LiveSource.Mirror => "Mirror",
                    LiveSource.Spire => "Spire sight",
                    LiveSource.Outpost => "Held",
                    _ => "In sight",
                },
                Sight.Scouted when fog.LastReport(site.Def.Id) is Report rep => town.Day - rep.Day > Fog.StaleDays ? $"Last seen day {rep.Day}" : $"Scouted day {rep.Day}",
                _ => site.Believed != "" ? $"Rumoured: {site.Believed}" : "Rumoured",
            };
            Text(at + new Vector2(0, size * 2 + 1), sub, Ui.Medium, size - 3, new Color("efe6d2", 0.62f));
            var bounty = town.Bounties.FirstOrDefault(b => b.SiteId == site.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken);
            if (bounty != null) Text(_map.ToScreen(pin - new Vector2(0, r + 0.12f)), $"{Math.Round(bounty.Reward)}g", Ui.Bold, size, Ui.Gold);
        }
    }

    private void Text(Vector2 at, string text, Font font, int size, Color color)
    {
        var w = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        var p = at - new Vector2(w / 2, 0);
        DrawStringOutline(font, p, text, HorizontalAlignment.Left, -1, size, 4, new Color(0.08f, 0.06f, 0.04f, 0.8f));
        DrawString(font, p, text, HorizontalAlignment.Left, -1, size, color);
    }
}
