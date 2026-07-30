using FluentAssertions;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Automations.CloseStaleApplication;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - CloseStaleApplicationHandler only calls
/// FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here (ADR-031).
/// </summary>
public class CloseStaleApplicationHandlerTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly Guid DogListingId = Guid.NewGuid();
    private static readonly Guid ShelterAccountId = Guid.NewGuid();

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_DoesNothing()
    {
        var applicationId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<Application>(applicationId, null, out _);

        await CloseStaleApplicationHandler.Handle(new CheckApplicationClosed(applicationId), session, CancellationToken.None);

        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenApplicantRespondedBeforeTheCloseCheckFired_DoesNothing()
    {
        // Marked stale, then the applicant responded and the shelter approved it before the 30-day close check fired.
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default).Application;
        application.Review();
        application.RequestAdditionalDetails("please provide vet references");
        application.MarkStale();
        application.SubmitAdditionalDetails();
        application.Approve();

        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out var stream);

        await CloseStaleApplicationHandler.Handle(new CheckApplicationClosed(application.Id), session, CancellationToken.None);

        application.Status.Should().Be(ApplicationStatus.Approved);
        stream.DidNotReceiveWithAnyArgs().AppendOne(default!);
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenStillStale_ClosesTheApplication()
    {
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default).Application;
        application.Review();
        application.RequestAdditionalDetails("please provide vet references");
        application.MarkStale();

        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out var stream);

        await CloseStaleApplicationHandler.Handle(new CheckApplicationClosed(application.Id), session, CancellationToken.None);

        application.Status.Should().Be(ApplicationStatus.Closed);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.ApplicationClosedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRedeliveredAfterAlreadyClosed_IsIdempotent()
    {
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default).Application;
        application.Review();
        application.RequestAdditionalDetails("please provide vet references");
        application.MarkStale();
        application.Close(); // already acted on once

        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out var stream);

        await CloseStaleApplicationHandler.Handle(new CheckApplicationClosed(application.Id), session, CancellationToken.None);

        stream.DidNotReceiveWithAnyArgs().AppendOne(default!);
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
