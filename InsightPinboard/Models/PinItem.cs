using System.Text.Json.Serialization;

namespace InsightPinboard.Models;

public enum PinItemType
{
    File,
    Folder,
    Url,
    Note
}

public class PinItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public PinItemType ItemType { get; set; } = PinItemType.File;

    /// <summary>ファイルパス、フォルダパス、またはURL</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>表示名（空の場合はファイル名を自動取得）</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>ユーザーが付けたコメント・メモ</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>キャンバス上のX座標</summary>
    public double X { get; set; }

    /// <summary>キャンバス上のY座標</summary>
    public double Y { get; set; }

    /// <summary>幅（0の場合はデフォルト）</summary>
    public double Width { get; set; } = 0;

    /// <summary>高さ（0の場合はデフォルト）</summary>
    public double Height { get; set; } = 0;

    /// <summary>色（#AARRGGBB形式）</summary>
    public string Color { get; set; } = "#FF2D9254";

    /// <summary>所属するグループID（空の場合はグループなし）</summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>作成日時</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>最終更新日時</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>ノートの場合のテキスト内容</summary>
    public string NoteText { get; set; } = string.Empty;

    [JsonIgnore]
    public string ResolvedDisplayName =>
        !string.IsNullOrEmpty(DisplayName) ? DisplayName :
        ItemType == PinItemType.Note ? "メモ" :
        ItemType == PinItemType.Url ? Path :
        System.IO.Path.GetFileName(Path);
}
