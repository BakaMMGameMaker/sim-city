using Godot;

namespace MySimCity;

[GlobalClass]
[Tool]
public partial class MaterialAmount : Resource
{
	[Export]
	public MaterialType MaterialId { get; set; } = MaterialType.Wood;

	[Export]
	public uint Amount { get; set; } = 1;

	public MaterialAmount()
	{
	}

	public MaterialAmount(MaterialType materialId, uint amount)
	{
		MaterialId = materialId;
		Amount = amount;
	}
}
