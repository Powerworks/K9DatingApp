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
/// Layer 2 (TestingApproach.md) - RequestAdditionalDetailsHandler calls
/// FetchForWriting/AppendOne/SaveChangesAsync against Application plus a
/// plain LoadAsync against ShelterAccount for the ownership check
/// (read-only), plus (ADR-026) IMessageBus.ScheduleAsync (ADR-031).
/// </summary>
public class RequestAdditionalDetailsHandlerTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly Guid DogListingId = Guid.NewGuid();
    private static readonly Guid ShelterOwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static IDocumentSession BuildSession(ShelterAccount shelterAccount, Application? application, out JasperFx.Events.IEventStream<Application> stream)
    {
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application?.Id ?? Guid.NewGuid(), application, out stream);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);
        return session;
    }

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_ReturnsNotFound()
    {
        var applicationId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<Application>(applicationId, null, out _);
        var bus = Substitute.For<IMessageBus>();

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
        var shelterAccount = ShelterAccount.RequestNew(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, shelterAccount.Id, TestIntake.Default).Application;
        var session = BuildSession(shelterAccount, application, out _);
        var bus = Substitute.For<IMessageBus>();

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
        var shelterAccount = ShelterAccount.RequestNew(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, shelterAccount.Id, TestIntake.Default).Application;
        application.Review(); // UnderReview - the only status RequestAdditionalDetails is valid from
        var session = BuildSession(shelterAccount, application, out var stream);
        var bus = Substitute.For<IMessageBus>();

        var result = await RequestAdditionalDetailsHandler.Handle(
            application.Id,
            new RequestAdditionalDetailsRequest("please provide vet references"),
            BuildUser(ShelterOwnerId),
            session,
            bus,
            CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RequestAdditionalDetailsResponse>>();
        application.Status.Should().Be(ApplicationStatus.ReturnedForAlteration);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.ApplicationAdditionalDetailsRequestedV1)));

        await bus.Received(1).PublishAsync(
            Arg.Is<CheckApplicationStale>(m => m != null && m.ApplicationId == application.Id),
            Arg.Is<DeliveryOptions?>(o => o != null && o.ScheduleDelay == TimeSpan.FromDays(15)));
    }

    [Fact]
    public async Task Handle_WhenApplicationIsNotUnderReview_ReturnsConflictAndDoesNotSchedule()
    {
        var shelterAccount = ShelterAccount.RequestNew(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, shelterAccount.Id, TestIntake.Default).Application; // Status = Pending, not UnderReview
        var session = BuildSession(shelterAccount, application, out _);
        var bus = Substitute.For<IMessageBus>();

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
