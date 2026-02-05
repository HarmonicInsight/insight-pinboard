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
    private PinGroup? _dragGroup;
    private Point _dragStartPoint;
    private bool _isDragging;

    // リサイズ用
    private FrameworkElement? _resizeTarget;
    private PinItem? _resizeItem;
    private PinGroup? _resizeGroup;
    private string _resizeDirection = "";

    // パン用
    private bool _isPanning;
    private Point _panStartPoint;
    private Point _panStartTranslate;

    // 範囲選択用
    private bool _isSelecting;
    private Point _selectionStart;

    // ズーム
    private double _zoomLevel = 1.0;
    private const double ZoomMin = 0.25;
    private const double ZoomMax = 3.0;
    private const double ZoomStep = 0.1;
    private const int GridSize = 20;

    // 複数選択用
    private HashSet<PinItem> _selectedItems = new();
    private HashSet<PinGroup> _selectedGroups = new();
    private Dictionary<PinItem, Border> _itemElements = new();
    private Dictionary<PinGroup, Border> _groupElements = new();

    // 検索用
    private string _searchQuery = "";
    private List<PinItem> _searchResults = new();
    private int _searchIndex = 0;

    // Undo/Redo
    private readonly UndoManager _undoManager = new();

    // ドラッグ開始時の位置保存（Undo用）
    private Dictionary<PinItem, (double x, double y)> _dragStartPositions = new();
    private (double x, double y)? _groupDragStartPosition;

    // リサイズ開始時のサイズ保存（Undo用）
    private (double width, double height)? _resizeStartSize;

    // 色パレット
    private static readonly string[] NoteColors = {
        "#FFFBBF24", // Yellow
        "#FF60A5FA", // Blue
        "#FF4ADE80", // Green
        "#FFF472B6", // Pink
        "#FFA78BFA", // Purple
        "#FF38BDF8", // Cyan
        "#FFFB923C", // Orange
        "#FFF87171"  // Red
    };

    private static readonly string[] GroupColors = {
        "#301E4A8A", // Blue
        "#302D9254", // Green
        "#30D97706", // Yellow
        "#30DC2626", // Red
        "#307C3AED", // Purple
        "#300891B2", // Cyan
        "#30EA580C", // Orange
        "#30DB2777"  // Pink
    };

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        // Undo/Redo状態変更時にボタン更新
        _undoManager.StateChanged += (_, _) => UpdateUndoRedoButtons();

        Loaded += (_, _) =>
        {
            RefreshBoardTabs();
            RenderAll();
            UpdateStatus();
            UpdateUndoRedoButtons();
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
            btn.MouseRightButtonDown += BoardTab_RightClick;
            BoardTabs.Items.Add(btn);
        }
    }

    private void BoardTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Board board)
        {
            _vm.ActiveBoard = board;
            RefreshBoardTabs();
            RenderAll();
            UpdateStatus();
        }
    }

    private void BoardTab_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Board board)
        {
            var menu = new ContextMenu();

            var renameItem = new MenuItem { Header = "名前変更..." };
            renameItem.Click += (_, _) =>
            {
                var result = ShowInputDialog("ボード名変更", "新しい名前:", board.Name);
                if (result != null)
                {
                    board.Name = result;
                    RefreshBoardTabs();
                    _vm.Save();
                }
            };
            menu.Items.Add(renameItem);

            if (_vm.Boards.Count > 1)
            {
                var deleteItem = new MenuItem { Header = "削除" };
                deleteItem.Click += (_, _) =>
                {
                    if (MessageBox.Show($"ボード「{board.Name}」を削除しますか？", "確認",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        _vm.ActiveBoard = board;
                        _vm.DeleteBoardCommand.Execute(null);
                        RefreshBoardTabs();
                        RenderAll();
                        UpdateStatus();
                    }
                };
                menu.Items.Add(deleteItem);
            }

            menu.IsOpen = true;
        }
    }

    private void AddBoard_Click(object sender, RoutedEventArgs e)
    {
        _vm.AddBoardCommand.Execute($"ボード {_vm.Boards.Count + 1}");
        RefreshBoardTabs();
        RenderAll();
        UpdateStatus();
    }

    // ===== Rendering =====

    private void RenderAll()
    {
        PinCanvas.Children.Clear();
        if (EmptyHint.Parent is Panel hintParent)
        {
            hintParent.Children.Remove(EmptyHint);
        }
        if (SelectionRect.Parent is Panel selectionParent)
        {
            selectionParent.Children.Remove(SelectionRect);
        }
        PinCanvas.Children.Add(EmptyHint);
        PinCanvas.Children.Add(SelectionRect);

        _itemElements.Clear();
        _groupElements.Clear();

        var hasContent = _vm.Items.Count > 0 || _vm.Groups.Count > 0;
        EmptyHint.Visibility = hasContent ? Visibility.Collapsed : Visibility.Visible;

        // グループを先に描画（背景）
        foreach (var group in _vm.Groups)
        {
            var groupElement = CreateGroupElement(group);
            Canvas.SetLeft(groupElement, group.X);
            Canvas.SetTop(groupElement, group.Y);
            PinCanvas.Children.Add(groupElement);
            _groupElements[group] = groupElement;
        }

        // アイテムを描画（検索フィルタ適用）
        var itemsToRender = string.IsNullOrEmpty(_searchQuery)
            ? _vm.Items
            : _vm.Items.Where(i => MatchesSearch(i, _searchQuery));

        foreach (var item in itemsToRender)
        {
            var card = CreatePinCard(item);
            Canvas.SetLeft(card, item.X);
            Canvas.SetTop(card, item.Y);
            PinCanvas.Children.Add(card);
            _itemElements[item] = card;

            // 選択状態を反映
            if (_selectedItems.Contains(item))
                ApplySelectionStyle(card, true);

            // 検索ハイライト
            if (_searchResults.Contains(item))
                ApplySearchHighlight(card, item == _searchResults.ElementAtOrDefault(_searchIndex));
        }

        UpdateSelectionInfo();
    }

    private bool MatchesSearch(PinItem item, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        query = query.ToLowerInvariant();

        return (item.ResolvedDisplayName?.ToLowerInvariant().Contains(query) ?? false)
            || (item.Path?.ToLowerInvariant().Contains(query) ?? false)
            || (item.Comment?.ToLowerInvariant().Contains(query) ?? false)
            || (item.NoteText?.ToLowerInvariant().Contains(query) ?? false);
    }

    private void ApplySelectionStyle(Border border, bool selected)
    {
        if (selected)
        {
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(96, 165, 250));
            border.BorderThickness = new Thickness(2);
        }
    }

    private void ApplySearchHighlight(Border border, bool isCurrent)
    {
        if (isCurrent)
        {
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36));
            border.BorderThickness = new Thickness(3);
        }
        else
        {
            border.BorderBrush = new SolidColorBrush(Color.FromArgb(150, 251, 191, 36));
            border.BorderThickness = new Thickness(2);
        }
    }

    private Border CreateGroupElement(PinGroup group)
    {
        var color = ParseColor(group.Color);

        var titleBlock = new TextBlock
        {
            Text = group.Name,
            Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
            FontSize = 13,
            FontWeight = FontWeights.Medium,
            Margin = new Thickness(10, 6, 10, 6)
        };

        var border = new Border
        {
            Background = new SolidColorBrush(color),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, color.R, color.G, color.B)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(12),
            Width = group.Width,
            Height = group.Height,
            Tag = group,
            Cursor = Cursors.SizeAll,
            Child = new DockPanel
            {
                Children =
                {
                    titleBlock
                }
            }
        };

        DockPanel.SetDock(titleBlock, Dock.Top);

        // リサイズハンドル（右下）
        var resizeHandle = new Rectangle
        {
            Width = 16,
            Height = 16,
            Fill = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
            Cursor = Cursors.SizeNWSE,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 4, 4)
        };
        resizeHandle.MouseLeftButtonDown += (s, e) =>
        {
            _resizeTarget = border;
            _resizeGroup = group;
            _resizeDirection = "SE";
            _dragStartPoint = e.GetPosition(PinCanvas);
            resizeHandle.CaptureMouse();
            e.Handled = true;
        };
        resizeHandle.MouseMove += ResizeHandle_MouseMove;
        resizeHandle.MouseLeftButtonUp += ResizeHandle_MouseUp;

        var grid = new Grid();
        grid.Children.Add(border.Child);
        grid.Children.Add(resizeHandle);
        border.Child = grid;

        // ドラッグイベント
        border.MouseLeftButtonDown += GroupElement_MouseLeftButtonDown;
        border.MouseMove += GroupElement_MouseMove;
        border.MouseLeftButtonUp += GroupElement_MouseLeftButtonUp;

        // 右クリックメニュー
        border.ContextMenu = CreateGroupContextMenu(group);

        return border;
    }

    private Border CreatePinCard(PinItem item)
    {
        var color = ParseColor(item.Color);
        var isNote = item.ItemType == PinItemType.Note;

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
            MaxWidth = isNote ? item.Width - 40 : 200,
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
                MaxWidth = isNote ? item.Width - 50 : 200
            });
        }

        // パス（小さく表示）
        if (!isNote && !string.IsNullOrEmpty(item.Path))
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
        if (isNote)
        {
            var noteTextBlock = new TextBlock
            {
                Text = string.IsNullOrEmpty(item.NoteText) ? "(ダブルクリックで編集)" : item.NoteText,
                Foreground = string.IsNullOrEmpty(item.NoteText)
                    ? (Brush)FindResource("FgDim")
                    : (Brush)FindResource("FgPrimary"),
                FontSize = 12,
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = item.Width > 0 ? item.Width - 30 : 170
            };
            stack.Children.Add(noteTextBlock);
        }

        var bgColor = isNote
            ? Color.FromArgb(240, color.R, color.G, color.B)
            : Color.FromArgb(200, 30, 34, 51);

        var border = new Border
        {
            Child = stack,
            Background = new SolidColorBrush(bgColor),
            BorderBrush = new SolidColorBrush(Color.FromArgb(isNote ? (byte)100 : (byte)40, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Cursor = Cursors.Hand,
            Tag = item,
            MinWidth = 120,
            Width = item.Width > 0 ? item.Width : double.NaN,
            Height = item.Height > 0 ? item.Height : double.NaN,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 12,
                ShadowDepth = 2,
                Opacity = 0.4
            }
        };

        // 左端の色バー（ノート以外）
        if (!isNote)
        {
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
        }

        // リサイズハンドル（ノートの場合）
        if (isNote)
        {
            var grid = new Grid();
            grid.Children.Add(border.Child);

            var resizeHandle = new Rectangle
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)),
                Cursor = Cursors.SizeNWSE,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 4, 4)
            };
            resizeHandle.MouseLeftButtonDown += (s, e) =>
            {
                _resizeTarget = border;
                _resizeItem = item;
                _resizeDirection = "SE";
                _dragStartPoint = e.GetPosition(PinCanvas);
                resizeHandle.CaptureMouse();
                e.Handled = true;
            };
            resizeHandle.MouseMove += ResizeHandle_MouseMove;
            resizeHandle.MouseLeftButtonUp += ResizeHandle_MouseUp;

            grid.Children.Add(resizeHandle);
            border.Child = grid;
        }

        // Events
        border.MouseLeftButtonDown += Card_MouseLeftButtonDown;
        border.MouseMove += Card_MouseMove;
        border.MouseLeftButtonUp += Card_MouseLeftButtonUp;

        var originalBg = border.Background;
        border.MouseEnter += (s, e) =>
        {
            if (isNote)
                border.Background = new SolidColorBrush(Color.FromArgb(255, color.R, color.G, color.B));
            else
                border.Background = new SolidColorBrush(Color.FromArgb(230, 35, 40, 60));
        };
        border.MouseLeave += (s, e) =>
        {
            border.Background = originalBg;
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
        border.ContextMenu = CreateItemContextMenu(item);

        return border;
    }

    // ===== Resize Handling =====

    private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (_resizeTarget == null) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(PinCanvas);
        var diffX = pos.X - _dragStartPoint.X;
        var diffY = pos.Y - _dragStartPoint.Y;

        if (_resizeItem != null)
        {
            var newWidth = Math.Max(100, (_resizeItem.Width > 0 ? _resizeItem.Width : 200) + diffX);
            var newHeight = Math.Max(80, (_resizeItem.Height > 0 ? _resizeItem.Height : 100) + diffY);

            if (GridSnapCheck.IsChecked == true)
            {
                newWidth = SnapToGrid(newWidth);
                newHeight = SnapToGrid(newHeight);
            }

            _resizeItem.Width = newWidth;
            _resizeItem.Height = newHeight;
            _resizeTarget.Width = newWidth;
            _resizeTarget.Height = newHeight;
        }
        else if (_resizeGroup != null)
        {
            var newWidth = Math.Max(150, _resizeGroup.Width + diffX);
            var newHeight = Math.Max(100, _resizeGroup.Height + diffY);

            if (GridSnapCheck.IsChecked == true)
            {
                newWidth = SnapToGrid(newWidth);
                newHeight = SnapToGrid(newHeight);
            }

            _resizeGroup.Width = newWidth;
            _resizeGroup.Height = newHeight;
            _resizeTarget.Width = newWidth;
            _resizeTarget.Height = newHeight;
        }

        _dragStartPoint = pos;
    }

    private void ResizeHandle_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Rectangle handle)
            handle.ReleaseMouseCapture();

        _resizeTarget = null;
        _resizeItem = null;
        _resizeGroup = null;
        _vm.Save();
    }

    // ===== Drag Items on Canvas =====

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2) return;

        _dragTarget = sender as FrameworkElement;
        _dragItem = _dragTarget?.Tag as PinItem;
        _dragStartPoint = e.GetPosition(PinCanvas);
        _isDragging = false;

        // ドラッグ開始位置を保存（Undo用）
        _dragStartPositions.Clear();
        if (_dragItem != null)
        {
            // 複数選択の場合は全ての開始位置を保存
            if (_selectedItems.Contains(_dragItem) && _selectedItems.Count > 1)
            {
                foreach (var item in _selectedItems)
                    _dragStartPositions[item] = (item.X, item.Y);
            }
            else
            {
                _dragStartPositions[_dragItem] = (_dragItem.X, _dragItem.Y);
            }
        }

        // Ctrl/Shiftクリックで複数選択
        var addToSelection = Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == ModifierKeys.Shift;
        if (_dragItem != null)
        {
            if (addToSelection)
            {
                // 選択をトグル
                if (_selectedItems.Contains(_dragItem))
                    _selectedItems.Remove(_dragItem);
                else
                    _selectedItems.Add(_dragItem);
                RenderAll();
            }
            else if (!_selectedItems.Contains(_dragItem))
            {
                // 選択されていないアイテムをクリック → 選択をクリアしてこのアイテムを選択
                _selectedItems.Clear();
                _selectedGroups.Clear();
                _selectedItems.Add(_dragItem);
                RenderAll();
            }
            // 選択済みアイテムをクリック → ドラッグ準備（選択はそのまま）
        }

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
            var snapDiffX = diff.X;
            var snapDiffY = diff.Y;

            if (GridSnapCheck.IsChecked == true)
            {
                snapDiffX = SnapToGrid(_dragItem.X + diff.X) - _dragItem.X;
                snapDiffY = SnapToGrid(_dragItem.Y + diff.Y) - _dragItem.Y;
            }

            // 複数選択されていれば全て一緒に移動
            if (_selectedItems.Count > 1 && _selectedItems.Contains(_dragItem))
            {
                foreach (var item in _selectedItems)
                {
                    item.X += snapDiffX;
                    item.Y += snapDiffY;
                    if (_itemElements.TryGetValue(item, out var element))
                    {
                        Canvas.SetLeft(element, item.X);
                        Canvas.SetTop(element, item.Y);
                    }
                }
            }
            else
            {
                _dragItem.X += snapDiffX;
                _dragItem.Y += snapDiffY;
                Canvas.SetLeft(_dragTarget, _dragItem.X);
                Canvas.SetTop(_dragTarget, _dragItem.Y);
            }

            _dragStartPoint = pos;
        }
    }

    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragTarget != null)
        {
            _dragTarget.ReleaseMouseCapture();
            if (_isDragging && _dragStartPositions.Count > 0)
            {
                // Undoアクションを作成
                if (_dragStartPositions.Count > 1)
                {
                    // 複数アイテム移動
                    var moves = new List<(PinItem, double, double, double, double)>();
                    foreach (var kvp in _dragStartPositions)
                    {
                        var item = kvp.Key;
                        var (oldX, oldY) = kvp.Value;
                        if (Math.Abs(item.X - oldX) > 0.1 || Math.Abs(item.Y - oldY) > 0.1)
                            moves.Add((item, oldX, oldY, item.X, item.Y));
                    }
                    if (moves.Count > 0)
                    {
                        // 位置を元に戻してからアクション実行（Undoスタックに正しく積む）
                        foreach (var (item, oldX, oldY, _, _) in moves)
                        {
                            item.X = oldX;
                            item.Y = oldY;
                        }
                        _undoManager.Execute(new MoveMultipleItemsAction(moves));
                    }
                }
                else if (_dragItem != null && _dragStartPositions.TryGetValue(_dragItem, out var startPos))
                {
                    // 単一アイテム移動
                    var (oldX, oldY) = startPos;
                    if (Math.Abs(_dragItem.X - oldX) > 0.1 || Math.Abs(_dragItem.Y - oldY) > 0.1)
                    {
                        var newX = _dragItem.X;
                        var newY = _dragItem.Y;
                        _dragItem.X = oldX;
                        _dragItem.Y = oldY;
                        _undoManager.Execute(new MoveItemAction(_dragItem, oldX, oldY, newX, newY));
                    }
                }

                RenderAll();
                _vm.Save();
            }
        }
        _dragTarget = null;
        _dragStartPositions.Clear();
        _dragItem = null;
        _isDragging = false;
    }

    // ===== Group Drag =====

    private void GroupElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            // ダブルクリックで名前変更
            if (sender is FrameworkElement el && el.Tag is PinGroup group)
            {
                EditGroupName(group);
                e.Handled = true;
            }
            return;
        }

        _dragTarget = sender as FrameworkElement;
        _dragGroup = _dragTarget?.Tag as PinGroup;
        _dragStartPoint = e.GetPosition(PinCanvas);
        _isDragging = false;
        _dragTarget?.CaptureMouse();
        e.Handled = true;
    }

    private void GroupElement_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragTarget == null || _dragGroup == null) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(PinCanvas);
        var diff = pos - _dragStartPoint;

        if (!_isDragging && (Math.Abs(diff.X) > 3 || Math.Abs(diff.Y) > 3))
            _isDragging = true;

        if (_isDragging)
        {
            var newX = _dragGroup.X + diff.X;
            var newY = _dragGroup.Y + diff.Y;

            if (GridSnapCheck.IsChecked == true)
            {
                newX = SnapToGrid(newX);
                newY = SnapToGrid(newY);
            }

            Canvas.SetLeft(_dragTarget, newX);
            Canvas.SetTop(_dragTarget, newY);

            _dragGroup.X = newX;
            _dragGroup.Y = newY;
            _dragStartPoint = pos;
        }
    }

    private void GroupElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragTarget != null)
        {
            _dragTarget.ReleaseMouseCapture();
        }
        _dragTarget = null;
        _dragGroup = null;
        _isDragging = false;
        _vm.Save();
    }

    // ===== Zoom & Pan =====

    private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mousePos = e.GetPosition(PinCanvas);

        var oldZoom = _zoomLevel;
        if (e.Delta > 0)
            _zoomLevel = Math.Min(ZoomMax, _zoomLevel + ZoomStep);
        else
            _zoomLevel = Math.Max(ZoomMin, _zoomLevel - ZoomStep);

        // ズーム中心をマウス位置に
        var zoomFactor = _zoomLevel / oldZoom;
        CanvasTranslate.X = mousePos.X - (mousePos.X - CanvasTranslate.X) * zoomFactor;
        CanvasTranslate.Y = mousePos.Y - (mousePos.Y - CanvasTranslate.Y) * zoomFactor;

        CanvasScale.ScaleX = _zoomLevel;
        CanvasScale.ScaleY = _zoomLevel;
        UpdateZoomText();
    }

    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            _isPanning = true;
            _panStartPoint = e.GetPosition(this);
            _panStartTranslate = new Point(CanvasTranslate.X, CanvasTranslate.Y);
            ((UIElement)sender).CaptureMouse();
            e.Handled = true;
        }
    }

    private void Canvas_PanMove(object sender, MouseEventArgs e)
    {
        if (_isPanning && e.MiddleButton == MouseButtonState.Pressed)
        {
            var pos = e.GetPosition(this);
            CanvasTranslate.X = _panStartTranslate.X + (pos.X - _panStartPoint.X);
            CanvasTranslate.Y = _panStartTranslate.Y + (pos.Y - _panStartPoint.Y);
        }

        // 範囲選択中
        if (_isSelecting && e.LeftButton == MouseButtonState.Pressed)
        {
            var currentPos = e.GetPosition(PinCanvas);
            var x = Math.Min(_selectionStart.X, currentPos.X);
            var y = Math.Min(_selectionStart.Y, currentPos.Y);
            var w = Math.Abs(currentPos.X - _selectionStart.X);
            var h = Math.Abs(currentPos.Y - _selectionStart.Y);

            Canvas.SetLeft(SelectionRect, x);
            Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = w;
            SelectionRect.Height = h;
        }

        // マウス位置表示
        var canvasPos = e.GetPosition(PinCanvas);
        MousePosText.Text = $"X: {canvasPos.X:F0}  Y: {canvasPos.Y:F0}";
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            ((UIElement)sender).ReleaseMouseCapture();
        }

        // 範囲選択終了
        if (_isSelecting)
        {
            _isSelecting = false;
            PinCanvas.ReleaseMouseCapture();
            SelectionRect.Visibility = Visibility.Collapsed;

            // 選択範囲内のアイテムを選択
            var rect = new Rect(
                Canvas.GetLeft(SelectionRect),
                Canvas.GetTop(SelectionRect),
                SelectionRect.Width,
                SelectionRect.Height
            );

            if (rect.Width > 5 && rect.Height > 5)
            {
                var addToSelection = Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == ModifierKeys.Shift;
                if (!addToSelection)
                {
                    _selectedItems.Clear();
                    _selectedGroups.Clear();
                }

                foreach (var item in _vm.Items)
                {
                    var itemRect = new Rect(item.X, item.Y, item.Width > 0 ? item.Width : 150, item.Height > 0 ? item.Height : 60);
                    if (rect.IntersectsWith(itemRect))
                        _selectedItems.Add(item);
                }

                foreach (var group in _vm.Groups)
                {
                    var groupRect = new Rect(group.X, group.Y, group.Width, group.Height);
                    if (rect.IntersectsWith(groupRect))
                        _selectedGroups.Add(group);
                }

                RenderAll();
            }
        }
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _zoomLevel = Math.Min(ZoomMax, _zoomLevel + ZoomStep);
        ApplyZoom();
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _zoomLevel = Math.Max(ZoomMin, _zoomLevel - ZoomStep);
        ApplyZoom();
    }

    private void ZoomReset_Click(object sender, RoutedEventArgs e)
    {
        _zoomLevel = 1.0;
        CanvasTranslate.X = 0;
        CanvasTranslate.Y = 0;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        CanvasScale.ScaleX = _zoomLevel;
        CanvasScale.ScaleY = _zoomLevel;
        UpdateZoomText();
    }

    private void UpdateZoomText()
    {
        ZoomLevelText.Text = $"{_zoomLevel * 100:F0}%";
    }

    // ===== External Drag & Drop =====

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        var screenPos = e.GetPosition(PinCanvas);
        // スケールを考慮した座標
        var pos = new Point(
            (screenPos.X - CanvasTranslate.X) / _zoomLevel,
            (screenPos.Y - CanvasTranslate.Y) / _zoomLevel
        );

        if (GridSnapCheck.IsChecked == true)
        {
            pos = new Point(SnapToGrid(pos.X), SnapToGrid(pos.Y));
        }

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

        RenderAll();
        UpdateStatus();
    }

    // ===== Context Menus =====

    private ContextMenu CreateItemContextMenu(PinItem item)
    {
        var menu = new ContextMenu();

        if (item.ItemType != PinItemType.Note)
        {
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
        }

        // 色変更サブメニュー
        var colorMenu = new MenuItem { Header = "色変更" };
        foreach (var c in NoteColors)
        {
            var colorItem = new MenuItem
            {
                Header = new Rectangle
                {
                    Width = 16,
                    Height = 16,
                    Fill = new SolidColorBrush(ParseColor(c)),
                    RadiusX = 3,
                    RadiusY = 3
                }
            };
            var capturedColor = c;
            colorItem.Click += (_, _) =>
            {
                item.Color = capturedColor;
                RenderAll();
                _vm.Save();
            };
            colorMenu.Items.Add(colorItem);
        }
        menu.Items.Add(colorMenu);

        var commentItem = new MenuItem { Header = "コメント編集..." };
        commentItem.Click += (_, _) => EditComment(item);
        menu.Items.Add(commentItem);

        var renameItem = new MenuItem { Header = "表示名変更..." };
        renameItem.Click += (_, _) => EditDisplayName(item);
        menu.Items.Add(renameItem);

        if (item.ItemType == PinItemType.Note)
        {
            var editNoteItem = new MenuItem { Header = "メモ編集..." };
            editNoteItem.Click += (_, _) => EditNote(item);
            menu.Items.Add(editNoteItem);
        }

        menu.Items.Add(new Separator());

        var deleteItem = new MenuItem { Header = "削除" };
        deleteItem.Click += (_, _) =>
        {
            _vm.RemoveItem(item);
            RenderAll();
            UpdateStatus();
        };
        menu.Items.Add(deleteItem);

        return menu;
    }

    private ContextMenu CreateGroupContextMenu(PinGroup group)
    {
        var menu = new ContextMenu();

        var renameItem = new MenuItem { Header = "名前変更..." };
        renameItem.Click += (_, _) => EditGroupName(group);
        menu.Items.Add(renameItem);

        // 色変更サブメニュー
        var colorMenu = new MenuItem { Header = "色変更" };
        foreach (var c in GroupColors)
        {
            var colorItem = new MenuItem
            {
                Header = new Rectangle
                {
                    Width = 16,
                    Height = 16,
                    Fill = new SolidColorBrush(ParseColor(c)),
                    RadiusX = 3,
                    RadiusY = 3
                }
            };
            var capturedColor = c;
            colorItem.Click += (_, _) =>
            {
                group.Color = capturedColor;
                RenderAll();
                _vm.Save();
            };
            colorMenu.Items.Add(colorItem);
        }
        menu.Items.Add(colorMenu);

        menu.Items.Add(new Separator());

        var deleteItem = new MenuItem { Header = "削除" };
        deleteItem.Click += (_, _) =>
        {
            _vm.RemoveGroup(group);
            RenderAll();
            UpdateStatus();
        };
        menu.Items.Add(deleteItem);

        return menu;
    }

    // ===== Edit Dialogs =====

    private void EditComment(PinItem item)
    {
        var result = ShowInputDialog("コメント編集", "コメント:", item.Comment);
        if (result != null)
        {
            item.Comment = result;
            item.UpdatedAt = DateTime.Now;
            RenderAll();
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
            RenderAll();
            _vm.Save();
        }
    }

    private void EditNote(PinItem item)
    {
        var result = ShowInputDialog("メモ編集", "メモ:", item.NoteText, multiline: true);
        if (result != null)
        {
            item.NoteText = result;
            item.UpdatedAt = DateTime.Now;
            RenderAll();
            _vm.Save();
        }
    }

    private void EditGroupName(PinGroup group)
    {
        var result = ShowInputDialog("グループ名変更", "グループ名:", group.Name);
        if (result != null)
        {
            group.Name = result;
            RenderAll();
            _vm.Save();
        }
    }

    private string? ShowInputDialog(string title, string label, string defaultValue, bool multiline = false)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = multiline ? 280 : 180,
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
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            Height = multiline ? 120 : double.NaN,
            VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
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

    // ===== Toolbar Actions =====

    private void AddNote_Click(object sender, RoutedEventArgs e)
    {
        _vm.AddNoteCommand.Execute(null);
        RenderAll();
        UpdateStatus();
    }

    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        _vm.AddGroup();
        RenderAll();
        UpdateStatus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _vm.Save();
        StatusText.Text = "保存しました";
    }

    // ===== Undo/Redo =====

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_undoManager.CanUndo)
        {
            _undoManager.Undo();
            RenderAll();
            _vm.Save();
            StatusText.Text = "元に戻しました";
        }
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (_undoManager.CanRedo)
        {
            _undoManager.Redo();
            RenderAll();
            _vm.Save();
            StatusText.Text = "やり直しました";
        }
    }

    private void UpdateUndoRedoButtons()
    {
        Dispatcher.Invoke(() =>
        {
            UndoBtn.IsEnabled = _undoManager.CanUndo;
            RedoBtn.IsEnabled = _undoManager.CanRedo;
            UndoBtn.Foreground = _undoManager.CanUndo
                ? (Brush)FindResource("FgSecondary")
                : (Brush)FindResource("FgDim");
            RedoBtn.Foreground = _undoManager.CanRedo
                ? (Brush)FindResource("FgSecondary")
                : (Brush)FindResource("FgDim");

            // ツールチップに説明を追加
            UndoBtn.ToolTip = _undoManager.CanUndo
                ? $"元に戻す: {_undoManager.UndoDescription} (Ctrl+Z)"
                : "元に戻す (Ctrl+Z)";
            RedoBtn.ToolTip = _undoManager.CanRedo
                ? $"やり直し: {_undoManager.RedoDescription} (Ctrl+Y)"
                : "やり直し (Ctrl+Y)";
        });
    }

    // ===== Search =====

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchQuery = SearchBox.Text;
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(_searchQuery) ? Visibility.Visible : Visibility.Collapsed;
        SearchClearBtn.Visibility = string.IsNullOrEmpty(_searchQuery) ? Visibility.Collapsed : Visibility.Visible;

        // 検索実行
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            _searchResults.Clear();
            _searchIndex = 0;
            SearchResultText.Text = "";
        }
        else
        {
            _searchResults = _vm.Items.Where(i => MatchesSearch(i, _searchQuery)).ToList();
            _searchIndex = 0;
            SearchResultText.Text = _searchResults.Count > 0
                ? $"{_searchResults.Count} 件"
                : "見つかりません";
        }

        RenderAll();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _searchResults.Count > 0)
        {
            // 次の検索結果へ
            _searchIndex = (_searchIndex + 1) % _searchResults.Count;
            var item = _searchResults[_searchIndex];
            ScrollToItem(item);
            SearchResultText.Text = $"{_searchIndex + 1}/{_searchResults.Count} 件";
            RenderAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ClearSearch();
            e.Handled = true;
        }
    }

    private void SearchClear_Click(object sender, RoutedEventArgs e)
    {
        ClearSearch();
    }

    private void ClearSearch()
    {
        SearchBox.Text = "";
        _searchQuery = "";
        _searchResults.Clear();
        _searchIndex = 0;
        SearchResultText.Text = "";
        SearchPlaceholder.Visibility = Visibility.Visible;
        SearchClearBtn.Visibility = Visibility.Collapsed;
        RenderAll();
    }

    private void ScrollToItem(PinItem item)
    {
        // アイテムの位置にキャンバスをパン
        CanvasTranslate.X = -item.X * _zoomLevel + ActualWidth / 2;
        CanvasTranslate.Y = -item.Y * _zoomLevel + ActualHeight / 2;
    }

    // ===== Selection =====

    private void SelectItem(PinItem item, bool addToSelection)
    {
        if (!addToSelection)
        {
            _selectedItems.Clear();
            _selectedGroups.Clear();
        }

        if (_selectedItems.Contains(item))
            _selectedItems.Remove(item);
        else
            _selectedItems.Add(item);

        RenderAll();
    }

    private void SelectGroup(PinGroup group, bool addToSelection)
    {
        if (!addToSelection)
        {
            _selectedItems.Clear();
            _selectedGroups.Clear();
        }

        if (_selectedGroups.Contains(group))
            _selectedGroups.Remove(group);
        else
            _selectedGroups.Add(group);

        RenderAll();
    }

    private void SelectAll()
    {
        _selectedItems = new HashSet<PinItem>(_vm.Items);
        _selectedGroups = new HashSet<PinGroup>(_vm.Groups);
        RenderAll();
    }

    private void ClearSelection()
    {
        _selectedItems.Clear();
        _selectedGroups.Clear();
        RenderAll();
    }

    private void DeleteSelected()
    {
        if (_selectedItems.Count == 0 && _selectedGroups.Count == 0) return;
        if (_vm.ActiveBoard == null) return;

        var msg = $"{_selectedItems.Count} アイテム、{_selectedGroups.Count} グループを削除しますか？";
        if (MessageBox.Show(msg, "削除確認", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        // Undoアクションを作成
        var action = new DeleteMultipleAction(
            _vm.ActiveBoard,
            _selectedItems.ToList(),
            _selectedGroups.ToList(),
            _vm.Items,
            _vm.Groups
        );
        _undoManager.Execute(action);

        _selectedItems.Clear();
        _selectedGroups.Clear();
        RenderAll();
        UpdateStatus();
        _vm.Save();
    }

    private void UpdateSelectionInfo()
    {
        var count = _selectedItems.Count + _selectedGroups.Count;
        SelectionInfoText.Text = count > 0 ? $"{count} 個選択中" : "";
    }

    // ===== Canvas Events =====

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Ctrlが押されていなければ選択をクリア
        if (Keyboard.Modifiers != ModifierKeys.Control && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            ClearSelection();
        }

        // 範囲選択開始
        _isSelecting = true;
        _selectionStart = e.GetPosition(PinCanvas);
        SelectionRect.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRect, _selectionStart.X);
        Canvas.SetTop(SelectionRect, _selectionStart.Y);
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
        PinCanvas.CaptureMouse();
    }

    private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // キャンバス右クリック → 追加メニュー
        var pos = e.GetPosition(PinCanvas);

        var menu = new ContextMenu();

        var addNoteItem = new MenuItem { Header = "📝 メモ追加" };
        addNoteItem.Click += (_, _) =>
        {
            _vm.AddNoteAt(pos.X, pos.Y);
            RenderAll();
            UpdateStatus();
        };
        menu.Items.Add(addNoteItem);

        var addGroupItem = new MenuItem { Header = "📁 グループ追加" };
        addGroupItem.Click += (_, _) =>
        {
            _vm.AddGroupAt(pos.X, pos.Y);
            RenderAll();
            UpdateStatus();
        };
        menu.Items.Add(addGroupItem);

        menu.IsOpen = true;
    }

    // ===== Keyboard Shortcuts =====

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // 検索ボックスにフォーカスがある場合は一部のショートカットをスキップ
        if (SearchBox.IsFocused && e.Key != Key.Escape && e.Key != Key.Enter)
            return;

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.S:
                    _vm.Save();
                    StatusText.Text = "保存しました";
                    e.Handled = true;
                    break;
                case Key.Z:
                    Undo_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.Y:
                    Redo_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.F:
                    SearchBox.Focus();
                    SearchBox.SelectAll();
                    e.Handled = true;
                    break;
                case Key.A:
                    SelectAll();
                    e.Handled = true;
                    break;
                case Key.OemPlus:
                case Key.Add:
                    ZoomIn_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.OemMinus:
                case Key.Subtract:
                    ZoomOut_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.D0:
                case Key.NumPad0:
                    ZoomReset_Click(sender, e);
                    e.Handled = true;
                    break;
            }
        }
        else
        {
            switch (e.Key)
            {
                case Key.N:
                    if (!SearchBox.IsFocused)
                    {
                        AddNote_Click(sender, e);
                        e.Handled = true;
                    }
                    break;
                case Key.G:
                    if (!SearchBox.IsFocused)
                    {
                        AddGroup_Click(sender, e);
                        e.Handled = true;
                    }
                    break;
                case Key.Delete:
                    DeleteSelected();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    if (!string.IsNullOrEmpty(_searchQuery))
                        ClearSearch();
                    else
                        ClearSelection();
                    e.Handled = true;
                    break;
            }
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _vm.Save();
    }

    // ===== Utilities =====

    private void UpdateStatus()
    {
        var itemCount = _vm.Items.Count;
        var groupCount = _vm.Groups.Count;
        ItemCountText.Text = $"{itemCount} アイテム, {groupCount} グループ";
        StatusText.Text = $"{_vm.ActiveBoard?.Name ?? ""} — 準備完了";
        EmptyHint.Visibility = (itemCount == 0 && groupCount == 0) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CenterEmptyHint()
    {
        Canvas.SetLeft(EmptyHint, (PinCanvas.ActualWidth / _zoomLevel - 400) / 2);
        Canvas.SetTop(EmptyHint, (PinCanvas.ActualHeight / _zoomLevel - 40) / 2);
    }

    private double SnapToGrid(double value)
    {
        return Math.Round(value / GridSize) * GridSize;
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
