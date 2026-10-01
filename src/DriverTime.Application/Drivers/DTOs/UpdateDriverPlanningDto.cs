namespace DriverTime.Application.Drivers.DTOs;

public class UpdateDriverPlanningDto
{
    public bool IncludeInPlanning { get; set; }
    public bool PlanningNoNightDuty { get; set; }
    public bool PlanningNoWeekends { get; set; }
    public bool PlanningNoSaturdays { get; set; }
    public bool PlanningNoHolidays { get; set; }
    public bool PlanningNoDaysOff { get; set; }
}
