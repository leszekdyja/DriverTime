using System.ComponentModel.DataAnnotations;
using DriverTime.Application.Companies.DTOs;
using DriverTime.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/company/settings")]
public class CompanyController : ControllerBase
{
    private readonly ICompanySettingsService _companySettingsService;

    public CompanyController(ICompanySettingsService companySettingsService)
    {
        _companySettingsService = companySettingsService;
    }

    [HttpGet]
    public async Task<ActionResult<CompanySettingsDto>> Get(
        CancellationToken cancellationToken)
    {
        var settings = await _companySettingsService.GetAsync(cancellationToken);

        return settings is null ? NotFound() : Ok(settings);
    }

    [HttpPut]
    public async Task<ActionResult<CompanySettingsDto>> Update(
        UpdateCompanySettingsDto request,
        CancellationToken cancellationToken)
    {
        request.Name = request.Name.Trim();
        request.Email = request.Email?.Trim();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Nazwa firmy jest wymagana." });
        }

        if (!string.IsNullOrWhiteSpace(request.Email)
            && !new EmailAddressAttribute().IsValid(request.Email))
        {
            return BadRequest(new { message = "Adres e-mail firmy jest nieprawidlowy." });
        }

        var settings = await _companySettingsService.UpdateAsync(
            request,
            cancellationToken);

        return settings is null ? NotFound() : Ok(settings);
    }
}
