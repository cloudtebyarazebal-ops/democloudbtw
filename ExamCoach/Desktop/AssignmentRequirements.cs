namespace ExamCoachDesktop;

/// <summary>Структурированные требования, извлечённые из текста задания.</summary>
public sealed class AssignmentRequirements
{
    /// <summary>Kod — стандартный демоэкзамен; Custom — учебное ТЗ.</summary>
    public string? AssignmentKind { get; set; }

    public string? ExamVariant { get; set; }
    public string? DomainLabel { get; set; }
    public string ProjectName { get; set; } = "KodShopWeb";
    public string BrandName { get; set; } = "KodShop";
    public string ProductWord { get; set; } = "товар";
    public HashSet<string> Features { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Modules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> ModuleMinutes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public AssignmentSeedData? SeedData { get; set; }
}

public static class AssignmentFeatures
{
    public const string Auth = "Auth";
    public const string Guest = "Guest";
    public const string Import = "Import";
    public const string Orders = "Orders";
    public const string ProductCrud = "ProductCrud";
    public const string ProductFilters = "ProductFilters";
    public const string ProductSearch = "ProductSearch";
    public const string ProductSort = "ProductSort";
    public const string ErDiagram = "ErDiagram";
    public const string ErrorHandling = "ErrorHandling";
    public const string SequentialNav = "SequentialNav";
    public const string DiscountUi = "DiscountUi";

    /// <summary>Пункты выдачи, адрес ПВЗ, код получения.</summary>
    public const string PickupPoints = "PickupPoints";

    /// <summary>Дата доставки в заказах.</summary>
    public const string OrderDelivery = "OrderDelivery";

    /// <summary>Явные статусы заказа в UI (новый/завершён и т.п.).</summary>
    public const string OrderStatusDetail = "OrderStatusDetail";

    /// <summary>Поле «автор» у товара (книги), вместо производителя.</summary>
    public const string AuthorField = "AuthorField";
}
