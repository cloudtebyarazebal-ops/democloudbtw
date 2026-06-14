namespace ExamCoachDesktop;

/// <summary>Патчи ПУ/БУ и текстовые замены по требованиям ТЗ.</summary>
public static class ExamVariantAdapter
{
    public static void ApplyToData(CoachData data, AssignmentProfile profile)
    {
        if (profile.Requirements != null)
            TextAdaptEngine.AdaptInPlace(data, AssignmentTextReplacements.Build(profile.Requirements));
        else if (!string.IsNullOrEmpty(profile.ExamVariant) &&
                 profile.ExamVariant.Equals("BU", StringComparison.OrdinalIgnoreCase))
            TextAdaptEngine.AdaptInPlace(data, BuLegacyReplacements());

        if (string.IsNullOrEmpty(profile.ExamVariant)) return;

        var variant = profile.ExamVariant.ToUpperInvariant();
        var isBu = variant == "BU";
        AnnotateModules(data, isBu, profile);
        AnnotateSetupSteps(data, variant, profile);
        AnnotateKeyFileSteps(data, variant, profile, profile.Requirements);
    }

    public static List<TextReplacement> BuildReplacementList(AssignmentProfile profile)
    {
        if (profile.Requirements != null)
            return AssignmentTextReplacements.Build(profile.Requirements);

        if (string.IsNullOrEmpty(profile.ExamVariant))
            return profile.Replacements.ToList();

        return profile.ExamVariant.Equals("BU", StringComparison.OrdinalIgnoreCase)
            ? BuLegacyReplacements()
            : [];
    }

    private static List<TextReplacement> BuLegacyReplacements() =>
        AssignmentTextReplacements.Build(new AssignmentRequirements { ExamVariant = "BU" });

    private static void AnnotateModules(CoachData data, bool isBu, AssignmentProfile profile)
    {
        var ordersInTz = profile.Requirements?.Features.Contains(AssignmentFeatures.Orders) == true;

        foreach (var module in data.Modules)
        {
            if (module.Id != "m4") continue;
            module.Description = isBu
                ? ordersInTz
                    ? "Orders — заказы по ТЗ (BU, OrdersEnabled=true)"
                    : "Orders — модуль заказов (BU: OrdersEnabled=false)"
                : "Orders (вариант ПУ — полный модуль заказов)";
        }

        if (isBu)
        {
            var m2 = data.Modules.FirstOrDefault(m => m.Id == "m2");
            if (m2 != null)
                m2.Description = "Program + Account + Products — базовый уровень (велосипеды)";
        }
        else if (!string.IsNullOrEmpty(profile.DomainDescription))
        {
            var m2 = data.Modules.FirstOrDefault(m => m.Id == "m2");
            if (m2 != null)
                m2.Description = $"Program + Login + каталог — {profile.DomainDescription}";
        }
    }

    private static void AnnotateSetupSteps(CoachData data, string variant, AssignmentProfile profile)
    {
        var domain = profile.DomainDescription ?? (variant == "BU" ? "продажа велосипедов" : "продажа книг");
        var banner = $"[Задание: {variant}, {domain}]";

        foreach (var step in data.Steps.Where(s => s.Id.StartsWith("setup-", StringComparison.Ordinal)))
        {
            if (!step.VsHint.Contains(banner, StringComparison.Ordinal))
                step.VsHint = $"{banner}\n{step.VsHint}";
        }
    }

    private static void AnnotateKeyFileSteps(CoachData data, string variant, AssignmentProfile profile, AssignmentRequirements? req)
    {
        var appsettings = data.Steps.FirstOrDefault(s => s.Id == "file-appsettings-json");
        if (appsettings != null)
        {
            appsettings.VsHint = variant == "BU"
                ? $"appsettings.json — \"Variant\": \"BU\" (велосипеды).\n{appsettings.VsHint}"
                : $"appsettings.json — \"Variant\": \"PU\" (книги).\n{appsettings.VsHint}";
        }

        var shopSettings = data.Steps.FirstOrDefault(s =>
            s.Title.Contains("ShopSettings", StringComparison.OrdinalIgnoreCase) ||
            s.Code.Contains("class ShopSettings", StringComparison.Ordinal));

        if (shopSettings != null && variant == "BU")
        {
            var ordersNote = req?.Features.Contains(AssignmentFeatures.Orders) == true
                ? "OrdersEnabled=true (заказы в ТЗ)."
                : "OrdersEnabled=false.";
            shopSettings.VsHint =
                $"ShopSettings: пресет BU → «ВелосипедДрайв», скидка >15% → #483D8B, {ordersNote}\n" +
                shopSettings.VsHint;
        }

        if (variant == "BU")
        {
            foreach (var step in data.Steps.Where(s => s.VsHint.Contains("(PU module)", StringComparison.OrdinalIgnoreCase)))
                step.VsHint = step.VsHint.Replace("(PU module)", "(BU)", StringComparison.OrdinalIgnoreCase);
        }
    }
}
