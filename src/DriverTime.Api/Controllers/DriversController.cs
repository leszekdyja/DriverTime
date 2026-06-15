using DriverTime.Application.Drivers.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Violations.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/drivers")]
public class DriversController : ControllerBase
{
    private readonly IDriverService _driverService;
    private readonly IDriverViolationService _driverViolationService;

    public DriversController(
        IDriverService driverService,
        IDriverViolationService driverViolationService)
    {
        _driverService = driverService;
        _driverViolationService = driverViolationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DriverDto>>> GetAll()
    {
        var drivers = await _driverService.GetAllAsync();

        return Ok(drivers);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DriverDetailsDto>> GetById(Guid id)
    {
        var driver = await _driverService.GetByIdAsync(id);

        if (driver == null)
        {
            return NotFound();
        }

        return Ok(driver);
    }

    [HttpGet("{id:guid}/violations")]
    public async Task<ActionResult<IReadOnlyList<DriverViolationDto>>> GetViolations(
        Guid id,
        CancellationToken cancellationToken)
    {
        var violations = await _driverViolationService
            .GetViolationsForDriverAsync(id, cancellationToken);

        return violations is null ? NotFound() : Ok(violations);
    }

    [HttpPost]
    public async Task<ActionResult<DriverDto>> Create(
        [FromBody] CreateDriverDto dto)
    {
        var createdDriver = await _driverService.CreateAsync(dto);

        return Ok(createdDriver);
    }
}
