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

    public const string BearerScheme = "Bearer";
}
