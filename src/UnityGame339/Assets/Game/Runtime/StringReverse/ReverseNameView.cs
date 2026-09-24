using Game339.Shared.StringReverse;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime
{
    public class ReverseNameView : ObserverMonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text nameLabel;

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
            var svc = ServiceResolver.Resolve<IStringService>();
            nameLabel.text = svc.Reverse(nameLabel.text);
        }
    }
}
