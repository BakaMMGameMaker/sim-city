#if TOOLS
using Godot;

namespace MySimCity.EditorTools;

/// <summary>
/// 编辑器插件入口：注册「建筑类型定义」Dock 到左侧停靠区。
/// 整个类被 #if TOOLS 包裹，导出构建不会包含编辑器代码。
/// </summary>
[Tool]
public partial class BuildingDefinitionsPlugin : EditorPlugin
{
	private EditorDock _dock;

	public override void _EnterTree()
	{
		var ui = new BuildingDefinitionsDock();
		var dock = new EditorDock
		{
			Title = "建筑类型定义",
			DefaultSlot = EditorDock.DockSlot.LeftUl
		};
		dock.AddChild(ui);
		AddDock(dock);
		_dock = dock;
	}

	public override void _ExitTree()
	{
		if (_dock != null)
		{
			RemoveDock(_dock);
			_dock.QueueFree();
			_dock = null;
		}
	}
}
#endif
