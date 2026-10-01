namespace DriverTime.Application.Drivers.DTOs;

public class DriverDto
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string CardNumber { get; set; } = string.Empty;

    public DateTime? CardExpiryDate { get; set; }

    public string CardIssuingCountry { get; set; } = string.Empty;

    public bool IncludeInPlanning { get; set; }

    public bool PlanningNoNightDuty { get; set; }
    public bool PlanningNoWeekends { get; set; }
    public bool PlanningNoSaturdays { get; set; }
    public bool PlanningNoHolidays { get; set; }
    public bool PlanningNoDaysOff { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

