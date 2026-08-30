using Godot;
using System;
using System.Collections.Generic;

namespace MySimCity;

/// <summary>
/// 建筑定义的运行时数据库：从 res://Data/Buildings 目录加载所有 .tres 定义。
/// 数据由编辑器插件维护，运行时只读、懒加载、缓存。
/// 对消费方只暴露 IBuildingDefinition，按 Id 升序排序。
/// </summary>
public static class BuildingDefinitionDatabase
{
	public const string FolderPath = "res://Data/Buildings";

	private static IReadOnlyList<IBuildingDefinition> _sorted;
	private static IReadOnlyDictionary<uint, IBuildingDefinition> _byId;
	private static bool _warnedMissingFolder;

	public static IReadOnlyList<IBuildingDefinition> All
	{
		get
		{
			EnsureLoaded();
			return _sorted;
		}
	}

	public static IBuildingDefinition GetById(uint id)
	{
		EnsureLoaded();
		return _byId.TryGetValue(id, out var def) ? def : null;
	}

	private static void EnsureLoaded()
	{
		if (_sorted != null) return;

		var byId = LoadAll();

		var sorted = new List<IBuildingDefinition>(byId.Values);
		sorted.Sort((a, b) => a.Id.CompareTo(b.Id));

		_byId = byId;
		_sorted = sorted;
	}

	private static Dictionary<uint, IBuildingDefinition> LoadAll()
	{
		var byId = new Dictionary<uint, IBuildingDefinition>();

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
				else if (def.Id == 0)
				{
					GD.PushWarning($"跳过 Id 为 0 的建筑定义：{path}");
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
