using Fireline.Shared.Infrastructure;

namespace Fireline.Tests.Infrastructure;

public class HealthPoolTests
{
    [Test]
    public void DamageClampsAtZeroAndDeathHappensOnce()
    {
        var health = new HealthPool(100);
        Assert.That(health.TakeDamage(25), Is.False);
        Assert.That(health.Current, Is.EqualTo(75));
        Assert.That(health.TakeDamage(200), Is.True);
        Assert.That(health.Current, Is.Zero);
        Assert.That(health.TakeDamage(1), Is.False);
    }

    [TestCase(-10f)]
    [TestCase(0f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidDamageCannotHealOrKill(float damage)
    {
        var health = new HealthPool(100);
        Assert.That(health.TakeDamage(damage), Is.False);
        Assert.That(health.Current, Is.EqualTo(100));
    }

    [Test]
    public void ResetRestoresHealthForNextPoolUse()
    {
        var health = new HealthPool(15);
        health.TakeDamage(15);
        health.Reset(30);
        Assert.That(health.Current, Is.EqualTo(30));
        Assert.That(health.Maximum, Is.EqualTo(30));
        Assert.That(health.IsDead, Is.False);
        Assert.That(health.TakeDamage(30), Is.True);
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidMaximumIsRejected(float maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HealthPool(maximum));
    }
}
