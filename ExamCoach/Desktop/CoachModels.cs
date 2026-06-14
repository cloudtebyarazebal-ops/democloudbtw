using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExamCoachDesktop;

public sealed class CoachData
{
    [JsonPropertyName("steps")]
    public List<CoachStep> Steps { get; set; } = [];

    [JsonPropertyName("modules")]
    public List<CoachModule> Modules { get; set; } = [];
}

public sealed class CoachStep
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("phase")]
    public string Phase { get; set; } = "";

    [JsonPropertyName("module")]
    public string Module { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("vsHint")]
    public string VsHint { get; set; } = "";

    [JsonPropertyName("terminal")]
    public string Terminal { get; set; } = "";

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";
}

public sealed class CoachModule
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("minutes")]
    public int Minutes { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("stepIndices")]
    public List<int> StepIndices { get; set; } = [];
}

public sealed class TimerSnapshot
{
    public bool Running { get; set; }
    public DateTime? StartedAt { get; set; }
    public int RemainingSec { get; set; }
}

public static class CoachLoader
{
    public static CoachData Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CoachData>(json) ?? new CoachData();
    }
}

public static class FragmentBuilder
{
    public static List<string> Build(string code, string mode)
    {
        if (string.IsNullOrEmpty(code)) return [];
        return mode switch
        {
            "all" => [code],
            "line" => code.Split('\n').ToList(),
            "word" => System.Text.RegularExpressions.Regex.Matches(code, @"\S+|\s+")
                .Select(m => m.Value).ToList(),
            "char" => code.Select(c => c.ToString()).ToList(),
            _ => code.Split('\n').ToList()
        };
    }

    public static string Join(IEnumerable<string> parts, string mode) =>
        mode == "line" ? string.Join('\n', parts) : string.Concat(parts);
}
