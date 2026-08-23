using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class BuildController : Node3D
{
	[Export]
	public PackedScene BuildingScene;

	[Export]
	public Camera3D Camera;

	[Export]
	public float GridSize = 1.0f;

	[Export]
	public Control ConfirmPanel;

	[Export]
	public Button ConfirmButton;

	[Export]
	public Button CancelButton;

	[Export]
	public Button BuildListButton;

	/// <summary>已占用的栅格单元格 (x, z)</summary>
	private readonly HashSet<Vector2I> _occupiedCells = new();

	private enum Mode { Idle, Dragging, Confirming }
	private Mode _mode = Mode.Idle;

	private Building _preview;
	private bool _previewValid;
	private Vector3 _pendingPosition = Vector3.Zero;
	private Vector2I? _lastOriginCell;

	public override void _Ready()
	{
		AssertExports.AssertExportsNode(this);

		ConfirmPanel.Visible = false;
		ConfirmButton.Pressed += OnConfirmPressed;
		CancelButton.Pressed += OnCancelPressed;
		BuildListButton.ButtonDown += OnBuildListButtonDown;
	}

	public override void _Input(InputEvent @event)
	{
		if (IsCancel(@event))
		{
			Cancel();
			GetViewport().SetInputAsHandled();
			return;
		}

		// 仅在拖拽中且鼠标移动时更新预览位置（避免每帧 raycast）
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

	private void OnBuildListButtonDown()
	{
		if (_mode != Mode.Idle) return;
		StartDragging();
	}

	private void StartDragging()
	{
		_mode = Mode.Dragging;
		_lastOriginCell = null;
		_preview = BuildingScene.Instantiate<Building>();
		AddChild(_preview);
		_preview.EnterPreview();
		UpdatePreviewPosition();
	}

	private void Drag(InputEvent @event)
	{
		if (!IsDrag(@event)) return;

		if (_previewValid)
			EnterConfirming();
		else
			Cancel();
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

	/// <summary>
	/// 仅在栅格原点变化时做有效性判定与材质切换。
	/// </summary>
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

		// 位置未变则跳过后续判定
		if (_lastOriginCell.HasValue && _lastOriginCell.Value == originCell)
			return;

		_lastOriginCell = originCell;
		_preview.Visible = true;
		_preview.GlobalPosition = snapped;
		_pendingPosition = snapped;

		_previewValid = IsPositionValid(originCell, _preview.FootprintWidth, _preview.FootprintDepth);
		if (_previewValid)
			_preview.SetValidPreview();
		else
			_preview.SetInvalidPreview();
	}

	private void EnterConfirming()
	{
		_mode = Mode.Confirming;

		var screenPos = Camera.UnprojectPosition(_pendingPosition + new Vector3(0, _preview.Height * 0.6f, 0));
		ConfirmPanel.Position = screenPos - ConfirmPanel.Size * 0.5f;
		ConfirmPanel.Visible = true;
	}

	private void OnConfirmPressed()
	{
		if (_mode != Mode.Confirming || _preview == null) return;

		var realBuilding = BuildingScene.Instantiate<Building>();
		AddChild(realBuilding);
		realBuilding.GlobalPosition = _pendingPosition;
		realBuilding.StartConstruction();

		OccupyCells(realBuilding.GetOriginCell(), realBuilding.FootprintWidth, realBuilding.FootprintDepth);

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

	/// <summary>吸附到栅格点（中心对齐用 Round）</summary>
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

	/// <summary>
	/// 以中心格为基准，向两侧扩展 footprint（例如 4 格 → center-2 .. center+1）。
	/// </summary>
	private bool IsPositionValid(Vector2I centerCell, int footprintWidth, int footprintDepth)
	{
		int startX = centerCell.X - footprintWidth / 2;
		int startZ = centerCell.Y - footprintDepth / 2;

		for (int x = 0; x < footprintWidth; x++)
		{
			for (int z = 0; z < footprintDepth; z++)
			{
				if (_occupiedCells.Contains(new Vector2I(startX + x, startZ + z)))
					return false;
			}
		}
		return true;
	}

	private void OccupyCells(Vector2I centerCell, int footprintWidth, int footprintDepth)
	{
		int startX = centerCell.X - footprintWidth / 2;
		int startZ = centerCell.Y - footprintDepth / 2;

		for (int x = 0; x < footprintWidth; x++)
		{
			for (int z = 0; z < footprintDepth; z++)
				_occupiedCells.Add(new Vector2I(startX + x, startZ + z));
		}
	}

	public void RegisterOccupiedCells(Vector2I centerCell, int footprintWidth, int footprintDepth)
	{
		OccupyCells(centerCell, footprintWidth, footprintDepth);
	}
}
