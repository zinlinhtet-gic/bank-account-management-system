using Microsoft.AspNetCore.Authorization;

namespace bams.server.Services;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission.ToLower();
    }
}