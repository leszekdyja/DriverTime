using DriverTime.Domain.Entities;

namespace DriverTime.Application.Planning;

public static class PlanningDutyWorkMinutesResolver
{
    public static PlanningDutyWorkMinutesResult Resolve(PlanningDuty duty, PlanningWorkInterval interval)
    {
        if (duty.WorkMinutes.HasValue && duty.WorkMinutes.Value > 0)
        {
            return new PlanningDutyWorkMinutesResult(
                duty.WorkMinutes.Value,
                PlanningDutyWorkMinutesSource.ExplicitWorkMinutes,
                null);
        }

        if (duty.TotalDurationMinutes.HasValue && duty.TotalDurationMinutes.Value > 0)
        {
            return new PlanningDutyWorkMinutesResult(
                duty.TotalDurationMinutes.Value,
                PlanningDutyWorkMinutesSource.TotalDurationFallback,
                $"Służba {duty.DutyNumber}: brak poprawnego WorkMinutes, użyto TotalDurationMinutes.");
        }

        return new PlanningDutyWorkMinutesResult(
            interval.DurationMinutes,
            PlanningDutyWorkMinutesSource.IntervalDurationFallback,
            $"Służba {duty.DutyNumber}: brak poprawnego WorkMinutes i TotalDurationMinutes, użyto długości przedziału pracy.");
    }
}
