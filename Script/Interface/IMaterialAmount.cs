namespace MySimCity;

/// <summary>材料数量的运行时只读视图（成本或产出条目）。</summary>
public interface IMaterialAmount
{
	string MaterialId { get; }
	uint Amount { get; }
}
