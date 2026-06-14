using System.ComponentModel.DataAnnotations;
using KodShopWeb.Models;

namespace KodShopWeb.ViewModels;

/// <summary>
/// Модель представления формы входа в систему.
/// </summary>
public class LoginViewModel
{
    /// <summary>Логин пользователя.</summary>
    [Required(ErrorMessage = "Введите логин")]
    public string Login { get; set; } = string.Empty;

    /// <summary>Пароль пользователя.</summary>
    [Required(ErrorMessage = "Введите пароль")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Сообщение об ошибке аутентификации (заполняется контроллером).</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Модель формы создания и редактирования товара.
/// Содержит поля сущности, загрузку изображения и справочники для выпадающих списков.
/// </summary>
public class ProductFormViewModel
{
    /// <summary>0 — новый товар; иначе идентификатор для обновления.</summary>
    public int Id { get; set; }

    /// <summary>Артикул товара (уникальный).</summary>
    [Required(ErrorMessage = "Артикул обязателен")]
    public string Article { get; set; } = string.Empty;

    /// <summary>Наименование товара.</summary>
    [Required(ErrorMessage = "Наименование обязательно")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Идентификатор выбранной категории.</summary>
    [Required]
    public int CategoryId { get; set; }

    /// <summary>Описание товара.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Идентификатор производителя.</summary>
    [Required]
    public int ManufacturerId { get; set; }

    /// <summary>Идентификатор поставщика.</summary>
    [Required]
    public int SupplierId { get; set; }

    /// <summary>Базовая цена без скидки.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Цена не может быть отрицательной")]
    public decimal Price { get; set; }

    /// <summary>Единица измерения.</summary>
    public string Unit { get; set; } = "шт.";

    /// <summary>Количество на складе.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "Количество не может быть отрицательным")]
    public int QuantityOnStock { get; set; }

    /// <summary>Процент скидки (0–100).</summary>
    [Range(0, 100, ErrorMessage = "Скидка от 0 до 100%")]
    public decimal Discount { get; set; }

    /// <summary>Текущий путь к изображению (при редактировании без замены файла).</summary>
    public string? CurrentImagePath { get; set; }

    /// <summary>Загружаемый файл нового изображения.</summary>
    public IFormFile? ImageFile { get; set; }

    // --- Справочники для UI ---

    /// <summary>Список категорий для select.</summary>
    public List<Category> Categories { get; set; } = [];

    /// <summary>Список производителей для select.</summary>
    public List<Manufacturer> Manufacturers { get; set; } = [];

    /// <summary>Список поставщиков для select.</summary>
    public List<Supplier> Suppliers { get; set; } = [];
}

/// <summary>
/// Строка таблицы каталога товаров (готовые данные для отображения).
/// </summary>
public class ProductRowViewModel
{
    /// <summary>Идентификатор товара.</summary>
    public int Id { get; set; }

    /// <summary>Артикул.</summary>
    public string Article { get; set; } = string.Empty;

    /// <summary>Наименование.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Название категории (строка, не Id).</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Описание.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Название производителя.</summary>
    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>Название поставщика.</summary>
    public string Supplier { get; set; } = string.Empty;

    /// <summary>Базовая цена.</summary>
    public decimal Price { get; set; }

    /// <summary>Цена с учётом скидки.</summary>
    public decimal FinalPrice { get; set; }

    /// <summary>Есть ли действующая скидка.</summary>
    public bool HasDiscount { get; set; }

    /// <summary>Единица измерения.</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>Остаток на складе.</summary>
    public int QuantityOnStock { get; set; }

    /// <summary>Процент скидки.</summary>
    public decimal Discount { get; set; }

    /// <summary>URL изображения или заглушка picture.png.</summary>
    public string ImageUrl { get; set; } = "/images/picture.png";

    /// <summary>CSS-класс строки (out-of-stock, discount-highlight).</summary>
    public string RowClass { get; set; } = string.Empty;

    /// <summary>Inline-стиль фона строки (hex или transparent).</summary>
    public string RowStyle { get; set; } = string.Empty;
}

/// <summary>
/// Модель главной страницы каталога: список товаров и флаги прав доступа.
/// </summary>
public class ProductsIndexViewModel
{
    /// <summary>Строки таблицы каталога.</summary>
    public List<ProductRowViewModel> Products { get; set; } = [];

    /// <summary>Доступны ли фильтр и сортировка (менеджер/админ).</summary>
    public bool CanUseAdvancedTools { get; set; }

    /// <summary>Может ли пользователь редактировать товары.</summary>
    public bool CanEditProducts { get; set; }

    /// <summary>Порог скидки для подсветки строк (%).</summary>
    public decimal DiscountHighlightPercent { get; set; }

    /// <summary>Цвет подсветки больших скидок.</summary>
    public string DiscountHighlightColor { get; set; } = string.Empty;

    /// <summary>Вариант PU (профильный) или BU (базовый).</summary>
    public bool IsProfileVariant { get; set; }

    /// <summary>Сообщение об успехе/ошибке операции.</summary>
    public string? StatusMessage { get; set; }

    /// <summary>Текущая строка поиска (серверная фильтрация).</summary>
    public string Search { get; set; } = string.Empty;

    /// <summary>Ключ выбранного диапазона скидки.</summary>
    public string DiscountFilterKey { get; set; } = "all";

    /// <summary>Текущее поле сортировки.</summary>
    public ProductSortField SortField { get; set; } = ProductSortField.Price;

    /// <summary>Текущее направление сортировки.</summary>
    public SortDirection SortDirection { get; set; } = SortDirection.Asc;

    /// <summary>Доступные диапазоны скидок для выпадающего списка.</summary>
    public List<DiscountFilterRange> DiscountFilterOptions { get; set; } = [];
}

/// <summary>
/// Модель формы создания и редактирования заказа.
/// </summary>
public class OrderFormViewModel
{
    /// <summary>0 — новый заказ.</summary>
    public int Id { get; set; }

    /// <summary>Артикул заказа.</summary>
    [Required(ErrorMessage = "Артикул обязателен")]
    public string Article { get; set; } = string.Empty;

    /// <summary>Статус заказа.</summary>
    [Required]
    public OrderStatus Status { get; set; }

    /// <summary>Выбранный пункт выдачи.</summary>
    [Required]
    public int PickupPointId { get; set; }

    /// <summary>Дата оформления.</summary>
    [Required]
    public DateTime OrderDate { get; set; }

    /// <summary>Дата доставки (необязательно).</summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>Код получения в пункте выдачи.</summary>
    public string PickupCode { get; set; } = string.Empty;

    /// <summary>Идентификатор клиента (необязательно).</summary>
    public int? ClientId { get; set; }

    // --- Справочники для UI ---

    /// <summary>Список пунктов выдачи.</summary>
    public List<PickupPoint> PickupPoints { get; set; } = [];

    /// <summary>Список клиентов для привязки к заказу.</summary>
    public List<AppUser> Clients { get; set; } = [];

    /// <summary>Позиции заказа (только для просмотра при редактировании).</summary>
    public List<OrderItemLineViewModel> Items { get; set; } = [];

    /// <summary>Итоговая сумма заказа с учётом скидок на товары.</summary>
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// Строка позиции заказа в форме редактирования.
/// </summary>
public class OrderItemLineViewModel
{
    /// <summary>Наименование товара.</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Количество.</summary>
    public int Quantity { get; set; }

    /// <summary>Цена за единицу с учётом скидки.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Сумма по строке.</summary>
    public decimal LineTotal { get; set; }
}

/// <summary>
/// Строка таблицы списка заказов (форматированные поля для View).
/// </summary>
public class OrderRowViewModel
{
    /// <summary>Идентификатор заказа.</summary>
    public int Id { get; set; }

    /// <summary>Артикул заказа.</summary>
    public string Article { get; set; } = string.Empty;

    /// <summary>Текстовый статус («Новый» / «Завершен»).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Адрес пункта выдачи.</summary>
    public string PickupAddress { get; set; } = string.Empty;

    /// <summary>Дата заказа (dd.MM.yyyy).</summary>
    public string OrderDate { get; set; } = string.Empty;

    /// <summary>Дата доставки или «—».</summary>
    public string DeliveryDate { get; set; } = string.Empty;

    /// <summary>ФИО клиента или «—».</summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>Итоговая сумма заказа.</summary>
    public string TotalAmount { get; set; } = string.Empty;
}

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

/// <summary>
/// Модель страницы списка заказов.
/// </summary>
public class OrdersIndexViewModel
{
    /// <summary>Строки таблицы заказов.</summary>
    public List<OrderRowViewModel> Orders { get; set; } = [];

    /// <summary>Может ли пользователь редактировать и удалять заказы.</summary>
    public bool CanEdit { get; set; }

    /// <summary>Flash-сообщение после CRUD-операций.</summary>
    public string? Message { get; set; }
}
