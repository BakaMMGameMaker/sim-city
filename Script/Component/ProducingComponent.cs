using Godot;
using System;
using System.Collections.Generic;

namespace MySimCity;

/// <summary>
/// 订阅宿主自身的 ConstructionFinished 事件，事件触发后开始产出。
/// </summary>
public sealed class ProducingComponent
{
	private readonly IProducibleBuilding _owner;
	private readonly IReadOnlyDictionary<uint, IProducingLevelConfig> _table;
	private readonly IInventory _inventory;
	private readonly ITimerFactory _timerFactory;
	private bool _running;

	private uint Level => _owner.Level;

	public ProducingComponent(
		IProducibleBuilding owner,
		IReadOnlyDictionary<uint, IProducingLevelConfig> table,
		IInventory inventory,
		ITimerFactory timers)
	{
		_owner = owner ?? throw new ArgumentNullException(nameof(owner));
		_table = table ?? throw new ArgumentNullException(nameof(table));
		_inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
		_timerFactory = timers ?? throw new ArgumentNullException(nameof(timers));

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

		var timer = _timerFactory.CreateTimer(config.IntervalSeconds);
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

		foreach (var output in config.Outputs)
		{
			if (output == null) continue;
			_inventory.Add(output.MaterialId, output.Amount);
		}

		ArmTimer();
	}

	private IProducingLevelConfig FindBestConfig()
	{
		IProducingLevelConfig best = null;
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
