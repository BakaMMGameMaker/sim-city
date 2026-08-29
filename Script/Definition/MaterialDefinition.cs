using Godot;
using System;
using System.Collections.Generic;

namespace MySimCity;

/// <summary>
/// 材料显示名定义。以 .tres 资源形式存放在 res://Data/Materials/，
/// 由编辑器插件（addons/building_definitions 的「材料类型定义」Dock）
/// 可视化维护，运行时经 MaterialDatabase 数据驱动加载。
/// MaterialType 枚举仍是材料种类的唯一来源，本资源只负责显示名。
/// [Tool]：编辑器加载本资源时按本类型实例化（否则会退化为基础 Resource）。
/// </summary>
[GlobalClass]
[Tool]
public partial class MaterialDefinition : Resource
{
	/// <summary>材料 Id（MaterialType 枚举值），与文件名对应（枚举名小写）。</summary>
	[Export]
	public MaterialType Id { get; set; } = MaterialType.Wood;

	/// <summary>游戏内界面展示的名称，如「原木」。</summary>
	[Export]
	public string DisplayName { get; set; } = "";

	/// <summary>校验定义是否可保存/可加载。返回错误描述列表，为空表示合法。</summary>
	public string[] Validate()
	{
		var errors = new List<string>();

		if (!Enum.IsDefined(Id))
			errors.Add($"非法材料 Id：{(int)Id}");
		if (string.IsNullOrWhiteSpace(DisplayName))
			errors.Add("显示名不能为空");

		return errors.ToArray();
	}
}
