using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The castle look ("keep and armory"): windows are dark oak boards with iron corner brackets, title bars carved beams,
/// the top bar a stone parapet with merlons, the dock a stone ledge, buttons oak planks with iron studs and the main
/// action a red banner bound in gold. The art is nine-patch SVG in assets/ui, drawn by scripts/castle-ui.ts; the
/// margins here say where each one's corners end. Text stays light on dark so it reads as before.
/// </summary>
public static class Castle
{
    private static readonly Dictionary<string, Texture2D> Art = [];

    private static Texture2D Tex(string name)
    {
        if (!Art.TryGetValue(name, out var t)) Art[name] = t = GD.Load<Texture2D>($"res://assets/ui/{name}.svg");
        return t;
    }

    private static StyleBoxTexture Cut(string name, float left, float top, float right, float bottom, int padX, int padY)
    {
        var s = new StyleBoxTexture
        {
            Texture = Tex(name),
            TextureMarginLeft = left,
            TextureMarginTop = top,
            TextureMarginRight = right,
            TextureMarginBottom = bottom,
        };
        s.ContentMarginLeft = s.ContentMarginRight = padX;
        s.ContentMarginTop = s.ContentMarginBottom = padY;
        return s;
    }

    /// <summary>A window, dialog or bar: oak boards in a dark frame with iron brackets.</summary>
    public static StyleBoxTexture Oak(int padX = 0, int padY = 0) => Cut("oak-panel", 22, 22, 22, 22, padX, padY);

    /// <summary>A window's title bar: a carved beam with a stud at each end.</summary>
    public static StyleBoxTexture Beam(int padX = 14, int padY = 8) => Cut("beam", 14, 4, 14, 4, padX, padY);

    /// <summary>The top bar: stone with merlons hanging below it, tiled along its length.</summary>
    public static StyleBoxTexture Parapet(int padX = 16, int padY = 6)
    {
        var s = Cut("parapet", 0, 4, 0, 14, padX, padY);
        s.AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile;
        // The merlons hang over the map, below the bar's own height.
        s.ExpandMarginBottom = 12;
        s.ContentMarginBottom = padY;
        return s;
    }

    /// <summary>The dock: a stone ledge with merlons standing up from its top edge.</summary>
    public static StyleBoxTexture Ledge(int padX = 8, int padY = 6)
    {
        var s = Cut("ledge", 0, 12, 0, 4, padX, padY);
        s.AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile;
        s.ExpandMarginTop = 10;
        return s;
    }

    /// <summary>A plank button's look in each state; <paramref name="banner"/> is the red main action.</summary>
    public static StyleBoxTexture Button(string state, bool banner, int padX = 12, int padY = 6) =>
        Cut((banner ? "banner" : "button") + (state == "normal" ? "" : "-" + state), 9, 9, 9, 9, padX, padY);
}
