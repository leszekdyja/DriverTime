namespace DriverTime.Application.Planning;

public interface IPolishPublicHolidayProvider
{
    IReadOnlyCollection<PolishPublicHoliday> GetHolidays(int year);
}
