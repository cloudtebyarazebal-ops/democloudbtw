namespace ExamCoachDesktop;

public static class StepFeatureCatalog
{
    /// <summary>Если шаг требует функции — все перечисленные должны быть в задании (OR-группы через |).</summary>
    public static IReadOnlyList<string> GetRequiredFeatures(CoachStep step)
    {
        var key = $"{step.Id}|{step.Title}".ToLowerInvariant();

        if (key.Contains("importservice") || key.Contains("views/import"))
            return [AssignmentFeatures.Import];

        if (key.Contains("orderservice") || key.Contains("views/orders") || key.Contains("orders/edit"))
            return [AssignmentFeatures.Orders];

        if (key.Contains("orderscontroller"))
            return [$"ANY:{AssignmentFeatures.Orders}|{AssignmentFeatures.Import}"];

        if (key.Contains("products/edit"))
            return [AssignmentFeatures.ProductCrud];

        if (key.Contains("products.js"))
            return [$"ANY:{AssignmentFeatures.ProductFilters}|{AssignmentFeatures.ProductSearch}|{AssignmentFeatures.ProductSort}"];

        if (key.Contains("home/error"))
            return [AssignmentFeatures.ErrorHandling];

        return [];
    }

    public static bool IsRequired(CoachStep step, AssignmentRequirements req)
    {
        var needed = GetRequiredFeatures(step);
        if (needed.Count == 0) return true;

        foreach (var feature in needed)
        {
            if (!feature.StartsWith("ANY:", StringComparison.Ordinal))
                continue;

            var options = feature[4..].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (options.Any(o => req.Features.Contains(o)))
                return true;
            return false;
        }

        return needed.All(f => req.Features.Contains(f));
    }
}
