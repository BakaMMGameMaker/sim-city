using Godot;
using System;

namespace MySimCity;

/// <summary>
/// 产出组件。挂载在 Building 上即可让该建筑具备周期性产出能力。
/// 通过 Initialize(IInventory) 注入库存，禁止直接访问 Inventory.Instance。
/// </summary>
[GlobalClass]
public partial class ProductionComponent : Node
{
	[Export]
	public int Level { get; set; } = 1;

	[Export]
	public ProductionLevelConfig[] ProductionTable { get; set; } = [];

	private IInventory _inventory;
	private Timer _timer;
	private bool _running;

	public void Initialize(IInventory inventory)
	{
		_inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
	}

	public void Configure(ProductionLevelConfig[] table, int level = 1)
	{
		ProductionTable = table ?? [];
		Level = level;
	}

	public void StartProduction()
	{
		if (_inventory == null)
		{
			GD.PushError($"{GetPath()}: ProductionComponent 未注入 IInventory，无法启动产出");
			return;
		}

		var config = GetCurrentProductionConfig();
		if (config == null) return;

		if (_timer == null)
		{
			_timer = new Timer
			{
				Name = "ProductionTimer",
				OneShot = false,
				Autostart = false
			};
			AddChild(_timer);
			_timer.Timeout += OnProductionTick;
		}

		_timer.WaitTime = Mathf.Max(0.1f, config.IntervalSeconds);
		_timer.Start();
		_running = true;
	}

	public void StopProduction()
	{
		_timer?.Stop();
		_running = false;
	}

	private ProductionLevelConfig GetCurrentProductionConfig()
	{
		if (ProductionTable == null || ProductionTable.Length == 0)
			return null;

		ProductionLevelConfig best = null;
		foreach (var c in ProductionTable)
		{
			if (c == null) continue;
			if (c.Level <= Level && (best == null || c.Level > best.Level))
				best = c;
		}
		return best;
	}

	private void OnProductionTick()
	{
		if (!_running || _inventory == null) return;

		var config = GetCurrentProductionConfig();
		if (config == null) return;

		_inventory.Add(config.MaterialId, config.Amount);
	}
}
