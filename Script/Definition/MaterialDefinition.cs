using Godot;
using System.Collections.Generic;

namespace MySimCity;

/// <summary>
/// 材料定义（种类与显示名）。以 .tres 资源形式存放在 res://Data/Materials/，
/// 由编辑器插件（addons/definitions_editor 的「材料显示名定义」Dock）
/// 可视化维护，运行时经 MaterialDatabase 数据驱动加载。
/// Id 即材料唯一标识（字符串），与文件名一致。
/// [Tool]：编辑器加载本资源时按本类型实例化（否则会退化为基础 Resource）。
/// </summary>
[GlobalClass]
[Tool]
public partial class MaterialDefinition : Resource
{
	/// <summary>材料 Id，与文件名一致（小写字母开头，仅小写字母/数字/下划线）。</summary>
	[Export]
	public string Id { get; set; } = "";

	/// <summary>游戏内界面展示的名称，如「原木」。</summary>
	[Export]
	public string DisplayName { get; set; } = "";

	/// <summary>校验定义是否可保存/可加载。返回错误描述列表，为空表示合法。</summary>
	public string[] Validate()
	{
		var errors = new List<string>();

		if (!DefinitionIdValidation.IsValid(Id))
			errors.Add(DefinitionIdValidation.ErrorMessage);
		if (string.IsNullOrWhiteSpace(DisplayName))
			errors.Add("显示名不能为空");

		return [.. errors];
	}
}
