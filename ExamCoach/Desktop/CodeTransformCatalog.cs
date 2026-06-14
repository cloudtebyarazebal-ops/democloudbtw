namespace ExamCoachDesktop;

public static class CodeTransformCatalog
{
    public static IReadOnlyList<CodeTransformRule> GetBuiltInRules() =>
    [
        Rule("no-orders-controller", "*OrdersController*", TransformActionKind.RemoveType, "OrdersController",
            featureMissing: AssignmentFeatures.Orders, reason: "Заказы не требуются — удалён OrdersController"),
        Rule("no-orders-viewmodels", "*ViewModels*", TransformActionKind.RemoveType,
            "OrderFormViewModel|OrderRowViewModel|OrdersIndexViewModel", featureMissing: AssignmentFeatures.Orders,
            reason: "Заказы не требуются — удалены ViewModel заказов"),
        Rule("no-orders-service", "*OrderService*", TransformActionKind.RemoveType, "OrderService",
            featureMissing: AssignmentFeatures.Orders, reason: "Заказы не требуются — удалён OrderService"),
        Rule("no-orders-entities", "*Entities*", TransformActionKind.RemoveType, "OrderItem|Order",
            featureMissing: AssignmentFeatures.Orders, reason: "Заказы не требуются — удалены Order/OrderItem"),
        Rule("no-orders-dbset", "*AppDbContext*", TransformActionKind.RemoveLineContaining, "DbSet<Order",
            featureMissing: AssignmentFeatures.Orders, reason: "Заказы не требуются — убраны DbSet заказов"),
        Rule("no-orders-program", "*Program.cs*", TransformActionKind.RemoveLineContaining,
            "AddScoped<OrderService>", featureMissing: AssignmentFeatures.Orders,
            reason: "Заказы не требуются — убран DI OrderService"),
        Rule("no-orders-layout", "*_Layout*", TransformActionKind.RemoveRazorBlock, "Orders",
            featureMissing: AssignmentFeatures.Orders, reason: "Заказы не требуются — убран пункт «Заказы»"),
        Rule("no-orders-seeder", "*DbSeeder*", TransformActionKind.RemoveMarkedSection,
            "// --- Пример заказа", "await db.SaveChangesAsync", featureMissing: AssignmentFeatures.Orders,
            reason: "Заказы не требуются — убран seed заказа"),

        Rule("no-import-controller", "*OrdersController*", TransformActionKind.RemoveType, "ImportController",
            featureMissing: AssignmentFeatures.Import, reason: "Импорт не требуется — удалён ImportController"),
        Rule("no-import-service", "*ImportService*", TransformActionKind.RemoveType, "ImportService",
            featureMissing: AssignmentFeatures.Import, reason: "Импорт не требуется — удалён ImportService"),
        Rule("no-import-viewmodel", "*ViewModels*", TransformActionKind.RemoveType, "ImportViewModel",
            featureMissing: AssignmentFeatures.Import, reason: "Импорт не требуется — удалён ImportViewModel"),
        Rule("no-import-program", "*Program.cs*", TransformActionKind.RemoveLineContaining,
            "AddScoped<ImportService>", featureMissing: AssignmentFeatures.Import,
            reason: "Импорт не требуется — убран DI ImportService"),
        Rule("no-import-layout", "*_Layout*", TransformActionKind.RemoveRazorBlock, "Import",
            featureMissing: AssignmentFeatures.Import, reason: "Импорт не требуется — убран пункт «Импорт»"),

        Rule("no-crud-index-button", "*Products/Index*", TransformActionKind.RemoveLineContaining, "Добавить товар",
            featureMissing: AssignmentFeatures.ProductCrud, reason: "CRUD не требуется — убрана кнопка «Добавить»"),
        Rule("no-crud-edit-step", "*Products/Edit*", TransformActionKind.RemoveLineContaining, "asp-for",
            featureMissing: AssignmentFeatures.ProductCrud, reason: "CRUD не требуется — упрощён Edit"),
        Rule("no-search-toolbar", "*Products/Index*", TransformActionKind.RemoveLineContaining, "searchInput",
            featureMissing: AssignmentFeatures.ProductSearch, reason: "Поиск не требуется"),
        Rule("no-sort-toolbar", "*Products/Index*", TransformActionKind.RemoveLineContaining, "sortField",
            featureMissing: AssignmentFeatures.ProductSort, reason: "Сортировка не требуется"),
        Rule("no-advanced-index-toolbar", "*Products/Index*", TransformActionKind.RemoveRazorBlock, "CanUseAdvancedTools",
            featureMissing: AssignmentFeatures.ProductFilters, reason: "Фильтры не требуются — убрана панель инструментов"),
        Rule("no-products-js", "*products.js*", TransformActionKind.PrependHint,
            "// Файл не требуется по ТЗ — пропустите или оставьте заглушку",
            featureMissing: AssignmentFeatures.ProductFilters, reason: "JS фильтров не требуется"),
    ];

    private static CodeTransformRule Rule(
        string id,
        string stepMatch,
        TransformActionKind action,
        string parameter,
        string? parameter2 = null,
        string? featureMissing = null,
        string? featureRequired = null,
        string? variant = null,
        string reason = "") =>
        new()
        {
            Id = id,
            StepMatch = stepMatch,
            Action = action,
            Parameter = parameter,
            Parameter2 = parameter2,
            Reason = string.IsNullOrEmpty(reason) ? id : reason,
            Condition = new TransformCondition
            {
                FeatureMissing = featureMissing,
                FeatureRequired = featureRequired,
                Variant = variant
            }
        };
}
