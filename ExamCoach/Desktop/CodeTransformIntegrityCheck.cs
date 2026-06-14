namespace ExamCoachDesktop;

using System.Text.RegularExpressions;

/// <summary>Проверяет согласованность кода шагов после структурных правок.</summary>
public static class CodeTransformIntegrityCheck
{
    private static readonly (string StepMatch, string RequiredFragment, string BecauseStep, string BecauseFragment)[] Rules =
    [
        ("*OrdersController*", "SaveAsync", "*OrderService*", "SaveAsync"),
        ("*OrdersController*", "GetClientsAsync", "*OrderService*", "GetClientsAsync"),
        ("*ProductsController*", "StatusMessage", "*ViewModels*", "StatusMessage"),
        ("*Products/Index*", "StatusMessage", "*ViewModels*", "StatusMessage"),
        ("*OrdersController*", "Status = model.Status", "*Entities*", "Status { get"),
        ("*OrdersController*", "Status = order.Status", "*ViewModels*", "Status { get"),
        ("*Orders/Index*", "@order.Status", "*ViewModels*", "Status { get"),
    ];

    public static IReadOnlyList<string> Validate(CoachData data)
    {
        var errors = new List<string>();
        foreach (var (stepMatch, required, becauseStep, becauseFragment) in Rules)
        {
            var consumer = FindStep(data, stepMatch);
            if (consumer == null || !Contains(consumer.Code, becauseFragment))
                continue;

            var provider = FindStep(data, becauseStep);
            if (provider != null && Contains(provider.Code, required))
                continue;

            errors.Add(
                $"{consumer.Title}: ожидается «{required}» в {becauseStep}, но фрагмент отсутствует после адаптации.");
        }

        ValidateOrderServiceOrphans(data, errors);
        ValidatePickupFieldConsistency(data, errors);
        ValidateControllerEntityConsistency(data, errors);

        return errors;
    }

    private static void ValidateOrderServiceOrphans(CoachData data, List<string> errors)
    {
        var step = FindStep(data, "*OrderService*");
        if (step?.Code == null) return;

        if (step.Code.Contains("db.PickupPoints", StringComparison.Ordinal) &&
            !step.Code.Contains("GetPickupPointsAsync", StringComparison.Ordinal))
        {
            errors.Add(
                "Services/OrderService.cs: осиротевший фрагмент db.PickupPoints (метод GetPickupPointsAsync удалён неполностью).");
        }
    }

    private static void ValidatePickupFieldConsistency(CoachData data, List<string> errors)
    {
        var entities = FindStep(data, "*Entities*");
        var service = FindStep(data, "*OrderService*");
        if (entities?.Code == null || service?.Code == null) return;

        var orderHasPickup = entities.Code.Contains("PickupPointId", StringComparison.Ordinal);
        var serviceUsesPickup = service.Code.Contains("PickupPointId", StringComparison.Ordinal) ||
                                service.Code.Contains("PickupCode", StringComparison.Ordinal);

        if (!orderHasPickup && serviceUsesPickup)
        {
            errors.Add(
                "Services/OrderService.cs: SaveAsync ссылается на PickupPointId/PickupCode, но в Models/Entities.cs эти поля удалены.");
        }
    }

    private static void ValidateControllerEntityConsistency(CoachData data, List<string> errors)
    {
        var entities = FindStep(data, "*Entities*");
        var controller = FindStep(data, "*OrdersController*");
        var service = FindStep(data, "*OrderService*");
        if (entities?.Code == null) return;

        foreach (var field in new[] { "Status", "DeliveryDate", "PickupPointId", "PickupCode" })
        {
            if (entities.Code.Contains($"{field} {{ get", StringComparison.Ordinal))
                continue;

            if (controller?.Code != null && Regex.IsMatch(controller.Code, $@"\b{field}\b"))
                errors.Add($"Controllers/OrdersController.cs: ссылается на {field}, но поля нет в Order.");

            if (service?.Code != null && Regex.IsMatch(service.Code, $@"\b{field}\b"))
                errors.Add($"Services/OrderService.cs: ссылается на {field}, но поля нет в Order.");
        }

        if (controller?.Code.Contains("GetPickupPointsAsync", StringComparison.Ordinal) == true &&
            service?.Code.Contains("GetPickupPointsAsync", StringComparison.Ordinal) != true)
        {
            errors.Add("Controllers/OrdersController.cs: GetPickupPointsAsync отсутствует в OrderService.");
        }

        if (controller?.Code.Contains("SaveAsync", StringComparison.Ordinal) == true &&
            service?.Code.Contains("SaveAsync", StringComparison.Ordinal) != true)
        {
            errors.Add("Controllers/OrdersController.cs: SaveAsync отсутствует в OrderService.");
        }
    }

    private static CoachStep? FindStep(CoachData data, string pattern)
    {
        var tokens = pattern.Split('*', StringSplitOptions.RemoveEmptyEntries);
        return data.Steps.FirstOrDefault(s =>
            tokens.All(t => s.Title.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                            s.Id.Contains(t, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool Contains(string? code, string fragment) =>
        !string.IsNullOrEmpty(code) &&
        code.Contains(fragment, StringComparison.Ordinal);
}
