using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using FluentAssertions;
using K9Crush.Modules.Identity.Api.Automations.ProvisionOwnerOnSupabaseSignup;
using K9Crush.Modules.Identity.Domain;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ProvisionOwnerOnSupabaseSignupHandler
/// only calls LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here. Also the first test in this codebase to mock
/// HttpRequest/IConfiguration - HttpRequest is an abstract class (not an
/// interface) but NSubstitute can still proxy it since every member used
/// here (Headers) is virtual/abstract; IConfiguration's string indexer is
/// a plain interface member.
/// </summary>
public class ProvisionOwnerOnSupabaseSignupHandlerTests
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

    private static SupabaseUserWebhookPayload BuildInsertPayload(Guid ownerId, string email, DateTimeOffset createdAt) =>
        new(Type: "INSERT", Table: "users", Schema: "auth", Record: new SupabaseUserRecord(ownerId, email, createdAt));

    [Fact]
    public async Task Handle_WhenWebhookSecretIsWrong_ReturnsUnauthorizedAndCascadesNothing()
    {
        var (request, configuration) = BuildAuthenticatedContext(providedSecret: "wrong-secret");
        var session = Substitute.For<IDocumentSession>();

        var (result, integrationEvent) = await ProvisionOwnerOnSupabaseSignupHandler.Handle(
            BuildInsertPayload(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow),
            request, configuration, session, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
        integrationEvent.Should().BeNull();
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenPayloadIsNotAUsersInsert_AcksAndCascadesNothing()
    {
        var (request, configuration) = BuildAuthenticatedContext();
        var session = Substitute.For<IDocumentSession>();
        var payload = new SupabaseUserWebhookPayload(Type: "UPDATE", Table: "users", Schema: "auth", Record: null);

        var (result, integrationEvent) = await ProvisionOwnerOnSupabaseSignupHandler.Handle(
            payload, request, configuration, session, CancellationToken.None);

        result.Should().BeOfType<Ok>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenOwnerAlreadyProvisioned_AcksAndCascadesNothing()
    {
        var (request, configuration) = BuildAuthenticatedContext();
        var ownerId = Guid.NewGuid();
        var existing = OwnerAccount.Create(ownerId, "owner@example.com", DateTimeOffset.UtcNow);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(ownerId, Arg.Any<CancellationToken>()).Returns(existing);

        var (result, integrationEvent) = await ProvisionOwnerOnSupabaseSignupHandler.Handle(
            BuildInsertPayload(ownerId, "owner@example.com", DateTimeOffset.UtcNow),
            request, configuration, session, CancellationToken.None);

        result.Should().BeOfType<Ok>();
        integrationEvent.Should().BeNull();
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenNewUser_ProvisionsOwnerAccountAndCascadesOwnerRegistered()
    {
        var (request, configuration) = BuildAuthenticatedContext();
        var ownerId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(ownerId, Arg.Any<CancellationToken>()).Returns((OwnerAccount?)null);

        var (result, integrationEvent) = await ProvisionOwnerOnSupabaseSignupHandler.Handle(
            BuildInsertPayload(ownerId, "owner@example.com", createdAt),
            request, configuration, session, CancellationToken.None);

        result.Should().BeOfType<Ok>();
        integrationEvent.Should().NotBeNull();
        integrationEvent!.OwnerId.Should().Be(ownerId);
        integrationEvent.Email.Should().Be("owner@example.com");

        session.Received(1).Store(Arg.Is<OwnerAccount[]>(arr => arr.Length == 1 && arr[0].Id == ownerId));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
