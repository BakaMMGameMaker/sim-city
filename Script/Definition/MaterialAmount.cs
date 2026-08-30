using Godot;
using MySimCity.Definitions;

namespace MySimCity;

/// <summary>
/// 材料数量：某材料（字符串 Id）的引用 + 数量，作为成本或产出条目使用。
/// </summary>
[Tool]
public partial class MaterialAmount : ValidatableResource, IMaterialAmount
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

	public override string[] Validate()
	{
		return DefinitionValidation.ValidateMaterialAmount(MaterialId ?? "", Amount, MaterialDatabase.GetKnownIds);
	}
}
