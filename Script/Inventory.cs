using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MySimCity;

[GlobalClass]
public partial class Inventory : Node, IInventory
{
	public static Inventory Instance { get; private set; }

	private readonly Dictionary<string, uint> _amounts = new(StringComparer.OrdinalIgnoreCase)
	{
		{ "wood", 25 }
	};

	[Signal]
	public delegate void MaterialsChangedEventHandler();

	public override void _Ready()
	{
		Instance = this;
	}

	public uint GetAmount(string materialId)
	{
		return materialId != null && _amounts.TryGetValue(materialId, out var amount) ? amount : 0u;
	}

	public void Add(string materialId, uint amount)
	{
		if (amount == 0) return;
		_amounts.TryGetValue(materialId, out var current);
		_amounts[materialId] = current + amount;
		EmitSignal(SignalName.MaterialsChanged);
	}

	public bool CanAfford(string materialId, uint amount)
	{
		return GetAmount(materialId) >= amount;
	}

	public bool TrySpend(string materialId, uint amount)
	{
		if (!CanAfford(materialId, amount)) return false;
		_amounts[materialId] = GetAmount(materialId) - amount;
		EmitSignal(SignalName.MaterialsChanged);
		return true;
	}

	public bool CanAfford(IEnumerable<IMaterialAmount> costs)
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

	public bool TrySpend(IEnumerable<IMaterialAmount> costs)
	{
		if (costs == null) return true;
		var list = costs.Where(c => c != null).ToList();
		if (!CanAfford(list)) return false;

		foreach (var cost in list)
			TrySpend(cost.MaterialId, cost.Amount);

		return true;
	}
}
