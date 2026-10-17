using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// Command-line options after "--", for sharing a moment or checking a build headlessly:
/// <c>--seed 7 --day 40 --autoplay --tab bounties --site wolfwood --scry wolfwood --screenshot shot.png</c>.
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
    public bool Autoplay;
    public readonly List<(string Verb, string Arg)> Script = [];

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
                case "--autoplay": o.Autoplay = true; break;
                case "--bounty" or "--scry" or "--send" or "--recall" or "--build":
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
                case "build":
                    sim.BuildDefense(arg == "tower" ? DefenseWork.Tower : DefenseWork.Palisade);
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
