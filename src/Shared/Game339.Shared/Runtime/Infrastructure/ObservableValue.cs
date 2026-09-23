using System;
using System.Collections.Generic;

namespace Game339.Shared.Infrastructure
{
    public class ObservableValue<T> : IEquatable<T>
    {
        private static readonly IEqualityComparer<T> DefaultComparer = EqualityComparer<T>.Default;
        
        private T _value;
        private Action<T> _event;

        public T Value
        {
            get => _value;
            set => SetAndNotifyIfChanged(value);
        }

        public event Action<T> ChangeEvent
        {
            add => AddAndCall(value);
            remove => _event -= value;
        }

        public ObservableValue(T initialValue = default)
        {
            _value = initialValue;
            _event = null;
        }

        private void SetAndNotifyIfChanged(T value)
        {
            if (DefaultComparer.Equals(_value, value))
                return;
            SetAndNotify(value);
        }

        private void SetAndNotify(T value)
        {
            _value = value;
            _event?.Invoke(Value);
        }

        private void AddAndCall(Action<T> action)
        {
            _event += action;
            action?.Invoke(Value);
        }

        public bool Equals(T other) => DefaultComparer.Equals(_value, other);

        public override bool Equals(object obj)
        {
            if (obj is T other) return Equals(other);
            return ReferenceEquals(this, obj);
        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return DefaultComparer.GetHashCode(_value);
        }

        public override string ToString() => Value.ToString();
    }
}
