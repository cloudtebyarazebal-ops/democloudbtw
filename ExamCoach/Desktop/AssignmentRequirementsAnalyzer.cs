using System.Text.RegularExpressions;

namespace ExamCoachDesktop;

public static class AssignmentRequirementsAnalyzer
{
    public static AssignmentRequirements Analyze(string? assignmentText, AssignmentProfile profile)
    {
        var req = new AssignmentRequirements
        {
            AssignmentKind = profile.AssignmentKind,
            ExamVariant = profile.ExamVariant,
            DomainLabel = profile.DomainDescription
        };

        var text = (assignmentText ?? profile.SourceText ?? "").Replace('\r', '\n');
        var lower = text.ToLowerInvariant();

        DetectProjectName(text, req);
        DetectDomainWords(lower, req);
        DetectFeatures(text, lower, req);
        DetectModules(text, req);
        DetectModuleMinutes(text, req);
        EnsureDefaultModules(req);
        EnsureCoreFeatures(req);
        req.SeedData = AssignmentSeedExtractor.Extract(text, req);
        return req;
    }

    private static void DetectProjectName(string text, AssignmentRequirements req)
    {
        var patterns = new[]
        {
            @"dotnet\s+new\s+\w+\s+-n\s+(\w+)",
            @"Name:\s*(\w+)",
            @"(?:имя|название)\s+проекта\s*[:\-—]?\s*(\w+)"
        };

        foreach (var p in patterns)
        {
            var m = Regex.Match(text, p, RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            var name = m.Groups[1].Value;
            if (name.Length >= 3 && char.IsLetter(name[0]))
            {
                req.ProjectName = name;
                req.BrandName = name.EndsWith("Web", StringComparison.OrdinalIgnoreCase) && name.Length > 3
                    ? name[..^3]
                    : name;
                return;
            }
        }
    }

    private static void DetectDomainWords(string lower, AssignmentRequirements req)
    {
        if (lower.Contains("книг"))
            req.ProductWord = "книга";
        else if (lower.Contains("велосипед"))
            req.ProductWord = "велосипед";
        else if (lower.Contains("настольн") || lower.Contains("пазл") || Regex.IsMatch(lower, @"\bигр\b"))
            req.ProductWord = "настольная игра";
        else if (lower.Contains("смартфон") || (lower.Contains("мобильн") && lower.Contains("аксессуар")))
            req.ProductWord = "смартфон";
        else if (lower.Contains("комплектующ") || lower.Contains("процессор") || lower.Contains("видеокарт") || lower.Contains("материнск"))
            req.ProductWord = "комплектующие";
    }

    private static void DetectFeatures(string text, string lower, AssignmentRequirements req)
    {
        if (Regex.IsMatch(lower, @"авториз|логин|парол|вход"))
            req.Features.Add(AssignmentFeatures.Auth);
        if (Regex.IsMatch(lower, @"\bгост"))
            req.Features.Add(AssignmentFeatures.Guest);
        if (Regex.IsMatch(lower, @"импорт|import|tovar\.xlsx|user_import|components\.xlsx|suppliers_import|прил.*2"))
            req.Features.Add(AssignmentFeatures.Import);
        if (Regex.IsMatch(lower, @"\bзаказ"))
            req.Features.Add(AssignmentFeatures.Orders);
        if (Regex.IsMatch(lower, @"добавл.*товар|редакт.*товар|удал.*товар|\bcrud\b"))
            req.Features.Add(AssignmentFeatures.ProductCrud);
        if (Regex.IsMatch(lower, @"фильтрац|диапазон.*скид"))
            req.Features.Add(AssignmentFeatures.ProductFilters);
        if (Regex.IsMatch(lower, @"\bпоиск"))
            req.Features.Add(AssignmentFeatures.ProductSearch);
        if (Regex.IsMatch(lower, @"сортир"))
            req.Features.Add(AssignmentFeatures.ProductSort);
        if (Regex.IsMatch(lower, @"er[\s-]?диаграм"))
            req.Features.Add(AssignmentFeatures.ErDiagram);
        if (Regex.IsMatch(lower, @"исключ|сообщен.*ошиб|предупрежд"))
            req.Features.Add(AssignmentFeatures.ErrorHandling);
        if (Regex.IsMatch(lower, @"последовательн|кнопк.*назад"))
            req.Features.Add(AssignmentFeatures.SequentialNav);
        if (Regex.IsMatch(lower, @"скидк|#23e1ef|#483d8b|#a569bd|483d8b|23e1ef|a569bd"))
            req.Features.Add(AssignmentFeatures.DiscountUi);

        if (Regex.IsMatch(lower, @"пункт.*выдач|pickuppoint|ближайш.*пункт|код получ"))
            req.Features.Add(AssignmentFeatures.PickupPoints);
        if (Regex.IsMatch(lower, @"дата доставки|deliverydate|срок достав"))
            req.Features.Add(AssignmentFeatures.OrderDelivery);
        if (Regex.IsMatch(lower, @"\bзаказ"))
        {
            if (Regex.IsMatch(lower, @"статус.*заказ|заказ.*статус|orderstatus|«новый»|«заверш|новый.*заверш"))
                req.Features.Add(AssignmentFeatures.OrderStatusDetail);
            else if (Regex.IsMatch(text, @"●\s*статус", RegexOptions.IgnoreCase))
                req.Features.Add(AssignmentFeatures.OrderStatusDetail);
        }

        if (DetectAuthorField(text, lower))
            req.Features.Add(AssignmentFeatures.AuthorField);
    }

    private static bool DetectAuthorField(string text, string lower)
    {
        var hasProducerField = Regex.IsMatch(text, @"производител", RegexOptions.IgnoreCase);
        var hasExplicitAuthorField = Regex.IsMatch(
            text,
            @"(?:●|-|•|\t)\s*автор\s*(?:;|,|\r?\n|$)|\bавтор\s*;|пол[ейя]\s+товар[\s\S]{0,300}?●\s*автор",
            RegexOptions.IgnoreCase);

        // «автор» внутри «администратор» / «авторизация» — не поле товара.
        if (hasProducerField && !hasExplicitAuthorField)
            return false;

        if (hasExplicitAuthorField)
            return true;

        // Книжный домен: поле «автор» без «производитель».
        if (lower.Contains("книг") && !hasProducerField &&
            Regex.IsMatch(text, @"(?<![а-яё])автор(?![а-яёa-z])", RegexOptions.IgnoreCase))
            return true;

        return false;
    }

    private static void DetectModules(string text, AssignmentRequirements req)
    {
        foreach (Match m in Regex.Matches(text, @"Модуль\s*№\s*(\d)", RegexOptions.IgnoreCase))
            req.Modules.Add($"m{m.Groups[1].Value}");
    }

    private static void DetectModuleMinutes(string text, AssignmentRequirements req)
    {
        foreach (Match m in Regex.Matches(text,
                     @"Модуль\s*№\s*(\d)[\s\S]{0,120}?(\d+)\s*ч[\s\S]{0,20}?(\d+)\s*мин",
                     RegexOptions.IgnoreCase))
        {
            var moduleId = $"m{m.Groups[1].Value}";
            var minutes = int.Parse(m.Groups[2].Value) * 60 + int.Parse(m.Groups[3].Value);
            req.ModuleMinutes[moduleId] = minutes;
        }
    }

    private static void EnsureDefaultModules(AssignmentRequirements req)
    {
        if (req.Modules.Count > 0) return;
        req.Modules.Add("m1");
        req.Modules.Add("m2");
        req.Modules.Add("m3");
    }

    private static void EnsureCoreFeatures(AssignmentRequirements req)
    {
        req.Features.Add(AssignmentFeatures.Auth);
        req.Features.Add(AssignmentFeatures.Guest);

        if (req.Modules.Contains("m1"))
            req.Features.Add(AssignmentFeatures.ErDiagram);

        if (string.Equals(req.ExamVariant, "PU", StringComparison.OrdinalIgnoreCase) ||
            req.Features.Contains(AssignmentFeatures.AuthorField) ||
            string.Equals(req.ExamVariant, "BU", StringComparison.OrdinalIgnoreCase))
            req.Features.Add(AssignmentFeatures.DiscountUi);

        if (req.Features.Contains(AssignmentFeatures.Orders))
            req.Modules.Add("m4");

        // Стандартный КОД (ПУ/БУ): модуль заказов всегда с ПВЗ, статусом и датой выдачи.
        if (string.Equals(req.AssignmentKind, "Kod", StringComparison.OrdinalIgnoreCase) &&
            req.Features.Contains(AssignmentFeatures.Orders))
        {
            req.Features.Add(AssignmentFeatures.PickupPoints);
            req.Features.Add(AssignmentFeatures.OrderDelivery);
            req.Features.Add(AssignmentFeatures.OrderStatusDetail);
        }
    }
}
