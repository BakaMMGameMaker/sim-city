using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace MySimCity;

/// <summary>
/// 材料显示名数据库：从 res://Data/Materials 目录加载 MaterialDefinition。
/// 无缓存、按需读盘——目录内只有少量小文件，编辑器与运行时共用同一
/// 数据访问层且永远拿到最新配置，避免缓存失效问题。
/// MaterialType 枚举决定材料全集；未配置显示名的成员回退为枚举名。
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

		var seen = new HashSet<MaterialType>();
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
				else if (!Enum.IsDefined(def.Id))
				{
					GD.PushWarning($"跳过非法 Id 的材料定义：{path}（{(int)def.Id}）");
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

		list.Sort((a, b) => ((int)a.Id).CompareTo((int)b.Id));
		return list;
	}

	/// <summary>取材料显示名；未配置时回退为枚举名（如 Wood）或 #数值。</summary>
	public static string GetDisplayName(MaterialType id)
	{
		foreach (var def in LoadAllFromDisk())
		{
			if (def.Id == id)
				return def.DisplayName;
		}
		return FallbackName(id);
	}

	/// <summary>未配置显示名时的回退名：合法枚举值取枚举名，否则取 #数值。</summary>
	public static string FallbackName(MaterialType id)
	{
		return Enum.IsDefined(id) ? id.ToString() : $"#{(int)id}";
	}

	/// <summary>按 Id 升序返回全部已知材料（含未配置显示名的），供编辑器下拉框等使用。</summary>
	public static IReadOnlyList<(MaterialType Id, string Name)> GetAllNames()
	{
		var names = new Dictionary<MaterialType, string>();
		foreach (var def in LoadAllFromDisk())
			names[def.Id] = def.DisplayName;

		var list = new List<(MaterialType Id, string Name)>();
		foreach (MaterialType id in Enum.GetValues<MaterialType>())
			list.Add((id, names.TryGetValue(id, out var name) ? name : FallbackName(id)));

		list.Sort((a, b) => ((int)a.Id).CompareTo((int)b.Id));
		return list;
	}

	/// <summary>把成本列表格式化为「12 原木、5 木材」样式的文本；空列表返回空串。</summary>
	public static string FormatCosts(IEnumerable<MaterialAmount> costs)
	{
		if (costs == null) return "";

		var sb = new StringBuilder();
		foreach (var cost in costs)
		{
			if (cost == null) continue;
			if (sb.Length > 0) sb.Append("、");
			sb.Append(cost.Amount).Append(' ').Append(GetDisplayName(cost.MaterialId));
		}
		return sb.ToString();
	}
}
