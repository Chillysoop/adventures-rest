using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The sky dock's airship in flight (<see cref="Skyship"/>): over the first hours of a trade run it slips its mooring,
/// climbs over the rooftops and sails off toward the people it is bound for, and on the day it comes home it sails in
/// from that side and settles back onto the mast. While it is away, or carrying a company, the mast stands empty.
/// </summary>
public partial class TownView
{
    /// <summary>Hours the airship takes to climb away out of sight, or to come in and moor.</summary>
    private const int SkyHours = 6;

    private MeshInstance3D? _airship;
    private float _skyU = -1;

    /// <summary>Whether the airship rides at the mast: home, and not flying out or in.</summary>
    private static bool SkyMoored(Town town) =>
        !Skyship.Away(town) && !town.Parties.Any(p => p.Airship && !p.Returned) && town.Tick - town.Trade.Sky.LandedTick >= SkyHours;

    /// <summary>Each frame: where the airship is on its way out or in, eased between the hours.</summary>
    public void Sky(float dt)
    {
        var town = map.Sim.Town;
        var sky = town.Trade.Sky;
        var dock = town.Buildings.FirstOrDefault(b => b.Def.Id == "sky_dock" && !b.IsSite);
        // How far along it is: 0 at the mast, 1 out of sight, and whose way it flies.
        float target = -1;
        int? toward = null;
        if (dock != null && sky.Out is { } run && town.Tick - run.SetOutTick < SkyHours)
        {
            target = (town.Tick - run.SetOutTick + 0.5f) / SkyHours;
            toward = run.SettlementId;
        }
        else if (dock != null && sky.Out == null && town.Tick - sky.LandedTick < SkyHours && town.Settlements.Count > 0)
            target = 1 - (town.Tick - sky.LandedTick + 0.5f) / SkyHours;
        if (target < 0)
        {
            _skyU = -1;
            if (_airship != null) _airship.Visible = false;
            return;
        }
        _skyU = _skyU < 0 ? target : _skyU + (target - _skyU) * (1 - Mathf.Exp(-dt * 2));
        if (_airship == null)
        {
            var kit = new MeshKit();
            Airship(kit);
            _airship = new MeshInstance3D { Mesh = kit.Build(), MaterialOverride = Mats.Props };
            AddChild(_airship);
        }
        _airship.Visible = true;


        // Toward the people it trades with (bearing 0 north, clockwise; north is up the map), or the last one it flew to.
        int sid = toward ?? LastPort(town);
        double bearing = sid >= 0 && sid < town.Settlements.Count ? town.Regions[town.Settlements[sid].RegionId].Bearing : 0;
        float b = Mathf.DegToRad((float)bearing);
        var dir = new Vector3(Mathf.Sin(b), 0, -Mathf.Cos(b));
        var lot = town.Lot(dock!.LotId);
        var c = Geo.LotPos(lot);
        float scale = Math.Max(lot.W, lot.D) / (float)Land.Res / 0.92f;
        var mast = new Vector3(c.X, Floor(lot) + 0.7f * scale, c.Y);
        float u = Mathf.Clamp(_skyU, 0, 1);
        float climb = Mathf.SmoothStep(0, 1, Mathf.Min(1, u * 2.5f));
        var at = mast + Vector3.Up * (climb * 1.2f + u * 2) + dir * (u * u * u * 40) + Vector3.Up * 0.06f * Mathf.Sin((float)Time.GetTicksMsec() / 700f);
        // Nose into the wind it sails: out along the bearing, home against it; a little lean as it turns off the mast.
        float yaw = Mathf.Atan2(-dir.X, -dir.Z) + (toward == null ? Mathf.Pi : 0) + (1 - climb) * 0.6f;
        _airship.Transform = new Transform3D(Basis.FromEuler(new Vector3(0, yaw, 0)).Scaled(Vector3.One * scale * 1.15f), at);
    }

    /// <summary>The people the last run went to.</summary>
    private static int LastPort(Town town) => town.Trade.Sky.LastPort;
}
