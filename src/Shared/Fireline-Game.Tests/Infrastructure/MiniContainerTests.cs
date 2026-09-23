using System;
using Game339.Shared.Infrastructure.DependencyInjection;
using Game339.Shared.Infrastructure.DependencyInjection.Implementation;
using NUnit.Framework;

namespace Game339.Tests.Infrastructure;

public class MiniContainerTests
{
    private interface IService {}
    private sealed class ServiceA : IService { public Guid Id { get; } = Guid.NewGuid(); }

    [Test]
    public void Resolve_IMiniContainer_Returns_Self()
    {
        var c = new MiniContainer();
        var resolved = c.Resolve<IMiniContainer>();
        Assert.That(resolved, Is.SameAs(c));
    }

    [Test]
    public void Resolve_Unregistered_Throws()
    {
        var c = new MiniContainer();
        Assert.That(() => c.Resolve<IService>(), Throws.InvalidOperationException);
    }

    [Test]
    public void RegisterSingletonInstance_Resolves_Same_Instance()
    {
        var c = new MiniContainer();
        var inst = new ServiceA();
        c.RegisterSingletonInstance<IService>(inst);

        var r1 = c.Resolve<IService>();
        var r2 = c.Resolve<IService>();

        Assert.That(r1, Is.SameAs(inst));
        Assert.That(r2, Is.SameAs(inst));
    }

    [Test]
    public void RegisterSingletonFactory_Is_Lazy_And_Returns_Same_Instance()
    {
        var c = new MiniContainer();
        int factoryCalls = 0;
        c.RegisterSingletonFactory<IService>(_ => { factoryCalls++; return new ServiceA(); });

        Assert.That(factoryCalls, Is.EqualTo(0), "Factory should not be called until first resolve (lazy)");

        var r1 = c.Resolve<IService>();
        var r2 = c.Resolve<IService>();

        Assert.That(factoryCalls, Is.EqualTo(1));
        Assert.That(r2, Is.SameAs(r1));
    }

    [Test]
    public void RegisterTransientFactory_Returns_New_Instance_Each_Time()
    {
        var c = new MiniContainer();
        int factoryCalls = 0;
        c.RegisterTransientFactory<IService>(_ => { factoryCalls++; return new ServiceA(); });

        var r1 = c.Resolve<IService>();
        var r2 = c.Resolve<IService>();

        Assert.That(factoryCalls, Is.EqualTo(2));
        Assert.That(r1, Is.Not.SameAs(r2));
    }

    [Test]
    public void Duplicate_Registration_Throws_DuplicateRegistrationException()
    {
        var c = new MiniContainer();
        c.RegisterTransientFactory<IService>(_ => new ServiceA());
        Assert.That(() => c.RegisterSingletonInstance<IService>(new ServiceA()), Throws.TypeOf<DuplicateRegistrationException>());
    }

    [Test]
    public void Register_Methods_Throw_On_Null_Arguments()
    {
        var c = new MiniContainer();
        Assert.That(() => c.RegisterSingletonInstance<IService>(null!), Throws.ArgumentNullException);
        Assert.That(() => c.RegisterSingletonFactory<IService>(null!), Throws.ArgumentNullException);
        Assert.That(() => c.RegisterTransientFactory<IService>(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void Register_Methods_Return_Same_Container_For_Chaining()
    {
        var c = new MiniContainer();
        var returned = c.RegisterTransientFactory<IService>(_ => new ServiceA());
        Assert.That(returned, Is.SameAs(c));
    }
}
