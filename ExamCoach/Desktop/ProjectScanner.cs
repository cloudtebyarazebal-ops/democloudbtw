using System.IO;
using System.Text.RegularExpressions;

namespace ExamCoachDesktop;

public sealed class ScannedFile
{
    public string RelativePath { get; init; } = "";
    public string Phase { get; init; } = "";
    public string Module { get; init; } = "";
    public string VsHint { get; init; } = "";
    public string Content { get; init; } = "";
}

public static class ProjectScanner
{
    public static IReadOnlyList<ScannedFile> ScanProject(string projectRoot)
    {
        var files = new List<ScannedFile>();
        var roots = new (string sub, string[] patterns, bool recursive)[]
        {
            ("", new[] { "*.csproj", "appsettings.json", "Program.cs" }, false),
            ("Models", new[] { "*.cs" }, false),
            ("Data", new[] { "*.cs" }, false),
            ("Services", new[] { "*.cs" }, false),
            ("ViewModels", new[] { "*.cs" }, false),
            ("Controllers", new[] { "*.cs" }, false),
            ("Views", new[] { "*.cshtml" }, true),
            ("wwwroot/css", new[] { "*.css" }, false),
            ("wwwroot/js", new[] { "*.js" }, false),
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (sub, patterns, recursive) in roots)
        {
            var baseDir = string.IsNullOrEmpty(sub) ? projectRoot : Path.Combine(projectRoot, sub);
            if (!Directory.Exists(baseDir)) continue;

            foreach (var pattern in patterns)
            {
                var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (var full in Directory.GetFiles(baseDir, pattern, option).OrderBy(f => f))
                {
                    var rel = Path.GetRelativePath(projectRoot, full).Replace('\\', '/');
                    if (rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) ||
                        rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!seen.Add(rel)) continue;

                    files.Add(new ScannedFile
                    {
                        RelativePath = rel,
                        Phase = GetPhase(rel),
                        Module = GetModule(rel),
                        VsHint = GetVsHint(rel),
                        Content = File.ReadAllText(full)
                    });
                }
            }
        }

        return files.OrderBy(f => PhaseOrder(f.Phase)).ThenBy(f => ModuleOrder(f.Module)).ThenBy(f => f.RelativePath).ToList();
    }

    public static string? FindCsprojName(string projectRoot)
    {
        var csprojs = Directory.GetFiles(projectRoot, "*.csproj");
        return csprojs.Length > 0 ? Path.GetFileName(csprojs[0]) : null;
    }

    private static int PhaseOrder(string phase) => phase switch
    {
        "0. Start" => 0, "1. Models" => 1, "2. Database" => 2, "3. Services" => 3,
        "4. ViewModels" => 4, "5. Program" => 5, "6. Controllers" => 6,
        "7. Views" => 7, "8. Static" => 8, _ => 9
    };

    private static int ModuleOrder(string module) => module switch
    {
        "m1" => 0, "m2" => 1, "m3" => 2, "m4" => 3, _ => 4
    };

    private static string GetPhase(string p)
    {
        if (Regex.IsMatch(p, @"\.csproj$|appsettings\.json$")) return "0. Start";
        if (p.StartsWith("Models/", StringComparison.OrdinalIgnoreCase)) return "1. Models";
        if (p.StartsWith("Data/", StringComparison.OrdinalIgnoreCase)) return "2. Database";
        if (p.StartsWith("Services/", StringComparison.OrdinalIgnoreCase)) return "3. Services";
        if (p.StartsWith("ViewModels/", StringComparison.OrdinalIgnoreCase)) return "4. ViewModels";
        if (p.Equals("Program.cs", StringComparison.OrdinalIgnoreCase)) return "5. Program";
        if (p.StartsWith("Controllers/", StringComparison.OrdinalIgnoreCase)) return "6. Controllers";
        if (p.StartsWith("Views/", StringComparison.OrdinalIgnoreCase)) return "7. Views";
        if (p.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase)) return "8. Static";
        return "9. Other";
    }

    private static string GetModule(string p)
    {
        p = p.Replace('\\', '/');
        if (Regex.IsMatch(p, @"\.csproj$|appsettings\.json$|Models/|Data/")) return "m1";
        if (p.Contains("ImportService", StringComparison.OrdinalIgnoreCase) || p.StartsWith("Views/Import/", StringComparison.OrdinalIgnoreCase)) return "m1";
        if (p.Equals("Program.cs", StringComparison.OrdinalIgnoreCase) || p.StartsWith("ViewModels/", StringComparison.OrdinalIgnoreCase)) return "m2";
        if (p.Contains("AuthService", StringComparison.OrdinalIgnoreCase) || p.Contains("UserAccess", StringComparison.OrdinalIgnoreCase)) return "m2";
        if (p.Contains("AccountController", StringComparison.OrdinalIgnoreCase) || p.StartsWith("Views/Account/", StringComparison.OrdinalIgnoreCase)) return "m2";
        if (p.Contains("Views/Products/Index", StringComparison.OrdinalIgnoreCase) || p.Contains("_Layout", StringComparison.OrdinalIgnoreCase)) return "m2";
        if (p.Contains("ProductService", StringComparison.OrdinalIgnoreCase) || p.Contains("ProductsController", StringComparison.OrdinalIgnoreCase)) return "m3";
        if (p.Contains("Views/Products/Edit", StringComparison.OrdinalIgnoreCase) || p.Contains("wwwroot/js/products", StringComparison.OrdinalIgnoreCase)) return "m3";
        if (p.Contains("wwwroot/css/shop", StringComparison.OrdinalIgnoreCase)) return "m3";
        if (p.Contains("OrderService", StringComparison.OrdinalIgnoreCase) || p.Contains("OrdersController", StringComparison.OrdinalIgnoreCase)) return "m4";
        if (p.StartsWith("Views/Orders/", StringComparison.OrdinalIgnoreCase)) return "m4";
        if (p.StartsWith("Services/", StringComparison.OrdinalIgnoreCase)) return "m3";
        if (p.StartsWith("Controllers/", StringComparison.OrdinalIgnoreCase)) return "m4";
        if (p.StartsWith("Views/", StringComparison.OrdinalIgnoreCase)) return "m4";
        return "m1";
    }

    private static string GetVsHint(string rel)
    {
        var name = Path.GetFileName(rel);
        return $"Create or update {rel} in Visual Studio.";
    }
}
