using KodShopWeb.Models;
using Microsoft.EntityFrameworkCore;

using Order = KodShopWeb.Models.Order;
namespace KodShopWeb.Data;

/// <summary>
/// Контекст базы данных Entity Framework Core для приложения KodShopWeb.
/// Определяет набор сущностей и правила модели (индексы, связи, точность decimal).
/// </summary>
/// <param name="options">Параметры контекста (провайдер SQLite и строка подключения).</param>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // --- Наборы сущностей (таблицы) ---

/// <summary>Таблица пользователей.</summary>
public DbSet<AppUser> Users => Set<AppUser>();

/// <summary>Таблица категорий товаров.</summary>
public DbSet<Category> Categories => Set<Category>();

/// <summary>Таблица производителей.</summary>
public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();

/// <summary>Таблица поставщиков.</summary>
public DbSet<Supplier> Suppliers => Set<Supplier>();

/// <summary>Таблица товаров.</summary>
public DbSet<Product> Products => Set<Product>();

/// <summary>Таблица пунктов выдачи.</summary>
public DbSet<PickupPoint> PickupPoints => Set<PickupPoint>();

/// <summary>Таблица заказов.</summary>
public DbSet<Order> Orders => Set<Order>();

/// <summary>Таблица позиций заказов.</summary>
public DbSet<OrderItem> OrderItems => Set<OrderItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Логин пользователя должен быть уникальным.
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(u => u.Login).IsUnique();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            // Артикул товара — уникальный; цена и скидка с фиксированной точностью.
            entity.HasIndex(p => p.Article).IsUnique();
            entity.Property(p => p.Price).HasPrecision(10, 2);
            entity.Property(p => p.Discount).HasPrecision(5, 2);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(o => o.Article).IsUnique();
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            // При удалении заказа позиции удаляются каскадно.
            entity.HasOne(i => i.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Товар с позициями в заказах удалить нельзя (Restrict).
            entity.HasOne(i => i.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
