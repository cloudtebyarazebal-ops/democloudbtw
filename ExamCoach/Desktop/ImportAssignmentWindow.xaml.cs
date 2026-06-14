using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace ExamCoachDesktop;

public partial class ImportAssignmentWindow : Window
{
    private readonly CoachData _baseData;
    private readonly string _coachConfigPath;
    public CoachData? ResultData { get; private set; }
    public AssignmentProfile? ResultProfile { get; private set; }
    public bool ResetToBase { get; private set; }

    public ObservableCollection<TextReplacement> Replacements { get; } = [];

    public ImportAssignmentWindow(CoachData baseData, string coachConfigPath, AssignmentProfile? existing = null)
    {
        _baseData = baseData;
        _coachConfigPath = coachConfigPath;
        InitializeComponent();

        if (existing != null)
        {
            AssignmentTextBox.Text = existing.SourceText ?? "";
            ReferencePathBox.Text = existing.ReferenceProjectPath ?? "";
            foreach (var r in existing.Replacements)
                Replacements.Add(new TextReplacement { From = r.From, To = r.To });

            if (!string.IsNullOrWhiteSpace(existing.SourceText))
            {
                LoadStatusText.Text = existing.ExamVariant != null
                    ? AssignmentTextParser.DescribeExam(existing)
                    : $"Текст задания: {existing.SourceText.Length:N0} символов";
            }
        }

        ReplacementsGrid.ItemsSource = Replacements;
    }

    private async void LoadTextFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "PDF и текст|*.pdf;*.txt;*.md|PDF|*.pdf|Текст|*.txt;*.md|Все|*.*",
            Title = "Файл с текстом задания (PDF или TXT)",
            InitialDirectory = GetInitialBrowseDirectory()
        };

        if (!ShowFileDialogAboveWindow(dlg)) return;

        await LoadAssignmentFileAsync(dlg.FileName);
    }

    private async Task LoadAssignmentFileAsync(string path)
    {
        LoadTextFileButton.IsEnabled = false;
        LoadStatusText.Text = "Чтение файла…";
        Mouse.OverrideCursor = Cursors.Wait;

        try
        {
            var text = await Task.Run(() => AssignmentDocumentReader.ReadFile(path));
            AssignmentTextBox.Text = text;
            AssignmentTextBox.CaretIndex = 0;
            AssignmentTextBox.ScrollToHome();
            LoadStatusText.Text = $"Загружено {text.Length:N0} символов из {Path.GetFileName(path)}";
            ApplyParsedProfile(AssignmentTextParser.Parse(text, Replacements.ToList()), notifyIfEmpty: true);
        }
        catch (Exception ex)
        {
            LoadStatusText.Text = "";
            MessageBox.Show(this, ex.Message, "Не удалось прочитать файл", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            LoadTextFileButton.IsEnabled = true;
            Activate();
        }
    }

    /// <summary>Диалог выбора файла поверх модального окна (MainWindow с Topmost иначе его перекрывает).</summary>
    private bool ShowFileDialogAboveWindow(OpenFileDialog dlg)
    {
        bool? mainTopmost = null;
        if (Owner is Window owner)
        {
            mainTopmost = owner.Topmost;
            owner.Topmost = false;
        }

        var selfTopmost = Topmost;
        Topmost = false;

        try
        {
            return dlg.ShowDialog() == true;
        }
        finally
        {
            Topmost = selfTopmost;
            if (Owner is Window ownerRestore && mainTopmost.HasValue)
                ownerRestore.Topmost = mainTopmost.Value;
        }
    }

    private static string? GetInitialBrowseDirectory()
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var telegram = Path.Combine(downloads, "Telegram Desktop");
        if (Directory.Exists(telegram)) return telegram;
        if (Directory.Exists(downloads)) return downloads;
        return null;
    }

    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
        var text = AssignmentTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            MessageBox.Show(this, "Вставьте или загрузите текст задания.", "Анализ",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ApplyParsedProfile(AssignmentTextParser.Parse(text, Replacements.ToList()), notifyIfEmpty: true);
    }

    private void ApplyParsedProfile(AssignmentProfile profile, bool notifyIfEmpty)
    {
        var text = AssignmentTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        var preview = AssignmentTextParser.Parse(text);
        AssignmentAdaptEngine.Adapt(CoachDataSerializer.Clone(_baseData), preview);

        Replacements.Clear();
        foreach (var r in preview.Replacements)
            Replacements.Add(new TextReplacement { From = r.From, To = r.To });

        LoadStatusText.Text = preview.AdaptationSummary ?? AssignmentTextParser.DescribeExam(preview);

        if (!notifyIfEmpty) return;

        if (!string.IsNullOrEmpty(preview.ExamVariant) || preview.Replacements.Count > 0)
        {
            var excluded = preview.ExcludedStepIds?.Count ?? 0;
            MessageBox.Show(this,
                (preview.AdaptationSummary ?? AssignmentTextParser.DescribeExam(preview)) +
                (excluded > 0 ? $"\n\nИсключено шагов: {excluded}." : "") +
                "\n\nНажмите «Применить» для обновления всех подсказок и кода.",
                "План адаптации", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        MessageBox.Show(this,
            "Текст загружен. Стандартное задание КОД не распознано — добавьте замены вручную.",
            "Анализ", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BrowseReference_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Папка с эталонным ASP.NET проектом" };
        if (dlg.ShowDialog(this) != true) return;
        ReferencePathBox.Text = dlg.FolderName;
    }

    private void AddReplacement_Click(object sender, RoutedEventArgs e) =>
        Replacements.Add(new TextReplacement { From = "KodShopWeb", To = "" });

    private void RemoveReplacement_Click(object sender, RoutedEventArgs e)
    {
        if (ReplacementsGrid.SelectedItem is TextReplacement r)
            Replacements.Remove(r);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Вернуть исходный KodShopWeb?", "Сброс",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        ResetToBase = true;
        DialogResult = true;
        Close();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var refPath = ReferencePathBox.Text.Trim();
            var parsedProfile = AssignmentTextParser.Parse(AssignmentTextBox.Text.Trim());
            parsedProfile.Replacements = Replacements.ToList();
            CoachData data;

            if (!string.IsNullOrEmpty(refPath) && Directory.Exists(refPath))
            {
                data = CoachDataBuilder.BuildFromReferenceProject(refPath, _coachConfigPath);
                data = AssignmentAdaptEngine.Adapt(data, parsedProfile);
            }
            else
            {
                data = AssignmentAdaptEngine.Adapt(_baseData, parsedProfile);
            }

            parsedProfile.ReferenceProjectPath = string.IsNullOrEmpty(refPath) ? null : refPath;
            parsedProfile.AppliedAt = DateTime.UtcNow;
            ResultProfile = parsedProfile;
            ResultData = data;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Ошибка: " + ex.Message, "Импорт", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
