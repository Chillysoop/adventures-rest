using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// Command-line options after "--", for sharing a moment or checking a build headlessly:
/// <c>--seed 7 --day 40 --autoplay --tab bounties --site wolfwood --scry wolfwood --screenshot shot.png</c>.
/// Also <c>--scout site</c>, <c>--mirror site</c>, <c>--build pool|spire|tower|palisade</c> and <c>--relic item-id</c>.
/// </summary>
public sealed class Options
{
    public int? Seed;
    public int Day;
    public int Hour;
    public int? Speed;
    public string? Tab;
    public string? Site;
    public string? Screenshot;
    public int Frames = 45;

    /// <summary>For clips: the camera pulls slowly back from the town to the whole map, turning as it goes.</summary>
    public bool Flyout;
    public bool Autoplay;

    /// <summary>--load a save file instead of starting a town; --menu opens the game menu (for screenshots).</summary>
    public string? Load;
    public bool Menu;
    public bool Hero;
    /// <summary>For screenshots: click the alert at this position in the strip (0 is the first) once it shows.</summary>
    public int? Alert;

    /// <summary>Camera distance in tiles and an optional point to look at (--cam 40 --look 512,480), and the strategic view.</summary>
    public float? Cam;
    public Godot.Vector2? Look;
    public bool StrategicView;
    public int Yaw;

    /// <summary>Markers to place for a screenshot (--marker rally,512,470,Muster here) and one to open the card of (--open-marker 0).</summary>
    public readonly List<(Map.MarkerKind Kind, Godot.Vector2 At, string Label)> Markers = [];
    public int? OpenMarker;

    /// <summary>Extra trees per forest tile and a frame-time report, for the late-game performance check.</summary>
    public float Trees = 1;
    public bool Bench;

    /// <summary>Keep time running through war news, for recording clips.</summary>
    public bool NoPause;
    public readonly List<(string Verb, string Arg)> Script = [];

    /// <summary>--plan opens the build bar on a group; --tool picks a zone or building; --hover x,y,x2,y2 previews a stroke over lots.</summary>
    public string? Plan, Tool;
    public int[]? Hover;

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : "";
            switch (args[i])
            {
                case "--seed": o.Seed = int.Parse(Next()); break;
                case "--day": o.Day = int.Parse(Next()); break;
                case "--hour": o.Hour = int.Parse(Next()); break;
                case "--speed": o.Speed = int.Parse(Next()); break;
                case "--tab": o.Tab = Next(); break;
                case "--site": o.Site = Next(); break;
                case "--screenshot": o.Screenshot = Next(); break;
                case "--frames": o.Frames = int.Parse(Next()); break;
                case "--flyout": o.Flyout = true; break;
                case "--autoplay": o.Autoplay = true; break;
                case "--load": o.Load = Next(); break;
                case "--menu": o.Menu = true; break;
                case "--hero": o.Hero = true; break;
                case "--alert": o.Alert = int.Parse(Next()); break;
                case "--no-pause": o.NoPause = true; break;
                case "--cam": o.Cam = float.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
                case "--look":
                    var xy = Next().Split(',');
                    o.Look = new Godot.Vector2(float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "--strategic": o.StrategicView = true; break;
                case "--marker":
                    var mk = Next().Split(',', 4);
                    if (Enum.TryParse<Map.MarkerKind>(mk[0], true, out var kind))
                        o.Markers.Add((kind, new Godot.Vector2(float.Parse(mk[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(mk[2], System.Globalization.CultureInfo.InvariantCulture)), mk.Length > 3 ? mk[3] : ""));
                    break;
                case "--open-marker": o.OpenMarker = int.Parse(Next()); break;
                case "--yaw": o.Yaw = int.Parse(Next()); break;
                case "--trees": o.Trees = float.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
                case "--bench": o.Bench = true; break;
                case "--plan": o.Plan = Next(); break;
                case "--tool": o.Tool = Next(); break;
                case "--hover": o.Hover = Next().Split(',').Select(int.Parse).ToArray(); break;
                case "--bounty" or "--scry" or "--send" or "--recall" or "--build" or "--scout" or "--mirror" or "--relic":
                    o.Script.Add((args[i][2..], Next()));
                    break;
            }
        }
        return o;
    }

    /// <summary>Player actions to take once the town is set up, in order.</summary>
    public void ApplyScript(Simulation sim)
    {
        var t = sim.Town;
        foreach (var (verb, arg) in Script)
        {
            switch (verb)
            {
                case "bounty":
                    var site = Quests.Site(t, arg);
                    if (site != null) sim.PostBounty(arg, Quests.SuggestedReward(site.Def.Tier));
                    break;
                case "scry":
                    sim.CommissionScrying(arg);
                    break;
                case "send" or "recall":
                    var party = t.Parties.LastOrDefault(p => !p.Returned);
                    if (party != null) sim.SendWord(party.Id, verb == "recall");
                    break;
                case "build" when arg is "pool" or "spire":
                    t.Treasury += 2000;
                    sim.BuildSightWork(arg == "pool" ? "scrying_pool" : "sleepless_spire");
                    break;
                case "build":
                    sim.BuildDefense(arg == "tower" ? DefenseWork.Tower : DefenseWork.Palisade);
                    break;
                case "scout":
                    sim.PostScout(arg);
                    break;
                case "mirror":
                    sim.PlantMirror(arg);
                    break;
                case "relic":
                    // For showing a late-game moment quickly: a party has just brought a relic home.
                    if (t.Content.Library.Item(arg) is LibItem item) Watch.ReceiveRelic(t, item, "Company of the Compass");
                    break;
            }
        }
    }

    /// <summary>Save a screenshot and quit once enough frames have drawn for the camera to settle.</summary>
    public void Capture(Node node, ref int frames)
    {
        if (Screenshot == null || ++frames < Frames) return;
        var img = node.GetViewport().GetTexture().GetImage();
        img.SavePng(Screenshot);
        GD.Print($"Saved {Screenshot}");
        node.GetTree().Quit();
        Screenshot = null;
    }
}
