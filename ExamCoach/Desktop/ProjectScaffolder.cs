using System.IO;

namespace ExamCoachDesktop;

public static class ProjectScaffolder
{
    private static readonly string[] ExtraFolders =
    [
        "Models",
        "Data",
        "Services",
        "ViewModels",
        "Controllers",
        "Views",
        "Views/Account",
        "Views/Products",
        "Views/Orders",
        "Views/Import",
        "Views/Home",
        "Views/Shared",
        "wwwroot/css",
        "wwwroot/js",
        "wwwroot/images/products"
    ];

    public static IReadOnlyList<string> GetFolders(IEnumerable<string> filePaths)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in ExtraFolders)
            set.Add(folder.Replace('\\', '/'));

        foreach (var path in filePaths)
        {
            var normalized = path.Replace('\\', '/');
            var dir = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            while (!string.IsNullOrEmpty(dir))
            {
                set.Add(dir);
                var idx = dir.LastIndexOf('/');
                dir = idx > 0 ? dir[..idx] : null;
            }
        }

        return set.OrderBy(f => f.Count(c => c == '/')).ThenBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IReadOnlyList<string> NormalizeFilePaths(IEnumerable<string> paths) =>
        paths.Select(p => p.Replace('\\', '/')).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToList();
}
