using System.Collections.Generic;

namespace MySimCity.Definitions;

/// <summary>
/// 与 Godot 无关的纯数据载体：游戏侧的 Definition 资源与编译期校验工具
/// （解析 .tres 文本）都先把数据装进这些结构，再交给 DefinitionValidation 统一校验。
/// </summary>

public readonly struct MaterialAmountData
{
	public readonly string? MaterialId;
	public readonly uint Amount;

	public MaterialAmountData(string? materialId, uint amount)
	{
		MaterialId = materialId;
		Amount = amount;
	}
}

public readonly struct ProductionLevelData
{
	public readonly uint Level;
	public readonly float IntervalSeconds;
	public readonly IReadOnlyList<MaterialAmountData> Outputs;

	public ProductionLevelData(uint level, float intervalSeconds, IReadOnlyList<MaterialAmountData> outputs)
	{
		Level = level;
		IntervalSeconds = intervalSeconds;
		Outputs = outputs;
	}
}

public readonly struct BuildingData
{
	public readonly uint Id;
	public readonly string? DisplayName;
	public readonly float Width;
	public readonly float Depth;
	public readonly float Height;
	public readonly float BuildTime;
	public readonly int FoundationX;
	public readonly int FoundationY;
	public readonly BodyAlignMode BodyAlign;
	public readonly float BodyOffsetX;
	public readonly float BodyOffsetZ;
	public readonly IReadOnlyList<MaterialAmountData> Costs;
	public readonly IReadOnlyList<ProductionLevelData> ProductionTable;

	public BuildingData(
		uint id,
		string? displayName,
		float width,
		float depth,
		float height,
		float buildTime,
		int foundationX,
		int foundationY,
		BodyAlignMode bodyAlign,
		float bodyOffsetX,
		float bodyOffsetZ,
		IReadOnlyList<MaterialAmountData> costs,
		IReadOnlyList<ProductionLevelData> productionTable)
	{
		Id = id;
		DisplayName = displayName;
		Width = width;
		Depth = depth;
		Height = height;
		BuildTime = buildTime;
		FoundationX = foundationX;
		FoundationY = foundationY;
		BodyAlign = bodyAlign;
		BodyOffsetX = bodyOffsetX;
		BodyOffsetZ = bodyOffsetZ;
		Costs = costs;
		ProductionTable = productionTable;
	}
}
