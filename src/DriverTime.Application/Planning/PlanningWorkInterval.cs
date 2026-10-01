using DriverTime.Domain.Entities;

namespace DriverTime.Application.Planning;

public readonly record struct PlanningWorkInterval(
    DateTime Start,
    DateTime End,
    int DurationMinutes,
    bool CrossesMidnight)
{
    public static bool TryCreate(DateOnly workDate, PlanningDuty duty, out PlanningWorkInterval interval)
    {
        interval = default;
        if (!duty.StartTime.HasValue || !duty.EndTime.HasValue)
        {
            return false;
        }

        var start = workDate.ToDateTime(duty.StartTime.Value);
        var end = workDate.ToDateTime(duty.EndTime.Value);
        var crossesMidnight = duty.EndTime.Value <= duty.StartTime.Value;
        if (crossesMidnight)
        {
            end = end.AddDays(1);
        }

        interval = new PlanningWorkInterval(
            start,
            end,
            (int)(end - start).TotalMinutes,
            crossesMidnight);

        return true;
    }
}
