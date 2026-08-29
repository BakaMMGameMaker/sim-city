using Godot;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MySimCity;

/// <summary>
/// 建筑类型定义。以 .tres 资源形式存放在 res://Data/Buildings/，
/// 由编辑器插件（addons/building_definitions）可视化维护，运行时经
/// BuildingDefinitionDatabase 数据驱动加载。
/// [Tool]：编辑器加载本资源时按本类型实例化（否则会退化为基础 Resource）。
/// </summary>
[GlobalClass]
[Tool]
public partial class BuildingDefinition : Resource
{
	/// <summary>唯一标识，同时也是资源文件名（不含扩展名）。小写字母/数字/下划线。</summary>
	[Export]
	public string Id { get; set; } = "";

	/// <summary>游戏内按钮等界面展示的名称，如「住宅」。</summary>
	[Export]
	public string DisplayName { get; set; } = "";

	/// <summary>游戏内建造列表的排序权重，越小越靠前。</summary>
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
	public Building.BodyAlignMode BodyAlign { get; set; } = Building.BodyAlignMode.Center;

	[Export]
	public float BodyOffsetX { get; set; } = 0f;

	[Export]
	public float BodyOffsetZ { get; set; } = 0f;

	[Export]
	public MaterialAmount[] Costs { get; set; } = [];

	[Export]
	public ProducingLevelConfig[] ProductionTable { get; set; } = [];

	private static readonly Regex IdPattern = new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

	/// <summary>
	/// 校验定义是否可保存/可加载。返回错误描述列表，为空表示合法。
	/// </summary>
	public string[] Validate()
	{
		var errors = new List<string>();

		if (string.IsNullOrWhiteSpace(DisplayName))
			errors.Add("显示名不能为空");
		if (string.IsNullOrWhiteSpace(Id) || !IdPattern.IsMatch(Id))
			errors.Add("Id 需匹配 ^[a-z][a-z0-9_]*$（小写字母开头，仅小写字母/数字/下划线）");
		if (FoundationSize.X < 1 || FoundationSize.Y < 1)
			errors.Add("地基尺寸不能小于 1");
		if (Width <= 0f || Depth <= 0f || Height <= 0f)
			errors.Add("宽/深/高必须大于 0");
		if (BuildTime <= 0f)
			errors.Add("建造时间必须大于 0");

		return errors.ToArray();
	}
}
