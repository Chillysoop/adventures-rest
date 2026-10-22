using Godot;

namespace CrownAndCompass.Client.Map;

public partial class Crowd
{
    /// <summary>--bestiary: creatures stood in rows at <see cref="GalleryAt"/> for shots, each idling, walking on the spot or striking.</summary>
    public List<string> Gallery = [];
    public Vector2 GalleryAt;

    /// <summary>Where each of a gallery stands, from GalleryAt, rows as wide as the page allows; and how far the page spreads.</summary>
    public static (List<Vector2> Spots, float Extent) GalleryLayout(IReadOnlyList<float> scales)
    {
        var spots = new List<Vector2>();
        if (scales.Count == 0) return (spots, 3);
        float total = scales.Sum(s => 0.55f + 0.9f * s);
        // Rows about half again as wide as the page is deep, as the screen is.
        float widest = scales.Max(s => 0.55f + 0.9f * s);
        float rowWide = Mathf.Max(3, Mathf.Max(Mathf.Sqrt(total * widest * 1.6f), widest));
        float x = 0, y = 0, rowDeep = 0;
        var row = new List<int>();
        void Close()
        {
            // Centre the row.
            float shift = x / 2;
            foreach (int i in row) spots[i] -= new Vector2(shift, 0);
            row.Clear();
            y += rowDeep;
            x = 0;
            rowDeep = 0;
        }
        for (int i = 0; i < scales.Count; i++)
        {
            float w = 0.55f + 0.9f * scales[i];
            if (x > 0 && x + w > rowWide) Close();
            spots.Add(new Vector2(x + w / 2, y + w / 2));
            row.Add(i);
            x += w;
            rowDeep = Mathf.Max(rowDeep, w);
        }
        Close();
        for (int i = 0; i < spots.Count; i++) spots[i] -= new Vector2(0, y / 2);
        return (spots, Mathf.Max(rowWide, y));
    }

    private void GalleryPut(float now)
    {
        if (Gallery.Count == 0) return;
        var (spots, _) = GalleryLayout(Gallery.Select(id => (float)(Beasts.Of(id)?.Scale ?? 1)).ToList());
        for (int i = 0; i < Gallery.Count; i++)
        {
            string id = Gallery[i];
            var act = (i % 3) switch { 0 => Beasts.Act.Idle, 1 => Beasts.Act.Move, _ => Beasts.Act.Attack };
            float u = act == Beasts.Act.Move ? now * 1.4f + i * 0.3f : act == Beasts.Act.Attack ? now / 1.4f + i * 0.17f : now * 0.3f + i * 0.2f;
            var p = map.Gen.At(GalleryAt + spots[i]) + new Vector3(0, Beasts.Lift(id, act == Beasts.Act.Move) * 0.4f, 0);
            Beasts.Put(id, new Transform3D(Basis.FromEuler(new Vector3(0, 0.5f, 0)), p), act, u);
        }
    }
}
