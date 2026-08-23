extends Object
class_name AssertExports

static func assert_exports(node: Node) -> void:
	var script = node.get_script() as Script
	if script == null: return

	for prop in script.get_property_list():
		_assert(node, prop)

static func _assert(node: Node, prop: Dictionary) -> void:
	if prop.usage == PROPERTY_USAGE_SCRIPT_VARIABLE | PROPERTY_USAGE_EDITOR | PROPERTY_USAGE_STORAGE:
		var value = node.get(prop.name)
		assert(value != null, "导出属性 \"%s\" 在节点 %s 上未赋值" % [prop.name, node.get_path()])
