using Fireline.Shared.StringReverse.Implementation;
using Fireline.Tests.Infrastructure;
using NUnit.Framework;

namespace Fireline.Tests.StringReverse;

public class StringServiceTests
{
    private StringService _svc;

    [SetUp]
    public void SetUp()
    {
        _svc = new StringService(EmptyGameLog.Instance);
    }

    [TestCase("hello", "olleh")]
    [TestCase("", "")]
    [TestCase("a", "a")]
    [TestCase("racecar", "racecar")]
    public void Reverse_ReturnsExpectedString(string input, string expected)
    {
        var result = _svc.Reverse(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Reverse_NullString_ThrowsArgumentNullException()
    {
        Assert.Throws<System.ArgumentNullException>(() => _svc.Reverse(null));
    }
}
