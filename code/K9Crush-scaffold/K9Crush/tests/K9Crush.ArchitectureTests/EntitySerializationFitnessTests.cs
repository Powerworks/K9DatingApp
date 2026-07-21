using System.Reflection;
using System.Text.Json.Serialization;
using FluentAssertions;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.Modules.Admin.Domain;
using K9Crush.Modules.Chat.Domain;
using K9Crush.Modules.Discovery.Domain;
using K9Crush.Modules.Identity.Domain;
using K9Crush.Modules.Media.Domain;
using K9Crush.Modules.Moderation.Domain;
using K9Crush.Modules.Notifications.Domain;
using K9Crush.Modules.Profiles.Domain;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.ArchitectureTests;

/// <summary>
/// Mechanizes the fix from docs/05-event-modeling-blueprint.md Section 6.1:
/// DogProfile originally had a private constructor and private setters, which
/// is exactly what a DDD-minded entity should have - except System.Text.Json's
/// reflection-based converter only populates public constructors/settable
/// members by default, so LoadAsync&lt;DogProfile&gt; threw NotSupportedException
/// on the first real GET request. The fix was [JsonConstructor] +
/// [JsonInclude]; this test makes sure every current and future Entity-derived
/// type actually has both, instead of that bug reappearing silently on the
/// next new entity and waiting for a live request to reveal it.
/// </summary>
public class EntitySerializationFitnessTests
{
    private static readonly Assembly[] DomainAssemblies =
    [
        typeof(OwnerAccount).Assembly,
        typeof(DogProfile).Assembly,
        typeof(DiscoveryFeedItem).Assembly,
        typeof(K9Crush.Modules.ShelterAdoption.Domain.Application).Assembly,
        typeof(NotificationPreference).Assembly,
        typeof(ConversationSummary).Assembly,
        typeof(FeedbackInboxItem).Assembly,
        typeof(MediaAsset).Assembly,
        typeof(FlaggedContent).Assembly
    ];

    private static IEnumerable<Type> EntityTypes() =>
        DomainAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(Entity).IsAssignableFrom(t));

    public static IEnumerable<object[]> EntityTypeCases() =>
        EntityTypes().Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(EntityTypeCases))]
    public void NonPublicParameterlessConstructor_MustHaveJsonConstructorAttribute(Type entityType)
    {
        var parameterlessCtor = entityType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, types: Type.EmptyTypes, modifiers: null);

        if (parameterlessCtor is null || parameterlessCtor.IsPublic)
            return; // nothing for the serializer to trip over

        parameterlessCtor.GetCustomAttribute<JsonConstructorAttribute>().Should().NotBeNull(
            $"{entityType.FullName}'s non-public parameterless constructor needs [JsonConstructor] " +
            "or Marten's LoadAsync will throw NotSupportedException on the first real read - see " +
            "docs/05-event-modeling-blueprint.md Section 6.1");
    }

    [Theory]
    [MemberData(nameof(EntityTypeCases))]
    public void NonPublicSettableProperties_MustHaveJsonIncludeAttribute(Type entityType)
    {
        var offendingProperties = entityType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(p => p.GetSetMethod(nonPublic: true) is { IsPublic: false })
            .Where(p => p.GetCustomAttribute<JsonIncludeAttribute>() is null)
            .ToList();

        offendingProperties.Should().BeEmpty(
            $"every non-publicly-settable property on {entityType.FullName} needs [JsonInclude] " +
            "or Marten's LoadAsync will silently leave it at its default value - see " +
            "docs/05-event-modeling-blueprint.md Section 6.1");
    }
}
