using System.Reflection;

namespace KeyHolder.UnitTests;

public sealed class LayerDependencyTests
{
    private static readonly string[] OuterLayerNames =
    [
        "KeyHolder.Desktop",
        "KeyHolder.Infrastructure",
    ];

    [Fact]
    public void DomainDoesNotReferenceOuterLayers()
    {
        AssertDoesNotReference(
            typeof(Domain.AssemblyMarker).Assembly,
            OuterLayerNames.Append("KeyHolder.Cryptography"));
    }

    [Fact]
    public void CryptographyDoesNotReferenceOuterLayers()
    {
        AssertDoesNotReference(
            typeof(Cryptography.AssemblyMarker).Assembly,
            OuterLayerNames);
    }

    [Fact]
    public void InfrastructureDoesNotReferenceDesktop()
    {
        AssertDoesNotReference(
            typeof(Infrastructure.AssemblyMarker).Assembly,
            ["KeyHolder.Desktop"]);
    }

    private static void AssertDoesNotReference(
        Assembly assembly,
        IEnumerable<string> forbiddenAssemblyNames)
    {
        var references = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name);

        Assert.Empty(references.Intersect(forbiddenAssemblyNames, StringComparer.Ordinal));
    }
}
