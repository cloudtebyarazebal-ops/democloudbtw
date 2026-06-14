using System.Text;

namespace ExamCoachDesktop;

/// <summary>Генерирует DbSeeder.cs из данных, извлечённых из ТЗ.</summary>
public static class DbSeederGenerator
{
    public static string Generate(AssignmentSeedData seed, string signature, string rootNamespace = ProjectTargetHelper.ReferenceRootNamespace)
    {
        var makerType = seed.UseAuthorField ? "Author" : "Manufacturer";
        var makerSet = seed.UseAuthorField ? "Authors" : "Manufacturers";
        var makerId = seed.UseAuthorField ? "AuthorId" : "ManufacturerId";
        var makerVar = seed.UseAuthorField ? "authors" : "manufacturers";
        var domainComment = seed.UseAuthorField
            ? $"{seed.DomainLabel} + Author"
            : $"{seed.DomainLabel} + Manufacturer";

        var sb = new StringBuilder();
        sb.AppendLine($"using {rootNamespace}.Models;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace}.Data;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Заполняет базу данных демонстрационными данными ({domainComment}).");
        sb.AppendLine("/// Сгенерировано автоматически из текста ТЗ.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class DbSeeder");
        sb.AppendLine("{");
        sb.AppendLine($"    private const string SeedSignature = \"{signature}\";");
        sb.AppendLine();
        sb.AppendLine("    public static async Task SeedAsync(AppDbContext db, IWebHostEnvironment env, ShopSettings settings)");
        sb.AppendLine("    {");
        sb.AppendLine("        var forcePath = Path.Combine(env.ContentRootPath, \"Data\", \".force-db-reseed\");");
        sb.AppendLine("        var signaturePath = Path.Combine(env.ContentRootPath, \"Data\", \"seed-signature.txt\");");
        sb.AppendLine("        var forceReseed = File.Exists(forcePath);");
        sb.AppendLine("        var storedSignature = File.Exists(signaturePath) ? await File.ReadAllTextAsync(signaturePath) : \"\";");
        sb.AppendLine("        var needsReseed = forceReseed || storedSignature.Trim() != SeedSignature;");
        sb.AppendLine();
        sb.AppendLine("        if (needsReseed)");
        sb.AppendLine("        {");
        sb.AppendLine("            await db.Database.EnsureDeletedAsync();");
        sb.AppendLine("            await db.Database.EnsureCreatedAsync();");
        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");
        sb.AppendLine("            await db.Database.EnsureCreatedAsync();");
        sb.AppendLine("            if (await db.Products.AnyAsync())");
        sb.AppendLine("                return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        await EnsureUsersAsync(db);");
        sb.AppendLine();

        AppendArray(sb, makerVar, makerType, seed.Makers);
        sb.AppendLine($"        await db.{makerSet}.AddRangeAsync({makerVar});");
        sb.AppendLine();

        AppendArray(sb, "suppliers", "Supplier", seed.Suppliers);
        sb.AppendLine("        await db.Suppliers.AddRangeAsync(suppliers);");
        sb.AppendLine();

        AppendArray(sb, "categories", "Category", seed.Categories);
        sb.AppendLine("        await db.Categories.AddRangeAsync(categories);");
        sb.AppendLine();

        if (seed.OrdersEnabled)
        {
            sb.AppendLine("        var pickupPoints = new[]");
            sb.AppendLine("        {");
            sb.AppendLine("            new PickupPoint { Address = \"г. Москва, ул. Тверская, д. 1\" },");
            sb.AppendLine("            new PickupPoint { Address = \"г. Санкт-Петербург, Невский пр., д. 10\" }");
            sb.AppendLine("        };");
            sb.AppendLine("        await db.PickupPoints.AddRangeAsync(pickupPoints);");
            sb.AppendLine();
        }

        sb.AppendLine("        await db.SaveChangesAsync();");
        sb.AppendLine();

        AppendProducts(sb, seed, makerVar, makerId);
        sb.AppendLine("        await db.Products.AddRangeAsync(products);");
        sb.AppendLine("        await db.SaveChangesAsync();");

        if (seed.OrdersEnabled)
        {
            sb.AppendLine();
            sb.AppendLine("        if (!await db.Orders.AnyAsync())");
            sb.AppendLine("        {");
            sb.AppendLine("            var client = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Client);");
            sb.AppendLine("            var order = new Order");
            sb.AppendLine("            {");
            sb.AppendLine("                Article = \"ORD-1001\",");
            sb.AppendLine("                Status = OrderStatus.New,");
            sb.AppendLine("                OrderDate = DateTime.UtcNow.Date,");
            sb.AppendLine("                DeliveryDate = null,");
            sb.AppendLine("                PickupCode = \"482913\",");
            sb.AppendLine("                PickupPointId = pickupPoints[0].Id,");
            sb.AppendLine("                ClientId = client?.Id");
            sb.AppendLine("            };");
            sb.AppendLine("            await db.Orders.AddAsync(order);");
            sb.AppendLine("            await db.SaveChangesAsync();");
            sb.AppendLine("        }");
        }

        sb.AppendLine();
        sb.AppendLine("        Directory.CreateDirectory(Path.GetDirectoryName(signaturePath)!);");
        sb.AppendLine("        await File.WriteAllTextAsync(signaturePath, SeedSignature);");
        sb.AppendLine("        if (File.Exists(forcePath))");
        sb.AppendLine("            File.Delete(forcePath);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static async Task EnsureUsersAsync(AppDbContext db)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (await db.Users.AnyAsync())");
        sb.AppendLine("            return;");
        sb.AppendLine();
        sb.AppendLine("        var users = new[]");
        sb.AppendLine("        {");
        sb.AppendLine("            new AppUser { FullName = \"Администратор Системы\", Login = \"admin\", Password = \"admin123\", Role = UserRole.Administrator, Email = \"admin@kodshop.local\" },");
        sb.AppendLine("            new AppUser { FullName = \"Менеджер Склада\", Login = \"manager\", Password = \"manager123\", Role = UserRole.Manager, Email = \"manager@kodshop.local\" },");
        sb.AppendLine("            new AppUser { FullName = \"Иванов Иван Иванович\", Login = \"client\", Password = \"client123\", Role = UserRole.Client, Email = \"client@kodshop.local\" }");
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        await db.Users.AddRangeAsync(users);");
        sb.AppendLine("        await db.SaveChangesAsync();");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void AppendArray(StringBuilder sb, string varName, string typeName, IReadOnlyList<string> values)
    {
        sb.AppendLine($"        var {varName} = new[]");
        sb.AppendLine("        {");
        foreach (var value in values)
            sb.AppendLine($"            new {typeName} {{ Name = \"{Escape(value)}\" }},");
        sb.AppendLine("        };");
    }

    private static void AppendProducts(StringBuilder sb, AssignmentSeedData seed, string makerVar, string makerId)
    {
        sb.AppendLine("        var products = new[]");
        sb.AppendLine("        {");

        for (var i = 0; i < seed.Products.Count; i++)
        {
            var p = seed.Products[i];
            var categoryIndex = Math.Max(0, seed.Categories.FindIndex(c =>
                c.Equals(p.CategoryName, StringComparison.OrdinalIgnoreCase)));
            var makerIndex = Math.Max(0, seed.Makers.FindIndex(m =>
                m.Equals(p.MakerName, StringComparison.OrdinalIgnoreCase)));
            var supplierIndex = Math.Max(0, seed.Suppliers.FindIndex(s =>
                s.Equals(p.SupplierName, StringComparison.OrdinalIgnoreCase)));

            sb.AppendLine("            new Product");
            sb.AppendLine("            {");
            sb.AppendLine($"                Article = \"{Escape(p.Article)}\",");
            sb.AppendLine($"                Name = \"{Escape(p.Name)}\",");
            sb.AppendLine($"                CategoryId = categories[{categoryIndex}].Id,");
            sb.AppendLine($"                Description = \"{Escape(p.Description)}\",");
            sb.AppendLine($"                {makerId} = {makerVar}[{makerIndex}].Id,");
            sb.AppendLine($"                SupplierId = suppliers[{supplierIndex}].Id,");
            sb.AppendLine($"                Price = {p.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)}m,");
            sb.AppendLine("                Unit = \"шт.\",");
            sb.AppendLine($"                QuantityOnStock = {p.Quantity},");
            sb.AppendLine($"                Discount = {p.Discount.ToString(System.Globalization.CultureInfo.InvariantCulture)}m,");
            sb.AppendLine($"                ImagePath = \"images/products/item-{i + 1}.png\"");
            sb.AppendLine("            },");
        }

        sb.AppendLine("        };");
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
