using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using FluentAssertions;
using K9Crush.Modules.Identity.Api.Automations.VerifyOwnerOnSupabaseConfirmation;
using K9Crush.Modules.Identity.Domain;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - VerifyOwnerOnSupabaseConfirmationHandler
/// only calls LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here. Same HttpRequest/IConfiguration mocking approach as
/// ProvisionOwnerOnSupabaseSignupHandlerTests - see that file's doc
/// comment for why NSubstitute can proxy HttpRequest despite it being an
/// abstract class, not an interface.
/// </summary>
public class VerifyOwnerOnSupabaseConfirmationHandlerTests
{
    private const string WebhookSecret = "test-secret";

    private static (HttpRequest Request, IConfiguration Configuration) BuildAuthenticatedContext(string? providedSecret = WebhookSecret)
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration["Supabase:WebhookSecret"].Returns(WebhookSecret);

        var headers = new HeaderDictionary();
        if (providedSecret is not null)
            headers["X-Webhook-Secret"] = providedSecret;

        var request = Substitute.For<HttpRequest>();
        request.Headers.Returns(headers);

        return (request, configuration);
    }

    private static SupabaseUserUpdatePayload BuildJustConfirmedPayload(Guid ownerId, DateTimeOffset confirmedAt) => new(
        Type: "UPDATE", Table: "users", Schema: "auth",
        Record: new SupabaseUserUpdateRecord(ownerId, confirmedAt),
        OldRecord: new SupabaseUserUpdateRecord(ownerId, null));

    [Fact]
    public async Task Handle_WhenWebhookSecretIsWrong_ReturnsUnauthorizedAndCascadesNothing()
    {
        var (request, configuration) = BuildAuthenticatedContext(providedSecret: "wrong-secret");
        var session = Substitute.For<IDocumentSession>();

        var (result, integrationEvent) = await VerifyOwnerOnSupabaseConfirmationHandler.Handle(
            BuildJustConfirmedPayload(Guid.NewGuid(), DateTimeOffset.UtcNow), request, configuration, session, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenEmailWasAlreadyConfirmedBefore_AcksAndCascadesNothing()
    {
        var (request, configuration) = BuildAuthenticatedContext();
        var ownerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        var payload = new SupabaseUserUpdatePayload(
            Type: "UPDATE", Table: "users", Schema: "auth",
            Record: new SupabaseUserUpdateRecord(ownerId, DateTimeOffset.UtcNow),
            OldRecord: new SupabaseUserUpdateRecord(ownerId, DateTimeOffset.UtcNow.AddDays(-1))); // already confirmed before this update

        var (result, integrationEvent) = await VerifyOwnerOnSupabaseConfirmationHandler.Handle(
            payload, request, configuration, session, CancellationToken.None);

        result.Should().BeOfType<Ok>();
        integrationEvent.Should().BeNull();
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenOwnerAccountDoesNotExistYet_AcksAndCascadesNothing()
    {
        var (request, configuration) = BuildAuthenticatedContext();
        var ownerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(ownerId, Arg.Any<CancellationToken>()).Returns((OwnerAccount?)null);

        var (result, integrationEvent) = await VerifyOwnerOnSupabaseConfirmationHandler.Handle(
            BuildJustConfirmedPayload(ownerId, DateTimeOffset.UtcNow), request, configuration, session, CancellationToken.None);

        result.Should().BeOfType<Ok>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenEmailJustConfirmed_MarksVerifiedAndCascadesOwnerVerified()
    {
        var (request, configuration) = BuildAuthenticatedContext();
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var (result, integrationEvent) = await VerifyOwnerOnSupabaseConfirmationHandler.Handle(
            BuildJustConfirmedPayload(owner.Id, DateTimeOffset.UtcNow), request, configuration, session, CancellationToken.None);

        result.Should().BeOfType<Ok>();
        integrationEvent.Should().NotBeNull();
        integrationEvent!.OwnerId.Should().Be(owner.Id);
        owner.IsVerified.Should().BeTrue();
        session.Received(1).Store(Arg.Is<OwnerAccount[]>(arr => arr.Length == 1 && arr[0] == owner));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
