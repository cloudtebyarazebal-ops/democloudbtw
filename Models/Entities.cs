namespace KodShopWeb.Models;

/// <summary>
/// Пользователь системы (клиент, менеджер или администратор).
/// </summary>
public class AppUser
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? Email { get; set; }
    public ICollection<Order> Orders { get; set; } = [];
}

/// <summary>Категория товара.</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<Product> Products { get; set; } = [];
}

/// <summary>Производитель товара.</summary>
public class Manufacturer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<Product> Products { get; set; } = [];
}

/// <summary>Поставщик товара.</summary>
public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<Product> Products { get; set; } = [];
}

/// <summary>Товар каталога магазина.</summary>
public class Product
{
    public int Id { get; set; }
    public string Article { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Unit { get; set; } = "шт.";
    public int QuantityOnStock { get; set; }
    public decimal Discount { get; set; }
    public string? ImagePath { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public int ManufacturerId { get; set; }
    public Manufacturer Manufacturer { get; set; } = null!;

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public ICollection<OrderItem> OrderItems { get; set; } = [];

    public decimal FinalPrice => Discount > 0
        ? Math.Round(Price * (1 - Discount / 100m), 2)
        : Price;

    public bool HasDiscount => Discount > 0;
}

/// <summary>Пункт выдачи заказа.</summary>
public class PickupPoint
{
    public int Id { get; set; }
    public string Address { get; set; } = string.Empty;
    public ICollection<Order> Orders { get; set; } = [];
}

/// <summary>Заказ клиента.</summary>
public class Order
{
    public int Id { get; set; }
    public string Article { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string PickupCode { get; set; } = string.Empty;
    public int PickupPointId { get; set; }
    public PickupPoint PickupPoint { get; set; } = null!;
    public int? ClientId { get; set; }
    public AppUser? Client { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];

    public decimal TotalAmount => Items.Sum(i =>
        i.Product is null ? 0m : i.Quantity * i.Product.FinalPrice);
}

/// <summary>Позиция заказа.</summary>
public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
}