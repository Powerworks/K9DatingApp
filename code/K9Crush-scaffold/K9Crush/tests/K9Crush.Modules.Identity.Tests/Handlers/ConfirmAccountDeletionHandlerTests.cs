using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Wolverine;
using K9Crush.Modules.Identity.Api.Automations.PermanentlyDeleteAccountAfterGracePeriod;
using K9Crush.Modules.Identity.Api.Commands.ConfirmAccountDeletion;
using K9Crush.Modules.Identity.Domain;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ConfirmAccountDeletionHandler only calls
/// LoadAsync/Store/SaveChangesAsync plus (ADR-026) IMessageBus.ScheduleAsync,
/// so both IDocumentSession and IMessageBus mock cleanly here.
/// </summary>
public class ConfirmAccountDeletionHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenOwnerDoesNotExist_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        var bus = Substitute.For<IMessageBus>();
        session.LoadAsync<OwnerAccount>(ownerId, Arg.Any<CancellationToken>()).Returns((OwnerAccount?)null);

        var result = await ConfirmAccountDeletionHandler.Handle(BuildUser(ownerId), session, bus, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenDeletionWasNeverRequested_ReturnsConflictAndDoesNotSchedule()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        var session = Substitute.For<IDocumentSession>();
        var bus = Substitute.For<IMessageBus>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await ConfirmAccountDeletionHandler.Handle(BuildUser(owner.Id), session, bus, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        await bus.DidNotReceiveWithAnyArgs().PublishAsync(default(CheckAccountGracePeriodExpired)!, default);
    }

    [Fact]
    public async Task Handle_WhenAlreadyConfirmed_ReturnsConflictAndDoesNotSchedule()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        owner.ConfirmDeletion(30);
        var session = Substitute.For<IDocumentSession>();
        var bus = Substitute.For<IMessageBus>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await ConfirmAccountDeletionHandler.Handle(BuildUser(owner.Id), session, bus, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenDeletionWasRequested_ConfirmsAndSchedulesGracePeriodCheck30DaysOut()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        var session = Substitute.For<IDocumentSession>();
        var bus = Substitute.For<IMessageBus>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await ConfirmAccountDeletionHandler.Handle(BuildUser(owner.Id), session, bus, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ConfirmAccountDeletionResponse>>();
        var response = ((Ok<ConfirmAccountDeletionResponse>)result.Result).Value!;
        response.GracePeriodDays.Should().Be(30);
        response.Recoverable.Should().BeTrue();
        owner.GracePeriodEndsAt.Should().NotBeNull();

        await bus.Received(1).PublishAsync(
            Arg.Is<CheckAccountGracePeriodExpired>(m => m != null && m.OwnerId == owner.Id),
            Arg.Is<DeliveryOptions?>(o => o != null && o.ScheduleDelay == TimeSpan.FromDays(30)));
    }
}
