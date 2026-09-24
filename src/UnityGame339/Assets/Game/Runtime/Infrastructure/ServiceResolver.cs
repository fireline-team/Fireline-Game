using System;
using System.Collections.Generic;
using Game339.Shared.Cookie;
using Game339.Shared.Cookie.Implementation;
using Game339.Shared.Cookie.Models;
using Game339.Shared.Infrastructure.DependencyInjection;
using Game339.Shared.StringReverse;
using Game339.Shared.StringReverse.Implementation;
using Game339.Shared.Infrastructure.DependencyInjection.Implementation;
using Game339.Shared.Infrastructure.Diagnostics;

namespace Game.Runtime
{
    public static class ServiceResolver
    {
        public static T Resolve<T>() => Container.Value.Resolve<T>();

        private static readonly Lazy<IMiniContainer> Container = new (() =>
        {
            var container = new MiniContainer();

            var logger = new UnityGameLogger();
            container.RegisterSingletonInstance<IGameLog>(logger);

            var stringService = new StringService(logger);
            container.RegisterSingletonInstance<IStringService>(stringService);

            var cookieInventory = new CookieIngredientInventory(new Dictionary<CookieIngredient, int>
            {
                { CookieIngredient.Chocolate,    5 },
                { CookieIngredient.Nuts,         5 },
                { CookieIngredient.PeanutButter, 5 },
                { CookieIngredient.Butterscotch, 5 },
                { CookieIngredient.Sugar,        5 },
            });
            container.RegisterSingletonInstance(cookieInventory);

            var cookieService = new CookieService(cookieInventory);
            container.RegisterSingletonInstance<ICookieService>(cookieService);

            return container;
        });
    }
}
