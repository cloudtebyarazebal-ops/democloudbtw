using KodShopWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace KodShopWeb.Data;

/// <summary>
/// Заполняет базу данных демонстрационными данными (продажа компьютерных комплектующих: процессоры, видеокарты, материнские платы, оперативная память, блоки питания, накопители SSD/HDD + Manufacturer).
/// Сгенерировано автоматически из текста ТЗ.
/// </summary>
public static class DbSeeder
{
    private const string SeedSignature = "8118CECA974D198E";

    public static async Task SeedAsync(AppDbContext db, IWebHostEnvironment env, ShopSettings settings)
    {
        var forcePath = Path.Combine(env.ContentRootPath, "Data", ".force-db-reseed");
        var signaturePath = Path.Combine(env.ContentRootPath, "Data", "seed-signature.txt");
        var forceReseed = File.Exists(forcePath);
        var storedSignature = File.Exists(signaturePath) ? await File.ReadAllTextAsync(signaturePath) : "";
        var needsReseed = forceReseed || storedSignature.Trim() != SeedSignature;

        if (needsReseed)
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
            if (await db.Products.AnyAsync())
                return;
        }

        await EnsureUsersAsync(db);

        var manufacturers = new[]
        {
            new Manufacturer { Name = "Intel" },
            new Manufacturer { Name = "AMD" },
            new Manufacturer { Name = "NVIDIA" },
            new Manufacturer { Name = "ASUS" },
            new Manufacturer { Name = "MSI" },
        };
        await db.Manufacturers.AddRangeAsync(manufacturers);

        var suppliers = new[]
        {
            new Supplier { Name = "ООО «КомпСклад»" },
            new Supplier { Name = "ИП ТехноПоставка" },
        };
        await db.Suppliers.AddRangeAsync(suppliers);

        var categories = new[]
        {
            new Category { Name = "Процессоры" },
            new Category { Name = "Видеокарты" },
            new Category { Name = "Материнские платы" },
            new Category { Name = "Память" },
            new Category { Name = "Накопители" },
            new Category { Name = "Блоки питания" },
        };
        await db.Categories.AddRangeAsync(categories);

        var pickupPoints = new[]
        {
            new PickupPoint { Address = "г. Москва, ул. Тверская, д. 1" },
            new PickupPoint { Address = "г. Санкт-Петербург, Невский пр., д. 10" }
        };
        await db.PickupPoints.AddRangeAsync(pickupPoints);

        await db.SaveChangesAsync();

        var products = new[]
        {
            new Product
            {
                Article = "CPU-I7-14700",
                Name = "Intel Core i7-14700",
                CategoryId = categories[0].Id,
                Description = "Процессор Intel 20 ядер.",
                ManufacturerId = manufacturers[0].Id,
                SupplierId = suppliers[0].Id,
                Price = 38990m,
                Unit = "шт.",
                QuantityOnStock = 15,
                Discount = 5m,
                ImagePath = "images/products/item-1.png"
            },
            new Product
            {
                Article = "GPU-RTX4070",
                Name = "NVIDIA GeForce RTX 4070",
                CategoryId = categories[1].Id,
                Description = "Видеокарта 12 ГБ.",
                ManufacturerId = manufacturers[1].Id,
                SupplierId = suppliers[0].Id,
                Price = 64990m,
                Unit = "шт.",
                QuantityOnStock = 8,
                Discount = 10m,
                ImagePath = "images/products/item-2.png"
            },
            new Product
            {
                Article = "RAM-DDR5-32",
                Name = "DDR5 32GB Kit",
                CategoryId = categories[2].Id,
                Description = "Оперативная память 32 ГБ.",
                ManufacturerId = manufacturers[2].Id,
                SupplierId = suppliers[0].Id,
                Price = 12990m,
                Unit = "шт.",
                QuantityOnStock = 25,
                Discount = 0m,
                ImagePath = "images/products/item-3.png"
            },
        };
        await db.Products.AddRangeAsync(products);
        await db.SaveChangesAsync();

        if (!await db.Orders.AnyAsync())
        {
            var client = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Client);
            var order = new Order
            {
                Article = "ORD-1001",
                Status = OrderStatus.New,
                OrderDate = DateTime.UtcNow.Date,
                DeliveryDate = null,
                PickupCode = "482913",
                PickupPointId = pickupPoints[0].Id,
                ClientId = client?.Id
            };
            await db.Orders.AddAsync(order);
            await db.SaveChangesAsync();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(signaturePath)!);
        await File.WriteAllTextAsync(signaturePath, SeedSignature);
        if (File.Exists(forcePath))
            File.Delete(forcePath);
    }

    private static async Task EnsureUsersAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync())
            return;

        var users = new[]
        {
            new AppUser { FullName = "Администратор Системы", Login = "admin", Password = "admin123", Role = UserRole.Administrator, Email = "admin@kodshop.local" },
            new AppUser { FullName = "Менеджер Склада", Login = "manager", Password = "manager123", Role = UserRole.Manager, Email = "manager@kodshop.local" },
            new AppUser { FullName = "Иванов Иван Иванович", Login = "client", Password = "client123", Role = UserRole.Client, Email = "client@kodshop.local" }
        };

        await db.Users.AddRangeAsync(users);
        await db.SaveChangesAsync();
    }
}
