using System.Collections.Generic;

namespace MySimCity;

public interface IInventory
{
	void Add(string materialId, uint amount);

	bool CanAfford(string materialId, uint amount);

	bool TrySpend(string materialId, uint amount);

	bool CanAfford(IEnumerable<MaterialAmount> costs);

	bool TrySpend(IEnumerable<MaterialAmount> costs);
}
