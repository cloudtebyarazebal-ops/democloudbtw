using KodShopWeb.Models;
using KodShopWeb.Services;
using KodShopWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KodShopWeb.Controllers;

/// <summary>
/// Контроллер обработки ошибок приложения (страница /Home/Error).
/// </summary>
public class HomeController : Controller
{
    /// <summary>Отображает представление ошибки.</summary>
    public IActionResult Error() => View();
}

/// <summary>
/// Контроллер каталога товаров: список, создание, редактирование и удаление.
/// </summary>
/// <param name="productService">Сервис работы с товарами.</param>
/// <param name="settings">Настройки магазина (подсветка скидок, вариант PU/BU).</param>
public class ProductsController(ProductService productService, ShopSettings settings) : Controller
{
    /// <summary>
    /// Главная страница каталога. Права определяют доступ к расширенным инструментам и CRUD.
    /// Поиск, фильтрация и сортировка выполняются на сервере через query string.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? discountFilter,
        string? sortField,
        string? sortDirection,
        string? message)
    {
        var advanced = UserAccess.CanUseAdvancedProductTools(User);
        var query = advanced
            ? ProductQuery.FromRequest(search, discountFilter, sortField, sortDirection, settings)
            : new ProductQuery();
        var products = await productService.GetProductsAsync(query, advanced);
        var filterRanges = ShopVariantPresets.GetFilterRanges(settings);

        return View(new ProductsIndexViewModel
        {
            Products = products.Select(p => ProductPresentation.Map(p, settings)).ToList(),
            CanUseAdvancedTools = advanced,
            CanEditProducts = UserAccess.CanEditProducts(User),
            DiscountHighlightPercent = settings.DiscountHighlightPercent,
            DiscountHighlightColor = settings.DiscountHighlightColor,
            IsProfileVariant = settings.IsProfileVariant,
            StatusMessage = message,
            Search = query.Search,
            DiscountFilterKey = query.DiscountFilter.Key,
            SortField = query.SortField,
            SortDirection = query.SortDirection,
            DiscountFilterOptions = filterRanges.ToList()
        });
    }

    /// <summary>Форма создания нового товара (только администратор).</summary>
    [Authorize(Roles = nameof(UserRole.Administrator))]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        return View("Edit", await BuildFormAsync(new Product { Unit = "шт." }));
    }

    /// <summary>Форма редактирования существующего товара.</summary>
    /// <param name="id">Идентификатор товара.</param>
    [Authorize(Roles = nameof(UserRole.Administrator))]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await productService.GetByIdAsync(id);
        if (product is null)
            return NotFound();

        return View(await BuildFormAsync(product));
    }

    /// <summary>
    /// Сохраняет товар (создание или обновление) с опциональной загрузкой изображения.
    /// </summary>
    /// <param name="model">Данные формы товара.</param>
    [Authorize(Roles = nameof(UserRole.Administrator))]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ProductFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await FillLookupsAsync(model);
            return View("Edit", model);
        }

        var product = new Product
        {
            Id = model.Id,
            Article = model.Article,
            Name = model.Name,
            Description = model.Description,
            Price = model.Price,
            Unit = model.Unit,
            QuantityOnStock = model.QuantityOnStock,
            Discount = model.Discount,
            CategoryId = model.CategoryId,
            ManufacturerId = model.ManufacturerId,
            SupplierId = model.SupplierId,
            ImagePath = model.CurrentImagePath
        };

        Stream? imageStream = null;
        if (model.ImageFile is not null && model.ImageFile.Length > 0)
        {
            imageStream = model.ImageFile.OpenReadStream();
        }

        var (success, error) = await productService.SaveAsync(
            product,
            imageStream,
            model.ImageFile?.FileName,
            HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>());

        if (imageStream is not null)
            await imageStream.DisposeAsync();

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Ошибка сохранения.");
            await FillLookupsAsync(model);
            return View("Edit", model);
        }

        return RedirectToIndex("Товар сохранён.");
    }

    /// <summary>Удаляет товар по идентификатору.</summary>
    /// <param name="id">Идентификатор товара.</param>
    [Authorize(Roles = nameof(UserRole.Administrator))]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await productService.DeleteAsync(id);
        var message = success ? "Товар удалён." : error;
        return RedirectToIndex(message);
    }

    /// <summary>
    /// Преобразует сущность Product в модель формы и заполняет справочники.
    /// </summary>
    private async Task<ProductFormViewModel> BuildFormAsync(Product product)
    {
        var model = new ProductFormViewModel
        {
            Id = product.Id,
            Article = product.Article,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            Unit = product.Unit,
            QuantityOnStock = product.QuantityOnStock,
            Discount = product.Discount,
            CategoryId = product.CategoryId,
            ManufacturerId = product.ManufacturerId,
            SupplierId = product.SupplierId,
            CurrentImagePath = product.ImagePath
        };
        await FillLookupsAsync(model);
        return model;
    }

    /// <summary>
    /// Загружает списки категорий, производителей и поставщиков;
    /// для нового товара подставляет первые значения по умолчанию.
    /// </summary>
    private async Task FillLookupsAsync(ProductFormViewModel model)
    {
        model.Categories = await productService.GetCategoriesAsync();
        model.Manufacturers = await productService.GetManufacturersAsync();
        model.Suppliers = await productService.GetSuppliersAsync();

        if (model.CategoryId == 0 && model.Categories.Count > 0)
            model.CategoryId = model.Categories[0].Id;
        if (model.ManufacturerId == 0 && model.Manufacturers.Count > 0)
            model.ManufacturerId = model.Manufacturers[0].Id;
        if (model.SupplierId == 0 && model.Suppliers.Count > 0)
            model.SupplierId = model.Suppliers[0].Id;
    }

    /// <summary>
    /// Редирект на каталог с сохранением параметров фильтрации из текущего запроса.
    /// </summary>
    private IActionResult RedirectToIndex(string? message)
    {
        static string Pick(HttpRequest request, string key)
        {
            var fromQuery = request.Query[key].ToString();
            if (!string.IsNullOrEmpty(fromQuery))
                return fromQuery;

            var fromForm = request.Form[key].ToString();
            return string.IsNullOrEmpty(fromForm) ? string.Empty : fromForm;
        }

        return RedirectToAction(nameof(Index), new
        {
            message,
            search = Pick(Request, "search"),
            discountFilter = Pick(Request, "discountFilter"),
            sortField = Pick(Request, "sortField"),
            sortDirection = Pick(Request, "sortDirection")
        });
    }
}

/// <summary>
/// Преобразование сущности Product в строку таблицы каталога с правилами подсветки.
/// </summary>
public static class ProductPresentation
{
    /// <summary>
    /// Формирует ViewModel строки каталога: цены, изображение, CSS-класс и цвет фона.
    /// Серый фон — нет на складе; акцентный — скидка выше порога из настроек.
    /// </summary>
    /// <param name="product">Сущность товара.</param>
    /// <param name="settings">Настройки порога и цвета подсветки скидки.</param>
    public static ProductRowViewModel Map(Product product, ShopSettings settings)
    {
        var rowStyle = "transparent";
        var rowClass = string.Empty;

        if (product.QuantityOnStock <= 0)
        {
            rowStyle = "#d9d9d9";
            rowClass = "row-out-of-stock";
        }
        else if (product.Discount > settings.DiscountHighlightPercent)
        {
            rowStyle = settings.DiscountHighlightColor;
            rowClass = "row-discount-highlight";
        }

        return new ProductRowViewModel
        {
            Id = product.Id,
            Article = product.Article,
            Name = product.Name,
            Category = product.Category.Name,
            Description = product.Description,
            Manufacturer = product.Manufacturer.Name,
            Supplier = product.Supplier.Name,
            Price = product.Price,
            FinalPrice = product.FinalPrice,
            HasDiscount = product.HasDiscount,
            Unit = product.Unit,
            QuantityOnStock = product.QuantityOnStock,
            Discount = product.Discount,
            ImageUrl = string.IsNullOrWhiteSpace(product.ImagePath) ? "/images/picture.png" : product.ImagePath,
            RowClass = rowClass,
            RowStyle = rowStyle
        };
    }
}
