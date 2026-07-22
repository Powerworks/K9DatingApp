namespace K9Crush.Modules.Media.Api.Commands.RemoveMedia;

/// <summary>
/// The request/command for this slice. CascadeDeletesEngagement is
/// accepted but currently unused - the yaml's own prop, but no
/// Engagement/comments/likes concept exists anywhere in this codebase
/// yet for it to cascade into. Disclosed, not silently dropped.
/// </summary>
public sealed record RemoveMediaRequest(bool CascadeDeletesEngagement);
