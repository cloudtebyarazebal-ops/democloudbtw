using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExamCoachDesktop;

public static class CoachDataBuilder
{
    public static CoachData BuildFromReferenceProject(string projectRoot, string coachConfigPath)
    {
        var config = LoadCoachConfig(coachConfigPath);
        var scanned = ProjectScanner.ScanProject(projectRoot);
        var steps = new List<CoachStep>();

        foreach (var setup in config.Setup)
        {
            steps.Add(new CoachStep
            {
                Id = setup.Id,
                Phase = setup.Phase,
                Module = setup.Module,
                Title = setup.Title,
                VsHint = setup.VsHint,
                Terminal = setup.Terminal ?? "",
                Code = ""
            });
        }

        foreach (var file in scanned)
        {
            steps.Add(new CoachStep
            {
                Id = "file-" + file.RelativePath.Replace('/', '-').Replace('.', '-'),
                Phase = file.Phase,
                Module = file.Module,
                Title = file.RelativePath,
                VsHint = file.VsHint,
                Code = file.Content
            });
        }

        return BuildWithModules(steps, config.Modules);
    }

    private static CoachData BuildWithModules(List<CoachStep> steps, List<CoachModuleTemplate> moduleTemplates)
    {
        var moduleMap = new Dictionary<string, List<int>>();
        for (var i = 0; i < steps.Count; i++)
        {
            var mid = steps[i].Module;
            if (!moduleMap.ContainsKey(mid)) moduleMap[mid] = [];
            moduleMap[mid].Add(i);
        }

        var modules = moduleTemplates.Select(m => new CoachModule
        {
            Id = m.Id,
            Title = m.Title,
            Minutes = m.Minutes,
            Description = m.Description,
            StepIndices = moduleMap.TryGetValue(m.Id, out var idx) ? idx : []
        }).ToList();

        return new CoachData { Steps = steps, Modules = modules };
    }

    private static CoachConfig LoadCoachConfig(string path)
    {
        if (!File.Exists(path))
            return CoachConfig.Default();
        return JsonSerializer.Deserialize<CoachConfig>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? CoachConfig.Default();
    }

    private sealed class CoachConfig
    {
        [JsonPropertyName("setup")]
        public List<CoachStepTemplate> Setup { get; set; } = [];

        [JsonPropertyName("modules")]
        public List<CoachModuleTemplate> Modules { get; set; } = [];

        public static CoachConfig Default() => new()
        {
            Modules =
            [
                new() { Id = "m1", Title = "Модуль 1 — БД", Minutes = 50, Description = "Models + Data" },
                new() { Id = "m2", Title = "Модуль 2 — Вход", Minutes = 40, Description = "Program + Login" },
                new() { Id = "m3", Title = "Модуль 3 — Товары", Minutes = 90, Description = "Products" },
                new() { Id = "m4", Title = "Модуль 4 — Заказы", Minutes = 60, Description = "Orders" }
            ]
        };
    }

    private sealed class CoachStepTemplate
    {
        public string Id { get; set; } = "";
        public string Phase { get; set; } = "";
        public string Module { get; set; } = "";
        public string Title { get; set; } = "";
        public string VsHint { get; set; } = "";
        public string? Terminal { get; set; }
    }

    private sealed class CoachModuleTemplate
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public int Minutes { get; set; }
        public string Description { get; set; } = "";
    }
}
