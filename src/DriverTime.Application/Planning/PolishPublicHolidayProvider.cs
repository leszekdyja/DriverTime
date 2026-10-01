namespace DriverTime.Application.Planning;

public class PolishPublicHolidayProvider : IPolishPublicHolidayProvider
{
    public IReadOnlyCollection<PolishPublicHoliday> GetHolidays(int year)
    {
        var easter = CalculateEasterSunday(year);
        return new List<PolishPublicHoliday>
        {
            new(new DateOnly(year, 1, 1), "Nowy Rok"),
            new(new DateOnly(year, 1, 6), "Trzech Króli"),
            new(easter.AddDays(1), "Poniedziałek Wielkanocny"),
            new(new DateOnly(year, 5, 1), "Święto Pracy"),
            new(new DateOnly(year, 5, 3), "Święto Konstytucji 3 Maja"),
            new(easter.AddDays(60), "Boże Ciało"),
            new(new DateOnly(year, 8, 15), "Wniebowzięcie Najświętszej Maryi Panny"),
            new(new DateOnly(year, 11, 1), "Wszystkich Świętych"),
            new(new DateOnly(year, 11, 11), "Narodowe Święto Niepodległości"),
            new(new DateOnly(year, 12, 25), "Boże Narodzenie - pierwszy dzień"),
            new(new DateOnly(year, 12, 26), "Boże Narodzenie - drugi dzień")
        };
    }

    private static DateOnly CalculateEasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(year, month, day);
    }
}
