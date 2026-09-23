using System;
using System.Collections.Concurrent;

namespace Game339.Shared.Infrastructure.DependencyInjection.Implementation
{
    public sealed class MiniContainer : IMiniContainer
    {
        private readonly ConcurrentDictionary<Type, Func<IMiniContainer, object>> _registrations = new();

        public MiniContainer()
        {
            RegisterSingletonInstance<IMiniContainer>(this);
        }

        public T Resolve<T>()
        {
            var type = typeof(T);
            if (_registrations.TryGetValue(type, out var reg))
                return (T)reg(this);
            throw new InvalidOperationException($"No registration found for {type.FullName}.");
        }

        public MiniContainer RegisterSingletonFactory<TInterface>(Func<IMiniContainer, TInterface> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            var key = typeof(TInterface);
            var lazy = new Lazy<TInterface>(() => factory(this));
            if (_registrations.TryAdd(key, _ => lazy.Value))
                return this;
            throw new DuplicateRegistrationException(key);
        }

        public MiniContainer RegisterSingletonInstance<TInterface>(TInterface instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            var key = typeof(TInterface);
            if (_registrations.TryAdd(key, _ => instance))
                return this;
            throw new DuplicateRegistrationException(key);
        }

        public MiniContainer RegisterTransientFactory<TInterface>(Func<IMiniContainer, TInterface> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            var key = typeof(TInterface);
            if (_registrations.TryAdd(key, container => factory(container)))
                return this;
            throw new DuplicateRegistrationException(key);
        }
    }
}
