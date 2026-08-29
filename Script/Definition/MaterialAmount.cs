using Godot;

namespace MySimCity;

[GlobalClass]
[Tool]
public partial class MaterialAmount : Resource
{
	[Export]
	public string MaterialId { get; set; } = "";

	[Export]
	public uint Amount { get; set; } = 1;

	public MaterialAmount()
	{
	}

	public MaterialAmount(string materialId, uint amount)
	{
		MaterialId = materialId;
		Amount = amount;
	}
}
