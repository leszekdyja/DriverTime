using System.Runtime.InteropServices;
using System.Text;

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

builder.Services.AddSingleton<PcscReaderService>();

var app = builder.Build();

app.UseCors();

app.MapGet("/health", (PcscReaderService readerService) =>
{
    var snapshot = readerService.GetReaders();

    return Results.Ok(new
    {
        status = "ok",
        mode = "pcsc-detection",
        service = "DriverTime Card Reader Helper",
        pcscAvailable = snapshot.PcscAvailable,
        readersCount = snapshot.Readers.Count,
        message = snapshot.Message,
        checkedAtUtc = DateTime.UtcNow
    });
});

app.MapGet("/api/readers", (PcscReaderService readerService) =>
{
    var snapshot = readerService.GetReaders();

    return Results.Ok(new
    {
        status = snapshot.PcscAvailable ? "ok" : "unavailable",
        pcscAvailable = snapshot.PcscAvailable,
        message = snapshot.Message,
        readers = snapshot.Readers
    });
});

app.MapGet("/api/readers/{readerName}/atr", (string readerName, PcscReaderService readerService) =>
{
    var result = readerService.ReadAtr(Uri.UnescapeDataString(readerName));

    return Results.Ok(result);
});

app.MapPost("/api/card/read/start", (StartCardReadRequest? request) =>
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
        message = "Tryb testowy — realny odczyt karty zostanie dodany w następnym kroku.",
        selectedReaderName = request?.SelectedReaderName ?? string.Empty,
        startedAtUtc,
        completedAtUtc,
        fileName,
        filePath
    });
});

app.Run();

internal sealed class PcscReaderService
{
    public ReaderSnapshot GetReaders()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new ReaderSnapshot(
                PcscAvailable: false,
                Message: "PC/SC jest dostępne tylko na Windows w tym helperze MVP.",
                Readers: Array.Empty<ReaderInfo>());
        }

        var establishResult = NativeMethods.SCardEstablishContext(
            NativeMethods.ScardScopeUser,
            IntPtr.Zero,
            IntPtr.Zero,
            out var context);

        if (establishResult != NativeMethods.ScardSuccess)
        {
            return new ReaderSnapshot(
                PcscAvailable: false,
                Message: GetFriendlyError(establishResult),
                Readers: Array.Empty<ReaderInfo>());
        }

        try
        {
            var readerNames = ListReaderNames(context, out var listResult);

            if (listResult == NativeMethods.ScardNoReadersAvailable || readerNames.Count == 0)
            {
                return new ReaderSnapshot(
                    PcscAvailable: true,
                    Message: "Nie wykryto czytników kart inteligentnych. Podłącz czytnik i upewnij się, że usługa Windows Smart Card działa.",
                    Readers: Array.Empty<ReaderInfo>());
            }

            if (listResult != NativeMethods.ScardSuccess)
            {
                return new ReaderSnapshot(
                    PcscAvailable: false,
                    Message: GetFriendlyError(listResult),
                    Readers: Array.Empty<ReaderInfo>());
            }

            var readers = readerNames
                .Select(name => GetReaderStatus(context, name))
                .ToList();

            return new ReaderSnapshot(
                PcscAvailable: true,
                Message: $"Wykryto {readers.Count} czytników kart inteligentnych.",
                Readers: readers);
        }
        finally
        {
            NativeMethods.SCardReleaseContext(context);
        }
    }

    public AtrReadResult ReadAtr(string readerName)
    {
        if (string.IsNullOrWhiteSpace(readerName))
        {
            return AtrReadResult.Failure(
                readerName,
                cardPresent: false,
                status: "reader-name-required",
                errorMessage: "Nie podano nazwy czytnika.");
        }

        if (!OperatingSystem.IsWindows())
        {
            return AtrReadResult.Failure(
                readerName,
                cardPresent: false,
                status: "pcsc-unavailable",
                errorMessage: "PC/SC jest dostępne tylko na Windows w tym helperze MVP.");
        }

        var establishResult = NativeMethods.SCardEstablishContext(
            NativeMethods.ScardScopeUser,
            IntPtr.Zero,
            IntPtr.Zero,
            out var context);

        if (establishResult != NativeMethods.ScardSuccess)
        {
            return AtrReadResult.Failure(
                readerName,
                cardPresent: false,
                status: "pcsc-unavailable",
                errorMessage: GetFriendlyError(establishResult));
        }

        try
        {
            var readerNames = ListReaderNames(context, out var listResult);

            if (listResult != NativeMethods.ScardSuccess)
            {
                return AtrReadResult.Failure(
                    readerName,
                    cardPresent: false,
                    status: "readers-unavailable",
                    errorMessage: GetFriendlyError(listResult));
            }

            if (!readerNames.Contains(readerName, StringComparer.OrdinalIgnoreCase))
            {
                return AtrReadResult.Failure(
                    readerName,
                    cardPresent: false,
                    status: "reader-not-found",
                    errorMessage: "Nie znaleziono czytnika o podanej nazwie.");
            }

            var connectResult = NativeMethods.SCardConnect(
                context,
                readerName,
                NativeMethods.ScardShareShared,
                NativeMethods.ScardProtocolT0 | NativeMethods.ScardProtocolT1,
                out var card,
                out _);

            if (connectResult != NativeMethods.ScardSuccess)
            {
                return AtrReadResult.Failure(
                    readerName,
                    cardPresent: connectResult != NativeMethods.ScardNoSmartcard,
                    status: connectResult == NativeMethods.ScardNoSmartcard ? "card-not-present" : "connect-failed",
                    errorMessage: GetFriendlyError(connectResult));
            }

            try
            {
                var readerNameLength = 0;
                var atr = new byte[64];
                var atrLength = atr.Length;

                var statusResult = NativeMethods.SCardStatus(
                    card,
                    null,
                    ref readerNameLength,
                    out _,
                    out _,
                    atr,
                    ref atrLength);

                if (statusResult != NativeMethods.ScardSuccess)
                {
                    return AtrReadResult.Failure(
                        readerName,
                        cardPresent: true,
                        status: "atr-unavailable",
                        errorMessage: GetFriendlyError(statusResult));
                }

                if (atrLength <= 0)
                {
                    return AtrReadResult.Failure(
                        readerName,
                        cardPresent: true,
                        status: "atr-empty",
                        errorMessage: "Karta jest obecna, ale ATR jest pusty lub niedostępny.");
                }

                var atrBytes = atr.Take(atrLength).ToArray();
                var atrHex = string.Join(" ", atrBytes.Select(value => value.ToString("X2")));

                return new AtrReadResult(
                    ReaderName: readerName,
                    CardPresent: true,
                    AtrHex: atrHex,
                    AtrLength: atrLength,
                    Status: "ok",
                    ErrorMessage: string.Empty);
            }
            finally
            {
                NativeMethods.SCardDisconnect(card, NativeMethods.ScardLeaveCard);
            }
        }
        finally
        {
            NativeMethods.SCardReleaseContext(context);
        }
    }

    private static IReadOnlyList<string> ListReaderNames(
        IntPtr context,
        out uint result)
    {
        var length = 0;
        result = NativeMethods.SCardListReaders(
            context,
            null,
            null,
            ref length);

        if (result != NativeMethods.ScardSuccess)
        {
            return Array.Empty<string>();
        }

        var buffer = new StringBuilder(length);
        result = NativeMethods.SCardListReaders(
            context,
            null,
            buffer,
            ref length);

        if (result != NativeMethods.ScardSuccess)
        {
            return Array.Empty<string>();
        }

        return buffer
            .ToString()
            .Split('\0', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static ReaderInfo GetReaderStatus(
        IntPtr context,
        string readerName)
    {
        var state = new NativeMethods.ScardReaderState
        {
            ReaderName = readerName,
            CurrentState = NativeMethods.ScardStateUnaware,
            Atr = new byte[36]
        };

        var states = new[] { state };
        var result = NativeMethods.SCardGetStatusChange(
            context,
            timeoutMilliseconds: 0,
            states,
            states.Length);

        if (result != NativeMethods.ScardSuccess)
        {
            return new ReaderInfo(
                Name: readerName,
                CardPresent: null,
                Status: "unknown",
                Message: GetFriendlyError(result));
        }

        var eventState = states[0].EventState;
        var unavailable = (eventState & NativeMethods.ScardStateUnavailable) != 0;
        var present = (eventState & NativeMethods.ScardStatePresent) != 0;
        var empty = (eventState & NativeMethods.ScardStateEmpty) != 0;

        if (unavailable)
        {
            return new ReaderInfo(
                Name: readerName,
                CardPresent: null,
                Status: "unavailable",
                Message: "Czytnik jest niedostępny.");
        }

        if (present)
        {
            return new ReaderInfo(
                Name: readerName,
                CardPresent: true,
                Status: "card-present",
                Message: "Karta jest włożona do czytnika.");
        }

        if (empty)
        {
            return new ReaderInfo(
                Name: readerName,
                CardPresent: false,
                Status: "empty",
                Message: "Czytnik działa, ale karta nie jest włożona.");
        }

        return new ReaderInfo(
            Name: readerName,
            CardPresent: null,
            Status: "unknown",
            Message: "Nie udało się jednoznacznie ustalić obecności karty.");
    }

    private static string GetFriendlyError(uint errorCode)
    {
        return errorCode switch
        {
            NativeMethods.ScardNoService => "Usługa Windows Smart Card nie działa. Uruchom usługę „Karta inteligentna” i spróbuj ponownie.",
            NativeMethods.ScardServiceStopped => "Usługa Windows Smart Card jest zatrzymana.",
            NativeMethods.ScardNoReadersAvailable => "Nie wykryto czytników kart inteligentnych.",
            NativeMethods.ScardReaderUnavailable => "Czytnik kart jest niedostępny.",
            NativeMethods.ScardUnknownReader => "Nie znaleziono wskazanego czytnika.",
            NativeMethods.ScardNoSmartcard => "W wybranym czytniku nie ma karty.",
            NativeMethods.ScardSharingViolation => "Nie można połączyć się z kartą, bo jest używana przez inny proces.",
            _ => $"Podsystem PC/SC zwrócił błąd 0x{errorCode:X8}."
        };
    }
}

internal sealed record ReaderSnapshot(
    bool PcscAvailable,
    string Message,
    IReadOnlyList<ReaderInfo> Readers);

internal sealed record ReaderInfo(
    string Name,
    bool? CardPresent,
    string Status,
    string Message);

internal sealed record AtrReadResult(
    string ReaderName,
    bool CardPresent,
    string AtrHex,
    int AtrLength,
    string Status,
    string ErrorMessage)
{
    public static AtrReadResult Failure(
        string readerName,
        bool cardPresent,
        string status,
        string errorMessage)
    {
        return new AtrReadResult(
            ReaderName: readerName,
            CardPresent: cardPresent,
            AtrHex: string.Empty,
            AtrLength: 0,
            Status: status,
            ErrorMessage: errorMessage);
    }
}

internal sealed record StartCardReadRequest(
    string? SelectedReaderName);

internal static class NativeMethods
{
    public const uint ScardSuccess = 0x00000000;
    public const uint ScardNoService = 0x8010001Du;
    public const uint ScardServiceStopped = 0x8010001Eu;
    public const uint ScardNoReadersAvailable = 0x8010002Eu;
    public const uint ScardReaderUnavailable = 0x80100017u;
    public const uint ScardUnknownReader = 0x80100009u;
    public const uint ScardNoSmartcard = 0x8010000Cu;
    public const uint ScardSharingViolation = 0x8010000Bu;

    public const uint ScardScopeUser = 0;
    public const uint ScardShareShared = 2;
    public const uint ScardLeaveCard = 0;
    public const uint ScardProtocolT0 = 1;
    public const uint ScardProtocolT1 = 2;
    public const uint ScardStateUnaware = 0x00000000;
    public const uint ScardStateUnavailable = 0x00000008;
    public const uint ScardStateEmpty = 0x00000010;
    public const uint ScardStatePresent = 0x00000020;

    [DllImport("winscard.dll")]
    public static extern uint SCardEstablishContext(
        uint scope,
        IntPtr reserved1,
        IntPtr reserved2,
        out IntPtr context);

    [DllImport("winscard.dll")]
    public static extern uint SCardReleaseContext(
        IntPtr context);

    [DllImport("winscard.dll", CharSet = CharSet.Unicode)]
    public static extern uint SCardListReaders(
        IntPtr context,
        string? groups,
        StringBuilder? readers,
        ref int readersLength);

    [DllImport("winscard.dll", EntryPoint = "SCardGetStatusChangeW", CharSet = CharSet.Unicode)]
    public static extern uint SCardGetStatusChange(
        IntPtr context,
        uint timeoutMilliseconds,
        [In, Out] ScardReaderState[] readerStates,
        int readerCount);

    [DllImport("winscard.dll", EntryPoint = "SCardConnectW", CharSet = CharSet.Unicode)]
    public static extern uint SCardConnect(
        IntPtr context,
        string readerName,
        uint shareMode,
        uint preferredProtocols,
        out IntPtr card,
        out uint activeProtocol);

    [DllImport("winscard.dll")]
    public static extern uint SCardDisconnect(
        IntPtr card,
        uint disposition);

    [DllImport("winscard.dll", EntryPoint = "SCardStatusW", CharSet = CharSet.Unicode)]
    public static extern uint SCardStatus(
        IntPtr card,
        StringBuilder? readerNames,
        ref int readerNameLength,
        out uint state,
        out uint protocol,
        [Out] byte[] atr,
        ref int atrLength);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ScardReaderState
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string ReaderName;

        public IntPtr UserData;

        public uint CurrentState;

        public uint EventState;

        public uint AtrLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
        public byte[] Atr;
    }
}
