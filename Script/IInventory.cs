using System.Collections.Generic;

namespace MySimCity;

/// <summary>
/// 材料库存抽象。所有需要读写材料的类必须通过此接口注入，禁止直接依赖 Inventory 单例。
/// </summary>
public interface IInventory
{
	void Add(uint materialId, uint amount);

	bool CanAfford(uint materialId, uint amount);

	bool TrySpend(uint materialId, uint amount);

	/// <summary>批量查询是否可支付（所有材料都足够才返回 true）</summary>
	bool CanAfford(IEnumerable<MaterialAmount> costs);

	/// <summary>批量尝试扣除。任一材料不足则全部不扣并返回 false</summary>
	bool TrySpend(IEnumerable<MaterialAmount> costs);
}
