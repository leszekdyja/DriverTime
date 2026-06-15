namespace DriverTime.Application.Authentication;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Dispatcher = "Dispatcher";
    public const string Driver = "Driver";

    public static readonly string[] All = [Admin, Dispatcher, Driver];
}
