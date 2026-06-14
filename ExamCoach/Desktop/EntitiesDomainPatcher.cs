using System.Text.RegularExpressions;

namespace ExamCoachDesktop;

/// <summary>Приводит Models/Entities.cs к домену из ТЗ (Author или Manufacturer).</summary>
public static class EntitiesDomainPatcher
{
    public static string Patch(string code, bool authorMode)
    {
        if (authorMode)
            return PatchToAuthor(code);

        return PatchToManufacturer(code);
    }

    private static string PatchToAuthor(string code)
    {
        code = RemoveType(code, "Manufacturer");
        code = RemoveType(code, "Author");

        if (code.Contains("public class Manufacturer", StringComparison.Ordinal))
            code = code.Replace("public class Manufacturer", "public class Author");

        if (!code.Contains("class Author", StringComparison.Ordinal))
        {
            code = code.Replace(
                "public class Category",
                """
                /// <summary>
                /// Автор товара.
                /// </summary>
                public class Author
                {
                    /// <summary>Уникальный идентификатор автора.</summary>
                    public int Id { get; set; }

                    /// <summary>Наименование автора.</summary>
                    public string Name { get; set; } = string.Empty;

                    /// <summary>Товары данного автора.</summary>
                    public ICollection<Product> Products { get; set; } = [];
                }

                public class Category
                """);
        }

        code = code.Replace("DbSet<Manufacturer>", "DbSet<Author>");
        return PatchProductFk(code, "Author");
    }

    private static string PatchToManufacturer(string code)
    {
        code = RemoveType(code, "Manufacturer");
        code = RemoveType(code, "Author");

        if (code.Contains("public class Author", StringComparison.Ordinal))
            code = code.Replace("public class Author", "public class Manufacturer");

        if (!code.Contains("class Manufacturer", StringComparison.Ordinal))
        {
            code = code.Replace(
                "public class Category",
                """
                /// <summary>
                /// Производитель товара.
                /// </summary>
                public class Manufacturer
                {
                    /// <summary>Уникальный идентификатор производителя.</summary>
                    public int Id { get; set; }

                    /// <summary>Наименование производителя.</summary>
                    public string Name { get; set; } = string.Empty;

                    /// <summary>Товары данного производителя.</summary>
                    public ICollection<Product> Products { get; set; } = [];
                }

                public class Category
                """);
        }

        code = code.Replace("DbSet<Author>", "DbSet<Manufacturer>");
        return PatchProductFk(code, "Manufacturer");
    }

    private static string PatchProductFk(string code, string makerType)
    {
        var idName = makerType + "Id";
        var oppositeId = makerType == "Author" ? "ManufacturerId" : "AuthorId";
        var oppositeType = makerType == "Author" ? "Manufacturer" : "Author";

        code = Regex.Replace(code, $@"public int {oppositeId}[^\n]*\n", "");
        code = Regex.Replace(code, $@"public {oppositeType} {oppositeType}[^\n]*\n", "");
        code = Regex.Replace(code, $@"/// <summary>Идентификатор {GetLabel(oppositeType)}.</summary>\s*\n", "");

        if (!code.Contains($"public int {idName}", StringComparison.Ordinal))
        {
            code = code.Replace(
                "    /// <summary>Связанная категория.</summary>\r\n    public Category Category { get; set; } = null!;\r\n",
                $"    /// <summary>Связанная категория.</summary>\r\n    public Category Category {{ get; set; }} = null!;\r\n\r\n    /// <summary>Идентификатор {GetLabel(makerType)}.</summary>\r\n    public int {idName} {{ get; set; }}\r\n\r\n    /// <summary>Связанный {GetLabel(makerType)}.</summary>\r\n    public {makerType} {makerType} {{ get; set; }} = null!;\r\n");
            code = code.Replace(
                "    /// <summary>Связанная категория.</summary>\n    public Category Category { get; set; } = null!;\n",
                $"    /// <summary>Связанная категория.</summary>\n    public Category Category {{ get; set; }} = null!;\n\n    /// <summary>Идентификатор {GetLabel(makerType)}.</summary>\n    public int {idName} {{ get; set; }}\n\n    /// <summary>Связанный {GetLabel(makerType)}.</summary>\n    public {makerType} {makerType} {{ get; set; }} = null!;\n");
        }

        code = code.Replace(oppositeId, idName)
            .Replace($"{oppositeType} {oppositeType}", $"{makerType} {makerType}")
            .Replace($"public {oppositeType}", $"public {makerType}");

        return code;
    }

    private static string RemoveType(string code, string typeName)
    {
        var pattern = new Regex(
            $@"/// <summary>[\s\S]*?</summary>\s*public class {typeName}\s*\{{[\s\S]*?\}}\s*",
            RegexOptions.Multiline);
        return pattern.Replace(code, "");
    }

    private static string GetLabel(string makerType) =>
        makerType == "Author" ? "автора" : "производителя";
}
