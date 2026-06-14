namespace ExamCoachDesktop;

/// <summary>Данные для сида БД, извлечённые из текста ТЗ.</summary>
public sealed class AssignmentSeedData
{
    public bool UseAuthorField { get; set; }
    public string ProductWord { get; set; } = "товар";
    public string DomainLabel { get; set; } = "магазин";
    public string MakerFieldLabel { get; set; } = "Производитель";
    public List<string> Categories { get; set; } = [];
    public List<string> Makers { get; set; } = [];
    public List<string> Suppliers { get; set; } = [];
    public List<SeedProductTemplate> Products { get; set; } = [];
    public decimal DiscountHighlightPercent { get; set; } = 10m;
    public string DiscountHighlightColor { get; set; } = "#90EE90";
    public string? ShopName { get; set; }
    public bool OrdersEnabled { get; set; } = true;
    public List<DiscountRangeTemplate> DiscountRanges { get; set; } = [];
}

public sealed class SeedProductTemplate
{
    public string Article { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string MakerName { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public decimal Discount { get; set; }
}

public sealed class DiscountRangeTemplate
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
}
