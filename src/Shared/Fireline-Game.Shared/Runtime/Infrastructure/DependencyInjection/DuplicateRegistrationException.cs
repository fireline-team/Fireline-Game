using System;

namespace Game339.Shared.Infrastructure.DependencyInjection
{
    public sealed class DuplicateRegistrationException : InvalidOperationException
    {
        public Type RegisteredType { get; }

        public DuplicateRegistrationException(Type type)
            : base($"Type {type.FullName} is already registered.")
        {
            RegisteredType = type;
        }
    }
}
