using Godot;

namespace MySimCity;

/// <summary>
/// 定义类资源的抽象基类：要求子类实现 Validate()。
/// </summary>
public abstract partial class ValidatableResource : Resource
{
	protected ValidatableResource()
	{
	}

	/// <summary>校验并返回错误列表；无错误返回空数组。</summary>
	public abstract string[] Validate();
}
