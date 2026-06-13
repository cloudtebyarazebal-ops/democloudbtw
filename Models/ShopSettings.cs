namespace KodShopWeb.Models;

/// <summary>
/// Настройки внешнего вида и поведения магазина.
/// Загружаются из секции конфигурации «Shop» и дополняются пресетами PU/BU.
/// </summary>
public class ShopSettings
{
    /// <summary>Имя секции в appsettings.json.</summary>
    public const string SectionName = "Shop";

    /// <summary>
    /// Вариант демонстрации: PU — профильный (книги), BU — базовый (велосипеды).
    /// </summary>
    public string Variant { get; set; } = "PU";

    /// <summary>Отображаемое название магазина.</summary>
    public string ShopName { get; set; } = "ЧитайГород";

    /// <summary>CSS-семейство шрифтов для интерфейса.</summary>
    public string FontFamily { get; set; } = "Comic Sans MS, cursive";

    /// <summary>Основной цвет фона (hex).</summary>
    public string PrimaryColor { get; set; } = "#FFFFFF";

    /// <summary>Вторичный цвет интерфейса (hex).</summary>
    public string SecondaryColor { get; set; } = "#ABCFCE";

    /// <summary>Акцентный цвет (кнопки, заголовки).</summary>
    public string AccentColor { get; set; } = "#546F94";

    /// <summary>
    /// Порог скидки (%), выше которого строка товара подсвечивается в каталоге.
    /// </summary>
    public decimal DiscountHighlightPercent { get; set; } = 25m;

    /// <summary>Цвет подсветки строк с большой скидкой (hex).</summary>
    public string DiscountHighlightColor { get; set; } = "#23E1EF";

    /// <summary>
    /// Доступен ли раздел заказов (в варианте BU отключён).
    /// </summary>
    public bool OrdersEnabled { get; set; } = true;

    /// <summary>
    /// Признак профильного варианта PU (книжный магазин).
    /// </summary>
    public bool IsProfileVariant => string.Equals(Variant, "PU", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Диапазон фильтрации товаров по скидке для выпадающего списка.
/// </summary>
/// <param name="Label">Подпись в UI.</param>
/// <param name="Min">Минимальная скидка включительно (null — без нижней границы).</param>
/// <param name="Max">Максимальная скидка включительно (null — без верхней границы).</param>
public readonly record struct DiscountFilterRange(string Label, decimal? Min, decimal? Max);

/// <summary>
/// Пресеты оформления и фильтров для вариантов PU и BU.
/// Вызывается при старте приложения для переопределения настроек по Variant.
/// </summary>
public static class ShopVariantPresets
{
    /// <summary>
    /// Применяет пресет PU или BU к переданным настройкам магазина.
    /// Перезаписывает название, шрифты, цвета и флаг OrdersEnabled.
    /// </summary>
    /// <param name="settings">Объект настроек для модификации.</param>
    public static void Apply(ShopSettings settings)
    {
        if (settings.IsProfileVariant)
        {
            // Профильный уровень (PU): книжный магазин «ЧитайГород», заказы включены.
            settings.ShopName = "ЧитайГород";
            settings.FontFamily = "Comic Sans MS, cursive";
            settings.SecondaryColor = "#ABCFCE";
            settings.AccentColor = "#546F94";
            settings.DiscountHighlightPercent = 25m;
            settings.DiscountHighlightColor = "#23E1EF";
            settings.OrdersEnabled = true;
            return;
        }

        // Базовый уровень (BU): велосипеды, другая палитра, заказы отключены.
        settings.ShopName = "ВелосипедДрайв";
        settings.FontFamily = "Arial, sans-serif";
        settings.SecondaryColor = "#6A5ACD";
        settings.AccentColor = "#4B0082";
        settings.DiscountHighlightPercent = 15m;
        settings.DiscountHighlightColor = "#483D8B";
        settings.OrdersEnabled = false;
    }

    /// <summary>
    /// Возвращает список диапазонов скидок для фильтра каталога
    /// в зависимости от варианта PU/BU.
    /// </summary>
    /// <param name="settings">Текущие настройки магазина.</param>
    /// <returns>Неизменяемый список диапазонов с подписями.</returns>
    public static IReadOnlyList<DiscountFilterRange> GetFilterRanges(ShopSettings settings)
    {
        if (settings.IsProfileVariant)
        {
            return
            [
                new("Все диапазоны", null, null),
                new("0–12,99%", 0m, 12.99m),
                new("13–16,99%", 13m, 16.99m),
                new("17% и более", 17m, null)
            ];
        }

        return
        [
            new("Все диапазоны", null, null),
            new("0–11,99%", 0m, 11.99m),
            new("12–18,99%", 12m, 18.99m),
            new("19% и более", 19m, null)
        ];
    }
}
