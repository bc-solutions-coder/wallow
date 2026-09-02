using Wallow.Shared.Kernel.Errors;

namespace Wallow.Identity.Domain.Errors;

/// <summary>
/// The error catalog the Identity module owns. Registered by <c>AddIdentityModule</c>.
/// </summary>
/// <remarks>
/// The <c>Auth.*</c> and <c>Mfa.*</c> entries name the outcomes of the sign-in, MFA and account
/// endpoints. They are catalogued here so the aggregated <c>ErrorCode</c> enum is complete; the
/// endpoints themselves still answer with their legacy shape until they are migrated to problems.
/// </remarks>
public static class IdentityErrors
{
    // Users and organizations

    public static readonly ErrorCatalogEntry UserNotFound = new(
        "Identity.UserNotFound", ErrorKind.NotFound, "User not found");

    public static readonly ErrorCatalogEntry OrganizationNotFound = new(
        "Identity.OrganizationNotFound", ErrorKind.NotFound, "Organization not found");

    public static readonly ErrorCatalogEntry InvitationNotFound = new(
        "Identity.InvitationNotFound", ErrorKind.NotFound, "Invitation not found");

    public static readonly ErrorCatalogEntry FirstNameRequired = new(
        "Identity.FirstNameRequired", ErrorKind.BusinessRule, "First name cannot be empty");

    public static readonly ErrorCatalogEntry LastNameRequired = new(
        "Identity.LastNameRequired", ErrorKind.BusinessRule, "Last name cannot be empty");

    public static readonly ErrorCatalogEntry EmailRequired = new(
        "Identity.EmailRequired", ErrorKind.BusinessRule, "Email cannot be empty");

    public static readonly ErrorCatalogEntry MfaGraceDeadlineMustBeFuture = new(
        "Identity.MfaGraceDeadlineMustBeFuture", ErrorKind.BusinessRule, "MFA grace deadline must be in the future");

    public static readonly ErrorCatalogEntry ExpiryMustBeFuture = new(
        "Identity.ExpiryMustBeFuture", ErrorKind.BusinessRule, "Email change expiry must be in the future");

    public static readonly ErrorCatalogEntry NoPendingEmailChange = new(
        "Identity.NoPendingEmailChange", ErrorKind.BusinessRule, "No pending email change to confirm");

    public static readonly ErrorCatalogEntry ReservedRoleName = new(
        "Identity.ReservedRoleName", ErrorKind.Validation, "The global administrator name is reserved and cannot be assigned as a role.");

    public static readonly ErrorCatalogEntry RoleNotFound = new(
        "Identity.RoleNotFound", ErrorKind.BusinessRule, "The requested role does not exist");

    public static readonly ErrorCatalogEntry OrganizationNameRequired = new(
        "Identity.OrganizationNameRequired", ErrorKind.BusinessRule, "Organization name cannot be empty");

    public static readonly ErrorCatalogEntry OrganizationSlugRequired = new(
        "Identity.OrganizationSlugRequired", ErrorKind.BusinessRule, "Organization slug cannot be empty");

    public static readonly ErrorCatalogEntry OrganizationAlreadyInactive = new(
        "Identity.OrganizationAlreadyInactive", ErrorKind.BusinessRule, "Organization is already inactive");

    public static readonly ErrorCatalogEntry OrganizationAlreadyActive = new(
        "Identity.OrganizationAlreadyActive", ErrorKind.BusinessRule, "Organization is already active");

    public static readonly ErrorCatalogEntry PlatformSuspensionReasonRequired = new(
        "Identity.PlatformSuspensionReasonRequired", ErrorKind.BusinessRule, "A platform suspension requires a reason");

    public static readonly ErrorCatalogEntry OrganizationAlreadySuspendedByPlatform = new(
        "Identity.OrganizationAlreadySuspendedByPlatform", ErrorKind.BusinessRule, "Organization is already suspended by the platform");

    public static readonly ErrorCatalogEntry OrganizationNotSuspendedByPlatform = new(
        "Identity.OrganizationNotSuspendedByPlatform", ErrorKind.BusinessRule, "Organization is not suspended by the platform");

    public static readonly ErrorCatalogEntry OrganizationSuspendedByPlatform = new(
        "Identity.OrganizationSuspendedByPlatform", ErrorKind.BusinessRule, "The organization is suspended by the platform");

    public static readonly ErrorCatalogEntry OrganizationNameMismatch = new(
        "Identity.OrganizationNameMismatch", ErrorKind.BusinessRule, "The confirmed name does not match the organization name");

    // Memberships

    public static readonly ErrorCatalogEntry UserIdRequired = new(
        "Identity.UserIdRequired", ErrorKind.BusinessRule, "User ID cannot be empty");

    public static readonly ErrorCatalogEntry MemberNotFound = new(
        "Identity.MemberNotFound", ErrorKind.BusinessRule, "User is not a member of this organization");

    public static readonly ErrorCatalogEntry AlreadyAMember = new(
        "Identity.AlreadyAMember", ErrorKind.BusinessRule, "That email address already belongs to this organization");

    public static readonly ErrorCatalogEntry LastOwner = new(
        "Identity.LastOwner", ErrorKind.BusinessRule, "An organization must keep at least one active owner");

    public static readonly ErrorCatalogEntry MembershipNotPending = new(
        "Identity.MembershipNotPending", ErrorKind.BusinessRule, "Only a pending membership can be reviewed");

    public static readonly ErrorCatalogEntry MembershipNotActive = new(
        "Identity.MembershipNotActive", ErrorKind.BusinessRule, "Only an active membership can be suspended");

    public static readonly ErrorCatalogEntry MembershipNotSuspended = new(
        "Identity.MembershipNotSuspended", ErrorKind.BusinessRule, "Only a suspended membership can be reinstated");

    public static readonly ErrorCatalogEntry MembershipNotDenied = new(
        "Identity.MembershipNotDenied", ErrorKind.BusinessRule, "Only a denied membership can be asked for again");

    public static readonly ErrorCatalogEntry MembershipNotReinstatable = new(
        "Identity.MembershipNotReinstatable", ErrorKind.BusinessRule, "Membership of this organization cannot be resumed by invitation");

    public static readonly ErrorCatalogEntry DenialCooldown = new(
        "Identity.DenialCooldown", ErrorKind.BusinessRule, "A recent request to this organization was denied; it may be asked again later");

    // Invitations

    public static readonly ErrorCatalogEntry InvitationEmailRequired = new(
        "Identity.InvitationEmailRequired", ErrorKind.BusinessRule, "Invitation email cannot be empty");

    public static readonly ErrorCatalogEntry InvitationNotPending = new(
        "Identity.InvitationNotPending", ErrorKind.BusinessRule, "Only a pending invitation can be changed");

    public static readonly ErrorCatalogEntry InvitationExpired = new(
        "Identity.InvitationExpired", ErrorKind.BusinessRule, "This invitation has expired");

    public static readonly ErrorCatalogEntry InvitationEmailNotVerified = new(
        "Identity.InvitationEmailNotVerified", ErrorKind.BusinessRule, "Verify your email address before accepting an invitation");

    public static readonly ErrorCatalogEntry InvitationEmailMismatch = new(
        "Identity.InvitationEmailMismatch", ErrorKind.BusinessRule, "This invitation was issued to a different email address");

    // Clients and scopes

    public static readonly ErrorCatalogEntry ClientIdRequired = new(
        "Identity.ClientIdRequired", ErrorKind.BusinessRule, "Client id cannot be empty");

    public static readonly ErrorCatalogEntry ClientNameRequired = new(
        "Identity.ClientNameRequired", ErrorKind.BusinessRule, "Client name cannot be empty");

    public static readonly ErrorCatalogEntry ClientNameUnusable = new(
        "Identity.ClientNameUnusable", ErrorKind.BusinessRule, "A client name must contain at least one letter or digit.");

    public static readonly ErrorCatalogEntry ClientOrganizationRequired = new(
        "Identity.ClientOrganizationRequired", ErrorKind.BusinessRule, "A registered client must belong to an organization");

    public static readonly ErrorCatalogEntry ClientIdTaken = new(
        "Identity.ClientIdTaken", ErrorKind.BusinessRule, "A client with that id already exists");

    public static readonly ErrorCatalogEntry ClientAlreadySuspended = new(
        "Identity.ClientAlreadySuspended", ErrorKind.BusinessRule, "The client is already suspended");

    public static readonly ErrorCatalogEntry ClientNotSuspended = new(
        "Identity.ClientNotSuspended", ErrorKind.BusinessRule, "The client is not suspended");

    public static readonly ErrorCatalogEntry ClientAlreadySuspendedByPlatform = new(
        "Identity.ClientAlreadySuspendedByPlatform", ErrorKind.BusinessRule, "The client is already suspended by the platform");

    public static readonly ErrorCatalogEntry ClientNotSuspendedByPlatform = new(
        "Identity.ClientNotSuspendedByPlatform", ErrorKind.BusinessRule, "The client is not suspended by the platform");

    public static readonly ErrorCatalogEntry UnknownScope = new(
        "Identity.UnknownScope", ErrorKind.BusinessRule, "One or more requested scopes do not exist.");

    public static readonly ErrorCatalogEntry PlatformOnlyScope = new(
        "Identity.PlatformOnlyScope", ErrorKind.BusinessRule, "Scopes reserved for the platform's own clients cannot be granted here.");

    public static readonly ErrorCatalogEntry ScopeCodeRequired = new(
        "Identity.ScopeCodeRequired", ErrorKind.BusinessRule, "API scope code cannot be empty");

    public static readonly ErrorCatalogEntry ScopeDisplayNameRequired = new(
        "Identity.ScopeDisplayNameRequired", ErrorKind.BusinessRule, "API scope display name cannot be empty");

    public static readonly ErrorCatalogEntry ScopeCategoryRequired = new(
        "Identity.ScopeCategoryRequired", ErrorKind.BusinessRule, "API scope category cannot be empty");

    // Settings

    // Sign-in, account and MFA outcomes

    public static readonly ErrorCatalogEntry AuthInvalidCredentials = new(
        "Auth.InvalidCredentials", ErrorKind.Unauthenticated, "The email address or password is incorrect.");

    public static readonly ErrorCatalogEntry AuthLockedOut = new(
        "Auth.LockedOut", ErrorKind.Forbidden, "This account is locked. Try again later.");

    public static readonly ErrorCatalogEntry AuthEmailNotConfirmed = new(
        "Auth.EmailNotConfirmed", ErrorKind.Forbidden, "Confirm your email address before signing in.");

    public static readonly ErrorCatalogEntry AuthProviderRequired = new(
        "Auth.ProviderRequired", ErrorKind.Validation, "Choose a sign-in provider.");

    public static readonly ErrorCatalogEntry AuthProviderUnsupported = new(
        "Auth.ProviderUnsupported", ErrorKind.Validation, "That sign-in provider is not supported.");

    public static readonly ErrorCatalogEntry AuthTicketInvalid = new(
        "Auth.TicketInvalid", ErrorKind.Unauthenticated, "The sign-in ticket is invalid or has expired.");

    public static readonly ErrorCatalogEntry AuthTicketAlreadyUsed = new(
        "Auth.TicketAlreadyUsed", ErrorKind.Unauthenticated, "The sign-in ticket has already been used.");

    public static readonly ErrorCatalogEntry AuthPasswordsDoNotMatch = new(
        "Auth.PasswordsDoNotMatch", ErrorKind.Validation, "The passwords do not match.");

    public static readonly ErrorCatalogEntry AuthClientIdInvalid = new(
        "Auth.ClientIdInvalid", ErrorKind.Validation, "The client id is not recognised.");

    public static readonly ErrorCatalogEntry AuthEmailTaken = new(
        "Auth.EmailTaken", ErrorKind.Conflict, "An account with that email address already exists.");

    public static readonly ErrorCatalogEntry AuthTokenInvalid = new(
        "Auth.TokenInvalid", ErrorKind.Validation, "The token is invalid.");

    public static readonly ErrorCatalogEntry AuthTokenExpired = new(
        "Auth.TokenExpired", ErrorKind.Validation, "The token has expired.");

    public static readonly ErrorCatalogEntry AuthEmailUnchanged = new(
        "Auth.EmailUnchanged", ErrorKind.Validation, "The new email address is the same as the current one.");

    public static readonly ErrorCatalogEntry AuthEmailClaimMissing = new(
        "Auth.EmailClaimMissing", ErrorKind.Validation, "The sign-in did not include an email address.");

    public static readonly ErrorCatalogEntry MfaSessionMissing = new(
        "Mfa.SessionMissing", ErrorKind.Unauthenticated, "Start signing in again to continue with multi-factor authentication.");

    public static readonly ErrorCatalogEntry MfaCodeInvalid = new(
        "Mfa.CodeInvalid", ErrorKind.Validation, "The verification code is incorrect.");

    public static readonly ErrorCatalogEntry MfaLockedOut = new(
        "Mfa.LockedOut", ErrorKind.Forbidden, "Too many failed verification attempts. Try again later.");

    public static readonly ErrorCatalogEntry MfaNotEnabled = new(
        "Mfa.NotEnabled", ErrorKind.Validation, "Multi-factor authentication is not enabled for this account.");

    public static readonly ErrorCatalogEntry MfaEnrollmentTokenInvalid = new(
        "Mfa.EnrollmentTokenInvalid", ErrorKind.Unauthenticated, "The enrollment token is invalid or has expired.");

    public static readonly ErrorCatalogEntry MfaUpdateFailed = new(
        "Mfa.UpdateFailed", ErrorKind.Failure, "The multi-factor settings could not be saved.");
}
