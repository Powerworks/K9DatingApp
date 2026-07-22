using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Automations.CloseStaleApplication;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - CloseStaleApplicationHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here.
/// </summary>
public class CloseStaleApplicationHandlerTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly Guid DogListingId = Guid.NewGuid();
    private static readonly Guid ShelterAccountId = Guid.NewGuid();

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_DoesNothing()
    {
        var session = Substitute.For<IDocumentSession>();
        var applicationId = Guid.NewGuid();
        session.LoadAsync<Application>(applicationId, Arg.Any<CancellationToken>()).Returns((Application?)null);

        await CloseStaleApplicationHandler.Handle(new CheckApplicationClosed(applicationId), session, CancellationToken.None);

        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenApplicantRespondedBeforeTheCloseCheckFired_DoesNothing()
    {
        // Marked stale, then the applicant responded and the shelter approved it before the 30-day close check fired.
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default);
        application.Review();
        application.RequestAdditionalDetails("please provide vet references");
        application.MarkStale();
        application.SubmitAdditionalDetails();
        application.Approve();

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        await CloseStaleApplicationHandler.Handle(new CheckApplicationClosed(application.Id), session, CancellationToken.None);

        application.Status.Should().Be(ApplicationStatus.Approved);
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenStillStale_ClosesTheApplication()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default);
        application.Review();
        application.RequestAdditionalDetails("please provide vet references");
        application.MarkStale();

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        await CloseStaleApplicationHandler.Handle(new CheckApplicationClosed(application.Id), session, CancellationToken.None);

        application.Status.Should().Be(ApplicationStatus.Closed);
        session.Received(1).Store(Arg.Is<Application[]>(arr => arr != null && arr.Length == 1 && arr[0] == application));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRedeliveredAfterAlreadyClosed_IsIdempotent()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default);
        application.Review();
        application.RequestAdditionalDetails("please provide vet references");
        application.MarkStale();
        application.Close(); // already acted on once

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        await CloseStaleApplicationHandler.Handle(new CheckApplicationClosed(application.Id), session, CancellationToken.None);

        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
