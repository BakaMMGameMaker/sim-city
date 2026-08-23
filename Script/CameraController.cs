using Godot;

[GlobalClass]
public partial class CameraController : Camera3D
{
	[Export]
	public float MoveSpeed = 12.0f;

	[Export]
	public float MouseSensitivity = 0.003f;

	private Vector3 _moveDirection = Vector3.Zero;

	public override void _Input(InputEvent @event)
	{
		UpdateRotation(@event);
	}

	private void UpdateRotation(InputEvent @event)
	{
		if (@event is not InputEventMouseMotion motion) return;
		if (!Input.IsMouseButtonPressed(MouseButton.Right)) return;

		RotateY(-motion.Relative.X * MouseSensitivity);

		float newPitch = Rotation.X - motion.Relative.Y * MouseSensitivity;
		newPitch = Mathf.Clamp(newPitch, Mathf.DegToRad(-89.0f), Mathf.DegToRad(89.0f));
		Rotation = new Vector3(newPitch, Rotation.Y, Rotation.Z);
	}

	public override void _Process(double delta)
	{
		ResetMoveDirection();
		UpdateMoveDirection();
		UpdateGlobalPosition((float)delta);
	}

	private void ResetMoveDirection()
	{
		_moveDirection = Vector3.Zero;
	}

	private void UpdateMoveDirection()
	{
		if (Input.IsKeyPressed(Key.W)) _moveDirection -= GlobalBasis.Z;
		if (Input.IsKeyPressed(Key.S)) _moveDirection += GlobalBasis.Z;
		if (Input.IsKeyPressed(Key.A)) _moveDirection -= GlobalBasis.X;
		if (Input.IsKeyPressed(Key.D)) _moveDirection += GlobalBasis.X;

		_moveDirection = _moveDirection.Normalized();
	}

	private void UpdateGlobalPosition(float delta)
	{
		if (_moveDirection == Vector3.Zero) return;
		GlobalPosition += _moveDirection * MoveSpeed * delta;
	}
}
