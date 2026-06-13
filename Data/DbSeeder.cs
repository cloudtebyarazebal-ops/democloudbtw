using KodShopWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace KodShopWeb.Data;

/// <summary>
/// Начальное заполнение базы данных тестовыми данными.
/// Выполняется один раз при первом запуске, если каталог товаров пуст.
/// </summary>
public static class DbSeeder
{
    /// <summary>
    /// Создаёт схему БД и заполняет справочники, пользователей, товары и пример заказа.
    /// Пропускает заполнение, если в таблице Products уже есть записи.
    /// </summary>
    /// <param name="db">Контекст базы данных.</param>
    /// <param name="env">Окружение хоста (пути к wwwroot и ImportData).</param>
    public static async Task SeedAsync(AppDbContext db, IWebHostEnvironment env)
    {
        await db.Database.EnsureCreatedAsync();

        // Повторное заполнение не выполняем — достаточно проверки наличия товаров.
        if (await db.Products.AnyAsync())
            return;

        // --- Справочники ---
        var categories = new[]
        {
            new Category { Name = "Роман" },
            new Category { Name = "Хрестоматия" },
            new Category { Name = "Учебник" }
        };
        db.Categories.AddRange(categories);

        var manufacturers = new[]
        {
            new Manufacturer { Name = "АСТ" },
            new Manufacturer { Name = "Эксмо" },
            new Manufacturer { Name = "Просвещение" }
        };
        db.Manufacturers.AddRange(manufacturers);

        var suppliers = new[]
        {
            new Supplier { Name = "ООО «Книжный склад»" },
            new Supplier { Name = "ИП Петров" }
        };
        db.Suppliers.AddRange(suppliers);

        var pickupPoints = new[]
        {
            new PickupPoint { Address = "344288, г. Лесной, ул. Чехова, 1" },
            new PickupPoint { Address = "614164, г. Лесной, ул. Степная, 30" },
            new PickupPoint { Address = "394242, г. Лесной, ул. Коммунистическая, 43" }
        };
        db.PickupPoints.AddRange(pickupPoints);

        // --- Тестовые пользователи всех ролей ---
        var users = new[]
        {
            new AppUser { FullName = "Админов Админ Админович", Login = "admin", Password = "admin", Role = UserRole.Administrator, Email = "admin@shop.local" },
            new AppUser { FullName = "Менеджеров Менеджер Менеджерович", Login = "manager", Password = "manager", Role = UserRole.Manager, Email = "manager@shop.local" },
            new AppUser { FullName = "Клиентов Клиент Клиентович", Login = "client", Password = "client", Role = UserRole.Client, Email = "client@shop.local" }
        };
        db.Users.AddRange(users);

        await db.SaveChangesAsync();

        // --- Демонстрационный каталог товаров ---
        var products = new List<Product>
        {
            new()
            {
                Article = "A112T4",
                Name = "Прокляты и убиты",
                Description = "Роман-эпопея Виктора Астафьева.",
                Price = 890m,
                Unit = "шт.",
                QuantityOnStock = 12,
                Discount = 28m,
                CategoryId = categories[0].Id,
                ManufacturerId = manufacturers[0].Id,
                SupplierId = suppliers[0].Id,
                ImagePath = "/images/products/1.jpg"
            },
            new()
            {
                Article = "G843H5",
                Name = "Хрестоматия по литературе",
                Description = "Сборник для старших классов.",
                Price = 450m,
                Unit = "шт.",
                QuantityOnStock = 0,
                Discount = 0m,
                CategoryId = categories[1].Id,
                ManufacturerId = manufacturers[2].Id,
                SupplierId = suppliers[0].Id,
                ImagePath = null
            },
            new()
            {
                Article = "D325D4",
                Name = "Основы программирования",
                Description = "Учебник для СПО.",
                Price = 1200m,
                Unit = "шт.",
                QuantityOnStock = 5,
                Discount = 15m,
                CategoryId = categories[2].Id,
                ManufacturerId = manufacturers[2].Id,
                SupplierId = suppliers[1].Id,
                ImagePath = "/images/products/3.jpg"
            },
            new()
            {
                Article = "S432T5",
                Name = "Тихий Дон",
                Description = "Классика русской литературы.",
                Price = 760m,
                Unit = "шт.",
                QuantityOnStock = 8,
                Discount = 10m,
                CategoryId = categories[0].Id,
                ManufacturerId = manufacturers[1].Id,
                SupplierId = suppliers[0].Id,
                ImagePath = "/images/products/4.jpg"
            }
        };
        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        // --- Пример заказа клиента с двумя позициями ---
        var client = await db.Users.FirstAsync(u => u.Role == UserRole.Client);
        var order = new Order
        {
            Article = "ORD-001",
            Status = OrderStatus.New,
            OrderDate = DateTime.Today.AddDays(-2),
            DeliveryDate = null,
            PickupCode = "482913",
            PickupPointId = pickupPoints[0].Id,
            ClientId = client.Id,
            Items =
            [
                new OrderItem { ProductId = products[0].Id, Quantity = 2 },
                new OrderItem { ProductId = products[2].Id, Quantity = 1 }
            ]
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        CopySeedImages(env);
    }

    /// <summary>
    /// Копирует изображения товаров из ImportData/images в wwwroot/images/products,
    /// если целевой файл ещё не существует.
    /// </summary>
    /// <param name="env">Окружение хоста для определения путей.</param>
    private static void CopySeedImages(IWebHostEnvironment env)
    {
        var targetDir = Path.Combine(env.WebRootPath, "images", "products");
        Directory.CreateDirectory(targetDir);

        var importDir = Path.Combine(env.ContentRootPath, "ImportData", "images");
        if (!Directory.Exists(importDir))
            return;

        foreach (var file in Directory.GetFiles(importDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            // Не перезаписываем уже существующие файлы.
            if (!File.Exists(dest))
                File.Copy(file, dest);
        }
    }
}
