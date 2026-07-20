using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.RequestShelterAccount;

/// <summary>
/// The request/command for this slice - what the caller sends. Validated
/// by Wolverine.Http's built-in DataAnnotations middleware (see
/// Program.cs's MapWolverineEndpoints call), not FluentValidation.
///
/// Wolverine.Http endpoints bypass the message-bus pipeline entirely, so
/// WolverineFx.FluentValidation - which only hooks into
/// IMessageBus.InvokeAsync/SendAsync - never runs for [WolverineGet]/
/// [WolverinePost] handlers. Confirmed via reflection against the real
/// 6.17.2 assemblies: an invalid request here originally reached the
/// handler directly and 500'd on ShelterAccount.Create's guard clause
/// instead of 400ing. UtilityBillDocumentId's not-empty check needs
/// IValidatableObject since plain attributes can't express "this Guid
/// isn't the default value" the way [Required] does for reference types.
/// </summary>
public sealed record RequestShelterAccountRequest(
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
public sealed record RequestShelterAccountResponse(Guid ShelterAccountId);
