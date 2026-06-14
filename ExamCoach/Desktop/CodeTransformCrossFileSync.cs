namespace ExamCoachDesktop;

/// <summary>
/// Согласует OrdersController, OrderService и Views с фактическим содержимым
/// Models/Entities.cs и Models/Enums.cs после структурных правок.
/// </summary>
public static class CodeTransformCrossFileSync
{
    private static readonly string[] OrderConsumerPatterns =
    [
        "*OrdersController*",
        "*OrderService*",
        "*Orders/Index*",
        "*Orders/Edit*",
        "*ViewModels*",
        "*DbSeeder*"
    ];

    public static void Sync(CoachData data)
    {
        var entities = GetStepCode(data, "*Entities*");
        var enums = GetStepCode(data, "*Enums*");
        if (entities == null) return;

        var hasStatus = OrderEntityHas(entities, "Status") && enums?.Contains("enum OrderStatus", StringComparison.Ordinal) == true;
        var hasDelivery = OrderEntityHas(entities, "DeliveryDate");
        var hasPickup = OrderEntityHas(entities, "PickupCode") ||
                        OrderEntityHas(entities, "PickupPointId") ||
                        entities.Contains("class PickupPoint", StringComparison.Ordinal);

        if (!hasStatus)
            StripTokens(data, ["OrderStatus", "Status"]);

        if (!hasDelivery)
            StripTokens(data, ["DeliveryDate"]);

        if (!hasPickup)
        {
            StripTokens(data,
            [
                "GetPickupPointsAsync",
                "PickupPoints",
                "PickupPointId",
                "PickupAddress",
                "PickupCode",
                "PickupPoint",
                ".Include(o => o.PickupPoint)"
            ]);

            foreach (var step in FindSteps(data, ["*OrdersController*"]))
            {
                if (step.Code.Contains("FillLookupsAsync", StringComparison.Ordinal))
                    step.Code = CodeSurgery.RemovePublicMethod(step.Code, "FillLookupsAsync");
            }
        }

        SimplifyOrderIndexMapping(data, hasStatus, hasDelivery, hasPickup);
        EnsureProductsStatusMessage(data);
    }

    private static void SimplifyOrderIndexMapping(CoachData data, bool hasStatus, bool hasDelivery, bool hasPickup)
    {
        var controller = FindStep(data, "*OrdersController*");
        if (controller?.Code == null) return;

        if (hasStatus && hasDelivery && hasPickup) return;

        // Упрощённая проекция строки заказа, если часть полей снята с модели.
        const string complex = """
                Status = o.Status == OrderStatus.New ? "Новый" : "Завершен",
                PickupAddress = o.PickupPoint.Address,
                OrderDate = o.OrderDate.ToString("dd.MM.yyyy"),
                DeliveryDate = o.DeliveryDate?.ToString("dd.MM.yyyy") ?? "—",
                ClientName = o.Client?.FullName ?? "—"
            """;

        var simple = """
                OrderDate = o.OrderDate.ToString("dd.MM.yyyy"),
                ClientName = o.Client?.FullName ?? "—"
            """;

        if (controller.Code.Contains(complex, StringComparison.Ordinal))
            controller.Code = controller.Code.Replace(complex, simple);

        const string createComplex = """
            Status = OrderStatus.New,
            OrderDate = DateTime.Today,
            PickupCode = Guid.NewGuid().ToString("N")[..6]
            """;

        const string createSimple = """
            OrderDate = DateTime.Today
            """;

        if (controller.Code.Contains(createComplex, StringComparison.Ordinal))
            controller.Code = controller.Code.Replace(createComplex, createSimple);
    }

    private static void EnsureProductsStatusMessage(CoachData data)
    {
        var vmStep = FindStep(data, "*ViewModels*");
        var indexStep = FindStep(data, "*Products/Index*");
        var controllerStep = FindStep(data, "*ProductsController*");
        if (vmStep?.Code == null) return;

        if (vmStep.Code.Contains("StatusMessage { get", StringComparison.Ordinal))
            return;

        const string property = """
    /// <summary>Сообщение об успехе/ошибке операции.</summary>
    public string? StatusMessage { get; set; }
""";

        var marker = "public class ProductsIndexViewModel";
        var idx = vmStep.Code.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return;

        var insertAt = vmStep.Code.IndexOf('}', idx);
        if (insertAt < 0) return;

        vmStep.Code = vmStep.Code.Insert(insertAt, "\n" + property + "\n");

        // View/контроллер ссылаются на StatusMessage — свойство восстановлено.
        _ = indexStep;
        _ = controllerStep;
    }

    private static void StripTokens(CoachData data, IEnumerable<string> tokens)
    {
        foreach (var step in FindSteps(data, OrderConsumerPatterns))
        {
            var code = step.Code ?? "";
            foreach (var token in tokens)
                code = token.StartsWith('.')
                    ? CodeSurgery.RemoveLineContaining(code, token)
                    : CodeSurgery.RemoveLineContainingWord(code, token);
            step.Code = code;
        }
    }

    private static bool OrderEntityHas(string entitiesCode, string propertyName) =>
        entitiesCode.Contains($"{propertyName} {{ get", StringComparison.Ordinal) ||
        entitiesCode.Contains($"{propertyName}{{get", StringComparison.Ordinal);

    private static string? GetStepCode(CoachData data, string pattern) =>
        FindStep(data, pattern)?.Code;

    private static CoachStep? FindStep(CoachData data, string pattern)
    {
        var tokens = pattern.Split('*', StringSplitOptions.RemoveEmptyEntries);
        return data.Steps.FirstOrDefault(s =>
            tokens.All(t => s.Title.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                            s.Id.Contains(t, StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<CoachStep> FindSteps(CoachData data, IEnumerable<string> patterns)
    {
        var seen = new HashSet<CoachStep>();
        foreach (var pattern in patterns)
        {
            var step = FindStep(data, pattern);
            if (step != null && seen.Add(step))
                yield return step;
        }
    }
}
