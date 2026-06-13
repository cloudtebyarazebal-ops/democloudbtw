using System.Security.Claims;
using KodShopWeb.Models;

namespace KodShopWeb.Services;

/// <summary>
/// Вспомогательные методы проверки прав доступа по ClaimsPrincipal.
/// Централизует логику ролей для контроллеров и представлений.
/// </summary>
public static class UserAccess
{
    /// <summary>
    /// Извлекает роль текущего пользователя из claims.
    /// Неавторизованным возвращает Guest.
    /// </summary>
    /// <param name="user">Текущий пользователь HTTP-контекста.</param>
    /// <returns>Роль пользователя или Guest.</returns>
    public static UserRole GetRole(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return UserRole.Guest;

        var roleValue = user.FindFirst(ClaimTypes.Role)?.Value;
        return Enum.TryParse<UserRole>(roleValue, out var role) ? role : UserRole.Guest;
    }

    /// <summary>
    /// Возвращает отображаемое ФИО или «Гость» для неавторизованных.
    /// </summary>
    /// <param name="user">Текущий пользователь.</param>
    /// <returns>ФИО из claim Name или запасные подписи.</returns>
    public static string GetFullName(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true
            ? user.FindFirst(ClaimTypes.Name)?.Value ?? "Пользователь"
            : "Гость";

    /// <summary>
    /// Доступны ли расширенные инструменты каталога (фильтр, сортировка).
    /// Разрешено менеджерам и администраторам.
    /// </summary>
    /// <param name="user">Текущий пользователь.</param>
    public static bool CanUseAdvancedProductTools(ClaimsPrincipal? user)
    {
        var role = GetRole(user);
        return role is UserRole.Manager or UserRole.Administrator;
    }

    /// <summary>
    /// Может ли пользователь создавать, редактировать и удалять товары.
    /// Только администратор.
    /// </summary>
    /// <param name="user">Текущий пользователь.</param>
    public static bool CanEditProducts(ClaimsPrincipal? user) =>
        GetRole(user) == UserRole.Administrator;

    /// <summary>
    /// Может ли пользователь просматривать раздел заказов.
    /// Разрешено менеджерам и администраторам.
    /// </summary>
    /// <param name="user">Текущий пользователь.</param>
    public static bool CanViewOrders(ClaimsPrincipal? user)
    {
        var role = GetRole(user);
        return role is UserRole.Manager or UserRole.Administrator;
    }

    /// <summary>
    /// Может ли пользователь создавать, редактировать и удалять заказы.
    /// Только администратор.
    /// </summary>
    /// <param name="user">Текущий пользователь.</param>
    public static bool CanEditOrders(ClaimsPrincipal? user) =>
        GetRole(user) == UserRole.Administrator;
}
