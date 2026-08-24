using Godot;

namespace MySimCity;

/// <summary>
/// 建筑类型的完整配置表。可在编辑器中创建 .tres 资源，或通过代码生成默认值。
/// 后续新增建筑只需新增一条 Definition，无需再改 Building.ApplyPreset 的 switch。
/// </summary>
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

	/// <summary>地基占用格子数（X = Width, Y = Depth）</summary>
	[Export]
	public Vector2I FoundationSize { get; set; } = new(4, 4);

	[Export]
	public Building.BodyAlignMode BodyAlign { get; set; } = Building.BodyAlignMode.Center;

	[Export]
	public float BodyOffsetX { get; set; } = 0f;

	[Export]
	public float BodyOffsetZ { get; set; } = 0f;

	/// <summary>建造所需材料列表。空数组表示免费。</summary>
	[Export]
	public MaterialAmount[] Costs { get; set; } = System.Array.Empty<MaterialAmount>();

	/// <summary>
	/// 产出表。为空或 null 表示非生产建筑。
	/// 挂载 ProductionComponent 后会使用此表。
	/// </summary>
	[Export]
	public ProductionLevelConfig[] ProductionTable { get; set; } = System.Array.Empty<ProductionLevelConfig>();
}
