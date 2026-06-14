namespace ExamCoachDesktop;

public sealed class TransformLogEntry
{
    public string Action { get; set; } = "";
    public string Target { get; set; } = "";
    public string? Step { get; set; }
    public string Reason { get; set; } = "";
}

public enum TransformActionKind
{
    TextReplace,
    RemoveType,
    RemoveMarkedSection,
    RemoveLineContaining,
    RemoveLinesMatching,
    RemoveRazorBlock,
    RemoveRazorNav,
    RemoveMethod,
    RemoveEnum,
    RemoveFormGroup,
    RemoveProperty,
    PrependHint
}

public sealed class TransformCondition
{
    public string? Variant { get; set; }
    public string? VariantNot { get; set; }
    public string? FeatureRequired { get; set; }
    public string? FeatureMissing { get; set; }

    public bool Match(AssignmentRequirements req)
    {
        if (Variant != null && !Variant.Equals(req.ExamVariant, StringComparison.OrdinalIgnoreCase))
            return false;
        if (VariantNot != null && VariantNot.Equals(req.ExamVariant, StringComparison.OrdinalIgnoreCase))
            return false;
        if (FeatureRequired != null && !req.Features.Contains(FeatureRequired))
            return false;
        if (FeatureMissing != null && req.Features.Contains(FeatureMissing))
            return false;
        return true;
    }
}

public sealed class CodeTransformRule
{
    public string Id { get; set; } = "";
    public TransformCondition Condition { get; set; } = new();
    public string StepMatch { get; set; } = "*";
    public TransformActionKind Action { get; set; }
    public string? Parameter { get; set; }
    public string? Parameter2 { get; set; }
    public string Reason { get; set; } = "";
}
