using Godot;

/// <summary>
/// 全局材料库存（Autoload）。
/// </summary>
[GlobalClass]
public partial class Inventory : Node
{
	public static Inventory Instance { get; private set; }

	[Export]
	public int Wood { get; private set; } = 25;

	[Signal]
	public delegate void MaterialsChangedEventHandler();

	public override void _Ready()
	{
		Instance = this;
	}

	public bool CanAffordWood(int cost) => Wood >= cost;

	public bool TrySpendWood(int cost)
	{
		if (!CanAffordWood(cost)) return false;
		Wood -= cost;
		EmitSignal(SignalName.MaterialsChanged);
		return true;
	}

	public void AddWood(int amount)
	{
		if (amount <= 0) return;
		Wood += amount;
		EmitSignal(SignalName.MaterialsChanged);
	}
}
