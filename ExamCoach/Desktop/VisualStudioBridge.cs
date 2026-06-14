using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ExamCoachDesktop;

/// <summary>
/// Подключение к Visual Studio и работа с любым новым MVC-проектом (не только KodShopWeb).
/// </summary>
public sealed class VisualStudioBridge
{
    private const int VsProjectItemKindPhysicalFolder = 28;
    private const int VsProjectItemKindVirtualFolder = 27;

    private static readonly string[] DteProgIds =
    [
        "VisualStudio.DTE.18.0",
        "VisualStudio.DTE.17.0",
        "VisualStudio.DTE.16.0"
    ];

    private dynamic? _dte;
    private string? _manualProjectRoot;
    private string? _detectedProjectRoot;
    private string? _detectedProjectName;
    private string _status = "Visual Studio не подключён";
    private GhostHintState _ghost = new();
    private bool _overtypeWasEnabled;

    // vsFontColorComment — бледный «призрачный» текст (зависит от темы VS)
    private const int GhostFontColor = 3;
    private const int VsOverwriteMode = 1;

    public bool IsConnected => _dte != null;
    public string Status => _status;
    public string? ManualProjectRoot => _manualProjectRoot;
    public string? DetectedProjectRoot => _detectedProjectRoot;
    public string? ProjectRoot => _manualProjectRoot ?? _detectedProjectRoot;

    /// <summary>Шаг эталона KodShopWeb.csproj → реальный .csproj в новом/пустом проекте.</summary>
    public string ResolveStepPath(string relativePath)
    {
        relativePath = relativePath.Replace('\\', '/');
        if (!relativePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            return relativePath;

        var root = ProjectRoot;
        if (root == null) return relativePath;

        var csprojs = Directory.GetFiles(root, "*.csproj");
        if (csprojs.Length == 0) return relativePath;

        // Один web-проект в папке — используем его (Empty, MVC, любое имя)
        return Path.GetFileName(csprojs[0]).Replace('\\', '/');
    }

    public void SetManualProjectRoot(string? path)
    {
        _manualProjectRoot = string.IsNullOrWhiteSpace(path)
            ? null
            : Path.GetFullPath(path.TrimEnd('\\', '/'));
        RefreshProjectInfo();
    }

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.Interface)] out object ppunk);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static object? TryGetActiveObject(string progId)
    {
        var type = Type.GetTypeFromProgID(progId);
        if (type == null) return null;
        var clsid = type.GUID;
        GetActiveObject(ref clsid, IntPtr.Zero, out var obj);
        return obj;
    }

    private dynamic? _cachedWebProject;
    private static string? _devenvPath;

    private dynamic? GetCachedWebProject()
    {
        if (_cachedWebProject != null) return _cachedWebProject;
        _cachedWebProject = FindWebProject();
        return _cachedWebProject;
    }

    private void InvalidateProjectCache() => _cachedWebProject = null;

    public bool TryConnect()
    {
        _dte = null;
        _detectedProjectRoot = null;
        _detectedProjectName = null;
        InvalidateProjectCache();

        dynamic? best = null;
        var bestScore = -1;

        foreach (var progId in DteProgIds)
        {
            try
            {
                var obj = TryGetActiveObject(progId);
                if (obj == null) continue;
                var score = ScoreDteInstance(obj);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = obj;
                }
            }
            catch (COMException)
            {
                // next ProgID
            }
        }

        if (best == null)
        {
            _status = "Запустите Visual Studio и откройте новый MVC-проект (.sln)";
            return false;
        }

        _dte = best;
        RefreshProjectInfo();
        return true;
    }

    private static int ScoreDteInstance(dynamic dte)
    {
        var score = 0;
        if (Safe(() => (bool)dte.MainWindow.Visible)) score += 20;
        if (Safe(() => (bool)dte.Solution.IsOpen)) score += 10;
        if (Safe(() => dte.ActiveDocument != null)) score += 2;
        return score;
    }

    public void RefreshProjectInfo()
    {
        InvalidateProjectCache();
        if (_dte == null)
        {
            _status = "Visual Studio не подключён";
            return;
        }

        var version = Safe(() => (string)_dte!.Version) ?? "?";
        var solution = Safe(() => (string)_dte!.Solution.FullName);
        var project = FindWebProject();

        if (project != null)
        {
            var csproj = Safe(() => (string)project.FullName) ?? "";
            _detectedProjectRoot = string.IsNullOrEmpty(csproj) ? null : Path.GetDirectoryName(csproj);
            _detectedProjectName = Safe(() => (string)project.Name) ?? Path.GetFileNameWithoutExtension(csproj);
        }
        else
        {
            _detectedProjectRoot = null;
            _detectedProjectName = null;
        }

        var root = ProjectRoot;
        if (root != null)
        {
            var src = _manualProjectRoot != null ? "выбранная папка" : "из VS";
            _status = $"VS {version} · проект «{_detectedProjectName ?? Path.GetFileName(root)}» ({src})";
        }
        else if (!string.IsNullOrWhiteSpace(solution) && solution != "Unknown")
        {
            _status = $"VS {version} · {Path.GetFileName(solution)} — укажите папку нового проекта (.csproj)";
        }
        else
        {
            _status = $"VS {version} · создайте File → New → MVC и откройте .sln";
        }
    }

    public void ActivateVisualStudio()
    {
        if (_dte == null) return;
        Safe(() => _dte!.MainWindow.Activate());
        try
        {
            var hwnd = (IntPtr)_dte!.MainWindow.HWnd;
            if (hwnd != IntPtr.Zero)
                SetForegroundWindow(hwnd);
        }
        catch { /* COM */ }
    }

    public bool OpenStepFile(string relativePath) =>
        OpenStepFileInternal(relativePath, activateVs: true).Success;

    public bool IsDocumentActive(string relativePath)
    {
        if (_dte == null) return false;
        if (!TryResolveFullPath(relativePath, out var fullPath, out _) || fullPath == null)
            return false;

        var active = Safe(() => (string)_dte!.ActiveDocument.FullName);
        return !string.IsNullOrEmpty(active) && PathsEqual(active, fullPath);
    }

    public FileEnsureResult OpenStepFileInternal(string relativePath, bool activateVs = true)
    {
        if (_dte == null && !TryConnect())
            return FileEnsureResult.Fail("Visual Studio не подключён");

        var resolved = TryResolveFullPath(relativePath, out var fullPath, out var error);
        if (!resolved || fullPath == null)
            return FileEnsureResult.Fail(error ?? "Пустой путь");

        if (!File.Exists(fullPath))
            return FileEnsureResult.Fail($"Файл не найден: {relativePath}");

        if (!SwitchToDocument(fullPath, allowAddToProject: true))
        {
            var active = GetActiveDocumentPath();
            var hint = active != null ? $"\nСейчас в VS: {Path.GetFileName(active)}" : "";
            return FileEnsureResult.Fail($"Не удалось переключить: {relativePath}{hint}");
        }

        if (activateVs)
            ActivateVisualStudio();

        _status = $"Открыт: {Path.GetFileName(relativePath)}";
        return new FileEnsureResult(true, false, relativePath, null);
    }

    public FileEnsureResult EnsureFile(string relativePath, bool openInEditor, bool createIfMissing = true)
    {
        if (_dte == null)
            return FileEnsureResult.Fail("Visual Studio не подключён");

        var resolved = TryResolveFullPath(relativePath, out var fullPath, out var error);
        if (!resolved || fullPath == null)
            return FileEnsureResult.Fail(error ?? "Пустой путь");

        try
        {
            var wasCreated = false;
            if (!File.Exists(fullPath))
            {
                if (!createIfMissing)
                    return FileEnsureResult.Fail($"Файл не найден: {relativePath}");

                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(fullPath, "", Encoding.UTF8);
                wasCreated = true;
            }

            dynamic? docItem = null;
            if (wasCreated)
            {
                var project = GetCachedWebProject();
                if (project != null)
                    docItem = AddFileToProject(project, fullPath, ProjectRoot!);
            }

            if (openInEditor)
            {
                if (!SwitchToDocument(fullPath, allowAddToProject: wasCreated, addedItem: docItem))
                    return FileEnsureResult.Fail($"Не удалось открыть: {relativePath}");
                ActivateVisualStudio();
            }

            _status = wasCreated ? $"Создан: {relativePath}" : $"Открыт: {Path.GetFileName(relativePath)}";
            return new FileEnsureResult(true, wasCreated, relativePath, null);
        }
        catch (Exception ex)
        {
            return FileEnsureResult.Fail(ex.Message);
        }
    }

    private bool TryResolveFullPath(string relativePath, out string? fullPath, out string? error)
    {
        fullPath = null;
        error = null;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            error = "Пустой путь";
            return false;
        }

        var root = ProjectRoot;
        if (root == null)
        {
            error = "Укажите папку проекта";
            return false;
        }

        relativePath = ResolveStepPath(relativePath.Replace('\\', '/'));
        root = NormalizePath(root);
        fullPath = NormalizePath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !PathsEqual(fullPath, root))
        {
            error = "Недопустимый путь";
            fullPath = null;
            return false;
        }

        return true;
    }
    public ScaffoldSummary EnsureFolderStructure(IEnumerable<string> folderPaths)
    {
        var summary = new ScaffoldSummary();
        var root = ProjectRoot;
        if (_dte == null || root is null) return summary;
        var projectRoot = root;
        ArgumentNullException.ThrowIfNull(projectRoot);

        var project = GetCachedWebProject();
        foreach (var folder in folderPaths.Select(f => f.Replace('\\', '/')).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(folder)) continue;
            var fullDir = Path.Combine(projectRoot, folder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(fullDir))
            {
                Directory.CreateDirectory(fullDir);
                summary.FoldersCreated++;
            }

            if (project != null)
                Safe(() => EnsureFolderInProject(project, folder));
        }

        _status = $"Папки готовы ({summary.FoldersCreated} новых)";
        return summary;
    }

    /// <summary>Создаёт все пустые файлы шагов + папки.</summary>
    public ScaffoldSummary ScaffoldAllFiles(IReadOnlyList<string> filePaths)
    {
        var paths = ProjectScaffolder.NormalizeFilePaths(filePaths.Select(ResolveStepPath));
        var folders = ProjectScaffolder.GetFolders(paths);
        var folderSummary = EnsureFolderStructure(folders);

        var created = 0;
        var skipped = 0;
        foreach (var path in paths)
        {
            var result = EnsureFile(path, openInEditor: false);
            if (!result.Success) continue;
            if (result.WasCreated) created++;
            else skipped++;
        }

        var total = new ScaffoldSummary
        {
            FoldersCreated = folderSummary.FoldersCreated,
            FilesCreated = created,
            FilesSkipped = skipped
        };
        _status = "Структура проекта: " + total.Message;
        return total;
    }

    public FileEnsureResult EnsureFile(string relativePath, bool openInEditor)
        => EnsureFile(relativePath, openInEditor, createIfMissing: true);

    /// <summary>Переключает VS на файл: вкладки, DTE, devenv /Edit.</summary>
    private bool SwitchToDocument(string fullPath, bool allowAddToProject, dynamic? addedItem = null)
    {
        var target = NormalizePath(fullPath);

        if (IsActiveDocument(target))
            return true;

        if (TryActivateOpenDocument(target) && WaitForActiveDocument(target))
            return true;

        if (TryActivateViaWindows(target) && WaitForActiveDocument(target))
            return true;

        if (addedItem != null)
        {
            Safe(() => addedItem.Open("ViewCode"));
            if (WaitForActiveDocument(target))
                return true;
        }

        var project = GetCachedWebProject();
        if (project != null)
        {
            var item = FindProjectItemByPath(project, target);
            if (item != null)
            {
                Safe(() => item.Open("ViewCode"));
                if (WaitForActiveDocument(target))
                    return true;
            }
        }

        TryDteOpenFile(target);
        if (WaitForActiveDocument(target))
            return true;

        if (allowAddToProject && project != null)
        {
            var item = AddFileToProject(project, target, ProjectRoot!);
            Safe(() => item?.Open("ViewCode"));
            if (WaitForActiveDocument(target))
                return true;
        }

        if (TryOpenViaDevenvEdit(target) && WaitForActiveDocument(target, 1200))
            return true;

        return IsActiveDocument(target);
    }

    private void TryDteOpenFile(string fullPath)
    {
        if (_dte == null) return;
        foreach (var view in new[] { "ViewCode", "TextView", "Primary" })
        {
            try
            {
                dynamic win = _dte.ItemOperations.OpenFile(fullPath, view);
                Safe(() => win.Activate());
                Safe(() => { win.Visible = true; });
                return;
            }
            catch { /* next view kind */ }
        }
    }

    private bool TryActivateViaWindows(string fullPath)
    {
        if (_dte == null) return false;
        dynamic? windows = Safe(() => _dte!.Windows);
        if (windows == null) return false;

        var count = GetCount(windows);
        for (var i = 1; i <= count; i++)
        {
            dynamic w = windows!.Item(i);
            var docName = Safe(() => (string)w.Document.FullName) ?? "";
            if (string.IsNullOrEmpty(docName) || !PathsEqual(docName, fullPath))
                continue;

            Safe(() => w.Activate());
            Safe(() => { w.Visible = true; });
            return true;
        }

        return false;
    }

    private static string? FindDevenvPath()
    {
        if (_devenvPath != null && File.Exists(_devenvPath))
            return _devenvPath;

        var vswhere = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (File.Exists(vswhere))
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo(vswhere,
                    "-latest -products * -requires Microsoft.Component.MSBuild -property installationPath")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                var dir = proc?.StandardOutput.ReadToEnd().Trim();
                proc?.WaitForExit(5000);
                if (!string.IsNullOrEmpty(dir))
                {
                    var path = Path.Combine(dir, "Common7", "IDE", "devenv.exe");
                    if (File.Exists(path))
                    {
                        _devenvPath = path;
                        return path;
                    }
                }
            }
            catch { /* ignore */ }
        }

        foreach (var year in new[] { "2026", "2025", "2022" })
        foreach (var edition in new[] { "Community", "Professional", "Enterprise" })
        {
            var path = Path.Combine("C:\\Program Files\\Microsoft Visual Studio", year, edition,
                "Common7", "IDE", "devenv.exe");
            if (File.Exists(path))
            {
                _devenvPath = path;
                return path;
            }
        }

        return null;
    }

    private bool TryOpenViaDevenvEdit(string fullPath)
    {
        var devenv = FindDevenvPath();
        if (devenv == null) return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = devenv,
                Arguments = $"/Edit \"{fullPath}\"",
                UseShellExecute = true
            });
            Thread.Sleep(200);
            TryConnect();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool WaitForActiveDocument(string fullPath, int maxMs = 500)
    {
        var deadline = Environment.TickCount64 + maxMs;
        while (Environment.TickCount64 < deadline)
        {
            if (IsActiveDocument(fullPath))
                return true;
            Thread.Sleep(40);
        }

        return IsActiveDocument(fullPath);
    }

    private string? GetActiveDocumentPath()
    {
        if (_dte == null) return null;
        return Safe(() => (string)_dte!.ActiveDocument.FullName);
    }

    private bool IsActiveDocument(string fullPath)
    {
        var active = GetActiveDocumentPath();
        if (string.IsNullOrEmpty(active)) return false;
        return PathsEqual(active, fullPath);
    }

    private bool TryActivateOpenDocument(string fullPath)
    {
        if (_dte == null) return false;

        dynamic? docs = Safe(() => _dte!.Documents);
        if (docs == null) return false;

        var count = GetCount(docs);
        for (var i = 1; i <= count; i++)
        {
            dynamic doc = docs!.Item(i);
            var name = Safe(() => (string)doc.FullName) ?? "";
            if (!PathsEqual(name, fullPath)) continue;

            Safe(() => doc.Activate());
            Safe(() =>
            {
                dynamic? window = doc.ActiveWindow;
                if (window != null)
                {
                    window.Activate();
                    window.Visible = true;
                }
            });
            return true;
        }

        return false;
    }

    private static dynamic? FindProjectItemByPath(dynamic project, string fullPath)
    {
        try
        {
            return FindProjectItemRecursive(project.ProjectItems, fullPath);
        }
        catch
        {
            return null;
        }
    }

    private static dynamic? FindProjectItemRecursive(dynamic items, string fullPath)
    {
        var count = GetCount(items);
        for (var i = 1; i <= count; i++)
        {
            dynamic item = items.Item(i);
            var fileName = Safe(() => (string)item.FileNames[1]) ?? "";
            if (!string.IsNullOrEmpty(fileName) && PathsEqual(fileName, fullPath))
                return item;

            int kind;
            try { kind = (int)item.Kind; }
            catch { continue; }

            if (kind is VsProjectItemKindPhysicalFolder or VsProjectItemKindVirtualFolder)
            {
                var subCount = GetCount(item.ProjectItems);
                if (subCount > 0)
                {
                    var found = FindProjectItemRecursive(item.ProjectItems, fullPath);
                    if (found != null) return found;
                }
            }
        }

        return null;
    }

    private static void EnsureFolderInProject(dynamic project, string folderPath)
    {
        dynamic container = project;
        foreach (var part in folderPath.Split('/', '\\'))
        {
            if (string.IsNullOrEmpty(part)) continue;
            container = FindOrCreateFolder(container, part);
        }
    }

    public bool InsertText(string text)
    {
        if (_dte == null || string.IsNullOrEmpty(text)) return false;

        try
        {
            dynamic? doc = Safe(() => _dte!.ActiveDocument);
            if (doc == null)
            {
                _status = "Откройте файл в Visual Studio (кнопка «Открыть файл в VS»)";
                return false;
            }

            dynamic selection = doc!.Selection;
            selection.Insert(text, 1);
            Safe(() => _dte!.MainWindow.Activate());
            return true;
        }
        catch (Exception ex)
        {
            _status = "Ошибка вставки: " + ex.Message;
            return false;
        }
    }

    public bool ReplaceActiveDocument(string content)
    {
        if (_dte == null) return false;

        try
        {
            ClearGhostHint();
            dynamic? doc = Safe(() => _dte!.ActiveDocument);
            if (doc == null) return false;

            dynamic selection = doc!.Selection;
            selection.SelectAll();
            selection.Text = content;
            Safe(() => selection.StartOfDocument(false));
            return true;
        }
        catch (Exception ex)
        {
            _status = "Ошибка замены: " + ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Режим «обводка»: серый текст-подсказка в VS, пользователь печатает поверх (Overtype).
    /// </summary>
    public bool ShowTraceHint(string hintText)
    {
        if (_dte == null || string.IsNullOrEmpty(hintText)) return false;

        try
        {
            dynamic? doc = Safe(() => _dte!.ActiveDocument);
            if (doc == null)
            {
                _status = "Откройте файл в Visual Studio";
                return false;
            }

            ClearGhostHint();

            dynamic selection = doc!.Selection;
            Safe(() => selection.EndOfDocument(false));

            var start = GetAbsoluteOffset(selection);
            selection.Insert(hintText, 1);
            var end = GetAbsoluteOffset(selection);

            ApplyGhostFormatting(selection, start, end);
            MoveToAbsoluteOffset(selection, start, false);
            EnableOvertypeMode(true);

            _ghost = new GhostHintState
            {
                Active = true,
                StartOffset = start,
                Length = hintText.Length,
                Text = hintText
            };

            Safe(() => _dte!.MainWindow.Activate());
            _status = "Серым в VS — печатайте поверх (OVR). F8 — следующий фрагмент.";
            return true;
        }
        catch (Exception ex)
        {
            _status = "Ошибка подсказки: " + ex.Message;
            return false;
        }
    }

    public void ClearGhostHint()
    {
        if (!_ghost.Active || _dte == null) return;

        try
        {
            dynamic? doc = Safe(() => _dte!.ActiveDocument);
            if (doc == null) return;

            dynamic selection = doc!.Selection;
            var current = ReadRangeText(selection, _ghost.StartOffset, _ghost.Length);

            // Удаляем только если подсказку ещё не переписали
            if (current == _ghost.Text)
            {
                MoveToAbsoluteOffset(selection, _ghost.StartOffset, false);
                MoveToAbsoluteOffset(selection, _ghost.StartOffset + _ghost.Length, true);
                selection.Text = "";
            }
        }
        catch { /* document changed */ }
        finally
        {
            _ghost.Active = false;
            EnableOvertypeMode(false);
        }
    }

    private void EnableOvertypeMode(bool enable)
    {
        if (_dte == null) return;
        try
        {
            dynamic? doc = Safe(() => _dte!.ActiveDocument);
            if (doc == null) return;
            dynamic selection = doc!.Selection;
            var mode = GetSelectionMode(selection);

            if (enable)
            {
                _overtypeWasEnabled = mode == VsOverwriteMode;
                if (mode != VsOverwriteMode)
                    Safe(() => _dte!.ExecuteCommand("Edit.Overtype"));
            }
            else if (!_overtypeWasEnabled && mode == VsOverwriteMode)
            {
                Safe(() => _dte!.ExecuteCommand("Edit.Overtype"));
            }
        }
        catch { /* ignore */ }
    }

    private static int GetSelectionMode(dynamic selection)
    {
        try { return (int)selection.Mode; }
        catch { return 0; }
    }

    private static int GetAbsoluteOffset(dynamic selection)
    {
        try { return (int)selection.ActivePoint.AbsoluteCharOffset; }
        catch { return 0; }
    }

    private static void MoveToAbsoluteOffset(dynamic selection, int offset, bool extend)
    {
        try
        {
            selection.MoveToAbsoluteOffset(offset, extend);
            return;
        }
        catch { /* older DTE */ }

        Safe(() => selection.StartOfDocument(false));
        var pos = GetAbsoluteOffset(selection);
        var delta = offset - pos;
        if (delta > 0)
            selection.CharRight(extend, delta);
        else if (delta < 0)
            selection.CharLeft(extend, -delta);
    }

    private static void ApplyGhostFormatting(dynamic selection, int start, int end)
    {
        MoveToAbsoluteOffset(selection, start, false);
        MoveToAbsoluteOffset(selection, end, true);
        try
        {
            selection.Font.Color = GhostFontColor;
            selection.Font.Bold = false;
            selection.Font.Italic = false;
        }
        catch { /* theme may ignore */ }
        MoveToAbsoluteOffset(selection, start, false);
    }

    private static string ReadRangeText(dynamic selection, int start, int length)
    {
        if (length <= 0) return "";
        MoveToAbsoluteOffset(selection, start, false);
        MoveToAbsoluteOffset(selection, start + length, true);
        return Safe(() => (string)selection.Text) ?? "";
    }

    private dynamic? FindWebProject()
    {
        if (_dte == null) return null;

        dynamic? solution = Safe(() => _dte!.Solution);
        if (solution == null) return null;

        dynamic? best = null;
        CollectProjects(solution.Projects, ref best);
        return best;
    }

    private static int GetCount(dynamic items)
    {
        try { return (int)items.Count; }
        catch { return 0; }
    }

    private void CollectProjects(dynamic projects, ref dynamic? best)
    {
        var count = GetCount(projects);
        for (var i = 1; i <= count; i++)
        {
            dynamic project = projects.Item(i);
            var fullName = Safe(() => (string)project.FullName) ?? "";

            if (fullName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                var projDir = Path.GetDirectoryName(fullName)!;

                if (_manualProjectRoot != null)
                {
                    if (PathsEqual(projDir, _manualProjectRoot))
                    {
                        best = project;
                        return;
                    }
                }
                else if (IsWebProject(fullName))
                {
                    best = project;
                    return;
                }

                best ??= project;
            }

            try
            {
                dynamic sub = project.ProjectItems;
                if (sub != null && GetCount(sub) > 0)
                {
                    // solution folders
                }
            }
            catch { /* not a real project */ }

            try
            {
                dynamic subs = project.SubProjects;
                var subCount = GetCount(subs);
                for (var j = 1; j <= subCount; j++)
                    CollectProjects(subs.Item(j), ref best);
            }
            catch { /* no subprojects */ }
        }
    }

    private static bool IsWebProject(string csprojPath)
    {
        try
        {
            var text = File.ReadAllText(csprojPath);
            return text.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private dynamic? AddFileToProject(dynamic project, string fullPath, string projectRoot)
    {
        try
        {
            var relative = Path.GetRelativePath(projectRoot, fullPath);
            var folderPart = Path.GetDirectoryName(relative);
            dynamic container = project;

            if (!string.IsNullOrEmpty(folderPart))
            {
                foreach (var part in folderPart.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                {
                    if (string.IsNullOrEmpty(part)) continue;
                    container = FindOrCreateFolder(container, part);
                }
            }

            return container.ProjectItems.AddFromFile(fullPath);
        }
        catch
        {
            try { return project.ProjectItems.AddFromFile(fullPath); }
            catch { return null; }
        }
    }

    private static dynamic FindOrCreateFolder(dynamic parent, string folderName)
    {
        var count = GetCount(parent.ProjectItems);
        for (var i = 1; i <= count; i++)
        {
            dynamic item = parent.ProjectItems.Item(i);
            var name = Safe(() => (string)item.Name) ?? "";
            if (!name.Equals(folderName, StringComparison.OrdinalIgnoreCase)) continue;

            int kind;
            try { kind = (int)item.Kind; }
            catch { continue; }

            if (kind is VsProjectItemKindPhysicalFolder or VsProjectItemKindVirtualFolder)
                return item;
        }

        return parent.ProjectItems.AddFolder(folderName);
    }

    private static string NormalizePath(string path)
    {
        try { return Path.GetFullPath(path).TrimEnd('\\', '/'); }
        catch { return path.Trim(); }
    }

    private static bool PathsEqual(string a, string b)
    {
        try
        {
            return string.Equals(NormalizePath(a), NormalizePath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static T? Safe<T>(Func<T> action)
    {
        try { return action(); }
        catch { return default; }
    }

    private static void Safe(Action action)
    {
        try { action(); }
        catch { /* COM */ }
    }
}
