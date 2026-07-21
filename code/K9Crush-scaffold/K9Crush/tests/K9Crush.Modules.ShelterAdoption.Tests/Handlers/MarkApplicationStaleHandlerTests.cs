using FluentAssertions;
using Marten;
using NSubstitute;
using Wolverine;
using K9Crush.Modules.ShelterAdoption.Api.Automations.CloseStaleApplication;
using K9Crush.Modules.ShelterAdoption.Api.Automations.MarkApplicationStale;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - MarkApplicationStaleHandler only calls
/// LoadAsync/Store/SaveChangesAsync plus (ADR-026) IMessageBus.
/// ScheduleAsync, so both IDocumentSession and IMessageBus mock cleanly
/// here.
/// </summary>
public class MarkApplicationStaleHandlerTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly Guid DogListingId = Guid.NewGuid();
    private static readonly Guid ShelterAccountId = Guid.NewGuid();

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_DoesNothing()
    {
        var session = Substitute.For<IDocumentSession>();
        var bus = Substitute.For<IMessageBus>();
        var applicationId = Guid.NewGuid();
        session.LoadAsync<Application>(applicationId, Arg.Any<CancellationToken>()).Returns((Application?)null);

        await MarkApplicationStaleHandler.Handle(new CheckApplicationStale(applicationId), session, bus, CancellationToken.None);

        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
        await bus.DidNotReceiveWithAnyArgs().PublishAsync(default(CheckApplicationClosed)!, default);
    }

    [Fact]
    public async Task Handle_WhenApplicantAlreadyRespondedInTheMeantime_DoesNothing()
    {
        // Status moved back to UnderReview via SubmitAdditionalDetailsHandler before this scheduled check fired.
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId);
        application.Review();
        application.RequestAdditionalDetails("please provide vet references");
        application.SubmitAdditionalDetails(); // back to UnderReview

        var session = Substitute.For<IDocumentSession>();
        var bus = Substitute.For<IMessageBus>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        await MarkApplicationStaleHandler.Handle(new CheckApplicationStale(application.Id), session, bus, CancellationToken.None);

        application.Status.Should().Be(ApplicationStatus.UnderReview);
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
        await bus.DidNotReceiveWithAnyArgs().PublishAsync(default(CheckApplicationClosed)!, default);
    }

    [Fact]
    public async Task Handle_WhenStillAwaitingDetails_MarksStaleAndSchedulesTheCloseCheck30DaysOut()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId);
        application.Review();
        application.RequestAdditionalDetails("please provide vet references"); // ReturnedForAlteration, never responded to

        var session = Substitute.For<IDocumentSession>();
        var bus = Substitute.For<IMessageBus>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        await MarkApplicationStaleHandler.Handle(new CheckApplicationStale(application.Id), session, bus, CancellationToken.None);

        application.Status.Should().Be(ApplicationStatus.Stale);
        session.Received(1).Store(Arg.Is<Application[]>(arr => arr.Length == 1 && arr[0] == application));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        await bus.Received(1).PublishAsync(
            Arg.Is<CheckApplicationClosed>(m => m.ApplicationId == application.Id),
            Arg.Is<DeliveryOptions?>(o => o != null && o.ScheduleDelay == TimeSpan.FromDays(30)));
    }

    [Fact]
    public async Task Handle_WhenRedeliveredAfterAlreadyMarkedStale_IsIdempotentAndDoesNotReschedule()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId);
        application.Review();
        application.RequestAdditionalDetails("please provide vet references");
        application.MarkStale(); // already acted on once

        var session = Substitute.For<IDocumentSession>();
        var bus = Substitute.For<IMessageBus>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        await MarkApplicationStaleHandler.Handle(new CheckApplicationStale(application.Id), session, bus, CancellationToken.None);

        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
        await bus.DidNotReceiveWithAnyArgs().PublishAsync(default(CheckApplicationClosed)!, default);
    }
}
