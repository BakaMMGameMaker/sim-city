using System.Collections.Generic;

namespace MySimCity;

public interface IInventory
{
	void Add(string materialId, uint amount);

	bool CanAfford(string materialId, uint amount);

	bool TrySpend(string materialId, uint amount);

	bool CanAfford(IEnumerable<IMaterialAmount> costs);

	bool TrySpend(IEnumerable<IMaterialAmount> costs);
}
