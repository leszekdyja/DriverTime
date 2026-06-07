using DriverTime.Application.Companies.DTOs;
using DriverTime.Application.Companies.Services;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public CompaniesController(ICompanyService companyService)
    {
        _companyService = companyService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var companies = await _companyService.GetAllAsync();

        return Ok(companies);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCompanyDto dto)
    {
        var company = await _companyService.CreateAsync(dto);

        return Ok(company);
    }
}
