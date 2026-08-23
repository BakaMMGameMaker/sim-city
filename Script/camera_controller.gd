extends Camera3D

@export var move_speed: float = 12.0
@export var mouse_sensitivity: float = 0.003

var move_direction := Vector3.ZERO

func _input(event: InputEvent) -> void:
	_update_rotation(event)

func _update_rotation(event: InputEvent) -> void:
	if event is not InputEventMouseMotion: return
	if not Input.is_mouse_button_pressed(MOUSE_BUTTON_RIGHT): return
	
	rotate_y(-event.relative.x * mouse_sensitivity)
	
	var new_pitch : float = rotation.x - event.relative.y * mouse_sensitivity
	new_pitch = clamp(new_pitch, deg_to_rad(-89.0), deg_to_rad(89.0))
	rotation.x = new_pitch

func _process(delta: float) -> void:
	_reset_move_direction()

	_update_move_direction()

	_update_global_position(delta)

func _reset_move_direction() -> void:
	move_direction = Vector3.ZERO

func _update_move_direction() -> void:
	if Input.is_key_pressed(KEY_W): move_direction -= global_basis.z
	if Input.is_key_pressed(KEY_S): move_direction += global_basis.z
	if Input.is_key_pressed(KEY_A): move_direction -= global_basis.x
	if Input.is_key_pressed(KEY_D): move_direction += global_basis.x
	
	move_direction = move_direction.normalized()

func _update_global_position(delta: float) -> void:
	if move_direction == Vector3.ZERO: return

	global_position += move_direction * move_speed * delta
