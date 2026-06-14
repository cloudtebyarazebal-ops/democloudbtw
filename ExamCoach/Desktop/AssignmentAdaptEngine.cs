namespace ExamCoachDesktop;

/// <summary>
/// Полная адаптация эталона KodShopWeb под текст задания:
/// вариант ПУ/БУ, предметная область, фильтрация шагов, правки кода.
/// </summary>
public static class AssignmentAdaptEngine
{
    public static CoachData Adapt(CoachData baseData, AssignmentProfile profile)
    {
        var req = AssignmentRequirementsAnalyzer.Analyze(profile.SourceText, profile);
        profile.Requirements = req;

        if (string.IsNullOrEmpty(profile.AssignmentKind) && !string.IsNullOrEmpty(profile.SourceText))
        {
            var reparsed = AssignmentTextParser.Parse(profile.SourceText);
            profile.AssignmentKind = reparsed.AssignmentKind;
            if (string.IsNullOrEmpty(profile.ExamVariant))
                profile.ExamVariant = reparsed.ExamVariant;
        }

        var data = CoachDataSerializer.Clone(baseData);
        var allReplacements = new List<TextReplacement>();

        if (!string.IsNullOrEmpty(profile.ExamVariant))
        {
            ExamVariantAdapter.ApplyToData(data, profile);
            allReplacements.AddRange(ExamVariantAdapter.BuildReplacementList(profile));
        }
        else if (profile.Requirements != null)
        {
            var reqReplacements = AssignmentTextReplacements.Build(profile.Requirements);
            TextAdaptEngine.AdaptInPlace(data, reqReplacements);
            allReplacements.AddRange(reqReplacements);
        }

        if (!req.ProjectName.Equals("KodShopWeb", StringComparison.OrdinalIgnoreCase))
        {
            var rename = ProjectRenameReplacements("KodShopWeb", "KodShop", req.ProjectName, req.BrandName);
            TextAdaptEngine.AdaptInPlace(data, rename);
            allReplacements.AddRange(rename);
        }

        DomainContentPatches.Apply(data, req);
        allReplacements.AddRange(DomainContentPatches.GetDomainReplacements(req));
        AuthorDomainPatches.Apply(data, req);
        if (AuthorDomainPatches.IsApplicable(req))
            allReplacements.AddRange(AuthorDomainPatches.GetReplacements());

        profile.ExcludedStepIds = FilterSteps(data, req);
        CodeTransformationEngine.Apply(data, req, profile);
        CodeTransformCrossFileSync.Sync(data);
        profile.IntegrityIssues = CodeTransformIntegrityCheck.Validate(data).ToList();
        UpdateModuleTimers(data, req);
        AnnotatePlan(data, profile, req);

        if (profile.Replacements.Count > 0)
        {
            var manual = profile.Replacements
                .Where(r => allReplacements.All(a => !a.From.Equals(r.From, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (manual.Count > 0)
            {
                TextAdaptEngine.AdaptInPlace(data, manual);
                allReplacements.AddRange(manual);
            }
        }

        profile.Replacements = allReplacements
            .GroupBy(r => r.From, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(r => r.From.Length)
            .ToList();

        profile.AdaptationSummary = BuildSummary(req, profile);
        return data;
    }

    public static string BuildSummary(AssignmentProfile profile) =>
        profile.AdaptationSummary ?? BuildSummary(profile.Requirements ?? new AssignmentRequirements(), profile);

    private static string BuildSummary(AssignmentRequirements req, AssignmentProfile profile)
    {
        var lines = new List<string>();
        if (!string.IsNullOrEmpty(req.ExamVariant))
            lines.Add($"Вариант: {req.ExamVariant} ({req.DomainLabel ?? "магазин"})");
        if (!string.IsNullOrEmpty(profile.AssignmentKind))
            lines.Add($"Тип задания: {(profile.AssignmentKind == "Custom" ? "учебное ТЗ" : "демоэкзамен КОД")}");
        lines.Add($"Проект: {req.ProjectName}");
        lines.Add($"Шагов в плане: {(profile.ExcludedStepIds?.Count > 0 ? "сокращён" : "полный")} ({profile.ExcludedStepIds?.Count ?? 0} убрано)");
        lines.Add($"Функции: {string.Join(", ", req.Features.OrderBy(f => f))}");
        lines.Add($"Модули: {string.Join(", ", req.Modules.OrderBy(m => m))}");
        if (profile.Replacements.Count > 0)
            lines.Add($"Текстовых замен: {profile.Replacements.Count}");
        if (profile.TransformLog.Count > 0)
        {
            var byAction = profile.TransformLog.GroupBy(t => t.Action).OrderBy(g => g.Key);
            lines.Add($"Структурных правок: {profile.TransformLog.Count}");
            foreach (var g in byAction)
                lines.Add($"  · {g.Key}: {g.Count()}");
        }

        var omitted = new List<string>();
        if (!req.Features.Contains(AssignmentFeatures.PickupPoints)) omitted.Add("ПВЗ");
        if (!req.Features.Contains(AssignmentFeatures.OrderDelivery)) omitted.Add("дата доставки");
        if (!req.Features.Contains(AssignmentFeatures.OrderStatusDetail)) omitted.Add("статусы заказа");
        if (omitted.Count > 0)
            lines.Add($"Убрано из эталона (нет в ТЗ): {string.Join(", ", omitted)}");

        if (req.Features.Contains(AssignmentFeatures.AuthorField))
            lines.Add("Домен: Manufacturer → Author (поле «автор» в ТЗ)");

        if (req.SeedData != null)
        {
            lines.Add($"Сид БД: {req.SeedData.Categories.Count} категорий, {req.SeedData.Makers.Count} {(req.SeedData.UseAuthorField ? "авторов" : "производителей")}, {req.SeedData.Products.Count} товаров");
            lines.Add($"Подсветка скидки: >{req.SeedData.DiscountHighlightPercent}% → {req.SeedData.DiscountHighlightColor}");
        }

        if (profile.IntegrityIssues?.Count > 0)
        {
            lines.Add($"⚠ Проверка целостности: {profile.IntegrityIssues.Count} проблем");
            foreach (var issue in profile.IntegrityIssues.Take(5))
                lines.Add($"  · {issue}");
        }

        return string.Join("\n", lines);
    }

    private static List<TextReplacement> ProjectRenameReplacements(
        string fromProject, string fromBrand, string toProject, string toBrand) =>
    [
        new() { From = fromProject, To = toProject },
        new() { From = fromBrand, To = toBrand },
        new() { From = "kodshop.db", To = $"{toBrand.ToLowerInvariant()}.db" },
        new() { From = "Data Source=kodshop.db", To = $"Data Source={toBrand.ToLowerInvariant()}.db" },
    ];

    private static List<string> FilterSteps(CoachData data, AssignmentRequirements req)
    {
        var excluded = new List<string>();

        for (var i = data.Steps.Count - 1; i >= 0; i--)
        {
            var step = data.Steps[i];
            if (step.Id.StartsWith("setup-", StringComparison.Ordinal))
                continue;

            if (!string.IsNullOrEmpty(step.Module) && !req.Modules.Contains(step.Module))
            {
                excluded.Add($"{step.Id} (модуль {step.Module} не в задании)");
                data.Steps.RemoveAt(i);
                continue;
            }

            if (!StepFeatureCatalog.IsRequired(step, req))
            {
                excluded.Add($"{step.Id} (не требуется: {string.Join("/", StepFeatureCatalog.GetRequiredFeatures(step))})");
                data.Steps.RemoveAt(i);
            }
        }

        RebuildModuleIndices(data);
        return excluded;
    }

    private static void UpdateModuleTimers(CoachData data, AssignmentRequirements req)
    {
        foreach (var module in data.Modules)
        {
            if (req.ModuleMinutes.TryGetValue(module.Id, out var minutes))
                module.Minutes = Math.Max(1, minutes / 60);
        }
    }

    private static void AnnotatePlan(CoachData data, AssignmentProfile profile, AssignmentRequirements req)
    {
        var banner = $"[Адаптация: {req.ProjectName}, {req.ExamVariant ?? "—"}, {req.DomainLabel ?? req.ProductWord}]";
        foreach (var step in data.Steps.Where(s => s.Id.StartsWith("setup-", StringComparison.Ordinal)))
        {
            if (!step.VsHint.Contains(banner, StringComparison.Ordinal))
                step.VsHint = banner + "\n" + step.VsHint;
        }

        if (profile.ExcludedStepIds?.Count > 0)
        {
            var setup = data.Steps.FirstOrDefault(s => s.Id == "setup-3");
            if (setup != null)
                setup.VsHint += $"\n\nНе создавайте папки/файлы для исключённых шагов ({profile.ExcludedStepIds.Count} шт.).";
        }
    }

    private static void RebuildModuleIndices(CoachData data)
    {
        foreach (var module in data.Modules)
            module.StepIndices.Clear();

        for (var i = 0; i < data.Steps.Count; i++)
        {
            var mod = data.Modules.FirstOrDefault(m => m.Id == data.Steps[i].Module);
            mod?.StepIndices.Add(i);
        }
    }
}
