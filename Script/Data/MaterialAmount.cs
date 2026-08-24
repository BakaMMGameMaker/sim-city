using Godot;

namespace MySimCity;

/// <summary>
/// 一种材料的数量描述，用于建筑成本、产出等。
/// 在编辑器中可直接配置数组。
/// </summary>
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
