using KodShopWeb.Models;
using KodShopWeb.Services;
using KodShopWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KodShopWeb.Controllers;

/// <summary>
/// Контроллер заказов. Доступен менеджерам и администраторам.
/// В варианте BU (OrdersEnabled=false) список недоступен — редирект в каталог.
/// </summary>
/// <param name="orderService">Сервис работы с заказами.</param>
/// <param name="settings">Настройки магазина.</param>
[Authorize(Roles = $"{nameof(UserRole.Manager)},{nameof(UserRole.Administrator)}")]
public class OrdersController(OrderService orderService, ShopSettings settings) : Controller
{
    /// <summary>
    /// Список заказов с форматированными датами и статусами на русском языке.
    /// </summary>
    /// <param name="message">Flash-сообщение после операций CRUD.</param>
    [HttpGet]
    public async Task<IActionResult> Index(string? message)
    {
        if (!settings.OrdersEnabled)
            return RedirectToAction("Index", "Products");

        var orders = await orderService.GetOrdersAsync();
        return View(new OrdersIndexViewModel
        {
            Orders = orders.Select(o => new OrderRowViewModel
            {
                Id = o.Id,
                Article = o.Article,
                Status = o.Status == OrderStatus.New ? "Новый" : "Завершен",
                PickupAddress = o.PickupPoint.Address,
                OrderDate = o.OrderDate.ToString("dd.MM.yyyy"),
                DeliveryDate = o.DeliveryDate?.ToString("dd.MM.yyyy") ?? "—",
                ClientName = o.Client?.FullName ?? "—"
            }).ToList(),
            CanEdit = UserAccess.CanEditOrders(User),
            Message = message
        });
    }

    /// <summary>
    /// Форма создания заказа с кодом выдачи по умолчанию (6 символов из GUID).
    /// </summary>
    [Authorize(Roles = nameof(UserRole.Administrator))]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        return View("Edit", await BuildFormAsync(new Order
        {
            Status = OrderStatus.New,
            OrderDate = DateTime.Today,
            PickupCode = Guid.NewGuid().ToString("N")[..6]
        }));
    }

    /// <summary>Форма редактирования существующего заказа.</summary>
    /// <param name="id">Идентификатор заказа.</param>
    [Authorize(Roles = nameof(UserRole.Administrator))]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var order = await orderService.GetByIdAsync(id);
        if (order is null)
            return NotFound();

        return View(await BuildFormAsync(order));
    }

    /// <summary>Сохраняет заказ (создание или обновление шапки заказа).</summary>
    /// <param name="model">Данные формы заказа.</param>
    [Authorize(Roles = nameof(UserRole.Administrator))]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(OrderFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await FillLookupsAsync(model);
            return View("Edit", model);
        }

        var order = new Order
        {
            Id = model.Id,
            Article = model.Article,
            Status = model.Status,
            OrderDate = model.OrderDate,
            DeliveryDate = model.DeliveryDate,
            PickupCode = model.PickupCode,
            PickupPointId = model.PickupPointId,
            ClientId = model.ClientId
        };

        var (success, error) = await orderService.SaveAsync(order);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Ошибка сохранения.");
            await FillLookupsAsync(model);
            return View("Edit", model);
        }

        return RedirectToAction(nameof(Index), new { message = "Заказ сохранён." });
    }

    /// <summary>Удаляет заказ по идентификатору.</summary>
    /// <param name="id">Идентификатор заказа.</param>
    [Authorize(Roles = nameof(UserRole.Administrator))]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await orderService.DeleteAsync(id);
        return RedirectToAction(nameof(Index), new { message = success ? "Заказ удалён." : error });
    }

    /// <summary>
    /// Собирает модель формы заказа со списками пунктов выдачи и клиентов.
    /// </summary>
    private async Task<OrderFormViewModel> BuildFormAsync(Order order) =>
        new()
        {
            Id = order.Id,
            Article = order.Article,
            Status = order.Status,
            OrderDate = order.OrderDate,
            DeliveryDate = order.DeliveryDate,
            PickupCode = order.PickupCode,
            PickupPointId = order.PickupPointId,
            ClientId = order.ClientId,
            PickupPoints = await orderService.GetPickupPointsAsync(),
            Clients = await orderService.GetClientsAsync()
        };

    /// <summary>Перезагружает справочники для формы при ошибке валидации.</summary>
    private async Task FillLookupsAsync(OrderFormViewModel model)
    {
        model.PickupPoints = await orderService.GetPickupPointsAsync();
        model.Clients = await orderService.GetClientsAsync();
    }
}

/// <summary>
/// Контроллер импорта данных из Excel-папки (только администратор).
/// </summary>
/// <param name="importService">Сервис импорта пользователей и товаров.</param>
[Authorize(Roles = nameof(UserRole.Administrator))]
public class ImportController(ImportService importService) : Controller
{
    /// <summary>Отображает форму указания пути к папке импорта.</summary>
    [HttpGet]
    public IActionResult Index() => View(new ImportViewModel());

    /// <summary>
    /// Запускает импорт из указанной папки и отображает результат на той же странице.
    /// </summary>
    /// <param name="model">Модель с путём к папке и полями результата.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ImportViewModel model)
    {
        var (success, message) = await importService.ImportFromFolderAsync(model.FolderPath);
        model.Success = success;
        model.Message = message;
        return View(model);
    }
}
