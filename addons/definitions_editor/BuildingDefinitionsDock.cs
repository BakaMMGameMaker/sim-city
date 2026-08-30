#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using MySimCity.Definitions;

namespace MySimCity.EditorTools;

/// <summary>
/// 「建筑类型定义」编辑器 Dock：左侧定义列表 + 右侧自绘表单。
/// 负责 res://Data/Buildings/*.tres 的增删改查与保存。
/// 建筑 Id 为 uint（自动分配，与文件名一致），列表按 Id 排序。
/// </summary>
[Tool]
public partial class BuildingDefinitionsDock : Control
{
	private ItemList _list;
	private Label _statusLabel;
	private Button _duplicateButton;
	private Button _deleteButton;
	private Button _saveButton;
	private Button _saveAllButton;
	private Button _addCostButton;
	private Button _addProductionButton;

	private LineEdit _idEdit;
	private LineEdit _nameEdit;
	private SpinBox _widthSpin;
	private SpinBox _depthSpin;
	private SpinBox _heightSpin;
	private SpinBox _buildTimeSpin;
	private SpinBox _foundationXSpin;
	private SpinBox _foundationZSpin;
	private OptionButton _bodyAlignOption;
	private SpinBox _bodyOffsetXSpin;
	private SpinBox _bodyOffsetZSpin;
	private VBoxContainer _costsBox;
	private VBoxContainer _productionBox;

	private readonly List<BuildingDefinition> _definitions = new();
	private readonly List<Button> _selectionButtons = new();
	private BuildingDefinition _selected;
	private BuildingDefinition _pendingDelete;
	private AcceptDialog _errorDialog;
	private ConfirmationDialog _deleteDialog;
	private int _currentIndex = -1;
	private bool _loading;
	private bool _dirty;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(430, 320);
		BuildUi();

		_errorDialog = new AcceptDialog { Title = "错误" };
		AddChild(_errorDialog);

		_deleteDialog = new ConfirmationDialog { Title = "删除建筑类型" };
		_deleteDialog.Confirmed += DoDelete;
		AddChild(_deleteDialog);

		if (!DirAccess.DirExistsAbsolute(BuildingDefinitionDatabase.FolderPath))
			DirAccess.MakeDirRecursiveAbsolute(BuildingDefinitionDatabase.FolderPath);

		RefreshList();
	}

	// ------------------------------------------------------------------
	// UI 构建
	// ------------------------------------------------------------------

	private void BuildUi()
	{
		var split = new HSplitContainer();
		split.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		split.SplitOffsets = new[] { 250 };
		AddChild(split);

		// ---------- 左侧：列表 ----------
		var left = new VBoxContainer { CustomMinimumSize = new Vector2(230, 0) };
		split.AddChild(left);

		left.AddChild(MakeSection("建筑类型定义"));

		var folderLabel = new Label
		{
			Text = BuildingDefinitionDatabase.FolderPath,
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
		_selectionButtons.Add(_duplicateButton);
		_selectionButtons.Add(_deleteButton);

		var refreshButton = new Button { Text = "刷新列表" };
		refreshButton.Pressed += () => RefreshList();
		left.AddChild(refreshButton);

		// ---------- 右侧：表单 ----------
		var scroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		split.AddChild(scroll);

		var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		scroll.AddChild(form);

		form.AddChild(MakeSection("基本信息"));
		_idEdit = new LineEdit
		{
			Editable = false,
			TooltipText = "数字 Id：新建/复制时自动分配，作为运行时排序键；文件名约定为 {Id}_{显示名}.tres"
		};
		form.AddChild(MakeField("Id", _idEdit));
		_nameEdit = new LineEdit { PlaceholderText = "住宅" };
		_nameEdit.TextChanged += _ => MarkDirty();
		form.AddChild(MakeField("显示名", _nameEdit));

		form.AddChild(MakeSection("尺寸"));
		_widthSpin = MakeSpin(0.1, 100, 0.1, " m");
		_widthSpin.ValueChanged += _ => MarkDirty();
		form.AddChild(MakeField("宽", _widthSpin));
		_depthSpin = MakeSpin(0.1, 100, 0.1, " m");
		_depthSpin.ValueChanged += _ => MarkDirty();
		form.AddChild(MakeField("深", _depthSpin));
		_heightSpin = MakeSpin(0.1, 200, 0.1, " m");
		_heightSpin.ValueChanged += _ => MarkDirty();
		form.AddChild(MakeField("高", _heightSpin));
		_buildTimeSpin = MakeSpin(0.1, 3600, 0.1, " s");
		_buildTimeSpin.ValueChanged += _ => MarkDirty();
		form.AddChild(MakeField("建造时间", _buildTimeSpin));

		_foundationXSpin = MakeSpin(1, 64, 1);
		_foundationXSpin.ValueChanged += _ => MarkDirty();
		_foundationZSpin = MakeSpin(1, 64, 1);
		_foundationZSpin.ValueChanged += _ => MarkDirty();
		var foundationRow = new HBoxContainer();
		foundationRow.AddChild(_foundationXSpin);
		foundationRow.AddChild(_foundationZSpin);
		form.AddChild(MakeField("地基(格) X×Z", foundationRow));

		form.AddChild(MakeSection("摆放"));
		_bodyAlignOption = new OptionButton();
		_bodyAlignOption.AddItem("居中 (Center)", 0);
		_bodyAlignOption.AddItem("偏移 (Offset)", 1);
		_bodyAlignOption.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_bodyAlignOption.ItemSelected += _ => MarkDirty();
		form.AddChild(MakeField("对齐", _bodyAlignOption));
		_bodyOffsetXSpin = MakeSpin(-100, 100, 0.1, " m");
		_bodyOffsetXSpin.ValueChanged += _ => MarkDirty();
		form.AddChild(MakeField("偏移 X", _bodyOffsetXSpin));
		_bodyOffsetZSpin = MakeSpin(-100, 100, 0.1, " m");
		_bodyOffsetZSpin.ValueChanged += _ => MarkDirty();
		form.AddChild(MakeField("偏移 Z", _bodyOffsetZSpin));

		form.AddChild(MakeSection("建造成本"));
		_costsBox = new VBoxContainer();
		form.AddChild(_costsBox);
		_addCostButton = new Button { Text = "＋ 添加成本" };
		_addCostButton.Pressed += OnAddCostPressed;
		form.AddChild(_addCostButton);
		_selectionButtons.Add(_addCostButton);

		form.AddChild(MakeSection("产出表（按等级，留空则不产出）"));
		_productionBox = new VBoxContainer();
		form.AddChild(_productionBox);
		_addProductionButton = new Button { Text = "＋ 添加等级" };
		_addProductionButton.Pressed += OnAddProductionPressed;
		form.AddChild(_addProductionButton);
		_selectionButtons.Add(_addProductionButton);

		var saveRow = new HBoxContainer();
		_saveButton = new Button { Text = "保存", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_saveButton.Pressed += OnSavePressed;
		_saveAllButton = new Button { Text = "保存全部", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_saveAllButton.Pressed += OnSaveAllPressed;
		saveRow.AddChild(_saveButton);
		saveRow.AddChild(_saveAllButton);
		form.AddChild(saveRow);
		_selectionButtons.Add(_saveButton);
		_selectionButtons.Add(_saveAllButton);

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
			CustomMinimumSize = new Vector2(100, 0),
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		});
		input.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(input);
		return row;
	}

	private static SpinBox MakeSpin(double min, double max, double step, string suffix = "")
	{
		return new SpinBox
		{
			MinValue = min,
			MaxValue = max,
			Step = step,
			AllowGreater = true,
			AllowLesser = true,
			Suffix = suffix,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
	}

	// ------------------------------------------------------------------
	// 列表与选中
	// ------------------------------------------------------------------

	private void RefreshList(string selectId = null)
	{
		if (_dirty && _selected != null && !SaveSelected()) return;

		_definitions.Clear();
		_definitions.AddRange(LoadFromDisk());
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
				if (_definitions[i].Id.ToString() == selectId)
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

	private static List<BuildingDefinition> LoadFromDisk()
	{
		var list = new List<BuildingDefinition>();
		if (!DirAccess.DirExistsAbsolute(BuildingDefinitionDatabase.FolderPath))
			return list;

		using var dir = DirAccess.Open(BuildingDefinitionDatabase.FolderPath);
		if (dir == null) return list;

		dir.ListDirBegin();
		var file = dir.GetNext();
		while (file != "")
		{
			if (!dir.CurrentIsDir() && file.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
			{
				var def = ResourceLoader.Load<BuildingDefinition>($"{BuildingDefinitionDatabase.FolderPath}/{file}");
				if (def != null)
					list.Add(def);
				else
					GD.PushWarning($"建筑定义编辑器：无法加载 {file}");
			}
			file = dir.GetNext();
		}
		dir.ListDirEnd();

		list.Sort((a, b) => a.Id.CompareTo(b.Id));

		return list;
	}

	// ------------------------------------------------------------------
	// 表单回填 / 应用
	// ------------------------------------------------------------------

	private void LoadForm(BuildingDefinition def)
	{
		_selected = def;
		_dirty = false;
		_loading = true;

		var has = def != null;
		SetHasSelection(has);

		_idEdit.Text = def != null ? def.Id.ToString() : "";
		_nameEdit.Text = def?.DisplayName ?? "";
		_widthSpin.Value = def?.Width ?? 3.2f;
		_depthSpin.Value = def?.Depth ?? 3.2f;
		_heightSpin.Value = def?.Height ?? 12.0f;
		_buildTimeSpin.Value = def?.BuildTime ?? 6.0f;
		_foundationXSpin.Value = def?.FoundationSize.X ?? 4;
		_foundationZSpin.Value = def?.FoundationSize.Y ?? 4;
		_bodyAlignOption.Select(has ? (int)def.BodyAlign : 0);
		_bodyOffsetXSpin.Value = def?.BodyOffsetX ?? 0f;
		_bodyOffsetZSpin.Value = def?.BodyOffsetZ ?? 0f;

		RebuildCostRows(has ? def.Costs : null);
		RebuildProductionRows(has ? def.ProductionTable : null);

		_loading = false;
		UpdateStatus();
	}

	private void ApplyFormToSelected()
	{
		if (_selected == null) return;

		_selected.DisplayName = _nameEdit.Text.StripEdges();
		_selected.Width = (float)_widthSpin.Value;
		_selected.Depth = (float)_depthSpin.Value;
		_selected.Height = (float)_heightSpin.Value;
		_selected.BuildTime = (float)_buildTimeSpin.Value;
		_selected.FoundationSize = new Vector2I(
			Mathf.RoundToInt(_foundationXSpin.Value),
			Mathf.RoundToInt(_foundationZSpin.Value));
		_selected.BodyAlign = (BodyAlignMode)_bodyAlignOption.Selected;
		_selected.BodyOffsetX = (float)_bodyOffsetXSpin.Value;
		_selected.BodyOffsetZ = (float)_bodyOffsetZSpin.Value;
		_selected.Costs = CollectCosts();
		_selected.ProductionTable = CollectProduction();
	}

	// ------------------------------------------------------------------
	// 成本 / 产出表 行编辑
	// ------------------------------------------------------------------

	private void RebuildCostRows(MaterialAmount[] costs)
	{
		ClearChildren(_costsBox);
		if (costs == null) return;
		foreach (var cost in costs)
			AddCostRow(cost?.MaterialId ?? "", cost?.Amount ?? 0);
	}

	private void AddCostRow(string materialId, uint amount)
	{
		var row = new HBoxContainer();

		var material = MakeMaterialOption();
		EnsureMaterialItem(material, materialId);
		SelectMaterial(material, materialId);

		var amountSpin = MakeSpin(0, 100000, 1);
		amountSpin.Value = amount;

		var remove = new Button { Text = "✕", CustomMinimumSize = new Vector2(34, 0) };
		remove.Pressed += () =>
		{
			_costsBox.RemoveChild(row);
			row.QueueFree();
			MarkDirty();
		};

		material.ItemSelected += _ => MarkDirty();
		amountSpin.ValueChanged += _ => MarkDirty();

		row.AddChild(material);
		row.AddChild(amountSpin);
		row.AddChild(remove);
		_costsBox.AddChild(row);
	}

	private void OnAddCostPressed()
	{
		if (_selected == null) return;
		var all = MaterialDatabase.GetAllNames();
		var defaultId = all.Count > 0 ? all[0].Id : "";
		AddCostRow(defaultId, 1);
		MarkDirty();
	}

	private void RebuildProductionRows(ProducingLevelConfig[] table)
	{
		ClearChildren(_productionBox);
		if (table == null) return;
		foreach (var config in table)
		{
			AddProductionPanel(
				config?.Level ?? 1u,
				config?.IntervalSeconds ?? 10.0f,
				config?.Outputs);
		}
	}

	private void OnAddProductionPressed()
	{
		if (_selected == null) return;
		AddProductionPanel(1u, 10.0, null);
		MarkDirty();
	}

	/// <summary>添加一个等级面板：等级 + 间隔 + 若干产出行。</summary>
	private void AddProductionPanel(uint level, double interval, MaterialAmount[] outputs)
	{
		var panel = new VBoxContainer();

		var header = new HBoxContainer();
		var levelSpin = MakeSpin(1, 99, 1);
		levelSpin.Value = level;
		levelSpin.CustomMinimumSize = new Vector2(64, 0);
		levelSpin.ValueChanged += _ => MarkDirty();

		var intervalSpin = MakeSpin(0.1, 86400, 0.1, " s");
		intervalSpin.Value = interval;
		intervalSpin.ValueChanged += _ => MarkDirty();

		var removeLevel = new Button { Text = "✕", CustomMinimumSize = new Vector2(34, 0) };
		removeLevel.Pressed += () =>
		{
			_productionBox.RemoveChild(panel);
			panel.QueueFree();
			MarkDirty();
		};

		header.AddChild(levelSpin);
		header.AddChild(intervalSpin);
		header.AddChild(removeLevel);
		panel.AddChild(header);

		var outputsBox = new VBoxContainer();
		panel.AddChild(outputsBox);

		var addOutput = new Button { Text = "＋ 添加产出" };
		addOutput.Pressed += () =>
		{
			AddOutputRow(outputsBox);
			MarkDirty();
		};
		panel.AddChild(addOutput);

		if (outputs == null || outputs.Length == 0)
		{
			AddOutputRow(outputsBox);
		}
		else
		{
			foreach (var output in outputs)
				AddOutputRow(outputsBox, output?.MaterialId ?? "", output?.Amount ?? 1u);
		}

		_productionBox.AddChild(panel);
	}

	private void AddOutputRow(VBoxContainer outputsBox, string materialId = null, uint amount = 1)
	{
		var row = new HBoxContainer();

		var material = MakeMaterialOption();
		EnsureMaterialItem(material, materialId ?? "");
		SelectMaterial(material, materialId ?? "");

		var amountSpin = MakeSpin(1, 100000, 1);
		amountSpin.Value = amount;
		amountSpin.CustomMinimumSize = new Vector2(64, 0);

		var remove = new Button { Text = "✕", CustomMinimumSize = new Vector2(34, 0) };
		remove.Pressed += () =>
		{
			outputsBox.RemoveChild(row);
			row.QueueFree();
			MarkDirty();
		};

		material.ItemSelected += _ => MarkDirty();
		amountSpin.ValueChanged += _ => MarkDirty();

		row.AddChild(material);
		row.AddChild(amountSpin);
		row.AddChild(remove);
		outputsBox.AddChild(row);
	}

	private MaterialAmount[] CollectCosts()
	{
		var result = new List<MaterialAmount>();
		foreach (var child in _costsBox.GetChildren())
		{
			if (child is not HBoxContainer row) continue;
			var material = row.GetChild<OptionButton>(0);
			var amountSpin = row.GetChild<SpinBox>(1);
			result.Add(new MaterialAmount(
				material.GetItemMetadata(material.Selected).AsString(),
				(uint)Mathf.RoundToInt(amountSpin.Value)));
		}
		return result.ToArray();
	}

	private ProducingLevelConfig[] CollectProduction()
	{
		var result = new List<ProducingLevelConfig>();
		foreach (var child in _productionBox.GetChildren())
		{
			if (child is not VBoxContainer panel) continue;
			var header = panel.GetChild<HBoxContainer>(0);
			var outputsBox = panel.GetChild<VBoxContainer>(1);
			var levelSpin = header.GetChild<SpinBox>(0);
			var intervalSpin = header.GetChild<SpinBox>(1);

			var outputs = new List<MaterialAmount>();
			foreach (var outputChild in outputsBox.GetChildren())
			{
				if (outputChild is not HBoxContainer row) continue;
				var material = row.GetChild<OptionButton>(0);
				var amountSpin = row.GetChild<SpinBox>(1);
				outputs.Add(new MaterialAmount(
					material.GetItemMetadata(material.Selected).AsString(),
					(uint)Mathf.RoundToInt(amountSpin.Value)));
			}

			result.Add(new ProducingLevelConfig
			{
				Level = (uint)Mathf.RoundToInt(levelSpin.Value),
				IntervalSeconds = (float)intervalSpin.Value,
				Outputs = outputs.ToArray()
			});
		}
		return result.ToArray();
	}

	// ------------------------------------------------------------------
	// 保存
	// ------------------------------------------------------------------

	private void OnSavePressed()
	{
		if (_selected != null)
			SaveSelected();
	}

	private void OnSaveAllPressed()
	{
		SaveSelected();
		foreach (var def in _definitions)
		{
			if (def == _selected) continue;
			var err = ResourceSaver.Save(def, def.ResourcePath);
			if (err != Error.Ok)
				ShowError($"保存 {def.Id} 失败：{err}");
		}
	}

	private bool SaveSelected()
	{
		if (_selected == null) return true;

		ApplyFormToSelected();

		var errors = new List<string>(_selected.Validate());
		foreach (var other in _definitions)
		{
			if (other != _selected && other.Id == _selected.Id)
				errors.Add("Id 与其他定义重复");
		}

		if (errors.Count > 0)
		{
			ShowError($"无法保存：\n{string.Join("\n", errors)}");
			return false;
		}

		var targetPath = BuildTargetPath(_selected);

		if (string.IsNullOrEmpty(_selected.ResourcePath))
		{
			if (ResourceLoader.Exists(targetPath))
			{
				ShowError($"目标文件已存在：{targetPath}");
				return false;
			}
			_selected.ResourcePath = targetPath;
		}
		else if (!string.Equals(
			Path.GetFileName(_selected.ResourcePath),
			Path.GetFileName(targetPath),
			StringComparison.Ordinal))
		{
			// 显示名变化 → 按约定重命名文件（{Id}_{显示名}.tres）
			var renameErr = DirAccess.RenameAbsolute(
				ProjectSettings.GlobalizePath(_selected.ResourcePath),
				ProjectSettings.GlobalizePath(targetPath));
			if (renameErr != Error.Ok)
			{
				ShowError($"重命名文件失败：{renameErr}");
				return false;
			}
			_selected.ResourcePath = targetPath;
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

	/// <summary>自动分配可用 Id：避开当前内存中已加载的全部定义。</summary>
	private uint FindNextAvailableId()
	{
		var used = new HashSet<uint>();
		foreach (var def in _definitions)
			used.Add(def.Id);

		for (uint id = 1; id < uint.MaxValue; id++)
		{
			if (!used.Contains(id)) return id;
		}
		throw new InvalidOperationException("没有可用的建筑 Id");
	}

	private void OnNewPressed()
	{
		CreateDefinition(FindNextAvailableId(), null);
	}

	private void OnDuplicatePressed()
	{
		if (_selected == null) return;
		CreateDefinition(FindNextAvailableId(), _selected);
	}

	private void CreateDefinition(uint id, BuildingDefinition source)
	{
		BuildingDefinition def;
		if (source != null)
		{
			def = (BuildingDefinition)source.Duplicate(true);
			def.Id = id;
			def.ResourcePath = "";
		}
		else
		{
			def = new BuildingDefinition { Id = id, DisplayName = id.ToString() };
		}

		// 文件名自由（约定 {Id}_{显示名}.tres）；目标已存在（外部改动）时换下一个可用 Id
		var path = BuildTargetPath(def);
		while (ResourceLoader.Exists(path))
		{
			def.Id = FindNextAvailableId();
			path = BuildTargetPath(def);
		}

		var err = ResourceSaver.Save(def, path);
		if (err != Error.Ok)
		{
			ShowError($"创建失败：{err}");
			return;
		}

		_dirty = false;
		_selected = null;
		RefreshList(id.ToString());
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
		{
			// 尚未落盘的定义：无文件可删，直接从列表移除
			_pendingDelete = null;
			_selected = null;
			_dirty = false;
			RefreshList();
			return;
		}

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

	/// <summary>文件名约定：{Id}_{显示名}.tres（文件名自由，仅编辑器 Dock 维护的约定；运行时按内容 Id 识别）。</summary>
	private static string BuildTargetPath(BuildingDefinition def)
	{
		return $"{BuildingDefinitionDatabase.FolderPath}/{def.Id}_{SanitizeFileName(def.DisplayName)}.tres";
	}

	private static readonly char[] InvalidFileNameChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

	private static string SanitizeFileName(string name)
	{
		if (string.IsNullOrWhiteSpace(name)) return "";
		var chars = name.Trim().ToCharArray();
		for (int i = 0; i < chars.Length; i++)
		{
			if (Array.IndexOf(InvalidFileNameChars, chars[i]) >= 0)
				chars[i] = '_';
		}
		return new string(chars);
	}

	private void ShowError(string message)
	{
		_errorDialog.DialogText = message;
		_errorDialog.PopupCentered(new Vector2I(420, 160));
	}

	private static OptionButton MakeMaterialOption()
	{
		var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		foreach (var materialDef in MaterialDatabase.GetAllNames())
		{
			option.AddItem($"{materialDef.DisplayName} ({materialDef.Id})");
			option.SetItemMetadata(option.ItemCount - 1, materialDef.Id);
		}
		return option;
	}

	private static void EnsureMaterialItem(OptionButton option, string materialId)
	{
		for (int i = 0; i < option.ItemCount; i++)
		{
			if (option.GetItemMetadata(i).AsString() == materialId) return;
		}
		option.AddItem(string.IsNullOrEmpty(materialId) ? "（未选择）" : materialId);
		option.SetItemMetadata(option.ItemCount - 1, materialId);
	}

	private static void SelectMaterial(OptionButton option, string materialId)
	{
		for (int i = 0; i < option.ItemCount; i++)
		{
			if (option.GetItemMetadata(i).AsString() == materialId)
			{
				option.Select(i);
				return;
			}
		}
		option.Select(0);
	}

	private void ClearChildren(Container box)
	{
		foreach (var child in box.GetChildren())
		{
			box.RemoveChild(child);
			child.QueueFree();
		}
	}

	private void SetHasSelection(bool has)
	{
		foreach (var button in _selectionButtons)
			button.Disabled = !has;
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
