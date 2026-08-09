using Xunit;

namespace Pos.Application.Tests;

public class SmokeTests
{
    [Fact]
    public void BasicTruth()
    {
        Assert.True(1 + 1 == 2);
    }

    [Fact]
    public void AnotherSimpleTest()
    {
        Assert.Equal("hello", "hel" + "lo");
    }
}
