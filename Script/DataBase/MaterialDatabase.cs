using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace MySimCity;

/// <summary>
/// 材料数据库：从 res://Data/Materials 目录加载 MaterialDefinition。
/// 材料 Id 为字符串（由 .tres 定义）；未配置显示名时回退为 Id 本身。
/// </summary>
public static class MaterialDatabase
{
	public const string FolderPath = "res://Data/Materials";

	public static List<MaterialDefinition> LoadAllFromDisk()
	{
		var list = new List<MaterialDefinition>();

		if (!DirAccess.DirExistsAbsolute(FolderPath))
			return list;

		using var dir = DirAccess.Open(FolderPath);
		if (dir == null)
		{
			GD.PushWarning($"无法打开材料定义目录：{FolderPath}");
			return list;
		}

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		dir.ListDirBegin();
		var fileName = dir.GetNext();
		while (fileName != "")
		{
			if (!dir.CurrentIsDir() && fileName.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
			{
				var path = $"{FolderPath}/{fileName}";
				var def = ResourceLoader.Load<MaterialDefinition>(path);
				if (def == null)
				{
					GD.PushWarning($"跳过无法解析的材料定义：{path}");
				}
				else if (string.IsNullOrWhiteSpace(def.Id))
				{
					GD.PushWarning($"跳过 Id 为空的材料定义：{path}");
				}
				else if (!seen.Add(def.Id))
				{
					GD.PushWarning($"跳过 Id 重复的材料定义：{def.Id}（{path}）");
				}
				else
				{
					var errors = def.Validate();
					if (errors.Length > 0)
						GD.PushWarning($"材料定义 {def.Id} 校验失败：{string.Join("；", errors)}");
					else
						list.Add(def);
				}
			}
			fileName = dir.GetNext();
		}
		dir.ListDirEnd();

		list.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
		return list;
	}

	public static string GetDisplayName(string id)
	{
		if (string.IsNullOrWhiteSpace(id))
			return id ?? "";

		foreach (var def in LoadAllFromDisk())
		{
			if (string.Equals(def.Id, id, StringComparison.OrdinalIgnoreCase))
				return def.DisplayName;
		}
		return id;
	}

	public static IReadOnlyList<MaterialDefinition> GetAllNames()
	{
		return LoadAllFromDisk();
	}

	public static string FormatCosts(IEnumerable<MaterialAmount> costs)
	{
		if (costs == null) return "";

		var sb = new StringBuilder();
		foreach (var cost in costs)
		{
			if (cost == null) continue;
			if (sb.Length > 0) sb.Append(',');
			sb.Append(cost.Amount).Append(' ').Append(GetDisplayName(cost.MaterialId));
		}
		return sb.ToString();
	}
}
