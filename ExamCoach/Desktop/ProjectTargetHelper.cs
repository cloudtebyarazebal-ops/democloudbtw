using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ExamCoachDesktop;

/// <summary>Привязка эталонного кода KodShopWeb к любому MVC/Web-проекту (в т.ч. пустому).</summary>
public static class ProjectTargetHelper
{
    public const string ReferenceRootNamespace = "KodShopWeb";

    public static string? FindCsprojPath(string projectRoot)
    {
        if (!Directory.Exists(projectRoot))
            return null;

        var csprojs = Directory.GetFiles(projectRoot, "*.csproj", SearchOption.TopDirectoryOnly)
            .Where(p => !IsToolProject(p))
            .ToArray();

        if (csprojs.Length == 0)
            return null;

        return csprojs.Length == 1
            ? csprojs[0]
            : csprojs.FirstOrDefault(p => p.Contains("Shop", StringComparison.OrdinalIgnoreCase))
              ?? csprojs[0];
    }

    public static string GetRootNamespace(string projectRoot)
    {
        var csproj = FindCsprojPath(projectRoot);
        if (csproj == null)
            return ReferenceRootNamespace;

        var content = File.ReadAllText(csproj);
        var match = Regex.Match(content, @"<RootNamespace>\s*(.+?)\s*</RootNamespace>", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value.Trim();

        return Path.GetFileNameWithoutExtension(csproj);
    }

    public static string ResolveRelativePath(string relativePath, string projectRoot)
    {
        relativePath = relativePath.Replace('\\', '/');
        if (relativePath.Equals("KodShopWeb.csproj", StringComparison.OrdinalIgnoreCase))
        {
            var csproj = FindCsprojPath(projectRoot);
            if (csproj != null)
                return Path.GetFileName(csproj);
        }

        return relativePath.Replace('/', Path.DirectorySeparatorChar);
    }

    public static bool IsWebProject(string projectRoot) => FindCsprojPath(projectRoot) != null;

    public static bool IsScaffoldedProject(string projectRoot) =>
        File.Exists(Path.Combine(projectRoot, "Services", "ProductService.cs"));

    public static void EnsureFolderStructure(string projectRoot, IEnumerable<string> relativeFilePaths)
    {
        var paths = ProjectScaffolder.NormalizeFilePaths(
            relativeFilePaths.Select(p => ResolveRelativePath(p, projectRoot)));

        foreach (var folder in ProjectScaffolder.GetFolders(paths))
        {
            var full = Path.Combine(projectRoot, folder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(full);
        }

        Directory.CreateDirectory(Path.Combine(projectRoot, "wwwroot", "images", "products"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "wwwroot", "import"));
    }

    public static string RewriteRootNamespace(string code, string rootNamespace)
    {
        if (string.IsNullOrEmpty(code) ||
            string.Equals(rootNamespace, ReferenceRootNamespace, StringComparison.Ordinal))
            return code;

        return code
            .Replace($"using {ReferenceRootNamespace}.", $"using {rootNamespace}.", StringComparison.Ordinal)
            .Replace($"namespace {ReferenceRootNamespace}.", $"namespace {rootNamespace}.", StringComparison.Ordinal)
            .Replace($"@model {ReferenceRootNamespace}.", $"@model {rootNamespace}.", StringComparison.Ordinal)
            .Replace($"@using {ReferenceRootNamespace}.", $"@using {rootNamespace}.", StringComparison.Ordinal)
            .Replace($"Order = {ReferenceRootNamespace}.", $"Order = {rootNamespace}.", StringComparison.Ordinal);
    }

    public static void EnsureNuGetPackages(string projectRoot, string? templateCsproj = null)
    {
        var csprojPath = FindCsprojPath(projectRoot);
        if (csprojPath == null)
            return;

        var required = ExtractPackageReferences(templateCsproj ?? BuildDefaultCsprojTemplate());
        if (required.Count == 0)
            return;

        var doc = XDocument.Load(csprojPath);
        var root = doc.Root ?? throw new InvalidOperationException("Invalid csproj: " + csprojPath);
        var ns = root.Name.Namespace;

        var packageGroups = root.Elements(ns + "ItemGroup")
            .Where(g => g.Elements(ns + "PackageReference").Any())
            .ToList();

        var targetGroup = packageGroups.FirstOrDefault();
        if (targetGroup == null)
        {
            targetGroup = new XElement(ns + "ItemGroup");
            root.Add(targetGroup);
        }

        var existing = targetGroup.Elements(ns + "PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var package in required)
        {
            if (existing.Contains(package.Id))
                continue;

            var element = new XElement(ns + "PackageReference",
                new XAttribute("Include", package.Id),
                new XAttribute("Version", package.Version));

            if (package.Id.Contains("EntityFrameworkCore.Design", StringComparison.OrdinalIgnoreCase))
            {
                element.Add(
                    new XElement(ns + "PrivateAssets", "all"),
                    new XElement(ns + "IncludeAssets",
                        "runtime; build; native; contentfiles; analyzers; buildtransitive"));
            }

            targetGroup.Add(element);
        }

        doc.Save(csprojPath);
    }

    public static void EnsureExamCoachExcluded(string projectRoot)
    {
        var csprojPath = FindCsprojPath(projectRoot);
        if (csprojPath == null)
            return;

        var content = File.ReadAllText(csprojPath);
        if (content.Contains("ExamCoach/**", StringComparison.Ordinal))
            return;

        const string block = """
  <ItemGroup>
    <Compile Remove="ExamCoach/**" />
    <Content Remove="ExamCoach/**" />
    <None Remove="ExamCoach/**" />
    <EmbeddedResource Remove="ExamCoach/**" />
  </ItemGroup>

""";

        if (content.Contains("<PackageReference", StringComparison.Ordinal))
        {
            content = content.Replace("  <ItemGroup>\r\n    <PackageReference", block + "  <ItemGroup>\r\n    <PackageReference");
            content = content.Replace("  <ItemGroup>\n    <PackageReference", block + "  <ItemGroup>\n    <PackageReference");
        }
        else
        {
            content = content.Replace("</Project>", block + "</Project>");
        }

        File.WriteAllText(csprojPath, content);
    }

    public static int InitMvcProject(string projectRoot, string? projectName = null)
    {
        Directory.CreateDirectory(projectRoot);
        if (FindCsprojPath(projectRoot) != null)
            return 0;

        projectName ??= new DirectoryInfo(projectRoot).Name;
        if (string.IsNullOrWhiteSpace(projectName))
            projectName = "KodShopWeb";

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"new mvc -n \"{projectName}\" -f net8.0 -o \"{projectRoot}\"",
            WorkingDirectory = Path.GetDirectoryName(projectRoot) ?? projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start dotnet new mvc");

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var err = process.StandardError.ReadToEnd();
            throw new InvalidOperationException("dotnet new mvc failed: " + err);
        }

        return 1;
    }

    public static void CleanupDefaultMvcTemplate(string projectRoot)
    {
        var toRemove = new[]
        {
            Path.Combine(projectRoot, "Controllers", "HomeController.cs"),
            Path.Combine(projectRoot, "Models", "ErrorViewModel.cs"),
            Path.Combine(projectRoot, "Views", "Home", "Index.cshtml"),
            Path.Combine(projectRoot, "Views", "Home", "Privacy.cshtml"),
            Path.Combine(projectRoot, "Views", "Shared", "Error.cshtml"),
            Path.Combine(projectRoot, "Views", "Shared", "_Layout.cshtml.css")
        };

        foreach (var path in toRemove)
        {
            if (!File.Exists(path))
                continue;
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }

    public static string BuildDefaultCsprojTemplate() =>
        """
        <Project Sdk="Microsoft.NET.Sdk.Web">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="ClosedXML" Version="0.105.0" />
            <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.11" />
            <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.11" />
          </ItemGroup>
        </Project>
        """;

    private static bool IsToolProject(string path) =>
        path.Contains("ExamCoach", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("AdaptTest", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("PdfExtract", StringComparison.OrdinalIgnoreCase);

    private static List<PackageRef> ExtractPackageReferences(string csprojXml)
    {
        var result = new List<PackageRef>();
        if (string.IsNullOrWhiteSpace(csprojXml))
            return result;

        try
        {
            var doc = XDocument.Parse(csprojXml);
            var root = doc.Root;
            if (root == null)
                return result;

            var ns = root.Name.Namespace;
            foreach (var element in root.Descendants(ns + "PackageReference"))
            {
                var id = element.Attribute("Include")?.Value;
                var version = element.Attribute("Version")?.Value;
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(version))
                    result.Add(new PackageRef(id, version));
            }
        }
        catch
        {
            foreach (Match match in Regex.Matches(csprojXml,
                         "PackageReference\\s+Include=\"([^\"]+)\"\\s+Version=\"([^\"]+)\"",
                         RegexOptions.IgnoreCase))
            {
                result.Add(new PackageRef(match.Groups[1].Value, match.Groups[2].Value));
            }
        }

        return result;
    }

    private sealed record PackageRef(string Id, string Version);
}
