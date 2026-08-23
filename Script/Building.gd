extends Node3D
class_name Building

@export var width: float = 3.2
@export var depth: float = 3.2
@export var height: float = 12.0
@export var build_time: float = 6.0

@export var footprint_size := Vector2(4.0, 4.0)

@export var body_material: StandardMaterial3D
@export var edge_material: StandardMaterial3D
@export var edge_thickness: float = 0.07

@export var preview_valid_body_material: StandardMaterial3D
@export var preview_invalid_body_material: StandardMaterial3D
@export var preview_valid_edge_material: StandardMaterial3D
@export var preview_invalid_edge_material: StandardMaterial3D

@onready var body_instance: MeshInstance3D = $Body
@onready var edge_component: EdgeComponent = $EdgeComponent

enum Mode { PREVIEW, CONSTRUCTING, IDLE }
var _mode := Mode.IDLE
var _original_body_material: StandardMaterial3D
var _original_edge_material: StandardMaterial3D

func _ready() -> void:
	AssertExports.assert_exports(self)

	_setup_building()


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
			edge_component.register_edge(Vector3(t, height, t), Vector3(x, 0, z), updater)


func _register_top_edges(half_x: float, half_z: float, t: float) -> void:
	var updater = func(node: MeshInstance3D, state: EdgeComponent.BuildingConstructionState) -> void:
		node.position.y = state.top_

	for z in [-half_z, half_z]:
		edge_component.register_edge(Vector3(width, t, t), Vector3(0, 0, z), updater)
	for x in [-half_x, half_x]:
		edge_component.register_edge(Vector3(t, t, depth), Vector3(x, 0, 0), updater)


func _register_bottom_edges(half_x: float, half_z: float, t: float) -> void:
	var updater = func(_node: MeshInstance3D, _state: EdgeComponent.BuildingConstructionState) -> void:
		pass

	for z in [-half_z, half_z]:
		edge_component.register_edge(Vector3(width, t, t), Vector3(0, 0, z), updater)
	for x in [-half_x, half_x]:
		edge_component.register_edge(Vector3(t, t, depth), Vector3(x, 0, 0), updater)


func enter_preview() -> void:
	_mode = Mode.PREVIEW
	_original_body_material = body_material
	_original_edge_material = edge_material
	body_instance.scale = Vector3(1, 1, 1)
	body_instance.position.y = height / 2
	edge_component.update(EdgeComponent.BuildingConstructionState.new(1.0, height))


func set_valid_preview() -> void:
	if _mode != Mode.PREVIEW: return

	body_instance.material_override = preview_valid_body_material
	edge_component.set_material(preview_valid_edge_material)
		

func set_invalid_preview() -> void:
	if _mode != Mode.PREVIEW: return

	body_instance.material_override = preview_invalid_body_material
	edge_component.set_material(preview_invalid_edge_material)


func exit_preview() -> void:
	body_instance.material_override = body_material
	edge_component.set_material(edge_material)


func start_construction() -> void:
	_mode = Mode.CONSTRUCTING

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
	var tween := create_tween()
	tween.set_ease(Tween.EASE_OUT)
	tween.set_trans(Tween.TRANS_CUBIC)
	tween.tween_method(_update_construction, 0.0, 1.0, build_time)


func _update_construction(progress: float) -> void:
	var scale_y := progress
	var top := progress * height

	body_instance.scale = Vector3(1, scale_y, 1)
	body_instance.position.y = top / 2

	edge_component.update(EdgeComponent.BuildingConstructionState.new(scale_y, top))


func get_footprint_rect() -> Rect2:
	var half := footprint_size * 0.5
	return Rect2(
		global_position.x - half.x,
		global_position.z - half.y,
		footprint_size.x,
		footprint_size.y
	)
