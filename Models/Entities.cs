namespace KodShopWeb.Models;

/// <summary>
/// Пользователь системы (клиент, менеджер или администратор).
/// Хранит учётные данные и связь с заказами клиента.
/// </summary>
public class AppUser
{
    /// <summary>Уникальный идентификатор пользователя.</summary>
    public int Id { get; set; }

    /// <summary>ФИО для отображения в интерфейсе.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Логин для входа (уникальный).</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Пароль (в учебном проекте хранится в открытом виде).</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Роль определяет права доступа.</summary>
    public UserRole Role { get; set; }

    /// <summary>Электронная почта (необязательное поле).</summary>
    public string? Email { get; set; }

    /// <summary>Заказы, оформленные данным клиентом.</summary>
    public ICollection<Order> Orders { get; set; } = [];
}

/// <summary>
/// Категория товара (например: роман, учебник).
/// </summary>
public class Category
{
    /// <summary>Уникальный идентификатор категории.</summary>
    public int Id { get; set; }

    /// <summary>Наименование категории.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Товары, относящиеся к категории.</summary>
    public ICollection<Product> Products { get; set; } = [];
}

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

/// <summary>
/// Поставщик товара.
/// </summary>
public class Supplier
{
    /// <summary>Уникальный идентификатор поставщика.</summary>
    public int Id { get; set; }

    /// <summary>Наименование поставщика.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Товары от данного поставщика.</summary>
    public ICollection<Product> Products { get; set; } = [];
}

/// <summary>
/// Товар каталога магазина.
/// Содержит цену, скидку, остаток и связи со справочниками.
/// </summary>
public class Product
{
    /// <summary>Уникальный идентификатор товара.</summary>
    public int Id { get; set; }

    /// <summary>Артикул (уникальный бизнес-ключ).</summary>
    public string Article { get; set; } = string.Empty;

    /// <summary>Наименование товара.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Текстовое описание.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Базовая цена без учёта скидки.</summary>
    public decimal Price { get; set; }

    /// <summary>Единица измерения (по умолчанию «шт.»).</summary>
    public string Unit { get; set; } = "шт.";

    /// <summary>Количество единиц на складе.</summary>
    public int QuantityOnStock { get; set; }

    /// <summary>Процент скидки (0–100).</summary>
    public decimal Discount { get; set; }

    /// <summary>Относительный путь к изображению в wwwroot (например /images/products/1.jpg).</summary>
    public string? ImagePath { get; set; }

    // --- Внешние ключи и навигационные свойства ---

    /// <summary>Идентификатор категории.</summary>
    public int CategoryId { get; set; }

    /// <summary>Связанная категория.</summary>
    public Category Category { get; set; } = null!;

    /// <summary>Идентификатор производителя.</summary>
    public int ManufacturerId { get; set; }

    /// <summary>Связанный производитель.</summary>
    public Manufacturer Manufacturer { get; set; } = null!;

    /// <summary>Идентификатор поставщика.</summary>
    public int SupplierId { get; set; }

    /// <summary>Связанный поставщик.</summary>
    public Supplier Supplier { get; set; } = null!;

    /// <summary>Позиции заказов, в которых участвует товар.</summary>
    public ICollection<OrderItem> OrderItems { get; set; } = [];

    /// <summary>
    /// Итоговая цена с учётом скидки.
    /// При Discount &gt; 0: Price × (1 − Discount/100), округление до 2 знаков.
    /// </summary>
    public decimal FinalPrice => Discount > 0
        ? Math.Round(Price * (1 - Discount / 100m), 2)
        : Price;

    /// <summary>Признак наличия действующей скидки.</summary>
    public bool HasDiscount => Discount > 0;
}

/// <summary>
/// Пункт выдачи заказа (адрес самовывоза).
/// </summary>
public class PickupPoint
{
    /// <summary>Уникальный идентификатор пункта выдачи.</summary>
    public int Id { get; set; }

    /// <summary>Полный адрес пункта выдачи.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Заказы, привязанные к данному пункту.</summary>
    public ICollection<Order> Orders { get; set; } = [];
}

/// <summary>
/// Заказ клиента с датами, статусом и пунктом выдачи.
/// </summary>
public class Order
{
    /// <summary>Уникальный идентификатор заказа.</summary>
    public int Id { get; set; }

    /// <summary>Артикул заказа (уникальный).</summary>
    public string Article { get; set; } = string.Empty;

    /// <summary>Текущий статус обработки.</summary>
    public OrderStatus Status { get; set; }

    /// <summary>Дата оформления заказа.</summary>
    public DateTime OrderDate { get; set; }

    /// <summary>Дата доставки/выдачи (null — ещё не доставлен).</summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>Код получения в пункте выдачи.</summary>
    public string PickupCode { get; set; } = string.Empty;

    // --- Пункт выдачи ---

    /// <summary>Идентификатор пункта выдачи.</summary>
    public int PickupPointId { get; set; }

    /// <summary>Связанный пункт выдачи.</summary>
    public PickupPoint PickupPoint { get; set; } = null!;

    // --- Клиент (необязательно) ---

    /// <summary>Идентификатор клиента (null — заказ без привязки к пользователю).</summary>
    public int? ClientId { get; set; }

    /// <summary>Связанный клиент.</summary>
    public AppUser? Client { get; set; }

    /// <summary>Строки заказа (товары и количества).</summary>
    public ICollection<OrderItem> Items { get; set; } = [];
}

/// <summary>
/// Позиция заказа: товар и заказанное количество.
/// </summary>
public class OrderItem
{
    /// <summary>Уникальный идентификатор позиции.</summary>
    public int Id { get; set; }

    /// <summary>Идентификатор родительского заказа.</summary>
    public int OrderId { get; set; }

    /// <summary>Родительский заказ.</summary>
    public Order Order { get; set; } = null!;

    /// <summary>Идентификатор товара.</summary>
    public int ProductId { get; set; }

    /// <summary>Связанный товар.</summary>
    public Product Product { get; set; } = null!;

    /// <summary>Заказанное количество единиц.</summary>
    public int Quantity { get; set; }
}
