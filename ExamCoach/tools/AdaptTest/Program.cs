using System.Text;

namespace ExamCoachDesktop;

internal static class Program
{
    private const string DefaultPdf =
        @"c:\Users\User\Downloads\Telegram Desktop\В2_КОД 09.02.07-2-2026-БУ (2).pdf";

    private static readonly string DefaultCoachRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static readonly string DefaultProjectRoot =
        Path.GetFullPath(Path.Combine(DefaultCoachRoot, ".."));

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Contains("--test-surgery", StringComparer.OrdinalIgnoreCase))
            return RunSurgeryTests(ResolveCoachRoot(args));

        var tzPath = args.FirstOrDefault(a => !a.StartsWith('-') && !a.Contains('=')) ?? DefaultPdf;
        var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
        var init = args.Contains("--init", StringComparer.OrdinalIgnoreCase);
        var coachRoot = ResolveCoachRoot(args);
        var projectRoot = ResolveProjectRoot(args, coachRoot);

        if (apply)
        {
            if (!File.Exists(tzPath))
            {
                Console.Error.WriteLine("ТЗ не найдено: " + tzPath);
                return 1;
            }

            if (!ProjectTargetHelper.IsWebProject(projectRoot) && !init)
            {
                Console.Error.WriteLine("Web-проект не найден: " + projectRoot);
                Console.Error.WriteLine("Создайте MVC-проект (dotnet new mvc) или добавьте флаг --init.");
                return 1;
            }

            Console.WriteLine($"ТЗ: {tzPath}");
            Console.WriteLine($"Проект: {projectRoot}");
            Console.WriteLine($"ExamCoach: {coachRoot}");
            var count = ApplyAdaptedProject.Apply(tzPath, projectRoot, coachRoot, init);
            Console.WriteLine($"Записано файлов: {count}");
            return 0;
        }

        return RunChecks(tzPath, coachRoot);
    }

    private static string ResolveCoachRoot(string[] args)
    {
        var explicitPath = GetArgValue(args, "--coach-root");
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return Path.GetFullPath(explicitPath);

        if (File.Exists(Path.Combine(DefaultCoachRoot, "steps-data.json")))
            return DefaultCoachRoot;

        var fromCwd = FindCoachRoot(Environment.CurrentDirectory);
        return fromCwd ?? DefaultCoachRoot;
    }

    private static string ResolveProjectRoot(string[] args, string coachRoot)
    {
        var explicitPath = GetArgValue(args, "--project-root");
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return Path.GetFullPath(explicitPath);

        var fromCwd = FindMvcProjectRoot(Environment.CurrentDirectory);
        if (fromCwd != null)
            return fromCwd;

        var sibling = Path.GetFullPath(Path.Combine(coachRoot, ".."));
        if (ProjectTargetHelper.IsWebProject(sibling))
            return sibling;

        if (ProjectTargetHelper.IsWebProject(DefaultProjectRoot))
            return DefaultProjectRoot;

        return DefaultProjectRoot;
    }

    private static string? GetArgValue(string[] args, string name)
    {
        var prefix = name + "=";
        var match = args.FirstOrDefault(a =>
            a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return match?[prefix.Length..];
    }

    private static string? FindCoachRoot(string start)
    {
        var dir = start;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "steps-data.json")) &&
                Directory.Exists(Path.Combine(dir, "Desktop")))
                return dir;
            dir = Path.GetDirectoryName(dir)!;
        }

        return null;
    }

    private static string? FindMvcProjectRoot(string start)
    {
        var dir = start;
        while (!string.IsNullOrEmpty(dir))
        {
            if (ProjectTargetHelper.IsWebProject(dir))
                return dir;
            dir = Path.GetDirectoryName(dir)!;
        }

        return null;
    }

    private static int RunSurgeryTests(string coachRoot)
    {
        var sample = """
            public Task<List<PickupPoint>> GetPickupPointsAsync() =>
                db.PickupPoints.OrderBy(p => p.Address).ToListAsync();

            public Task<List<AppUser>> GetClientsAsync() =>
                db.Users.Where(u => u.Role == UserRole.Client).ToListAsync();

            public async Task<(bool Success, string? Error)> SaveAsync(Order order)
            {
                await db.SaveChangesAsync();
                return (true, null);
            }
            """;

        var result = CodeSurgery.RemovePublicMethod(sample, "GetPickupPointsAsync");
        var ok = result.Contains("GetClientsAsync") && result.Contains("SaveAsync")
            && !result.Contains("GetPickupPointsAsync") && !result.Contains("db.PickupPoints");
        Console.WriteLine(ok ? "✓ RemovePublicMethod: GetPickupPointsAsync не затронул соседние методы" : "✗ RemovePublicMethod сломал OrderService");
        if (!ok) return 1;

        var vm = """
            public string? StatusMessage { get; set; }
            public OrderStatus Status { get; set; }
            """;
        var vmResult = CodeSurgery.RemoveMembersContaining(vm, "Status");
        ok = vmResult.Contains("StatusMessage") && !vmResult.Contains("OrderStatus Status");
        Console.WriteLine(ok ? "✓ RemoveMembersContaining: Status не удаляет StatusMessage" : "✗ Status затронул StatusMessage");
        if (!ok) return 1;

        var vmMultiline = """
            public class OrderRowViewModel
            {
                public string Status { get; set; }
                    = string.Empty;
                public string OrderDate { get; set; }
                    = string.Empty;
            }
            """;
        var vmMultilineResult = CodeSurgery.RemoveMembersContaining(vmMultiline, "Status");
        ok = !vmMultilineResult.Contains("public string Status { get; set; }", StringComparison.Ordinal) &&
             !vmMultilineResult.Contains("\n                    = string.Empty;", StringComparison.Ordinal) &&
             vmMultilineResult.Contains("OrderDate", StringComparison.Ordinal);
        Console.WriteLine(ok ? "✓ RemoveMembersContaining: удаляет многострочный инициализатор свойства" : "✗ Остался хвост '= ...' после удаления свойства");
        if (!ok) return 1;

        var seederSample = """
            public static async Task SeedAsync()
            {
                var users = new[] { "u1" };
                // --- Пример заказа
                var order = new { PickupPointId = 1 };
                await db.SaveChangesAsync();
            }
            """;
        var seederResult = CodeSurgery.RemoveMarkedSection(seederSample, "// --- Пример заказа", "await db.SaveChangesAsync");
        ok = !seederResult.Contains("PickupPointId", StringComparison.Ordinal) &&
             seederResult.Contains("await db.SaveChangesAsync();", StringComparison.Ordinal) &&
             seederResult.Contains("public static async Task SeedAsync()", StringComparison.Ordinal);
        Console.WriteLine(ok ? "✓ RemoveMarkedSection: убирает блок заказа и сохраняет SaveChanges" : "✗ RemoveMarkedSection удалил лишний код в DbSeeder");
        if (!ok) return 1;

        ok = RunCrossFileSyncTest(coachRoot);
        Console.WriteLine(ok ? "✓ CrossFileSync: контроллер согласован с Entities" : "✗ CrossFileSync не синхронизировал OrdersController");
        return ok ? 0 : 1;
    }

    private static bool RunCrossFileSyncTest(string coachRoot)
    {
        var basePath = Path.Combine(coachRoot, "steps-data.json");
        var data = CoachLoader.Load(basePath);
        var profile = new AssignmentProfile
        {
            AssignmentKind = "Custom",
            SourceText = "магазин книг авторизация заказы crud товаров",
            Requirements = new AssignmentRequirements
            {
                AssignmentKind = "Custom",
                Features = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    AssignmentFeatures.Auth,
                    AssignmentFeatures.Orders,
                    AssignmentFeatures.ProductCrud
                }
            }
        };

        AssignmentRequirementsAnalyzer.Analyze(profile.SourceText, profile);
        profile.Requirements!.Features.Remove(AssignmentFeatures.PickupPoints);
        profile.Requirements.Features.Remove(AssignmentFeatures.OrderStatusDetail);
        profile.Requirements.Features.Remove(AssignmentFeatures.OrderDelivery);

        var adapted = AssignmentAdaptEngine.Adapt(CoachDataSerializer.Clone(data), profile);
        var controller = adapted.Steps.First(s => s.Title.Contains("OrdersController", StringComparison.OrdinalIgnoreCase));
        var entities = adapted.Steps.First(s => s.Title.Contains("Entities", StringComparison.OrdinalIgnoreCase));
        var service = adapted.Steps.First(s => s.Title.Contains("OrderService", StringComparison.OrdinalIgnoreCase));
        var vm = adapted.Steps.First(s => s.Title.Contains("ViewModels", StringComparison.OrdinalIgnoreCase));

        var entitiesOk = !entities.Code.Contains("PickupPointId { get", StringComparison.Ordinal);
        var controllerOk = !controller.Code.Contains("PickupPointId", StringComparison.Ordinal) &&
                           !controller.Code.Contains("OrderStatus", StringComparison.Ordinal) &&
                           controller.Code.Contains("SaveAsync", StringComparison.Ordinal);
        var serviceOk = service.Code.Contains("SaveAsync", StringComparison.Ordinal) &&
                        service.Code.Contains("GetClientsAsync", StringComparison.Ordinal) &&
                        !service.Code.Contains("db.PickupPoints", StringComparison.Ordinal);
        var vmOk = vm.Code.Contains("StatusMessage { get", StringComparison.Ordinal);

        return entitiesOk && controllerOk && serviceOk && vmOk && profile.IntegrityIssues.Count == 0;
    }

    private static int RunChecks(string tzPath, string coachRoot)
    {
        var basePath = Path.Combine(coachRoot, "steps-data.json");
        var text = AssignmentDocumentReader.ReadFile(tzPath);
        var profile = AssignmentTextParser.Parse(text);
        var adapted = AssignmentAdaptEngine.Adapt(CoachLoader.Load(basePath), profile);
        var req = profile.Requirements!;

        Console.WriteLine($"=== {Path.GetFileName(tzPath)} — сверка с кодом ExamCoach ===\n");
        Console.WriteLine($"Распознано: тип={profile.AssignmentKind ?? "—"}, вариант={profile.ExamVariant ?? "—"}");
        Console.WriteLine($"Функции: [{string.Join(", ", req.Features.OrderBy(f => f))}]");
        if (req.SeedData != null)
        {
            Console.WriteLine($"Сид БД: категории=[{string.Join(", ", req.SeedData.Categories)}]");
            Console.WriteLine($"Сид БД: {(req.SeedData.UseAuthorField ? "авторы" : "производители")}=[{string.Join(", ", req.SeedData.Makers)}]");
            Console.WriteLine($"Сид БД: товаров={req.SeedData.Products.Count}, скидка>{req.SeedData.DiscountHighlightPercent}% ({req.SeedData.DiscountHighlightColor})");
        }
        Console.WriteLine($"Шагов: {adapted.Steps.Count}, правок: {profile.TransformLog.Count}, замен: {profile.Replacements.Count}");
        Console.WriteLine();
        Console.WriteLine(profile.AdaptationSummary);

        if (profile.IntegrityIssues.Count > 0)
        {
            Console.WriteLine("\n⚠ Проблемы целостности:");
            foreach (var issue in profile.IntegrityIssues)
                Console.WriteLine("  · " + issue);
            return 1;
        }

        Console.WriteLine("\n✓ Проверка целостности пройдена");
        return 0;
    }
}
