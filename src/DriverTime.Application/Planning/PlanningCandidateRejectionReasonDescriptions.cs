namespace DriverTime.Application.Planning;

public static class PlanningCandidateRejectionReasonDescriptions
{
    public static string ToPolishDescription(PlanningCandidateRejectionReason reason) => reason switch
    {
        PlanningCandidateRejectionReason.DriverNotFound => "Nie znaleziono kierowcy.",
        PlanningCandidateRejectionReason.DutyMissingStartOrEndTime => "Służba nie ma godziny rozpoczęcia albo zakończenia.",
        PlanningCandidateRejectionReason.AlreadyAssignedOnDate => "Kierowca ma już przydział w tym dniu.",
        PlanningCandidateRejectionReason.ManualAssignmentOnDate => "Kierowca ma ręczny przydział w tym dniu.",
        PlanningCandidateRejectionReason.Vacation => "Kierowca ma urlop lub inną nieobecność.",
        PlanningCandidateRejectionReason.SickLeave => "Kierowca jest na chorobowym.",
        PlanningCandidateRejectionReason.DayOff => "Kierowca ma dzień wolny.",
        PlanningCandidateRejectionReason.Unavailable => "Kierowca jest niedostępny.",
        PlanningCandidateRejectionReason.OverlappingAssignment => "Przedział pracy nakłada się na inny przydział.",
        PlanningCandidateRejectionReason.InsufficientDailyRestBefore => "Za krótki odpoczynek przed służbą.",
        PlanningCandidateRejectionReason.InsufficientDailyRestAfter => "Za krótki odpoczynek po służbie.",
        PlanningCandidateRejectionReason.TooManyConsecutiveWorkDays => "Przydział przekroczyłby limit kolejnych dni pracy.",
        PlanningCandidateRejectionReason.WeeklyWorkMinutesExceeded => "Przydział przekroczyłby tygodniowy limit pracy.",
        PlanningCandidateRejectionReason.MonthlyWorkMinutesExceeded => "Przydział przekroczyłby miesięczną normę przy włączonym twardym limicie.",
        PlanningCandidateRejectionReason.InsufficientWeeklyRest => "Kierowca nie zachowałby minimalnego odpoczynku tygodniowego.",
        PlanningCandidateRejectionReason.DriverDutyForbidden => "Kierowca ma zakaz tej służby.",
        _ => "Kierowca nie spełnia warunków kwalifikacji."
    };

    public static string ToPolishWeeklyRestWarning(PlanningWeeklyRestKind kind) => kind switch
    {
        PlanningWeeklyRestKind.Insufficient => "Odpoczynek tygodniowy jest krótszy niż minimalne 24 godziny.",
        PlanningWeeklyRestKind.Reduced => "Odpoczynek tygodniowy jest skrócony i krótszy niż 45 godzin.",
        PlanningWeeklyRestKind.PreferredReduced => "Odpoczynek tygodniowy jest skrócony, ale osiąga preferowany próg 35 godzin.",
        PlanningWeeklyRestKind.Regular => "Odpoczynek tygodniowy jest regularny i ma co najmniej 45 godzin.",
        _ => "Brak pełnej oceny odpoczynku tygodniowego."
    };
}



