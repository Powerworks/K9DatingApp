using System.Security.Claims;
using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.Notifications.Api.ReadModels.ViewNotificationPreferences;
using K9Crush.Modules.Notifications.Domain;
using Xunit;

namespace K9Crush.Modules.Notifications.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ViewNotificationPreferencesHandler only
/// calls LoadAsync, so IQuerySession mocks cleanly here.
/// </summary>
public class ViewNotificationPreferencesHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenNoPreferenceDocumentExistsYet_ReturnsAllEnabledDefaultsWithoutPersisting()
    {
        var ownerId = Guid.NewGuid();
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<NotificationPreference>(ownerId, Arg.Any<CancellationToken>()).Returns((NotificationPreference?)null);

        var response = await ViewNotificationPreferencesHandler.Handle(BuildUser(ownerId), session, CancellationToken.None);

        response.Preferences.Should().HaveCount(Enum.GetValues<NotificationType>().Length);
        response.Preferences.Should().OnlyContain(p => p.Enabled && !p.Mandatory);
    }

    [Fact]
    public async Task Handle_WhenAPreferenceHasBeenDisabled_ReflectsIt()
    {
        var ownerId = Guid.NewGuid();
        var preference = NotificationPreference.CreateDefault(ownerId);
        preference.SetEnabled(NotificationType.Matches, false);

        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<NotificationPreference>(ownerId, Arg.Any<CancellationToken>()).Returns(preference);

        var response = await ViewNotificationPreferencesHandler.Handle(BuildUser(ownerId), session, CancellationToken.None);

        response.Preferences.Single(p => p.NotificationType == NotificationType.Matches).Enabled.Should().BeFalse();
        response.Preferences.Where(p => p.NotificationType != NotificationType.Matches).Should().OnlyContain(p => p.Enabled);
    }
}
