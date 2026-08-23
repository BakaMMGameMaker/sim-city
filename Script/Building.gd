extends Node3D
class_name Building

## 视觉尺寸（实际 mesh 大小，单位米，可浮点）
@export var width: float = 3.2
@export var depth: float = 3.2
@export var height: float = 12.0

## 占地面积（栅格单位，1 单位 = 1m）。用于吸附和碰撞检测，通常比视觉尺寸稍大，避免建筑物严丝合缝。
@export var footprint_size: Vector2 = Vector2(4.0, 4.0)

@export var build_time: float = 6.0

@export var body_material: StandardMaterial3D
@export var edge_material: StandardMaterial3D
@export var edge_thickness: float = 0.07

## 预览用材质（浅绿色 = 合法，浅红色 = 不合法）
@export var preview_valid_body: StandardMaterial3D
@export var preview_invalid_body: StandardMaterial3D
@export var preview_valid_edge: StandardMaterial3D
@export var preview_invalid_edge: StandardMaterial3D

@onready var body_instance: MeshInstance3D = $Body
@onready var edge_component: EdgeComponent = $EdgeComponent

var tween: Tween
var _is_preview: bool = false
var _original_body_material: StandardMaterial3D
var _original_edge_material: StandardMaterial3D


func _ready() -> void:
	_validate()
	_setup_building()
	# 不再自动开始建造。由外部调用 start_construction() 或 enter_preview()


func _validate() -> void:
	assert(body_material != null, "Building 未指定 body_material")
	assert(edge_material != null, "Building 未指定 edge_material")
	assert(edge_component != null, "Building 缺少 EdgeComponent 组件")


func _setup_building() -> void:
	_setup_body()
	_setup_edge_component()


func _setup_body() -> void:
	var box := BoxMesh.new()
	box.size = Vector3(width, height, depth)
	body_instance.mesh = box
	body_instance.material_override = body_material


func _setup_edge_component() -> void:
	edge_component.setup(edge_material, edge_thickness)
	_register_edges()


func _register_edges() -> void:
	var half_x := width * 0.5
	var half_z := depth * 0.5

	_register_vertical_edges(half_x, half_z, edge_thickness)
	_register_top_edges(half_x, half_z, edge_thickness)
	_register_bottom_edges(half_x, half_z, edge_thickness)


func _register_vertical_edges(half_x: float, half_z: float, t: float) -> void:
	var updater = func(node: MeshInstance3D, state: EdgeComponent.BuildingConstructionState) -> void:
		node.scale.y = state.scale_y_
		node.position.y = state.top_ / 2

	for x in [-half_x, half_x]:
		for z in [-half_z, half_z]:
			edge_component.register_edge(
				Vector3(t, height, t),
				Vector3(x, 0, z),
				updater
			)


func _register_top_edges(half_x: float, half_z: float, t: float) -> void:
	var updater = func(node: MeshInstance3D, state: EdgeComponent.BuildingConstructionState) -> void:
		node.position.y = state.top_

	for z in [-half_z, half_z]:
		edge_component.register_edge(
			Vector3(width, t, t),
			Vector3(0, 0, z),
				updater
		)
	for x in [-half_x, half_x]:
		edge_component.register_edge(
			Vector3(t, t, depth),
			Vector3(x, 0, 0),
				updater
		)


func _register_bottom_edges(half_x: float, half_z: float, t: float) -> void:
	var updater = func(_node: MeshInstance3D, _state: EdgeComponent.BuildingConstructionState) -> void:
		pass

	for z in [-half_z, half_z]:
		edge_component.register_edge(
			Vector3(width, t, t),
			Vector3(0, 0, z),
				updater
		)
	for x in [-half_x, half_x]:
		edge_component.register_edge(
			Vector3(t, t, depth),
			Vector3(x, 0, 0),
				updater
		)


## ========== 预览模式 ==========

## 进入预览模式：显示完整建筑物（已建造完毕的样子），并准备材质切换
func enter_preview() -> void:
	_is_preview = true
	_original_body_material = body_material
	_original_edge_material = edge_material
	# 立即显示完整高度
	body_instance.scale = Vector3(1, 1, 1)
	body_instance.position.y = height / 2
	edge_component.update(EdgeComponent.BuildingConstructionState.new(1.0, height))


## 设置预览合法性（改变材质颜色）
func set_preview_valid(is_valid: bool) -> void:
	if not _is_preview:
		return
	if is_valid:
		if preview_valid_body:
			body_instance.material_override = preview_valid_body
		if preview_valid_edge:
			edge_component.set_material(preview_valid_edge)
	else:
		if preview_invalid_body:
			body_instance.material_override = preview_invalid_body
		if preview_invalid_edge:
			edge_component.set_material(preview_invalid_edge)


## 退出预览，恢复原始材质（如果之后要变成真正建造的话）
func exit_preview() -> void:
	_is_preview = false
	if _original_body_material:
		body_instance.material_override = _original_body_material
	if _original_edge_material:
		edge_component.set_material(_original_edge_material)


## ========== 建造模式 ==========

## 初始化为“未建造”状态（高度为 0），然后开始生长动画
func start_construction() -> void:
	_is_preview = false
	# 恢复原始材质
	body_instance.material_override = body_material
	edge_component.set_material(edge_material)

	_init_building()
	_start_construction()


func _init_building() -> void:
	_init_body_instance()
	_init_edge_component()


func _init_body_instance() -> void:
	body_instance.scale = Vector3(1, 0, 1)
	body_instance.position.y = 0


func _init_edge_component() -> void:
	edge_component.update(EdgeComponent.BuildingConstructionState.new(0.0, 0.0))


func _start_construction() -> void:
	if tween and tween.is_valid():
		tween.kill()
	tween = create_tween()
	tween.set_ease(Tween.EASE_OUT)
	tween.set_trans(Tween.TRANS_CUBIC)
	tween.tween_method(_update_construction, 0.0, 1.0, build_time)


func _update_construction(progress: float) -> void:
	var scale_y := progress
	var top := progress * height

	body_instance.scale = Vector3(1, scale_y, 1)
	body_instance.position.y = top / 2

	edge_component.update(EdgeComponent.BuildingConstructionState.new(scale_y, top))


## 获取当前占地矩形（世界 XZ 平面，以自身 position 为中心）
func get_footprint_rect() -> Rect2:
	var half := footprint_size * 0.5
	return Rect2(
		global_position.x - half.x,
		global_position.z - half.y,
		footprint_size.x,
		footprint_size.y
	)
