namespace ExamCoachDesktop;

public sealed class GhostHintState
{
    public int StartOffset { get; set; }
    public int Length { get; set; }
    public string Text { get; set; } = "";
    public bool Active { get; set; }
}
