using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using InsightPinboard.Models;
using InsightPinboard.Services;
using InsightPinboard.ViewModels;

namespace InsightPinboard;

public partial class MainWindow : Window
{
    private MainViewModel _vm;

    // ドラッグ用
    private FrameworkElement? _dragTarget;
    private PinItem? _dragItem;
    private Point _dragStartPoint;
    private bool _isDragging;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        Loaded += (_, _) =>
        {
            RefreshBoardTabs();
            RenderItems();
            UpdateStatus();
            CenterEmptyHint();
        };

        SizeChanged += (_, _) => CenterEmptyHint();
    }

    // ===== Board Tabs =====

    private void RefreshBoardTabs()
    {
        BoardTabs.Items.Clear();
        foreach (var board in _vm.Boards)
        {
            var btn = new Button
            {
                Content = board.Name,
                Tag = board,
                Background = board == _vm.ActiveBoard
                    ? new SolidColorBrush(Color.FromArgb(30, 100, 165, 250))
                    : Brushes.Transparent,
                Foreground = board == _vm.ActiveBoard
                    ? (Brush)FindResource("FgPrimary")
                    : (Brush)FindResource("FgDim"),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 4, 12, 4),
                Margin = new Thickness(0, 0, 2, 0),
                FontSize = 12,
                Cursor = Cursors.Hand
            };
            btn.Click += BoardTab_Click;
            BoardTabs.Items.Add(btn);
        }
    }

    private void BoardTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Board board)
        {
            _vm.ActiveBoard = board;
            RefreshBoardTabs();
            RenderItems();
            UpdateStatus();
        }
    }

    private void AddBoard_Click(object sender, RoutedEventArgs e)
    {
        _vm.AddBoardCommand.Execute($"ボード {_vm.Boards.Count + 1}");
        RefreshBoardTabs();
        RenderItems();
        UpdateStatus();
    }

    // ===== Canvas Rendering =====

    private void RenderItems()
    {
        PinCanvas.Children.Clear();
        PinCanvas.Children.Add(EmptyHint);

        EmptyHint.Visibility = _vm.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var item in _vm.Items)
        {
            var card = CreatePinCard(item);
            Canvas.SetLeft(card, item.X);
            Canvas.SetTop(card, item.Y);
            PinCanvas.Children.Add(card);
        }
    }

    private Border CreatePinCard(PinItem item)
    {
        var color = ParseColor(item.Color);

        // アイコン/絵文字
        var emoji = FileIconService.GetDefaultEmoji(item.ItemType);
        var iconText = new TextBlock
        {
            Text = emoji,
            FontSize = 20,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        // ファイル名
        var nameText = new TextBlock
        {
            Text = item.ResolvedDisplayName,
            Foreground = (Brush)FindResource("FgPrimary"),
            FontSize = 13,
            FontWeight = FontWeights.Medium,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 200,
            VerticalAlignment = VerticalAlignment.Center
        };

        var topRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { iconText, nameText }
        };

        var stack = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };
        stack.Children.Add(topRow);

        // コメント
        if (!string.IsNullOrEmpty(item.Comment))
        {
            stack.Children.Add(new TextBlock
            {
                Text = item.Comment,
                Foreground = (Brush)FindResource("FgDim"),
                FontSize = 10,
                Margin = new Thickness(28, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 200
            });
        }

        // パス（小さく表示）
        if (item.ItemType != PinItemType.Note && !string.IsNullOrEmpty(item.Path))
        {
            var displayPath = item.Path.Length > 50
                ? "..." + item.Path[^47..]
                : item.Path;

            stack.Children.Add(new TextBlock
            {
                Text = displayPath,
                Foreground = new SolidColorBrush(Color.FromArgb(100, 74, 85, 104)),
                FontSize = 9,
                Margin = new Thickness(28, 1, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 200
            });
        }

        // ノートの場合はテキスト表示
        if (item.ItemType == PinItemType.Note)
        {
            stack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(item.NoteText) ? "(ダブルクリックで編集)" : item.NoteText,
                Foreground = string.IsNullOrEmpty(item.NoteText)
                    ? (Brush)FindResource("FgDim")
                    : (Brush)FindResource("FgPrimary"),
                FontSize = 12,
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 180
            });
        }

        var border = new Border
        {
            Child = stack,
            Background = new SolidColorBrush(Color.FromArgb(200, 30, 34, 51)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Cursor = Cursors.Hand,
            Tag = item,
            MinWidth = 120,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 12,
                ShadowDepth = 2,
                Opacity = 0.4
            }
        };

        // 左端の色バー
        var colorBar = new Rectangle
        {
            Width = 3,
            Fill = new SolidColorBrush(color),
            RadiusX = 1.5,
            RadiusY = 1.5,
            Margin = new Thickness(0, 8, 0, 8),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var outerGrid = new Grid();
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(colorBar, 0);
        Grid.SetColumn(stack, 1);
        outerGrid.Children.Add(colorBar);
        outerGrid.Children.Add(stack);
        border.Child = outerGrid;

        // Events
        border.MouseLeftButtonDown += Card_MouseLeftButtonDown;
        border.MouseMove += Card_MouseMove;
        border.MouseLeftButtonUp += Card_MouseLeftButtonUp;
        border.MouseEnter += (s, e) =>
        {
            border.Background = new SolidColorBrush(Color.FromArgb(230, 35, 40, 60));
        };
        border.MouseLeave += (s, e) =>
        {
            border.Background = new SolidColorBrush(Color.FromArgb(200, 30, 34, 51));
        };

        // ダブルクリックで開く
        border.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount == 2)
            {
                if (item.ItemType == PinItemType.Note)
                    EditNote(item);
                else
                    _vm.OpenItem(item);
                e.Handled = true;
            }
        };

        // 右クリックメニュー
        border.ContextMenu = CreateContextMenu(item);

        return border;
    }

    // ===== Drag Items on Canvas =====

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2) return; // ダブルクリックは別処理

        _dragTarget = sender as FrameworkElement;
        _dragItem = _dragTarget?.Tag as PinItem;
        _dragStartPoint = e.GetPosition(PinCanvas);
        _isDragging = false;
        _dragTarget?.CaptureMouse();
        e.Handled = true;
    }

    private void Card_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragTarget == null || _dragItem == null) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(PinCanvas);
        var diff = pos - _dragStartPoint;

        if (!_isDragging && (Math.Abs(diff.X) > 3 || Math.Abs(diff.Y) > 3))
            _isDragging = true;

        if (_isDragging)
        {
            var newX = _dragItem.X + diff.X;
            var newY = _dragItem.Y + diff.Y;

            Canvas.SetLeft(_dragTarget, newX);
            Canvas.SetTop(_dragTarget, newY);

            _dragItem.X = newX;
            _dragItem.Y = newY;
            _dragStartPoint = pos;
        }
    }

    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragTarget != null)
        {
            _dragTarget.ReleaseMouseCapture();
            if (_isDragging && _dragItem != null)
                _vm.UpdateItemPosition(_dragItem, _dragItem.X, _dragItem.Y);
        }
        _dragTarget = null;
        _dragItem = null;
        _isDragging = false;
    }

    // ===== External Drag & Drop =====

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        var pos = e.GetPosition(PinCanvas);

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            _vm.AddFileDrop(files, pos.X, pos.Y);
        }
        else if (e.Data.GetDataPresent(DataFormats.Text))
        {
            var text = (string)e.Data.GetData(DataFormats.Text)!;
            if (Uri.IsWellFormedUriString(text, UriKind.Absolute))
                _vm.AddUrlDrop(text, pos.X, pos.Y);
        }

        RenderItems();
        UpdateStatus();
    }

    // ===== Context Menu =====

    private ContextMenu CreateContextMenu(PinItem item)
    {
        var menu = new ContextMenu();

        var openItem = new MenuItem { Header = "開く" };
        openItem.Click += (_, _) => _vm.OpenItem(item);
        menu.Items.Add(openItem);

        if (item.ItemType == PinItemType.File || item.ItemType == PinItemType.Folder)
        {
            var openFolderItem = new MenuItem { Header = "フォルダを開く" };
            openFolderItem.Click += (_, _) =>
            {
                var dir = item.ItemType == PinItemType.Folder
                    ? item.Path
                    : System.IO.Path.GetDirectoryName(item.Path);
                if (dir != null && Directory.Exists(dir))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dir,
                        UseShellExecute = true
                    });
                }
            };
            menu.Items.Add(openFolderItem);
        }

        menu.Items.Add(new Separator());

        var commentItem = new MenuItem { Header = "コメント編集..." };
        commentItem.Click += (_, _) => EditComment(item);
        menu.Items.Add(commentItem);

        var renameItem = new MenuItem { Header = "表示名変更..." };
        renameItem.Click += (_, _) => EditDisplayName(item);
        menu.Items.Add(renameItem);

        menu.Items.Add(new Separator());

        var deleteItem = new MenuItem { Header = "削除" };
        deleteItem.Click += (_, _) =>
        {
            _vm.RemoveItem(item);
            RenderItems();
            UpdateStatus();
        };
        menu.Items.Add(deleteItem);

        return menu;
    }

    // ===== Edit Dialogs (simple InputBox) =====

    private void EditComment(PinItem item)
    {
        var result = ShowInputDialog("コメント編集", "コメント:", item.Comment);
        if (result != null)
        {
            item.Comment = result;
            item.UpdatedAt = DateTime.Now;
            RenderItems();
            _vm.Save();
        }
    }

    private void EditDisplayName(PinItem item)
    {
        var result = ShowInputDialog("表示名変更", "表示名:", item.DisplayName);
        if (result != null)
        {
            item.DisplayName = result;
            item.UpdatedAt = DateTime.Now;
            RenderItems();
            _vm.Save();
        }
    }

    private void EditNote(PinItem item)
    {
        var result = ShowInputDialog("メモ編集", "メモ:", item.NoteText);
        if (result != null)
        {
            item.NoteText = result;
            item.UpdatedAt = DateTime.Now;
            RenderItems();
            _vm.Save();
        }
    }

    private string? ShowInputDialog(string title, string label, string defaultValue)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(22, 25, 37))
        };

        var stack = new StackPanel { Margin = new Thickness(16) };

        var lbl = new TextBlock
        {
            Text = label,
            Foreground = Brushes.LightGray,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var textBox = new TextBox
        {
            Text = defaultValue,
            FontSize = 14,
            Padding = new Thickness(8, 6, 8, 6),
            Background = new SolidColorBrush(Color.FromRgb(12, 14, 20)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(60, 65, 80)),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 60
        };

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };

        string? result = null;

        var okBtn = new Button
        {
            Content = "OK",
            Width = 80,
            Padding = new Thickness(0, 4, 0, 4),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(45, 146, 84)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        okBtn.Click += (_, _) => { result = textBox.Text; dialog.Close(); };

        var cancelBtn = new Button
        {
            Content = "キャンセル",
            Width = 80,
            Padding = new Thickness(0, 4, 0, 4),
            Background = Brushes.Transparent,
            Foreground = Brushes.Gray,
            BorderBrush = new SolidColorBrush(Color.FromRgb(60, 65, 80)),
            BorderThickness = new Thickness(1)
        };
        cancelBtn.Click += (_, _) => dialog.Close();

        btnPanel.Children.Add(okBtn);
        btnPanel.Children.Add(cancelBtn);

        stack.Children.Add(lbl);
        stack.Children.Add(textBox);
        stack.Children.Add(btnPanel);
        dialog.Content = stack;

        textBox.SelectAll();
        textBox.Focus();

        dialog.ShowDialog();
        return result;
    }

    // ===== Misc =====

    private void AddNote_Click(object sender, RoutedEventArgs e)
    {
        _vm.AddNoteCommand.Execute(null);
        RenderItems();
        UpdateStatus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _vm.Save();
        StatusText.Text = "保存しました";
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // キャンバス空白クリック → 選択解除（将来の拡張用）
    }

    private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // キャンバス右クリック → 将来の拡張用（グループ作成など）
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _vm.Save();
    }

    private void UpdateStatus()
    {
        var count = _vm.Items.Count;
        ItemCountText.Text = $"{count} アイテム";
        StatusText.Text = $"{_vm.ActiveBoard?.Name ?? ""} — 準備完了";
        EmptyHint.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CenterEmptyHint()
    {
        Canvas.SetLeft(EmptyHint, (PinCanvas.ActualWidth - 400) / 2);
        Canvas.SetTop(EmptyHint, (PinCanvas.ActualHeight - 40) / 2);
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return c;
        }
        catch
        {
            return Color.FromRgb(45, 146, 84);
        }
    }
}
