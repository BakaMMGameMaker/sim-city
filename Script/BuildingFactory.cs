using Godot;
using System;
using System.Collections.Generic;

namespace MySimCity;

/// <summary>
/// 建筑工厂。
/// 专门负责实例化非预览的正式建筑：应用定义、定位、开始建造，
/// 并在产出表非空时创建 ProductionComponent 挂到建筑上
/// （组件自行监听 ConstructionFinished 事件启动产出）。
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
		BuildingDefinition def,
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

	private void AttachProducingComponentIfNeeded(Building building, ProducingLevelConfig[] table)
	{
		var dict = BuildProductionDict(table);
		if (dict.Count == 0) return;

		var tree = building.GetTree();
		if (tree == null)
		{
			GD.PushError($"BuildingFactory：{building.Name} 不在场景树中，无法创建产出组件");
			return;
		}

		_ = new ProducingComponent(building, dict, _inventory, tree);
	}

	/// <summary>把产出表数组转为 Level → 配置 的字典；跳过非法/重复条目并告警。</summary>
	public static Dictionary<uint, ProducingLevelConfig> BuildProductionDict(ProducingLevelConfig[] table)
	{
		var dict = new Dictionary<uint, ProducingLevelConfig>();
		if (table == null) return dict;

		foreach (var config in table)
		{
			if (config == null) continue;
			if (config.Level < 1)
			{
				GD.PushWarning($"产出表存在非法等级 {config.Level}，已跳过");
				continue;
			}
			if (dict.ContainsKey(config.Level))
			{
				GD.PushWarning($"产出表存在重复等级 {config.Level}，已跳过");
				continue;
			}
			dict.Add(config.Level, config);
		}
		return dict;
	}
}
