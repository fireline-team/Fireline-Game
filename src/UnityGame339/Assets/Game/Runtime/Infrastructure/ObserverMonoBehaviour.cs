using Game339.Shared.Infrastructure.Diagnostics;
using UnityEngine;

namespace Game.Runtime
{
    // Acts as a lightweight ViewModel in MVVM — subclasses bind to model events and push
    // updates to their serialized UI references. Unity's scene hierarchy is the View itself,
    // so no separate View layer is needed.
    public abstract class ObserverMonoBehaviour : MonoBehaviour
    {
        private static IGameLog Log => ServiceResolver.Resolve<IGameLog>();
        
        private bool _didCallStart;
        private bool _didSubscribe;

        protected virtual void Awake()
        {
            _didCallStart = false;
            _didSubscribe = false;
        }

        protected virtual void Start()
        {
            _didCallStart = true;
            TrySubscribe();
        }

        protected virtual void OnEnable()
        {
            TrySubscribe();
        }

        protected virtual void OnDisable()
        {
            TryUnsubscribe();
        }

        private void TrySubscribe()
        {
            if (!_didSubscribe && _didCallStart)
            {
                _didSubscribe = true;
                Log.Info(GetType().Name + "." + nameof(TrySubscribe));
                Subscribe();
            }
        }

        private void TryUnsubscribe()
        {
            if (_didSubscribe)
            {
                _didSubscribe = false;
                Log.Info(GetType().Name + "." + nameof(TryUnsubscribe));
                Unsubscribe();
            }
        }

        protected abstract void Subscribe();

        protected abstract void Unsubscribe();
    }
}
