using Godot;

namespace MySimCity;

[GlobalClass]
public partial class BuildingDefinition : Resource
{
	[Export]
	public BuildingType Type { get; set; } = BuildingType.Residential;

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
	public Building.BodyAlignMode BodyAlign { get; set; } = Building.BodyAlignMode.Center;

	[Export]
	public float BodyOffsetX { get; set; } = 0f;

	[Export]
	public float BodyOffsetZ { get; set; } = 0f;

	[Export]
	public MaterialAmount[] Costs { get; set; } = [];

	[Export]
	public ProductionLevelConfig[] ProductionTable { get; set; } = [];
}
