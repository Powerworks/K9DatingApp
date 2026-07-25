using FluentAssertions;
using K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetSurrenderReviewQueue;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.ShelterAdoption;

/// <summary>
/// Layer 3 (TestingApproach.md) - GetSurrenderReviewQueueHandler calls
/// session.Query&lt;DogSurrenderRequest&gt;().ToListAsync() with NO filter at
/// all (every surrender request platform-wide) - a genuinely global,
/// unscoped query, the same class of check that forced
/// GetAdoptionListingsIntegrationTests/GetFeedbackInboxIntegrationTests/
/// GetModerationQueueIntegrationTests onto their own dedicated
/// per-instance IAsyncLifetime container instead of sharing one via
/// [Collection(...)] - see those test classes' doc comments for the full
/// writeup of why. Same fix applied here up front. Seeding now goes
/// through Events.StartStream (ADR-031) rather than session.Store, since
/// DogSurrenderRequest is event-sourced.
/// </summary>
public class GetSurrenderReviewQueueIntegrationTests : IAsyncLifetime
{
    private readonly ShelterAdoptionPostgresFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Handle_WhenNoRequestsExist_ReturnsEmptyList()
    {
        await using var session = _fixture.Store.LightweightSession();

        var response = await GetSurrenderReviewQueueHandler.Handle(session, CancellationToken.None);

        response.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsEverySurrenderRequestRegardlessOfStatus()
    {
        var (requested, requestedEvent) = DogSurrenderRequest.RequestNew(
            Guid.NewGuid(), "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy");
        var (declined, declinedRequestedEvent) = DogSurrenderRequest.RequestNew(
            Guid.NewGuid(), "Max", "Beagle", 24, "Allergies in the household", "Playful", "Healthy");
        var reviewedEvent = declined.Review();
        var declinedEvent = declined.Decline("Outside current intake capacity");

        await using (var seedSession = _fixture.Store.LightweightSession())
        {
            seedSession.Events.StartStream<DogSurrenderRequest>(requested.Id, requestedEvent);
            seedSession.Events.StartStream<DogSurrenderRequest>(declined.Id, declinedRequestedEvent, reviewedEvent, declinedEvent);
            await seedSession.SaveChangesAsync();
        }

        await using var session = _fixture.Store.LightweightSession();
        var response = await GetSurrenderReviewQueueHandler.Handle(session, CancellationToken.None);

        response.Items.Should().HaveCount(2);
        response.Items.Should().Contain(x => x.SurrenderRequestId == requested.Id && x.Status == nameof(SurrenderRequestStatus.Requested));
        response.Items.Should().Contain(x => x.SurrenderRequestId == declined.Id && x.Status == nameof(SurrenderRequestStatus.Declined));
    }
}
