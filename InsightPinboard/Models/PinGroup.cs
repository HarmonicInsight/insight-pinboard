namespace InsightPinboard.Models;

public class PinGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>グループ名</summary>
    public string Name { get; set; } = "新しいグループ";

    /// <summary>背景色（#AARRGGBB形式）</summary>
    public string Color { get; set; } = "#201E4A8A";

    /// <summary>キャンバス上のX座標</summary>
    public double X { get; set; }

    /// <summary>キャンバス上のY座標</summary>
    public double Y { get; set; }

    /// <summary>幅</summary>
    public double Width { get; set; } = 300;

    /// <summary>高さ</summary>
    public double Height { get; set; } = 200;
}
