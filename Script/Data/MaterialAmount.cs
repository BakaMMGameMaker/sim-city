using Godot;

namespace MySimCity;

[GlobalClass]
public partial class MaterialAmount : Resource
{
	[Export]
	public uint MaterialId { get; set; } = MaterialIds.Wood;

	[Export]
	public uint Amount { get; set; } = 1;

	public MaterialAmount()
	{
	}

	public MaterialAmount(uint materialId, uint amount)
	{
		MaterialId = materialId;
		Amount = amount;
	}
}
