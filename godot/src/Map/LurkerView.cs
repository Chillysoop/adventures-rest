using CrownAndCompass.Sim;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// Enemies' agents loitering in town until dark (<see cref="Sim.Systems.Lurkers"/>): a hooded figure in a drab cloak
/// drifting round the spot it watches, among the townsfolk, for the player to find and click. One draw call for all.
/// </summary>
public partial class LurkerView(MapView map) : Node3D
{
    private readonly MultiMeshInstance3D _mesh = new()
    {
        Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true },
        MaterialOverride = Mats.People,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
    };

    /// <summary>Where each agent stands this frame, by id, for picking them out with the mouse.</summary>
    public readonly Dictionary<int, Vector2> Spots = [];

    private static readonly Color[] Cloaks = [new("34302c"), new("3b3a33"), new("2f3236"), new("40352e")];

    public override void _Ready()
    {
        var k = new MeshKit();
        var skin = new Color(1, 1, 1, MeshKit.Flag.Skin);
        // Stooped under a long cloak (tinted per instance) with the hood up, the face in shadow.
        k.Push(MeshKit.At(0, 0, 0, 0, 1.55f));
        k.Frustum(new Vector3(-0.025f, 0, 0), 0.018f, 0.018f, 0.05f, 4, new Color("2a201a"));
        k.Frustum(new Vector3(0.025f, 0, 0), 0.018f, 0.018f, 0.05f, 4, new Color("2a201a"));
        k.Frustum(new Vector3(0, 0.03f, 0), 0.075f, 0.05f, 0.15f, 6, Colors.White);
        k.Blob(new Vector3(0, 0.2f, 0.012f), new Vector3(0.036f, 0.04f, 0.036f), skin);
        k.Frustum(new Vector3(0, 0.17f, -0.006f), 0.056f, 0.012f, 0.1f, 6, Colors.White);
        k.Blob(new Vector3(0.055f, 0.11f, 0.03f), new Vector3(0.016f, 0.016f, 0.016f), skin);
        k.Pop();
        _mesh.Multimesh.Mesh = k.Build();
        AddChild(_mesh);
    }

    public void Advance()
    {
        Spots.Clear();
        var town = map.Sim.Town;
        var mm = _mesh.Multimesh;
        Visible = map.Distance < 260 && town.Lurkers.Count > 0;
        if (!Visible)
        {
            if (mm.InstanceCount != 0) mm.InstanceCount = 0;
            return;
        }
        float now = (float)Time.GetTicksMsec() / 1000f;
        var buf = new float[town.Lurkers.Count * 20];
        int n = 0;
        foreach (var l in town.Lurkers)
        {
            // A slow, aimless circuit of the spot with long stops, glancing at what it watches.
            float t = now * 0.18f + l.Id * 1.7f;
            float drift = Mathf.Sin(t) * 0.5f + Mathf.Sin(t * 0.43f + 1.1f) * 0.35f;
            var at = Planner.FromTown(l.X, l.Y) + new Vector2(Mathf.Cos(t * 0.6f + l.Id), Mathf.Sin(t * 0.6f + l.Id)) * (0.35f + drift * 0.25f);
            Spots[l.Id] = at;
            var look = l.BuildingId is int b && town.Buildings.FirstOrDefault(x => x.Id == b) is { } site ? Geo.LotPos(town.Lot(site.LotId)) : Geo.Center;
            var d = look - at;
            var basis = Basis.FromEuler(new Vector3(0, Mathf.Atan2(d.X, d.Y), 0));
            var p = map.Gen.At(at);
            var cloak = Cloaks[l.Id % Cloaks.Length];
            var skin = new Color("8a7060");
            float[] row = [basis.X.X, basis.Y.X, basis.Z.X, p.X, basis.X.Y, basis.Y.Y, basis.Z.Y, p.Y, basis.X.Z, basis.Y.Z, basis.Z.Z, p.Z, cloak.R, cloak.G, cloak.B, 1, skin.R, skin.G, skin.B, 1];
            Array.Copy(row, 0, buf, n * 20, 20);
            n++;
        }
        if (mm.InstanceCount != n) mm.InstanceCount = n;
        mm.Buffer = buf;
    }
}
