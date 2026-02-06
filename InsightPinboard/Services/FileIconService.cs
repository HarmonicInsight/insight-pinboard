using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InsightPinboard.Services;

public static class FileIconService
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly ConcurrentDictionary<string, BitmapImage?> _faviconCache = new();
    private static readonly ConcurrentDictionary<string, bool> _faviconFetching = new();
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

    /// <summary>
    /// URLからファビコンを取得（キャッシュあり）
    /// </summary>
    public static BitmapImage? GetFavicon(string url)
    {
        try
        {
            var uri = new Uri(url);
            var domain = uri.Host;

            if (_faviconCache.TryGetValue(domain, out var cached))
                return cached;

            return null; // 非同期で取得中または未取得
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// ファビコンを非同期で取得してキャッシュに保存
    /// </summary>
    public static async Task<BitmapImage?> FetchFaviconAsync(string url, Action? onComplete = null)
    {
        try
        {
            var uri = new Uri(url);
            var domain = uri.Host;

            if (_faviconCache.TryGetValue(domain, out var cached))
                return cached;

            // 既に取得中の場合はスキップ
            if (!_faviconFetching.TryAdd(domain, true))
                return null;

            try
            {
                // Google Favicon APIを使用
                var faviconUrl = $"https://www.google.com/s2/favicons?sz=32&domain={domain}";
                var bytes = await _httpClient.GetByteArrayAsync(faviconUrl);

                BitmapImage? image = null;
                // UIスレッドでBitmapImageを作成
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    image = new BitmapImage();
                    using (var stream = new MemoryStream(bytes))
                    {
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream;
                        image.EndInit();
                        image.Freeze();
                    }
                });

                if (image != null)
                {
                    _faviconCache[domain] = image;
                    onComplete?.Invoke();
                }
                return image;
            }
            finally
            {
                _faviconFetching.TryRemove(domain, out _);
            }
        }
        catch
        {
            return null;
        }
    }
}
