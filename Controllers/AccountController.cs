using KodShopWeb.Data;
using KodShopWeb.Models;
using KodShopWeb.Services;
using KodShopWeb.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KodShopWeb.Controllers;

/// <summary>
/// Контроллер учётной записи: вход, выход и режим гостя.
/// </summary>
/// <param name="authService">Сервис проверки логина и пароля.</param>
public class AccountController(AuthService authService) : Controller
{
    /// <summary>
    /// Отображает форму входа. Авторизованных перенаправляет в каталог.
    /// </summary>
    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Products");

        return View(new LoginViewModel());
    }

    /// <summary>
    /// Обрабатывает отправку формы входа и устанавливает cookie-аутентификацию.
    /// </summary>
    /// <param name="model">Логин и пароль из формы.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await authService.ValidateAsync(model.Login, model.Password);
        if (user is null)
        {
            model.ErrorMessage = "Неверный логин или пароль.";
            return View(model);
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AuthService.CreatePrincipal(user));

        return RedirectToAction("Index", "Products");
    }

    /// <summary>
    /// Выход из учётной записи и переход в каталог как гость (без повторного входа).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guest()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Products");
    }

    /// <summary>
    /// Выход из системы с перенаправлением на страницу входа.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
