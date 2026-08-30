using Godot;
using System.Collections.Generic;
using MySimCity.Definitions;

namespace MySimCity;

/// <summary>
/// 建筑数据定义。以 .tres 资源形式存放在 res://Data/Buildings/，
/// 文件名约定为 {Id}_{显示名}.tres，
/// 运行时按内容中的 Id 识别，经 BuildingDefinitionDatabase 加载。
/// </summary>
[Tool]
public partial class BuildingDefinition : ValidatableResource, IBuildingDefinition
{
	[Export]
	public uint Id { get; set; } = 0;

	[Export]
	public string DisplayName { get; set; } = "";

	[Export]
	public float Width { get; set; } = 3.2f;

	[Export]
	public float Depth { get; set; } = 3.2f;

	[Export]
	public float Height { get; set; } = 12.0f;

	[Export]
	public float BuildTime { get; set; } = 6.0f;

	[Export]
	public Vector2I FoundationSize { get; set; } = new(4, 4);

	[Export]
	public BodyAlignMode BodyAlign { get; set; } = BodyAlignMode.Center;

	[Export]
	public float BodyOffsetX { get; set; } = 0f;

	[Export]
	public float BodyOffsetZ { get; set; } = 0f;

	[Export]
	public MaterialAmount[] Costs { get; set; } = [];

	[Export]
	public ProducingLevelConfig[] ProductionTable { get; set; } = [];

	IReadOnlyList<IMaterialAmount> IBuildingDefinition.Costs => Costs;

	IReadOnlyList<IProducingLevelConfig> IBuildingDefinition.ProductionTable => ProductionTable;

	public override string[] Validate()
	{
		return DefinitionValidation.ValidateBuilding(
			new BuildingData(
				Id,
				DisplayName,
				Width,
				Depth,
				Height,
				BuildTime,
				FoundationSize.X,
				FoundationSize.Y,
				BodyAlign,
				BodyOffsetX,
				BodyOffsetZ,
				ToCostData(Costs),
				ToProductionData(ProductionTable)),
			MaterialDatabase.GetKnownIds);
	}

	private static IReadOnlyList<MaterialAmountData> ToCostData(IReadOnlyList<MaterialAmount> costs)
	{
		var list = new List<MaterialAmountData>();
		if (costs == null) return list;

		foreach (var cost in costs)
		{
			if (cost == null) continue;
			list.Add(new MaterialAmountData(cost.MaterialId ?? "", cost.Amount));
		}
		return list;
	}

	private static IReadOnlyList<ProductionLevelData> ToProductionData(IReadOnlyList<ProducingLevelConfig> table)
	{
		var list = new List<ProductionLevelData>();
		if (table == null) return list;

		foreach (var config in table)
		{
			if (config == null) continue;

			var outputs = new List<MaterialAmountData>();
			if (config.Outputs != null)
			{
				foreach (var output in config.Outputs)
				{
					if (output == null) continue;
					outputs.Add(new MaterialAmountData(output.MaterialId ?? "", output.Amount));
				}
			}
			list.Add(new ProductionLevelData(config.Level, config.IntervalSeconds, outputs));
		}
		return list;
	}
}
