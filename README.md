# KodShopWeb — тренировочное приложение для КОД 09.02.07

**ASP.NET Core MVC** (.NET 8) + **HTML / CSS / JavaScript** (без Blazor).

## Запуск

```bash
cd KodShopWeb
dotnet run
```

Откройте адрес из консоли → `/Account/Login`

## Логины

| Логин | Пароль | Роль |
|-------|--------|------|
| admin | admin | Администратор |
| manager | manager | Менеджер |
| client | client | Клиент |

Кнопка **«Просмотр как гость»** — без авторизации.

## Структура

```
Controllers/     Account, Products, Orders, Import
Views/           Razor-шаблоны (HTML)
wwwroot/css/     shop.css
wwwroot/js/      products.js — поиск/фильтр/сортировка
Data/            EF Core + SQLite
Services/        бизнес-логика
```

## Вариант ПУ / БУ

`appsettings.json` → `"Variant": "PU"` или `"BU"`

## Импорт

Войти как admin → **Импорт** → указать путь к распакованной папке с `Tovar.xlsx`.
