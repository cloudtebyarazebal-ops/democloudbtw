using System.Text.RegularExpressions;

namespace ExamCoachDesktop;

/// <summary>Извлекает из текста ТЗ категории, производителей, пороги скидок и прочие данные для БД.</summary>
public static class AssignmentSeedExtractor
{
    public static AssignmentSeedData Extract(string text, AssignmentRequirements req)
    {
        var normalized = text.Replace('\r', '\n');
        var lower = normalized.ToLowerInvariant();

        var seed = new AssignmentSeedData
        {
            UseAuthorField = req.Features.Contains(AssignmentFeatures.AuthorField),
            ProductWord = req.ProductWord,
            DomainLabel = req.DomainLabel ?? req.ProductWord,
            MakerFieldLabel = req.Features.Contains(AssignmentFeatures.AuthorField) ? "Автор" : "Производитель",
            OrdersEnabled = req.Features.Contains(AssignmentFeatures.Orders)
        };

        ExtractCategories(normalized, lower, seed);
        ExtractMakers(normalized, lower, seed);
        ExtractSuppliers(normalized, lower, seed);
        ExtractDiscountUi(normalized, lower, seed);
        ExtractDiscountRanges(normalized, seed);
        ExtractShopName(normalized, seed);
        ExtractBranding(normalized, lower, seed);
        EnsureDomainDefaults(seed, req);
        BuildSampleProducts(seed);

        return seed;
    }

    private static void ExtractCategories(string text, string lower, AssignmentSeedData seed)
    {
        foreach (var line in text.Split('\n'))
        {
            var lineLower = line.ToLowerInvariant();
            if (!lineLower.Contains("категор"))
                continue;

            // Пропускаем строки полей CRUD-формы («категория (список), описание…»).
            if (lineLower.Contains("форма") || lineLower.Contains("добавления/редактирования")
                || (lineLower.Contains("название") && lineLower.Contains("описание"))
                || lineLower.Contains("фото (предпросмотр)"))
                continue;

            var quoted = ExtractQuotedValues(line);
            if (quoted.Count > 0)
            {
                seed.Categories.AddRange(quoted);
                continue;
            }

            var afterColon = Regex.Match(line, @":\s*(.+)$");
            if (!afterColon.Success)
                continue;

            // Только если после двоеточия есть кавычки или явный пример.
            if (!afterColon.Groups[1].Value.Contains('«') && !lineLower.Contains("например"))
                continue;

            foreach (var part in afterColon.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var name = CleanListItem(part);
                if (!string.IsNullOrWhiteSpace(name))
                    seed.Categories.Add(name);
            }
        }

        seed.Categories = Distinct(seed.Categories);
    }

    private static void ExtractMakers(string text, string lower, AssignmentSeedData seed)
    {
        var field = seed.UseAuthorField ? "автор" : "производител";

        foreach (var line in text.Split('\n'))
        {
            var lineLower = line.ToLowerInvariant();
            if (!lineLower.Contains(field))
                continue;
            if (seed.UseAuthorField && lineLower.Contains("авториз"))
                continue;

            if (lineLower.Contains("форма") || lineLower.Contains("добавления/редактирования")
                || (lineLower.Contains("название") && lineLower.Contains("описание")))
                continue;

            var quoted = ExtractQuotedValues(line);
            if (quoted.Count > 0)
            {
                seed.Makers.AddRange(quoted);
                continue;
            }

            var match = Regex.Match(line, $@"(?:{field})[^\n]*?\t([^\n]+)|(?:{field})[^\n]*?:\s*([^\n]+)",
                RegexOptions.IgnoreCase);
            if (!match.Success)
                continue;

            var raw = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var name = CleanListItem(part);
                if (!string.IsNullOrWhiteSpace(name) && !name.Contains(" и др", StringComparison.OrdinalIgnoreCase))
                    seed.Makers.Add(name);
            }
        }

        seed.Makers = Distinct(seed.Makers);
    }

    private static void ExtractSuppliers(string text, string lower, AssignmentSeedData seed)
    {
        foreach (var line in text.Split('\n'))
        {
            if (!line.Contains("поставщик", StringComparison.OrdinalIgnoreCase))
                continue;

            var quoted = ExtractQuotedValues(line);
            if (quoted.Count > 0)
            {
                seed.Suppliers.AddRange(quoted);
                continue;
            }

            var match = Regex.Match(line, @"поставщик[^\n]*?\t([^\n]+)|поставщик[^\n]*?:\s*([^\n]+)",
                RegexOptions.IgnoreCase);
            if (!match.Success)
                continue;

            var raw = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            var name = CleanListItem(raw);
            if (!string.IsNullOrWhiteSpace(name))
                seed.Suppliers.Add(name);
        }

        seed.Suppliers = Distinct(seed.Suppliers);
    }

    private static void ExtractDiscountUi(string text, string lower, AssignmentSeedData seed)
    {
        var percentMatch = Regex.Match(lower, @"скидк[^\n]{0,120}?(?:превышает|≥|>=)\s*(\d+(?:[.,]\d+)?)\s*%");
        if (percentMatch.Success)
        {
            var raw = percentMatch.Groups[1].Value.Replace(',', '.');
            if (decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var percent))
                seed.DiscountHighlightPercent = percent;
        }

        // Цвет подсветки скидки ищем в строках про скидку/подсветку, а не первый HEX в документе.
        var scopedColor = Regex.Match(text,
            @"скидк[^\n]{0,200}?(?:фон|подсвет|строк)[^\n]{0,120}?(#[0-9A-Fa-f]{6})",
            RegexOptions.IgnoreCase);
        if (scopedColor.Success)
        {
            seed.DiscountHighlightColor = scopedColor.Groups[1].Value.ToUpperInvariant();
            return;
        }

        var fallbackColor = Regex.Match(text,
            @"скидк[^\n]{0,160}?(#[0-9A-Fa-f]{6})",
            RegexOptions.IgnoreCase);
        if (fallbackColor.Success)
            seed.DiscountHighlightColor = fallbackColor.Groups[1].Value.ToUpperInvariant();
    }

    private static void ExtractDiscountRanges(string text, AssignmentSeedData seed)
    {
        var ranges = new List<DiscountRangeTemplate>();
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            var match = Regex.Match(trimmed,
                @"^(\d+(?:[.,]\d+)?)\s*[–-]\s*(\d+(?:[.,]\d+)?)\s*%");
            if (match.Success)
            {
                var min = ParseDecimal(match.Groups[1].Value);
                var max = ParseDecimal(match.Groups[2].Value);
                ranges.Add(new DiscountRangeTemplate
                {
                    Key = $"{min}-{max}",
                    Label = $"{min}–{max}%".Replace('.', ','),
                    Min = min,
                    Max = max
                });
                continue;
            }

            var reversed = Regex.Match(trimmed,
                @"^(\d+(?:[.,]\d+)?)\s*%\s*[–-]\s*(\d+(?:[.,]\d+)?)\s*%");
            if (reversed.Success)
            {
                var min = ParseDecimal(reversed.Groups[1].Value);
                var max = ParseDecimal(reversed.Groups[2].Value);
                ranges.Add(new DiscountRangeTemplate
                {
                    Key = $"{min}-{max}",
                    Label = $"{min}% – {max}%".Replace('.', ','),
                    Min = min,
                    Max = max
                });
                continue;
            }

            var plusMatch = Regex.Match(trimmed, @"^(\d+(?:[.,]\d+)?)\s*%\s*и\s*более");
            if (plusMatch.Success)
            {
                var min = ParseDecimal(plusMatch.Groups[1].Value);
                ranges.Add(new DiscountRangeTemplate
                {
                    Key = $"{min}+",
                    Label = $"{min}% и более".Replace('.', ','),
                    Min = min,
                    Max = null
                });
            }
        }

        if (ranges.Count >= 2)
            seed.DiscountRanges = ranges;
    }

    private static void ExtractShopName(string text, AssignmentSeedData seed)
    {
        var quoted = Regex.Match(text, @"магазин\s+[«""']([^«""']+)[«""']", RegexOptions.IgnoreCase);
        if (quoted.Success)
            seed.ShopName = quoted.Groups[1].Value.Trim();

        if (string.IsNullOrWhiteSpace(seed.ShopName))
        {
            var company = Regex.Match(text, @"компан\w*\s+[«""']([^«""']+)[»""']", RegexOptions.IgnoreCase);
            if (company.Success)
                seed.ShopName = company.Groups[1].Value.Trim();
        }
    }

    private static void ExtractBranding(string text, string lower, AssignmentSeedData seed)
    {
        ApplyBrandingHeuristics(lower, seed);

        var fontMatch = Regex.Match(text,
            @"(Times New Roman|Arial|Segoe UI|Calibri|Tahoma|Verdana|Roboto|Inter)",
            RegexOptions.IgnoreCase);
        if (fontMatch.Success)
            seed.FontFamily = NormalizeFontFamily(fontMatch.Value);

        foreach (var (hex, context) in ExtractHexWithContext(text))
        {
            ApplyColorByContext(hex, context, seed);
        }

        var colors = Regex.Matches(text, @"#[0-9A-Fa-f]{6}")
            .Select(m => m.Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var remaining = colors.Where(c =>
                !string.Equals(c, seed.AccentColor, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(c, seed.SecondaryColor, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(c, seed.PrimaryColor, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (string.IsNullOrWhiteSpace(seed.AccentColor) && remaining.Count > 0)
            seed.AccentColor = remaining[0];
        if (string.IsNullOrWhiteSpace(seed.SecondaryColor) && remaining.Count > 1)
            seed.SecondaryColor = remaining[1];
        if (string.IsNullOrWhiteSpace(seed.PrimaryColor) && remaining.Count > 2)
            seed.PrimaryColor = remaining[2];

        if (string.IsNullOrWhiteSpace(seed.ProjectName))
        {
            var source = !string.IsNullOrWhiteSpace(seed.ShopName) ? seed.ShopName : seed.DomainLabel;
            seed.ProjectName = BuildProjectName(source);
        }
    }

    private static void EnsureDomainDefaults(AssignmentSeedData seed, AssignmentRequirements req)
    {
        if (seed.Categories.Count == 0)
            seed.Categories = GetDefaultCategories(req.ProductWord);

        if (seed.Makers.Count == 0)
            seed.Makers = seed.UseAuthorField
                ? ["АСТ", "Эксмо", "Просвещение"]
                : GetDefaultMakers(req.ProductWord);

        if (seed.Suppliers.Count == 0)
            seed.Suppliers = GetDefaultSuppliers(req.ProductWord);

        if (string.IsNullOrWhiteSpace(seed.ShopName))
            seed.ShopName = GetDefaultShopName(req);

        seed.FontFamily ??= GetDefaultFont(req.ProductWord);
        seed.SecondaryColor ??= "#2E8B57";
        seed.AccentColor ??= "#006400";
        seed.PrimaryColor ??= "#FFFFFF";
        seed.ProjectName ??= BuildProjectName(seed.ShopName ?? req.BrandName);
    }

    private static void BuildSampleProducts(AssignmentSeedData seed)
    {
        if (seed.Products.Count > 0)
            return;

        var templates = GetDomainProductTemplates(seed.ProductWord, seed.UseAuthorField);
        var supplier = seed.Suppliers.FirstOrDefault() ?? "ООО «Склад»";

        for (var i = 0; i < Math.Min(3, seed.Categories.Count); i++)
        {
            var category = seed.Categories[i];
            var maker = seed.Makers[i % seed.Makers.Count];
            var template = templates[i % templates.Count];

            seed.Products.Add(new SeedProductTemplate
            {
                Article = template.Article,
                Name = template.Name,
                Description = template.Description,
                CategoryName = category,
                MakerName = maker,
                SupplierName = supplier,
                Price = template.Price,
                Quantity = template.Quantity,
                Discount = template.Discount
            });
        }
    }

    private static List<string> GetDefaultCategories(string productWord)
    {
        if (productWord.Contains("смартфон", StringComparison.OrdinalIgnoreCase))
            return ["Флагманский", "Средний сегмент", "Бюджетный", "Аксессуары"];
        if (productWord.Contains("велосипед", StringComparison.OrdinalIgnoreCase))
            return ["Горный", "Шоссейный", "Городской"];
        if (productWord.Contains("книг", StringComparison.OrdinalIgnoreCase))
            return ["Роман", "Хрестоматия", "Учебник"];
        if (productWord.Contains("настольн", StringComparison.OrdinalIgnoreCase) || productWord.Contains("игр", StringComparison.OrdinalIgnoreCase))
            return ["Стратегии", "Детские игры", "Пазлы", "Аксессуары"];
        if (productWord.Contains("комплектующ", StringComparison.OrdinalIgnoreCase))
            return ["Процессоры", "Видеокарты", "Материнские платы", "Память", "Накопители", "Блоки питания"];
        return ["Основная", "Премиум", "Стандарт"];
    }

    private static List<string> GetDefaultMakers(string productWord)
    {
        if (productWord.Contains("смартфон", StringComparison.OrdinalIgnoreCase))
            return ["Apple", "Samsung", "Xiaomi", "Google"];
        if (productWord.Contains("велосипед", StringComparison.OrdinalIgnoreCase))
            return ["Trek", "Giant", "Stels"];
        if (productWord.Contains("настольн", StringComparison.OrdinalIgnoreCase) || productWord.Contains("игр", StringComparison.OrdinalIgnoreCase))
            return ["Hasbro", "Hobby World", "Ravensburger", "Zvezda"];
        if (productWord.Contains("комплектующ", StringComparison.OrdinalIgnoreCase))
            return ["Intel", "AMD", "NVIDIA", "ASUS", "MSI"];
        return ["Производитель А", "Производитель Б", "Производитель В"];
    }

    private static List<string> GetDefaultSuppliers(string productWord)
    {
        if (productWord.Contains("смартфон", StringComparison.OrdinalIgnoreCase))
            return ["ООО «Мобильный склад»", "ИП Сидоров"];
        if (productWord.Contains("велосипед", StringComparison.OrdinalIgnoreCase))
            return ["ООО «ВелоСклад»", "ИП Веломастер"];
        if (productWord.Contains("книг", StringComparison.OrdinalIgnoreCase))
            return ["ООО «Книжный склад»", "ИП Петров"];
        if (productWord.Contains("комплектующ", StringComparison.OrdinalIgnoreCase))
            return ["ООО «КомпСклад»", "ИП ТехноПоставка"];
        return ["ООО «Склад»", "ИП Поставщик"];
    }

    private static string GetDefaultShopName(AssignmentRequirements req)
    {
        if (req.ProductWord.Contains("смартфон", StringComparison.OrdinalIgnoreCase))
            return "СмартМаркет";
        if (req.ProductWord.Contains("велосипед", StringComparison.OrdinalIgnoreCase))
            return "ВелосипедДрайв";
        if (req.ProductWord.Contains("книг", StringComparison.OrdinalIgnoreCase))
            return "ЧитайГород";
        if (req.ProductWord.Contains("комплектующ", StringComparison.OrdinalIgnoreCase))
            return "КомпМаркет";
        return req.BrandName;
    }

    private static List<SeedProductTemplate> GetDomainProductTemplates(string productWord, bool authorMode)
    {
        if (productWord.Contains("смартфон", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new() { Article = "APL-IP16-256", Name = "iPhone 16 256GB", Description = "Флагман Apple.", Price = 129990m, Quantity = 12, Discount = 10m },
                new() { Article = "SMS-S25-512", Name = "Galaxy S25 Ultra 512GB", Description = "Топовый Android-смартфон.", Price = 149990m, Quantity = 7, Discount = 5m },
                new() { Article = "XMI-RD13-128", Name = "Redmi 13 128GB", Description = "Бюджетный смартфон.", Price = 18990m, Quantity = 45, Discount = 0m }
            ];
        }

        if (productWord.Contains("велосипед", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new() { Article = "TRK-X29-001", Name = "Trail X 29", Description = "Горный велосипед.", Price = 89990m, Quantity = 8, Discount = 20m },
                new() { Article = "GNT-SPD-002", Name = "Speed Pro", Description = "Шоссейный велосипед.", Price = 119990m, Quantity = 4, Discount = 10m },
                new() { Article = "STL-CITY-003", Name = "City Comfort", Description = "Городской велосипед.", Price = 45990m, Quantity = 15, Discount = 0m }
            ];
        }

        if (productWord.Contains("комплектующ", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new() { Article = "CPU-I7-14700", Name = "Intel Core i7-14700", Description = "Процессор Intel 20 ядер.", Price = 38990m, Quantity = 15, Discount = 5m },
                new() { Article = "GPU-RTX4070", Name = "NVIDIA GeForce RTX 4070", Description = "Видеокарта 12 ГБ.", Price = 64990m, Quantity = 8, Discount = 10m },
                new() { Article = "RAM-DDR5-32", Name = "DDR5 32GB Kit", Description = "Оперативная память 32 ГБ.", Price = 12990m, Quantity = 25, Discount = 0m }
            ];
        }

        return
        [
            new() { Article = "BK-001", Name = authorMode ? "Прокляты и убиты" : "Товар 1", Description = "Демонстрационный товар.", Price = 890m, Quantity = 25, Discount = 15m },
            new() { Article = "BK-002", Name = authorMode ? "Хрестоматия по литературе" : "Товар 2", Description = "Демонстрационный товар.", Price = 450m, Quantity = 40, Discount = 0m },
            new() { Article = "BK-003", Name = authorMode ? "Основы программирования" : "Товар 3", Description = "Демонстрационный товар.", Price = 1200m, Quantity = 10, Discount = 8m }
        ];
    }

    private static List<string> ExtractQuotedValues(string line)
    {
        var result = new List<string>();
        foreach (Match match in Regex.Matches(line, @"[«""']([^«""']+)[»""']"))
        {
            var value = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(value);
        }

        return result;
    }

    private static string CleanListItem(string value) =>
        value.Trim().TrimEnd('.', ';').Replace(" и др.", "", StringComparison.OrdinalIgnoreCase).Trim();

    private static List<string> Distinct(IEnumerable<string> values) =>
        values.Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static decimal ParseDecimal(string value) =>
        decimal.Parse(value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);

    private static string NormalizeFontFamily(string font) =>
        font.Trim().ToLowerInvariant() switch
        {
            "times new roman" => "Times New Roman, serif",
            "arial" => "Arial, sans-serif",
            "segoe ui" => "Segoe UI, sans-serif",
            "calibri" => "Calibri, sans-serif",
            "tahoma" => "Tahoma, sans-serif",
            "verdana" => "Verdana, sans-serif",
            "roboto" => "Roboto, sans-serif",
            "inter" => "Inter, sans-serif",
            _ => "Arial, sans-serif"
        };

    private static void ApplyBrandingHeuristics(string lower, AssignmentSeedData seed)
    {
        if (string.IsNullOrWhiteSpace(seed.FontFamily))
        {
            if (Regex.IsMatch(lower, @"классическ|книжн|с\s+засечк"))
                seed.FontFamily = "Times New Roman, serif";
            else if (Regex.IsMatch(lower, @"без\s+засеч|читабельн|нейтральн"))
                seed.FontFamily = "Arial, sans-serif";
            else if (Regex.IsMatch(lower, @"современ|минимал|windows|интерфейс"))
                seed.FontFamily = "Segoe UI, sans-serif";
            else if (Regex.IsMatch(lower, @"техн|цифров|hi[-\s]?tech"))
                seed.FontFamily = "Roboto, sans-serif";
        }

        // Цвета по текстовым формулировкам ТЗ (даже без HEX).
        if (Regex.IsMatch(lower, @"фирменн\w*\s+цвет|цветов\w*\s+схем|палитр"))
        {
            seed.AccentColor ??= InferColorFromWords(lower, ColorRole.Accent);
            seed.SecondaryColor ??= InferColorFromWords(lower, ColorRole.Secondary);
            seed.PrimaryColor ??= InferColorFromWords(lower, ColorRole.Primary);
        }
    }

    private static IEnumerable<(string Hex, string Context)> ExtractHexWithContext(string text)
    {
        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            var context = line.ToLowerInvariant();
            foreach (Match match in Regex.Matches(line, @"#[0-9A-Fa-f]{6}"))
                yield return (match.Value.ToUpperInvariant(), context);
        }
    }

    private static void ApplyColorByContext(string hex, string context, AssignmentSeedData seed)
    {
        if ((context.Contains("фон") || context.Contains("background") || context.Contains("страниц")) &&
            (context.Contains("главного") || context.Contains("окна") || context.Contains("главн")))
        {
            seed.PrimaryColor = hex;
            return;
        }

        if (context.Contains("акцент") || context.Contains("кноп") || context.Contains("заголов"))
        {
            // Для формулировок «заголовки окон» используем secondary, а «кнопки/акцент» — accent.
            if (context.Contains("заголов"))
                seed.SecondaryColor = hex;
            else
                seed.AccentColor = hex;
            return;
        }

        if (context.Contains("вторич") || context.Contains("дополн") || context.Contains("secondary"))
        {
            seed.SecondaryColor = hex;
            return;
        }
    }

    private enum ColorRole { Primary, Secondary, Accent }

    private static string? InferColorFromWords(string lower, ColorRole role)
    {
        // В учебных ТЗ часто используются словесные описания без HEX.
        var colorMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["зелен"] = "#2E8B57",
            ["салатов"] = "#90EE90",
            ["син"] = "#4682B4",
            ["фиолет"] = "#6A5ACD",
            ["жёлт"] = "#FFD966",
            ["желт"] = "#FFD966",
            ["золот"] = "#DAA520",
            ["красн"] = "#DC2626",
            ["сер"] = "#E5E7EB",
            ["бел"] = "#FFFFFF",
            ["чёрн"] = "#111827",
            ["черн"] = "#111827"
        };

        var roleHint = role switch
        {
            ColorRole.Primary => @"фон|основн",
            ColorRole.Secondary => @"вторич|дополн",
            _ => @"акцент|кноп|заголов"
        };

        foreach (var pair in colorMap)
        {
            if (!lower.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
                continue;

            if (Regex.IsMatch(lower, roleHint))
                return pair.Value;
        }

        return null;
    }

    private static string GetDefaultFont(string productWord)
    {
        if (productWord.Contains("книг", StringComparison.OrdinalIgnoreCase))
            return "Times New Roman, serif";
        if (productWord.Contains("комплектующ", StringComparison.OrdinalIgnoreCase))
            return "Segoe UI, sans-serif";
        return "Arial, sans-serif";
    }

    private static string BuildProjectName(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "KodShopWeb";

        var map = new Dictionary<char, string>
        {
            ['а']="a",['б']="b",['в']="v",['г']="g",['д']="d",['е']="e",['ё']="e",['ж']="zh",['з']="z",
            ['и']="i",['й']="y",['к']="k",['л']="l",['м']="m",['н']="n",['о']="o",['п']="p",['р']="r",
            ['с']="s",['т']="t",['у']="u",['ф']="f",['х']="h",['ц']="ts",['ч']="ch",['ш']="sh",['щ']="sch",
            ['ъ']="",['ы']="y",['ь']="",['э']="e",['ю']="yu",['я']="ya"
        };

        var latin = new System.Text.StringBuilder();
        foreach (var ch in source.Trim())
        {
            var lower = char.ToLowerInvariant(ch);
            if (map.TryGetValue(lower, out var tr))
            {
                latin.Append(tr);
                continue;
            }

            if (char.IsLetterOrDigit(ch))
                latin.Append(ch);
            else
                latin.Append(' ');
        }

        var words = latin.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant())
            .ToList();

        var name = string.Concat(words);
        if (string.IsNullOrWhiteSpace(name))
            return "KodShopWeb";
        if (!char.IsLetter(name[0]))
            name = "Shop" + name;
        return name.Length > 40 ? name[..40] : name;
    }
}
