using Godot;

namespace MySimCity;

[GlobalClass]
[Tool]
public partial class ProducingLevelConfig : Resource
{
	[Export]
	public uint Level { get; set; } = 1u;

	[Export]
	public float IntervalSeconds { get; set; } = 10.0f;

	[Export]
	public MaterialType Material { get; set; } = MaterialType.Wood;

	[Export]
	public uint Amount { get; set; } = 1u;
}
