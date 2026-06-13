using KodShopWeb.Data;
using KodShopWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace KodShopWeb.Services;

/// <summary>
/// Параметры запроса списка товаров: поиск, фильтр по скидке, сортировка.
/// Используется в расширенном режиме каталога (менеджер/администратор).
/// </summary>
public class ProductQuery
{
    /// <summary>Строка поиска по полям товара и справочникам.</summary>
    public string Search { get; set; } = string.Empty;

    /// <summary>Выбранный диапазон скидки для фильтрации.</summary>
    public DiscountFilterRange DiscountFilter { get; set; } = new("Все диапазоны", null, null);

    /// <summary>Поле сортировки.</summary>
    public ProductSortField SortField { get; set; } = ProductSortField.Price;

    /// <summary>Направление сортировки.</summary>
    public SortDirection SortDirection { get; set; } = SortDirection.Asc;
}

/// <summary>
/// Сервис работы с каталогом товаров: выборка, CRUD, загрузка изображений.
/// </summary>
/// <param name="db">Контекст базы данных.</param>
public class ProductService(AppDbContext db)
{
    /// <summary>
    /// Возвращает список товаров с учётом прав пользователя.
    /// Для гостей/клиентов — простая сортировка по имени; для менеджеров — фильтры и сортировка из query.
    /// </summary>
    /// <param name="query">Параметры фильтрации и сортировки.</param>
    /// <param name="advancedToolsEnabled">Включены ли расширенные инструменты.</param>
    /// <returns>Список товаров с подгруженными справочниками.</returns>
    public async Task<List<Product>> GetProductsAsync(ProductQuery query, bool advancedToolsEnabled)
    {
        var products = db.Products
            .Include(p => p.Category)
            .Include(p => p.Manufacturer)
            .Include(p => p.Supplier)
            .AsQueryable();

        if (advancedToolsEnabled)
            products = ApplyFilters(products, query);

        var list = await products.ToListAsync();

        // Без расширенных инструментов — только алфавитный порядок по наименованию.
        return advancedToolsEnabled ? ApplySort(list, query) : list.OrderBy(p => p.Name).ToList();
    }

    /// <summary>
    /// Загружает один товар по идентификатору со всеми связанными справочниками.
    /// </summary>
    /// <param name="id">Идентификатор товара.</param>
    public Task<Product?> GetByIdAsync(int id) =>
        db.Products
            .Include(p => p.Category)
            .Include(p => p.Manufacturer)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == id);

    /// <summary>Возвращает все категории, отсортированные по имени.</summary>
    public Task<List<Category>> GetCategoriesAsync() =>
        db.Categories.OrderBy(c => c.Name).ToListAsync();

    /// <summary>Возвращает всех производителей, отсортированных по имени.</summary>
    public Task<List<Manufacturer>> GetManufacturersAsync() =>
        db.Manufacturers.OrderBy(m => m.Name).ToListAsync();

    /// <summary>Возвращает всех поставщиков, отсортированных по имени.</summary>
    public Task<List<Supplier>> GetSuppliersAsync() =>
        db.Suppliers.OrderBy(s => s.Name).ToListAsync();

    /// <summary>
    /// Создаёт или обновляет товар с валидацией и опциональной загрузкой изображения.
    /// </summary>
    /// <param name="product">Данные товара (Id=0 — создание).</param>
    /// <param name="imageStream">Поток нового изображения или null.</param>
    /// <param name="imageFileName">Имя файла изображения.</param>
    /// <param name="env">Окружение для пути wwwroot.</param>
    /// <returns>Кортеж (успех, текст ошибки).</returns>
    public async Task<(bool Success, string? Error)> SaveAsync(
        Product product,
        Stream? imageStream,
        string? imageFileName,
        IWebHostEnvironment env)
    {
        // --- Серверная валидация бизнес-правил ---
        if (product.Price < 0)
            return (false, "Цена не может быть отрицательной.");

        if (product.QuantityOnStock < 0)
            return (false, "Количество на складе не может быть отрицательным.");

        if (product.Discount < 0 || product.Discount > 100)
            return (false, "Скидка должна быть от 0 до 100%.");

        if (string.IsNullOrWhiteSpace(product.Article))
            return (false, "Артикул обязателен.");

        if (string.IsNullOrWhiteSpace(product.Name))
            return (false, "Наименование обязательно.");

        var duplicateArticle = await db.Products.AnyAsync(p => p.Article == product.Article && p.Id != product.Id);
        if (duplicateArticle)
            return (false, "Товар с таким артикулом уже существует.");

        if (product.Id == 0)
        {
            db.Products.Add(product);
        }
        else
        {
            // Обновление существующей записи — копируем поля в tracked-сущность.
            var existing = await db.Products.FindAsync(product.Id);
            if (existing is null)
                return (false, "Товар не найден.");

            existing.Article = product.Article;
            existing.Name = product.Name;
            existing.Description = product.Description;
            existing.Price = product.Price;
            existing.Unit = product.Unit;
            existing.QuantityOnStock = product.QuantityOnStock;
            existing.Discount = product.Discount;
            existing.CategoryId = product.CategoryId;
            existing.ManufacturerId = product.ManufacturerId;
            existing.SupplierId = product.SupplierId;
            product = existing;
        }

        if (imageStream is not null && !string.IsNullOrWhiteSpace(imageFileName))
        {
            var imageResult = await SaveImageAsync(product, imageStream, imageFileName, env);
            if (!imageResult.Success)
                return imageResult;
        }

        await db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>
    /// Удаляет товар, если он не участвует в заказах.
    /// Также удаляет файл изображения с диска.
    /// </summary>
    /// <param name="id">Идентификатор товара.</param>
    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var product = await db.Products
            .Include(p => p.OrderItems)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
            return (false, "Товар не найден.");

        if (product.OrderItems.Count > 0)
            return (false, "Нельзя удалить товар, который присутствует в заказе.");

        if (!string.IsNullOrWhiteSpace(product.ImagePath))
            DeletePhysicalImage(product.ImagePath, null);

        db.Products.Remove(product);
        await db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>
    /// Применяет текстовый поиск и фильтр по диапазону скидки к IQueryable.
    /// </summary>
    private static IQueryable<Product> ApplyFilters(IQueryable<Product> source, ProductQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            // Поиск по артикулу, названию, описанию и связанным справочникам.
            source = source.Where(p =>
                p.Article.ToLower().Contains(term) ||
                p.Name.ToLower().Contains(term) ||
                p.Description.ToLower().Contains(term) ||
                p.Category.Name.ToLower().Contains(term) ||
                p.Manufacturer.Name.ToLower().Contains(term) ||
                p.Supplier.Name.ToLower().Contains(term) ||
                p.Unit.ToLower().Contains(term));
        }

        if (query.DiscountFilter.Min is not null)
            source = source.Where(p => p.Discount >= query.DiscountFilter.Min);

        if (query.DiscountFilter.Max is not null)
            source = source.Where(p => p.Discount <= query.DiscountFilter.Max);

        return source;
    }

    /// <summary>
    /// Сортирует материализованный список товаров в памяти по выбранному полю.
    /// </summary>
    private static List<Product> ApplySort(List<Product> products, ProductQuery query)
    {
        Func<Product, IComparable> keySelector = query.SortField switch
        {
            ProductSortField.Quantity => p => p.QuantityOnStock,
            ProductSortField.Discount => p => p.Discount,
            _ => p => p.Price
        };

        return query.SortDirection == SortDirection.Asc
            ? products.OrderBy(keySelector).ToList()
            : products.OrderByDescending(keySelector).ToList();
    }

    /// <summary>
    /// Сохраняет изображение товара в wwwroot/images/products.
    /// Имя файла формируется из артикула; допустимы JPG, PNG, WEBP.
    /// </summary>
    private async Task<(bool Success, string? Error)> SaveImageAsync(
        Product product,
        Stream imageStream,
        string imageFileName,
        IWebHostEnvironment env)
    {
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(imageFileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            return (false, "Допустимы только изображения JPG, PNG или WEBP.");

        var imagesDir = Path.Combine(env.WebRootPath, "images", "products");
        Directory.CreateDirectory(imagesDir);

        var fileName = $"{product.Article}{ext}".Replace(" ", "_");
        var physicalPath = Path.Combine(imagesDir, fileName);

        // Удаляем старое изображение, если оно отличается от нового пути.
        DeletePhysicalImage(product.ImagePath, physicalPath);

        await using var target = File.Create(physicalPath);
        imageStream.Position = 0;
        await imageStream.CopyToAsync(target);

        product.ImagePath = $"/images/products/{fileName}";
        return (true, null);
    }

    /// <summary>
    /// Удаляет физический файл изображения по относительному URL.
    /// </summary>
    /// <param name="relativePath">Путь вида /images/products/...</param>
    /// <param name="exceptPath">Физический путь, который не нужно удалять (новый файл).</param>
    private static void DeletePhysicalImage(string? relativePath, string? exceptPath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return;

        var physical = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(physical) && !string.Equals(physical, exceptPath, StringComparison.OrdinalIgnoreCase))
            File.Delete(physical);
    }
}
