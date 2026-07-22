using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Notifications.Api.Commands.EditNotificationTemplate;
using K9Crush.Modules.Notifications.Domain;
using Xunit;

namespace K9Crush.Modules.Notifications.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - EditNotificationTemplateHandler only
/// calls LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here.
/// </summary>
public class EditNotificationTemplateHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenTemplateDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var templateId = Guid.NewGuid();
        session.LoadAsync<NotificationTemplate>(templateId, Arg.Any<CancellationToken>()).Returns((NotificationTemplate?)null);

        var result = await EditNotificationTemplateHandler.Handle(
            templateId, new EditNotificationTemplateRequest("Subject", "Body"), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenUnlocked_EditsAndAcquiresTheLock()
    {
        var callerOwnerId = Guid.NewGuid();
        var template = NotificationTemplate.Create("application_approved", "Application Approved", "Old subject", "Old body");
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<NotificationTemplate>(template.Id, Arg.Any<CancellationToken>()).Returns(template);

        var result = await EditNotificationTemplateHandler.Handle(
            template.Id, new EditNotificationTemplateRequest("New subject", "New body"), BuildUser(callerOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<EditNotificationTemplateResponse>>();
        template.Subject.Should().Be("New subject");
        template.Body.Should().Be("New body");
        template.Locked.Should().BeTrue();
        template.LockedByOwnerId.Should().Be(callerOwnerId);
        session.Received(1).Store(Arg.Is<NotificationTemplate[]>(arr => arr.Length == 1 && arr[0] == template));
    }

    [Fact]
    public async Task Handle_WhenLockedByAnotherCaller_ReturnsTemplateEditBlocked()
    {
        var lockHolderId = Guid.NewGuid();
        var template = NotificationTemplate.Create("application_approved", "Application Approved", "Old subject", "Old body");
        template.Edit(lockHolderId, "In-progress subject", "In-progress body");

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<NotificationTemplate>(template.Id, Arg.Any<CancellationToken>()).Returns(template);

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
        var template = NotificationTemplate.Create("application_approved", "Application Approved", "Old subject", "Old body");
        template.Edit(callerOwnerId, "First draft", "First draft body");

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<NotificationTemplate>(template.Id, Arg.Any<CancellationToken>()).Returns(template);

        var result = await EditNotificationTemplateHandler.Handle(
            template.Id, new EditNotificationTemplateRequest("Second draft", "Second draft body"), BuildUser(callerOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<EditNotificationTemplateResponse>>();
        template.Subject.Should().Be("Second draft");
    }
}
