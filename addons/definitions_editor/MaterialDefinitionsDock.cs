#if TOOLS
using Godot;
using System;
using System.Collections.Generic;

namespace MySimCity.EditorTools;

/// <summary>
/// 「材料显示名定义」编辑器 Dock：左侧定义列表 + 右侧显示名表单。
/// 负责 res://Data/Materials/*.tres 的增删改查与保存。
/// 材料 Id 为字符串（与文件名一致），在新建/复制时指定。
/// </summary>
[Tool]
public partial class MaterialDefinitionsDock : Control
{
	private ItemList _list;
	private Label _statusLabel;
	private Button _duplicateButton;
	private Button _deleteButton;
	private Button _saveButton;
	private LineEdit _idEdit;
	private LineEdit _nameEdit;
	private AcceptDialog _errorDialog;
	private ConfirmationDialog _deleteDialog;

	private readonly List<MaterialDefinition> _definitions = new();
	private MaterialDefinition _selected;
	private MaterialDefinition _pendingDelete;
	private int _currentIndex = -1;
	private bool _loading;
	private bool _dirty;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(380, 260);
		BuildUi();

		_errorDialog = new AcceptDialog { Title = "错误" };
		AddChild(_errorDialog);

		_deleteDialog = new ConfirmationDialog { Title = "删除材料显示名定义" };
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

		// ---------- 左侧：列表 ----------
		var left = new VBoxContainer { CustomMinimumSize = new Vector2(200, 0) };
		split.AddChild(left);

		left.AddChild(MakeSection("材料显示名定义"));

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

		var listButtons = new HBoxContainer();
		var newButton = new Button { Text = "新建", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		newButton.Pressed += OnNewPressed;
		_duplicateButton = new Button { Text = "复制", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_duplicateButton.Pressed += OnDuplicatePressed;
		_deleteButton = new Button { Text = "删除", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_deleteButton.Pressed += OnDeletePressed;
		listButtons.AddChild(newButton);
		listButtons.AddChild(_duplicateButton);
		listButtons.AddChild(_deleteButton);
		left.AddChild(listButtons);

		var refreshButton = new Button { Text = "刷新列表" };
		refreshButton.Pressed += () => RefreshList();
		left.AddChild(refreshButton);

		// ---------- 右侧：表单 ----------
		var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		split.AddChild(form);

		form.AddChild(MakeSection("显示名配置"));
		_idEdit = new LineEdit
		{
			Editable = false,
			TooltipText = "Id 在新建/复制时指定，与文件名一致"
		};
		form.AddChild(MakeField("Id", _idEdit));

		_nameEdit = new LineEdit { PlaceholderText = "原木" };
		_nameEdit.TextChanged += _ => MarkDirty();
		form.AddChild(MakeField("显示名", _nameEdit));

		_saveButton = new Button { Text = "保存" };
		_saveButton.Pressed += OnSavePressed;
		form.AddChild(_saveButton);

		_statusLabel = new Label
		{
			Modulate = new Color(0.7f, 0.7f, 0.7f),
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		form.AddChild(_statusLabel);

		SetHasSelection(false);
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

	private void RefreshList(string selectId = null)
	{
		if (_dirty && _selected != null && !SaveSelected()) return;

		_definitions.Clear();
		_definitions.AddRange(MaterialDatabase.LoadAllFromDisk());
		_list.Clear();

		foreach (var def in _definitions)
			_list.AddItem($"{def.DisplayName} ({def.Id})");

		if (_definitions.Count == 0)
		{
			_currentIndex = -1;
			LoadForm(null);
			return;
		}

		int index = 0;
		if (selectId != null)
		{
			for (int i = 0; i < _definitions.Count; i++)
			{
				if (string.Equals(_definitions[i].Id, selectId, StringComparison.OrdinalIgnoreCase))
				{
					index = i;
					break;
				}
			}
		}

		_list.Select(index);
		_currentIndex = index;
		LoadForm(_definitions[index]);
	}

	private void OnItemSelected(long index)
	{
		var next = (int)index;
		if (_dirty && _selected != null && !SaveSelected())
		{
			_list.DeselectAll();
			if (_currentIndex >= 0 && _currentIndex < _list.ItemCount)
				_list.Select(_currentIndex);
			return;
		}

		_currentIndex = next;
		LoadForm(_definitions[next]);
	}

	private void LoadForm(MaterialDefinition def)
	{
		_selected = def;
		_dirty = false;
		_loading = true;

		SetHasSelection(def != null);

		_idEdit.Text = def?.Id ?? "";
		_nameEdit.Text = def?.DisplayName ?? "";

		_loading = false;
		UpdateStatus();
	}

	// ------------------------------------------------------------------
	// 保存
	// ------------------------------------------------------------------

	private void OnSavePressed()
	{
		if (_selected != null)
			SaveSelected();
	}

	private bool SaveSelected()
	{
		if (_selected == null) return true;

		_selected.DisplayName = _nameEdit.Text.StripEdges();

		var errors = new List<string>(_selected.Validate());
		foreach (var other in _definitions)
		{
			if (other != _selected && string.Equals(other.Id, _selected.Id, StringComparison.OrdinalIgnoreCase))
				errors.Add("Id 与其他定义重复");
		}

		if (errors.Count > 0)
		{
			ShowError($"无法保存：\n{string.Join("\n", errors)}");
			return false;
		}

		if (string.IsNullOrEmpty(_selected.ResourcePath))
		{
			ShowError("资源路径为空，无法保存");
			return false;
		}

		var err = ResourceSaver.Save(_selected, _selected.ResourcePath);
		if (err != Error.Ok)
		{
			ShowError($"保存失败：{err}");
			return false;
		}

		_dirty = false;
		if (_currentIndex >= 0 && _currentIndex < _list.ItemCount)
			_list.SetItemText(_currentIndex, $"{_selected.DisplayName} ({_selected.Id})");
		UpdateStatus();
		return true;
	}

	// ------------------------------------------------------------------
	// 新建 / 复制 / 删除
	// ------------------------------------------------------------------

	private void OnNewPressed()
	{
		ShowIdPrompt("新建材料", "", id =>
		{
			if (!IsValidNewId(id, out var error))
			{
				ShowError(error);
				return;
			}
			CreateDefinition(id, null);
		});
	}

	private void OnDuplicatePressed()
	{
		if (_selected == null) return;
		ShowIdPrompt("复制材料", SuggestCopyId(_selected.Id), id =>
		{
			if (!IsValidNewId(id, out var error))
			{
				ShowError(error);
				return;
			}
			CreateDefinition(id, _selected);
		});
	}

	private string SuggestCopyId(string id)
	{
		for (int i = 2; ; i++)
		{
			var candidate = i == 2 ? $"{id}_copy" : $"{id}_copy{i}";
			if (!ResourceLoader.Exists($"{MaterialDatabase.FolderPath}/{candidate}.tres"))
				return candidate;
		}
	}

	private void CreateDefinition(string id, MaterialDefinition source)
	{
		MaterialDefinition def;
		if (source != null)
		{
			def = (MaterialDefinition)source.Duplicate(true);
			def.Id = id;
			def.ResourcePath = "";
		}
		else
		{
			def = new MaterialDefinition { Id = id, DisplayName = id };
		}

		var path = $"{MaterialDatabase.FolderPath}/{id}.tres";
		var err = ResourceSaver.Save(def, path);
		if (err != Error.Ok)
		{
			ShowError($"创建失败：{err}");
			return;
		}

		_dirty = false;
		_selected = null;
		RefreshList(id);
	}

	private void OnDeletePressed()
	{
		if (_selected == null) return;
		_pendingDelete = _selected;
		_deleteDialog.DialogText = $"确定删除 {_selected.DisplayName} ({_selected.Id})？\n文件将从磁盘移除。";
		_deleteDialog.PopupCentered(new Vector2I(380, 150));
	}

	private void DoDelete()
	{
		if (_pendingDelete == null) return;

		var path = _pendingDelete.ResourcePath;
		if (string.IsNullOrEmpty(path))
			path = $"{MaterialDatabase.FolderPath}/{_pendingDelete.Id}.tres";

		var err = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
		if (err != Error.Ok)
		{
			ShowError($"删除失败：{err}");
			_pendingDelete = null;
			return;
		}

		_pendingDelete = null;
		_selected = null;
		_dirty = false;
		RefreshList();
	}

	// ------------------------------------------------------------------
	// 弹窗与工具
	// ------------------------------------------------------------------

	private bool IsValidNewId(string id, out string error)
	{
		error = "";
		if (string.IsNullOrWhiteSpace(id))
		{
			error = "Id 不能为空";
			return false;
		}
		if (!DefinitionIdValidation.IsValid(id))
		{
			error = DefinitionIdValidation.ErrorMessage;
			return false;
		}
		foreach (var def in _definitions)
		{
			if (string.Equals(def.Id, id, StringComparison.OrdinalIgnoreCase))
			{
				error = "Id 已存在";
				return false;
			}
		}
		if (ResourceLoader.Exists($"{MaterialDatabase.FolderPath}/{id}.tres"))
		{
			error = "同名文件已存在";
			return false;
		}
		return true;
	}

	private void ShowIdPrompt(string title, string defaultId, Action<string> onConfirm)
	{
		var dialog = new AcceptDialog
		{
			Title = title,
			InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen
		};

		var content = new VBoxContainer();
		content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		content.OffsetLeft = 12;
		content.OffsetTop = 12;
		content.OffsetRight = -12;
		content.OffsetBottom = -44;

		var hint = new Label
		{
			Text = "Id（小写字母开头，仅小写字母/数字/下划线）：",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		var input = new LineEdit { Text = defaultId, PlaceholderText = "wood" };

		content.AddChild(hint);
		content.AddChild(input);
		dialog.AddChild(content);
		AddChild(dialog);

		dialog.PopupCentered(new Vector2I(380, 170));
		input.GrabFocus();
		input.SelectAll();

		dialog.Confirmed += () =>
		{
			var id = input.Text.StripEdges().ToLowerInvariant();
			if (!string.IsNullOrEmpty(id))
				onConfirm(id);
			dialog.QueueFree();
		};
		dialog.Canceled += () => dialog.QueueFree();
	}

	private void ShowError(string message)
	{
		_errorDialog.DialogText = message;
		_errorDialog.PopupCentered(new Vector2I(380, 140));
	}

	private void SetHasSelection(bool has)
	{
		_duplicateButton.Disabled = !has;
		_deleteButton.Disabled = !has;
		_saveButton.Disabled = !has;
	}

	private void MarkDirty()
	{
		if (_loading) return;
		_dirty = true;
		UpdateStatus();
	}

	private void UpdateStatus()
	{
		if (_selected == null)
		{
			_statusLabel.Text = "未选中任何定义";
			return;
		}
		_statusLabel.Text = (_dirty ? "● 未保存  " : "") + _selected.ResourcePath;
	}
}
#endif
