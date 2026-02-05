using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InsightPinboard.Services;

public static class FileIconService
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// ファイルパスからアイコンを取得してImageSourceに変換
    /// </summary>
    public static ImageSource? GetIcon(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path)) return null;

            var shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_LARGEICON;

            // ファイルが存在しない場合は拡張子からアイコンを取得
            if (!File.Exists(path) && !Directory.Exists(path))
                flags |= SHGFI_USEFILEATTRIBUTES;

            SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);

            if (shfi.hIcon == IntPtr.Zero) return null;

            var imageSource = Imaging.CreateBitmapSourceFromHIcon(
                shfi.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            DestroyIcon(shfi.hIcon);
            return imageSource;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// PinItemTypeに応じたデフォルト絵文字を返す
    /// </summary>
    public static string GetDefaultEmoji(Models.PinItemType type) => type switch
    {
        Models.PinItemType.File => "📄",
        Models.PinItemType.Folder => "📁",
        Models.PinItemType.Url => "🔗",
        Models.PinItemType.Note => "📝",
        _ => "📌"
    };
}
