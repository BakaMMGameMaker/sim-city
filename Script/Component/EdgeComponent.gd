extends Node3D
class_name EdgeComponent

@export var material: StandardMaterial3D
@export var thickness: float = 0.07

class BuildingConstructionState extends RefCounted:
	var scale_y_: float = 0.0
	var top_: float = 0.0

	func _init(scale_y: float = 0.0, top: float = 0.0) -> void:
		scale_y_ = scale_y
		top_ = top

class EdgeData:
	var instance_: MeshInstance3D
	var updater_: Callable

	func _init(instance: MeshInstance3D, updater: Callable) -> void:
		instance_ = instance
		updater_ = updater

var _enabled: bool = true
var _edges: Array[EdgeData] = []

func setup(edge_material: StandardMaterial3D, edge_thickness: float) -> void:
	material = edge_material
	thickness = edge_thickness
	_enabled = true
	_clear_edges()

func enable() -> void:
	_enabled = true
	# 显示 edges

func disable() -> void:
	_enabled = false
	# 隐藏 edges

func register_edge(edge_size: Vector3, base_position: Vector3, updater: Callable) -> void:
	assert(_enabled, "组件未启用但尝试注册边")

	var mi := _make_edge(edge_size)
	mi.position = base_position
	_edges.append(EdgeData.new(mi, updater))

func update(state: BuildingConstructionState) -> void:
	if not _enabled: return

	for edge in _edges:
		edge.updater_.call(edge.instance_, state)

func _make_edge(size: Vector3) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var box := BoxMesh.new()
	box.size = size
	mi.mesh = box
	mi.material_override = material
	add_child(mi)
	return mi

func _clear_edges() -> void:
	for edge_data in _edges:
		var mi: MeshInstance3D = edge_data["instance"]
		if is_instance_valid(mi): mi.queue_free()
	_edges.clear()
