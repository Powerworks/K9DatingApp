using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Wolverine;
using K9Crush.Modules.ShelterAdoption.Api.Automations.MarkApplicationStale;
using K9Crush.Modules.ShelterAdoption.Api.Commands.RequestAdditionalDetails;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RequestAdditionalDetailsHandler only
/// calls LoadAsync/Store/SaveChangesAsync plus (ADR-026) IMessageBus.
/// ScheduleAsync, so both IDocumentSession and IMessageBus mock cleanly
/// here.
/// </summary>
public class RequestAdditionalDetailsHandlerTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly Guid DogListingId = Guid.NewGuid();
    private static readonly Guid ShelterOwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var bus = Substitute.For<IMessageBus>();
        var applicationId = Guid.NewGuid();
        session.LoadAsync<Application>(applicationId, Arg.Any<CancellationToken>()).Returns((Application?)null);

        var result = await RequestAdditionalDetailsHandler.Handle(
            applicationId,
            new RequestAdditionalDetailsRequest("please provide vet references"),
            BuildUser(ShelterOwnerId),
            session,
            bus,
            CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        await bus.DidNotReceiveWithAnyArgs().PublishAsync(default(CheckApplicationStale)!, default);
    }

    [Fact]
    public async Task Handle_WhenCallerDoesNotOwnTheShelter_ReturnsForbid()
    {
        var shelterAccount = ShelterAccount.Create(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        var application = Application.Submit(ApplicantOwnerId, DogListingId, shelterAccount.Id);

        var session = Substitute.For<IDocumentSession>();
        var bus = Substitute.For<IMessageBus>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await RequestAdditionalDetailsHandler.Handle(
            application.Id,
            new RequestAdditionalDetailsRequest("please provide vet references"),
            BuildUser(Guid.NewGuid()), // not ShelterOwnerId
            session,
            bus,
            CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenApplicationSchedulesTheStaleCheck15DaysOut()
    {
        var shelterAccount = ShelterAccount.Create(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        var application = Application.Submit(ApplicantOwnerId, DogListingId, shelterAccount.Id);
        application.Review(); // UnderReview - the only status RequestAdditionalDetails is valid from

        var session = Substitute.For<IDocumentSession>();
        var bus = Substitute.For<IMessageBus>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await RequestAdditionalDetailsHandler.Handle(
            application.Id,
            new RequestAdditionalDetailsRequest("please provide vet references"),
            BuildUser(ShelterOwnerId),
            session,
            bus,
            CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RequestAdditionalDetailsResponse>>();
        application.Status.Should().Be(ApplicationStatus.ReturnedForAlteration);

        await bus.Received(1).PublishAsync(
            Arg.Is<CheckApplicationStale>(m => m != null && m.ApplicationId == application.Id),
            Arg.Is<DeliveryOptions?>(o => o != null && o.ScheduleDelay == TimeSpan.FromDays(15)));
    }

    [Fact]
    public async Task Handle_WhenApplicationIsNotUnderReview_ReturnsConflictAndDoesNotSchedule()
    {
        var shelterAccount = ShelterAccount.Create(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        var application = Application.Submit(ApplicantOwnerId, DogListingId, shelterAccount.Id); // Status = Pending, not UnderReview

        var session = Substitute.For<IDocumentSession>();
        var bus = Substitute.For<IMessageBus>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await RequestAdditionalDetailsHandler.Handle(
            application.Id,
            new RequestAdditionalDetailsRequest("please provide vet references"),
            BuildUser(ShelterOwnerId),
            session,
            bus,
            CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        await bus.DidNotReceiveWithAnyArgs().PublishAsync(default(CheckApplicationStale)!, default);
    }
}
