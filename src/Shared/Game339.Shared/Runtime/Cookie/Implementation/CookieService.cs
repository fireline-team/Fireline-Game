using System;
using System.Collections.Generic;
using Game339.Shared.Cookie.Models;

namespace Game339.Shared.Cookie.Implementation
{
    public class CookieService : ICookieService
    {
        private readonly CookieIngredientInventory _inventory;

        public CookieService(CookieIngredientInventory inventory)
        {
            _inventory = inventory;
        }

        public bool TryMakeCookie(string cookieName)
        {
            var required = new List<CookieIngredient>();

            foreach (CookieIngredient ingredient in Enum.GetValues(typeof(CookieIngredient)))
            {
                if (cookieName.IndexOf(ingredient.ToString(), StringComparison.OrdinalIgnoreCase) >= 0)
                    required.Add(ingredient);
            }

            foreach (var ingredient in required)
            {
                if (_inventory.Get(ingredient).Value < 1)
                    return false;
            }

            foreach (var ingredient in required)
                _inventory.Get(ingredient).Value--;

            return true;
        }
    }
}
