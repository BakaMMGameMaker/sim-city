#if TOOLS
using Godot;
using System;
using System.Collections.Generic;

namespace MySimCity.EditorTools;

/// <summary>
/// 「材料类型定义」编辑器 Dock：列举所有 MaterialType 及其显示名，
/// 负责 res://Data/Materials/*.tres 的保存与删除。
/// MaterialType 枚举决定材料全集；本面板只维护每个枚举值的显示名配置。
/// </summary>
[Tool]
public partial class MaterialDefinitionsDock : Control
{
	private ItemList _list;
	private Label _idLabel;
	private LineEdit _nameEdit;
	private Label _statusLabel;
	private Button _saveButton;
	private Button _deleteButton;
	private AcceptDialog _errorDialog;
	private ConfirmationDialog _deleteDialog;

	private readonly List<MaterialType> _materials = new();
	private readonly Dictionary<MaterialType, MaterialDefinition> _loadedDefs = new();
	private MaterialType? _selected;
	private int _currentIndex = -1;
	private bool _loading;
	private bool _dirty;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(380, 260);
		BuildUi();

		_errorDialog = new AcceptDialog { Title = "错误" };
		AddChild(_errorDialog);

		_deleteDialog = new ConfirmationDialog { Title = "删除材料显示名配置" };
		_deleteDialog.Confirmed += DoDelete;
		AddChild(_deleteDialog);

		if (!DirAccess.DirExistsAbsolute(MaterialDatabase.FolderPath))
			DirAccess.MakeDirRecursiveAbsolute(MaterialDatabase.FolderPath);

		RefreshList();
	}

	// ------------------------------------------------------------------
	// UI 构建
	// ------------------------------------------------------------------

	private void BuildUi()
	{
		var split = new HSplitContainer();
		split.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		split.SplitOffsets = new[] { 220 };
		AddChild(split);

		// ---------- 左侧：全部 MaterialType 列表 ----------
		var left = new VBoxContainer { CustomMinimumSize = new Vector2(200, 0) };
		split.AddChild(left);

		left.AddChild(MakeSection("材料类型"));

		var folderLabel = new Label
		{
			Text = MaterialDatabase.FolderPath,
			Modulate = new Color(0.7f, 0.7f, 0.7f),
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		left.AddChild(folderLabel);

		_list = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
		_list.ItemSelected += OnItemSelected;
		left.AddChild(_list);

		var refreshButton = new Button { Text = "刷新列表" };
		refreshButton.Pressed += () => RefreshList();
		left.AddChild(refreshButton);

		// ---------- 右侧：表单 ----------
		var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		split.AddChild(form);

		form.AddChild(MakeSection("显示名配置"));
		_idLabel = new Label { Text = "" };
		form.AddChild(MakeField("Id", _idLabel));

		_nameEdit = new LineEdit { PlaceholderText = "原木" };
		_nameEdit.TextChanged += _ => MarkDirty();
		form.AddChild(MakeField("显示名", _nameEdit));

		var buttonRow = new HBoxContainer();
		_saveButton = new Button { Text = "保存", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_saveButton.Pressed += OnSavePressed;
		_deleteButton = new Button { Text = "删除配置", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_deleteButton.Pressed += OnDeletePressed;
		buttonRow.AddChild(_saveButton);
		buttonRow.AddChild(_deleteButton);
		form.AddChild(buttonRow);

		_statusLabel = new Label
		{
			Modulate = new Color(0.7f, 0.7f, 0.7f),
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		form.AddChild(_statusLabel);
	}

	private static Control MakeSection(string title)
	{
		var box = new VBoxContainer();
		box.AddChild(new HSeparator());
		box.AddChild(new Label { Text = title, ThemeTypeVariation = "HeaderSmall" });
		return box;
	}

	private static Control MakeField(string label, Control input)
	{
		var row = new HBoxContainer();
		row.AddChild(new Label
		{
			Text = label,
			CustomMinimumSize = new Vector2(70, 0),
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		});
		input.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(input);
		return row;
	}

	// ------------------------------------------------------------------
	// 列表与选中
	// ------------------------------------------------------------------

	private void RefreshList()
	{
		if (_dirty && _selected.HasValue && !SaveSelected()) return;

		_loadedDefs.Clear();
		foreach (var def in MaterialDatabase.LoadAllFromDisk())
			_loadedDefs[def.Id] = def;

		_materials.Clear();
		var values = new List<MaterialType>(Enum.GetValues<MaterialType>());
		values.Sort((a, b) => ((int)a).CompareTo((int)b));
		_materials.AddRange(values);

		_list.Clear();
		foreach (var id in _materials)
			_list.AddItem(ListLabel(id));

		_currentIndex = 0;
		_list.Select(0);
		LoadForm(_materials[0]);
	}

	private static string ListLabel(MaterialType id)
	{
		var def = FindOnDisk(id);
		return def != null ? $"{def.DisplayName} ({id})" : $"{id}（未配置）";
	}

	private static MaterialDefinition FindOnDisk(MaterialType id)
	{
		foreach (var def in MaterialDatabase.LoadAllFromDisk())
		{
			if (def.Id == id)
				return def;
		}
		return null;
	}

	private void OnItemSelected(long index)
	{
		var next = (int)index;
		if (_dirty && _selected.HasValue && !SaveSelected())
		{
			_list.DeselectAll();
			_list.Select(_currentIndex);
			return;
		}

		_currentIndex = next;
		LoadForm(_materials[next]);
	}

	private void LoadForm(MaterialType id)
	{
		_selected = id;
		_dirty = false;
		_loading = true;

		_idLabel.Text = id.ToString();
		_nameEdit.Text = _loadedDefs.TryGetValue(id, out var def) ? def.DisplayName : "";
		_nameEdit.PlaceholderText = MaterialDatabase.FallbackName(id);

		_loading = false;
		UpdateStatus();
	}

	// ------------------------------------------------------------------
	// 保存 / 删除
	// ------------------------------------------------------------------

	private void OnSavePressed()
	{
		if (_selected.HasValue)
			SaveSelected();
	}

	private bool SaveSelected()
	{
		if (!_selected.HasValue) return true;

		var id = _selected.Value;
		var name = _nameEdit.Text.StripEdges();
		if (string.IsNullOrEmpty(name))
		{
			ShowError("显示名不能为空");
			return false;
		}

		MaterialDefinition def;
		if (_loadedDefs.TryGetValue(id, out var existing))
		{
			def = existing;
			def.DisplayName = name;
		}
		else
		{
			def = new MaterialDefinition { Id = id, DisplayName = name };
		}

		var path = PathFor(id);
		var err = ResourceSaver.Save(def, path);
		if (err != Error.Ok)
		{
			ShowError($"保存失败：{err}");
			return false;
		}

		_loadedDefs[id] = def;
		_dirty = false;
		if (_currentIndex >= 0 && _currentIndex < _list.ItemCount)
			_list.SetItemText(_currentIndex, ListLabel(id));
		UpdateStatus();
		return true;
	}

	private void OnDeletePressed()
	{
		if (!_selected.HasValue) return;
		_deleteDialog.DialogText = $"确定删除 {_selected.Value} 的显示名配置？\n文件将从磁盘移除，显示名回退为枚举名。";
		_deleteDialog.PopupCentered(new Vector2I(380, 150));
	}

	private void DoDelete()
	{
		if (!_selected.HasValue) return;
		var id = _selected.Value;

		var path = PathFor(id);
		var err = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
		if (err != Error.Ok && err != Error.FileNotFound)
		{
			ShowError($"删除失败：{err}");
			return;
		}

		_loadedDefs.Remove(id);
		_dirty = false;
		if (_currentIndex >= 0 && _currentIndex < _list.ItemCount)
			_list.SetItemText(_currentIndex, ListLabel(id));
		LoadForm(id);
	}

	// ------------------------------------------------------------------
	// 工具
	// ------------------------------------------------------------------

	/// <summary>材料 Id 对应的 .tres 路径：res://Data/Materials/{枚举名小写}.tres</summary>
	private static string PathFor(MaterialType id)
	{
		return $"{MaterialDatabase.FolderPath}/{id.ToString().ToLowerInvariant()}.tres";
	}

	private void ShowError(string message)
	{
		_errorDialog.DialogText = message;
		_errorDialog.PopupCentered(new Vector2I(380, 140));
	}

	private void MarkDirty()
	{
		if (_loading) return;
		_dirty = true;
		UpdateStatus();
	}

	private void UpdateStatus()
	{
		if (!_selected.HasValue)
		{
			_statusLabel.Text = "未选中任何材料";
			return;
		}

		var id = _selected.Value;
		_statusLabel.Text = (_dirty ? "● 未保存  " : "")
			+ (_loadedDefs.ContainsKey(id) ? PathFor(id) : "未配置（使用枚举名回退）");
	}
}
#endif
