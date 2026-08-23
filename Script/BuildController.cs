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

	private readonly List<Rect2> _occupied = new();

	private enum Mode { Idle, Dragging, Confirming }
	private Mode _mode = Mode.Idle;

	private Building _preview;
	private bool _previewValid;
	private Vector3 _pendingPosition = Vector3.Zero;

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

	public override void _Process(double delta)
	{
		if (_mode == Mode.Dragging && _preview != null)
		{
			UpdatePreviewPosition();
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

	private void UpdatePreviewPosition()
	{
		var hit = RaycastToGround();
		if (hit == null)
		{
			_preview.Visible = false;
			return;
		}

		_preview.Visible = true;
		var snappedTo = SnapToGrid(hit.Value);
		_preview.GlobalPosition = snappedTo;
		_pendingPosition = snappedTo;

		_previewValid = IsPositionValid(snappedTo, _preview.FootprintSize);
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

		_occupied.Add(realBuilding.GetFootprintRect());

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

	private bool IsPositionValid(Vector3 center, Vector2 footprint)
	{
		var half = footprint * 0.5f;
		var newRect = new Rect2(center.X - half.X, center.Z - half.Y, footprint.X, footprint.Y);

		foreach (var occupied in _occupied)
		{
			if (newRect.Intersects(occupied))
				return false;
		}
		return true;
	}

	public void RegisterOccupied(Rect2 rect)
	{
		_occupied.Add(rect);
	}
}
