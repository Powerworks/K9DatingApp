using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Notifications.Api.Commands.SaveNotificationTemplate;
using K9Crush.Modules.Notifications.Domain;
using K9Crush.Modules.Notifications.Domain.Events;
using Xunit;

namespace K9Crush.Modules.Notifications.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - SaveNotificationTemplateHandler only
/// calls FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession
/// mocks cleanly here (ADR-031).
/// </summary>
public class SaveNotificationTemplateHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static NotificationTemplate BuildTemplate() =>
        NotificationTemplate.Create(new NotificationTemplateCreatedV1("application_approved", "Application Approved", "Subject", "Body"));

    [Fact]
    public async Task Handle_WhenTemplateDoesNotExist_ReturnsNotFound()
    {
        var templateId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<NotificationTemplate>(templateId, null, out _);

        var result = await SaveNotificationTemplateHandler.Handle(
            templateId, new SaveNotificationTemplateRequest(false), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotLocked_ReturnsConflict()
    {
        var template = BuildTemplate();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(template.Id, template, out _);

        var result = await SaveNotificationTemplateHandler.Handle(
            template.Id, new SaveNotificationTemplateRequest(false), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenLockedBySomeoneElse_ReturnsForbid()
    {
        var lockHolderId = Guid.NewGuid();
        var template = BuildTemplate();
        template.Edit(lockHolderId, "Draft subject", "Draft body");

        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(template.Id, template, out _);

        var result = await SaveNotificationTemplateHandler.Handle(
            template.Id, new SaveNotificationTemplateRequest(false), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
        template.Locked.Should().BeTrue("a forbidden save attempt must not release someone else's lock");
    }

    [Fact]
    public async Task Handle_WhenLockedByTheCaller_ReleasesTheLock()
    {
        var callerOwnerId = Guid.NewGuid();
        var template = BuildTemplate();
        template.Edit(callerOwnerId, "Draft subject", "Draft body");

        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(template.Id, template, out var stream);

        var result = await SaveNotificationTemplateHandler.Handle(
            template.Id, new SaveNotificationTemplateRequest(true), BuildUser(callerOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<SaveNotificationTemplateResponse>>();
        ((Ok<SaveNotificationTemplateResponse>)result.Result).Value!.AppliesToAlreadyQueuedNotifications.Should().BeTrue();
        template.Locked.Should().BeFalse();
        template.LockedByOwnerId.Should().BeNull();
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(NotificationTemplateSavedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
