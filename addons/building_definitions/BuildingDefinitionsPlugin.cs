#if TOOLS
using Godot;

namespace MySimCity.EditorTools;

/// <summary>
/// 编辑器插件入口：注册「建筑类型定义」与「材料类型定义」Dock 到左侧停靠区。
/// 整个类被 #if TOOLS 包裹，导出构建不会包含编辑器代码。
/// </summary>
[Tool]
public partial class BuildingDefinitionsPlugin : EditorPlugin
{
	private EditorDock _buildingDock;
	private EditorDock _materialDock;

	public override void _EnterTree()
	{
		_buildingDock = AddDefinitionsDock(new BuildingDefinitionsDock(), "建筑类型定义");
		_materialDock = AddDefinitionsDock(new MaterialDefinitionsDock(), "材料类型定义");
	}

	private EditorDock AddDefinitionsDock(Control ui, string title)
	{
		var dock = new EditorDock
		{
			Title = title,
			DefaultSlot = EditorDock.DockSlot.LeftUl
		};
		dock.AddChild(ui);
		AddDock(dock);
		return dock;
	}

	public override void _ExitTree()
	{
		RemoveDockIfValid(ref _buildingDock);
		RemoveDockIfValid(ref _materialDock);
	}

	private void RemoveDockIfValid(ref EditorDock dock)
	{
		if (dock == null) return;
		RemoveDock(dock);
		dock.QueueFree();
		dock = null;
	}
}
#endif
