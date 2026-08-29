using System.Collections.Generic;
using System.Text;

namespace MySimCity;

/// <summary>
/// 材料 Id → 显示名 的唯一映射表（编辑器和运行时共用）。
/// 新增材料时：在 MaterialIds 加常量，再在此处加一行。
/// </summary>
public static class MaterialNames
{
	private static readonly Dictionary<uint, string> Names = new()
	{
		{ MaterialIds.Wood, "原木" },
	};

	public static string GetName(uint id)
	{
		return Names.TryGetValue(id, out var name) ? name : $"#{id}";
	}

	/// <summary>按 Id 升序返回全部已知材料，供编辑器下拉框使用。</summary>
	public static IReadOnlyList<(uint Id, string Name)> GetAll()
	{
		var list = new List<(uint Id, string Name)>(Names.Count);
		foreach (var (id, name) in Names)
			list.Add((id, name));
		list.Sort((a, b) => a.Id.CompareTo(b.Id));
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
			sb.Append(cost.Amount).Append(' ').Append(GetName(cost.MaterialId));
		}
		return sb.ToString();
	}
}
