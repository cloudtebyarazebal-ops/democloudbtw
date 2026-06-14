using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExamCoachDesktop;

/// <summary>Применяет декларативные правки кода к шагам плана.</summary>
public static class CodeTransformationEngine
{
    public static List<TransformLogEntry> Apply(CoachData data, AssignmentRequirements req, AssignmentProfile profile)
    {
        var log = new List<TransformLogEntry>();
        var rules = new List<CodeTransformRule>();
        rules.AddRange(CodeTransformCatalog.GetBuiltInRules());
        rules.AddRange(ReferenceCleanupRules.Generate(req));
        rules.AddRange(LoadExternalRules());

        foreach (var rule in rules)
        {
            if (!rule.Condition.Match(req))
                continue;

            foreach (var step in data.Steps)
            {
                if (!StepMatches(step, rule.StepMatch))
                    continue;

                if (rule.Action == TransformActionKind.PrependHint)
                {
                    if (PrependHint(step, rule.Parameter ?? "") && !string.IsNullOrEmpty(rule.Parameter))
                    {
                        log.Add(new TransformLogEntry
                        {
                            Action = rule.Action.ToString(),
                            Target = rule.Parameter,
                            Step = step.Title,
                            Reason = rule.Reason
                        });
                    }
                    continue;
                }

                var before = step.Code ?? "";
                var after = ApplyRule(before, rule, step);
                if (after == before)
                    continue;

                step.Code = after;
                log.Add(new TransformLogEntry
                {
                    Action = rule.Action.ToString(),
                    Target = rule.Parameter ?? rule.Id,
                    Step = step.Title,
                    Reason = rule.Reason
                });
            }
        }

        profile.TransformLog = log;
        return log;
    }

    private static string ApplyRule(string code, CodeTransformRule rule, CoachStep step) =>
        rule.Action switch
        {
            TransformActionKind.RemoveType => RemoveTypes(code, rule.Parameter),
            TransformActionKind.RemoveMarkedSection => CodeSurgery.RemoveMarkedSection(
                code, rule.Parameter ?? "", rule.Parameter2),
            TransformActionKind.RemoveLineContaining => RemoveLinesContaining(code, rule.Parameter),
            TransformActionKind.RemoveLinesMatching => CodeSurgery.RemoveLinesMatching(code, rule.Parameter ?? ""),
            TransformActionKind.RemoveRazorBlock => CodeSurgery.RemoveRazorBlockContaining(code, rule.Parameter ?? ""),
            TransformActionKind.RemoveRazorNav => CodeSurgery.RemoveRazorBlockContaining(code, rule.Parameter ?? ""),
            TransformActionKind.RemoveMethod => RemoveMethods(code, rule.Parameter),
            TransformActionKind.RemoveEnum => CodeSurgery.RemovePublicEnum(code, rule.Parameter ?? ""),
            TransformActionKind.RemoveFormGroup => RemoveFormGroupsContaining(code, rule.Parameter ?? ""),
            TransformActionKind.RemoveProperty => CodeSurgery.RemoveMembersContaining(code, rule.Parameter ?? ""),
            TransformActionKind.TextReplace when rule.Parameter != null && rule.Parameter2 != null =>
                code.Replace(rule.Parameter, rule.Parameter2),
            _ => code
        };

    private static bool PrependHint(CoachStep step, string hint)
    {
        if (step.VsHint.Contains(hint, StringComparison.Ordinal))
            return false;
        step.VsHint = hint + "\n" + step.VsHint;
        return true;
    }

    private static string RemoveTypes(string code, string? names)
    {
        if (string.IsNullOrEmpty(names)) return code;
        foreach (var name in names.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            code = CodeSurgery.RemovePublicType(code, name);
        return code;
    }

    private static string RemoveLinesContaining(string code, string? patterns)
    {
        if (string.IsNullOrEmpty(patterns)) return code;
        foreach (var pattern in patterns.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            code = CodeSurgery.RemoveLineContaining(code, pattern);
        return code;
    }

    private static string RemoveFormGroupsContaining(string code, string patterns)
    {
        foreach (var pattern in patterns.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string next;
            do
            {
                next = CodeSurgery.RemoveFormGroupContaining(code, pattern);
                if (next == code) break;
                code = next;
            } while (code.Contains(pattern, StringComparison.Ordinal));
        }

        return code;
    }

    private static string RemoveMethods(string code, string? names)
    {
        if (string.IsNullOrEmpty(names)) return code;
        foreach (var name in names.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            code = CodeSurgery.RemovePublicMethod(code, name);
        return code;
    }

    private static bool StepMatches(CoachStep step, string pattern)
    {
        if (string.IsNullOrEmpty(pattern) || pattern == "*")
            return true;

        var haystack = $"{step.Id}\n{step.Title}";
        var tokens = pattern.Split('*', StringSplitOptions.RemoveEmptyEntries);
        return tokens.All(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<CodeTransformRule> LoadExternalRules()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assignment-transform-rules.json");
        if (!File.Exists(path))
            return [];

        try
        {
            var json = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<ExternalRulesFile>(json, JsonOptions);
            return doc?.Rules ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed class ExternalRulesFile
    {
        public List<CodeTransformRule> Rules { get; set; } = [];
    }
}
