using System.Collections.Generic;

namespace MySimCity;

/// <summary>产出等级配置的运行时只读视图：某等级下按间隔产出若干材料。</summary>
public interface IProducingLevelConfig
{
	uint Level { get; }
	float IntervalSeconds { get; }
	IReadOnlyList<IMaterialAmount> Outputs { get; }
}
