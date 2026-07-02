namespace Resync;

public class SubtitleBlock
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public List<string> Lines { get; set; } = [];
}