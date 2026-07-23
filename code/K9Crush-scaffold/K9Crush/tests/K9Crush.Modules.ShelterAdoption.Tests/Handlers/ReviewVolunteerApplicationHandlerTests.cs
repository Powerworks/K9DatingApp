using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.ReviewVolunteerApplication;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ReviewVolunteerApplicationHandler only
/// calls LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here.
/// </summary>
public class ReviewVolunteerApplicationHandlerTests
{
    private static VolunteerApplication BuildSubmitted() =>
        VolunteerApplication.Apply(Guid.NewGuid(), VolunteerAreaOfInterest.HomeChecks);

    [Fact]
    public async Task Handle_WhenSubmitted_ReviewsAndPersists()
    {
        var application = BuildSubmitted();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<VolunteerApplication>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var result = await ReviewVolunteerApplicationHandler.Handle(application.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ReviewVolunteerApplicationResponse>>();
        session.Received(1).Store(Arg.Is<VolunteerApplication[]>(arr =>
            arr != null && arr.Length == 1 && arr[0].Status == VolunteerApplicationStatus.UnderReview));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var applicationId = Guid.NewGuid();
        session.LoadAsync<VolunteerApplication>(applicationId, Arg.Any<CancellationToken>()).Returns((VolunteerApplication?)null);

        var result = await ReviewVolunteerApplicationHandler.Handle(applicationId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotSubmitted_ReturnsConflict()
    {
        var application = BuildSubmitted();
        application.Review();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<VolunteerApplication>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var result = await ReviewVolunteerApplicationHandler.Handle(application.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
