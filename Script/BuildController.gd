extends Node3D
class_name BuildController

## 负责玩家建造操作

@export var building_scene: PackedScene
@export var camera: Camera3D
@export var grid_size: float = 1.0

@export var confirm_panel: Control
@export var confirm_button: Button
@export var cancel_button: Button
@export var build_list_button: Button

var _occupied: Array[Rect2] = []

enum Mode { IDLE, DRAGGING, CONFIRMING }
var _mode: Mode = Mode.IDLE

var _preview: Building = null
var _preview_valid: bool = false
var _pending_position: Vector3 = Vector3.ZERO


func _ready() -> void:
	AssertExports.assert_exports(self)

	confirm_panel.visible = false
	confirm_button.pressed.connect(_on_confirm_pressed)
	cancel_button.pressed.connect(_on_cancel_pressed)
	build_list_button.button_down.connect(_on_build_list_button_down)


func _input(event: InputEvent) -> void:
	if _is_cancel(event):
		_cancel()
		get_viewport().set_input_as_handled()
		return

	_handle(event)


func _handle(event: InputEvent) -> void:
	match _mode:
		Mode.DRAGGING:
			_drag(event)
		Mode.CONFIRMING:
			pass
		Mode.IDLE:
			pass

func _process(_delta: float) -> void:
	if _mode == Mode.DRAGGING and _preview:
		_update_preview_position()


func _on_build_list_button_down() -> void:
	if _mode != Mode.IDLE: return
	_start_dragging()


func _start_dragging() -> void:
	_mode = Mode.DRAGGING
	_preview = building_scene.instantiate() as Building
	add_child(_preview)
	_preview.enter_preview()
	_update_preview_position()


func _drag(event: InputEvent) -> void:
	if not _is_drag(event): return

	if _preview_valid:
		_enter_confirming()
	else:
		_cancel()
	get_viewport().set_input_as_handled()


func _is_cancel(event: InputEvent) -> bool:
	return event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE


func _is_drag(event: InputEvent) -> bool:
	return event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and not event.pressed


func _update_preview_position() -> void:
	var hit = _raycast_to_ground()
	if hit == null:
		_preview.visible = false
		return

	_preview.visible = true
	var snapped_to := _snap_to_grid(hit)
	_preview.global_position = snapped_to
	_pending_position = snapped_to

	_preview_valid = _is_position_valid(snapped_to, _preview.footprint_size)
	if _preview_valid: _preview.set_valid_preview()
	else: _preview.set_invalid_preview()


func _enter_confirming() -> void:
	_mode = Mode.CONFIRMING

	var screen_pos := camera.unproject_position(_pending_position + Vector3(0, _preview.height * 0.6, 0))
	confirm_panel.position = screen_pos - confirm_panel.size * 0.5
	confirm_panel.visible = true


func _on_confirm_pressed() -> void:
	if _mode != Mode.CONFIRMING or _preview == null: return

	var real_building := building_scene.instantiate() as Building
	add_child(real_building)
	real_building.global_position = _pending_position
	real_building.start_construction()

	_occupied.append(real_building.get_footprint_rect())

	_cleanup_preview()
	_mode = Mode.IDLE
	confirm_panel.visible = false


func _on_cancel_pressed() -> void:
	_cancel()


func _cancel() -> void:
	_cleanup_preview()
	_mode = Mode.IDLE
	confirm_panel.visible = false


func _cleanup_preview() -> void:
	if _preview and is_instance_valid(_preview): _preview.queue_free()
	_preview = null
	_preview_valid = false


func _raycast_to_ground() -> Variant:
	var mouse_pos := get_viewport().get_mouse_position()
	var from := camera.project_ray_origin(mouse_pos)
	var dir := camera.project_ray_normal(mouse_pos)
	if abs(dir.y) < 0.0001: return null
	var t := -from.y / dir.y
	if t < 0: return null
	return from + dir * t


func _snap_to_grid(pos: Vector3) -> Vector3:
	var x := roundf(pos.x / grid_size) * grid_size
	var z := roundf(pos.z / grid_size) * grid_size
	return Vector3(x, 0.0, z)


func _is_position_valid(center: Vector3, footprint: Vector2) -> bool:
	var half := footprint * 0.5
	var new_rect := Rect2(center.x - half.x, center.z - half.y, footprint.x, footprint.y)

	for occupied in _occupied:
		if new_rect.intersects(occupied):
			return false

	return true


func register_occupied(rect: Rect2) -> void:
	_occupied.append(rect)
