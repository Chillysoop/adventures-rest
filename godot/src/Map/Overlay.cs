using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// Icons and names in screen space over the 3D map: site medallions, companies on the road,
/// scouts, the warband, the selection and the player's markers. They keep one size at every zoom,
/// so the map stays readable from the street to the whole realm.
/// </summary>
public partial class Overlay(MapView map) : Control
{
    private static readonly Color InkC = new("1d1712");
    private static readonly Color Cream = new("f6eedb");

    public override void _Draw() => MapView.Time(Paint);

    private void Paint()
    {
        if (map.Sim == null) return;
        var town = map.Sim.Town;
        DrawSites(town);
        DrawParties(town);
        DrawWarband(town);
        DrawMarkers();
        DrawSelection(town);
    }

    private void Text(Vector2 at, string text, Font font, int size, Color color, float outline = 4)
    {
        var w = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        var p = at - new Vector2(w / 2, 0);
        DrawStringOutline(font, p, text, HorizontalAlignment.Left, -1, size, (int)outline, new Color(0.08f, 0.06f, 0.04f, 0.8f));
        DrawString(font, p, text, HorizontalAlignment.Left, -1, size, color);
    }

    private void Disc(Vector2 c, float r, Color fill, Color ring, float width)
    {
        DrawCircle(c + new Vector2(1.5f, 2.5f), r + width * 0.5f, new Color(0, 0, 0, 0.35f));
        DrawCircle(c, r + width * 0.5f, ring);
        DrawCircle(c, r - width * 0.5f, fill);
    }

    private void DrawSites(Town town)
    {
        var fog = map.Fog;
        bool far = map.Distance > 260;
        foreach (var site in town.Sites)
        {
            // Nobody has so much as heard of it: nothing on the map.
            if (site.Known == SiteKnowledge.Unheard) continue;
            if (!map.Geo.Pins.TryGetValue(site.Def.Id, out var pin)) continue;
            if (!map.OnScreen(map.Gen.At(pin, 3.5f), out var at)) continue;
            var sight = fog.SightOf(site.Def.Id, pin);
            var bounty = town.Bounties.FirstOrDefault(b => b.SiteId == site.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken);
            float r = 13 + site.Def.Tier * 1.5f;
            int size = 14;
            if (sight == Sight.Rumoured)
            {
                // A name from tavern gossip: a dashed ring and a question mark, nothing more.
                DrawArc(at, r, 0, Mathf.Tau, 32, new Color(Cream, 0.6f), 2, true);
                Text(at + new Vector2(0, 6), "?", Ui.Display, 17, new Color(Cream, 0.8f));
                if (bounty != null) DrawArc(at, r + 5, 0, Mathf.Tau, 32, Palette.Gold, 3, true);
            }
            else
            {
                bool enemy = site.Def.Id == town.Content.Enemy.Outpost.Id;
                // A scouted site shows what the last report said; only a live view shows it now.
                bool cleared = sight == Sight.Live ? site.Cleared : fog.LastReport(site.Def.Id)?.Outcome == ExpeditionOutcome.Cleared;
                var fill = cleared ? new Color("6f8f5b") : enemy ? new Color("8e2a1f") : site.Def.Tier >= 3 ? new Color("4a2c3a") : site.Def.Tier == 2 ? new Color("5a3d2e") : new Color("5e5236");
                Disc(at, r, fill, cleared ? new Color("e8f0d8") : bounty != null ? Palette.Gold : InkC, bounty != null ? 4 : 2.5f);
                var glyph = cleared ? new Color("e8f0d8") : new Color("efe6d2");
                if (cleared)
                {
                    DrawRect(new Rect2(at.X - 1.5f, at.Y - 8, 2.5f, 16), glyph);
                    DrawColoredPolygon([at + new Vector2(1, -8), at + new Vector2(9, -5), at + new Vector2(1, -2)], glyph);
                }
                else Glyph(at, TownView.LandmarkKind(town, site.Def), glyph);
                for (int i = 0; i < site.Def.Tier; i++)
                    DrawCircle(at + new Vector2(-(site.Def.Tier - 1) * 3.5f + i * 7, r + 5), 2.4f, Palette.Gold);
                switch (Watch.LiveOf(town, site))
                {
                    case LiveSource.Scrying:
                        DrawArc(at, r + 7 + Mathf.Sin(Time.GetTicksMsec() / 400f) * 1.5f, 0, Mathf.Tau, 40, new Color("b48cff", 0.85f), 2, true);
                        break;
                    case LiveSource.Mirror:
                        DrawArc(at, r + 7, 0, Mathf.Tau, 40, new Color("cfe3f0", 0.8f), 2, true);
                        break;
                    case LiveSource.Scout:
                        DrawArc(at, r + 7, 0, Mathf.Tau, 40, new Color("8cc084", 0.8f), 2, true);
                        break;
                    case LiveSource.Outpost:
                        // The town's banner over a held site.
                        DrawArc(at, r + 7, 0, Mathf.Tau, 40, new Color(Palette.Gold, 0.85f), 2.5f, true);
                        DrawColoredPolygon([at + new Vector2(r - 2, -r - 12), at + new Vector2(r + 10, -r - 8), at + new Vector2(r - 2, -r - 4)], Palette.Gold);
                        break;
                }
                if (Watch.MirrorAt(town, site.Def.Id) != null)
                {
                    var m = at + new Vector2(r + 8, -r * 0.4f);
                    DrawColoredPolygon([m + new Vector2(0, -7), m + new Vector2(5, 0), m + new Vector2(0, 7), m + new Vector2(-5, 0)], new Color("dfe8ee"));
                }
            }
            string name = Ui.Cap(site.Def.Name.StartsWith("the ") ? site.Def.Name[4..] : site.Def.Name);
            Text(at + new Vector2(0, r + size + 6), name, Ui.Display, size, sight == Sight.Rumoured ? new Color(Cream, 0.75f) : Cream);
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
            if (!far || sight != Sight.Rumoured) Text(at + new Vector2(0, r + size * 2 + 7), sub, Ui.Medium, size - 3, new Color(Cream, 0.68f), 3);
            if (bounty != null) Text(at - new Vector2(0, r + 7), $"{Math.Round(bounty.Reward)}g", Ui.Bold, 14, Ui.Gold);
        }
    }

    /// <summary>A small pictogram of what kind of place a site is.</summary>
    private void Glyph(Vector2 at, string kind, Color c)
    {
        switch (kind)
        {
            case "cave":
                DrawColoredPolygon([at + new Vector2(-9, 6), at + new Vector2(-3, -7), at + new Vector2(3, -4), at + new Vector2(9, 6)], c);
                DrawColoredPolygon([at + new Vector2(-3, 6), at + new Vector2(0, 0), at + new Vector2(3, 6)], InkC);
                break;
            case "grove":
                DrawColoredPolygon([at + new Vector2(0, -9), at + new Vector2(7, 3), at + new Vector2(-7, 3)], c);
                DrawRect(new Rect2(at.X - 1.5f, at.Y + 3, 3, 5), c);
                break;
            case "ruin":
                DrawRect(new Rect2(at.X - 6, at.Y - 4, 12, 11), c);
                for (int i = 0; i < 3; i++) DrawRect(new Rect2(at.X - 6 + i * 4.5f, at.Y - 8, 3, 4), c);
                DrawRect(new Rect2(at.X - 1.5f, at.Y + 2, 3, 5), InkC);
                break;
            case "temple":
                DrawColoredPolygon([at + new Vector2(-9, -3), at + new Vector2(0, -9), at + new Vector2(9, -3)], c);
                for (int i = 0; i < 3; i++) DrawRect(new Rect2(at.X - 7 + i * 6, at.Y - 2, 2.5f, 8), c);
                break;
            case "camp":
                DrawColoredPolygon([at + new Vector2(-9, 7), at + new Vector2(0, -8), at + new Vector2(9, 7)], c);
                DrawColoredPolygon([at + new Vector2(-2.5f, 7), at + new Vector2(0, 1), at + new Vector2(2.5f, 7)], InkC);
                break;
            default:
                // A skull for the lair of something large.
                DrawCircle(at - new Vector2(0, 1.5f), 6.5f, c);
                DrawRect(new Rect2(at.X - 4, at.Y + 2, 8, 5), c);
                DrawCircle(at + new Vector2(-2.5f, -1.5f), 1.8f, InkC);
                DrawCircle(at + new Vector2(2.5f, -1.5f), 1.8f, InkC);
                break;
        }
    }

    private void Banner(Vector2 at, Color flag, Color pole, float alpha)
    {
        DrawRect(new Rect2(at.X - 1, at.Y - 22, 2.5f, 24), pole with { A = alpha });
        DrawColoredPolygon([at + new Vector2(1.5f, -22), at + new Vector2(15, -18), at + new Vector2(1.5f, -12)], flag with { A = alpha });
    }

    /// <summary>
    /// A company marches out under its banner and is lost to the fog past the town's light; it is
    /// seen again only on its way home, or under a scrying.
    /// </summary>
    private void DrawParties(Town town)
    {
        foreach (var p in town.Parties)
        {
            if (p.Returned || !map.Geo.Pins.ContainsKey(p.SiteId)) continue;
            var pos = map.Crowd.PartyPos(town, p);
            float fogA = map.Fog.AlphaAt(pos);
            if (fogA > 0.45f || !map.OnScreen(map.Gen.At(pos, 1), out var at)) continue;
            float fade = 1 - Mathf.Clamp(fogA / 0.45f, 0, 1);
            Banner(at - new Vector2(0, 14), Palette.Gold, InkC, fade);
            if (map.Distance > 90) Text(at + new Vector2(0, 6), p.Name, Ui.Medium, 11, new Color(Cream, fade), 3);
        }
        foreach (var s in town.Scouts)
        {
            if (!s.Out || !map.Geo.Pins.TryGetValue(s.SiteId, out var pin)) continue;
            var pos = map.Crowd.ScoutPos(town, s, pin);
            if (map.Fog.AlphaAt(pos) > 0.45f || !map.OnScreen(map.Gen.At(pos, 1), out var at)) continue;
            if (map.Distance < 90) continue;
            DrawColoredPolygon([at + new Vector2(-6, 4), at + new Vector2(0, -8), at + new Vector2(6, 4)], new Color("3f5f3a"));
            DrawCircle(at + new Vector2(0, -6), 3, new Color("e0c0a0"));
        }
    }

    private void DrawWarband(Town town)
    {
        var en = town.Enemy;
        if (en.Stage != ThreatStage.Marching || en.ArrivalTick is not int arrival) return;
        float total = town.Content.Enemy.MarchDays * SimTime.HoursPerDay;
        float t = Mathf.Clamp(1 - (arrival - town.Tick - map.HourFraction) / total, 0, 1);
        var from = map.Geo.Pins.TryGetValue(town.Content.Enemy.Outpost.Id, out var camp) ? camp : Geo.Center + new Vector2(60, -60);
        var pos = from + (Geo.Center - from) * t * 0.85f;
        if (!map.Fog.IsLive(pos) || !map.OnScreen(map.Gen.At(pos, 2), out var at)) return;
        Banner(at - new Vector2(0, 16), new Color("c0392b"), InkC, 1);
        Text(at + new Vector2(0, 8), town.Content.Enemy.Name, Ui.Bold, 13, new Color("ffb0a0"));
    }

    public static Color MarkerColor(MarkerKind k) => k switch
    {
        MarkerKind.Attack => new Color("c0392b"),
        MarkerKind.Defend => new Color("3a6ab0"),
        MarkerKind.Scout => new Color("4f8a4a"),
        MarkerKind.Danger => new Color("e0a020"),
        _ => new Color("8a7a66"),
    };

    public static string MarkerGlyph(MarkerKind k) => k switch
    {
        MarkerKind.Attack => "⚔",
        MarkerKind.Defend => "⛨",
        MarkerKind.Scout => "◉",
        MarkerKind.Danger => "!",
        _ => "✎",
    };

    /// <summary>The player's markers: pins with a glyph, at one size on both views.</summary>
    private void DrawMarkers()
    {
        foreach (var m in map.Markers)
        {
            if (!map.OnScreen(map.Gen.At(m.P), out var at)) continue;
            var c = MarkerColor(m.Kind);
            DrawColoredPolygon([at, at + new Vector2(-9, -16), at + new Vector2(9, -16)], c.Darkened(0.3f));
            Disc(at - new Vector2(0, 22), 11, c, Cream, 2);
            Text(at - new Vector2(0, 16), MarkerGlyph(m.Kind), Ui.Bold, 13, Cream, 2);
        }
    }

    private void DrawSelection(Town town)
    {
        var sel = map.Selection;
        if (sel == null) return;
        var c = new Color("fff6d8");
        if (sel.Kind == "building")
        {
            var lot = town.Lot(town.Building(sel.Id).LotId);
            var p = Geo.LotPos(lot);
            Vector2[] corners = [p, p + new Vector2(1, 0), p + new Vector2(1, 1), p + new Vector2(0, 1)];
            var pts = corners.Select(q => map.ToScreen(q)).ToList();
            pts.Add(pts[0]);
            DrawPolyline(pts.ToArray(), c, 2.5f, true);
        }
        else if (sel.Kind == "agent")
        {
            if (map.Agents.Sprites.TryGetValue(sel.Id, out var s) && s.Visible)
            {
                var at = map.ToScreen(s.P, 0.15f);
                DrawArc(at, 12, 0, Mathf.Tau, 24, c, 2, true);
            }
        }
        else if (map.Geo.Pins.TryGetValue(sel.SiteId, out var pin) && map.OnScreen(map.Gen.At(pin, 3.5f), out var at)) DrawArc(at, 26, 0, Mathf.Tau, 40, c, 2.5f, true);
    }
}
