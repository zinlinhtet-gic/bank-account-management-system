namespace bams.desktop.Constants;

/// <summary>
/// Server address and endpoint paths used by the client services.
/// </summary>
public static class ApiConstants
{
    public const string ServerBaseAddress = "http://localhost:5121";

    public const string AuthEndpoint = "api/auth";
    public const string LoginEndpoint = AuthEndpoint + "/login";
    public const string PermissionsEndpoint = AuthEndpoint + "/permissions";
    public const string ChangePasswordEndpoint = AuthEndpoint + "/change-password";
    public const string HeartbeatEndpoint = AuthEndpoint + "/heartbeat";
    public const string LogoutEndpoint = AuthEndpoint + "/logout";

    public const string AccountsEndpoint = "api/accounts";
    public const string AccountCustomerLookupEndpoint = AccountsEndpoint + "/customer-lookup";
    public const string AccountOpeningOptionsEndpoint = AccountsEndpoint + "/opening-options";
    public const string CustomersEndpoint = "api/customers";
    public const string AccountTypesEndpoint = "api/account-types";
    public const string InterestRateRulesEndpoint = "api/interest-rate-rules";
    // User Management. Routes with an id: $"{UsersEndpoint}/{id}" and $"{UsersEndpoint}/{id}/{ResetPasswordSegment}".
    public const string UsersEndpoint = "api/users";
    public const string UserRolesEndpoint = UsersEndpoint + "/roles";
    public const string ResetPasswordSegment = "reset-password";

    // Other Banks
    public const string OtherBanksEndpoint = "api/other-banks";

    // Interest Rate. Routes with an id: $"{InterestRatesEndpoint}/{id}" (update).
    public const string InterestRatesEndpoint = "api/interest-rates";
    public const string InterestRateAccountTypesEndpoint = InterestRatesEndpoint + "/account-types";

    // Fee Rate. Routes with an id: $"{FeeRatesEndpoint}/{id}" (update).
    public const string FeeRatesEndpoint = "api/fee-rates";
    public const string FeeRateAccountTypesEndpoint = FeeRatesEndpoint + "/account-types";

    public const string BearerScheme = "Bearer";
}
