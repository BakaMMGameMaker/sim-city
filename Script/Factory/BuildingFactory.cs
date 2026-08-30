using Godot;
using System;
using System.Collections.Generic;

namespace MySimCity;

/// <summary>
/// 建筑工厂。
/// 专门负责实例化非预览的正式建筑：应用定义、定位、开始建造，
/// 并在产出表非空时创建 ProducingComponent 挂到建筑上
/// （组件自行监听 ConstructionFinished 事件启动产出）。
/// 本工厂是 Godot 场景树的边界：在此处把建筑所在树包装成
/// ITimerFactory 交给组件，组件本身不依赖具体场景树。
/// 由 BuildController 注入 IInventory。
/// </summary>
public sealed class BuildingFactory
{
	private readonly IInventory _inventory;

	public BuildingFactory(IInventory inventory)
	{
		_inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
	}

	/// <summary>
	/// 实例化正式建筑并开始建造；产出表非空时按需挂载产出组件。
	/// </summary>
	public Building CreateBuilding(
		PackedScene buildingScene,
		Node parent,
		IBuildingDefinition def,
		Vector3 globalPosition)
	{
		ArgumentNullException.ThrowIfNull(buildingScene);
		ArgumentNullException.ThrowIfNull(parent);
		ArgumentNullException.ThrowIfNull(def);

		var building = buildingScene.Instantiate<Building>();
		parent.AddChild(building);
		building.ApplyDefinition(def);
		building.GlobalPosition = globalPosition;

		AttachProducingComponentIfNeeded(building, def.ProductionTable);

		building.StartConstruction();
		return building;
	}

	private void AttachProducingComponentIfNeeded(
		Building building,
		IReadOnlyList<IProducingLevelConfig> table)
	{
		var dict = BuildProductionDict(table);
		if (dict.Count == 0) return;

		var tree = building.GetTree();
		if (tree == null)
		{
			GD.PushError($"BuildingFactory：{building.Name} 不在场景树中，无法创建产出组件");
			return;
		}

		_ = new ProducingComponent(building, dict, _inventory, new SceneTreeTimerFactory(tree));
	}

	private static Dictionary<uint, IProducingLevelConfig> BuildProductionDict(
		IReadOnlyList<IProducingLevelConfig> table)
	{
		var dict = new Dictionary<uint, IProducingLevelConfig>();
		if (table == null) return dict;

		foreach (var config in table)
		{
			if (config == null) continue;
			// 编译期校验已保证等级唯一；重复时覆盖而非抛异常，仅作运行时防御
			dict[config.Level] = config;
		}
		return dict;
	}
}
