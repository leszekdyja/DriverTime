using System.Security.Claims;
using DriverTime.Application.Interfaces;

namespace DriverTime.Api.Authentication;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId => GetGuidClaim(ClaimTypes.NameIdentifier);

    public Guid CompanyId => GetGuidClaim("company_id");

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    private Guid GetGuidClaim(string claimType)
    {
        var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);

        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
