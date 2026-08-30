using Godot;
using System.Collections.Generic;
using MySimCity.Definitions;

namespace MySimCity;

/// <summary>
/// 产出等级配置：某等级下按固定间隔产出若干种材料（多材料产出）。
/// [Tool]：编辑器需要实例化本类型来编辑/保存所属资源；无需 [GlobalClass]。
/// </summary>
[Tool]
public partial class ProducingLevelConfig : ValidatableResource, IProducingLevelConfig
{
	[Export]
	public uint Level { get; set; } = 1u;

	[Export]
	public float IntervalSeconds { get; set; } = 10.0f;

	[Export]
	public MaterialAmount[] Outputs { get; set; } = [];

	IReadOnlyList<IMaterialAmount> IProducingLevelConfig.Outputs => Outputs;

	public override string[] Validate()
	{
		var outputs = new List<MaterialAmountData>();
		if (Outputs != null)
		{
			foreach (var output in Outputs)
			{
				if (output == null) continue;
				outputs.Add(new MaterialAmountData(output.MaterialId ?? "", output.Amount));
			}
		}

		return DefinitionValidation.ValidateProductionLevel(
			Level,
			IntervalSeconds,
			outputs,
			MaterialDatabase.GetKnownIds);
	}
}
