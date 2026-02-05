namespace InsightPinboard.Models;

public class Board
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "新しいボード";
    public List<PinItem> Items { get; set; } = new();
    public List<PinGroup> Groups { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>キャンバスのズームレベル</summary>
    public double Zoom { get; set; } = 1.0;

    /// <summary>キャンバスのパンオフセットX</summary>
    public double OffsetX { get; set; } = 0;

    /// <summary>キャンバスのパンオフセットY</summary>
    public double OffsetY { get; set; } = 0;
}

public class AppData
{
    public List<Board> Boards { get; set; } = new();
    public string ActiveBoardId { get; set; } = string.Empty;
    public int WindowWidth { get; set; } = 1200;
    public int WindowHeight { get; set; } = 800;
}
