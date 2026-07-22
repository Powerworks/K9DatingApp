using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Notifications.Api.Commands.SaveNotificationTemplate;
using K9Crush.Modules.Notifications.Domain;
using Xunit;

namespace K9Crush.Modules.Notifications.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - SaveNotificationTemplateHandler only
/// calls LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here.
/// </summary>
public class SaveNotificationTemplateHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenTemplateDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var templateId = Guid.NewGuid();
        session.LoadAsync<NotificationTemplate>(templateId, Arg.Any<CancellationToken>()).Returns((NotificationTemplate?)null);

        var result = await SaveNotificationTemplateHandler.Handle(
            templateId, new SaveNotificationTemplateRequest(false), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotLocked_ReturnsConflict()
    {
        var template = NotificationTemplate.Create("application_approved", "Application Approved", "Subject", "Body");
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<NotificationTemplate>(template.Id, Arg.Any<CancellationToken>()).Returns(template);

        var result = await SaveNotificationTemplateHandler.Handle(
            template.Id, new SaveNotificationTemplateRequest(false), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenLockedBySomeoneElse_ReturnsForbid()
    {
        var lockHolderId = Guid.NewGuid();
        var template = NotificationTemplate.Create("application_approved", "Application Approved", "Subject", "Body");
        template.Edit(lockHolderId, "Draft subject", "Draft body");

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<NotificationTemplate>(template.Id, Arg.Any<CancellationToken>()).Returns(template);

        var result = await SaveNotificationTemplateHandler.Handle(
            template.Id, new SaveNotificationTemplateRequest(false), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
        template.Locked.Should().BeTrue("a forbidden save attempt must not release someone else's lock");
    }

    [Fact]
    public async Task Handle_WhenLockedByTheCaller_ReleasesTheLock()
    {
        var callerOwnerId = Guid.NewGuid();
        var template = NotificationTemplate.Create("application_approved", "Application Approved", "Subject", "Body");
        template.Edit(callerOwnerId, "Draft subject", "Draft body");

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<NotificationTemplate>(template.Id, Arg.Any<CancellationToken>()).Returns(template);

        var result = await SaveNotificationTemplateHandler.Handle(
            template.Id, new SaveNotificationTemplateRequest(true), BuildUser(callerOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<SaveNotificationTemplateResponse>>();
        ((Ok<SaveNotificationTemplateResponse>)result.Result).Value!.AppliesToAlreadyQueuedNotifications.Should().BeTrue();
        template.Locked.Should().BeFalse();
        template.LockedByOwnerId.Should().BeNull();
        session.Received(1).Store(Arg.Is<NotificationTemplate[]>(arr => arr.Length == 1 && arr[0] == template));
    }
}
