using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

public partial class Main
{
    /// <summary>Creatures in a gallery page.</summary>
    private const int GalleryPage = 24;

    /// <summary>
    /// --bestiary: a page of creatures on open ground for shots. "3" is the third page of every creature by plan,
    /// "dragon" or "dragon:2" a plan's pages, and a list of ids those creatures.
    /// </summary>
    private void StageBestiary(string spec)
    {
        // Several pages in one run: "quadruped;dragon;5" shoots each to out-1.png, out-2.png...
        if (spec.Contains(';') && _opts.Screenshot is string shot)
        {
            var pages = spec.Split(';');
            int shotNo = 0;
            string Name(int i) => shot.Replace(".png", $"-{pages[i].Replace(':', '_').Replace(',', '_')}.png");
            StageBestiary(pages[0]);
            _opts.Screenshot = Name(0);
            _opts.NextShot = () =>
            {
                if (++shotNo >= pages.Length) return null;
                StageBestiary(pages[shotNo]);
                return Name(shotNo);
            };
            return;
        }
        var c = _sim.Town.Content;
        var all = Bestiary.All(c).Select(m => Bestiary.Of(c, m.Id)).OrderBy(a => a.Plan, StringComparer.Ordinal).ThenBy(a => a.Scale).ThenBy(a => a.Id, StringComparer.Ordinal).ToList();
        List<Anatomy> pick;
        var parts = spec.Split(':');
        if (int.TryParse(parts[0], out int page)) pick = all.Skip((page - 1) * GalleryPage).Take(GalleryPage).ToList();
        else if (all.Any(a => a.Plan == parts[0]))
        {
            int p = parts.Length > 1 && int.TryParse(parts[1], out int pp) ? pp : 1;
            pick = all.Where(a => a.Plan == parts[0]).Skip((p - 1) * GalleryPage).Take(GalleryPage).ToList();
        }
        else pick = spec.Split(',').Select(id => Bestiary.Of(c, id)).ToList();
        var at = (_opts.Look ??= (OpenGround(_sim.Town) ?? Map.Geo.Center) + new Vector2(0, 8));
        var (_, extent) = Map.Crowd.GalleryLayout(pick.Select(a => (float)a.Scale).ToList());
        _map.Crowd.Gallery = pick.Select(a => a.Id).ToList();
        _map.Crowd.GalleryAt = at;
        _map.LookAt(at, _opts.Cam ?? 2f + extent * 0.9f);
        GD.Print($"bestiary: {pick.Count} of {all.Count}: {string.Join(" ", pick.Select(a => a.Id))}");
    }
}
