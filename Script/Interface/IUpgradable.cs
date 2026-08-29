namespace MySimCity;

/// <summary>
/// 可升级对象：暴露当前等级。
/// Building 持有等级，组件通过 Owner 读取，升级后组件自动套用新等级区间。
/// </summary>
public interface IUpgradable
{
	int Level { get; }
}
