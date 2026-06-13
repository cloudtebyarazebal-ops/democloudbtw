using ClosedXML.Excel;
using KodShopWeb.Data;
using KodShopWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace KodShopWeb.Services;

/// <summary>
/// Сервис импорта данных из Excel-файлов в папке (Tovar.xlsx, user_import.xlsx).
/// Вся операция выполняется в одной транзакции БД.
/// </summary>
/// <param name="db">Контекст базы данных.</param>
/// <param name="env">Окружение хоста для копирования изображений.</param>
public class ImportService(AppDbContext db, IWebHostEnvironment env)
{
    /// <summary>
    /// Импортирует пользователей и товары из указанной папки.
    /// Ищет Tovar.xlsx (обязательно) и user_import.xlsx (опционально) рекурсивно.
    /// </summary>
    /// <param name="folderPath">Путь к корневой папке импорта.</param>
    /// <returns>Кортеж (успех, сообщение для пользователя).</returns>
    public async Task<(bool Success, string Message)> ImportFromFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return (false, $"Папка не найдена: {folderPath}");

        var tovarPath = Directory.GetFiles(folderPath, "Tovar.xlsx", SearchOption.AllDirectories).FirstOrDefault();
        var usersPath = Directory.GetFiles(folderPath, "user_import.xlsx", SearchOption.AllDirectories).FirstOrDefault();

        if (tovarPath is null)
            return (false, "Файл Tovar.xlsx не найден.");

        // Откат при любой ошибке — целостность данных.
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            if (usersPath is not null)
                await ImportUsersAsync(usersPath);

            await ImportProductsAsync(tovarPath, folderPath);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return (true, "Импорт выполнен успешно.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return (false, $"Ошибка импорта: {ex.Message}");
        }
    }

    /// <summary>
    /// Импортирует пользователей из user_import.xlsx.
    /// Столбцы: роль, ФИО, логин, пароль. Существующие логины обновляются.
    /// </summary>
    /// <param name="path">Путь к файлу Excel.</param>
    private async Task ImportUsersAsync(string path)
    {
        using var workbook = new XLWorkbook(path);
        var rows = workbook.Worksheet(1).RowsUsed().Skip(1);

        foreach (var row in rows)
        {
            var roleName = row.Cell(1).GetString().Trim();
            var fullName = row.Cell(2).GetString().Trim();
            var login = row.Cell(3).GetString().Trim();
            var password = row.Cell(4).GetString().Trim();

            if (string.IsNullOrWhiteSpace(login))
                continue;

            // Сопоставление русских названий ролей из файла импорта.
            var role = roleName switch
            {
                "Администратор" => UserRole.Administrator,
                "Менеджер" => UserRole.Manager,
                "Авторизированный клиент" => UserRole.Client,
                _ => UserRole.Client
            };

            var user = await db.Users.FirstOrDefaultAsync(u => u.Login == login);
            if (user is null)
            {
                db.Users.Add(new AppUser
                {
                    FullName = fullName,
                    Login = login,
                    Password = password,
                    Role = role
                });
            }
            else
            {
                user.FullName = fullName;
                user.Password = password;
                user.Role = role;
            }
        }
    }

    /// <summary>
    /// Импортирует товары из Tovar.xlsx с автосозданием справочников и копированием изображений.
    /// Заголовки столбцов ищутся по имени (несколько синонимов на поле).
    /// </summary>
    /// <param name="path">Путь к Tovar.xlsx.</param>
    /// <param name="folderPath">Корень папки для поиска файлов изображений.</param>
    private async Task ImportProductsAsync(string path, string folderPath)
    {
        using var workbook = new XLWorkbook(path);
        var rows = workbook.Worksheet(1).RowsUsed().Skip(1).ToList();
        if (rows.Count == 0)
            return;

        var headers = workbook.Worksheet(1).Row(1).CellsUsed().Select(c => c.GetString().Trim()).ToList();

        foreach (var row in rows)
        {
            var article = GetCell(row, headers, "Артикул");
            if (string.IsNullOrWhiteSpace(article))
                continue;

            var categoryName = GetCell(row, headers, "Категория товара");
            var manufacturerName = GetCell(row, headers, "Производитель");
            var supplierName = GetCell(row, headers, "Поставщик");
            var name = GetCell(row, headers, "Наименование товара", "Название");
            var description = GetCell(row, headers, "Описание товара", "Описание");
            var unit = GetCell(row, headers, "Единица измерения", defaultValue: "шт.");
            var price = ParseDecimal(GetCell(row, headers, "Цена"));
            var quantity = ParseInt(GetCell(row, headers, "Кол-во на складе", "Количество на складе"));
            var discount = ParseDecimal(GetCell(row, headers, "Действующая скидка", "Скидка"));

            var category = await EnsureCategoryAsync(categoryName);
            var manufacturer = await EnsureManufacturerAsync(manufacturerName);
            var supplier = await EnsureSupplierAsync(supplierName);

            // Имя файла изображения может быть в любой ячейке строки.
            var imageFileName = row.CellsUsed()
                .Select(c => c.GetString().Trim())
                .FirstOrDefault(v => v.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                                  || v.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                                  || v.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

            string? imagePath = null;
            if (!string.IsNullOrWhiteSpace(imageFileName))
            {
                var sourceImage = Directory.GetFiles(folderPath, imageFileName, SearchOption.AllDirectories).FirstOrDefault();
                if (sourceImage is not null)
                    imagePath = await CopyProductImageAsync(sourceImage, article);
            }

            var product = await db.Products.FirstOrDefaultAsync(p => p.Article == article);
            if (product is null)
            {
                db.Products.Add(new Product
                {
                    Article = article,
                    Name = name,
                    Description = description,
                    Unit = unit,
                    Price = price,
                    QuantityOnStock = quantity,
                    Discount = discount,
                    CategoryId = category.Id,
                    ManufacturerId = manufacturer.Id,
                    SupplierId = supplier.Id,
                    ImagePath = imagePath
                });
            }
            else
            {
                // Обновление существующего товара по артикулу.
                product.Name = name;
                product.Description = description;
                product.Unit = unit;
                product.Price = price;
                product.QuantityOnStock = quantity;
                product.Discount = discount;
                product.CategoryId = category.Id;
                product.ManufacturerId = manufacturer.Id;
                product.SupplierId = supplier.Id;
                if (imagePath is not null)
                    product.ImagePath = imagePath;
            }
        }
    }

    /// <summary>
    /// Находит или создаёт категорию по имени; пустое имя заменяется на «Без категории».
    /// </summary>
    private async Task<Category> EnsureCategoryAsync(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Без категории" : name;
        var entity = await db.Categories.FirstOrDefaultAsync(c => c.Name == name);
        if (entity is not null)
            return entity;

        entity = new Category { Name = name };
        db.Categories.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    /// <summary>
    /// Находит или создаёт производителя по имени.
    /// </summary>
    private async Task<Manufacturer> EnsureManufacturerAsync(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Не указан" : name;
        var entity = await db.Manufacturers.FirstOrDefaultAsync(m => m.Name == name);
        if (entity is not null)
            return entity;

        entity = new Manufacturer { Name = name };
        db.Manufacturers.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    /// <summary>
    /// Находит или создаёт поставщика по имени.
    /// </summary>
    private async Task<Supplier> EnsureSupplierAsync(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Не указан" : name;
        var entity = await db.Suppliers.FirstOrDefaultAsync(s => s.Name == name);
        if (entity is not null)
            return entity;

        entity = new Supplier { Name = name };
        db.Suppliers.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    /// <summary>
    /// Копирует файл изображения в wwwroot/images/products с именем по артикулу.
    /// </summary>
    /// <param name="sourcePath">Исходный путь к файлу.</param>
    /// <param name="article">Артикул товара для имени файла.</param>
    /// <returns>Относительный URL изображения.</returns>
    private async Task<string> CopyProductImageAsync(string sourcePath, string article)
    {
        var ext = Path.GetExtension(sourcePath);
        var targetDir = Path.Combine(env.WebRootPath, "images", "products");
        Directory.CreateDirectory(targetDir);
        var fileName = $"{article}{ext}".Replace(" ", "_");
        var targetPath = Path.Combine(targetDir, fileName);
        File.Copy(sourcePath, targetPath, overwrite: true);
        await Task.CompletedTask;
        return $"/images/products/{fileName}";
    }

    /// <summary>
    /// Читает значение ячейки по одному из возможных заголовков столбца.
    /// </summary>
    /// <param name="row">Строка Excel.</param>
    /// <param name="headers">Список заголовков первой строки.</param>
    /// <param name="names">Варианты имени столбца.</param>
    private static string GetCell(IXLRow row, IList<string> headers, params string[] names)
    {
        foreach (var name in names)
        {
            for (var i = 0; i < headers.Count; i++)
            {
                if (headers[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                    return row.Cell(i + 1).GetString().Trim();
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Читает ячейку с подстановкой значения по умолчанию, если ячейка пуста.
    /// </summary>
    private static string GetCell(IXLRow row, IList<string> headers, string name, string defaultValue)
    {
        var value = GetCell(row, headers, name);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    /// <summary>
    /// Парсит decimal из строки; запятая заменяется на точку (InvariantCulture).
    /// </summary>
    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value.Replace(',', '.'), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : 0m;

    /// <summary>Парсит целое число; при ошибке возвращает 0.</summary>
    private static int ParseInt(string value) =>
        int.TryParse(value, out var result) ? result : 0;
}
