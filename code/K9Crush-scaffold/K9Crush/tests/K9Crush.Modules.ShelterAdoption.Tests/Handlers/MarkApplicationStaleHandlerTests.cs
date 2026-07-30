using FluentAssertions;
using NSubstitute;
using Wolverine;
using K9Crush.Modules.ShelterAdoption.Api.Automations.CloseStaleApplication;
using K9Crush.Modules.ShelterAdoption.Api.Automations.MarkApplicationStale;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - MarkApplicationStaleHandler only calls
/// FetchForWriting/AppendOne/SaveChangesAsync plus (ADR-026) IMessageBus.
/// ScheduleAsync, so both IDocumentSession and IMessageBus mock cleanly
/// here (ADR-031).
/// </summary>
public class MarkApplicationStaleHandlerTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly Guid DogListingId = Guid.NewGuid();
    private static readonly Guid ShelterAccountId = Guid.NewGuid();

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_DoesNothing()
    {
        var applicationId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<Application>(applicationId, null, out _);
        var bus = Substitute.For<IMessageBus>();

        await MarkApplicationStaleHandler.Handle(new CheckApplicationStale(applicationId), session, bus, CancellationToken.None);

        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
        await bus.DidNotReceiveWithAnyArgs().PublishAsync(default(CheckApplicationClosed)!, default);
    }

    [Fact]
    public async Task Handle_WhenApplicantAlreadyRespondedInTheMeantime_DoesNothing()
    {
        // Status moved back to UnderReview via SubmitAdditionalDetailsHandler before this scheduled check fired.
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default).Application;
        application.Review();
        application.RequestAdditionalDetails("please provide vet references");
        application.SubmitAdditionalDetails(); // back to UnderReview

        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out var stream);
        var bus = Substitute.For<IMessageBus>();

        await MarkApplicationStaleHandler.Handle(new CheckApplicationStale(application.Id), session, bus, CancellationToken.None);

        application.Status.Should().Be(ApplicationStatus.UnderReview);
        stream.DidNotReceiveWithAnyArgs().AppendOne(default!);
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
        await bus.DidNotReceiveWithAnyArgs().PublishAsync(default(CheckApplicationClosed)!, default);
    }

    [Fact]
    public async Task Handle_WhenStillAwaitingDetails_MarksStaleAndSchedulesTheCloseCheck30DaysOut()
    {
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default).Application;
        application.Review();
        application.RequestAdditionalDetails("please provide vet references"); // ReturnedForAlteration, never responded to

        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out var stream);
        var bus = Substitute.For<IMessageBus>();

        await MarkApplicationStaleHandler.Handle(new CheckApplicationStale(application.Id), session, bus, CancellationToken.None);

        application.Status.Should().Be(ApplicationStatus.Stale);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.ApplicationMarkedStaleV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        await bus.Received(1).PublishAsync(
            Arg.Is<CheckApplicationClosed>(m => m != null && m.ApplicationId == application.Id),
            Arg.Is<DeliveryOptions?>(o => o != null && o.ScheduleDelay == TimeSpan.FromDays(30)));
    }

    [Fact]
    public async Task Handle_WhenRedeliveredAfterAlreadyMarkedStale_IsIdempotentAndDoesNotReschedule()
    {
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default).Application;
        application.Review();
        application.RequestAdditionalDetails("please provide vet references");
        application.MarkStale(); // already acted on once

        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out var stream);
        var bus = Substitute.For<IMessageBus>();

        await MarkApplicationStaleHandler.Handle(new CheckApplicationStale(application.Id), session, bus, CancellationToken.None);

        stream.DidNotReceiveWithAnyArgs().AppendOne(default!);
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
        await bus.DidNotReceiveWithAnyArgs().PublishAsync(default(CheckApplicationClosed)!, default);
    }
}
