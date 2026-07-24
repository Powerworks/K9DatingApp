using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using K9Crush.Modules.Admin.Domain;
using K9Crush.Modules.Identity.Domain;
using K9Crush.Modules.Media.Domain;
using K9Crush.Modules.Notifications.Domain;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.ArchitectureTests;

/// <summary>
/// Mechanizes the module boundary rule from docs/05-event-modeling-blueprint.md:
/// a module's Domain project may reference K9Crush.BuildingBlocks.Domain
/// only, never another module's Domain/Api/Contracts. Nothing catches a
/// violation of this today except code review - this makes it a build-time
/// failure instead.
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly (string ModuleName, Assembly DomainAssembly)[] Modules =
    [
        ("Identity", typeof(OwnerAccount).Assembly),
        ("ShelterAdoption", typeof(Application).Assembly),
        ("Notifications", typeof(NotificationPreference).Assembly),
        ("Admin", typeof(FeedbackInboxItem).Assembly),
        ("Media", typeof(MediaAsset).Assembly)
    ];

    public static IEnumerable<object[]> ModuleCases() =>
        Modules.Select(m => new object[] { m.ModuleName, m.DomainAssembly });

    [Theory]
    [MemberData(nameof(ModuleCases))]
    public void DomainAssembly_MustNotDependOnAnyOtherModule(string moduleName, Assembly domainAssembly)
    {
        var otherModuleNamespaces = Modules
            .Where(m => m.ModuleName != moduleName)
            .Select(m => $"K9Crush.Modules.{m.ModuleName}")
            .ToArray();

        var result = Types.InAssembly(domainAssembly)
            .Should()
            .NotHaveDependencyOnAny(otherModuleNamespaces)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"{moduleName}.Domain must not depend on any other module, but found: " +
            string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []));
    }
}
