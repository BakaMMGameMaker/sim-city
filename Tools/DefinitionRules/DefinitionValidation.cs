using System;
using System.Collections.Generic;

namespace MySimCity.Definitions;

/// <summary>
/// 定义资源的统一校验规则。
/// 所有方法对 null / 默认值安全（Godot 反序列化先构造后赋属性，
/// 构造函数中的首次校验会看到默认值，不能抛异常）。
/// 已知材料集合通过 Func 惰性获取：仅当 MaterialId 通过格式校验后才求值，
/// 避免资源构造期间重入资源加载。
/// </summary>
public static class DefinitionValidation
{
	public const float MinProductionIntervalSeconds = 0.1f;

	public const string ErrorDisplayNameEmpty = "显示名不能为空";

	public static string[] ValidateMaterialDefinition(string? id, string? displayName)
	{
		var errors = new List<string>();

		if (!DefinitionIdValidation.IsValid(id))
			errors.Add(DefinitionIdValidation.ErrorMessage);
		if (string.IsNullOrWhiteSpace(displayName))
			errors.Add(ErrorDisplayNameEmpty);

		return [.. errors];
	}

	public static string[] ValidateMaterialAmount(
		string? materialId,
		uint amount,
		Func<IReadOnlyCollection<string>> knownMaterialIds)
	{
		var errors = new List<string>();

		if (!DefinitionIdValidation.IsValid(materialId))
		{
			errors.Add($"材料 Id 非法：{materialId ?? "（空）"}（{DefinitionIdValidation.PatternHint}）");
		}
		else if (!ContainsIgnoreCase(knownMaterialIds, materialId))
		{
			errors.Add($"引用了不存在的材料 {materialId}");
		}

		if (amount == 0)
			errors.Add("数量必须大于 0");

		return [.. errors];
	}

	public static string[] ValidateProductionLevel(
		uint level,
		float intervalSeconds,
		IReadOnlyList<MaterialAmountData> outputs,
		Func<IReadOnlyCollection<string>> knownMaterialIds)
	{
		var errors = new List<string>();

		if (level == 0)
			errors.Add("等级必须大于 0");
		if (!(intervalSeconds > MinProductionIntervalSeconds))
			errors.Add($"产出间隔必须大于 {MinProductionIntervalSeconds:0.#} 秒");

		if (outputs == null || outputs.Count == 0)
		{
			errors.Add("产出列表不能为空");
		}
		else
		{
			foreach (var output in outputs)
				errors.AddRange(ValidateMaterialAmount(output.MaterialId, output.Amount, knownMaterialIds));
		}

		return [.. errors];
	}

	public static string[] ValidateBuilding(
		BuildingData data,
		Func<IReadOnlyCollection<string>> knownMaterialIds)
	{
		var errors = new List<string>();
		errors.AddRange(ValidateBuildingFields(
			data.Id, data.DisplayName, data.Width, data.Depth, data.Height, data.BuildTime,
			data.FoundationX, data.FoundationY));
		errors.AddRange(ValidateBuildingCosts(data.Costs, knownMaterialIds));
		errors.AddRange(ValidateBuildingProduction(data.ProductionTable, knownMaterialIds));
		return [.. errors];
	}

	/// <summary>基础字段规则（编译期工具把结果定位到 [resource] 段）。</summary>
	public static string[] ValidateBuildingFields(
		uint id,
		string? displayName,
		float width,
		float depth,
		float height,
		float buildTime,
		int foundationX,
		int foundationY)
	{
		var errors = new List<string>();

		if (id == 0)
			errors.Add("Id 不能为 0");
		if (string.IsNullOrWhiteSpace(displayName))
			errors.Add(ErrorDisplayNameEmpty);
		if (foundationX < 1 || foundationY < 1)
			errors.Add("地基尺寸不能小于 1");
		if (width <= 0f || depth <= 0f || height <= 0f)
			errors.Add("宽/深/高必须大于 0");
		if (buildTime <= 0f)
			errors.Add("建造时间必须大于 0");

		return [.. errors];
	}

	/// <summary>成本规则：逐元素校验 + MaterialId 去重（编译期工具把结果定位到 Costs 行）。</summary>
	public static string[] ValidateBuildingCosts(
		IReadOnlyList<MaterialAmountData> costs,
		Func<IReadOnlyCollection<string>> knownMaterialIds)
	{
		var errors = new List<string>();
		if (costs == null) return [.. errors];

		var costIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var cost in costs)
		{
			errors.AddRange(ValidateMaterialAmount(cost.MaterialId, cost.Amount, knownMaterialIds));
			if (cost.MaterialId != null && !costIds.Add(cost.MaterialId))
				errors.Add($"成本中存在重复的材料 {cost.MaterialId}");
		}
		return [.. errors];
	}

	/// <summary>产出表规则：逐等级校验 + Level 去重（编译期工具把结果定位到 ProductionTable 行）。</summary>
	public static string[] ValidateBuildingProduction(
		IReadOnlyList<ProductionLevelData> table,
		Func<IReadOnlyCollection<string>> knownMaterialIds)
	{
		var errors = new List<string>();
		if (table == null) return [.. errors];

		var levels = new HashSet<uint>();
		foreach (var config in table)
		{
			errors.AddRange(ValidateProductionLevel(config.Level, config.IntervalSeconds, config.Outputs, knownMaterialIds));
			if (!levels.Add(config.Level))
				errors.Add($"产出表中存在重复的等级 {config.Level}");
		}
		return [.. errors];
	}

	private static bool ContainsIgnoreCase(Func<IReadOnlyCollection<string>> knownMaterialIds, string? id)
	{
		if (knownMaterialIds == null || id == null) return false;

		var ids = knownMaterialIds();
		if (ids == null) return false;

		foreach (var known in ids)
		{
			if (string.Equals(known, id, StringComparison.OrdinalIgnoreCase))
				return true;
		}
		return false;
	}
}
