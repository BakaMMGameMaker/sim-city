using Godot;
using System.Reflection;

public static class AssertExports
{
	public static void AssertExportsNode(Node node)
	{
		var type = node.GetType();
		foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (prop.GetCustomAttribute<ExportAttribute>() == null) continue;

			var value = prop.GetValue(node);
			if (value == null && !prop.PropertyType.IsValueType && prop.PropertyType != typeof(string))
			{
				GD.PushError($"导出属性 \"{prop.Name}\" 在节点 {node.GetPath()} 上未赋值");
			}
		}

		// Also check public fields marked with [Export]
		foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
		{
			if (field.GetCustomAttribute<ExportAttribute>() == null) continue;

			var value = field.GetValue(node);
			if (value == null && !field.FieldType.IsValueType && field.FieldType != typeof(string))
			{
				GD.PushError($"导出属性 \"{field.Name}\" 在节点 {node.GetPath()} 上未赋值");
			}
		}
	}
}
