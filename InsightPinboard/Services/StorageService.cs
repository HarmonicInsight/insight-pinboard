using System.IO;
using System.Text.Json;
using InsightPinboard.Models;

namespace InsightPinboard.Services;

public class StorageService
{
    private static readonly string AppFolder = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InsightPinboard");

    private static readonly string DataFile = System.IO.Path.Combine(AppFolder, "data.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AppData Load()
    {
        try
        {
            if (!File.Exists(DataFile))
                return CreateDefault();

            var json = File.ReadAllText(DataFile);
            return JsonSerializer.Deserialize<AppData>(json, JsonOptions) ?? CreateDefault();
        }
        catch
        {
            return CreateDefault();
        }
    }

    public static void Save(AppData data)
    {
        try
        {
            Directory.CreateDirectory(AppFolder);
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(DataFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Save error: {ex.Message}");
        }
    }

    public static void Export(AppData data, string filePath)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    public static AppData? Import(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<AppData>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static AppData CreateDefault()
    {
        var defaultBoard = new Board { Name = "メインボード" };
        var data = new AppData
        {
            Boards = new List<Board> { defaultBoard },
            ActiveBoardId = defaultBoard.Id
        };
        return data;
    }
}
