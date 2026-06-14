using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ExamCoachDesktop;

public partial class MainWindow : Window
{
    private static readonly Dictionary<string, string> PhaseRu = new()
    {
        ["0. Start"] = "0. Старт проекта",
        ["1. Models"] = "1. Модели (Модуль 1 — БД)",
        ["2. Database"] = "2. База данных",
        ["3. Services"] = "3. Сервисы",
        ["4. ViewModels"] = "4. ViewModels",
        ["5. Program"] = "5. Program.cs",
        ["6. Controllers"] = "6. Контроллеры (Модуль 2–4)",
        ["7. Views"] = "7. Представления (HTML)",
        ["8. Static"] = "8. CSS и JavaScript"
    };

    private readonly string _statePath;
    private readonly string _baseDataPath;
    private readonly string _customDataPath;
    private readonly string _coachConfigPath;
    private readonly string _assignmentProfilePath;
    private CoachData _baseData = new();
    private AssignmentProfile? _assignmentProfile;
    private bool _uiReady;
    private CoachData _data = new();
    private int _current;
    private string _mode = "line";
    private List<string> _fragments = [];
    private int _fragmentIndex;
    private HashSet<string> _done = [];
    private Dictionary<string, TimerSnapshot> _timers = [];
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly VisualStudioBridge _vs = new();
    private GlobalHotkeyService? _globalHotkeys;
    private readonly List<TimerCardView> _timerViews = [];
    private bool _timerUiBuilt;

    public MainWindow()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dataPath = Path.Combine(baseDir, "steps-data.json");
        _baseDataPath = dataPath;
        _customDataPath = Path.Combine(baseDir, "steps-data-custom.json");
        _coachConfigPath = Path.Combine(baseDir, "coach-config.json");
        _assignmentProfilePath = Path.Combine(baseDir, "examcoach-assignment.json");
        _statePath = Path.Combine(baseDir, "examcoach-state.json");

        InitializeComponent();
        Topmost = true;

        if (!File.Exists(dataPath))
        {
            MessageBox.Show(
                "Файл steps-data.json не найден.\n\nСначала выполните:\ncd ExamCoach\npowershell -File .\\Generate-ExamCoach.ps1 -UpdateManifest",
                "ExamCoach", MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
            return;
        }

        _baseData = CoachLoader.Load(dataPath);
        LoadAssignmentProfile();
        _data = LoadActiveCoachData();
        if (_assignmentProfile?.ExamVariant != null || !string.IsNullOrWhiteSpace(_assignmentProfile?.SourceText))
            CoachDataSerializer.Save(_data, _customDataPath);
        UpdateAssignmentSourceLabel();
        LoadState();
        PopulateStepList();
        _clock.Tick += (_, _) => RefreshTimers();
        _clock.Start();
        KeyDown += MainWindow_KeyDown;

        TryConnectVisualStudio();
        UpdateProjectRootDisplay();

        if (_data.Steps.Count > 0)
        {
            StepList.SelectedIndex = 0;
        }

        _uiReady = true;
    }

    private CoachData LoadActiveCoachData()
    {
        if (_assignmentProfile != null &&
            (!string.IsNullOrEmpty(_assignmentProfile.ExamVariant) ||
             !string.IsNullOrWhiteSpace(_assignmentProfile.SourceText)))
            return AssignmentAdaptEngine.Adapt(_baseData, _assignmentProfile);

        if (File.Exists(_customDataPath))
            return CoachLoader.Load(_customDataPath);

        return CoachDataSerializer.Clone(_baseData);
    }

    private void LoadAssignmentProfile()
    {
        if (!File.Exists(_assignmentProfilePath)) return;
        try
        {
            _assignmentProfile = JsonSerializer.Deserialize<AssignmentProfile>(File.ReadAllText(_assignmentProfilePath));
        }
        catch { /* ignore */ }
    }

    private void SaveAssignmentProfile()
    {
        if (_assignmentProfile == null)
        {
            if (File.Exists(_assignmentProfilePath)) File.Delete(_assignmentProfilePath);
            return;
        }
        File.WriteAllText(_assignmentProfilePath, JsonSerializer.Serialize(_assignmentProfile, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void UpdateAssignmentSourceLabel()
    {
        if (File.Exists(_customDataPath))
        {
            if (_assignmentProfile?.ExamVariant is { } variant)
            {
                var domain = _assignmentProfile.DomainDescription ?? "магазин";
                AssignmentSourceText.Text = $"Задание: {variant} — {domain} (KodShopWeb)";
            }
            else
            {
                var title = _assignmentProfile?.AssignmentTitle ?? "кастомное задание";
                AssignmentSourceText.Text = $"Задание: {title} (адаптированный код)";
            }
        }
        else
        {
            AssignmentSourceText.Text = "Задание: KodShopWeb (эталон)";
        }
    }

    private void ReloadCoachData(CoachData data, bool saveCustom)
    {
        _data = data;
        _current = 0;
        _fragmentIndex = 0;
        _timerUiBuilt = false;
        if (saveCustom)
            CoachDataSerializer.Save(_data, _customDataPath);
        else if (File.Exists(_customDataPath))
            File.Delete(_customDataPath);

        PopulateStepList();
        UpdateAssignmentSourceLabel();
        if (_data.Steps.Count > 0)
        {
            StepList.SelectedIndex = 0;
        }
        else
        {
            RenderStep();
        }
    }

    private void ImportAssignment_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ImportAssignmentWindow(_baseData, _coachConfigPath, _assignmentProfile)
        {
            Owner = this
        };
        if (dlg.ShowDialog() != true) return;

        if (dlg.ResetToBase)
        {
            _assignmentProfile = null;
            SaveAssignmentProfile();
            ReloadCoachData(CoachDataSerializer.Clone(_baseData), saveCustom: false);
            MessageBox.Show("Восстановлен эталон KodShopWeb.", "Импорт", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (dlg.ResultData == null) return;
        _assignmentProfile = dlg.ResultProfile;
        SaveAssignmentProfile();
        ReloadCoachData(dlg.ResultData, saveCustom: true);
        var summary = dlg.ResultProfile?.AdaptationSummary
            ?? (dlg.ResultProfile?.ExamVariant is { } v
                ? $"{v} — {dlg.ResultProfile.DomainDescription}"
                : $"Замен: {dlg.ResultProfile?.Replacements.Count ?? 0}");
        MessageBox.Show(
            $"Шагов в плане: {_data.Steps.Count}\n{summary}",
            "Задание применено", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _globalHotkeys = new GlobalHotkeyService();
        _globalHotkeys.HotkeyPressed += OnGlobalHotkey;
        _globalHotkeys.Attach(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        _globalHotkeys?.Dispose();
        _globalHotkeys = null;
        base.OnClosed(e);
    }

    private void OnGlobalHotkey(GlobalHotkeyAction action)
    {
        if (!_uiReady) return;

        Dispatcher.Invoke(() =>
        {
            switch (action)
            {
                case GlobalHotkeyAction.NextFragment:
                    Reveal(1);
                    break;
                case GlobalHotkeyAction.RevealTen:
                    Reveal(10);
                    break;
                case GlobalHotkeyAction.RevealToVs:
                    RevealToVs_Click(this, new RoutedEventArgs());
                    break;
                case GlobalHotkeyAction.PrevStep:
                    Prev_Click(this, new RoutedEventArgs());
                    break;
                case GlobalHotkeyAction.NextStep:
                    Next_Click(this, new RoutedEventArgs());
                    break;
            }
        });
    }

    private void TryConnectVisualStudio()
    {
        _vs.TryConnect();
        _vs.RefreshProjectInfo();
        VsConnectionText.Text = _vs.Status;
        UpdateProjectRootDisplay();
    }

    private void UpdateProjectRootDisplay()
    {
        var root = _vs.ProjectRoot;
        if (root == null)
        {
            ProjectRootText.Text = "Папка не выбрана — создайте новый MVC в VS и нажмите «Из VS»";
            return;
        }

        var source = _vs.ManualProjectRoot != null ? "вручную" : "авто из VS";
        ProjectRootText.Text = $"{root}\n({source})";
    }

    private void DetectProject_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureVsConnected()) return;
        _vs.SetManualProjectRoot(null);
        _vs.RefreshProjectInfo();
        VsConnectionText.Text = _vs.Status;
        UpdateProjectRootDisplay();
        SaveState();

        if (_vs.ProjectRoot == null)
        {
            MessageBox.Show(
                "Не найден MVC-проект в открытом решении.\n\nСоздайте: File → New → Project → ASP.NET Core Web App (MVC), .NET 8.",
                "ExamCoach", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BrowseProject_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Папка нового MVC-проекта (где лежит .csproj)"
        };
        if (!string.IsNullOrEmpty(_vs.ProjectRoot))
            dlg.InitialDirectory = _vs.ProjectRoot;

        if (dlg.ShowDialog() != true) return;

        _vs.SetManualProjectRoot(dlg.FolderName);
        VsConnectionText.Text = _vs.Status;
        UpdateProjectRootDisplay();
        SaveState();
    }

    private bool EnsureVsConnected()
    {
        if (_vs.IsConnected) return true;
        if (_vs.TryConnect())
        {
            VsConnectionText.Text = _vs.Status;
            return true;
        }
        VsConnectionText.Text = _vs.Status;
        MessageBox.Show(_vs.Status, "Visual Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
    }

    private string? GetStepFilePath()
    {
        if (_current >= _data.Steps.Count) return null;
        var step = _data.Steps[_current];
        if (string.IsNullOrWhiteSpace(step.Code)) return null;
        return _vs.ResolveStepPath(step.Title.Replace('\\', '/'));
    }

    private bool TryEnsureVsConnectedSilent()
    {
        if (_vs.IsConnected) return true;
        if (_vs.TryConnect())
        {
            VsConnectionText.Text = _vs.Status;
            return true;
        }
        VsConnectionText.Text = "VS не подключён";
        return false;
    }

    private static bool FileExistsOnDisk(string? projectRoot, string relativePath) =>
        projectRoot != null &&
        File.Exists(Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private void OpenCurrentStepInVs()
    {
        if (!EnsureVsConnected()) return;
        var path = GetStepFilePath();
        if (path == null)
        {
            MessageBox.Show("Для этого шага нет файла — выполните действия в VS вручную.", "ExamCoach",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = FileExistsOnDisk(_vs.ProjectRoot, path)
            ? _vs.OpenStepFileInternal(path)
            : _vs.EnsureFile(path, openInEditor: true, createIfMissing: AutoScaffoldCheck.IsChecked == true);

        if (result.Success)
            VsConnectionText.Text = _vs.Status;
        else if (result.Error != null)
            VsConnectionText.Text = result.Error;
    }

    private IEnumerable<string> GetAllStepFilePaths() =>
        _data.Steps
            .Where(s => !string.IsNullOrWhiteSpace(s.Code))
            .Select(s => _vs.ResolveStepPath(s.Title.Replace('\\', '/')));

    private void PrepareCurrentStepInVs()
    {
        if (!_uiReady || _current >= _data.Steps.Count) return;

        var openInEditor = AutoOpenFileCheck.IsChecked == true;
        var scaffold = AutoScaffoldCheck.IsChecked == true;
        if (!openInEditor && !scaffold) return;
        if (!TryEnsureVsConnectedSilent()) return;

        var step = _data.Steps[_current];

        if (scaffold && step.Id == "setup-3")
        {
            var paths = GetAllStepFilePaths().ToList();
            var summary = _vs.EnsureFolderStructure(ProjectScaffolder.GetFolders(paths));
            VsConnectionText.Text = "Папки: " + summary.Message;
            return;
        }

        var path = GetStepFilePath();
        if (path == null) return;

        FileEnsureResult result;
        if (FileExistsOnDisk(_vs.ProjectRoot, path))
            result = _vs.OpenStepFileInternal(path, activateVs: openInEditor);
        else if (scaffold)
            result = _vs.EnsureFile(path, openInEditor: openInEditor, createIfMissing: true);
        else
        {
            if (openInEditor)
                VsConnectionText.Text = $"Файл ещё не создан: {path}";
            return;
        }

        if (result.Success)
            VsConnectionText.Text = _vs.Status;
        else if (result.Error != null)
            VsConnectionText.Text = result.Error;
    }

    private void AutoOpenVs_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady) return;
        SaveState();
    }

    private void ScaffoldAll_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureVsConnected()) return;
        if (_vs.ProjectRoot == null)
        {
            MessageBox.Show("Сначала укажите проект: «Из VS» или «Папка проекта».", "ExamCoach",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var summary = _vs.ScaffoldAllFiles(GetAllStepFilePaths().ToList());
        VsConnectionText.Text = _vs.Status;
        MessageBox.Show(summary.Message + "\n\nПапки и пустые файлы добавлены в проект VS.",
            "Структура создана", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void InsertRevealedToVs(int fromIndex, int toIndex)
    {
        if (toIndex <= fromIndex) return;
        if (!EnsureVsConnected()) return;

        var chunk = FragmentBuilder.Join(_fragments.Skip(fromIndex).Take(toIndex - fromIndex), _mode);
        if (string.IsNullOrEmpty(chunk)) return;

        if (TraceModeCheck.IsChecked == true)
        {
            OpenCurrentStepInVs();
            if (_vs.ShowTraceHint(chunk))
                VsConnectionText.Text = _vs.Status;
            return;
        }

        if (InsertToVsCheck.IsChecked != true) return;

        if (_vs.InsertText(chunk))
            VsConnectionText.Text = "Вставлено в Visual Studio";
    }

    private void VsInsertMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady) return;

        if (TraceModeCheck.IsChecked == true && InsertToVsCheck.IsChecked == true)
        {
            if (sender == TraceModeCheck)
                InsertToVsCheck.IsChecked = false;
            else
                TraceModeCheck.IsChecked = false;
        }
        UpdateCodeView();
        SaveState();
    }

    private void LoadState()
    {
        if (!File.Exists(_statePath)) return;
        try
        {
            var state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(_statePath));
            if (state == null) return;
            _done = state.Done?.ToHashSet() ?? [];
            _timers = state.Timers ?? new Dictionary<string, TimerSnapshot>();
            if (!string.IsNullOrWhiteSpace(state.TargetProjectRoot))
                _vs.SetManualProjectRoot(state.TargetProjectRoot);
            if (state.TraceMode == true)
            {
                TraceModeCheck.IsChecked = true;
                InsertToVsCheck.IsChecked = false;
            }
            else if (state.InsertToVs == true)
            {
                InsertToVsCheck.IsChecked = true;
                TraceModeCheck.IsChecked = false;
            }
            if (state.AutoScaffold == false)
                AutoScaffoldCheck.IsChecked = false;
            if (state.AutoOpenFile == false)
                AutoOpenFileCheck.IsChecked = false;
        }
        catch
        {
            // ignore corrupt state
        }
    }

    private void SaveState()
    {
        if (!_uiReady || string.IsNullOrEmpty(_statePath)) return;
        var state = new AppState
        {
            Done = _done.ToList(),
            Timers = _timers,
            TargetProjectRoot = _vs.ManualProjectRoot,
            TraceMode = TraceModeCheck.IsChecked == true,
            InsertToVs = InsertToVsCheck.IsChecked == true,
            AutoScaffold = AutoScaffoldCheck.IsChecked == true,
            AutoOpenFile = AutoOpenFileCheck.IsChecked == true
        };
        File.WriteAllText(_statePath, JsonSerializer.Serialize(state));
    }

    private void PopulateStepList()
    {
        StepList.Items.Clear();
        for (var i = 0; i < _data.Steps.Count; i++)
        {
            var step = _data.Steps[i];
            var item = new ListBoxItem
            {
                Content = $"{i + 1}. {step.Title}",
                Tag = i,
                Foreground = _done.Contains(step.Id)
                    ? new SolidColorBrush(Color.FromRgb(148, 163, 184))
                    : (Brush)FindResource("TextBrush")
            };
            if (_done.Contains(step.Id))
            {
                item.FontStyle = FontStyles.Italic;
                item.Content = $"✓ {item.Content}";
            }
            StepList.Items.Add(item);
        }
        ProgressText.Text = $"{_done.Count} готово / {_data.Steps.Count} шагов";
    }

    private CoachModule? GetModule(string id) => _data.Modules.FirstOrDefault(m => m.Id == id);

    private int GetRemainingSec(CoachModule module)
    {
        _timers.TryGetValue(module.Id, out var st);
        st ??= new TimerSnapshot { RemainingSec = module.Minutes * 60 };
        if (!st.Running) return st.RemainingSec;
        var elapsed = (int)(DateTime.UtcNow - st.StartedAt!.Value).TotalSeconds;
        return st.RemainingSec - elapsed;
    }

    private static string FormatTime(int totalSec)
    {
        var sign = totalSec < 0 ? "-" : "";
        var abs = Math.Abs(totalSec);
        var h = abs / 3600;
        var m = abs % 3600 / 60;
        var s = abs % 60;
        return h > 0
            ? $"{sign}{h}:{m:D2}:{s:D2}"
            : $"{sign}{m:D2}:{s:D2}";
    }

    private void RebuildTimerUi()
    {
        _timerViews.Clear();
        TimerItems.Items.Clear();

        foreach (var module in _data.Modules)
        {
            var timeText = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var descText = new TextBlock
            {
                Foreground = (Brush)FindResource("MutedBrush"),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 4),
                TextWrapping = TextWrapping.Wrap
            };

            var head = new DockPanel { LastChildFill = false };
            head.Children.Add(new TextBlock
            {
                Text = module.Title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12
            });
            DockPanel.SetDock(head.Children[0], Dock.Left);
            head.Children.Add(timeText);
            DockPanel.SetDock(timeText, Dock.Right);

            var panel = new StackPanel();
            panel.Children.Add(head);
            panel.Children.Add(descText);

            var actions = new WrapPanel();
            var startBtn = new Button { Content = "▶ Старт", Tag = module.Id, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 4, 0) };
            var pauseBtn = new Button { Content = "⏸ Пауза", Tag = module.Id, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 4, 0) };
            var resetBtn = new Button { Content = "↺", Tag = module.Id, Padding = new Thickness(6, 2, 6, 2) };
            startBtn.Click += (_, _) => StartTimer(module.Id);
            pauseBtn.Click += (_, _) => PauseTimer(module.Id);
            resetBtn.Click += (_, _) => ResetTimer(module.Id);
            actions.Children.Add(startBtn);
            actions.Children.Add(pauseBtn);
            actions.Children.Add(resetBtn);
            panel.Children.Add(actions);

            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 6),
                BorderThickness = new Thickness(3, 0, 0, 0),
                Child = panel
            };

            _timerViews.Add(new TimerCardView(module.Id, border, timeText, descText));
            TimerItems.Items.Add(border);
        }

        _timerUiBuilt = true;
    }

    private void RefreshTimers()
    {
        if (!_timerUiBuilt || _timerViews.Count != _data.Modules.Count)
            RebuildTimerUi();

        var currentModule = _current < _data.Steps.Count ? _data.Steps[_current].Module : "";
        var panelBrush = (Brush)FindResource("PanelBrush");
        var accentBrush = (Brush)FindResource("AccentBrush");

        foreach (var view in _timerViews)
        {
            var module = GetModule(view.ModuleId);
            if (module == null) continue;

            var left = GetRemainingSec(module);
            var total = module.Minutes * 60;
            var doneCount = module.StepIndices.Count(i => i < _data.Steps.Count && _done.Contains(_data.Steps[i].Id));

            view.TimeText.Text = FormatTime(left);
            view.DescText.Text = $"{module.Description} · {doneCount}/{module.StepIndices.Count} шагов";

            view.Border.Background = module.Id == currentModule
                ? new SolidColorBrush(Color.FromArgb(40, 20, 83, 45))
                : panelBrush;
            view.Border.BorderBrush = left <= 0
                ? Brushes.IndianRed
                : left <= total * 0.15
                    ? Brushes.Goldenrod
                    : accentBrush;
        }
    }

    private void StartTimer(string moduleId)
    {
        var module = GetModule(moduleId);
        if (module == null) return;

        _timers.TryGetValue(moduleId, out var st);
        var remaining = st?.RemainingSec ?? module.Minutes * 60;
        if (remaining <= 0) return;

        _timers[moduleId] = new TimerSnapshot
        {
            Running = true,
            StartedAt = DateTime.UtcNow,
            RemainingSec = remaining
        };
        SaveState();
        RefreshTimers();
    }

    private void PauseTimer(string moduleId)
    {
        var module = GetModule(moduleId);
        if (module == null) return;

        if (!_timers.TryGetValue(moduleId, out var st) || !st.Running) return;
        _timers[moduleId] = new TimerSnapshot
        {
            Running = false,
            RemainingSec = GetRemainingSec(module)
        };
        SaveState();
        RefreshTimers();
    }

    private void ResetTimer(string moduleId)
    {
        var module = GetModule(moduleId);
        if (module == null) return;
        _timers[moduleId] = new TimerSnapshot { Running = false, RemainingSec = module.Minutes * 60 };
        SaveState();
        RefreshTimers();
    }

    private void ResetFragments()
    {
        if (_current >= _data.Steps.Count) return;
        _fragments = FragmentBuilder.Build(_data.Steps[_current].Code, _mode);
        _fragmentIndex = 0;
        UpdateCodeView();
    }

    private void UpdateCodeView()
    {
        if (_current >= _data.Steps.Count) return;
        var step = _data.Steps[_current];

        CodeView.Inlines.Clear();
        var typed = FragmentBuilder.Join(_fragments.Take(_fragmentIndex), _mode);
        var pending = FragmentBuilder.Join(_fragments.Skip(_fragmentIndex), _mode);

        CodeView.Inlines.Add(new Run(typed) { Foreground = (Brush)FindResource("TypedBrush") });
        CodeView.Inlines.Add(new Run(pending) { Foreground = (Brush)FindResource("PendingBrush") });

        FragmentInfo.Text = !string.IsNullOrEmpty(step.Code)
            ? $"Фрагмент {_fragmentIndex} / {_fragments.Count} ({_mode}). F8 — следующий; после последнего — переход к следующему шагу и файл в VS." +
              (TraceModeCheck.IsChecked == true ? " Пишите в VS, Пробел — как обычно." :
               InsertToVsCheck.IsChecked == true ? " + вставка в VS." : "")
            : "Код не нужен — выполните действия в Visual Studio.";
    }

    private void RenderStep()
    {
        if (_current >= _data.Steps.Count) return;
        var step = _data.Steps[_current];

        StepTitle.Text = step.Title;
        PhaseBadge.Text = PhaseRu.GetValueOrDefault(step.Phase, step.Phase);
        ModuleBadge.Text = GetModule(step.Module)?.Title ?? step.Module;
        VsHintText.Text = step.VsHint;

        if (!string.IsNullOrWhiteSpace(step.Terminal))
        {
            TerminalBox.Visibility = Visibility.Visible;
            TerminalBox.Text = step.Terminal.Trim();
        }
        else
        {
            TerminalBox.Visibility = Visibility.Collapsed;
        }

        ResetFragments();
        _vs.ClearGhostHint();

        PrepareCurrentStepInVs();

        RefreshTimers();
    }

    private void StepList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StepList.SelectedItem is not ListBoxItem item || item.Tag is not int index) return;
        _current = index;
        RenderStep();
    }

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        if (_current <= 0) return;
        StepList.SelectedIndex = _current - 1;
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_current >= _data.Steps.Count - 1) return;
        StepList.SelectedIndex = _current + 1;
    }

    private void MarkDone_Click(object sender, RoutedEventArgs e) =>
        AdvanceToNextStep(markCurrentDone: true);

    private void AdvanceToNextStep(bool markCurrentDone)
    {
        if (_current >= _data.Steps.Count) return;

        if (markCurrentDone)
        {
            _done.Add(_data.Steps[_current].Id);
            SaveState();
            PopulateStepList();
        }

        if (_current < _data.Steps.Count - 1)
            StepList.SelectedIndex = _current + 1;
        else if (markCurrentDone)
            StepList.SelectedIndex = _current;

        RefreshTimers();
    }

    private void Reveal_Click(object sender, RoutedEventArgs e) => Reveal(1);
    private void Reveal10_Click(object sender, RoutedEventArgs e) => Reveal(10);
    private void RevealToVs_Click(object sender, RoutedEventArgs e)
    {
        OpenCurrentStepInVs();
        Reveal(1);
        _vs.ActivateVisualStudio();
    }

    private void Reveal(int count)
    {
        var oldIndex = _fragmentIndex;
        _fragmentIndex = Math.Min(_fragmentIndex + count, _fragments.Count);
        InsertRevealedToVs(oldIndex, _fragmentIndex);
        UpdateCodeView();

        if (_fragments.Count > 0 &&
            oldIndex < _fragments.Count &&
            _fragmentIndex >= _fragments.Count)
        {
            AdvanceToNextStep(markCurrentDone: true);
        }
    }

    private void ConnectVs_Click(object sender, RoutedEventArgs e) => TryConnectVisualStudio();

    private void OpenInVs_Click(object sender, RoutedEventArgs e) => OpenCurrentStepInVs();

    private void SyncToVs_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureVsConnected()) return;
        OpenCurrentStepInVs();
        var typed = FragmentBuilder.Join(_fragments.Take(_fragmentIndex), _mode);
        if (_vs.ReplaceActiveDocument(typed))
        {
            VsConnectionText.Text = "Документ VS синхронизирован";
            _vs.ActivateVisualStudio();
        }
        else
        {
            VsConnectionText.Text = _vs.Status;
        }
    }

    private void ModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _mode = tag;
            ResetFragments();
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_current >= _data.Steps.Count) return;
        Clipboard.SetText(_data.Steps[_current].Code);
    }

    private void TopmostCheck_Changed(object sender, RoutedEventArgs e) =>
        Topmost = TopmostCheck.IsChecked == true;

    private void ResetAllTimers_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Сбросить все таймеры модулей?", "ExamCoach", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;
        foreach (var m in _data.Modules) ResetTimer(m.Id);
    }

    private void ResetProgress_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Сбросить отметки готовых шагов?", "ExamCoach", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;
        _done.Clear();
        SaveState();
        PopulateStepList();
        RefreshTimers();
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        // F8/F7/F9/Ctrl+Right — глобально через GlobalHotkeyService (работает и когда окно свёрнуто)
        if (e.Key == Key.Right && Keyboard.Modifiers == ModifierKeys.None)
        {
            Reveal(1);
            e.Handled = true;
        }
    }

    private sealed class AppState
    {
        public List<string>? Done { get; set; }
        public Dictionary<string, TimerSnapshot>? Timers { get; set; }
        public string? TargetProjectRoot { get; set; }
        public bool? TraceMode { get; set; }
        public bool? InsertToVs { get; set; }
        public bool? AutoScaffold { get; set; }
        public bool? AutoOpenFile { get; set; }
    }

    private sealed class TimerCardView(string moduleId, Border border, TextBlock timeText, TextBlock descText)
    {
        public string ModuleId { get; } = moduleId;
        public Border Border { get; } = border;
        public TextBlock TimeText { get; } = timeText;
        public TextBlock DescText { get; } = descText;
    }
}
