using System.ComponentModel.DataAnnotations;
using K9Crush.Modules.Media.Domain;

namespace K9Crush.Modules.Media.Api.Commands.UploadMedia;

/// <summary>
/// The request/command for this slice. StorageUrl is a reference to an
/// object the client already uploaded directly to Supabase Storage
/// (ADR-005/024) - this backend never receives raw file bytes.
/// </summary>
public sealed record UploadMediaRequest(
    MediaType MediaType,
    [property: Required] string StorageUrl) : IValidatableObject
{
    /// <summary>
    /// The emlang yaml's "Upload Failed" -> "Reject Upload" -> "Upload
    /// Rejected: Invalid File" branch. A disclosed simplification: with
    /// no real file-content access (StorageUrl is just a reference, not
    /// bytes), "invalid file" is modeled as an extension check against
    /// the declared MediaType rather than real content-type sniffing.
    /// </summary>
    private static readonly string[] PhotoExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private static readonly string[] VideoExtensions = [".mp4", ".mov", ".webm"];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var allowedExtensions = MediaType == MediaType.Photo ? PhotoExtensions : VideoExtensions;
        if (!allowedExtensions.Any(ext => StorageUrl.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
        {
            yield return new ValidationResult(
                $"StorageUrl does not have a valid extension for MediaType {MediaType}.", [nameof(StorageUrl)]);
        }
    }
}

/// <summary>What this slice hands back to the caller.</summary>
public sealed record UploadMediaResponse(Guid MediaAssetId, string MediaType, DateTimeOffset UploadedAt);
