using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Notifications.Api.Commands.EditNotificationTemplate;
using K9Crush.Modules.Notifications.Domain;
using K9Crush.Modules.Notifications.Domain.Events;
using Xunit;

namespace K9Crush.Modules.Notifications.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - EditNotificationTemplateHandler only
/// calls FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession
/// mocks cleanly here (ADR-031).
/// </summary>
public class EditNotificationTemplateHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static NotificationTemplate BuildTemplate() =>
        NotificationTemplate.Create(new NotificationTemplateCreatedV1("application_approved", "Application Approved", "Old subject", "Old body"));

    [Fact]
    public async Task Handle_WhenTemplateDoesNotExist_ReturnsNotFound()
    {
        var templateId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<NotificationTemplate>(templateId, null, out _);

        var result = await EditNotificationTemplateHandler.Handle(
            templateId, new EditNotificationTemplateRequest("Subject", "Body"), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenUnlocked_EditsAndAcquiresTheLock()
    {
        var callerOwnerId = Guid.NewGuid();
        var template = BuildTemplate();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(template.Id, template, out var stream);

        var result = await EditNotificationTemplateHandler.Handle(
            template.Id, new EditNotificationTemplateRequest("New subject", "New body"), BuildUser(callerOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<EditNotificationTemplateResponse>>();
        template.Subject.Should().Be("New subject");
        template.Body.Should().Be("New body");
        template.Locked.Should().BeTrue();
        template.LockedByOwnerId.Should().Be(callerOwnerId);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && ((NotificationTemplateEditedV1)o).Subject == "New subject"));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenLockedByAnotherCaller_ReturnsTemplateEditBlocked()
    {
        var lockHolderId = Guid.NewGuid();
        var template = BuildTemplate();
        template.Edit(lockHolderId, "In-progress subject", "In-progress body");

        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(template.Id, template, out _);

        var result = await EditNotificationTemplateHandler.Handle(
            template.Id, new EditNotificationTemplateRequest("Hijack subject", "Hijack body"), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        ((Conflict<string>)result.Result).Value.Should().Be("Template Edit Blocked.");
        template.Subject.Should().Be("In-progress subject", "the blocked edit must not have applied");
    }

    [Fact]
    public async Task Handle_WhenLockedByTheSameCaller_AllowsResubmittingTheDraft()
    {
        var callerOwnerId = Guid.NewGuid();
        var template = BuildTemplate();
        template.Edit(callerOwnerId, "First draft", "First draft body");

        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(template.Id, template, out _);

        var result = await EditNotificationTemplateHandler.Handle(
            template.Id, new EditNotificationTemplateRequest("Second draft", "Second draft body"), BuildUser(callerOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<EditNotificationTemplateResponse>>();
        template.Subject.Should().Be("Second draft");
    }
}
