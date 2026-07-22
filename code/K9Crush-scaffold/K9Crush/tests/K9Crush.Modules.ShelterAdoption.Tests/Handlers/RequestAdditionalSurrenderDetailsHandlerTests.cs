using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.RequestAdditionalSurrenderDetails;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RequestAdditionalSurrenderDetailsHandler
/// only calls LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here.
/// </summary>
public class RequestAdditionalSurrenderDetailsHandlerTests
{
    private static DogSurrenderRequest BuildUnderReview()
    {
        var request = DogSurrenderRequest.Request(
            Guid.NewGuid(), "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy");
        request.Review();
        return request;
    }

    [Fact]
    public async Task Handle_WhenUnderReview_RequestsAdditionalDetailsAndPersists()
    {
        var surrenderRequest = BuildUnderReview();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequest.Id, Arg.Any<CancellationToken>()).Returns(surrenderRequest);

        var result = await RequestAdditionalSurrenderDetailsHandler.Handle(
            surrenderRequest.Id, new RequestAdditionalSurrenderDetailsRequest("Please confirm vaccination records"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RequestAdditionalSurrenderDetailsResponse>>();
        session.Received(1).Store(Arg.Is<DogSurrenderRequest[]>(arr =>
            arr != null && arr.Length == 1 &&
            arr[0].Status == SurrenderRequestStatus.AdditionalDetailsRequested &&
            arr[0].AdditionalDetailsRequestReason == "Please confirm vaccination records"));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequestDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var surrenderRequestId = Guid.NewGuid();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequestId, Arg.Any<CancellationToken>()).Returns((DogSurrenderRequest?)null);

        var result = await RequestAdditionalSurrenderDetailsHandler.Handle(
            surrenderRequestId, new RequestAdditionalSurrenderDetailsRequest("reason"), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotUnderReview_ReturnsConflict()
    {
        var surrenderRequest = DogSurrenderRequest.Request(
            Guid.NewGuid(), "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy"); // Requested, not UnderReview
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequest.Id, Arg.Any<CancellationToken>()).Returns(surrenderRequest);

        var result = await RequestAdditionalSurrenderDetailsHandler.Handle(
            surrenderRequest.Id, new RequestAdditionalSurrenderDetailsRequest("reason"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
