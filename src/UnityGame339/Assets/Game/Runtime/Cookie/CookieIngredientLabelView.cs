using System;
using Game339.Shared.Cookie.Models;
using TMPro;
using UnityEngine;

namespace Game.Runtime
{
    public class CookieIngredientLabelView : ObserverMonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private string ingredientName;

        private CookieIngredient ParseCookieIngredientLabel()
        {
            return Enum.Parse<CookieIngredient>(ingredientName);
        }

        protected override void Subscribe()
        {
            var ingredient = ParseCookieIngredientLabel();
            var inventory = ServiceResolver.Resolve<CookieIngredientInventory>();
            inventory.Get(ingredient).ChangeEvent += OnChange;
        }

        protected override void Unsubscribe()
        {
            var inventory = ServiceResolver.Resolve<CookieIngredientInventory>();
            var ingredient = ParseCookieIngredientLabel();
            inventory.Get(ingredient).ChangeEvent -= OnChange;
        }

        private void OnChange(int value)
        {
            label.text = ingredientName + ": " + value;
        }
    }
}
