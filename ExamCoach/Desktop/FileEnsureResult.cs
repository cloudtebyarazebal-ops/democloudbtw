namespace ExamCoachDesktop;

public sealed record FileEnsureResult(bool Success, bool WasCreated, string? Path, string? Error)
{
    public static FileEnsureResult Fail(string error) => new(false, false, null, error);
}

public sealed class ScaffoldSummary
{
    public int FoldersCreated { get; set; }
    public int FilesCreated { get; set; }
    public int FilesSkipped { get; set; }

    public string Message =>
        $"Папок: {FoldersCreated}, новых файлов: {FilesCreated}, уже были: {FilesSkipped}";
}
