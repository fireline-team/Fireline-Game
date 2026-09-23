using System.Collections.Generic;
using Game339.Shared.Cookie;
using Game339.Shared.Cookie.Implementation;
using Game339.Shared.Cookie.Models;
using NUnit.Framework;

namespace Game339.Tests.Cookie;

public class CookieServiceTests
{
    private static CookieIngredientInventory MakeInventory(int each = 5)
    {
        return new CookieIngredientInventory(new Dictionary<CookieIngredient, int>
        {
            { CookieIngredient.Chocolate,    each },
            { CookieIngredient.Nuts,         each },
            { CookieIngredient.PeanutButter, each },
            { CookieIngredient.Butterscotch, each },
            { CookieIngredient.Sugar,        each },
        });
    }

    [Test]
    public void TryMakeCookie_NoMatchingIngredients_ReturnsTrue_NothingDeducted()
    {
        var inventory = MakeInventory(each: 5);
        var svc = new CookieService(inventory);
        var result = svc.TryMakeCookie("Vanilla Dream");
        Assert.That(result, Is.True);
        Assert.That(inventory.Get(CookieIngredient.Chocolate).Value,    Is.EqualTo(5));
        Assert.That(inventory.Get(CookieIngredient.Nuts).Value,         Is.EqualTo(5));
        Assert.That(inventory.Get(CookieIngredient.PeanutButter).Value, Is.EqualTo(5));
        Assert.That(inventory.Get(CookieIngredient.Butterscotch).Value, Is.EqualTo(5));
        Assert.That(inventory.Get(CookieIngredient.Sugar).Value,        Is.EqualTo(5));
    }

    [Test]
    public void TryMakeCookie_OneIngredient_SufficientStock_ReturnsTrueAndDeducts()
    {
        var inventory = MakeInventory(each: 3);
        var svc = new CookieService(inventory);
        var result = svc.TryMakeCookie("Chocolate Chip");
        Assert.That(result, Is.True);
        Assert.That(inventory.Get(CookieIngredient.Chocolate).Value, Is.EqualTo(2));
        Assert.That(inventory.Get(CookieIngredient.Nuts).Value,      Is.EqualTo(3));
    }

    [Test]
    public void TryMakeCookie_OneIngredient_ZeroStock_ReturnsFalse_NothingDeducted()
    {
        var inventory = new CookieIngredientInventory(new Dictionary<CookieIngredient, int>
        {
            { CookieIngredient.Chocolate,    0 },
            { CookieIngredient.Nuts,         5 },
            { CookieIngredient.PeanutButter, 5 },
            { CookieIngredient.Butterscotch, 5 },
            { CookieIngredient.Sugar,        5 },
        });
        var svc = new CookieService(inventory);
        var result = svc.TryMakeCookie("Chocolate Fudge");
        Assert.That(result, Is.False);
        Assert.That(inventory.Get(CookieIngredient.Chocolate).Value, Is.EqualTo(0));
    }

    [Test]
    public void TryMakeCookie_MultipleIngredients_AllAvailable_ReturnsTrueAndDeductsAll()
    {
        var inventory = MakeInventory(each: 10);
        var svc = new CookieService(inventory);
        var result = svc.TryMakeCookie("Chocolate Nuts Sugar Delight");
        Assert.That(result, Is.True);
        Assert.That(inventory.Get(CookieIngredient.Chocolate).Value, Is.EqualTo(9));
        Assert.That(inventory.Get(CookieIngredient.Nuts).Value,      Is.EqualTo(9));
        Assert.That(inventory.Get(CookieIngredient.Sugar).Value,     Is.EqualTo(9));
        Assert.That(inventory.Get(CookieIngredient.PeanutButter).Value, Is.EqualTo(10));
        Assert.That(inventory.Get(CookieIngredient.Butterscotch).Value, Is.EqualTo(10));
    }

    [Test]
    public void TryMakeCookie_MultipleIngredients_OneShort_ReturnsFalse_NothingDeducted()
    {
        var inventory = new CookieIngredientInventory(new Dictionary<CookieIngredient, int>
        {
            { CookieIngredient.Chocolate,    5 },
            { CookieIngredient.Nuts,         0 },
            { CookieIngredient.PeanutButter, 5 },
            { CookieIngredient.Butterscotch, 5 },
            { CookieIngredient.Sugar,        5 },
        });
        var svc = new CookieService(inventory);
        var result = svc.TryMakeCookie("Chocolate Nuts Cookie");
        Assert.That(result, Is.False);
        Assert.That(inventory.Get(CookieIngredient.Chocolate).Value, Is.EqualTo(5));
        Assert.That(inventory.Get(CookieIngredient.Nuts).Value,      Is.EqualTo(0));
    }

    [Test]
    public void TryMakeCookie_CaseInsensitiveMatch_DeductsCorrectly()
    {
        var inventory = MakeInventory(each: 2);
        var svc = new CookieService(inventory);
        var result = svc.TryMakeCookie("chocolate butterscotch bar");
        Assert.That(result, Is.True);
        Assert.That(inventory.Get(CookieIngredient.Chocolate).Value,    Is.EqualTo(1));
        Assert.That(inventory.Get(CookieIngredient.Butterscotch).Value, Is.EqualTo(1));
    }

    [Test]
    public void TryMakeCookie_ButterCookieName_MatchesNoIngredients_ReturnsTrue_NothingDeducted()
    {
        var inventory = MakeInventory(each: 5);
        var svc = new CookieService(inventory);
        var result = svc.TryMakeCookie("Butter Cookie");
        Assert.That(result, Is.True);
        foreach (CookieIngredient ing in Enum.GetValues(typeof(CookieIngredient)))
            Assert.That(inventory.Get(ing).Value, Is.EqualTo(5));
    }

    [Test]
    public void TryMakeCookie_DuplicateIngredientInName_DeductsOnlyOnce()
    {
        var inventory = MakeInventory(each: 3);
        var svc = new CookieService(inventory);
        var result = svc.TryMakeCookie("Chocolate Chocolate Chip");
        Assert.That(result, Is.True);
        Assert.That(inventory.Get(CookieIngredient.Chocolate).Value, Is.EqualTo(2));
    }
}
