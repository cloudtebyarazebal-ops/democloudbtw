using System.Security.Claims;
using KodShopWeb.Data;
using KodShopWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace KodShopWeb.Services;

/// <summary>
/// Сервис аутентификации пользователей.
/// Проверяет логин/пароль и формирует ClaimsPrincipal для cookie-аутентификации.
/// </summary>
/// <param name="db">Контекст базы данных.</param>
public class AuthService(AppDbContext db)
{
    /// <summary>
    /// Проверяет учётные данные и возвращает пользователя при успехе.
    /// </summary>
    /// <param name="login">Логин.</param>
    /// <param name="password">Пароль.</param>
    /// <returns>Найденный пользователь или null при неверных данных.</returns>
    public async Task<AppUser?> ValidateAsync(string login, string password) =>
        await db.Users.FirstOrDefaultAsync(u => u.Login == login && u.Password == password);

    /// <summary>
    /// Создаёт объект ClaimsPrincipal для установки cookie-сессии.
    /// В claims записываются Id, ФИО и роль пользователя.
    /// </summary>
    /// <param name="user">Аутентифицированный пользователь.</param>
    /// <returns>Principal с identity схемы «ShopCookie».</returns>
    public static ClaimsPrincipal CreatePrincipal(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "ShopCookie"));
    }
}
