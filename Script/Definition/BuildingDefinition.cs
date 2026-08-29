using Godot;
using System.Collections.Generic;

namespace MySimCity;

/// <summary>
/// 建筑类型定义。以 .tres 资源形式存放在 res://Data/Buildings/，
/// 由编辑器插件（addons/definitions_editor）可视化维护，运行时经
/// BuildingDefinitionDatabase 数据驱动加载。
/// [Tool]：编辑器加载本资源时按本类型实例化（否则会退化为基础 Resource）。
/// </summary>
[GlobalClass]
[Tool]
public partial class BuildingDefinition : Resource
{
	/// <summary>身体网格相对地基的对齐方式。</summary>
	public enum BodyAlignMode
	{
		Center,
		Offset
	}

	[Export]
	public string Id { get; set; } = "";

	[Export]
	public string DisplayName { get; set; } = "";

	[Export]
	public int SortOrder { get; set; } = 0;

	[Export]
	public float Width { get; set; } = 3.2f;

	[Export]
	public float Depth { get; set; } = 3.2f;

	[Export]
	public float Height { get; set; } = 12.0f;

	[Export]
	public float BuildTime { get; set; } = 6.0f;

	[Export]
	public Vector2I FoundationSize { get; set; } = new(4, 4);

	[Export]
	public BodyAlignMode BodyAlign { get; set; } = BodyAlignMode.Center;

	[Export]
	public float BodyOffsetX { get; set; } = 0f;

	[Export]
	public float BodyOffsetZ { get; set; } = 0f;

	[Export]
	public MaterialAmount[] Costs { get; set; } = [];

	[Export]
	public ProducingLevelConfig[] ProductionTable { get; set; } = [];

	/// <summary>校验定义是否可保存/可加载。返回错误描述列表，为空表示合法。</summary>
	public string[] Validate()
	{
		var errors = new List<string>();

		if (string.IsNullOrWhiteSpace(DisplayName))
			errors.Add("显示名不能为空");
		if (!DefinitionIdValidation.IsValid(Id))
			errors.Add(DefinitionIdValidation.ErrorMessage);
		if (FoundationSize.X < 1 || FoundationSize.Y < 1)
			errors.Add("地基尺寸不能小于 1");
		if (Width <= 0f || Depth <= 0f || Height <= 0f)
			errors.Add("宽/深/高必须大于 0");
		if (BuildTime <= 0f)
			errors.Add("建造时间必须大于 0");

		return [.. errors];
	}
}
