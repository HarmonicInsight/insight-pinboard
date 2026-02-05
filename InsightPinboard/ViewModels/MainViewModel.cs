using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using InsightPinboard.Helpers;
using InsightPinboard.Models;
using InsightPinboard.Services;

namespace InsightPinboard.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private AppData _appData;
    private Board? _activeBoard;
    private System.Timers.Timer _autoSaveTimer;

    public MainViewModel()
    {
        _appData = StorageService.Load();
        _activeBoard = _appData.Boards.FirstOrDefault(b => b.Id == _appData.ActiveBoardId)
                       ?? _appData.Boards.FirstOrDefault();

        Boards = new ObservableCollection<Board>(_appData.Boards);
        RefreshItems();

        // 自動保存（30秒ごと）
        _autoSaveTimer = new System.Timers.Timer(30000);
        _autoSaveTimer.Elapsed += (_, _) => Save();
        _autoSaveTimer.Start();

        AddBoardCommand = new RelayCommand(AddBoard);
        DeleteBoardCommand = new RelayCommand(o => DeleteBoard());
        AddNoteCommand = new RelayCommand(o => AddNote());
        SaveCommand = new RelayCommand(o => Save());
    }

    // --- Properties ---

    public ObservableCollection<Board> Boards { get; }
    public ObservableCollection<PinItem> Items { get; private set; } = new();
    public ObservableCollection<PinGroup> Groups { get; private set; } = new();

    public Board? ActiveBoard
    {
        get => _activeBoard;
        set
        {
            _activeBoard = value;
            _appData.ActiveBoardId = value?.Id ?? string.Empty;
            OnPropertyChanged();
            RefreshItems();
        }
    }

    // --- Commands ---

    public ICommand AddBoardCommand { get; }
    public ICommand DeleteBoardCommand { get; }
    public ICommand AddNoteCommand { get; }
    public ICommand SaveCommand { get; }

    // --- Methods ---

    private void RefreshItems()
    {
        Items = new ObservableCollection<PinItem>(ActiveBoard?.Items ?? new List<PinItem>());
        Groups = new ObservableCollection<PinGroup>(ActiveBoard?.Groups ?? new List<PinGroup>());
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(Groups));
    }

    private void AddBoard(object? param)
    {
        var name = param as string ?? $"ボード {Boards.Count + 1}";
        var board = new Board { Name = name };
        _appData.Boards.Add(board);
        Boards.Add(board);
        ActiveBoard = board;
        Save();
    }

    private void DeleteBoard()
    {
        if (ActiveBoard == null || Boards.Count <= 1) return;

        var board = ActiveBoard;
        _appData.Boards.Remove(board);
        Boards.Remove(board);
        ActiveBoard = Boards.FirstOrDefault();
        Save();
    }

    public void RenameBoardCommand(string newName)
    {
        if (ActiveBoard == null || string.IsNullOrWhiteSpace(newName)) return;
        ActiveBoard.Name = newName;
        ActiveBoard.UpdatedAt = DateTime.Now;
        OnPropertyChanged(nameof(ActiveBoard));
        Save();
    }

    /// <summary>
    /// ファイル/フォルダをドロップしてピンアイテムを追加
    /// </summary>
    public void AddFileDrop(string[] paths, double x, double y)
    {
        if (ActiveBoard == null) return;

        double offsetY = 0;
        foreach (var path in paths)
        {
            var item = new PinItem
            {
                Path = path,
                X = x,
                Y = y + offsetY,
                ItemType = Directory.Exists(path) ? PinItemType.Folder :
                           Uri.IsWellFormedUriString(path, UriKind.Absolute) ? PinItemType.Url :
                           PinItemType.File
            };
            ActiveBoard.Items.Add(item);
            Items.Add(item);
            offsetY += 70;
        }
        ActiveBoard.UpdatedAt = DateTime.Now;
        Save();
    }

    /// <summary>
    /// URLをドロップしてピンアイテムを追加
    /// </summary>
    public void AddUrlDrop(string url, double x, double y)
    {
        if (ActiveBoard == null) return;

        var item = new PinItem
        {
            Path = url,
            X = x,
            Y = y,
            ItemType = PinItemType.Url,
            DisplayName = url
        };
        ActiveBoard.Items.Add(item);
        Items.Add(item);
        ActiveBoard.UpdatedAt = DateTime.Now;
        Save();
    }

    /// <summary>
    /// メモ（付箋）を追加
    /// </summary>
    private void AddNote()
    {
        if (ActiveBoard == null) return;

        var item = new PinItem
        {
            ItemType = PinItemType.Note,
            NoteText = "",
            X = 100 + new Random().Next(200),
            Y = 100 + new Random().Next(200),
            Color = "#FFFBBF24",
            Width = 200,
            Height = 150
        };
        ActiveBoard.Items.Add(item);
        Items.Add(item);
        ActiveBoard.UpdatedAt = DateTime.Now;
        Save();
    }

    /// <summary>
    /// ピンアイテムを削除
    /// </summary>
    public void RemoveItem(PinItem item)
    {
        if (ActiveBoard == null) return;
        ActiveBoard.Items.Remove(item);
        Items.Remove(item);
        ActiveBoard.UpdatedAt = DateTime.Now;
        Save();
    }

    /// <summary>
    /// アイテムの位置を更新
    /// </summary>
    public void UpdateItemPosition(PinItem item, double x, double y)
    {
        item.X = x;
        item.Y = y;
        item.UpdatedAt = DateTime.Now;
    }

    /// <summary>
    /// ピンアイテムを開く（ファイル起動 / URL / フォルダ）
    /// </summary>
    public void OpenItem(PinItem item)
    {
        try
        {
            switch (item.ItemType)
            {
                case PinItemType.File:
                case PinItemType.Folder:
                    if (File.Exists(item.Path) || Directory.Exists(item.Path))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = item.Path,
                            UseShellExecute = true
                        });
                    }
                    break;

                case PinItemType.Url:
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.Path,
                        UseShellExecute = true
                    });
                    break;

                case PinItemType.Note:
                    // ノートはダブルクリックで編集モードに（View側で処理）
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Open error: {ex.Message}");
        }
    }

    public void Save()
    {
        _appData.Boards = new List<Board>(Boards);
        StorageService.Save(_appData);
    }

    // --- INotifyPropertyChanged ---

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
