using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ExamCoachDesktop;

public static class ApplyAdaptedProject
{
    private static readonly string[] FileExtensions =
        [".cs", ".cshtml", ".json", ".js", ".css", ".csproj", ".html", ".txt"];

    public static int Apply(string pdfPath, string projectRoot, string coachRoot, bool initIfEmpty = false)
    {
        var basePath = Path.Combine(coachRoot, "steps-data.json");
        var desktopApp = Path.Combine(coachRoot, "DesktopApp");

        if (initIfEmpty && !ProjectTargetHelper.IsWebProject(projectRoot))
            ProjectTargetHelper.InitMvcProject(projectRoot);

        var text = AssignmentDocumentReader.ReadFile(pdfPath);
        var profile = AssignmentTextParser.Parse(text);
        var adapted = AssignmentAdaptEngine.Adapt(CoachLoader.Load(basePath), profile);
        profile.SourceText = text;
        profile.AppliedAt = DateTime.UtcNow;

        Directory.CreateDirectory(desktopApp);
        File.WriteAllText(
            Path.Combine(desktopApp, "examcoach-assignment.json"),
            JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }),
            Encoding.UTF8);
        CoachDataSerializer.Save(adapted, Path.Combine(desktopApp, "steps-data-custom.json"));

        var rootNamespace = ProjectTargetHelper.GetRootNamespace(projectRoot);
        var scaffoldMode = !ProjectTargetHelper.IsScaffoldedProject(projectRoot);
        if (scaffoldMode)
        {
            var relativePaths = adapted.Steps
                .Where(s => LooksLikeProjectFile(s.Title))
                .Select(s => s.Title);
            ProjectTargetHelper.EnsureFolderStructure(projectRoot, relativePaths);
            Console.WriteLine($"Режим: заполнение {(initIfEmpty ? "нового" : "пустого")} проекта (namespace: {rootNamespace})");
        }

        string? csprojTemplate = null;
        var written = 0;
        foreach (var step in adapted.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Code))
                continue;
            if (!LooksLikeProjectFile(step.Title))
                continue;

            var relative = ProjectTargetHelper.ResolveRelativePath(step.Title, projectRoot);
            if (relative.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                csprojTemplate ??= step.Code;
                continue;
            }

            var fullPath = Path.Combine(projectRoot, relative);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var code = ProjectTargetHelper.RewriteRootNamespace(step.Code, rootNamespace);
            var sanitizedCode = SanitizeGeneratedCode(relative, code, profile.ExamVariant, fullPath, rootNamespace);
            File.WriteAllText(fullPath, sanitizedCode, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            written++;
        }

        ProjectTargetHelper.EnsureNuGetPackages(projectRoot, csprojTemplate);
        ProjectTargetHelper.EnsureExamCoachExcluded(projectRoot);

        var seed = profile.Requirements?.SeedData
            ?? AssignmentSeedExtractor.Extract(text, profile.Requirements ?? new AssignmentRequirements());
        var authorMode = seed.UseAuthorField;

        if (scaffoldMode)
            ProjectTargetHelper.CleanupDefaultMvcTemplate(projectRoot);

        NormalizeDomainConsistency(projectRoot, authorMode);
        RewriteEntities(projectRoot, authorMode, rootNamespace);
        RewriteAppDbContextDbSets(projectRoot, authorMode);
        RewriteDbSeeder(projectRoot, seed, rootNamespace);
        ApplyShopSettingsFromSeed(projectRoot, seed, profile.ExamVariant);
        NormalizeImportViewModelConsistency(projectRoot);
        ResetSqliteDatabase(projectRoot);
        return written;
    }

    private static bool LooksLikeProjectFile(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Contains('\n'))
            return false;
        return FileExtensions.Any(ext => title.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    private static string SanitizeGeneratedCode(string relativePath, string code, string? examVariant, string fullPath, string rootNamespace)
    {
        if (string.IsNullOrEmpty(code))
            return code;

        code = SanitizeVariantConfig(relativePath, code, examVariant);

        if (!relativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedPath = relativePath.Replace('\\', '/');
            if (normalizedPath.EndsWith("wwwroot/js/products.js", StringComparison.OrdinalIgnoreCase))
                return BuildProductsJs();
            if (normalizedPath.EndsWith("wwwroot/css/shop.css", StringComparison.OrdinalIgnoreCase))
                return EnsureModalStyles(code);
            return code;
        }

        var sanitized = Regex.Replace(
            code,
            @"^\s*using\s+DocumentFormat\.[^;]+;\s*\r?\n?",
            string.Empty,
            RegexOptions.Multiline);

        sanitized = SanitizeProductServiceForSqlite(relativePath, sanitized);
        sanitized = SanitizeBuOrdersEnabled(relativePath, sanitized);
        sanitized = SanitizeDbSeederSafety(relativePath, sanitized, fullPath);
        sanitized = SanitizeOrderNullability(relativePath, sanitized);

        if (!NeedsOrderAlias(relativePath, sanitized))
            return sanitized;

        var orderAlias = $"using Order = {rootNamespace}.Models.Order;";
        if (sanitized.Contains(orderAlias, StringComparison.Ordinal))
            return sanitized;

        var newLine = sanitized.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var usingMatches = Regex.Matches(sanitized, @"^\s*using\s+[^;]+;\s*$", RegexOptions.Multiline);
        if (usingMatches.Count == 0)
            return orderAlias + newLine + sanitized;

        var lastUsing = usingMatches[^1];
        var insertAt = lastUsing.Index + lastUsing.Length;
        return sanitized.Insert(insertAt, newLine + orderAlias);
    }

    private static string SanitizeDbSeederSafety(string relativePath, string code, string fullPath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (!normalized.EndsWith("Data/DbSeeder.cs", StringComparison.OrdinalIgnoreCase))
            return code;

        if (!IsLikelyBrokenDbSeeder(code))
            return code;

        if (File.Exists(fullPath))
        {
            var existing = File.ReadAllText(fullPath);
            if (!IsLikelyBrokenDbSeeder(existing))
                return existing;
        }

        return BuildDbSeederFallback();
    }

    private static bool IsLikelyBrokenDbSeeder(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return true;
        if (!code.Contains("public static class DbSeeder", StringComparison.Ordinal))
            return false;
        if (!code.Contains("SeedAsync", StringComparison.Ordinal))
            return true;

        var open = 0;
        var close = 0;
        foreach (var ch in code)
        {
            if (ch == '{') open++;
            else if (ch == '}') close++;
        }

        if (open != close)
            return true;

        return !Regex.IsMatch(code, @"\}\s*\}\s*$", RegexOptions.Multiline);
    }

    private static string BuildDbSeederFallback() =>
        """
        using KodShopWeb.Models;
        using Microsoft.EntityFrameworkCore;
        
        namespace KodShopWeb.Data;
        
        public static class DbSeeder
        {
            public static async Task SeedAsync(AppDbContext db, IWebHostEnvironment env, ShopSettings settings)
            {
                await db.Database.EnsureCreatedAsync();
            }
        }
        """;

    private static bool NeedsOrderAlias(string relativePath, string code)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith("Models/", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!code.Contains("using KodShopWeb.Models;", StringComparison.Ordinal))
            return false;
        if (!Regex.IsMatch(code, @"\bOrder\??\b"))
            return false;

        return true;
    }

    private static string SanitizeOrderNullability(string relativePath, string code)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (!normalized.EndsWith("Controllers/OrdersController.cs", StringComparison.OrdinalIgnoreCase))
            return code;

        var updated = code.Replace(
            "        return View(await BuildFormAsync(order));",
            "        return View(await BuildFormAsync(order!));");

        updated = updated.Replace(
            "        return View(await BuildFormAsync(order));\r\n",
            "        return View(await BuildFormAsync(order!));\r\n");

        return updated;
    }

    private static string SanitizeVariantConfig(string relativePath, string code, string? examVariant)
    {
        if (!string.Equals(examVariant, "PU", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(examVariant, "BU", StringComparison.OrdinalIgnoreCase))
            return code;

        var normalized = relativePath.Replace('\\', '/');
        var targetVariant = examVariant!.ToUpperInvariant();

        if (normalized.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase))
        {
            return Regex.Replace(
                code,
                "\"Variant\"\\s*:\\s*\"(?:PU|BU)\"",
                $"\"Variant\": \"{targetVariant}\"");
        }

        if (normalized.EndsWith("Models/ShopSettings.cs", StringComparison.OrdinalIgnoreCase))
        {
            return Regex.Replace(
                code,
                "public\\s+string\\s+Variant\\s*\\{\\s*get;\\s*set;\\s*\\}\\s*=\\s*\"(?:PU|BU)\";",
                $"public string Variant {{ get; set; }} = \"{targetVariant}\";");
        }

        return code;
    }

    private static string SanitizeProductServiceForSqlite(string relativePath, string code)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (!normalized.EndsWith("Services/ProductService.cs", StringComparison.OrdinalIgnoreCase))
            return code;

        var updated = code;
        updated = updated.Replace(
            "            products = ApplyFilters(products, query);\r\n            products = ApplySort(products, query);",
            "            products = ApplyFilters(products, query);\r\n            var list = await products.ToListAsync();\r\n            return ApplySortInMemory(list, query);");
        updated = updated.Replace(
            "            products = ApplyFilters(products, query);\n            products = ApplySort(products, query);",
            "            products = ApplyFilters(products, query);\n            var list = await products.ToListAsync();\n            return ApplySortInMemory(list, query);");

        updated = updated.Replace(
            "        else\r\n        {\r\n            products = products.OrderBy(p => p.Name);\r\n        }\r\n\r\n        return await products.ToListAsync();",
            "        return await products.OrderBy(p => p.Name).ToListAsync();");
        updated = updated.Replace(
            "        else\n        {\n            products = products.OrderBy(p => p.Name);\n        }\n\n        return await products.ToListAsync();",
            "        return await products.OrderBy(p => p.Name).ToListAsync();");

        updated = updated.Replace(
            "    /// Применяет сортировку на стороне сервера (IQueryable до материализации).\r\n    /// </summary>\r\n    private static IQueryable<Product> ApplySort(IQueryable<Product> source, ProductQuery query)",
            "    /// Применяет сортировку после материализации данных.\r\n    /// SQLite не поддерживает decimal в ORDER BY через EF-провайдер.\r\n    /// </summary>\r\n    private static List<Product> ApplySortInMemory(List<Product> source, ProductQuery query)");
        updated = updated.Replace(
            "    /// Применяет сортировку на стороне сервера (IQueryable до материализации).\n    /// </summary>\n    private static IQueryable<Product> ApplySort(IQueryable<Product> source, ProductQuery query)",
            "    /// Применяет сортировку после материализации данных.\n    /// SQLite не поддерживает decimal в ORDER BY через EF-провайдер.\n    /// </summary>\n    private static List<Product> ApplySortInMemory(List<Product> source, ProductQuery query)");

        updated = updated.Replace(
            "            ProductSortField.Quantity => asc\r\n                ? source.OrderBy(p => p.QuantityOnStock)\r\n                : source.OrderByDescending(p => p.QuantityOnStock),",
            "            ProductSortField.Quantity => asc\r\n                ? source.OrderBy(p => p.QuantityOnStock).ToList()\r\n                : source.OrderByDescending(p => p.QuantityOnStock).ToList(),");
        updated = updated.Replace(
            "            ProductSortField.Quantity => asc\n                ? source.OrderBy(p => p.QuantityOnStock)\n                : source.OrderByDescending(p => p.QuantityOnStock),",
            "            ProductSortField.Quantity => asc\n                ? source.OrderBy(p => p.QuantityOnStock).ToList()\n                : source.OrderByDescending(p => p.QuantityOnStock).ToList(),");

        updated = updated.Replace(
            "            ProductSortField.Discount => asc\r\n                ? source.OrderBy(p => p.Discount)\r\n                : source.OrderByDescending(p => p.Discount),",
            "            ProductSortField.Discount => asc\r\n                ? source.OrderBy(p => p.Discount).ToList()\r\n                : source.OrderByDescending(p => p.Discount).ToList(),");
        updated = updated.Replace(
            "            ProductSortField.Discount => asc\n                ? source.OrderBy(p => p.Discount)\n                : source.OrderByDescending(p => p.Discount),",
            "            ProductSortField.Discount => asc\n                ? source.OrderBy(p => p.Discount).ToList()\n                : source.OrderByDescending(p => p.Discount).ToList(),");

        updated = updated.Replace(
            "            _ => asc\r\n                ? source.OrderBy(p => p.Price)\r\n                : source.OrderByDescending(p => p.Price)",
            "            _ => asc\r\n                ? source.OrderBy(p => p.Price).ToList()\r\n                : source.OrderByDescending(p => p.Price).ToList()");
        updated = updated.Replace(
            "            _ => asc\n                ? source.OrderBy(p => p.Price)\n                : source.OrderByDescending(p => p.Price)",
            "            _ => asc\n                ? source.OrderBy(p => p.Price).ToList()\n                : source.OrderByDescending(p => p.Price).ToList()");

        return updated;
    }

    private static string SanitizeBuOrdersEnabled(string relativePath, string code)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (!normalized.EndsWith("Models/ShopSettings.cs", StringComparison.OrdinalIgnoreCase) &&
            !normalized.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase))
            return code;

        var updated = code
            .Replace("OrdersEnabled { get; set; } = false;", "OrdersEnabled { get; set; } = true;")
            .Replace("settings.OrdersEnabled = false;", "settings.OrdersEnabled = true;")
            .Replace("\"OrdersEnabled\": false", "\"OrdersEnabled\": true");

        return updated;
    }

    private static void NormalizeDomainConsistency(string projectRoot, bool authorMode)
    {
        var entitiesPath = Path.Combine(projectRoot, "Models", "Entities.cs");
        if (!File.Exists(entitiesPath))
            return;

        var entities = File.ReadAllText(entitiesPath);
        var hasAuthor = entities.Contains("class Author", StringComparison.Ordinal);
        var hasManufacturer = entities.Contains("class Manufacturer", StringComparison.Ordinal);

        // If TZ wants Author but Entities still has Manufacturer → convert to Author
        if (authorMode && hasManufacturer && !hasAuthor)
        {
            ApplyDomainReplacement(projectRoot, "Manufacturer", "Author", true);
            return;
        }

        // If TZ does NOT want Author but Entities has Author (or mixed) → force Manufacturer
        if (!authorMode && hasAuthor)
        {
            ApplyDomainReplacement(projectRoot, "Author", "Manufacturer", false);
        }
    }

    private static void ApplyDomainReplacement(string projectRoot, string from, string to, bool toAuthor)
    {
        var targets = new[]
        {
            Path.Combine(projectRoot, "Services", "ImportService.cs"),
            Path.Combine(projectRoot, "Services", "ProductService.cs"),
            Path.Combine(projectRoot, "Controllers", "ProductsController.cs"),
            Path.Combine(projectRoot, "ViewModels", "ViewModels.cs"),
            Path.Combine(projectRoot, "Views", "Products", "Edit.cshtml"),
            Path.Combine(projectRoot, "Views", "Products", "Index.cshtml"),
            Path.Combine(projectRoot, "Data", "AppDbContext.cs"),
            Path.Combine(projectRoot, "Data", "DbSeeder.cs"),
            Path.Combine(projectRoot, "Models", "Entities.cs")
        };

        var replacements = toAuthor
            ? new (string from, string to)[]
            {
                ("GetManufacturersAsync", "GetAuthorsAsync"),
                ("EnsureManufacturerAsync", "EnsureAuthorAsync"),
                ("db.Manufacturers", "db.Authors"),
                ("Manufacturers", "Authors"),
                ("ManufacturerId", "AuthorId"),
                ("ManufacturerName", "AuthorName"),
                ("manufacturerName", "authorName"),
                ("manufacturer", "author"),
                ("Manufacturer", "Author")
            }
            : new (string from, string to)[]
            {
                ("GetAuthorsAsync", "GetManufacturersAsync"),
                ("EnsureAuthorAsync", "EnsureManufacturerAsync"),
                ("db.Authors", "db.Manufacturers"),
                ("Authors", "Manufacturers"),
                ("AuthorId", "ManufacturerId"),
                ("AuthorName", "ManufacturerName"),
                ("authorName", "manufacturerName"),
                ("author", "manufacturer"),
                ("Author", "Manufacturer")
            };

        foreach (var path in targets)
        {
            if (!File.Exists(path))
                continue;

            var code = File.ReadAllText(path);
            var updated = code;
            foreach (var (f, t) in replacements)
                updated = updated.Replace(f, t);

            if (!string.Equals(code, updated, StringComparison.Ordinal))
                File.WriteAllText(path, updated, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    /// <summary>
    /// Полностью переписывает AppDbContext.cs — обнуляет все DbSet'ы и вставляет правильные
    /// (Author или Manufacturer) в зависимости от ТЗ.
    /// </summary>
    private static void RewriteAppDbContextDbSets(string projectRoot, bool authorMode)
    {
        var path = Path.Combine(projectRoot, "Data", "AppDbContext.cs");
        if (!File.Exists(path))
            return;

        var code = File.ReadAllText(path);

        // Находим секцию DbSet'ов и заменяем её целиком
        var pattern = new Regex(
            @"(// --- Наборы сущностей \(таблицы\) ---[\s\S]*?)(protected override void OnModelCreating)",
            RegexOptions.Singleline);

        if (!pattern.IsMatch(code))
            return;

        var dbSets = authorMode
            ? """
            /// <summary>Таблица пользователей.</summary>
            public DbSet<AppUser> Users => Set<AppUser>();

            /// <summary>Таблица категорий товаров.</summary>
            public DbSet<Category> Categories => Set<Category>();

            /// <summary>Таблица авторов.</summary>
            public DbSet<Author> Authors => Set<Author>();

            /// <summary>Таблица поставщиков.</summary>
            public DbSet<Supplier> Suppliers => Set<Supplier>();

            /// <summary>Таблица товаров.</summary>
            public DbSet<Product> Products => Set<Product>();

            /// <summary>Таблица пунктов выдачи.</summary>
            public DbSet<PickupPoint> PickupPoints => Set<PickupPoint>();

            /// <summary>Таблица заказов.</summary>
            public DbSet<Order> Orders => Set<Order>();

            /// <summary>Таблица позиций заказов.</summary>
            public DbSet<OrderItem> OrderItems => Set<OrderItem>();
            """
            : """
            /// <summary>Таблица пользователей.</summary>
            public DbSet<AppUser> Users => Set<AppUser>();

            /// <summary>Таблица категорий товаров.</summary>
            public DbSet<Category> Categories => Set<Category>();

            /// <summary>Таблица производителей.</summary>
            public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();

            /// <summary>Таблица поставщиков.</summary>
            public DbSet<Supplier> Suppliers => Set<Supplier>();

            /// <summary>Таблица товаров.</summary>
            public DbSet<Product> Products => Set<Product>();

            /// <summary>Таблица пунктов выдачи.</summary>
            public DbSet<PickupPoint> PickupPoints => Set<PickupPoint>();

            /// <summary>Таблица заказов.</summary>
            public DbSet<Order> Orders => Set<Order>();

            /// <summary>Таблица позиций заказов.</summary>
            public DbSet<OrderItem> OrderItems => Set<OrderItem>();
            """;

        var replacement = $"// --- Наборы сущностей (таблицы) ---\n\n{dbSets}\n\n        protected override void OnModelCreating";
        var updated = pattern.Replace(code, replacement);

        if (!string.Equals(code, updated, StringComparison.Ordinal))
            File.WriteAllText(path, updated, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void RewriteEntities(string projectRoot, bool authorMode, string rootNamespace)
    {
        var path = Path.Combine(projectRoot, "Models", "Entities.cs");
        var code = EntitiesGenerator.Generate(authorMode, rootNamespace);
        File.WriteAllText(path, code, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// Полностью переписывает DbSeeder.cs по данным, извлечённым из текста ТЗ.
    /// </summary>
    private static void RewriteDbSeeder(string projectRoot, AssignmentSeedData seed, string rootNamespace)
    {
        var path = Path.Combine(projectRoot, "Data", "DbSeeder.cs");
        var signature = SeedSignatureHelper.Compute(seed);
        var seederCode = DbSeederGenerator.Generate(seed, signature, rootNamespace);
        File.WriteAllText(path, seederCode, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var dataDir = Path.Combine(projectRoot, "Data");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "seed-signature.txt"), signature, Encoding.UTF8);
        File.WriteAllText(Path.Combine(dataDir, ".force-db-reseed"), DateTime.UtcNow.ToString("O"), Encoding.UTF8);
    }

    private static void ApplyShopSettingsFromSeed(string projectRoot, AssignmentSeedData seed, string? examVariant)
    {
        var shopSettingsPath = Path.Combine(projectRoot, "Models", "ShopSettings.cs");
        if (File.Exists(shopSettingsPath))
        {
            var code = File.ReadAllText(shopSettingsPath);
            var updated = code
                .Replace("DiscountHighlightPercent { get; set; } = 10m", $"DiscountHighlightPercent {{ get; set; }} = {seed.DiscountHighlightPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)}m")
                .Replace("DiscountHighlightPercent { get; set; } = 15m", $"DiscountHighlightPercent {{ get; set; }} = {seed.DiscountHighlightPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)}m")
                .Replace("DiscountHighlightPercent { get; set; } = 25m", $"DiscountHighlightPercent {{ get; set; }} = {seed.DiscountHighlightPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)}m")
                .Replace("DiscountHighlightColor { get; set; } = \"#90EE90\"", $"DiscountHighlightColor {{ get; set; }} = \"{seed.DiscountHighlightColor}\"")
                .Replace("DiscountHighlightColor { get; set; } = \"#23E1EF\"", $"DiscountHighlightColor {{ get; set; }} = \"{seed.DiscountHighlightColor}\"")
                .Replace("DiscountHighlightColor { get; set; } = \"#483D8B\"", $"DiscountHighlightColor {{ get; set; }} = \"{seed.DiscountHighlightColor}\"")
                .Replace("OrdersEnabled { get; set; } = false", $"OrdersEnabled {{ get; set; }} = {(seed.OrdersEnabled ? "true" : "false")}")
                .Replace("OrdersEnabled { get; set; } = true", $"OrdersEnabled {{ get; set; }} = {(seed.OrdersEnabled ? "true" : "false")}");

            if (!string.IsNullOrWhiteSpace(seed.ShopName))
            {
                updated = Regex.Replace(updated,
                    "ShopName \\{ get; set; \\} = \"[^\"]+\";",
                    $"ShopName {{ get; set; }} = \"{seed.ShopName}\";");
            }

            if (!string.Equals(code, updated, StringComparison.Ordinal))
                File.WriteAllText(shopSettingsPath, updated, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        var appsettingsPath = Path.Combine(projectRoot, "appsettings.json");
        if (File.Exists(appsettingsPath))
        {
            var json = File.ReadAllText(appsettingsPath);
            var updated = json
                .Replace("\"DiscountHighlightPercent\": 10", $"\"DiscountHighlightPercent\": {seed.DiscountHighlightPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
                .Replace("\"DiscountHighlightPercent\": 15", $"\"DiscountHighlightPercent\": {seed.DiscountHighlightPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
                .Replace("\"DiscountHighlightPercent\": 25", $"\"DiscountHighlightPercent\": {seed.DiscountHighlightPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
                .Replace("\"DiscountHighlightColor\": \"#90EE90\"", $"\"DiscountHighlightColor\": \"{seed.DiscountHighlightColor}\"")
                .Replace("\"DiscountHighlightColor\": \"#23E1EF\"", $"\"DiscountHighlightColor\": \"{seed.DiscountHighlightColor}\"")
                .Replace("\"DiscountHighlightColor\": \"#483D8B\"", $"\"DiscountHighlightColor\": \"{seed.DiscountHighlightColor}\"")
                .Replace("\"OrdersEnabled\": false", $"\"OrdersEnabled\": {(seed.OrdersEnabled ? "true" : "false")}")
                .Replace("\"OrdersEnabled\": true", $"\"OrdersEnabled\": {(seed.OrdersEnabled ? "true" : "false")}");

            if (!string.IsNullOrWhiteSpace(seed.ShopName))
            {
                updated = Regex.Replace(updated,
                    "\"ShopName\"\\s*:\\s*\"[^\"]+\"",
                    $"\"ShopName\": \"{seed.ShopName}\"");
            }

            if (!string.Equals(json, updated, StringComparison.Ordinal))
                File.WriteAllText(appsettingsPath, updated, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private static void NormalizeImportViewModelConsistency(string projectRoot)
    {
        var importView = Path.Combine(projectRoot, "Views", "Import", "Index.cshtml");
        var ordersController = Path.Combine(projectRoot, "Controllers", "OrdersController.cs");
        var importService = Path.Combine(projectRoot, "Services", "ImportService.cs");
        var viewModelsPath = Path.Combine(projectRoot, "ViewModels", "ViewModels.cs");

        var importUsed = File.Exists(importView)
            || (File.Exists(ordersController) && File.ReadAllText(ordersController).Contains("ImportController", StringComparison.Ordinal))
            || File.Exists(importService);

        if (!importUsed)
            return;

        if (!File.Exists(viewModelsPath))
            return;

        var code = File.ReadAllText(viewModelsPath);
        if (code.Contains("class ImportViewModel", StringComparison.Ordinal))
            return;

        var append = """

/// <summary>
/// Модель страницы импорта данных из папки с Excel-файлами.
/// </summary>
public class ImportViewModel
{
    /// <summary>Путь к папке импорта на диске сервера.</summary>
    public string FolderPath { get; set; } = string.Empty;

    /// <summary>Текст результата последнего импорта.</summary>
    public string? Message { get; set; }

    /// <summary>Успешен ли последний импорт.</summary>
    public bool Success { get; set; }
}
""";

        File.WriteAllText(viewModelsPath, code + append, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void ResetSqliteDatabase(string projectRoot)
    {
        var appsettings = Path.Combine(projectRoot, "appsettings.json");
        if (!File.Exists(appsettings))
            return;

        var json = File.ReadAllText(appsettings);
        var match = Regex.Match(
            json,
            "\"DefaultConnection\"\\s*:\\s*\"Data Source=([^\"]+)\"",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            return;

        var dbPathRaw = match.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(dbPathRaw))
            return;

        // В connection string могут быть дополнительные параметры:
        // Data Source=kodshop.db;Cache=Shared
        var semicolon = dbPathRaw.IndexOf(';');
        if (semicolon >= 0)
            dbPathRaw = dbPathRaw[..semicolon].Trim();

        var dbPath = Path.IsPathRooted(dbPathRaw)
            ? dbPathRaw
            : Path.Combine(projectRoot, dbPathRaw);

        var dbCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            dbPath,
            Path.Combine(projectRoot, "bin", "Debug", "net8.0", dbPathRaw),
            Path.Combine(projectRoot, "bin", "Release", "net8.0", dbPathRaw)
        };

        foreach (var path in dbCandidates.SelectMany(p => new[] { p, p + "-wal", p + "-shm" }))
        {
            if (!File.Exists(path))
                continue;

            try
            {
                File.Delete(path);
            }
            catch
            {
                // Если файл занят процессом, пропускаем удаление:
                // при следующем запуске без блокировки БД будет пересоздана корректно.
            }
        }
    }

    private static string BuildProductsJs() =>
        """
        /**
         * Мгновенная фильтрация/поиск каталога через fetch + клиентский lock формы редактирования.
         */
        (function () {
            const form = document.getElementById('productsFilterForm');
            const results = document.getElementById('productsResults')
                || (document.querySelector('.product-table-wrapper') ? document.querySelector('.product-table-wrapper').parentElement : null);
            const modalElements = ensureLockModal();
            const lockModal = modalElements.modal;
            const lockClose = modalElements.close;
            const lockKey = 'kodshop.product.edit.lock';
            const lockTtlMs = 30 * 60 * 1000;
        
            let debounceTimer;
            let abortController;
        
            function hasFreshEditLock() {
                const raw = localStorage.getItem(lockKey);
                if (!raw) return false;
                const ts = Number(raw);
                if (!Number.isFinite(ts)) {
                    localStorage.removeItem(lockKey);
                    return false;
                }
                if (Date.now() - ts > lockTtlMs) {
                    localStorage.removeItem(lockKey);
                    return false;
                }
                return true;
            }
        
            function showLockModal() {
                if (!lockModal) return;
                lockModal.classList.remove('hidden');
            }
        
            function hideLockModal() {
                if (!lockModal) return;
                lockModal.classList.add('hidden');
            }
        
            function bindEditLinks() {
                const links = document.querySelectorAll('[data-product-edit-link="true"], a[href*="/Products/Edit"], a[href*="/Products/Create"], a[href$="/Products/Create"]');
                links.forEach(link => {
                    link.addEventListener('click', function (e) {
                        if (hasFreshEditLock()) {
                            e.preventDefault();
                            showLockModal();
                            return;
                        }
                        localStorage.setItem(lockKey, String(Date.now()));
                    });
                });
            }
        
            function ensureLockModal() {
                let modal = document.getElementById('editLockModal');
                let close = document.getElementById('editLockModalClose');
                if (modal && close) return { modal, close };
        
                modal = document.createElement('div');
                modal.id = 'editLockModal';
                modal.className = 'modal-overlay hidden';
                modal.setAttribute('role', 'dialog');
                modal.setAttribute('aria-modal', 'true');
                modal.innerHTML = [
                    '<div class="modal-card">',
                    '  <h3>Форма редактирования уже открыта</h3>',
                    '  <p>Сначала закройте текущую форму редактирования товара, затем откройте новую.</p>',
                    '  <div class="modal-actions">',
                    '    <button type="button" id="editLockModalClose" class="btn btn-accent">Понятно</button>',
                    '  </div>',
                    '</div>'
                ].join('');
                document.body.appendChild(modal);
                close = document.getElementById('editLockModalClose');
                return { modal, close };
            }
        
            if (lockClose) {
                lockClose.addEventListener('click', hideLockModal);
            }
            if (lockModal) {
                lockModal.addEventListener('click', function (e) {
                    if (e.target === lockModal) hideLockModal();
                });
            }
        
            bindEditLinks();
        
            if (!form || !results) return;
        
            const searchInput = document.getElementById('searchInput');
            const discountFilter = document.getElementById('discountFilter');
            const sortField = document.getElementById('sortField');
            const sortDirection = document.getElementById('sortDirection');
        
            async function fetchFilteredResults() {
                const params = new URLSearchParams(new FormData(form));
                const url = form.action + '?' + params.toString();
        
                if (abortController) abortController.abort();
                abortController = new AbortController();
        
                try {
                    const response = await fetch(url, {
                        method: 'GET',
                        headers: { 'X-Requested-With': 'fetch' },
                        signal: abortController.signal
                    });
                    if (!response.ok) return;
        
                    const html = await response.text();
                    const doc = new DOMParser().parseFromString(html, 'text/html');
                    const next = doc.getElementById('productsResults')
                        || (doc.querySelector('.product-table-wrapper') ? doc.querySelector('.product-table-wrapper').parentElement : null);
                    if (!next) return;
        
                    results.innerHTML = next.innerHTML;
                    history.replaceState(null, '', url);
                    bindEditLinks();
                } catch (err) {
                    if (err && err.name === 'AbortError') return;
                }
            }
        
            function debouncedFetch() {
                clearTimeout(debounceTimer);
                debounceTimer = setTimeout(fetchFilteredResults, 300);
            }
        
            if (searchInput) {
                searchInput.addEventListener('input', debouncedFetch);
            }
        
            [discountFilter, sortField, sortDirection].forEach(el => {
                if (el) el.addEventListener('change', fetchFilteredResults);
            });
        })();
        """;

    private static string EnsureModalStyles(string css)
    {
        if (css.Contains(".modal-overlay", StringComparison.Ordinal))
            return css;

        var append = """

/* --- Простое модальное окно для клиентских блокировок --- */
.hidden { display: none !important; }
.modal-overlay {
    position: fixed;
    inset: 0;
    background: rgba(15, 23, 42, 0.45);
    display: grid;
    place-items: center;
    z-index: 1000;
    padding: 1rem;
}

.modal-card {
    width: min(520px, 92vw);
    background: #fff;
    border-radius: 12px;
    box-shadow: 0 20px 40px rgba(15, 23, 42, 0.2);
    padding: 1rem 1.25rem;
}

.modal-card h3 {
    margin: 0 0 0.5rem;
}

.modal-actions {
    margin-top: 1rem;
    display: flex;
    justify-content: flex-end;
}
""";

        return css + append;
    }
}
