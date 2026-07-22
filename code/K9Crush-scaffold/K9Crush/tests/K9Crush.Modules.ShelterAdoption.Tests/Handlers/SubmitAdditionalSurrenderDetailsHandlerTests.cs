using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.SubmitAdditionalSurrenderDetails;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - SubmitAdditionalSurrenderDetailsHandler
/// only calls LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here.
/// </summary>
public class SubmitAdditionalSurrenderDetailsHandlerTests
{
    private static readonly Guid RequestedByOwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static DogSurrenderRequest BuildAdditionalDetailsRequested()
    {
        var request = DogSurrenderRequest.Request(
            RequestedByOwnerId, "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy");
        request.Review();
        request.RequestAdditionalDetails("Please confirm vaccination records");
        return request;
    }

    [Fact]
    public async Task Handle_WhenAdditionalDetailsRequestedAndCallerOwnsIt_SubmitsAndPersists()
    {
        var surrenderRequest = BuildAdditionalDetailsRequested();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequest.Id, Arg.Any<CancellationToken>()).Returns(surrenderRequest);

        var result = await SubmitAdditionalSurrenderDetailsHandler.Handle(
            surrenderRequest.Id, BuildUser(RequestedByOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<SubmitAdditionalSurrenderDetailsResponse>>();
        session.Received(1).Store(Arg.Is<DogSurrenderRequest[]>(arr =>
            arr != null && arr.Length == 1 && arr[0].Status == SurrenderRequestStatus.UnderReview));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequestDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var surrenderRequestId = Guid.NewGuid();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequestId, Arg.Any<CancellationToken>()).Returns((DogSurrenderRequest?)null);

        var result = await SubmitAdditionalSurrenderDetailsHandler.Handle(
            surrenderRequestId, BuildUser(RequestedByOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerDidNotRequestTheSurrender_ReturnsForbid()
    {
        var surrenderRequest = BuildAdditionalDetailsRequested();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequest.Id, Arg.Any<CancellationToken>()).Returns(surrenderRequest);

        var result = await SubmitAdditionalSurrenderDetailsHandler.Handle(
            surrenderRequest.Id, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenNotInAdditionalDetailsRequestedStatus_ReturnsConflict()
    {
        var surrenderRequest = DogSurrenderRequest.Request(
            RequestedByOwnerId, "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy"); // Requested
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequest.Id, Arg.Any<CancellationToken>()).Returns(surrenderRequest);

        var result = await SubmitAdditionalSurrenderDetailsHandler.Handle(
            surrenderRequest.Id, BuildUser(RequestedByOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
