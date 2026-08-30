using System;

namespace MySimCity;

/// <summary>
/// 暴露等级与建造完成事件。
/// 将来加入建筑拆除时，可追加 Demolished 事件供组件停产出。
/// </summary>
public interface IProducibleBuilding : IUpgradable
{
	/// <summary>建造完成时触发。</summary>
	event Action ConstructionFinished;
}
