using Godot;
using System;
using System.Collections.Generic;

namespace MySimCity;

/// <summary>
/// 建筑产出组件（纯 C# 类，非节点）。
/// 由 Building 在产出表非空时创建并持有；构造注入 Owner / 产出表 / 库存，
/// 建造完成时由 Building 直接调用 StartProduction。
/// 计时使用 SceneTreeTimer：建筑被销毁后挂起的 timer 仍会触发一次，
/// 靠 _running 与 IsInstanceValid(_owner) 双重守卫。
/// </summary>
public sealed class ProductionComponent : IUpgradable
{
	private readonly Building _owner;
	private readonly IInventory _inventory;
	private Dictionary<int, ProductionLevelConfig> _table;
	private bool _running;

	/// <summary>等级取自 Owner，升级后下一次产出自动套用新等级区间。</summary>
	public int Level => _owner.Level;

	public ProductionComponent(
		Building owner,
		IReadOnlyDictionary<int, ProductionLevelConfig> table,
		IInventory inventory)
	{
		_owner = owner ?? throw new ArgumentNullException(nameof(owner));
		_inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
		Configure(table);
	}

	/// <summary>更新产出表（Level 为键）。</summary>
	public void Configure(IReadOnlyDictionary<int, ProductionLevelConfig> table)
	{
		_table = table != null
			? new Dictionary<int, ProductionLevelConfig>(table)
			: new Dictionary<int, ProductionLevelConfig>();
	}

	public void StartProduction()
	{
		if (_running) return;

		if (_owner.GetTree() == null)
		{
			GD.PushError($"ProductionComponent 无法启动产出：{_owner.Name} 不在场景树中");
			return;
		}

		_running = true;
		ArmTimer();
	}

	public void StopProduction()
	{
		_running = false;
	}

	private void ArmTimer()
	{
		var config = FindBestConfig();
		if (config == null)
		{
			_running = false;
			return;
		}

		var tree = _owner.GetTree();
		if (tree == null)
		{
			_running = false;
			return;
		}

		var timer = tree.CreateTimer(Mathf.Max(0.1f, config.IntervalSeconds));
		timer.Timeout += OnTimeout;
	}

	private void OnTimeout()
	{
		if (!_running || !GodotObject.IsInstanceValid(_owner)) return;

		var config = FindBestConfig();
		if (config == null)
		{
			_running = false;
			return;
		}

		_inventory.Add(config.MaterialId, config.Amount);
		ArmTimer();
	}

	/// <summary>取不高于当前等级的最高级配置（与原实现语义一致）。</summary>
	private ProductionLevelConfig FindBestConfig()
	{
		ProductionLevelConfig best = null;
		int bestLevel = 0;
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
