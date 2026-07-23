using System.Reflection;
using FluentAssertions;
using K9Crush.Modules.Admin.Api.Commands.RespondToFeedback;
using K9Crush.Modules.Identity.Api.Automations.ProvisionOwnerOnSupabaseSignup;
using K9Crush.Modules.Media.Api.Commands.UploadMedia;
using K9Crush.Modules.Notifications.Api.Automations.NotifyOnApplicationApproved;
using K9Crush.Modules.Profiles.Api.ReadModels.GetDogProfile;
using K9Crush.Modules.ShelterAdoption.Api.Commands.SubmitApplication;
using Xunit;

namespace K9Crush.ArchitectureTests;

/// <summary>
/// Mechanizes the fix from docs/05-event-modeling-blueprint.md Section 5.2:
/// Wolverine's convention-based handler discovery only recognizes a Handle
/// method if its containing class name ends in "Handler" - a class named
/// after the trigger event instead (the original DogProfileCreatedProjector)
/// silently never got registered. Wolverine logged "No known handler..." and
/// the projector's writes just never happened; nothing failed loudly. This
/// test makes that a build-time failure instead of a silent runtime no-op.
/// </summary>
public class HandlerNamingFitnessTests
{
    private static readonly Assembly[] ApiAssemblies =
    [
        typeof(ProvisionOwnerOnSupabaseSignupHandler).Assembly,
        typeof(GetDogProfileHandler).Assembly,
        typeof(SubmitApplicationHandler).Assembly,
        typeof(NotifyOnApplicationApprovedHandler).Assembly,
        typeof(RespondToFeedbackHandler).Assembly,
        typeof(UploadMediaHandler).Assembly
    ];

    private static IEnumerable<Type> TypesWithPublicStaticHandleMethod() =>
        ApiAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Any(m => m.Name == "Handle"));

    public static IEnumerable<object[]> HandleMethodTypeCases() =>
        TypesWithPublicStaticHandleMethod().Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(HandleMethodTypeCases))]
    public void ClassWithPublicStaticHandleMethod_MustBeNamedEndingInHandler(Type type)
    {
        type.Name.Should().EndWith("Handler",
            $"{type.FullName} declares a public static Handle method, but Wolverine's convention-based " +
            "discovery only recognizes it if the containing class name ends in \"Handler\" - otherwise " +
            "it's silently never registered (see docs/05-event-modeling-blueprint.md Section 5.2). " +
            "Keep the file named after the trigger event if you like, but the class itself must end in Handler.");
    }
}
