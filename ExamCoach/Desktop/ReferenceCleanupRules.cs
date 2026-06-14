namespace ExamCoachDesktop;

/// <summary>Правила очистки эталона по деталям, отсутствующим в тексте ТЗ.</summary>
public static class ReferenceCleanupRules
{
    public static IReadOnlyList<CodeTransformRule> Generate(AssignmentRequirements req)
    {
        var rules = new List<CodeTransformRule>();

        if (!req.Features.Contains(AssignmentFeatures.PickupPoints))
            rules.AddRange(PickupPointCleanup());

        if (!req.Features.Contains(AssignmentFeatures.OrderDelivery))
            rules.AddRange(OrderDeliveryCleanup());

        if (!req.Features.Contains(AssignmentFeatures.OrderStatusDetail))
            rules.AddRange(OrderStatusCleanup());

        if (req.Features.Contains(AssignmentFeatures.Orders) &&
            !req.Features.Contains(AssignmentFeatures.PickupPoints))
        {
            rules.Add(Rule("orders-no-pickup-seed", "*DbSeeder*", TransformActionKind.RemoveMarkedSection,
                "// --- Пример заказа", "await db.SaveChangesAsync",
                reason: "В ТЗ нет ПВЗ — убран пример заказа с PickupPoint"));
        }

        if (!req.Features.Contains(AssignmentFeatures.DiscountUi))
        {
            rules.Add(Rule("no-discount-highlight-vm", "*ViewModels*", TransformActionKind.RemoveProperty,
                "DiscountHighlightPercent|DiscountHighlightColor|IsProfileVariant",
                reason: "Скидки не в ТЗ — убраны поля подсветки"));
            rules.Add(Rule("no-discount-filter-index", "*Products/Index*", TransformActionKind.RemoveRazorBlock,
                "discountFilter", reason: "Скидки не в ТЗ — убран фильтр скидок"));
        }

        return rules;
    }

    private static IEnumerable<CodeTransformRule> PickupPointCleanup() =>
    [
        Rule("no-pickup-seeder", "*DbSeeder*", TransformActionKind.RemoveMarkedSection,
            "var pickupPoints =", "// --- Тестовые",
            reason: "ПВЗ нет в ТЗ — убран seed пунктов выдачи"),
        Rule("no-pickup-entity", "*Entities*", TransformActionKind.RemoveType, "PickupPoint",
            reason: "ПВЗ нет в ТЗ — удалён класс PickupPoint"),
        Rule("no-pickup-dbset", "*AppDbContext*", TransformActionKind.RemoveLineContaining,
            "DbSet<PickupPoint>", reason: "ПВЗ нет в ТЗ — убран DbSet PickupPoint"),
        Rule("no-pickup-order-nav", "*Entities*", TransformActionKind.RemoveLineContaining,
            "PickupPointId", reason: "ПВЗ нет в ТЗ — убран PickupPointId"),
        Rule("no-pickup-order-nav2", "*Entities*", TransformActionKind.RemoveLineContaining,
            "PickupPoint ", reason: "ПВЗ нет в ТЗ — убрана навигация PickupPoint"),
        Rule("no-pickup-vm", "*ViewModels*", TransformActionKind.RemoveProperty,
            "PickupPointId|PickupPoints|PickupCode|PickupAddress",
            reason: "ПВЗ нет в ТЗ — убраны поля ПВЗ в ViewModel"),
        Rule("no-pickup-service", "*OrderService*", TransformActionKind.RemoveMethod,
            "GetPickupPointsAsync", reason: "ПВЗ нет в ТЗ — убран GetPickupPointsAsync"),
        Rule("no-pickup-include", "*OrderService*", TransformActionKind.RemoveLineContaining,
            ".Include(o => o.PickupPoint)", reason: "ПВЗ нет в ТЗ — убран Include PickupPoint"),
        Rule("no-pickup-save-fields", "*OrderService*", TransformActionKind.RemoveLineContaining,
            "PickupCode|PickupPointId", reason: "ПВЗ нет в ТЗ — поля PickupCode/PickupPointId в SaveAsync"),
        Rule("no-pickup-edit", "*Orders/Edit*", TransformActionKind.RemoveFormGroup,
            "PickupPointId|PickupCode", reason: "ПВЗ нет в ТЗ — убраны поля ПВЗ в форме"),
        Rule("no-pickup-index-col", "*Orders/Index*", TransformActionKind.RemoveLineContaining,
            "PickupAddress|пункта выдачи|PickupCode|код получ",
            reason: "ПВЗ нет в ТЗ — убраны колонки ПВЗ"),
        Rule("no-pickup-service-method", "*OrderService*", TransformActionKind.RemoveMethod,
            "GetPickupPointsAsync", reason: "ПВЗ нет в ТЗ — убран GetPickupPointsAsync"),
        Rule("no-pickup-controller-method", "*OrdersController*", TransformActionKind.RemoveMethod,
            "FillLookupsAsync", reason: "ПВЗ нет в ТЗ — убран FillLookupsAsync"),
        Rule("no-pickup-controller-lines", "*OrdersController*", TransformActionKind.RemoveLineContaining,
            "PickupAddress|PickupPointId|PickupPoints|o.PickupPoint|GetPickupPointsAsync|PickupCode",
            reason: "ПВЗ нет в ТЗ — убраны ссылки на PickupPoint"),
        Rule("no-pickup-edit-all", "*Orders/Edit*", TransformActionKind.RemoveLineContaining,
            "PickupPoint|PickupCode", reason: "ПВЗ нет в ТЗ — форма заказа"),
        Rule("no-pickup-order-entity", "*Entities*", TransformActionKind.RemoveLineContaining,
            "PickupCode", reason: "ПВЗ нет в ТЗ — PickupCode"),
    ];

    private static IEnumerable<CodeTransformRule> OrderDeliveryCleanup() =>
    [
        Rule("no-delivery-vm", "*ViewModels*", TransformActionKind.RemoveProperty, "DeliveryDate",
            reason: "Дата доставки не в ТЗ"),
        Rule("no-delivery-entity", "*Entities*", TransformActionKind.RemoveProperty, "DeliveryDate",
            reason: "Дата доставки не в ТЗ — поле Order.DeliveryDate"),
        Rule("no-delivery-edit", "*Orders/Edit*", TransformActionKind.RemoveFormGroup, "DeliveryDate",
            reason: "Дата доставки не в ТЗ — поле формы"),
        Rule("no-delivery-index", "*Orders/Index*", TransformActionKind.RemoveLineContaining,
            "DeliveryDate|Доставк", reason: "Дата доставки не в ТЗ — колонка"),
        Rule("no-delivery-controller", "*OrdersController*", TransformActionKind.RemoveLineContaining,
            "DeliveryDate", reason: "Дата доставки не в ТЗ — контроллер"),
        Rule("no-delivery-service", "*OrderService*", TransformActionKind.RemoveLineContaining,
            "DeliveryDate", reason: "Дата доставки не в ТЗ — сервис"),
    ];

    private static IEnumerable<CodeTransformRule> OrderStatusCleanup() =>
    [
        Rule("no-status-enum", "*Enums*", TransformActionKind.RemoveEnum, "OrderStatus",
            reason: "Статусы заказа не в ТЗ — удалён enum OrderStatus"),
        Rule("no-status-vm", "*ViewModels*", TransformActionKind.RemoveProperty, "Status",
            reason: "Статусы не в ТЗ — поле Status"),
        Rule("no-status-entity", "*Entities*", TransformActionKind.RemoveProperty, "Status",
            reason: "Статусы не в ТЗ — Order.Status"),
        Rule("no-status-edit", "*Orders/Edit*", TransformActionKind.RemoveFormGroup, "Status|OrderStatus",
            reason: "Статусы не в ТЗ — поле формы"),
        Rule("no-status-index", "*Orders/Index*", TransformActionKind.RemoveLineContaining,
            ">Статус<|@order.Status", reason: "Статусы не в ТЗ — колонка"),
        Rule("no-status-controller", "*OrdersController*", TransformActionKind.RemoveLineContaining,
            "OrderStatus|Status = model|Status = order|Status = o.", reason: "Статусы не в ТЗ — контроллер"),
    ];

    private static CodeTransformRule Rule(
        string id,
        string stepMatch,
        TransformActionKind action,
        string parameter,
        string? parameter2 = null,
        string reason = "") =>
        new()
        {
            Id = id,
            StepMatch = stepMatch,
            Action = action,
            Parameter = parameter,
            Parameter2 = parameter2,
            Reason = string.IsNullOrEmpty(reason) ? id : reason,
            Condition = new TransformCondition()
        };
}
