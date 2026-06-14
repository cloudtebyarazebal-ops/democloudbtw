namespace ExamCoachDesktop;

/// <summary>Замена производителя на автора для книжных заданий.</summary>
public static class AuthorDomainPatches
{
    public static bool IsApplicable(AssignmentRequirements req) =>
        req.Features.Contains(AssignmentFeatures.AuthorField) &&
        !string.Equals(req.ExamVariant, "BU", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<TextReplacement> GetReplacements() =>
    [
        new() { From = "DbSet<Manufacturer>", To = "DbSet<Author>" },
        new() { From = "class Manufacturer", To = "class Author" },
        new() { From = "EnsureManufacturerAsync", To = "EnsureAuthorAsync" },
        new() { From = "GetManufacturersAsync", To = "GetAuthorsAsync" },
        new() { From = "db.Manufacturers", To = "db.Authors" },
        new() { From = "ManufacturerId", To = "AuthorId" },
        new() { From = "ManufacturerName", To = "AuthorName" },
        new() { From = "manufacturerName", To = "authorName" },
        new() { From = "manufacturer", To = "author" },
        new() { From = "new Manufacturer {", To = "new Author {" },
        new() { From = " Author Manufacturer {", To = " Author Author {" },
        new() { From = "public string Manufacturer", To = "public string Author" },
        new() { From = "Manufacturer =", To = "Author =" },
        new() { From = "=> p.Manufacturer", To = "=> p.Author" },
        new() { From = "p.Manufacturer", To = "p.Author" },
        new() { From = "product.Manufacturer", To = "product.Author" },
        new() { From = "@product.Manufacturer", To = "@product.Author" },
        new() { From = "Manufacturer.", To = "Author." },
        new() { From = "(Manufacturer", To = "(Author" },
        new() { From = "Manufacturer ", To = "Author " },
        new() { From = "Manufacturers", To = "Authors" },
        new() { From = "manufacturers", To = "authors" },
        new() { From = "Manufacturer>", To = "Author>" },
        new() { From = "Производитель", To = "Автор" },
        new() { From = "производител", To = "автор" },
    ];

    public static void Apply(CoachData data, AssignmentRequirements req)
    {
        if (!IsApplicable(req)) return;
        TextAdaptEngine.AdaptInPlace(data, GetReplacements());
    }
}
