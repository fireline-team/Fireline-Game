using Game339.Shared.StringReverse.Implementation;
using Game339.Tests.Infrastructure;
using NUnit.Framework;

namespace Game339.Tests.StringReverse;

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
