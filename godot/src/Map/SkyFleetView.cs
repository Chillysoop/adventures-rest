using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;
using Airships = CrownAndCompass.Sim.Systems.Sky;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The town's airships on their jobs (<see cref="Sky"/>): a patrol circles high over the rooftops, and a scout or a
/// strike climbs away toward the land it is bound for over its first hours and sails home from that side over its
/// last. Bigger kinds fly bigger; the merchant run keeps its own ship (<see cref="Sky(float)"/>).
/// </summary>
public partial class TownView
{
    private readonly Dictionary<int, MeshInstance3D> _skyFleet = [];
    private ArrayMesh? _airshipMesh;

    /// <summary>Each frame: each airship aloft over or near the town, and none for those out of sight or down.</summary>
    public void SkyFleet(float dt)
    {
        var town = map.Sim.Town;
        var dock = town.Buildings.FirstOrDefault(b => b.Def.Id == "sky_dock" && !b.IsSite);
        var seen = new HashSet<int>();
        if (dock != null)
            foreach (var s in Airships.Ships(town))
            {
                if (s.Flight is not { } f) continue;
                int hours = SimTime.HoursPerDay;
                float outU = (town.Tick - f.SetOutTick + 0.5f) / SkyHours;
                float inU = (f.BackDay * hours - town.Tick - 0.5f) / SkyHours;
                float u = f.Job == "patrol" ? Mathf.Min(1, Mathf.Min(outU, inU)) : Mathf.Min(outU, inU);
                if (u > 1 && f.Job != "patrol") continue;
                seen.Add(s.Id);
                if (!_skyFleet.TryGetValue(s.Id, out var m))
                {
                    if (_airshipMesh == null)
                    {
                        var kit = new MeshKit();
                        Airship(kit);
                        _airshipMesh = kit.Build();
                    }
                    m = new MeshInstance3D { Mesh = _airshipMesh, MaterialOverride = Mats.Props };
                    AddChild(m);
                    _skyFleet[s.Id] = m;
                }
                var lot = town.Lot(dock.LotId);
                var c = Geo.LotPos(lot);
                float scale = Math.Max(lot.W, lot.D) / (float)Land.Res / 0.92f * s.Kind switch { "leviathan" => 3f, "dreadnought" => 2.3f, "galleon" => 1.7f, "frigate" => 1.35f, _ => 1f };
                var mast = new Vector3(c.X, Floor(lot) + 0.7f * scale, c.Y);
                float bob = 0.06f * Mathf.Sin((float)Time.GetTicksMsec() / 700f + s.Id);
                float climb = Mathf.SmoothStep(0, 1, Mathf.Clamp(u * 2.5f, 0, 1));
                Vector3 at;
                float yaw;
                if (f.Job == "patrol")
                {
                    // A slow ring round the town's middle, high enough to clear the towers, one lap every few hours.
                    var mid = new Vector3(Geo.Center.X, mast.Y, Geo.Center.Y);
                    float ring = 10 + 2 * s.Id % 5;
                    float a = (town.Tick + (float)(Time.GetTicksMsec() % 60000) / 60000f) * 0.35f + s.Id * 1.7f;
                    var round = mid + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * ring + Vector3.Up * 4;
                    at = mast.Lerp(round, climb) + Vector3.Up * bob;
                    yaw = Mathf.Atan2(Mathf.Sin(a), -Mathf.Cos(a));
                }
                else
                {
                    var (rx, ry) = f.RegionId >= 0 ? World.Middle(town, town.Regions[f.RegionId]) : (0.0, -1.0);
                    float b = Mathf.Atan2((float)rx, (float)-ry);
                    var dir = new Vector3(Mathf.Sin(b), 0, -Mathf.Cos(b));
                    u = Mathf.Clamp(u, 0, 1);
                    at = mast + Vector3.Up * (climb * 1.2f + u * 2.5f) + dir * (u * u * u * 40) + Vector3.Up * bob;
                    yaw = Mathf.Atan2(-dir.X, -dir.Z) + (outU <= inU ? 0 : Mathf.Pi);
                }
                m.Visible = true;
                m.Transform = new Transform3D(Basis.FromEuler(new Vector3(0, yaw, 0)).Scaled(Vector3.One * scale * 1.15f), at);
            }
        foreach (var (id, m) in _skyFleet)
            if (!seen.Contains(id)) m.Visible = false;
    }
}
