namespace ExamCoachDesktop;

public static class DomainContentPatches
{
    public static IReadOnlyList<TextReplacement> GetDomainReplacements(AssignmentRequirements req)
    {
        if (!string.Equals(req.AssignmentKind, "Kod", StringComparison.OrdinalIgnoreCase))
            return [];

        if (req.ExamVariant?.Equals("BU", StringComparison.OrdinalIgnoreCase) == true)
            return BicycleDomainReplacements();

        if (req.ProductWord.Contains("книг", StringComparison.OrdinalIgnoreCase))
            return [];

        return [];
    }

    public static void Apply(CoachData data, AssignmentRequirements req)
    {
        var replacements = GetDomainReplacements(req);
        if (replacements.Count > 0)
            TextAdaptEngine.AdaptInPlace(data, replacements);
    }

    private static List<TextReplacement> BicycleDomainReplacements() =>
    [
        new() { From = "new Category { Name = \"Роман\" }", To = "new Category { Name = \"Горный\" }" },
        new() { From = "new Category { Name = \"Хрестоматия\" }", To = "new Category { Name = \"Шоссейный\" }" },
        new() { From = "new Category { Name = \"Учебник\" }", To = "new Category { Name = \"Городской\" }" },
        new() { From = "new Manufacturer { Name = \"АСТ\" }", To = "new Manufacturer { Name = \"Trek\" }" },
        new() { From = "new Manufacturer { Name = \"Эксмо\" }", To = "new Manufacturer { Name = \"Giant\" }" },
        new() { From = "new Manufacturer { Name = \"Просвещение\" }", To = "new Manufacturer { Name = \"Stels\" }" },
        new() { From = "new Supplier { Name = \"ООО «Книжный склад»\" }", To = "new Supplier { Name = \"ООО «ВелоСклад»\" }" },
        new() { From = "Name = \"Прокляты и убиты\"", To = "Name = \"Trail X 29\"" },
        new() { From = "Description = \"Роман-эпопея Виктора Астафьева.\"", To = "Description = \"Горный велосипед для бездорожья.\"" },
        new() { From = "Name = \"Хрестоматия по литературе\"", To = "Name = \"Speed Pro\"" },
        new() { From = "Description = \"Сборник для старших классов.\"", To = "Description = \"Шоссейный велосипед.\"" },
        new() { From = "Name = \"Основы программирования\"", To = "Name = \"City Comfort\"" },
        new() { From = "Description = \"Учебник для СПО.\"", To = "Description = \"Городской велосипед.\"" },
        new() { From = "Name = \"Тихий Дон\"", To = "Name = \"Mountain Fury\"" },
        new() { From = "Description = \"Классика русской литературы.\"", To = "Description = \"Гибридный велосипед.\"" },
        new() { From = "// --- Демонстрационный каталог товаров ---", To = "// --- Демонстрационный каталог велосипедов ---" },
        new() { From = "книжный магазин", To = "магазин велосипедов" },
        new() { From = "книги)", To = "велосипеды)" },
    ];
}
