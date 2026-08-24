using Godot;

namespace MySimCity;

public static class DefaultBuildingDefinitions
{
	public static BuildingDefinition Residential()
	{
		return new BuildingDefinition
		{
			Type = BuildingType.Residential,
			Width = 2.8f,
			Depth = 2.8f,
			Height = 5.5f,
			BuildTime = 4.5f,
			FoundationSize = new Vector2I(3, 3),
			BodyAlign = Building.BodyAlignMode.Center,
			BodyOffsetX = 0f,
			BodyOffsetZ = 0f,
			Costs =
			[
				new MaterialAmount(MaterialIds.Wood, 12)
			],
			ProductionTable = []
		};
	}

	public static BuildingDefinition LumberMill()
	{
		return new BuildingDefinition
		{
			Type = BuildingType.LumberMill,
			Width = 3.6f,
			Depth = 3.2f,
			Height = 4.2f,
			BuildTime = 7.0f,
			FoundationSize = new Vector2I(4, 4),
			BodyAlign = Building.BodyAlignMode.Offset,
			BodyOffsetX = 0.2f,
			BodyOffsetZ = 0.15f,
			Costs =
			[
				new MaterialAmount(MaterialIds.Wood, 5)
			],
			ProductionTable =
			[
				new ProductionLevelConfig { Level = 1, IntervalSeconds = 12.0f, Amount = 2, MaterialId = MaterialIds.Wood },
				new ProductionLevelConfig { Level = 2, IntervalSeconds = 10.0f, Amount = 3, MaterialId = MaterialIds.Wood },
				new ProductionLevelConfig { Level = 3, IntervalSeconds = 8.0f, Amount = 5, MaterialId = MaterialIds.Wood },
			]
		};
	}

	public static BuildingDefinition Get(BuildingType type) => type switch
	{
		BuildingType.Residential => Residential(),
		BuildingType.LumberMill => LumberMill(),
		_ => Residential()
	};
}
