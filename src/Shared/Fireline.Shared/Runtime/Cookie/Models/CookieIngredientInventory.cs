using System.Collections.Generic;
using Fireline.Shared.Infrastructure;

namespace Fireline.Shared.Cookie.Models
{
    public class CookieIngredientInventory
    {
        private Dictionary<CookieIngredient, ObservableValue<int>> _ingredientCounts { get; }

        public CookieIngredientInventory(Dictionary<CookieIngredient, int> initialIngredientCounts)
        {
            _ingredientCounts = new Dictionary<CookieIngredient, ObservableValue<int>>();
            foreach (var kvp in initialIngredientCounts)
                _ingredientCounts[kvp.Key] = new ObservableValue<int>(kvp.Value);
        }

        public ObservableValue<int> Get(CookieIngredient ingredient)
        {
            return _ingredientCounts[ingredient];
        }
    }
}
