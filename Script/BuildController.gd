extends Node3D
class_name BuildController

## 负责玩家建造操作：从 UI 拖拽建筑 -> 预览吸附 -> 确认/取消 -> 开始生长

@export var building_scene: PackedScene
@export var camera: Camera3D
@export var grid_size: float = 1.0  # 1 栅格 = 1m

## UI 引用（在场景里拖入）
@export var confirm_panel: Control
@export var confirm_button: Button
@export var cancel_button: Button
@export var build_list_button: Button  # 目前只有一个建筑的占位按钮

## 已放置建筑的占地矩形列表（世界 XZ）
var _occupied: Array[Rect2] = []

enum Mode { IDLE, DRAGGING, CONFIRMING }
var _mode: Mode = Mode.IDLE

var _preview: Building = null
var _preview_valid: bool = false
var _pending_position: Vector3 = Vector3.ZERO


func _ready() -> void:
	if confirm_panel:
		confirm_panel.visible = false
	if confirm_button:
		confirm_button.pressed.connect(_on_confirm_pressed)
	if cancel_button:
		cancel_button.pressed.connect(_on_cancel_pressed)
	if build_list_button:
		build_list_button.button_down.connect(_on_build_list_button_down)
		# 注意：真正的拖拽会在 _input 里持续跟踪，直到松开


func _input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
		_cancel_current_mode()
		get_viewport().set_input_as_handled()
		return

	match _mode:
		Mode.DRAGGING:
			_handle_dragging_input(event)
		Mode.CONFIRMING:
			# 确认阶段只允许点按钮或 ESC，忽略其他地图点击
			pass
		Mode.IDLE:
			pass


func _process(_delta: float) -> void:
	if _mode == Mode.DRAGGING and _preview:
		_update_preview_position()


## ========== 开始拖拽（从 UI 按钮按下触发） ==========

func _on_build_list_button_down() -> void:
	if _mode != Mode.IDLE:
		return
	_start_dragging()


func _start_dragging() -> void:
	if building_scene == null:
		push_error("BuildController: building_scene 未设置")
		return

	_mode = Mode.DRAGGING
	_preview = building_scene.instantiate() as Building
	add_child(_preview)
	_preview.enter_preview()
	_update_preview_position()


func _handle_dragging_input(event: InputEvent) -> void:
	# 鼠标左键松开 -> 尝试进入确认阶段
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and not event.pressed:
		if _preview_valid:
			_enter_confirming()
		else:
			# 不合法位置直接取消
			_cancel_current_mode()
		get_viewport().set_input_as_handled()


func _update_preview_position() -> void:
	var hit := _raycast_to_ground()
	if hit == null:
		_preview.visible = false
		return

	_preview.visible = true
	var snapped := _snap_to_grid(hit)
	_preview.global_position = snapped
	_pending_position = snapped

	_preview_valid = _is_position_valid(snapped, _preview.footprint_size)
	_preview.set_preview_valid(_preview_valid)


func _enter_confirming() -> void:
	_mode = Mode.CONFIRMING
	# 预览停在当前位置，显示确认 UI
	if confirm_panel and camera:
		var screen_pos := camera.unproject_position(_pending_position + Vector3(0, _preview.height * 0.6, 0))
		confirm_panel.position = screen_pos - confirm_panel.size * 0.5
		confirm_panel.visible = true


func _on_confirm_pressed() -> void:
	if _mode != Mode.CONFIRMING or _preview == null:
		return

	# 正式建造
	var real_building := building_scene.instantiate() as Building
	add_child(real_building)
	real_building.global_position = _pending_position
	real_building.start_construction()

	# 记录占地
	_occupied.append(real_building.get_footprint_rect())

	_cleanup_preview()
	_mode = Mode.IDLE
	if confirm_panel:
		confirm_panel.visible = false


func _on_cancel_pressed() -> void:
	_cancel_current_mode()


func _cancel_current_mode() -> void:
	_cleanup_preview()
	_mode = Mode.IDLE
	if confirm_panel:
		confirm_panel.visible = false


func _cleanup_preview() -> void:
	if _preview and is_instance_valid(_preview):
		_preview.queue_free()
	_preview = null
	_preview_valid = false


## ========== 工具函数 ==========

func _raycast_to_ground() -> Variant:
	if camera == null:
		return null
	var mouse_pos := get_viewport().get_mouse_position()
	var from := camera.project_ray_origin(mouse_pos)
	var dir := camera.project_ray_normal(mouse_pos)
	if abs(dir.y) < 0.0001:
		return null
	# 与 y=0 平面求交
	var t := -from.y / dir.y
	if t < 0:
		return null
	return from + dir * t


func _snap_to_grid(pos: Vector3) -> Vector3:
	# 以 footprint 中心对齐到栅格中心（简单取整）
	var x := roundf(pos.x / grid_size) * grid_size
	var z := roundf(pos.z / grid_size) * grid_size
	return Vector3(x, 0.0, z)


func _is_position_valid(center: Vector3, footprint: Vector2) -> bool:
	var half := footprint * 0.5
	var new_rect := Rect2(center.x - half.x, center.z - half.y, footprint.x, footprint.y)

	# 简单边界检查（假设地图大约 -20~20）
	if new_rect.position.x < -20.0 or new_rect.position.y < -20.0 \
		or new_rect.end.x > 20.0 or new_rect.end.y > 20.0:
		return false

	for occupied in _occupied:
		if new_rect.intersects(occupied):
			return false
	return true


## 供外部（例如已有建筑初始化时）注册已占用区域
func register_occupied(rect: Rect2) -> void:
	_occupied.append(rect)
