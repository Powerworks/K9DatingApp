using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.Chat.Api.Commands.SendMessage;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record SendMessageRequest([property: Required, MaxLength(2000)] string Text);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record SendMessageResponse(Guid MessageId, DateTimeOffset SentAt);
