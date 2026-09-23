using System.Collections.Generic;
using Game339.Shared.Infrastructure;

namespace Game339.Shared.Cookie.Models
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
