using Godot;

/// <summary>
/// 产出建筑某一等级的配置：每隔 IntervalSeconds 产出 Amount 个指定材料。
/// </summary>
[GlobalClass]
public partial class ProductionLevelConfig : Resource
{
	[Export]
	public int Level { get; set; } = 1;

	[Export]
	public float IntervalSeconds { get; set; } = 10.0f;

	[Export]
	public int Amount { get; set; } = 1;

	/// <summary>材料 ID，目前仅支持 "wood"</summary>
	[Export]
	public string MaterialId { get; set; } = "wood";
}
