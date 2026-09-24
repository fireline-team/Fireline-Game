namespace Fireline.Shared.Infrastructure.DependencyInjection
{
    public interface IMiniContainer
    {
        T Resolve<T>();
    }
}
