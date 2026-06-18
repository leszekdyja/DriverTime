var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:47888");

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:3000",
                "https://drivetime.com.pl")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        mode = "mock",
        service = "DriverTime Card Reader Helper",
        message = "Helper działa lokalnie. Realny odczyt PC/SC zostanie dodany w kolejnym etapie.",
        checkedAtUtc = DateTime.UtcNow
    });
});

app.MapGet("/api/readers", () =>
{
    return Results.Ok(new
    {
        status = "mock",
        message = "Tryb bezpieczny MVP: fizyczne czytniki PC/SC nie są jeszcze odpytywane.",
        readers = Array.Empty<object>()
    });
});

app.MapPost("/api/card/read/start", () =>
{
    var startedAtUtc = DateTime.UtcNow;
    var completedAtUtc = startedAtUtc.AddSeconds(2);
    var fileName = $"mock-driver-card-{completedAtUtc:yyyyMMdd-HHmmss}.ddd";
    var filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DriverTime",
        "CardReaderHelper",
        "MockReads",
        fileName);

    return Results.Ok(new
    {
        status = "completed",
        message = "Wykonano testowy odczyt karty w trybie mock. Plik nie został utworzony ani wysłany do importu DDD.",
        startedAtUtc,
        completedAtUtc,
        fileName,
        filePath
    });
});

app.Run();
