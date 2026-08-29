using Godot;
using System.Collections.Generic;
using MySimCity;

[GlobalClass]
public partial class BuildController : Node3D
{
	[Export]
	public PackedScene BuildingScene;

	[Export]
	public Camera3D Camera;

	[Export]
	public Control ConfirmPanel;

	[Export]
	public Button ConfirmButton;

	[Export]
	public Button CancelButton;

	[Export]
	public VBoxContainer BuildListVBox;

	[Export]
	public Label WoodLabel;

	private readonly HashSet<Vector2I> _occupiedCells = new();
	private readonly List<Node> _buildListDynamicNodes = new();

	private enum Mode { Idle, Dragging, Confirming }
	private Mode _mode = Mode.Idle;

	private Building _preview;
	private bool _previewValid;
	private Vector2I? _lastOriginCell;
	private BuildingDefinition _selectedDef;

	private IInventory _inventory;
	private BuildingFactory _buildingFactory;

	private float GridSize => GameConfig.Instance != null ? GameConfig.Instance.GridSize : 1.0f;

	public override void _Ready()
	{
		AssertExports.AssertExportsNode(this);

		// 强制从 Autoload 获取接口实例
		_inventory = Inventory.Instance as IInventory
			?? throw new System.InvalidOperationException("Inventory Autoload 未实现 IInventory");
		_buildingFactory = new BuildingFactory(_inventory);

		ConfirmPanel.Visible = false;
		ConfirmButton.Pressed += OnConfirmPressed;
		CancelButton.Pressed += OnCancelPressed;

		RebuildBuildList();

		if (Inventory.Instance != null)
		{
			Inventory.Instance.MaterialsChanged += UpdateWoodLabel;
			UpdateWoodLabel();
		}
	}

	private void UpdateWoodLabel()
	{
		if (WoodLabel != null && Inventory.Instance != null)
			WoodLabel.Text = $"{MaterialDatabase.GetDisplayName(WoodId)}: {Inventory.Instance.GetAmount(WoodId)}";
	}

	private const string WoodId = "wood";

	public override void _Input(InputEvent @event)
	{
		if (IsCancel(@event))
		{
			Cancel();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_mode == Mode.Dragging && @event is InputEventMouseMotion)
		{
			UpdatePreviewPosition();
			return;
		}

		Handle(@event);
	}

	private void Handle(InputEvent @event)
	{
		switch (_mode)
		{
			case Mode.Dragging:
				Drag(@event);
				break;
			case Mode.Confirming:
			case Mode.Idle:
				break;
		}
	}

	private void OnSelectBuilding(BuildingDefinition def)
	{
		if (_mode != Mode.Idle) return;
		if (def == null) return;

		_selectedDef = def;

		if (!_inventory.CanAfford(_selectedDef.Costs))
		{
			GD.Print($"材料不足，无法建造 {def.DisplayName}");
			return;
		}

		StartDragging();
	}

	/// <summary>根据 BuildingDefinitionDatabase 动态生成建造列表按钮。</summary>
	private void RebuildBuildList()
	{
		if (BuildListVBox == null) return;

		foreach (var node in _buildListDynamicNodes)
		{
			if (IsInstanceValid(node))
				node.QueueFree();
		}
		_buildListDynamicNodes.Clear();

		var definitions = BuildingDefinitionDatabase.All;
		if (definitions.Count == 0)
		{
			var hint = new Label
			{
				Text = "未找到建筑定义\n请在编辑器中配置"
			};
			_buildListDynamicNodes.Add(hint);
			BuildListVBox.AddChild(hint);
			return;
		}

		foreach (var def in definitions)
		{
			var button = new Button
			{
				CustomMinimumSize = new Vector2(140, 50),
				Text = MakeButtonText(def),
				TooltipText = def.Id
			};
			button.Pressed += () => OnSelectBuilding(def);
			_buildListDynamicNodes.Add(button);
			BuildListVBox.AddChild(button);
		}
	}

	private static string MakeButtonText(BuildingDefinition def)
	{
		var costs = MaterialDatabase.FormatCosts(def.Costs);
		return costs.Length > 0 ? $"{def.DisplayName} ({costs})" : def.DisplayName;
	}

	private void StartDragging()
	{
		_mode = Mode.Dragging;
		_lastOriginCell = null;
		_preview = BuildingScene.Instantiate<Building>();
		AddChild(_preview);
		_preview.ApplyDefinition(_selectedDef);
		_preview.EnterPreview();
		UpdatePreviewPosition();
	}

	private void Drag(InputEvent @event)
	{
		if (!IsDrag(@event)) return;

		if (_previewValid) EnterConfirming();
		else Cancel();
		GetViewport().SetInputAsHandled();
	}

	private static bool IsCancel(InputEvent @event)
	{
		return @event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape;
	}

	private static bool IsDrag(InputEvent @event)
	{
		return @event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && !mb.Pressed;
	}

	private void UpdatePreviewPosition()
	{
		if (_preview == null) return;

		var hit = RaycastToGround();
		if (hit == null)
		{
			_preview.Visible = false;
			return;
		}

		var snapped = SnapToGrid(hit.Value);
		var originCell = WorldToCell(snapped);

		if (PreviewNotMoved(originCell)) return;

		_lastOriginCell = originCell;
		_preview.Visible = true;
		_preview.GlobalPosition = snapped;

		_previewValid = IsPositionValid(originCell, _preview.Definition.FoundationSize.X, _preview.Definition.FoundationSize.Y);
		_preview.SetPreviewValid(_previewValid);
	}

	private bool PreviewNotMoved(Vector2I newOriginCell)
	{
		return _lastOriginCell.HasValue && _lastOriginCell.Value == newOriginCell;
	}

	private void EnterConfirming()
	{
		_mode = Mode.Confirming;

		var screenPos = Camera.UnprojectPosition(_preview.GlobalPosition + new Vector3(0, _preview.Definition.Height * 0.6f, 0));
		ConfirmPanel.Position = screenPos - ConfirmPanel.Size * 0.5f;
		ConfirmPanel.Visible = true;
	}

	private void OnConfirmPressed()
	{
		if (_mode != Mode.Confirming || _preview == null) return;

		if (!_inventory.TrySpend(_selectedDef.Costs))
		{
			GD.Print("确认时材料不足");
			Cancel();
			return;
		}

		var realBuilding = _buildingFactory.CreateBuilding(BuildingScene, this, _selectedDef, _preview.GlobalPosition);

		OccupyCells(realBuilding.GetOriginCell(), realBuilding.Definition.FoundationSize.X, realBuilding.Definition.FoundationSize.Y);

		CleanupPreview();
		_mode = Mode.Idle;
		ConfirmPanel.Visible = false;
	}

	private void OnCancelPressed()
	{
		Cancel();
	}

	private void Cancel()
	{
		CleanupPreview();
		_mode = Mode.Idle;
		ConfirmPanel.Visible = false;
	}

	private void CleanupPreview()
	{
		if (_preview != null && IsInstanceValid(_preview))
			_preview.QueueFree();
		_preview = null;
		_previewValid = false;
		_lastOriginCell = null;
	}

	private Vector3? RaycastToGround()
	{
		var mousePos = GetViewport().GetMousePosition();
		var from = Camera.ProjectRayOrigin(mousePos);
		var dir = Camera.ProjectRayNormal(mousePos);
		if (Mathf.Abs(dir.Y) < 0.0001f) return null;
		float t = -from.Y / dir.Y;
		if (t < 0) return null;
		return from + dir * t;
	}

	private Vector3 SnapToGrid(Vector3 pos)
	{
		float x = Mathf.Round(pos.X / GridSize) * GridSize;
		float z = Mathf.Round(pos.Z / GridSize) * GridSize;
		return new Vector3(x, 0.0f, z);
	}

	private Vector2I WorldToCell(Vector3 pos)
	{
		return new Vector2I(
			Mathf.RoundToInt(pos.X / GridSize),
			Mathf.RoundToInt(pos.Z / GridSize)
		);
	}

	private bool IsPositionValid(Vector2I centerCell, int foundationWidth, int foundationDepth)
	{
		int startX = centerCell.X - foundationWidth / 2;
		int startZ = centerCell.Y - foundationDepth / 2;

		for (int x = 0; x < foundationWidth; x++)
		{
			for (int z = 0; z < foundationDepth; z++)
			{
				if (_occupiedCells.Contains(new Vector2I(startX + x, startZ + z)))
					return false;
			}
		}
		return true;
	}

	private void OccupyCells(Vector2I centerCell, int foundationWidth, int foundationDepth)
	{
		int startX = centerCell.X - foundationWidth / 2;
		int startZ = centerCell.Y - foundationDepth / 2;

		for (int x = 0; x < foundationWidth; x++)
		{
			for (int z = 0; z < foundationDepth; z++)
				_occupiedCells.Add(new Vector2I(startX + x, startZ + z));
		}
	}

	public void RegisterOccupiedCells(Vector2I centerCell, int foundationWidth, int foundationDepth)
	{
		OccupyCells(centerCell, foundationWidth, foundationDepth);
	}
}
