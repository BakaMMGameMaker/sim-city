using Godot;
using System.Collections.Generic;
using MySimCity.Definitions;

namespace MySimCity;

/// <summary>
/// 建筑定义的运行时只读视图。。
/// </summary>
public interface IBuildingDefinition
{
	uint Id { get; }
	string DisplayName { get; }
	float Width { get; }
	float Depth { get; }
	float Height { get; }
	float BuildTime { get; }
	Vector2I FoundationSize { get; }
	BodyAlignMode BodyAlign { get; }
	float BodyOffsetX { get; }
	float BodyOffsetZ { get; }
	IReadOnlyList<IMaterialAmount> Costs { get; }
	IReadOnlyList<IProducingLevelConfig> ProductionTable { get; }
}
