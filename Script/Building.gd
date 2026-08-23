extends Node3D

@export var width: float = 4.0
@export var depth: float = 4.0
@export var height: float = 12.0
@export var build_time: float = 6.0

@export var body_material: StandardMaterial3D

@export var edge_material: StandardMaterial3D
@export var edge_thickness: float = 0.07

@onready var body_instance: MeshInstance3D = $Body
@onready var edge_component: EdgeComponent = $EdgeComponent

var tween: Tween

func _ready() -> void:
	_validate()
	_setup_building()
	_init_building()
	_start_construction()

func _validate() -> void:
	assert(body_material != null, "Building 未指定材质")
	assert(edge_material != null, "Building 未指定棱角材质")
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

func _init_building() -> void:
	_init_body_instance()
	_init_edge_component()

func _init_body_instance() -> void:
	body_instance.scale = Vector3(1, 0, 1)
	body_instance.position.y = 0

func _init_edge_component() -> void:
	edge_component.update(EdgeComponent.BuildingConstructionState.new(0.0, 0.0))

func _start_construction() -> void:
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
