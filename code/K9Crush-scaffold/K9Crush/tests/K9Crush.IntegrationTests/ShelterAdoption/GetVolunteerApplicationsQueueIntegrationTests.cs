using FluentAssertions;
using K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetVolunteerApplicationsQueue;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.ShelterAdoption;

/// <summary>
/// Layer 3 (TestingApproach.md) - GetVolunteerApplicationsQueueHandler
/// calls session.Query&lt;VolunteerApplication&gt;().ToListAsync() with NO
/// filter at all - a genuinely global, unscoped query, same class of check
/// that forced GetFosterApplicationsQueueIntegrationTests onto its own
/// dedicated per-instance IAsyncLifetime container instead of sharing one
/// via [Collection(...)]. Same fix applied here up front. Seeding now
/// goes through Events.StartStream (ADR-031) rather than session.Store,
/// since VolunteerApplication is event-sourced.
/// </summary>
public class GetVolunteerApplicationsQueueIntegrationTests : IAsyncLifetime
{
    private readonly ShelterAdoptionPostgresFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Handle_WhenNoApplicationsExist_ReturnsEmptyList()
    {
        await using var session = _fixture.Store.LightweightSession();

        var response = await GetVolunteerApplicationsQueueHandler.Handle(session, CancellationToken.None);

        response.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsEveryVolunteerApplication()
    {
        var (homeChecks, homeChecksEvent) = VolunteerApplication.ApplyNew(Guid.NewGuid(), VolunteerAreaOfInterest.HomeChecks);
        var (transport, transportEvent) = VolunteerApplication.ApplyNew(Guid.NewGuid(), VolunteerAreaOfInterest.Transport);

        await using (var seedSession = _fixture.Store.LightweightSession())
        {
            seedSession.Events.StartStream<VolunteerApplication>(homeChecks.Id, homeChecksEvent);
            seedSession.Events.StartStream<VolunteerApplication>(transport.Id, transportEvent);
            await seedSession.SaveChangesAsync();
        }

        await using var session = _fixture.Store.LightweightSession();
        var response = await GetVolunteerApplicationsQueueHandler.Handle(session, CancellationToken.None);

        response.Items.Should().HaveCount(2);
        response.Items.Should().Contain(x =>
            x.VolunteerApplicationId == homeChecks.Id &&
            x.AreaOfInterest == nameof(VolunteerAreaOfInterest.HomeChecks) &&
            x.Status == nameof(VolunteerApplicationStatus.Submitted));
        response.Items.Should().Contain(x =>
            x.VolunteerApplicationId == transport.Id &&
            x.AreaOfInterest == nameof(VolunteerAreaOfInterest.Transport) &&
            x.Status == nameof(VolunteerApplicationStatus.Submitted));
    }
}
