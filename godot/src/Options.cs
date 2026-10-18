using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// Command-line options after "--", for sharing a moment or checking a build headlessly:
/// <c>--seed 7 --day 40 --autoplay --tab bounties --site wolfwood --scry wolfwood --screenshot shot.png</c>.
/// Also <c>--scout site</c>, <c>--mirror site</c>, <c>--build pool|spire|tower|palisade</c>, <c>--relic item-id</c> and <c>--buy all|N</c> (companies' reports).
/// </summary>
public sealed class Options
{
    public int? Seed;
    public int Day;
    public int Hour;
    public int? Speed;
    public string? Tab;
    public float? Scroll;

    /// <summary>With --autoplay, leave companies' reports unbought for the player (screenshots of the companies window).</summary>
    public bool KeepReports;

    /// <summary>Lift the fog everywhere (screenshots of the wilds and the roads through them).</summary>
    public bool Reveal;
    public string? Site;
    public string? Screenshot;
    public int Frames = 45;

    /// <summary>For clips: the camera pulls slowly back from the town to the whole map, turning as it goes.</summary>
    public bool Flyout;
    public bool Autoplay;

    /// <summary>--load a save file instead of starting a town; --menu opens the game menu (for screenshots).</summary>
    public string? Load;
    public bool Menu;

    /// <summary>--title shows the main menu; --tutorial start|step-id runs the tutorial from there (screenshots).</summary>
    public bool Title;

    /// <summary>--history [power-id]: open the world's history timeline, with a power's story picked.</summary>
    public string? History;
    public string? Tutorial;
    public bool Hero;

    /// <summary>--house: select the house on the most pleasant street, for screenshots of the inspector.</summary>
    public bool House;
    /// <summary>For screenshots: click the alert at this position in the strip (0 is the first) once it shows.</summary>
    public int? Alert;
    /// <summary>For screenshots: show the hover summary of the alert icon at this position.</summary>
    public int? AlertHover;

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

    /// <summary>--smoke plays a build through from the main menu and quits with its verdict (see <see cref="Main"/>'s Smoke.cs); --smoke-days sets how far the town runs first.</summary>
    public bool Smoke;
    public int SmokeDays = 120;

    /// <summary>Keep time running through war news, for recording clips.</summary>
    public bool NoPause;
    public readonly List<(string Verb, string Arg)> Script = [];

    /// <summary>--plan opens the build bar on a group; --tool picks a zone or building; --hover x,y,x2,y2 previews a stroke over lots.</summary>
    public string? Plan, Tool;
    public int[]? Hover;

    /// <summary>--beautify D: dress the town as a player who cares how it looks, keeping its square open from day D (see <see cref="Showcase"/>).</summary>
    public int? Beautify;

    /// <summary>--decor picks a decoration (or "eraser") to hold; --decor-at x,y,angle shows it at a point in lots.</summary>
    /// <summary>--story ID opens that story event's card (a fresh one, bound to the town as it stands); --story-pick N takes option N; --background ID picks the mayor's past.</summary>
    public string? Story, Background;
    public int? StoryPick;

    /// <summary>--mayor-level N raises the mayor to level N (advances left to choose); --advance A spends one (repeatable); --inspiration N sets it. For shots.</summary>
    public int? MayorLevel, Inspiration;
    public List<string> Advances = [];

    public string? DecorTool;
    public double[]? DecorAt;

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
                case "--scroll": o.Scroll = float.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
                case "--site": o.Site = Next(); break;
                case "--screenshot": o.Screenshot = Next(); break;
                case "--frames": o.Frames = int.Parse(Next()); break;
                case "--flyout": o.Flyout = true; break;
                case "--autoplay": o.Autoplay = true; break;
                case "--load": o.Load = Next(); break;
                case "--menu": o.Menu = true; break;
                case "--title": o.Title = true; break;
                case "--history": o.History = args.Length > i + 1 && !args[i + 1].StartsWith("--") ? Next() : ""; break;
                case "--tutorial": o.Tutorial = Next(); break;
                case "--hero": o.Hero = true; break;
                case "--house": o.House = true; break;
                case "--alert": o.Alert = int.Parse(Next()); break;
                case "--alert-hover": o.AlertHover = int.Parse(Next()); break;
                case "--no-pause": o.NoPause = true; break;
                case "--keep-reports": o.KeepReports = true; break;
                case "--reveal": o.Reveal = true; break;
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
                case "--smoke": o.Smoke = true; break;
                case "--smoke-days": o.SmokeDays = int.Parse(Next()); break;
                case "--plan": o.Plan = Next(); break;
                case "--tool": o.Tool = Next(); break;
                case "--hover": o.Hover = Next().Split(',').Select(int.Parse).ToArray(); break;
                case "--beautify": o.Beautify = int.Parse(Next()); break;
                case "--decor": o.DecorTool = Next(); break;
                case "--story": o.Story = Next(); break;
                case "--story-pick": o.StoryPick = int.Parse(Next()); break;
                case "--background": o.Background = Next(); break;
                case "--mayor-level": o.MayorLevel = int.Parse(Next()); break;
                case "--advance": o.Advances.Add(Next()); break;
                case "--inspiration": o.Inspiration = int.Parse(Next()); break;
                case "--decor-at": o.DecorAt = Next().Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray(); break;
                case "--bounty" or "--scry" or "--send" or "--recall" or "--build" or "--scout" or "--mirror" or "--relic" or "--buy":
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
                case "buy":
                    // Buy companies' reports ("all", or how many, newest first), topping up the treasury to pay.
                    var sale = Intel.ForSale(t);
                    int n = arg == "all" ? sale.Count : int.Parse(arg);
                    foreach (var p in sale.Take(n))
                    {
                        t.Treasury += Intel.Ask(t, p);
                        sim.BuyIntel(p.Id);
                    }
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
