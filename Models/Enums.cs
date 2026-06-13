namespace KodShopWeb.Models;

/// <summary>
/// Роли пользователей системы.
/// Определяют уровень доступа к каталогу, заказам и администрированию.
/// </summary>
public enum UserRole
{
    /// <summary>Неавторизованный посетитель (гость).</summary>
    Guest = 0,

    /// <summary>Авторизованный клиент — просмотр каталога.</summary>
    Client = 1,

    /// <summary>Менеджер — расширенные инструменты каталога и просмотр заказов.</summary>
    Manager = 2,

    /// <summary>Администратор — полный CRUD товаров, заказов и импорт.</summary>
    Administrator = 3
}

/// <summary>
/// Статус заказа в жизненном цикле обработки.
/// </summary>
public enum OrderStatus
{
    /// <summary>Новый заказ, ожидает обработки.</summary>
    New = 0,

    /// <summary>Заказ завершён (выдан / закрыт).</summary>
    Completed = 1
}

/// <summary>
/// Поле сортировки товаров в расширенном режиме каталога.
/// </summary>
public enum ProductSortField
{
    /// <summary>Сортировка по цене.</summary>
    Price,

    /// <summary>Сортировка по количеству на складе.</summary>
    Quantity,

    /// <summary>Сортировка по проценту скидки.</summary>
    Discount
}

/// <summary>
/// Направление сортировки (по возрастанию / убыванию).
/// </summary>
public enum SortDirection
{
    /// <summary>По возрастанию.</summary>
    Asc,

    /// <summary>По убыванию.</summary>
    Desc
}
