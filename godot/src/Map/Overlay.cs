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
        DrawTerrain(town);
        DrawWilds(town);
        DrawSites(town);
        DrawFinds(town);
        DrawTrails(town);
        DrawParties(town);
        DrawWarband(town);
        DrawSightings(town);
        DrawSpots(town);
        DrawSick(town);
        DrawTraps(town);
        DrawDarkCompany(town);
        DrawEquips();
        DrawMarkers();
        DrawSelection(town);
    }

    /// <summary>
    /// The names of the ridges, mires and deep woods the roads bend round, spaced out along their arc
    /// like a map-maker's lettering, once the town has seen or been told of the ground.
    /// </summary>
    private void DrawTerrain(Town town)
    {
        if (map.Distance < 70) return;
        var ink = map.Strategic ? new Color("4a3418") : new Color(0.95f, 0.92f, 0.82f, 0.85f);
        foreach (var f in town.Terrain)
        {
            double mid = (f.Inner + f.Outer) / 2;
            double a = f.Bearing * Math.PI / 180;
            var c = Geo.FromDays(mid * Math.Sin(a), -mid * Math.Cos(a));
            float fogA = map.Fog.AlphaAt(c);
            if (fogA >= Fog.UnknownAlpha - 0.02f || !map.OnScreen(map.Gen.At(c, 2), out var at)) continue;
            float fade = 1 - Mathf.Clamp((fogA - Fog.SightAlpha) / (Fog.UnknownAlpha - Fog.SightAlpha), 0, 0.7f);
            Text(at, f.Name.ToUpperInvariant(), Ui.Medium, 10, ink with { A = ink.A * fade }, 3);
        }
    }

    /// <summary>A banner over a hero who has just put on something powerful: rises, holds, fades.</summary>
    private void DrawEquips()
    {
        var heroes = map.Crowd.Heroes;
        if (!map.Crowd.Visible) return;
        foreach (var e in heroes.Equips)
        {
            if (!heroes.Spots.TryGetValue(e.HeroId, out var p)) continue;
            float age = heroes.Now - e.Born;
            float rise = Mathf.Min(1, age / 0.4f);
            if (!map.OnScreen(p + new Vector3(0, 0.8f + 0.12f * rise, 0), out var at)) continue;
            float a = Mathf.Min(rise, Mathf.Clamp((6 - age) / 1.2f, 0, 1));
            const int Size = 15;
            var w = Ui.Bold.GetStringSize(e.Text, HorizontalAlignment.Left, -1, Size).X;
            var box = new Rect2(at.X - w / 2 - 12, at.Y - 22, w + 24, 26);
            DrawRect(box, new Color(InkC, 0.82f * a));
            DrawRect(box, new Color(Palette.Gold, a), false, 1.5f);
            DrawRect(new Rect2(box.Position.X, box.Position.Y, 4, box.Size.Y), new Color(e.Colour, a));
            DrawString(Ui.Bold, new Vector2(at.X - w / 2, at.Y - 4), e.Text, HorizontalAlignment.Left, -1, Size, new Color(e.Colour.Lerp(Cream, 0.55f), a));
        }
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
        var labels = new List<Rect2>();
        // Seen places claim their label space first, then those with a bounty, then rumours; a label that
        // would overlap one already drawn is left off (the disc stays), so crowded regions stay readable.
        var order = town.Sites
            .Where(s => s.Known != SiteKnowledge.Unheard && map.Geo.Pins.ContainsKey(s.Def.Id))
            .Select(s => (Site: s, Sight: fog.SightOf(s.Def.Id, map.Geo.Pins[s.Def.Id])))
            .OrderBy(x => x.Sight == Sight.Live ? 0 : x.Sight == Sight.Scouted ? 1 : 2)
            .ThenBy(x => town.Bounties.Any(b => b.SiteId == x.Site.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken) ? 0 : 1)
            .ThenBy(x => x.Site.Def.Id, StringComparer.Ordinal)
            .Select(x => x.Site)
            .ToList();
        foreach (var site in order)
        {
            var pin = map.Geo.Pins[site.Def.Id];
            var sight = fog.SightOf(site.Def.Id, pin);
            // Hearsay puts a place only roughly where it is; someone has to go and look.
            var spot = sight == Sight.Rumoured ? map.Geo.Rough(site) : pin;
            if (!map.OnScreen(map.Gen.At(spot, 3.5f), out var at)) continue;
            var bounty = town.Bounties.FirstOrDefault(b => b.SiteId == site.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken);
            float r = 13 + Math.Min(site.Def.Tier, 5) * 1.5f;
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
                int tier = Math.Min(site.Def.Tier, 5);
                for (int i = 0; i < tier; i++)
                    DrawCircle(at + new Vector2(-(tier - 1) * 3.5f + i * 7, r + 5), 2.4f, Palette.Gold);
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
            if (bounty != null) Text(at - new Vector2(0, r + 7), $"{Math.Round(bounty.Reward)}g", Ui.Bold, 14, Ui.Gold);
            string name = Ui.Cap(site.Def.Name.StartsWith("the ") ? site.Def.Name[4..] : site.Def.Name);
            float nw = Ui.Display.GetStringSize(name, HorizontalAlignment.Left, -1, size).X;
            var box = new Rect2(at.X - nw / 2 - 4, at.Y + r + 4, nw + 8, size * 2 + 8);
            if (labels.Any(l => l.Intersects(box))) continue;
            labels.Add(box);
            Text(at + new Vector2(0, r + size + 6), name, Ui.Display, size, sight == Sight.Rumoured ? new Color(Cream, 0.75f) : Cream);
            string sub = sight switch
            {
                Sight.Live => Watch.LiveOf(town, site) switch
                {
                    LiveSource.Scrying when Knowledge.ActiveScrying(town, site.Def.Id) is Scrying s => $"Scrying, {s.EndTick - town.Tick}h left",
                    LiveSource.Scout => "Scout watching",
                    LiveSource.Mirror => "Mirror",
                    LiveSource.Spire => "Spire sight",
                    LiveSource.Tower => "Watchtower sight",
                    LiveSource.Outpost => site.TowerDue != null ? "Tower rising" : "Held",
                    _ => "In sight",
                },
                Sight.Scouted when fog.LastReport(site.Def.Id) is Report rep => town.Day - rep.Day > Fog.StaleDays ? $"Last seen day {rep.Day}" : $"Scouted day {rep.Day}",
                _ => site.Found ? $"Found: {site.Believed}" : site.HeardFrom != "" ? $"Somewhere near here, says {site.HeardFrom}" : site.Believed != "" ? $"Rumoured: {site.Believed}" : "Rumoured",
            };
            if (!far || sight != Sight.Rumoured) Text(at + new Vector2(0, r + size * 2 + 7), sub, Ui.Medium, size - 3, new Color(Cream, 0.68f), 3);
        }
    }

    /// <summary>
    /// What explorers have sent word of besides lairs (those are sites): settlements met, and fonts, ruins,
    /// companies and caravans where they were found. Companies and caravans move on, so they show only
    /// while the explorers wait on the mayor about them.
    /// </summary>
    private void DrawFinds(Town town)
    {
        foreach (var st in town.Settlements)
        {
            if (!st.Met || !map.OnScreen(map.Gen.At(Geo.SettlementPin(town, st), 3), out var at)) continue;
            // An enemy's shadow over it: a dark pall round the pin, deeper as its grip tightens, and a violet rim.
            bool dark = Shadows.Shadowed(st);
            if (dark)
            {
                float depth = (float)Math.Clamp(st.Shadow!.Depth / 100, 0.2, 1);
                DrawCircle(at, 44 + 30 * depth, new Color(0.06f, 0.02f, 0.1f, 0.22f + 0.2f * depth));
                DrawCircle(at, 32 + 16 * depth, new Color(0.06f, 0.02f, 0.1f, 0.3f + 0.25f * depth));
                DrawCircle(at, 20 + 6 * depth, new Color(0.1f, 0.03f, 0.14f, 0.5f));
            }
            Disc(at, 12, st.Razed ? new Color("4a4440") : dark ? new Color("2e2438") : st.Allied ? new Color("3c6a4a") : new Color("4a5a6a"), dark ? new Color("9a6ad0") : st.Allied ? Palette.Gold : Cream, dark ? 3 : 2);
            DrawColoredPolygon([at + new Vector2(-7, 5), at + new Vector2(-7, -1), at + new Vector2(0, -7), at + new Vector2(7, -1), at + new Vector2(7, 5)], dark ? new Color("a898b8") : Cream);
            DrawRect(new Rect2(at.X - 1.5f, at.Y, 3, 5), InkC);
            Text(at + new Vector2(0, 28), st.Name, Ui.Display, 13, Cream);
            if (dark && Shadows.Over(town, st) is { } m)
            {
                string what = st.Shadow!.Form switch { "dread" => "In dread of", "tribute" => "Pays tribute to", "cult" => "Sings the hymns of", _ => "Ruled for" };
                Text(at + new Vector2(0, 43), $"{what} {m.Name}", Ui.Medium, 12, new Color("dcc4ff"), 4);
            }
        }
        foreach (var f in town.Finds)
        {
            if (!f.Known || town.Tick < f.ReportTick || f.Kind is "lair" or "settlement") continue;
            bool roaming = f.Kind is "company" or "caravan";
            if (roaming && f.State == FindState.Done) continue;
            if (!map.OnScreen(map.Gen.At(Geo.FromDays(f.X, f.Y), 3), out var at)) continue;
            var fill = f.Kind switch { "font" => new Color("4a3a7a"), "ruin" => new Color("5a5246"), "caravan" => new Color("6a5a2a"), _ => new Color("3a4a5a") };
            bool waiting = f.State == FindState.Open;
            Disc(at, 11, fill, waiting ? Palette.Gold : Cream, waiting ? 3 : 2);
            switch (f.Kind)
            {
                case "font":
                    DrawColoredPolygon([at + new Vector2(0, -8), at + new Vector2(5, 0), at + new Vector2(0, 8), at + new Vector2(-5, 0)], new Color("d8c8ff"));
                    break;
                case "ruin":
                    Glyph(at, "ruin", Cream);
                    break;
                default:
                    Banner(at + new Vector2(-1, 8), f.Kind == "caravan" ? new Color("d9a021") : new Color("8fb0d0"), Cream, 1);
                    break;
            }
            Text(at + new Vector2(0, 26), Explore.Title(town, f), Ui.Display, 12, new Color(Cream, 0.9f));
            if (f.Chapter > 0 && Explore.Def(town, f).Chapters.Count is int n and > 0)
                Text(at + new Vector2(0, 39), f.Chapter >= n ? "Fully explored" : $"Investigated {f.Chapter} of {n}", Ui.Medium, 10, new Color(Cream, 0.7f), 3);
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
            if (Geo.PartyAt(town, p, map.HourFraction) is not var (pos, pace)) continue;
            float fogA = map.Fog.AlphaAt(pos);
            if (fogA >= Fog.SightAlpha || !map.OnScreen(map.Gen.At(pos, 1), out var at)) continue;
            float fade = 1 - Mathf.Clamp(fogA / Fog.SightAlpha, 0, 1);
            Banner(at - new Vector2(0, 14), Palette.Gold, InkC, fade);
            // A camp fire by night, a thin smoke at the noon rest.
            if (pace == Pace.Camped)
            {
                DrawCircle(at + new Vector2(0, 2), 11, new Color(1f, 0.6f, 0.2f, 0.18f * fade));
                DrawCircle(at + new Vector2(0, 2), 6, new Color(1f, 0.6f, 0.2f, 0.3f * fade));
                DrawCircle(at + new Vector2(0, 2), 3.2f, new Color(1f, 0.75f, 0.3f, 0.95f * fade));
            }
            else if (pace == Pace.Resting) DrawCircle(at + new Vector2(0, 2), 2.2f, new Color(0.85f, 0.8f, 0.7f, 0.6f * fade));
            if (map.Distance > 90) Text(at + new Vector2(0, 6), p.Name, Ui.Medium, 11, new Color(Cream, fade), 3);
        }
        foreach (var s in town.Surveys)
        {
            if (!Explore.Out(s)) continue;
            var (pos, moving) = Geo.SurveyAt(town, s, map.HourFraction);
            float fogA = map.Fog.AlphaAt(pos);
            if (fogA >= Fog.SightAlpha || !map.OnScreen(map.Gen.At(pos, 1), out var at)) continue;
            float fade = 1 - Mathf.Clamp(fogA / Fog.SightAlpha, 0, 1);
            Banner(at - new Vector2(0, 14), new Color("6f9a5a"), InkC, fade);
            if (!moving) DrawCircle(at + new Vector2(0, 2), 3.2f, new Color(1f, 0.75f, 0.3f, 0.95f * fade));
            if (map.Distance > 90) Text(at + new Vector2(0, 6), s.Name, Ui.Medium, 11, new Color(Cream, fade), 3);
        }
        foreach (var s in town.Scouts)
        {
            if (!s.Out || !map.Geo.Pins.TryGetValue(s.SiteId, out var pin)) continue;
            var pos = map.Crowd.ScoutPos(town, s, pin);
            if (map.Fog.AlphaAt(pos) >= Fog.SightAlpha || !map.OnScreen(map.Gen.At(pos, 1), out var at)) continue;
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

    private static readonly Color SightRed = new("c0392b");

    /// <summary>Where a sighting puts the force on the map, and the way it was heading.</summary>
    public static (Vector2 Pos, Vector2 Heading) SightingAt(Town town, Geo geo, Sighting g)
    {
        var target = g.TargetId is int bid && town.Buildings.FirstOrDefault(b => b.Id == bid) is { } mark
            ? Geo.LotPos(town.Lot(mark.LotId))
            : g.Section is int sec
                ? new Vector2(Geo.Margin, Geo.Margin) + new Vector2((float)Garrison.WallPoint(town, sec).X, (float)Garrison.WallPoint(town, sec).Y)
                : Geo.Center;
        var site = g.FromSite != null ? town.Sites.FirstOrDefault(x => x.Def.Id == g.FromSite) : null;
        var from = site != null && geo.Pins.ContainsKey(site.Def.Id) ? geo.Rough(site) : Geo.Center + (target - Geo.Center).Normalized() * Geo.TilesPerDay * 2;
        float p = (float)g.Progress;
        return g.Kind == Sightings.Flight ? (target + (from - target) * p, from) : (from + (target - from) * p, target);
    }

    /// <summary>
    /// Enemies someone has seen and reported: a red pin where they were seen, an arrow the way they were heading, and who
    /// saw them and when. It is news, not a live view, so it shows through the fog and fades as it goes stale.
    /// </summary>
    private void DrawSightings(Town town)
    {
        foreach (var g in town.Sightings.OrderBy(g => g.Id))
        {
            var (pos, heading) = SightingAt(town, map.Geo, g);
            if (!map.OnScreen(map.Gen.At(pos, 2), out var at)) continue;
            float age = Mathf.Clamp((town.Tick + map.HourFraction - g.Tick) / Mathf.Max(1, g.UntilTick - g.Tick), 0, 1);
            float a = 1 - age * 0.6f;
            if (map.OnScreen(map.Gen.At(pos + (heading - pos).LimitLength(14), 2), out var ahead))
            {
                var dir = (ahead - at).Normalized();
                for (float d = 14; d < (ahead - at).Length() - 6; d += 9) DrawLine(at + dir * d, at + dir * Mathf.Min(d + 5, (ahead - at).Length() - 6), new Color(SightRed, 0.85f * a), 2.5f, true);
                var tip = ahead;
                var side = new Vector2(-dir.Y, dir.X);
                DrawColoredPolygon([tip, tip - dir * 9 + side * 5, tip - dir * 9 - side * 5], new Color(SightRed, 0.85f * a));
            }
            Disc(at - new Vector2(0, 18), 10, new Color(SightRed, a), new Color(Cream, a), 2);
            MarkerGlyph(this, MarkerKind.Danger, at - new Vector2(0, 18), 0.9f, new Color(Cream, a));
            if (map.Distance < 25) continue;
            string label = $"{Ui.Cap(Sightings.What(g))} · {g.Reporter.TrimEnd(',')}, {Sightings.Ago(town, g)}";
            float w = Ui.Medium.GetStringSize(label, HorizontalAlignment.Left, -1, 12).X;
            var box = new Rect2(at.X - w / 2 - 6, at.Y + 2, w + 12, 18);
            DrawStyleBox(LabelBox(SightRed), box);
            DrawString(Ui.Medium, new Vector2(box.Position.X + 6, box.Position.Y + 13), label, HorizontalAlignment.Left, -1, 12, new Color(Cream, a));
        }
    }

    public static Color MarkerColor(MarkerKind k) => k switch
    {
        MarkerKind.Rally => new Color("d07a2c"),
        MarkerKind.Attack => new Color("c0392b"),
        MarkerKind.Defend => new Color("3a6ab0"),
        MarkerKind.Scout => new Color("4f8a4a"),
        MarkerKind.Danger => new Color("d9a021"),
        MarkerKind.Claim => new Color("7d55a8"),
        _ => new Color("8a7a66"),
    };

    public static string MarkerName(MarkerKind k) => k switch
    {
        MarkerKind.Rally => "Rally point",
        MarkerKind.Attack => "Attack",
        MarkerKind.Defend => "Defend",
        MarkerKind.Scout => "Scout here",
        MarkerKind.Danger => "Danger",
        MarkerKind.Claim => "Claim",
        _ => "Note",
    };

    /// <summary>The kinds in the order the palette shows them.</summary>
    public static readonly MarkerKind[] MarkerKinds = [MarkerKind.Rally, MarkerKind.Attack, MarkerKind.Defend, MarkerKind.Scout, MarkerKind.Danger, MarkerKind.Claim, MarkerKind.Note];

    /// <summary>A marker's pictogram, drawn with shapes so it looks the same on every system; s is about 1 for a 22 px disc.</summary>
    public static void MarkerGlyph(CanvasItem ci, MarkerKind k, Vector2 c, float s, Color col)
    {
        Vector2 P(float x, float y) => c + new Vector2(x, y) * s;
        switch (k)
        {
            case MarkerKind.Rally:
                ci.DrawRect(new Rect2(P(-4.5f, -7), new Vector2(1.8f, 14) * s), col);
                ci.DrawColoredPolygon([P(-2.7f, -7), P(6, -4.5f), P(-2.7f, -1.5f)], col);
                break;
            case MarkerKind.Attack:
                ci.DrawLine(P(-6, -6), P(6, 6), col, 2 * s, true);
                ci.DrawLine(P(6, -6), P(-6, 6), col, 2 * s, true);
                ci.DrawLine(P(-6, 1.5f), P(-1.5f, 6), col, 2 * s, true);
                ci.DrawLine(P(6, 1.5f), P(1.5f, 6), col, 2 * s, true);
                break;
            case MarkerKind.Defend:
                ci.DrawColoredPolygon([P(-6, -6), P(6, -6), P(6, -1), P(0, 7), P(-6, -1)], col);
                ci.DrawLine(P(0, -4.5f), P(0, 4.5f), MarkerColor(k), 1.5f * s);
                break;
            case MarkerKind.Scout:
                var eye = new Vector2[16];
                for (int i = 0; i < 16; i++)
                {
                    float a = i / 16f * Mathf.Tau;
                    eye[i] = P(Mathf.Cos(a) * 7, Mathf.Sin(a) * 4);
                }
                ci.DrawColoredPolygon(eye, col);
                ci.DrawCircle(c, 2.6f * s, MarkerColor(k).Darkened(0.4f));
                break;
            case MarkerKind.Danger:
                ci.DrawColoredPolygon([P(-1.6f, -7), P(1.6f, -7), P(1, 2), P(-1, 2)], col);
                ci.DrawCircle(P(0, 5), 1.7f * s, col);
                break;
            case MarkerKind.Claim:
                ci.DrawColoredPolygon([P(-6.5f, 5), P(-6.5f, -4), P(-3.2f, 0), P(0, -6), P(3.2f, 0), P(6.5f, -4), P(6.5f, 5)], col);
                break;
            default:
                for (int i = 0; i < 3; i++) ci.DrawRect(new Rect2(P(-5.5f, -5 + i * 4), new Vector2(i == 2 ? 7 : 11, 1.8f) * s), col);
                break;
        }
    }

    public static readonly Color ExploreInk = new("4f9fd0");
    public static readonly Color PatrolInk = new("d0763a");

    /// <summary>Where a cell's corner (days from the town) lands on screen, lifted a little off the ground.</summary>
    private bool Corner(Town town, double x, double y, out Vector2 at) => map.OnScreen(map.Gen.At(Geo.FromDays(x, y), 1.5f), out at);

    /// <summary>One cell as a quad on screen, or null when part of it is off the view.</summary>
    private Vector2[]? Quad(Town town, int cx, int cy)
    {
        double c = town.Content.Wilds.Cell;
        double x0 = cx * c, y0 = cy * c, x1 = x0 + c, y1 = y0 + c;
        if (!Corner(town, x0, y0, out var a) || !Corner(town, x1, y0, out var b) || !Corner(town, x1, y1, out var d) || !Corner(town, x0, y1, out var e)) return null;
        var r = GetViewportRect().Grow(200);
        if (!r.HasPoint(a) && !r.HasPoint(b) && !r.HasPoint(d) && !r.HasPoint(e)) return null;
        return [a, b, d, e];
    }

    /// <summary>
    /// Painted zones: explore ground in blue (walked cells paler), patrols in rust (grey while unpaid), each edged where it
    /// ends, with a tag at its middle saying how it stands; and the stroke under the brush.
    /// </summary>
    private void DrawWilds(Town town)
    {
        foreach (var z in town.Wilds.OrderBy(z => z.Id))
        {
            bool explore = z.Kind == Wilds.Explore;
            var ink = explore ? ExploreInk : z.Unpaid ? new Color("8a8478") : PatrolInk;
            var cells = new HashSet<(int, int)>(z.Cells.Select(c => (c.X, c.Y)));
            foreach (var c in z.Cells)
            {
                if (Quad(town, c.X, c.Y) is not { } q) continue;
                DrawColoredPolygon(q, new Color(ink, explore && c.Seen && !c.Pending ? 0.08f : 0.24f));
                // Edges only where the zone ends, so it reads as one painted patch.
                (int dx, int dy, int i)[] sides = [(0, -1, 0), (1, 0, 1), (0, 1, 2), (-1, 0, 3)];
                foreach (var (dx, dy, i) in sides)
                    if (!cells.Contains((c.X + dx, c.Y + dy)))
                    {
                        if (explore) DrawDashedLine(q[i], q[(i + 1) % 4], new Color(ink, 0.95f), 2.5f, 8);
                        else DrawLine(q[i], q[(i + 1) % 4], new Color(ink, 0.95f), 3);
                    }
            }
            var (mx, my) = Wilds.Middle(town, z);
            if (!map.OnScreen(map.Gen.At(Geo.FromDays(mx, my), 2), out var at)) continue;
            Disc(at, 11, new Color("2a241c"), ink, 2.5f);
            DrawTextureRect(Ui.Icon(explore ? "act-explore" : "order-patrol"), new Rect2(at - new Vector2(8, 8), new Vector2(16, 16)), false);
            Text(at + new Vector2(0, 26), WildsTag(town, z), Ui.Bold, 13, ink.Lerp(Cream, 0.35f));
        }
        if (map.Brush is string brush && map.BrushCells.Count > 0)
        {
            var ink = map.BrushErasing ? new Color("c0392b") : brush == Wilds.Explore ? ExploreInk : PatrolInk;
            foreach (var (x, y) in map.BrushCells)
                if (Quad(town, x, y) is { } q)
                {
                    bool blocked = !map.BrushErasing && Wilds.CellBlocked(town, x, y) != null;
                    DrawColoredPolygon(q, new Color(blocked ? new Color("c0392b") : ink, 0.38f));
                }
        }
    }

    /// <summary>A zone's short tag: what is left to walk and who is on it, or what the patrol costs and who rides it.</summary>
    public static string WildsTag(Town town, WildZone z)
    {
        if (z.Kind == Wilds.Explore)
        {
            int left = Wilds.Unseen(z);
            var s = town.Surveys.FirstOrDefault(x => x.ZoneId == z.Id && Explore.Out(x));
            if (s != null) return $"The {s.Name} · {left} left";
            var c = town.Commissions.FirstOrDefault(x => x.ZoneId == z.Id && x.Status == BountyStatus.Open);
            if (c != null) return $"{left} left · {Math.Round(c.Reward)}g offered";
            if (left == 0 && Wilds.Untold(z) > 0) return "Walked · awaiting their report";
            return left == 0 ? "Walked" : $"{left} left · needs {Wilds.TripReward(town, z):0}g";
        }
        if (z.Unpaid) return $"Unpaid · {Wilds.Wage(town, z):0.#}g a day";
        var r = Wilds.Riders(town, z);
        return r != null ? $"{r.Name} · {Wilds.Wage(town, z):0.#}g a day" : $"Patrol · {Wilds.Wage(town, z):0.#}g a day";
    }

    /// <summary>Spots the mayor marked for a company to look at: a dashed gold ring round the ground, a spyglass pin, and who is on it.</summary>
    private void DrawSpots(Town town)
    {
        foreach (var c in Explore.Spots(town))
        {
            var p = Geo.FromDays(c.SpotX!.Value, c.SpotY!.Value);
            if (!map.OnScreen(map.Gen.At(p, 3), out var at)) continue;
            // The ring is as wide as the ground they will cast about, measured on screen.
            float reach = Geo.TilesPerDay * (float)town.Content.Explore.Commission.SpotRadius;
            float r = map.OnScreen(map.Gen.At(p + new Vector2(reach, 0), 3), out var edge) ? Mathf.Clamp(at.DistanceTo(edge), 14, 400) : 30;
            for (int i = 0; i < 24; i += 2) DrawArc(at, r, i * Mathf.Tau / 24, (i + 1) * Mathf.Tau / 24, 4, new Color(Palette.Gold, 0.9f), 2.5f, true);
            DrawColoredPolygon([at, at + new Vector2(-8, -16), at + new Vector2(8, -16)], Palette.Gold.Darkened(0.3f));
            Disc(at - new Vector2(0, 22), 11, new Color("3a3226"), Palette.Gold, 2.5f);
            MarkerGlyph(this, MarkerKind.Scout, at - new Vector2(0, 22), 1, Cream);
            var s = c.SurveyId is int sid ? Explore.Survey(town, sid) : null;
            string label = s == null ? $"Look here · {Math.Round(c.Reward)}g" : s.Phase == SurveyPhase.Outbound ? $"The {s.Name}, on the way" : $"The {s.Name}, looking";
            Text(at + new Vector2(0, 18), label, Ui.Bold, 13, Palette.Gold);
        }
    }

    /// <summary>A sick house: a cloth on its door, as the townsfolk hang one.</summary>
    private void DrawSick(Town town)
    {
        if (!Sickness.Outbreak(town)) return;
        foreach (var b in Sickness.Sick(town))
        {
            if (!map.OnScreen(map.Gen.At(Geo.LotPos(town.Lot(b.LotId)), 3), out var at)) continue;
            Disc(at - new Vector2(0, 14), 10, new Color("3a4a26"), new Color("9cb85a"), 2);
            DrawTextureRect(Ui.Icon("sick-house"), new Rect2(at - new Vector2(8, 22), new Vector2(16, 16)), false);
        }
    }

    /// <summary>Traps before the wall: a small mark on each stretch that has them, red when spent.</summary>
    private void DrawTraps(Town town)
    {
        if (town.Traps.Laid.Count == 0) return;
        foreach (var g in town.Traps.Laid.GroupBy(x => x.Section).OrderBy(g => g.Key))
        {
            var w = Garrison.WallPoint(town, g.Key);
            var pos = new Vector2(Geo.Margin, Geo.Margin) + new Vector2((float)w.X, (float)w.Y);
            if (!map.OnScreen(map.Gen.At(pos, 1), out var at)) continue;
            int i = 0;
            foreach (var trap in g.OrderBy(x => x.Id))
            {
                if (Traps.Def(town, trap.Kind) is not { } d) continue;
                var c = at + new Vector2((i++ - (g.Count() - 1) / 2f) * 20, 18);
                Disc(c, 9, new Color("2a241c"), trap.Spent ? new Color("c0392b") : new Color("b3ada4"), 2);
                DrawTextureRect(Ui.Icon(d.Icon), new Rect2(c - new Vector2(7, 7), new Vector2(14, 14)), false, trap.Spent ? new Color(1, 1, 1, 0.4f) : Colors.White);
            }
        }
    }

    /// <summary>The big bad's company on the road: a black banner between its master's seat and the hoard it rides for.</summary>
    private void DrawDarkCompany(Town town)
    {
        var c = town.DarkCompany;
        if (DarkCompany.Target(town) is not { } site || Saga.BigBad(town) is not { } big) return;
        if (!map.Geo.Pins.TryGetValue(site.Def.Id, out var to)) return;
        var from = big.SeatId != "" && map.Geo.Pins.TryGetValue(big.SeatId, out var seat) ? seat : to + new Vector2(40, -40);
        float total = Math.Max(1, c.ArriveTick - c.SetOutTick);
        float t = Mathf.Clamp((town.Tick + map.HourFraction - c.SetOutTick) / total, 0, 1);
        var pos = from + (to - from) * t;
        // Word on the roads: it shows through the fog.
        if (!map.OnScreen(map.Gen.At(pos, 2), out var at)) return;
        Banner(at - new Vector2(0, 16), new Color("4a2a5a"), InkC, 1);
        Text(at + new Vector2(0, 8), Ui.Cap(c.Name), Ui.Bold, 13, new Color("d8b8ff"));
    }

    /// <summary>The player's markers: pins with a pictogram and an optional name, at one size on both views.</summary>
    private void DrawMarkers()
    {
        var labels = new List<Rect2>();
        // The marker in hand or under the cursor draws last, so it sits on top.
        foreach (var m in map.Markers.OrderBy(m => m == map.HoverMarker ? 1 : 0))
        {
            if (!map.OnScreen(map.Gen.At(m.P), out var at)) continue;
            var c = MarkerColor(m.Kind);
            bool hot = m == map.HoverMarker;
            float r = hot ? 13 : 11;
            DrawCircle(at, 2.5f, new Color(0, 0, 0, 0.45f));
            DrawColoredPolygon([at, at + new Vector2(-r * 0.75f, -16), at + new Vector2(r * 0.75f, -16)], c.Darkened(0.3f));
            Disc(at - new Vector2(0, 22), r, c, hot ? Colors.White : Cream, hot ? 2.5f : 2);
            MarkerGlyph(this, m.Kind, at - new Vector2(0, 22), r / 11, Cream);
            if (m.Label == "") continue;
            // Names keep out of each other's way like site names: the later one is left off.
            float w = Ui.Bold.GetStringSize(m.Label, HorizontalAlignment.Left, -1, 13).X;
            var box = new Rect2(at.X - w / 2 - 6, at.Y + 3, w + 12, 19);
            if (!hot && labels.Any(l => l.Intersects(box))) continue;
            labels.Add(box);
            DrawStyleBox(LabelBox(c), box);
            DrawString(Ui.Bold, new Vector2(box.Position.X + 6, box.Position.Y + 14), m.Label, HorizontalAlignment.Left, -1, 13, Cream);
        }
    }

    private static readonly Dictionary<Color, StyleBoxFlat> LabelBoxes = [];

    private static StyleBoxFlat LabelBox(Color c)
    {
        if (!LabelBoxes.TryGetValue(c, out var box)) LabelBoxes[c] = box = Ui.Box(new Color(0.09f, 0.07f, 0.05f, 0.82f), c, 6, 1, 0, 0);
        return box;
    }

    // ---------- Trails ----------

    /// <summary>How many game hours of road a trail remembers, and how finely it is sampled.</summary>
    private const float TrailHours = 30, TrailStep = 0.5f;

    private readonly List<(Vector2 P, float A)> _trail = [];

    /// <summary>
    /// Tracks behind companies, scouts and the warband over the last day or so of their road. A track
    /// shows only where the town can see the ground now, thinning with age and fading out where it
    /// runs into the fog, so it never gives away where anyone went unseen.
    /// </summary>
    private void DrawTrails(Town town)
    {
        float now = town.Tick + map.HourFraction;
        bool strategic = map.Strategic;
        foreach (var p in town.Parties)
        {
            if (!map.Geo.Pins.ContainsKey(p.SiteId)) continue;
            if (p.Returned && p.ReturnTick + TrailHours < now) continue;
            float end = p.Returned ? p.ReturnTick : float.MaxValue;
            Trail(h => now + h < p.DepartTick || now + h > end ? null : Geo.PartyAt(town, p, map.HourFraction + h)?.Pos,
                strategic ? new Color("5a3a12") : new Color("ffd65a"));
        }
        foreach (var s in town.Surveys)
        {
            if (s.Phase is SurveyPhase.Engaged or SurveyPhase.Lost || (s.Phase == SurveyPhase.Home && s.ReturnTick + TrailHours < now)) continue;
            float end = s.Phase == SurveyPhase.Home ? s.ReturnTick : float.MaxValue;
            Trail(h => now + h < s.DepartTick || now + h > end ? null : Geo.SurveyAt(town, s, map.HourFraction + h).Pos,
                strategic ? new Color("2f4f2a") : new Color("c8f0a0"));
        }
        foreach (var s in town.Scouts)
        {
            if (!map.Geo.Pins.TryGetValue(s.SiteId, out var pin) || s.Lost) continue;
            if (s.Home && s.ReturnTick + TrailHours < now) continue;
            var dir = (pin - Geo.Center).Normalized();
            Trail(h =>
            {
                if (now + h < s.DepartTick || now + h > s.ReturnTick) return null;
                float leg = (float)Watch.ScoutLeg(town, s, map.HourFraction + h);
                return leg >= 1 ? pin - dir * 6 + new Vector2(dir.Y, -dir.X) * 2 : Geo.Center + (pin - Geo.Center) * leg;
            }, strategic ? new Color("2f4f2a") : new Color("b8f0a0"));
        }
        var en = town.Enemy;
        if (en.Stage == ThreatStage.Marching && en.ArrivalTick is int arrival)
        {
            float total = town.Content.Enemy.MarchDays * SimTime.HoursPerDay;
            var from = map.Geo.Pins.TryGetValue(town.Content.Enemy.Outpost.Id, out var camp) ? camp : Geo.Center + new Vector2(60, -60);
            Trail(h =>
            {
                float t = 1 - (arrival - now - h) / total;
                return t < 0 ? null : from + (Geo.Center - from) * Mathf.Min(t, 1) * 0.85f;
            }, strategic ? new Color("7a1a10") : new Color("ff7a60"));
        }
    }

    /// <summary>One trail: positions back through time (h hours ago is pos(-h)), drawn as a line of footsteps.</summary>
    private void Trail(Func<float, Vector2?> pos, Color color)
    {
        _trail.Clear();
        var fog = map.Fog;
        for (float h = 0; h <= TrailHours; h += TrailStep)
        {
            if (pos(-h) is not Vector2 w) break;
            if (_trail.Count > 0 && _trail[^1].P.DistanceSquaredTo(w) < 0.04f) continue;
            float seen = 1 - Mathf.Clamp(fog.AlphaAt(w) / Fog.SightAlpha, 0, 1);
            _trail.Add((w, seen * (1 - h / TrailHours)));
        }
        if (_trail.Count < 2) return;
        // Steps at an even spacing on screen, so the trail reads the same at any zoom.
        const float Spacing = 8;
        bool ink = map.Strategic;
        float carry = 0;
        Vector2? prev = null;
        float prevA = 0;
        int steps = 0;
        foreach (var (w, a) in _trail)
        {
            var cam = map.Camera;
            var w3 = map.Gen.At(w, 0.2f);
            if (cam.IsPositionBehind(w3))
            {
                prev = null;
                continue;
            }
            var sp = cam.UnprojectPosition(w3);
            if (prev is Vector2 q && (a > 0.01f || prevA > 0.01f))
            {
                float len = q.DistanceTo(sp);
                bool onScreen = new Rect2(-40, -40, Size.X + 80, Size.Y + 80).Intersects(new Rect2(q, Vector2.Zero).Expand(sp));
                if (onScreen)
                    for (float d = Spacing - carry; d < len && steps < 900; d += Spacing, steps++)
                    {
                        float k = d / len;
                        float alpha = Mathf.Lerp(prevA, a, k);
                        if (alpha < 0.03f) continue;
                        var at = q.Lerp(sp, k);
                        // Steps shrink as well as pale toward the fog, so the trail tapers into it.
                        float size = 0.55f + 0.45f * alpha;
                        // Gilded steps on the live map; inked dashes across the campaign map's parchment.
                        if (ink) DrawLine(at - (sp - q) / len * 2.5f * size, at + (sp - q) / len * 2.5f * size, color with { A = 0.85f * alpha }, 2.2f * size, true);
                        else
                        {
                            DrawCircle(at, 2.8f * size, new Color(0.08f, 0.06f, 0.04f, 0.6f * alpha));
                            DrawCircle(at, 1.9f * size, color with { A = alpha });
                        }
                    }
                carry = len < 1e-3f ? carry : (carry + len) % Spacing;
            }
            prev = sp;
            prevA = a;
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
            var corners = Geo.LotCorners(lot, 0.08f);
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
