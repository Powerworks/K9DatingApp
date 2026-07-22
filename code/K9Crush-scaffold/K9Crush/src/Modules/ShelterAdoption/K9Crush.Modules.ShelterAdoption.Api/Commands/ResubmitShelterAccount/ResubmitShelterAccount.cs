using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ResubmitShelterAccount;

/// <summary>
/// The request/command for this slice - what the caller sends. Named
/// ResubmitShelterAccount, not ResubmitShelterAccountRequest, to avoid
/// the doubled "Request" this record's own name suffix would produce -
/// the emlang yaml's literal command name is "Resubmit Shelter Account
/// Request" (resubmitting the account *request*).
/// </summary>
public sealed record ResubmitShelterAccountRequest(
    [property: Required, MaxLength(500)] string BusinessDetails,
    Guid UtilityBillDocumentId) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (UtilityBillDocumentId == Guid.Empty)
            yield return new ValidationResult("UtilityBillDocumentId is required.", [nameof(UtilityBillDocumentId)]);
    }
}

/// <summary>What this slice hands back to the caller.</summary>
public sealed record ResubmitShelterAccountResponse(Guid ShelterAccountId, string Status);
