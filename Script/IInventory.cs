using System.Collections.Generic;

namespace MySimCity;

public interface IInventory
{
	void Add(uint materialId, uint amount);

	bool CanAfford(uint materialId, uint amount);

	bool TrySpend(uint materialId, uint amount);

	bool CanAfford(IEnumerable<MaterialAmount> costs);

	bool TrySpend(IEnumerable<MaterialAmount> costs);
}
