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
	public Button ResidentialButton;

	[Export]
	public Button LumberMillButton;

	[Export]
	public Label WoodLabel;

	private readonly HashSet<Vector2I> _occupiedCells = new();

	private enum Mode { Idle, Dragging, Confirming }
	private Mode _mode = Mode.Idle;

	private Building _preview;
	private bool _previewValid;
	private Vector2I? _lastOriginCell;
	private BuildingType _selectedType = BuildingType.Residential;

	private float GridSize => GameConfig.Instance != null ? GameConfig.Instance.GridSize : 1.0f;

	public override void _Ready()
	{
		AssertExports.AssertExportsNode(this);

		ConfirmPanel.Visible = false;
		ConfirmButton.Pressed += OnConfirmPressed;
		CancelButton.Pressed += OnCancelPressed;

		if (ResidentialButton != null)
			ResidentialButton.Pressed += () => OnSelectBuilding(BuildingType.Residential);
		if (LumberMillButton != null)
			LumberMillButton.Pressed += () => OnSelectBuilding(BuildingType.LumberMill);

		if (Inventory.Instance != null)
		{
			Inventory.Instance.MaterialsChanged += UpdateWoodLabel;
			UpdateWoodLabel();
		}
	}

	private void UpdateWoodLabel()
	{
		if (WoodLabel != null && Inventory.Instance != null)
			WoodLabel.Text = $"原木: {Inventory.Instance.Wood}";
	}

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

	private void OnSelectBuilding(BuildingType type)
	{
		if (_mode != Mode.Idle) return;

		_selectedType = type;

		// 先创建临时实例拿成本，判断材料是否足够
		var temp = BuildingScene.Instantiate<Building>();
		temp.ApplyPreset(type);
		int cost = temp.WoodCost;
		temp.QueueFree();

		if (Inventory.Instance != null && !Inventory.Instance.CanAffordWood(cost))
		{
			GD.Print($"原木不足（需要 {cost}，当前 {Inventory.Instance.Wood}），无法建造");
			return;
		}

		StartDragging();
	}

	private void StartDragging()
	{
		_mode = Mode.Dragging;
		_lastOriginCell = null;
		_preview = BuildingScene.Instantiate<Building>();
		_preview.ApplyPreset(_selectedType);
		AddChild(_preview);
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

		_previewValid = IsPositionValid(originCell, _preview.FoundationWidth, _preview.FoundationDepth);
		_preview.SetPreviewValid(_previewValid);
	}

	private bool PreviewNotMoved(Vector2I newOriginCell)
	{
		return _lastOriginCell.HasValue && _lastOriginCell.Value == newOriginCell;
	}

	private void EnterConfirming()
	{
		_mode = Mode.Confirming;

		var screenPos = Camera.UnprojectPosition(_preview.GlobalPosition + new Vector3(0, _preview.Height * 0.6f, 0));
		ConfirmPanel.Position = screenPos - ConfirmPanel.Size * 0.5f;
		ConfirmPanel.Visible = true;
	}

	private void OnConfirmPressed()
	{
		if (_mode != Mode.Confirming || _preview == null) return;

		int cost = _preview.WoodCost;
		if (Inventory.Instance != null && !Inventory.Instance.TrySpendWood(cost))
		{
			GD.Print($"确认时原木不足（需要 {cost}）");
			Cancel();
			return;
		}

		var realBuilding = BuildingScene.Instantiate<Building>();
		realBuilding.ApplyPreset(_selectedType);
		AddChild(realBuilding);
		realBuilding.GlobalPosition = _preview.GlobalPosition;
		realBuilding.StartConstruction();

		OccupyCells(realBuilding.GetOriginCell(), realBuilding.FoundationWidth, realBuilding.FoundationDepth);

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
