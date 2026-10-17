using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrownAndCompass.Sim.Core;

public enum Good { Food, Timber, Tools, Ale }

public enum Zone { None, Civic, Residential, Farmland, Commercial }

public sealed class BuildingDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public Zone Zone { get; init; }
    public bool Buildable { get; init; } = true;
    public int Cost { get; init; }
    public int Timber { get; init; }
    public int BuildHours { get; init; }
    public int Jobs { get; init; }
    public double Wage { get; init; }
    public int Housing { get; init; }
    public int Rent { get; init; }
    public bool Venue { get; init; }
    public int Capacity { get; init; }
    public string? Profession { get; init; }
    public Dictionary<Good, double> Produces { get; init; } = new();
    public Dictionary<Good, double> Consumes { get; init; } = new();

    public bool IsBusiness => Jobs > 0;
}

public sealed class RaceDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int Weight { get; init; }
    public List<string> Given { get; init; } = new();
    public List<string> Family { get; init; } = new();
}

/// <summary>
/// Data-driven content (buildings, races, names). Loaded from embedded JSON by default so the
/// simulation runs anywhere; a mod folder can override it later.
/// </summary>
public sealed class Content
{
    public IReadOnlyDictionary<string, BuildingDef> Buildings { get; }
    public IReadOnlyList<RaceDef> Races { get; }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public Content(IEnumerable<BuildingDef> buildings, IEnumerable<RaceDef> races)
    {
        Buildings = buildings.ToDictionary(b => b.Id);
        Races = races.ToList();
    }

    public BuildingDef Building(string id) => Buildings[id];

    public static Content LoadDefault()
    {
        var buildings = Load<List<BuildingDef>>("Content.buildings.json");
        var names = Load<NamesFile>("Content.names.json");
        return new Content(buildings, names.Races);
    }

    private static T Load<T>(string resource)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing content resource {resource}");
        return JsonSerializer.Deserialize<T>(stream, Json)
            ?? throw new InvalidOperationException($"Empty content resource {resource}");
    }

    private sealed class NamesFile
    {
        public List<RaceDef> Races { get; init; } = new();
    }
}
