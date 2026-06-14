using System.Text.RegularExpressions;

namespace ExamCoachDesktop;

/// <summary>Извлекает из PDF/текста задания КОД (ПУ/БУ) и учебные ТЗ магазина.</summary>
public static class AssignmentTextParser
{
    private const string DefaultProject = "KodShopWeb";

    public static AssignmentProfile Parse(string assignmentText, IReadOnlyList<TextReplacement>? existing = null)
    {
        var profile = new AssignmentProfile { SourceText = assignmentText };
        var text = Normalize(assignmentText);

        var exam = DetectKodExamAssignment(text);
        if (exam != null)
            return FinalizeProfile(profile, assignmentText, exam, existing);

        var custom = DetectCustomAssignment(text);
        if (custom != null)
            return FinalizeProfile(profile, assignmentText, custom, existing);

        return FinalizeManualProfile(profile, text, existing);
    }

    private static AssignmentProfile FinalizeProfile(
        AssignmentProfile profile,
        string assignmentText,
        ParsedAssignment parsed,
        IReadOnlyList<TextReplacement>? existing)
    {
        profile.ExamVariant = parsed.Variant;
        profile.DomainDescription = parsed.DomainLabel;
        profile.ExamCipher = parsed.Cipher;
        profile.ExamLevel = parsed.Level;
        profile.AssignmentTitle = parsed.Title;
        profile.AssignmentKind = parsed.Kind;
        profile.Requirements = AssignmentRequirementsAnalyzer.Analyze(assignmentText, profile);
        profile.Replacements = BuildAllReplacements(profile, existing);
        profile.AdaptationSummary = null;
        return profile;
    }

    private static AssignmentProfile FinalizeManualProfile(
        AssignmentProfile profile,
        string text,
        IReadOnlyList<TextReplacement>? existing)
    {
        var replacements = new List<TextReplacement>();
        if (existing != null)
            replacements.AddRange(existing.Where(r => !string.IsNullOrWhiteSpace(r.From)));

        var projectName = DetectProjectName(text);
        if (!string.IsNullOrEmpty(projectName) &&
            !projectName.Equals(DefaultProject, StringComparison.OrdinalIgnoreCase))
        {
            AddReplacement(replacements, DefaultProject, projectName);
            AddReplacement(replacements, "KodShop", TrimProjectSuffix(projectName));
            profile.AssignmentTitle = projectName;
        }

        DetectQuotedNames(text, replacements);
        DetectEntityRenames(text, replacements);

        profile.Replacements = replacements
            .GroupBy(r => r.From, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(r => r.From.Length)
            .ToList();

        return profile;
    }

    private static List<TextReplacement> BuildAllReplacements(AssignmentProfile profile, IReadOnlyList<TextReplacement>? existing)
    {
        var list = new List<TextReplacement>();
        list.AddRange(ExamVariantAdapter.BuildReplacementList(profile));

        if (profile.Requirements != null && AuthorDomainPatches.IsApplicable(profile.Requirements))
            list.AddRange(AuthorDomainPatches.GetReplacements());

        if (existing != null)
        {
            foreach (var r in existing.Where(r => !string.IsNullOrWhiteSpace(r.From)))
            {
                if (list.All(a => !a.From.Equals(r.From, StringComparison.OrdinalIgnoreCase)))
                    list.Add(r);
            }
        }

        return list
            .GroupBy(r => r.From, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderByDescending(r => r.From.Length)
            .ToList();
    }

    public static string DescribeExam(AssignmentProfile profile)
    {
        if (!string.IsNullOrEmpty(profile.ExamVariant))
        {
            var domain = profile.DomainDescription ?? "магазин";
            var level = profile.ExamLevel ?? (profile.ExamVariant == "PU" ? "Профильный" : "Базовый");
            var kind = profile.AssignmentKind == "Custom" ? "учебное ТЗ" : "демоэкзамен КОД";
            var changes = profile.ExamVariant == "BU"
                ? "BU: велосипеды, ShopSettings, адаптация шагов."
                : "PU: книги, ShopSettings, адаптация шагов.";
            return $"{profile.ExamVariant} — {domain} ({level}, {kind}). {changes}";
        }

        if (profile.Replacements.Count > 0)
            return $"Найдено замен в коде: {profile.Replacements.Count}.";

        return "Текст загружен. Автоматически ничего не распознано — добавьте замены вручную.";
    }

    private static string Normalize(string text) =>
        text.Replace('\r', '\n')
            .Replace('\u00A0', ' ')
            .Replace('\u2011', '-')
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .Replace('‑', '-')
            .Replace('–', '-');

    /// <summary>Стандартное задание КОД (Компания занимается продажей…).</summary>
    private static ParsedAssignment? DetectKodExamAssignment(string text)
    {
        var domainMatch = Regex.Match(text,
            @"Компания\s+занимается\s+продаж(?:ей|а)\s+([^.!\n]+)",
            RegexOptions.IgnoreCase);
        var domainRaw = domainMatch.Success ? domainMatch.Groups[1].Value.Trim() : null;

        var levelMatch = Regex.Match(text, @"(Профильный|Базовый)\s+уровень", RegexOptions.IgnoreCase);
        var level = levelMatch.Success ? levelMatch.Groups[1].Value : null;

        var cipherMatch = Regex.Match(text,
            @"Шифр\s+варианта\s+задания\s*:?\s*([^\n]+)",
            RegexOptions.IgnoreCase);
        var cipher = cipherMatch.Success ? cipherMatch.Groups[1].Value.Trim().TrimStart(':').Trim() : null;

        var variant = DetectVariantFromCipher(cipher)
                      ?? DetectVariantFromLevel(level)
                      ?? DetectVariantFromDomain(domainRaw);

        if (variant == null && domainRaw == null && cipher == null)
            return null;

        var domainLabel = FormatDomainLabel(domainRaw);
        var title = BuildTitle(variant, domainLabel, cipher);

        return new ParsedAssignment(variant ?? "PU", domainRaw, domainLabel, level, cipher, title, "Kod");
    }

    /// <summary>Учебное ТЗ (магазин, роли, БД) — без формата КОД, но с ПУ/БУ по предметной области.</summary>
    private static ParsedAssignment? DetectCustomAssignment(string text)
    {
        var lower = text.ToLowerInvariant();

        var looksLikeShop =
            Regex.IsMatch(lower, @"предметная\s+область|минимальный\s+состав\s+полей|баз[aы]\s+данн", RegexOptions.IgnoreCase) ||
            (Regex.IsMatch(lower, @"\bмагазин\b|\bтовар|\bзаказ") &&
             Regex.IsMatch(lower, @"авториз|рол|гост|менеджер|администратор|crud|er[\s-]?диаграм", RegexOptions.IgnoreCase));

        if (!looksLikeShop)
            return null;

        var domainRaw = DetectCustomDomainRaw(text, lower);
        var shopName = DetectShopBrandName(text);

        var variant = DetectVariantFromDomain(domainRaw)
                      ?? (domainRaw?.Contains("книг", StringComparison.OrdinalIgnoreCase) == true ? "PU" : null)
                      ?? (domainRaw?.Contains("велосипед", StringComparison.OrdinalIgnoreCase) == true ? "BU" : null);

        if (variant == null && domainRaw == null)
            return null;

        var domainLabel = FormatDomainLabel(domainRaw);
        if (shopName != null && domainRaw?.Contains("книг") == true)
            domainLabel = $"магазин «{shopName}»";

        var variantLabel = variant ?? "NO-PRESET";
        var title = shopName != null
            ? $"Учебное ТЗ — {shopName} ({variantLabel}, {domainLabel})"
            : $"Учебное ТЗ ({variantLabel}, {domainLabel})";

        return new ParsedAssignment(variant, domainRaw, domainLabel, null, null, title, "Custom");
    }

    private static string? DetectCustomDomainRaw(string text, string lower)
    {
        if (Regex.IsMatch(lower, @"магазин\s+книг|прода[её]т\s+книг|книг"))
            return "книг";
        if (Regex.IsMatch(lower, @"велосипед"))
            return "велосипедов";
        if (Regex.IsMatch(lower, @"смартфон|аксессуар|мобильн"))
            return "смартфонов";

        var m = Regex.Match(text, @"магазин\s+(\w+)", RegexOptions.IgnoreCase);
        if (m.Success && m.Groups[1].Value.Contains("книг", StringComparison.OrdinalIgnoreCase))
            return "книг";

        return null;
    }

    private static string? DetectShopBrandName(string text)
    {
        var m = Regex.Match(text, @"[«""']([^«""']{2,40})[«""']", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var name = m.Groups[1].Value.Trim();
            if (!name.Contains(' ') || name.Length <= 25)
                return name;
        }
        return null;
    }

    private static string? DetectVariantFromCipher(string? cipher)
    {
        if (string.IsNullOrWhiteSpace(cipher)) return null;

        // Явный ПУ/БУ в шифре важнее маркеров В1/В2 (например «В2-ПУ-ПК» → PU).
        if (Regex.IsMatch(cipher, @"(?:^|[-_\s])(?:ПУ|PU)(?:\b|$)", RegexOptions.IgnoreCase)) return "PU";
        if (Regex.IsMatch(cipher, @"(?:^|[-_\s])(?:БУ|BU)(?:\b|$)", RegexOptions.IgnoreCase)) return "BU";
        if (Regex.IsMatch(cipher, @"\bВ1\b", RegexOptions.IgnoreCase)) return "PU";
        if (Regex.IsMatch(cipher, @"\bВ2\b", RegexOptions.IgnoreCase)) return "BU";
        return null;
    }

    private static string? DetectVariantFromLevel(string? level)
    {
        if (string.IsNullOrWhiteSpace(level)) return null;
        if (level.Contains("Профиль", StringComparison.OrdinalIgnoreCase)) return "PU";
        if (level.Contains("Базов", StringComparison.OrdinalIgnoreCase)) return "BU";
        return null;
    }

    private static string? DetectVariantFromDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return null;
        if (domain.Contains("книг", StringComparison.OrdinalIgnoreCase)) return "PU";
        if (domain.Contains("велосипед", StringComparison.OrdinalIgnoreCase)) return "BU";
        if (domain.Contains("смартфон", StringComparison.OrdinalIgnoreCase)) return "PU";
        return null;
    }

    private static string FormatDomainLabel(string? domainRaw)
    {
        if (string.IsNullOrWhiteSpace(domainRaw)) return "магазин";
        if (domainRaw.Contains("книг", StringComparison.OrdinalIgnoreCase)) return "продажа книг";
        if (domainRaw.Contains("велосипед", StringComparison.OrdinalIgnoreCase)) return "продажа велосипедов";
        if (domainRaw.Contains("смартфон", StringComparison.OrdinalIgnoreCase)) return "продажа смартфонов";
        return "продажа " + domainRaw;
    }

    private static string BuildTitle(string? variant, string domainLabel, string? cipher)
    {
        var v = variant ?? "?";
        var cipherShort = cipher;
        if (!string.IsNullOrEmpty(cipherShort) && cipherShort.Length > 40)
            cipherShort = cipherShort[..40] + "…";
        return string.IsNullOrEmpty(cipherShort)
            ? $"{v} — {domainLabel}"
            : $"{cipherShort} ({v}, {domainLabel})";
    }

    private static string? DetectProjectName(string text)
    {
        var patterns = new[]
        {
            @"(?:имя\s+проекта|название\s+проекта|project\s+name|проект)\s*[:\-—]?\s*[«""']?(\w+)",
            @"(?:приложение|application|система)\s+[«""'](\w+)[»""']",
            @"dotnet\s+new\s+\w+\s+-n\s+(\w+)",
            @"ASP\.NET\s+Core[^.]*\.?\s*(\w{3,})"
        };

        foreach (var p in patterns)
        {
            var m = Regex.Match(text, p, RegexOptions.IgnoreCase);
            if (m.Success && IsValidName(m.Groups[1].Value))
                return m.Groups[1].Value;
        }
        return null;
    }

    private static void DetectQuotedNames(string text, List<TextReplacement> replacements)
    {
        var ns = Regex.Match(text, @"(?:namespace|пространство\s+имён)\s+(\w+(?:\.\w+)*)", RegexOptions.IgnoreCase);
        if (!ns.Success) return;

        var root = ns.Groups[1].Value.Split('.')[0];
        if (IsValidName(root) && !root.Equals(DefaultProject, StringComparison.OrdinalIgnoreCase))
            AddReplacement(replacements, DefaultProject, root);
    }

    private static void DetectEntityRenames(string text, List<TextReplacement> replacements)
    {
        var pairs = new (string exam, string[] keywords)[]
        {
            ("Product", ["товар", "product", "изделие", "номенклатур"]),
            ("Order", ["заказ", "order", "покуп"]),
            ("User", ["пользовател", "user", "клиент", "admin"]),
            ("Shop", ["магазин", "shop", "склад"])
        };

        foreach (var (defaultName, keywords) in pairs)
        {
            foreach (var kw in keywords)
            {
                var m = Regex.Match(text,
                    $@"(?:сущность|entity|таблиц|class|модел)\s+[«""']?(\w*{kw}\w*)[»""']?",
                    RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                var newName = ToPascalCase(m.Groups[1].Value);
                if (string.IsNullOrEmpty(newName) || newName.Equals(defaultName, StringComparison.OrdinalIgnoreCase))
                    continue;
                AddReplacement(replacements, defaultName, newName);
                break;
            }
        }
    }

    private static void AddReplacement(List<TextReplacement> list, string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) return;
        if (from.Equals(to, StringComparison.OrdinalIgnoreCase)) return;
        if (list.Any(r => r.From.Equals(from, StringComparison.OrdinalIgnoreCase))) return;
        list.Add(new TextReplacement { From = from, To = to });
    }

    private static string TrimProjectSuffix(string name) =>
        name.EndsWith("Web", StringComparison.OrdinalIgnoreCase) && name.Length > 3
            ? name[..^3]
            : name;

    private static string ToPascalCase(string value)
    {
        var letters = new string(value.Where(char.IsLetterOrDigit).ToArray());
        if (letters.Length == 0) return "";
        return char.ToUpper(letters[0]) + letters[1..];
    }

    private static bool IsValidName(string name) =>
        name.Length >= 2 && char.IsLetter(name[0]) && name.All(c => char.IsLetterOrDigit(c) || c == '_');

    private sealed record ParsedAssignment(
        string? Variant,
        string? DomainRaw,
        string DomainLabel,
        string? Level,
        string? Cipher,
        string Title,
        string Kind);
}
