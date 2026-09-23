namespace Game339.Shared.Infrastructure.DependencyInjection
{
    public interface IMiniContainer
    {
        T Resolve<T>();
    }
}
