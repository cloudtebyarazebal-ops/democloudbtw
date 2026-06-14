using KodShopWeb.Data;
using KodShopWeb.Models;
using KodShopWeb.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

// Точка входа веб-приложения «КодШоп» (KodShopWeb).
// Конфигурирует DI, аутентификацию, БД и конвейер HTTP-запросов.

var builder = WebApplication.CreateBuilder(args);

// Загрузка настроек магазина из appsettings (секция «Shop») и применение пресета PU/BU.
var shopSettings = builder.Configuration.GetSection(ShopSettings.SectionName).Get<ShopSettings>() ?? new ShopSettings();
ShopVariantPresets.Apply(shopSettings);

// Настройки магазина — singleton (один экземпляр на всё приложение).
builder.Services.AddSingleton(shopSettings);

// Контекст EF Core с SQLite; строка подключения — из конфигурации.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Регистрация доменных сервисов (scoped — на один HTTP-запрос).
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ImportService>();

// Аутентификация по cookie; пути входа/выхода и отказа в доступе.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
    });
builder.Services.AddAuthorization();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// При старте: создание БД (если нет) и начальное заполнение тестовыми данными.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
    var settings = scope.ServiceProvider.GetRequiredService<ShopSettings>();
    await DbSeeder.SeedAsync(db, env, settings);
}

// В production — обработчик ошибок и HSTS; в Development middleware не подключается.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Маршрут по умолчанию: стартовая страница — вход в систему.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
