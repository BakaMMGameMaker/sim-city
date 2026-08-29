using System;

namespace MySimCity;

/// <summary>
/// 暴露等级与建造完成事件。
/// 将来加入建筑拆除时，可追加 Demolished 事件供组件停产出。
/// </summary>
public interface IProducibleBuilding : IUpgradable
{
	/// <summary>
	/// 建造完成时触发；sender 为完成建造的 owner，
	/// 订阅方（产出组件）据此判断是否与自己的 owner 匹配。
	/// </summary>
	event Action<IProducibleBuilding> ConstructionFinished;
}
