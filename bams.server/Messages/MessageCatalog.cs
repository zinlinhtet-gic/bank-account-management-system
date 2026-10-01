namespace bams.server.Messages;

public static class MessageCatalog
{
    private static readonly IReadOnlyDictionary<MessageCode, string> Messages =
        new Dictionary<MessageCode, string>
        {
            [MessageCode.Success] = "Operation completed successfully.",
            [MessageCode.UserCreatedSuccessfully] = "User created successfully.",
            [MessageCode.UserUpdatedSuccessfully] = "User updated successfully.",
            [MessageCode.UserPasswordResetSuccessfully] = "Password reset successfully. The user must change it at the next login.",
            [MessageCode.UserDeletedSuccessfully] = "User deleted successfully.",
            [MessageCode.AccountCreatedSuccessfully] = "Account created successfully.",
            [MessageCode.CustomerCreatedSuccessfully] = "Customer created successfully.",
            [MessageCode.CustomerUpdatedSuccessfully] = "Customer updated successfully.",
            [MessageCode.CustomerKycReviewedSuccessfully] = "Customer KYC review recorded successfully.",
            [MessageCode.ValidationFailed] = "One or more validation errors occurred.",
            [MessageCode.RequiredFieldMissing] = "Please fill in all required fields.",
            [MessageCode.InvalidRequest] = "The request is invalid.",
            [MessageCode.InvalidAmount] = "The provided amount is invalid.",
            [MessageCode.InvalidCredentials] = "Invalid username or password.",
            [MessageCode.PasswordDoesNotMeetRequirements] = "Password must be at least 8 characters long and include: 1 uppercase letter (A-Z), 1 lowercase letter (a-z), 1 number (0-9), and 1 special character (!@#$%^&*).",
            [MessageCode.InvalidEmailFormat] = "Please enter a valid email address.",
            [MessageCode.InvalidUsernameFormat] = "Username must be 3-64 characters: letters, numbers, dot, underscore or hyphen, with no spaces.",
            [MessageCode.InvalidPhoneFormat] = "Please enter a valid phone number (digits, +, spaces, hyphens or brackets).",
            [MessageCode.InvalidRole] = "The selected role is not valid.",
            [MessageCode.FieldTooLong] = "One or more fields are longer than allowed.",
            [MessageCode.InvalidDateRange] = "The start date must be on or before the end date.",
            [MessageCode.NewPasswordSameAsCurrent] = "The new password must be different from your current password.",
            [MessageCode.DefaultPasswordNotAllowed] = "You cannot use a default password. Please choose your own password.",
            [MessageCode.OpeningBalanceInvalid] = "Opening balance is below the account type's minimum.",
            [MessageCode.CustomerFullNameRequired] = "Customer full name is required.",
            [MessageCode.CustomerDateOfBirthInvalid] = "Customer date of birth is invalid.",
            [MessageCode.CustomerBelowMinimumAge] = "Customer does not meet the minimum age requirement.",
            [MessageCode.NrcNumberRequired] = "NRC number is required for citizen customers.",
            [MessageCode.PassportNumberRequired] = "Passport number is required for foreigner customers.",
            [MessageCode.CustomerDocumentFileEmpty] = "The uploaded customer document file is empty.",
            [MessageCode.CustomerEmailInvalid] = "The email address is invalid.",
            [MessageCode.CustomerDocumentTypeRequired] = "A new document entry must specify a document type.",
            [MessageCode.InvalidKycReviewStatus] = "A KYC review must set the status to either Verified or Rejected.",
            [MessageCode.AuthenticationRequired] = "Authentication is required.",
            [MessageCode.PasswordChangeRequired] = "You must change your password before continuing.",
            [MessageCode.PasswordChangedSuccessfully] = "Password changed successfully.",
            [MessageCode.UserAccountDisabled] = "This user account is disabled. Please contact your administrator.",
            [MessageCode.UserAccountDeleted] = "This user account has been deleted. Please contact your administrator.",
            [MessageCode.AccessDenied] = "Access denied.",
            [MessageCode.InsufficientPermission] = "You do not have permission to perform this operation.",
            [MessageCode.AccountNotFound] = "Account was not found.",
            [MessageCode.CustomerNotFound] = "Customer was not found.",
            [MessageCode.AccountTypeNotFound] = "Account type was not found.",
            [MessageCode.CustomerDocumentNotFound] = "The specified customer document was not found.",
            [MessageCode.KycReviewerNotFound] = "The specified reviewing user was not found.",
            [MessageCode.CustomerAlreadyExists] = "A customer with the same NRC number, passport number, or email already exists.",
            [MessageCode.UserNotFound] = "User was not found.",
            [MessageCode.UsernameAlreadyExists] = "A user with this username already exists.",
            [MessageCode.EmailAlreadyExists] = "A user with this email already exists.",
            [MessageCode.BusinessRuleViolation] = "The requested operation violates a business rule.",
            [MessageCode.LastManagerCannotBeRemoved] = "This is the only active manager. Add another manager before deleting this account or changing its role.",
            [MessageCode.InternalServerError] = "An unexpected error occurred."
        };

    // Resolves the human-readable message associated with a stable message code.
    public static string GetMessage(MessageCode code)
    {
        return Messages.TryGetValue(code, out var message)
            ? message
            : "Unknown application message.";
    }
}
