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

    public Guid? OperatingCompanyId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue("operating_company_id");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public Guid DriverId => GetGuidClaim("driver_id");

    public bool IsMobileDriver =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue("token_type") == "mobile_driver";

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    private Guid GetGuidClaim(string claimType)
    {
        var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);

        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
