using Xunit;
using Monolith.ArchitectureRules;

namespace Monolith.Sample;

public class ArchitectureTests
{
    [Fact]
    public void VerifyMonolithArchitectureRules()
    {
        var assembly = typeof(Program).Assembly;
        var result = ArchitectureEnforcer.Verify(assembly);

        Assert.True(result.IsSuccessful, result.ErrorMessage);
    }
}
