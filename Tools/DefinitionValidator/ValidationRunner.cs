using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MySimCity.Definitions;

namespace MySimCity.BuildTools;

/// <summary>
/// 校验流程编排：
/// 1. 全项目 .tres/.tscn 的 ext_resource res:// 引用必须指向真实存在的文件；
/// 2. Data/Materials 下的材料定义逐项跑 DefinitionValidation（与运行时同一份规则）；
/// 3. Data/Buildings 下的建筑定义逐项跑 DefinitionValidation，并做跨文件 Id 唯一、
///    文件名与 Id 一致等检查。
/// 所有规则来自 Tools/DefinitionRules，游戏运行时 ValidatableResource.Validate
/// 调用的正是同一套函数，错误文案完全一致。
/// </summary>
public static class ValidationRunner
{
	private static readonly string[] ExcludedDirs = { ".git", ".godot", ".vs", ".vscode", ".idea", "bin", "obj" };

	public sealed class Result
	{
		public readonly List<DefinitionError> Errors = new();
		/// <summary>扫描的 .tres/.tscn 总数（ext_resource 路径检查）。</summary>
		public int FilesScanned;
		/// <summary>Data 目录下实际校验的定义数量。</summary>
		public int DefinitionsChecked;
	}

	public static Result ValidateProject(string projectDir)
	{
		var result = new Result();
		var root = Path.GetFullPath(projectDir);

		CheckExtResourcePaths(root, result);

		var materialIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		ValidateMaterials(Path.Combine(root, "Data", "Materials"), result, materialIds);
		ValidateBuildings(Path.Combine(root, "Data", "Buildings"), result, materialIds);

		return result;
	}

	// ------------------------------------------------------------------
	// 1. ext_resource 路径存在性（全项目）
	// ------------------------------------------------------------------

	private static void CheckExtResourcePaths(string root, Result result)
	{
		var pathPattern = new Regex(@"path\s*=\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

		foreach (var pattern in new[] { "*.tres", "*.tscn" })
		{
			foreach (var file in Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
			{
				var rel = file.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
				if (IsExcluded(rel)) continue;

				result.FilesScanned++;

				string[] lines;
				try
				{
					lines = File.ReadAllLines(file);
				}
				catch (Exception ex)
				{
					result.Errors.Add(new DefinitionError(file, 0, $"无法读取文件：{ex.Message}"));
					continue;
				}

				for (int i = 0; i < lines.Length; i++)
				{
					var trimmed = lines[i].TrimStart();
					if (!trimmed.StartsWith("[ext_resource", StringComparison.Ordinal)) continue;

					var match = pathPattern.Match(trimmed);
					if (!match.Success) continue;

					var resPath = match.Groups[1].Value;
					if (!resPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase)) continue;

					var local = resPath.Substring("res://".Length);
					var full = Path.GetFullPath(Path.Combine(root, local.Replace('/', Path.DirectorySeparatorChar)));
					if (File.Exists(full)) continue;

					result.Errors.Add(new DefinitionError(file, i + 1, $"引用了不存在的资源 {resPath}"));
				}
			}
		}
	}

	private static bool IsExcluded(string relativePath)
	{
		foreach (var segment in relativePath.Split('/'))
		{
			foreach (var excluded in ExcludedDirs)
			{
				if (string.Equals(segment, excluded, StringComparison.OrdinalIgnoreCase))
					return true;
			}
		}
		return false;
	}

	// ------------------------------------------------------------------
	// 2. 材料定义
	// ------------------------------------------------------------------

	private static void ValidateMaterials(string folder, Result result, HashSet<string> materialIds)
	{
		if (!Directory.Exists(folder)) return;

		foreach (var file in Directory.EnumerateFiles(folder, "*.tres").OrderBy(f => f, StringComparer.Ordinal))
		{
			result.DefinitionsChecked++;
			var doc = TresParser.Parse(file, result.Errors);
			if (doc.Root == null) continue;

			var scriptPath = ResolveSectionScript(doc, doc.Root, result, "材料资源");
			if (scriptPath == null) continue;
			if (!scriptPath.EndsWith("/MaterialDefinition.cs", StringComparison.Ordinal))
			{
				result.Errors.Add(new DefinitionError(file, doc.Root.ScriptLine,
					$"材料目录下的资源必须是 MaterialDefinition：{scriptPath}"));
				continue;
			}

			var id = GetString(doc.Root, "Id", result, file, "");
			var displayName = GetString(doc.Root, "DisplayName", result, file, "");
			var idLine = GetPropertyLine(doc.Root, "Id", doc.Root.HeaderLine);
			var displayNameLine = GetPropertyLine(doc.Root, "DisplayName", doc.Root.HeaderLine);

			foreach (var message in DefinitionValidation.ValidateMaterialDefinition(id, displayName))
			{
				var line = message == DefinitionValidation.ErrorDisplayNameEmpty ? displayNameLine : idLine;
				result.Errors.Add(new DefinitionError(file, line, message));
			}

			if (!string.IsNullOrEmpty(id))
			{
				var expectedFile = $"{id}.tres";
				if (!string.Equals(expectedFile, Path.GetFileName(file), StringComparison.OrdinalIgnoreCase))
				{
					result.Errors.Add(new DefinitionError(file, doc.Root.HeaderLine,
						$"材料文件名应为 {expectedFile}（当前：{Path.GetFileName(file)}）"));
				}

				if (!materialIds.Add(id))
					result.Errors.Add(new DefinitionError(file, idLine, $"材料 Id 重复：{id}"));
			}
		}
	}

	// ------------------------------------------------------------------
	// 3. 建筑定义
	// ------------------------------------------------------------------

	private static void ValidateBuildings(string folder, Result result, HashSet<string> materialIds)
	{
		if (!Directory.Exists(folder)) return;

		var usedIds = new HashSet<uint>();
		foreach (var file in Directory.EnumerateFiles(folder, "*.tres").OrderBy(f => f, StringComparer.Ordinal))
		{
			result.DefinitionsChecked++;
			var doc = TresParser.Parse(file, result.Errors);
			if (doc.Root == null) continue;

			var scriptPath = ResolveSectionScript(doc, doc.Root, result, "建筑资源");
			if (scriptPath == null) continue;
			if (!scriptPath.EndsWith("/BuildingDefinition.cs", StringComparison.Ordinal))
			{
				result.Errors.Add(new DefinitionError(file, doc.Root.ScriptLine,
					$"建筑目录下的资源必须是 BuildingDefinition：{scriptPath}"));
				continue;
			}

			var root = doc.Root;
			var id = GetUInt(root, "Id", result, file, 0);
			var displayName = GetString(root, "DisplayName", result, file, "");
			var width = GetFloat(root, "Width", result, file, 3.2f);
			var depth = GetFloat(root, "Depth", result, file, 3.2f);
			var height = GetFloat(root, "Height", result, file, 12.0f);
			var buildTime = GetFloat(root, "BuildTime", result, file, 6.0f);
			var foundation = GetVector2I(root, "FoundationSize", result, file, 4, 4);
			// 以下属性无校验规则，仅做类型解析检查（解析失败会报错）
			_ = GetInt(root, "BodyAlign", result, file, 0);
			_ = GetFloat(root, "BodyOffsetX", result, file, 0f);
			_ = GetFloat(root, "BodyOffsetZ", result, file, 0f);

			var costs = ResolveAmountList(doc, root, "Costs", result);
			var production = ResolveProductionList(doc, root, "ProductionTable", result);

			// 基础字段错误 → [resource] 段头行
			foreach (var message in DefinitionValidation.ValidateBuildingFields(
				id, displayName, width, depth, height, buildTime, foundation.X, foundation.Y))
			{
				result.Errors.Add(new DefinitionError(file, root.HeaderLine, message));
			}

			// 成本相关错误 → Costs 行；产出表相关错误 → ProductionTable 行
			var costsLine = GetPropertyLine(root, "Costs", root.HeaderLine);
			foreach (var message in DefinitionValidation.ValidateBuildingCosts(costs, () => materialIds))
				result.Errors.Add(new DefinitionError(file, costsLine, message));

			var productionLine = GetPropertyLine(root, "ProductionTable", root.HeaderLine);
			foreach (var message in DefinitionValidation.ValidateBuildingProduction(production, () => materialIds))
				result.Errors.Add(new DefinitionError(file, productionLine, message));

			// 建筑文件名自由（约定 {Id}_{显示名}.tres，非强制），只按内容检查 Id 跨文件唯一
			if (id != 0 && !usedIds.Add(id))
				result.Errors.Add(new DefinitionError(file, root.HeaderLine, $"建筑 Id 重复：{id}"));
		}
	}

	// ------------------------------------------------------------------
	// 子资源解析
	// ------------------------------------------------------------------

	private static List<MaterialAmountData> ResolveAmountList(
		TresDocument doc, ResourceSection owner, string key, Result result)
	{
		var list = new List<MaterialAmountData>();
		foreach (var refId in GetSubRefArray(owner, key, result, doc.FilePath))
		{
			if (!doc.SubById.TryGetValue(refId, out var sub))
			{
				result.Errors.Add(new DefinitionError(doc.FilePath,
					GetPropertyLine(owner, key, owner.HeaderLine),
					$"引用了不存在的子资源 {refId}"));
				continue;
			}

			var scriptPath = ResolveSectionScript(doc, sub, result, $"子资源 {refId}");
			if (scriptPath == null) continue;
			if (!scriptPath.EndsWith("/MaterialAmount.cs", StringComparison.Ordinal))
			{
				result.Errors.Add(new DefinitionError(doc.FilePath, sub.HeaderLine,
					$"子资源 {refId} 必须是 MaterialAmount：{scriptPath}"));
				continue;
			}

			var materialId = GetString(sub, "MaterialId", result, doc.FilePath, "");
			var amount = GetUInt(sub, "Amount", result, doc.FilePath, 1);
			list.Add(new MaterialAmountData(materialId, amount));
		}
		return list;
	}

	private static List<ProductionLevelData> ResolveProductionList(
		TresDocument doc, ResourceSection owner, string key, Result result)
	{
		var list = new List<ProductionLevelData>();
		foreach (var refId in GetSubRefArray(owner, key, result, doc.FilePath))
		{
			if (!doc.SubById.TryGetValue(refId, out var sub))
			{
				result.Errors.Add(new DefinitionError(doc.FilePath,
					GetPropertyLine(owner, key, owner.HeaderLine),
					$"引用了不存在的子资源 {refId}"));
				continue;
			}

			var scriptPath = ResolveSectionScript(doc, sub, result, $"子资源 {refId}");
			if (scriptPath == null) continue;
			if (!scriptPath.EndsWith("/ProducingLevelConfig.cs", StringComparison.Ordinal))
			{
				result.Errors.Add(new DefinitionError(doc.FilePath, sub.HeaderLine,
					$"子资源 {refId} 必须是 ProducingLevelConfig：{scriptPath}"));
				continue;
			}

			var level = GetUInt(sub, "Level", result, doc.FilePath, 1);
			var interval = GetFloat(sub, "IntervalSeconds", result, doc.FilePath, 10.0f);
			var outputs = ResolveAmountList(doc, sub, "Outputs", result);
			list.Add(new ProductionLevelData(level, interval, outputs));
		}
		return list;
	}

	private static string? ResolveSectionScript(TresDocument doc, ResourceSection section, Result result, string context)
	{
		if (string.IsNullOrEmpty(section.ScriptExtRef))
		{
			result.Errors.Add(new DefinitionError(doc.FilePath, section.HeaderLine, $"{context}缺少 script 声明"));
			return null;
		}

		var path = doc.ResolveExtPath(section.ScriptExtRef);
		if (path == null)
		{
			result.Errors.Add(new DefinitionError(doc.FilePath, section.ScriptLine,
				$"{context}引用了不存在的 ext_resource：{section.ScriptExtRef}"));
			return null;
		}
		return path;
	}

	// ------------------------------------------------------------------
	// 类型化属性读取（缺省值对齐 C# 类的默认值）
	// ------------------------------------------------------------------

	private static int GetPropertyLine(ResourceSection section, string key, int fallback)
	{
		return section.TryGet(key, out var prop) ? prop.Line : fallback;
	}

	private static string GetString(ResourceSection section, string key, Result result, string file, string fallback)
	{
		if (!section.TryGet(key, out var prop)) return fallback;
		var value = TresParser.ParseValue(prop.RawValue, file, prop.Line, key, result.Errors);
		if (value.Kind == ValueKind.String) return value.StringValue ?? "";
		if (value.Kind == ValueKind.Null) return fallback;
		result.Errors.Add(new DefinitionError(file, prop.Line, $"属性 {key} 应为字符串：{prop.RawValue}"));
		return fallback;
	}

	private static uint GetUInt(ResourceSection section, string key, Result result, string file, uint fallback)
	{
		if (!section.TryGet(key, out var prop)) return fallback;
		var value = TresParser.ParseValue(prop.RawValue, file, prop.Line, key, result.Errors);
		if (value.Kind == ValueKind.Int && value.IntValue >= 0 && value.IntValue <= uint.MaxValue)
			return (uint)value.IntValue;
		result.Errors.Add(new DefinitionError(file, prop.Line, $"属性 {key} 应为 0~{uint.MaxValue} 的整数：{prop.RawValue}"));
		return fallback;
	}

	private static int GetInt(ResourceSection section, string key, Result result, string file, int fallback)
	{
		if (!section.TryGet(key, out var prop)) return fallback;
		var value = TresParser.ParseValue(prop.RawValue, file, prop.Line, key, result.Errors);
		if (value.Kind == ValueKind.Int && value.IntValue >= int.MinValue && value.IntValue <= int.MaxValue)
			return (int)value.IntValue;
		result.Errors.Add(new DefinitionError(file, prop.Line, $"属性 {key} 应为整数：{prop.RawValue}"));
		return fallback;
	}

	private static float GetFloat(ResourceSection section, string key, Result result, string file, float fallback)
	{
		if (!section.TryGet(key, out var prop)) return fallback;
		var value = TresParser.ParseValue(prop.RawValue, file, prop.Line, key, result.Errors);
		if (value.Kind == ValueKind.Float) return (float)value.FloatValue;
		if (value.Kind == ValueKind.Int) return value.IntValue;
		result.Errors.Add(new DefinitionError(file, prop.Line, $"属性 {key} 应为数字：{prop.RawValue}"));
		return fallback;
	}

	private static (int X, int Y) GetVector2I(ResourceSection section, string key, Result result, string file,
		int fallbackX, int fallbackY)
	{
		if (!section.TryGet(key, out var prop)) return (fallbackX, fallbackY);
		var value = TresParser.ParseValue(prop.RawValue, file, prop.Line, key, result.Errors);
		if (value.Kind == ValueKind.Vector2I) return (value.X, value.Y);
		result.Errors.Add(new DefinitionError(file, prop.Line, $"属性 {key} 应为 Vector2i：{prop.RawValue}"));
		return (fallbackX, fallbackY);
	}

	private static IReadOnlyList<string> GetSubRefArray(ResourceSection section, string key, Result result, string file)
	{
		if (!section.TryGet(key, out var prop)) return Array.Empty<string>();
		var value = TresParser.ParseValue(prop.RawValue, file, prop.Line, key, result.Errors);
		if (value.Kind == ValueKind.SubRefArray) return value.RefIds ?? Array.Empty<string>();
		if (value.Kind == ValueKind.Null) return Array.Empty<string>();
		result.Errors.Add(new DefinitionError(file, prop.Line, $"属性 {key} 应为 SubResource 数组：{prop.RawValue}"));
		return Array.Empty<string>();
	}
}
