using Godot;
using MySimCity.Definitions;

namespace MySimCity;

/// <summary>
/// 材料定义。以 .tres 资源形式存放在 res://Data/Materials/（文件名 = {Id}.tres），
/// 由编辑器插件维护，运行时经 MaterialDatabase 加载。
/// Id 即材料唯一标识（字符串），与文件名一致。
/// [Tool]：编辑器加载本资源时按本类型实例化（否则会退化为基础 Resource）。
/// 无需 [GlobalClass]：项目中没有按全局类名引用本类型的地方。
/// </summary>
[Tool]
public partial class MaterialDefinition : ValidatableResource, IMaterialDefinition
{
	/// <summary>材料 Id，与文件名一致（小写字母开头，仅小写字母/数字/下划线）。</summary>
	[Export]
	public string Id { get; set; } = "";

	/// <summary>游戏内界面展示的名称，如「原木」。</summary>
	[Export]
	public string DisplayName { get; set; } = "";

	public override string[] Validate()
	{
		return DefinitionValidation.ValidateMaterialDefinition(Id ?? "", DisplayName ?? "");
	}
}
