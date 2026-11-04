namespace KeyHolder.IntegrationTests;

public sealed class SolutionSmokeTests
{
    [Fact]
    public void DomainAssemblyIsLoadable()
    {
        var assembly = typeof(Domain.AssemblyMarker).Assembly;

        Assert.Equal("KeyHolder.Domain", assembly.GetName().Name);
    }

    [Fact]
    public void CryptographyAssemblyIsLoadable()
    {
        var assembly = typeof(Cryptography.AssemblyMarker).Assembly;

        Assert.Equal("KeyHolder.Cryptography", assembly.GetName().Name);
    }

    [Fact]
    public void InfrastructureAssemblyIsLoadable()
    {
        var assembly = typeof(Infrastructure.AssemblyMarker).Assembly;

        Assert.Equal("KeyHolder.Infrastructure", assembly.GetName().Name);
    }
}
