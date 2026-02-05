using System;
using System.Collections.Generic;

namespace InsightPinboard.Services;

/// <summary>
/// Undo可能なアクションのインターフェース
/// </summary>
public interface IUndoableAction
{
    string Description { get; }
    void Execute();
    void Undo();
}

/// <summary>
/// Undo/Redo管理クラス
/// </summary>
public class UndoManager
{
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();
    private const int MaxUndoCount = 50;

    public event EventHandler? StateChanged;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string? UndoDescription => _undoStack.Count > 0 ? _undoStack.Peek().Description : null;
    public string? RedoDescription => _redoStack.Count > 0 ? _redoStack.Peek().Description : null;

    /// <summary>
    /// アクションを実行してUndoスタックに追加
    /// </summary>
    public void Execute(IUndoableAction action)
    {
        action.Execute();
        _undoStack.Push(action);
        _redoStack.Clear();

        // スタックサイズ制限
        if (_undoStack.Count > MaxUndoCount)
        {
            var temp = new Stack<IUndoableAction>();
            for (int i = 0; i < MaxUndoCount; i++)
                temp.Push(_undoStack.Pop());
            _undoStack.Clear();
            while (temp.Count > 0)
                _undoStack.Push(temp.Pop());
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 最後のアクションを取り消す
    /// </summary>
    public void Undo()
    {
        if (!CanUndo) return;

        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 取り消したアクションをやり直す
    /// </summary>
    public void Redo()
    {
        if (!CanRedo) return;

        var action = _redoStack.Pop();
        action.Execute();
        _undoStack.Push(action);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// スタックをクリア
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

// ===== 具体的なアクション実装 =====

/// <summary>
/// アイテム移動アクション
/// </summary>
public class MoveItemAction : IUndoableAction
{
    private readonly Models.PinItem _item;
    private readonly double _oldX, _oldY;
    private readonly double _newX, _newY;

    public string Description => "アイテム移動";

    public MoveItemAction(Models.PinItem item, double oldX, double oldY, double newX, double newY)
    {
        _item = item;
        _oldX = oldX;
        _oldY = oldY;
        _newX = newX;
        _newY = newY;
    }

    public void Execute()
    {
        _item.X = _newX;
        _item.Y = _newY;
    }

    public void Undo()
    {
        _item.X = _oldX;
        _item.Y = _oldY;
    }
}

/// <summary>
/// 複数アイテム移動アクション
/// </summary>
public class MoveMultipleItemsAction : IUndoableAction
{
    private readonly List<(Models.PinItem item, double oldX, double oldY, double newX, double newY)> _moves;

    public string Description => $"{_moves.Count}個のアイテム移動";

    public MoveMultipleItemsAction(List<(Models.PinItem, double, double, double, double)> moves)
    {
        _moves = moves;
    }

    public void Execute()
    {
        foreach (var (item, _, _, newX, newY) in _moves)
        {
            item.X = newX;
            item.Y = newY;
        }
    }

    public void Undo()
    {
        foreach (var (item, oldX, oldY, _, _) in _moves)
        {
            item.X = oldX;
            item.Y = oldY;
        }
    }
}

/// <summary>
/// グループ移動アクション
/// </summary>
public class MoveGroupAction : IUndoableAction
{
    private readonly Models.PinGroup _group;
    private readonly double _oldX, _oldY;
    private readonly double _newX, _newY;

    public string Description => "グループ移動";

    public MoveGroupAction(Models.PinGroup group, double oldX, double oldY, double newX, double newY)
    {
        _group = group;
        _oldX = oldX;
        _oldY = oldY;
        _newX = newX;
        _newY = newY;
    }

    public void Execute()
    {
        _group.X = _newX;
        _group.Y = _newY;
    }

    public void Undo()
    {
        _group.X = _oldX;
        _group.Y = _oldY;
    }
}

/// <summary>
/// アイテム追加アクション
/// </summary>
public class AddItemAction : IUndoableAction
{
    private readonly Models.Board _board;
    private readonly Models.PinItem _item;
    private readonly System.Collections.ObjectModel.ObservableCollection<Models.PinItem> _items;

    public string Description => "アイテム追加";

    public AddItemAction(Models.Board board, Models.PinItem item,
        System.Collections.ObjectModel.ObservableCollection<Models.PinItem> items)
    {
        _board = board;
        _item = item;
        _items = items;
    }

    public void Execute()
    {
        if (!_board.Items.Contains(_item))
            _board.Items.Add(_item);
        if (!_items.Contains(_item))
            _items.Add(_item);
    }

    public void Undo()
    {
        _board.Items.Remove(_item);
        _items.Remove(_item);
    }
}

/// <summary>
/// アイテム削除アクション
/// </summary>
public class DeleteItemAction : IUndoableAction
{
    private readonly Models.Board _board;
    private readonly Models.PinItem _item;
    private readonly System.Collections.ObjectModel.ObservableCollection<Models.PinItem> _items;

    public string Description => "アイテム削除";

    public DeleteItemAction(Models.Board board, Models.PinItem item,
        System.Collections.ObjectModel.ObservableCollection<Models.PinItem> items)
    {
        _board = board;
        _item = item;
        _items = items;
    }

    public void Execute()
    {
        _board.Items.Remove(_item);
        _items.Remove(_item);
    }

    public void Undo()
    {
        if (!_board.Items.Contains(_item))
            _board.Items.Add(_item);
        if (!_items.Contains(_item))
            _items.Add(_item);
    }
}

/// <summary>
/// グループ追加アクション
/// </summary>
public class AddGroupAction : IUndoableAction
{
    private readonly Models.Board _board;
    private readonly Models.PinGroup _group;
    private readonly System.Collections.ObjectModel.ObservableCollection<Models.PinGroup> _groups;

    public string Description => "グループ追加";

    public AddGroupAction(Models.Board board, Models.PinGroup group,
        System.Collections.ObjectModel.ObservableCollection<Models.PinGroup> groups)
    {
        _board = board;
        _group = group;
        _groups = groups;
    }

    public void Execute()
    {
        if (!_board.Groups.Contains(_group))
            _board.Groups.Add(_group);
        if (!_groups.Contains(_group))
            _groups.Add(_group);
    }

    public void Undo()
    {
        _board.Groups.Remove(_group);
        _groups.Remove(_group);
    }
}

/// <summary>
/// グループ削除アクション
/// </summary>
public class DeleteGroupAction : IUndoableAction
{
    private readonly Models.Board _board;
    private readonly Models.PinGroup _group;
    private readonly System.Collections.ObjectModel.ObservableCollection<Models.PinGroup> _groups;

    public string Description => "グループ削除";

    public DeleteGroupAction(Models.Board board, Models.PinGroup group,
        System.Collections.ObjectModel.ObservableCollection<Models.PinGroup> groups)
    {
        _board = board;
        _group = group;
        _groups = groups;
    }

    public void Execute()
    {
        _board.Groups.Remove(_group);
        _groups.Remove(_group);
    }

    public void Undo()
    {
        if (!_board.Groups.Contains(_group))
            _board.Groups.Add(_group);
        if (!_groups.Contains(_group))
            _groups.Add(_group);
    }
}

/// <summary>
/// 複数削除アクション
/// </summary>
public class DeleteMultipleAction : IUndoableAction
{
    private readonly Models.Board _board;
    private readonly List<Models.PinItem> _items;
    private readonly List<Models.PinGroup> _groups;
    private readonly System.Collections.ObjectModel.ObservableCollection<Models.PinItem> _itemsCollection;
    private readonly System.Collections.ObjectModel.ObservableCollection<Models.PinGroup> _groupsCollection;

    public string Description => $"{_items.Count + _groups.Count}個削除";

    public DeleteMultipleAction(
        Models.Board board,
        List<Models.PinItem> items,
        List<Models.PinGroup> groups,
        System.Collections.ObjectModel.ObservableCollection<Models.PinItem> itemsCollection,
        System.Collections.ObjectModel.ObservableCollection<Models.PinGroup> groupsCollection)
    {
        _board = board;
        _items = new List<Models.PinItem>(items);
        _groups = new List<Models.PinGroup>(groups);
        _itemsCollection = itemsCollection;
        _groupsCollection = groupsCollection;
    }

    public void Execute()
    {
        foreach (var item in _items)
        {
            _board.Items.Remove(item);
            _itemsCollection.Remove(item);
        }
        foreach (var group in _groups)
        {
            _board.Groups.Remove(group);
            _groupsCollection.Remove(group);
        }
    }

    public void Undo()
    {
        foreach (var item in _items)
        {
            if (!_board.Items.Contains(item))
                _board.Items.Add(item);
            if (!_itemsCollection.Contains(item))
                _itemsCollection.Add(item);
        }
        foreach (var group in _groups)
        {
            if (!_board.Groups.Contains(group))
                _board.Groups.Add(group);
            if (!_groupsCollection.Contains(group))
                _groupsCollection.Add(group);
        }
    }
}

/// <summary>
/// リサイズアクション
/// </summary>
public class ResizeAction : IUndoableAction
{
    private readonly object _target;
    private readonly double _oldWidth, _oldHeight;
    private readonly double _newWidth, _newHeight;

    public string Description => "サイズ変更";

    public ResizeAction(object target, double oldWidth, double oldHeight, double newWidth, double newHeight)
    {
        _target = target;
        _oldWidth = oldWidth;
        _oldHeight = oldHeight;
        _newWidth = newWidth;
        _newHeight = newHeight;
    }

    public void Execute()
    {
        if (_target is Models.PinItem item)
        {
            item.Width = _newWidth;
            item.Height = _newHeight;
        }
        else if (_target is Models.PinGroup group)
        {
            group.Width = _newWidth;
            group.Height = _newHeight;
        }
    }

    public void Undo()
    {
        if (_target is Models.PinItem item)
        {
            item.Width = _oldWidth;
            item.Height = _oldHeight;
        }
        else if (_target is Models.PinGroup group)
        {
            group.Width = _oldWidth;
            group.Height = _oldHeight;
        }
    }
}

/// <summary>
/// プロパティ変更アクション（色、名前など）
/// </summary>
public class PropertyChangeAction : IUndoableAction
{
    private readonly object _target;
    private readonly string _propertyName;
    private readonly object? _oldValue;
    private readonly object? _newValue;

    public string Description { get; }

    public PropertyChangeAction(object target, string propertyName, object? oldValue, object? newValue, string description)
    {
        _target = target;
        _propertyName = propertyName;
        _oldValue = oldValue;
        _newValue = newValue;
        Description = description;
    }

    public void Execute()
    {
        SetProperty(_newValue);
    }

    public void Undo()
    {
        SetProperty(_oldValue);
    }

    private void SetProperty(object? value)
    {
        var prop = _target.GetType().GetProperty(_propertyName);
        prop?.SetValue(_target, value);
    }
}
