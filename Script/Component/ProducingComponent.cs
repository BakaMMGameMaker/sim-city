using Godot;
using System;
using System.Collections.Generic;

namespace MySimCity;

/// <summary>
/// 产出组件。
/// 由工厂在产出表非空时创建并挂到宿主上；
/// 构造注入 宿主 / 产出表 / 库存 / SceneTree。
/// 订阅宿主自身的 ConstructionFinished 事件，事件触发后开始产出。
/// 计时使用 SceneTreeTimer：Owner 被销毁后挂起的 timer 仍会触发一次，
/// 靠 _running 与 IsInstanceValid(owner) 双重守卫。
/// </summary>
public sealed class ProducingComponent
{
	private readonly IProducibleBuilding _owner;
	private readonly IReadOnlyDictionary<uint, ProducingLevelConfig> _table;
	private readonly IInventory _inventory;
	private readonly SceneTree _tree;
	private bool _running;

	private uint Level => _owner.Level;

	public ProducingComponent(
		IProducibleBuilding owner,
		IReadOnlyDictionary<uint, ProducingLevelConfig> table,
		IInventory inventory,
		SceneTree tree)
	{
		_owner = owner ?? throw new ArgumentNullException(nameof(owner));
		_inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
		_tree = tree ?? throw new ArgumentNullException(nameof(tree));
		_table = table ?? throw new ArgumentNullException(nameof(table));

		_owner.ConstructionFinished += OnOwnerConstructionFinished;
	}

	private void OnOwnerConstructionFinished()
	{
		if (_running) return;

		_running = true;
		ArmTimer();
	}

	private void ArmTimer()
	{
		if (!_running) return;

		var config = FindBestConfig();
		if (config == null)
		{
			_running = false;
			return;
		}

		var timer = _tree.CreateTimer(config.IntervalSeconds);
		timer.Timeout += OnTimeout;
	}

	private void OnTimeout()
	{
		if (!_running) return;

		if (_owner is not GodotObject godotOwner || !GodotObject.IsInstanceValid(godotOwner))
		{
			_running = false;
			return;
		}

		var config = FindBestConfig();
		if (config == null)
		{
			_running = false;
			return;
		}

		_inventory.Add(config.MaterialId, config.Amount);
		ArmTimer();
	}

	private ProducingLevelConfig FindBestConfig()
	{
		ProducingLevelConfig best = null;
		uint bestLevel = 0;
		foreach (var (level, config) in _table)
		{
			if (config == null || level > Level) continue;
			if (best == null || level > bestLevel)
			{
				best = config;
				bestLevel = level;
			}
		}
		return best;
	}
}
