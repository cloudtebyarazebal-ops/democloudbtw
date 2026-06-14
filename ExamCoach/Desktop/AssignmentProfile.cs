using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExamCoachDesktop;

public sealed class TextReplacement
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}

public sealed class AssignmentProfile
{
    public string? AssignmentTitle { get; set; }
    /// <summary>PU — книги (профильный), BU — велосипеды (базовый).</summary>
    public string? ExamVariant { get; set; }
    public string? DomainDescription { get; set; }
    public string? ExamLevel { get; set; }
    public string? ExamCipher { get; set; }
    /// <summary>Kod — демоэкзамен КОД; Custom — учебное ТЗ (магазин, БД, роли).</summary>
    public string? AssignmentKind { get; set; }
    public AssignmentRequirements? Requirements { get; set; }
    public List<string> ExcludedStepIds { get; set; } = [];
    public string? AdaptationSummary { get; set; }
    public string? ReferenceProjectPath { get; set; }
    public string? SourceText { get; set; }
    public List<TextReplacement> Replacements { get; set; } = [];
    public List<TransformLogEntry> TransformLog { get; set; } = [];
    /// <summary>Несогласованности кода после правок (пусто — всё ок).</summary>
    public List<string> IntegrityIssues { get; set; } = [];
    public DateTime? AppliedAt { get; set; }
}

public static class TextAdaptEngine
{
    public static string Apply(string? text, IReadOnlyList<TextReplacement> replacements)
    {
        if (string.IsNullOrEmpty(text) || replacements.Count == 0) return text ?? "";
        var result = text;
        foreach (var r in replacements.OrderByDescending(x => x.From.Length))
        {
            if (string.IsNullOrEmpty(r.From)) continue;
            result = result.Replace(r.From, r.To ?? "");
        }
        return result;
    }

    public static CoachData Adapt(CoachData source, IReadOnlyList<TextReplacement> replacements)
    {
        var clone = CoachDataSerializer.Clone(source);
        AdaptInPlace(clone, replacements);
        return clone;
    }

    public static void AdaptInPlace(CoachData data, IReadOnlyList<TextReplacement> replacements)
    {
        foreach (var step in data.Steps)
        {
            step.Title = Apply(step.Title, replacements);
            step.VsHint = Apply(step.VsHint, replacements);
            step.Terminal = Apply(step.Terminal, replacements);
            step.Code = Apply(step.Code, replacements);
        }
        foreach (var m in data.Modules)
        {
            m.Title = Apply(m.Title, replacements);
            m.Description = Apply(m.Description, replacements);
        }
    }
}

public static class CoachDataSerializer
{
    public static CoachData Clone(CoachData data)
    {
        var json = JsonSerializer.Serialize(data);
        return JsonSerializer.Deserialize<CoachData>(json) ?? new CoachData();
    }

    public static void Save(CoachData data, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));

    public static CoachData Load(string path) => CoachLoader.Load(path);
}
