namespace ExamCoachDesktop;

/// <summary>Текстовые замены в коде шагов по требованиям ТЗ (без структурной хирургии).</summary>
public static class AssignmentTextReplacements
{
    public static List<TextReplacement> Build(AssignmentRequirements req)
    {
        var list = new List<TextReplacement>();

        if (string.Equals(req.ExamVariant, "BU", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(req.AssignmentKind, "Kod", StringComparison.OrdinalIgnoreCase))
            list.AddRange(BuVariantReplacements(req));

        if (req.Features.Contains(AssignmentFeatures.Orders))
            list.AddRange(OrdersEnabledReplacements());

        if (!req.Features.Contains(AssignmentFeatures.Orders))
            list.AddRange(DisableOrdersReplacements());

        return list
            .Where(r => !string.IsNullOrEmpty(r.From))
            .GroupBy(r => r.From, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderByDescending(r => r.From.Length)
            .ToList();
    }

    private static IEnumerable<TextReplacement> BuVariantReplacements(AssignmentRequirements req)
    {
        yield return new TextReplacement { From = "\"Variant\": \"PU\"", To = "\"Variant\": \"BU\"" };
        yield return new TextReplacement { From = "Variant { get; set; } = \"PU\"", To = "Variant { get; set; } = \"BU\"" };
        yield return new TextReplacement { From = "ShopName { get; set; } = \"ЧитайГород\"", To = "ShopName { get; set; } = \"ВелосипедДрайв\"" };
        yield return new TextReplacement { From = "FontFamily { get; set; } = \"Comic Sans MS, cursive\"", To = "FontFamily { get; set; } = \"Arial, sans-serif\"" };
        yield return new TextReplacement { From = "SecondaryColor { get; set; } = \"#ABCFCE\"", To = "SecondaryColor { get; set; } = \"#6A5ACD\"" };
        yield return new TextReplacement { From = "AccentColor { get; set; } = \"#546F94\"", To = "AccentColor { get; set; } = \"#4B0082\"" };
        yield return new TextReplacement { From = "DiscountHighlightPercent { get; set; } = 25m", To = "DiscountHighlightPercent { get; set; } = 15m" };
        yield return new TextReplacement { From = "DiscountHighlightColor { get; set; } = \"#23E1EF\"", To = "DiscountHighlightColor { get; set; } = \"#483D8B\"" };
        yield return new TextReplacement { From = "#23E1EF", To = "#483D8B" };
        yield return new TextReplacement { From = "#23e1ef", To = "#483D8B" };
        yield return new TextReplacement { From = "settings.DiscountHighlightPercent = 25m", To = "settings.DiscountHighlightPercent = 15m" };
        yield return new TextReplacement { From = "settings.DiscountHighlightColor = \"#23E1EF\"", To = "settings.DiscountHighlightColor = \"#483D8B\"" };
        yield return new TextReplacement { From = "settings.ShopName = \"ЧитайГород\"", To = "settings.ShopName = \"ВелосипедДрайв\"" };
        yield return new TextReplacement { From = "settings.FontFamily = \"Comic Sans MS, cursive\"", To = "settings.FontFamily = \"Arial, sans-serif\"" };
        yield return new TextReplacement { From = "settings.SecondaryColor = \"#ABCFCE\"", To = "settings.SecondaryColor = \"#6A5ACD\"" };
        yield return new TextReplacement { From = "settings.AccentColor = \"#546F94\"", To = "settings.AccentColor = \"#4B0082\"" };
        yield return new TextReplacement { From = "// Профильный уровень (PU): книжный магазин «ЧитайГород», заказы включены.", To = "// Базовый уровень (BU): велосипеды «ВелосипедДрайв»." };
        yield return new TextReplacement { From = "// Базовый уровень (BU): велосипеды, другая палитра, заказы отключены.", To = req.Features.Contains(AssignmentFeatures.Orders) ? "// Базовый уровень (BU): велосипеды, заказы по ТЗ." : "// Базовый уровень (BU): велосипеды, заказы отключены." };
        yield return new TextReplacement { From = "new(\"0–12,99%\", 0m, 12.99m)", To = "new(\"0–11,99%\", 0m, 11.99m)" };
        yield return new TextReplacement { From = "new(\"13–16,99%\", 13m, 16.99m)", To = "new(\"12–18,99%\", 12m, 18.99m)" };
        yield return new TextReplacement { From = "new(\"17% и более\", 17m, null)", To = "new(\"19% и более\", 19m, null)" };
        yield return new TextReplacement { From = "<option value=\"0-12.99\">0–12,99%</option>", To = "<option value=\"0-11.99\">0–11,99%</option>" };
        yield return new TextReplacement { From = "<option value=\"13-16.99\">13–16,99%</option>", To = "<option value=\"12-18.99\">12–18,99%</option>" };
        yield return new TextReplacement { From = "<option value=\"17+\">17% и более</option>", To = "<option value=\"19+\">19% и более</option>" };
    }

    private static IEnumerable<TextReplacement> OrdersEnabledReplacements()
    {
        yield return new TextReplacement { From = "OrdersEnabled { get; set; } = false", To = "OrdersEnabled { get; set; } = true" };
        yield return new TextReplacement { From = "settings.OrdersEnabled = false;", To = "settings.OrdersEnabled = true;" };
        yield return new TextReplacement { From = "settings.OrdersEnabled = false", To = "settings.OrdersEnabled = true" };
        yield return new TextReplacement { From = "OrdersEnabled=false", To = "OrdersEnabled=true" };
        yield return new TextReplacement { From = "заказы отключены", To = "заказы включены по ТЗ" };
    }

    private static IEnumerable<TextReplacement> DisableOrdersReplacements()
    {
        yield return new TextReplacement { From = "OrdersEnabled { get; set; } = true", To = "OrdersEnabled { get; set; } = false" };
        yield return new TextReplacement { From = "settings.OrdersEnabled = true", To = "settings.OrdersEnabled = false" };
    }
}
