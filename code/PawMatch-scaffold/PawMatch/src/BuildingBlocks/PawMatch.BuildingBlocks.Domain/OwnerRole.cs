namespace PawMatch.BuildingBlocks.Domain;

/// <summary>
/// The role dimension from ADR-017 - looked up from our own Postgres,
/// keyed by the Supabase JWT's sub claim, never carried as a Supabase
/// custom claim (see Solution Architecture doc Section 10, ADR-017).
///
/// Lives in BuildingBlocks.Domain, not the Identity module, even though
/// Identity's OwnerAccount is the system of record for it - authorization
/// policies in Api.Host and every module's [Authorize(Policy = ...)]
/// attributes need to reference this type, and a Domain project may only
/// reference BuildingBlocks.Domain (never another module's Domain), so a
/// shared vocabulary type is the only place this can live without
/// breaking that rule. See IOwnerRoleLookup in BuildingBlocks.Web for how
/// this gets resolved without any module referencing Identity's Domain
/// directly.
/// </summary>
public enum OwnerRole
{
    Owner,
    Vendor,
    Shelter,
    Admin
}
