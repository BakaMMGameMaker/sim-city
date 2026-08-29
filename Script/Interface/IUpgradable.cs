namespace MySimCity;

/// <summary>
/// 可升级对象：暴露当前等级。
/// </summary>
public interface IUpgradable
{
	uint Level { get; }
}
