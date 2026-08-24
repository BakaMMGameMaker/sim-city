using Godot;
using System.Collections.Generic;
using System.Linq;

namespace MySimCity;

/// <summary>
/// 全局材料库存（Autoload）。实现 IInventory，供依赖注入使用。
/// </summary>
[GlobalClass]
public partial class Inventory : Node, IInventory
{
	public static Inventory Instance { get; private set; }

	private readonly Dictionary<uint, uint> _amounts = new()
	{
		{ MaterialIds.Wood, 25 }
	};

	[Signal]
	public delegate void MaterialsChangedEventHandler();

	public override void _Ready()
	{
		Instance = this;
	}

	public uint GetAmount(uint materialId)
	{
		return _amounts.TryGetValue(materialId, out var amount) ? amount : 0u;
	}

	public void Add(uint materialId, uint amount)
	{
		if (amount == 0) return;
		_amounts.TryGetValue(materialId, out var current);
		_amounts[materialId] = current + amount;
		EmitSignal(SignalName.MaterialsChanged);
	}

	public bool CanAfford(uint materialId, uint amount)
	{
		return GetAmount(materialId) >= amount;
	}

	public bool TrySpend(uint materialId, uint amount)
	{
		if (!CanAfford(materialId, amount)) return false;
		_amounts[materialId] = GetAmount(materialId) - amount;
		EmitSignal(SignalName.MaterialsChanged);
		return true;
	}

	public bool CanAfford(IEnumerable<MaterialAmount> costs)
	{
		if (costs == null) return true;
		foreach (var cost in costs)
		{
			if (cost == null) continue;
			if (!CanAfford(cost.MaterialId, cost.Amount))
				return false;
		}
		return true;
	}

	public bool TrySpend(IEnumerable<MaterialAmount> costs)
	{
		if (costs == null) return true;
		var list = costs.Where(c => c != null).ToList();
		if (!CanAfford(list)) return false;

		foreach (var cost in list)
			TrySpend(cost.MaterialId, cost.Amount);

		return true;
	}

	/// <summary>兼容旧 UI 的便捷属性</summary>
	public uint Wood => GetAmount(MaterialIds.Wood);
}
