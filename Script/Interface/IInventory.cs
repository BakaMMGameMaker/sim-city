using System.Collections.Generic;

namespace MySimCity;

public interface IInventory
{
	void Add(MaterialType materialId, uint amount);

	bool CanAfford(MaterialType materialId, uint amount);

	bool TrySpend(MaterialType materialId, uint amount);

	bool CanAfford(IEnumerable<MaterialAmount> costs);

	bool TrySpend(IEnumerable<MaterialAmount> costs);
}
