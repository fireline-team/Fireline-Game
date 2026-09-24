using Game339.Shared.Cookie;
using Game339.Shared.Infrastructure.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime
{
    public class CookieButtonView : ObserverMonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private string cookieName;

        protected override void Subscribe()
        {
            button.onClick.AddListener(OnClick);
        }

        protected override void Unsubscribe()
        {
            button.onClick.RemoveListener(OnClick);
        }

        private void OnClick()
        {
            var svc = ServiceResolver.Resolve<ICookieService>();
            var log = ServiceResolver.Resolve<IGameLog>();
            bool ok = svc.TryMakeCookie(cookieName);
            log.Info(ok ? $"Made {cookieName}!" : $"Missing ingredients for {cookieName}.");
        }
    }
}
