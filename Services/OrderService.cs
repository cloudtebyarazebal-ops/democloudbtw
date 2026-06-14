using KodShopWeb.Data;
using KodShopWeb.Models;
using Microsoft.EntityFrameworkCore;

using Order = KodShopWeb.Models.Order;
namespace KodShopWeb.Services;

/// <summary>
/// Сервис управления заказами: выборка, сохранение и удаление.
/// </summary>
/// <param name="db">Контекст базы данных.</param>
public class OrderService(AppDbContext db)
{
    /// <summary>
    /// Возвращает все заказы с пунктами выдачи, клиентами и позициями (с товарами),
    /// отсортированные по дате заказа по убыванию.
    /// </summary>
    public Task<List<Order>> GetOrdersAsync() =>
        db.Orders
            .Include(o => o.PickupPoint)
            .Include(o => o.Client)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

    /// <summary>
    /// Загружает заказ по Id с пунктом выдачи, клиентом и позициями.
    /// </summary>
    /// <param name="id">Идентификатор заказа.</param>
    public Task<Order?> GetByIdAsync(int id) =>
        db.Orders
            .Include(o => o.PickupPoint)
            .Include(o => o.Client)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

    /// <summary>Возвращает список пунктов выдачи для выпадающих списков.</summary>
    public Task<List<PickupPoint>> GetPickupPointsAsync() =>
        db.PickupPoints.OrderBy(p => p.Address).ToListAsync();

    /// <summary>Возвращает клиентов (роль Client) для привязки к заказу.</summary>
    public Task<List<AppUser>> GetClientsAsync() =>
        db.Users.Where(u => u.Role == UserRole.Client).OrderBy(u => u.FullName).ToListAsync();

    /// <summary>
    /// Создаёт или обновляет заказ с проверкой уникальности артикула.
    /// Позиции заказа через этот метод не редактируются (только шапка заказа).
    /// </summary>
    /// <param name="order">Данные заказа.</param>
    /// <returns>Кортеж (успех, текст ошибки).</returns>
    public async Task<(bool Success, string? Error)> SaveAsync(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.Article))
            return (false, "Артикул заказа обязателен.");

        var duplicate = await db.Orders.AnyAsync(o => o.Article == order.Article && o.Id != order.Id);
        if (duplicate)
            return (false, "Заказ с таким артикулом уже существует.");

        if (order.Id == 0)
        {
            db.Orders.Add(order);
        }
        else
        {
            var existing = await db.Orders.FindAsync(order.Id);
            if (existing is null)
                return (false, "Заказ не найден.");

            existing.Article = order.Article;
            existing.Status = order.Status;
            existing.OrderDate = order.OrderDate;
            existing.DeliveryDate = order.DeliveryDate;
            existing.PickupCode = order.PickupCode;
            existing.PickupPointId = order.PickupPointId;
            existing.ClientId = order.ClientId;
        }

        await db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>
    /// Удаляет заказ по идентификатору (позиции удаляются каскадно на уровне БД).
    /// </summary>
    /// <param name="id">Идентификатор заказа.</param>
    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null)
            return (false, "Заказ не найден.");

        db.Orders.Remove(order);
        await db.SaveChangesAsync();
        return (true, null);
    }
}
