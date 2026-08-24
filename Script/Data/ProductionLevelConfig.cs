using Godot;

namespace MySimCity;

/// <summary>
/// 产出建筑某一等级的配置：每隔 IntervalSeconds 产出 Amount 个指定材料。
/// 可在编辑器中直接编辑数组，无需改代码。
/// </summary>
[GlobalClass]
public partial class ProductionLevelConfig : Resource
{
	[Export]
	public int Level { get; set; } = 1;

	[Export]
	public float IntervalSeconds { get; set; } = 10.0f;

	[Export]
	public uint Amount { get; set; } = 1;

	[Export]
	public uint MaterialId { get; set; } = MaterialIds.Wood;
}
