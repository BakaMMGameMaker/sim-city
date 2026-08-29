using Godot;

namespace MySimCity;

[GlobalClass]
[Tool]
public partial class ProductionLevelConfig : Resource
{
	[Export]
	public int Level { get; set; } = 1;

	[Export]
	public float IntervalSeconds { get; set; } = 10.0f;

	[Export]
	public uint Amount { get; set; } = 1;

	[Export]
	public MaterialType MaterialId { get; set; } = MaterialType.Wood;
}
