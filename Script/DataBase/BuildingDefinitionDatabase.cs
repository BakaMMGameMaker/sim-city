using Godot;
using System;
using System.Collections.Generic;

namespace MySimCity;

/// <summary>
/// 建筑定义的运行时数据库：从 res://Data/Buildings 目录加载所有 .tres 定义。
/// 数据由编辑器插件维护，运行时只读、懒加载、缓存。
/// 缓存结构：Dictionary（Id → 定义，大小写不敏感，O(1) 查找）+ 排序列表（UI 顺序遍历）。
/// </summary>
public static class BuildingDefinitionDatabase
{
	public const string FolderPath = "res://Data/Buildings";

	private static IReadOnlyList<BuildingDefinition> _sorted;
	private static IReadOnlyDictionary<string, BuildingDefinition> _byId;
	private static bool _warnedMissingFolder;

	/// <summary>按 SortOrder、DisplayName 排序后的全部定义，供 UI 顺序遍历。</summary>
	public static IReadOnlyList<BuildingDefinition> All
	{
		get
		{
			EnsureLoaded();
			return _sorted;
		}
	}

	/// <summary>按 Id 精确查找（大小写不敏感），O(1)。</summary>
	public static BuildingDefinition GetById(string id)
	{
		if (string.IsNullOrEmpty(id)) return null;
		EnsureLoaded();
		return _byId.TryGetValue(id, out var def) ? def : null;
	}

	private static void EnsureLoaded()
	{
		if (_sorted != null) return;

		var byId = LoadAll();

		var sorted = new List<BuildingDefinition>(byId.Values);
		sorted.Sort((a, b) =>
		{
			var byOrder = a.SortOrder.CompareTo(b.SortOrder);
			return byOrder != 0
				? byOrder
				: string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
		});

		_byId = byId;
		_sorted = sorted;
	}

	private static Dictionary<string, BuildingDefinition> LoadAll()
	{
		var byId = new Dictionary<string, BuildingDefinition>(StringComparer.OrdinalIgnoreCase);

		if (!DirAccess.DirExistsAbsolute(FolderPath))
		{
			if (!_warnedMissingFolder)
			{
				GD.PushWarning($"建筑定义目录不存在：{FolderPath}");
				_warnedMissingFolder = true;
			}
			return byId;
		}

		using var dir = DirAccess.Open(FolderPath);
		if (dir == null)
		{
			GD.PushWarning($"无法打开建筑定义目录：{FolderPath}");
			return byId;
		}

		dir.ListDirBegin();
		var fileName = dir.GetNext();
		while (fileName != "")
		{
			if (!dir.CurrentIsDir() && fileName.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
			{
				var path = $"{FolderPath}/{fileName}";
				var def = ResourceLoader.Load<BuildingDefinition>(path);
				if (def == null)
				{
					GD.PushWarning($"跳过无法解析的建筑定义：{path}");
				}
				else if (string.IsNullOrWhiteSpace(def.Id))
				{
					GD.PushWarning($"跳过 Id 为空的建筑定义：{path}");
				}
				else if (byId.ContainsKey(def.Id))
				{
					GD.PushWarning($"跳过 Id 重复的建筑定义：{def.Id}（{path}）");
				}
				else
				{
					var errors = def.Validate();
					if (errors.Length > 0)
						GD.PushWarning($"建筑定义 {def.Id} 校验失败：{string.Join("；", errors)}");
					else
						byId.Add(def.Id, def);
				}
			}
			fileName = dir.GetNext();
		}
		dir.ListDirEnd();

		return byId;
	}
}
