using DriverTime.Application.Companies.DTOs;

namespace DriverTime.Application.Interfaces;

public interface ICompanySettingsService
{
    Task<CompanySettingsDto?> GetAsync(
        CancellationToken cancellationToken = default);

    Task<CompanySettingsDto?> UpdateAsync(
        UpdateCompanySettingsDto settings,
        CancellationToken cancellationToken = default);
}
