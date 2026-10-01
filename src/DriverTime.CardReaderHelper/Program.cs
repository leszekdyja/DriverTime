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
    IReadOnlyList<ReaderInfo> readers = snapshot.Readers.Count == 0
        ? new[] { PcscReaderService.CreateMockReader() }
        : snapshot.Readers;

    return Results.Ok(new
    {
        status = snapshot.PcscAvailable ? "ok" : "unavailable",
        pcscAvailable = snapshot.PcscAvailable,
        message = snapshot.Readers.Count == 0
            ? $"{snapshot.Message} Możesz użyć czytnika testowego, aby sprawdzić przepływ UI bez urządzenia."
            : snapshot.Message,
        mockModeAvailable = snapshot.Readers.Count == 0,
        readers
    });
});

app.MapGet("/api/diagnostics", (PcscReaderService readerService) =>
{
    return Results.Ok(readerService.GetDiagnostics());
});

app.MapGet("/api/readers/{readerName}/atr", (string readerName, PcscReaderService readerService) =>
{
    var result = readerService.ReadAtr(Uri.UnescapeDataString(readerName));

    return Results.Ok(result);
});

app.MapGet("/api/readers/{readerName}/status", (string readerName, PcscReaderService readerService) =>
{
    var result = readerService.GetReaderConnectionStatus(Uri.UnescapeDataString(readerName));

    return Results.Ok(result);
});

app.MapGet("/api/reader-status", (string? readerName, PcscReaderService readerService) =>
{
    var result = readerService.GetReaderConnectionStatus(readerName);

    return Results.Ok(result);
});

app.MapPost("/api/card/read/start", (StartCardReadRequest? request) =>
{
    var startedAtUtc = DateTime.UtcNow;
    var completedAtUtc = startedAtUtc.AddSeconds(2);
    var selectedReaderName = string.IsNullOrWhiteSpace(request?.GetRequestedReaderName())
        ? PcscReaderService.MockReaderName
        : request.GetRequestedReaderName()!;
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
        selectedReaderName,
        mockMode = true,
        startedAtUtc,
        completedAtUtc,
        fileName,
        filePath
    });
});

app.MapPost("/api/card/read/technical", (StartCardReadRequest? request, PcscReaderService readerService) =>
{
    var result = readerService.ReadCardTechnical(request?.GetRequestedReaderName());

    return Results.Ok(result);
});

app.MapPost("/api/card/read/tachograph-structure", (StartCardReadRequest? request, PcscReaderService readerService) =>
{
    var result = readerService.ReadTachographStructure(request?.GetRequestedReaderName());

    return Results.Ok(result);
});

app.Run();

internal sealed class PcscReaderService
{
    public const string MockReaderName = "DriverTime czytnik testowy";

    public static ReaderInfo CreateMockReader()
    {
        return new ReaderInfo(
            Name: MockReaderName,
            CardPresent: false,
            Status: "mock",
            Message: "Tryb testowy bez fizycznego czytnika. Służy tylko do sprawdzenia UI i historii sesji.",
            ErrorCode: null,
            ErrorCodeHex: string.Empty,
            IsMock: true);
    }

    public ReaderConnectionStatus GetReaderConnectionStatus(string? readerName)
    {
        if (string.Equals(readerName, MockReaderName, StringComparison.OrdinalIgnoreCase))
        {
            return ReaderConnectionStatus.Mock();
        }

        var snapshot = GetReaders();
        var selectedReader = string.IsNullOrWhiteSpace(readerName)
            ? snapshot.Readers.FirstOrDefault()
            : snapshot.Readers.FirstOrDefault(x =>
                string.Equals(x.Name, readerName, StringComparison.OrdinalIgnoreCase));

        if (selectedReader is null)
        {
            return snapshot.Readers.Count == 0
                ? ReaderConnectionStatus.NoReader(snapshot.Message)
                : ReaderConnectionStatus.NoReader("Nie znaleziono wskazanego czytnika.");
        }

        if (selectedReader.CardPresent != true)
        {
            return new ReaderConnectionStatus(
                ReaderName: selectedReader.Name,
                ReaderConnected: true,
                CardPresent: selectedReader.CardPresent == true,
                Atr: string.Empty,
                AtrLength: 0,
                Status: selectedReader.Status,
                Message: selectedReader.Message,
                ErrorMessage: selectedReader.ErrorCodeHex,
                IsMockReader: selectedReader.IsMock,
                CheckedAtUtc: DateTime.UtcNow);
        }

        var atr = ReadAtr(selectedReader.Name);

        return new ReaderConnectionStatus(
            ReaderName: selectedReader.Name,
            ReaderConnected: atr.Connected,
            CardPresent: atr.CardPresent,
            Atr: atr.AtrHex,
            AtrLength: atr.AtrLength,
            Status: atr.Status,
            Message: atr.ErrorMessage.Length == 0
                ? "Karta jest włożona i ATR został odczytany."
                : atr.ErrorMessage,
            ErrorMessage: atr.ErrorCodeHex,
            IsMockReader: selectedReader.IsMock,
            CheckedAtUtc: DateTime.UtcNow);
    }

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

    public PcscDiagnostics GetDiagnostics()
    {
        var snapshot = GetReaders();
        var readerDiagnostics = new List<ReaderDiagnostic>();

        foreach (var reader in snapshot.Readers)
        {
            AtrReadResult? atr = null;

            if (reader.CardPresent == true)
            {
                atr = ReadAtr(reader.Name);
            }

            readerDiagnostics.Add(new ReaderDiagnostic(
                Name: reader.Name,
                CardPresent: reader.CardPresent,
                Status: reader.Status,
                Message: reader.Message,
                AtrHex: atr?.AtrHex ?? string.Empty,
                AtrLength: atr?.AtrLength ?? 0,
                Connected: atr?.Connected ?? false,
                Protocol: atr?.Protocol ?? string.Empty,
                ErrorMessage: atr?.ErrorMessage ?? reader.Message,
                ErrorCodeHex: atr?.ErrorCodeHex ?? reader.ErrorCodeHex));
        }

        var lastError = !snapshot.PcscAvailable
            ? snapshot.Message
            : readerDiagnostics.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.ErrorCodeHex))?.ErrorMessage ?? string.Empty;

        return new PcscDiagnostics(
            Status: snapshot.PcscAvailable ? "ok" : "unavailable",
            PcscAvailable: snapshot.PcscAvailable,
            Message: snapshot.Message,
            CheckedAtUtc: DateTime.UtcNow,
            ReadersCount: snapshot.Readers.Count,
            Readers: readerDiagnostics,
            LastError: lastError);
    }

    public AtrReadResult ReadAtr(string readerName)
    {
        if (string.IsNullOrWhiteSpace(readerName))
        {
            return AtrReadResult.Failure(
                readerName,
                cardPresent: false,
                connected: false,
                status: "reader-name-required",
                errorMessage: "Nie podano nazwy czytnika.");
        }

        if (!OperatingSystem.IsWindows())
        {
            return AtrReadResult.Failure(
                readerName,
                cardPresent: false,
                connected: false,
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
                connected: false,
                status: "pcsc-unavailable",
                errorMessage: GetFriendlyError(establishResult),
                errorCode: establishResult);
        }

        try
        {
            var readerNames = ListReaderNames(context, out var listResult);

            if (listResult != NativeMethods.ScardSuccess)
            {
                return AtrReadResult.Failure(
                    readerName,
                    cardPresent: false,
                    connected: false,
                    status: "readers-unavailable",
                    errorMessage: GetFriendlyError(listResult),
                    errorCode: listResult);
            }

            if (!readerNames.Contains(readerName, StringComparer.OrdinalIgnoreCase))
            {
                return AtrReadResult.Failure(
                    readerName,
                    cardPresent: false,
                    connected: false,
                    status: "reader-not-found",
                    errorMessage: "Nie znaleziono czytnika o podanej nazwie.",
                    errorCode: NativeMethods.ScardUnknownReader);
            }

            var connectResult = NativeMethods.SCardConnect(
                context,
                readerName,
                NativeMethods.ScardShareShared,
                NativeMethods.ScardProtocolT0 | NativeMethods.ScardProtocolT1,
                out var card,
                out var activeProtocol);

            if (connectResult != NativeMethods.ScardSuccess)
            {
                return AtrReadResult.Failure(
                    readerName,
                    cardPresent: connectResult != NativeMethods.ScardNoSmartcard,
                    connected: false,
                    status: connectResult == NativeMethods.ScardNoSmartcard ? "card-not-present" : "connect-failed",
                    errorMessage: GetFriendlyError(connectResult),
                    errorCode: connectResult);
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
                    out var state,
                    out var protocol,
                    atr,
                    ref atrLength);

                if (statusResult != NativeMethods.ScardSuccess)
                {
                    return AtrReadResult.Failure(
                        readerName,
                        cardPresent: true,
                        connected: true,
                        status: "atr-unavailable",
                        errorMessage: GetFriendlyError(statusResult),
                        errorCode: statusResult,
                        activeProtocol: activeProtocol);
                }

                if (atrLength <= 0)
                {
                    return AtrReadResult.Failure(
                        readerName,
                        cardPresent: true,
                        connected: true,
                        status: "atr-empty",
                        errorMessage: "Karta jest obecna, ale ATR jest pusty lub niedostępny.",
                        activeProtocol: activeProtocol);
                }

                var atrBytes = atr.Take(atrLength).ToArray();
                var atrHex = string.Join(" ", atrBytes.Select(value => value.ToString("X2")));
                var resolvedProtocol = protocol == 0 ? activeProtocol : protocol;

                return new AtrReadResult(
                    ReaderName: readerName,
                    CardPresent: true,
                    Connected: true,
                    AtrHex: atrHex,
                    AtrLength: atrLength,
                    Status: "ok",
                    ErrorMessage: string.Empty,
                    ErrorCode: null,
                    ErrorCodeHex: string.Empty,
                    ActiveProtocol: resolvedProtocol,
                    Protocol: GetProtocolName(resolvedProtocol),
                    PcscState: state);
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

    public CardTechnicalReadResult ReadCardTechnical(string? selectedReaderName)
    {
        var startedAtUtc = DateTime.UtcNow;
        var requestedReaderName = string.IsNullOrWhiteSpace(selectedReaderName)
            ? string.Empty
            : selectedReaderName.Trim();
        var readerName = requestedReaderName;

        if (IsMockReaderName(requestedReaderName))
        {
            return WriteTechnicalReadFile(new CardTechnicalReadPayload(
                Success: true,
                Message: "Tryb testowy - zapisano techniczny wynik odczytu bez fizycznej karty.",
                RequestedReaderName: requestedReaderName,
                SelectedReaderName: MockReaderName,
                SelectedReaderIsMock: true,
                ReaderName: MockReaderName,
                Atr: string.Empty,
                StartedAtUtc: startedAtUtc,
                FinishedAtUtc: DateTime.UtcNow,
                IsMock: true,
                ApduResponses: new[]
                {
                    new CardApduResponse(
                        Name: "MOCK",
                        CommandHex: string.Empty,
                        ResponseHex: string.Empty,
                        StatusWord: "9000",
                        Success: true,
                        Message: "Symulowany odczyt techniczny.")
                },
                ErrorDetails: string.Empty,
                ExportReadiness: CardExportReadiness.TechnicalJson()));
        }

        if (!OperatingSystem.IsWindows())
        {
            return CardTechnicalReadResult.Failure(
                readerName,
                startedAtUtc,
                "PC/SC jest dostępne tylko na Windows w tym helperze MVP.",
                "pcsc-unavailable");
        }

        var establishResult = NativeMethods.SCardEstablishContext(
            NativeMethods.ScardScopeUser,
            IntPtr.Zero,
            IntPtr.Zero,
            out var context);

        if (establishResult != NativeMethods.ScardSuccess)
        {
            return CardTechnicalReadResult.Failure(
                readerName,
                startedAtUtc,
                GetFriendlyError(establishResult),
                ToHex(establishResult));
        }

        try
        {
            var readerNames = ListReaderNames(context, out var listResult);

            if (listResult != NativeMethods.ScardSuccess)
            {
                return CardTechnicalReadResult.Failure(
                    readerName,
                    startedAtUtc,
                    GetFriendlyError(listResult),
                    ToHex(listResult));
            }

            if (string.IsNullOrWhiteSpace(requestedReaderName) && readerNames.Count == 0)
            {
                return WriteTechnicalReadFile(new CardTechnicalReadPayload(
                    Success: true,
                    Message: "Tryb testowy - zapisano techniczny wynik odczytu bez fizycznej karty.",
                    RequestedReaderName: string.Empty,
                    SelectedReaderName: MockReaderName,
                    SelectedReaderIsMock: true,
                    ReaderName: MockReaderName,
                    Atr: string.Empty,
                    StartedAtUtc: startedAtUtc,
                    FinishedAtUtc: DateTime.UtcNow,
                    IsMock: true,
                    ApduResponses: new[]
                    {
                        new CardApduResponse(
                            Name: "MOCK",
                            CommandHex: string.Empty,
                            ResponseHex: string.Empty,
                            StatusWord: "9000",
                            Success: true,
                            Message: "Symulowany odczyt techniczny.")
                    },
                    ErrorDetails: string.Empty,
                    ExportReadiness: CardExportReadiness.TechnicalJson()));
            }

            readerName = string.IsNullOrWhiteSpace(requestedReaderName)
                ? readerNames.FirstOrDefault() ?? string.Empty
                : requestedReaderName;

            if (!readerNames.Contains(readerName, StringComparer.OrdinalIgnoreCase))
            {
                return CardTechnicalReadResult.Failure(
                    readerName,
                    startedAtUtc,
                    "Nie znaleziono czytnika o podanej nazwie.",
                    ToHex(NativeMethods.ScardUnknownReader));
            }

            var connectResult = NativeMethods.SCardConnect(
                context,
                readerName,
                NativeMethods.ScardShareShared,
                NativeMethods.ScardProtocolT0 | NativeMethods.ScardProtocolT1,
                out var card,
                out var activeProtocol);

            if (connectResult != NativeMethods.ScardSuccess)
            {
                return CardTechnicalReadResult.Failure(
                    readerName,
                    startedAtUtc,
                    GetFriendlyError(connectResult),
                    ToHex(connectResult));
            }

            try
            {
                var atr = ReadAtrFromConnectedCard(card, activeProtocol, out var statusError);
                if (!string.IsNullOrWhiteSpace(statusError))
                {
                    return CardTechnicalReadResult.Failure(
                        readerName,
                        startedAtUtc,
                        statusError,
                        "atr-unavailable");
                }

                var apduResponses = new List<CardApduResponse>
                {
                    TransmitApdu(card, activeProtocol, "SELECT MF", new byte[] { 0x00, 0xA4, 0x00, 0x0C, 0x02, 0x3F, 0x00 }),
                    TransmitApdu(card, activeProtocol, "SELECT tachograph application by AID", new byte[] { 0x00, 0xA4, 0x04, 0x0C, 0x06, 0xFF, 0x54, 0x41, 0x43, 0x48, 0x4F })
                };

                var anySuccessfulApdu = apduResponses.Any(x => x.Success);
                var message = anySuccessfulApdu
                    ? "Karta wykryta, odczyt techniczny wykonany. Pełny eksport C1B/DDD wymaga kolejnego kroku implementacji APDU."
                    : "Karta wykryta i ATR odczytany, ale podstawowe komendy SELECT nie zwróciły sukcesu. Pełny eksport C1B/DDD wymaga kolejnego kroku implementacji APDU.";

                return WriteTechnicalReadFile(new CardTechnicalReadPayload(
                    Success: true,
                    Message: message,
                    RequestedReaderName: requestedReaderName,
                    SelectedReaderName: readerName,
                    SelectedReaderIsMock: false,
                    ReaderName: readerName,
                    Atr: atr,
                    StartedAtUtc: startedAtUtc,
                    FinishedAtUtc: DateTime.UtcNow,
                    IsMock: false,
                    ApduResponses: apduResponses,
                    ErrorDetails: string.Empty,
                    ExportReadiness: CardExportReadiness.TechnicalJson()));
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

    public TachographCardReadResult ReadTachographStructure(string? selectedReaderName)
    {
        var startedAtUtc = DateTime.UtcNow;
        var requestedReaderName = string.IsNullOrWhiteSpace(selectedReaderName)
            ? string.Empty
            : selectedReaderName.Trim();
        var readerName = requestedReaderName;

        if (IsMockReaderName(requestedReaderName))
        {
            return WriteTachographStructureFile(new TachographCardReadPayload(
                Success: true,
                RequestedReaderName: requestedReaderName,
                SelectedReaderName: MockReaderName,
                SelectedReaderIsMock: true,
                Message: "Tryb testowy - zapisano przykładowy wynik odczytu struktury bez fizycznej karty.",
                ReaderName: MockReaderName,
                Atr: string.Empty,
                StartedAtUtc: startedAtUtc,
                FinishedAtUtc: DateTime.UtcNow,
                IsMock: true,
                ApduResponses: new[]
                {
                    ApduResponse.Mock("SELECT MF", "00 A4 00 0C 02 3F 00"),
                    ApduResponse.Mock("SELECT tachograph application by AID", "00 A4 04 0C 06 FF 54 41 43 48 4F")
                },
                FileReads: new[]
                {
                    CardFileReadResult.Mock("EF Application Identification", "0501")
                },
                ErrorDetails: string.Empty,
                ExportReadiness: CardExportReadiness.TachographStructure()));
        }

        if (!OperatingSystem.IsWindows())
        {
            return TachographCardReadResult.Failure(
                readerName,
                startedAtUtc,
                "PC/SC jest dostępne tylko na Windows w tym helperze MVP.",
                "pcsc-unavailable");
        }

        var establishResult = NativeMethods.SCardEstablishContext(
            NativeMethods.ScardScopeUser,
            IntPtr.Zero,
            IntPtr.Zero,
            out var context);

        if (establishResult != NativeMethods.ScardSuccess)
        {
            return TachographCardReadResult.Failure(
                readerName,
                startedAtUtc,
                GetFriendlyError(establishResult),
                ToHex(establishResult));
        }

        try
        {
            var readerNames = ListReaderNames(context, out var listResult);

            if (listResult != NativeMethods.ScardSuccess)
            {
                return TachographCardReadResult.Failure(
                    readerName,
                    startedAtUtc,
                    GetFriendlyError(listResult),
                    ToHex(listResult));
            }

            if (string.IsNullOrWhiteSpace(requestedReaderName) && readerNames.Count == 0)
            {
                return WriteTachographStructureFile(new TachographCardReadPayload(
                    Success: true,
                    RequestedReaderName: string.Empty,
                    SelectedReaderName: MockReaderName,
                    SelectedReaderIsMock: true,
                    Message: "Tryb testowy - zapisano przykładowy wynik odczytu struktury bez fizycznej karty.",
                    ReaderName: MockReaderName,
                    Atr: string.Empty,
                    StartedAtUtc: startedAtUtc,
                    FinishedAtUtc: DateTime.UtcNow,
                    IsMock: true,
                    ApduResponses: new[]
                    {
                        ApduResponse.Mock("SELECT MF", "00 A4 00 0C 02 3F 00"),
                        ApduResponse.Mock("SELECT tachograph application by AID", "00 A4 04 0C 06 FF 54 41 43 48 4F")
                    },
                    FileReads: new[]
                    {
                        CardFileReadResult.Mock("EF Application Identification", "0501")
                    },
                    ErrorDetails: string.Empty,
                    ExportReadiness: CardExportReadiness.TachographStructure()));
            }

            readerName = string.IsNullOrWhiteSpace(requestedReaderName)
                ? readerNames.FirstOrDefault() ?? string.Empty
                : requestedReaderName;

            if (!readerNames.Contains(readerName, StringComparer.OrdinalIgnoreCase))
            {
                return TachographCardReadResult.Failure(
                    readerName,
                    startedAtUtc,
                    "Nie znaleziono czytnika o podanej nazwie.",
                    ToHex(NativeMethods.ScardUnknownReader));
            }

            var connectResult = NativeMethods.SCardConnect(
                context,
                readerName,
                NativeMethods.ScardShareShared,
                NativeMethods.ScardProtocolT0 | NativeMethods.ScardProtocolT1,
                out var card,
                out var activeProtocol);

            if (connectResult != NativeMethods.ScardSuccess)
            {
                return TachographCardReadResult.Failure(
                    readerName,
                    startedAtUtc,
                    GetFriendlyError(connectResult),
                    ToHex(connectResult));
            }

            try
            {
                var atr = ReadAtrFromConnectedCard(card, activeProtocol, out var statusError);
                if (!string.IsNullOrWhiteSpace(statusError))
                {
                    return TachographCardReadResult.Failure(
                        readerName,
                        startedAtUtc,
                        statusError,
                        "atr-unavailable");
                }

                var apduResponses = new List<ApduResponse>
                {
                    TransmitStructuredApdu(card, activeProtocol, ApduCommand.SelectFile("SELECT MF", "3F00")),
                    TransmitStructuredApdu(card, activeProtocol, ApduCommand.SelectApplicationByAid("SELECT tachograph application by AID", "FF544143484F")),
                    TransmitStructuredApdu(card, activeProtocol, ApduCommand.SelectFile("SELECT DF Tachograph candidate", "0500"))
                };

                var fileReads = new List<CardFileReadResult>();
                foreach (var file in GetTachographFileCandidates())
                {
                    fileReads.Add(ReadCandidateFile(card, activeProtocol, file));
                }

                var selectedFiles = fileReads.Count(x => x.SelectSucceeded);
                var readFiles = fileReads.Count(x => x.ReadSucceeded);
                var message = readFiles > 0
                    ? $"Odczyt struktury wykonany. Wybrano {selectedFiles} plików, odczytano {readFiles} próbnych fragmentów."
                    : $"Odczyt struktury wykonany. Wybrano {selectedFiles} plików, ale nie odczytano danych EF bez dalszej implementacji APDU/specyfikacji.";

                return WriteTachographStructureFile(new TachographCardReadPayload(
                    Success: true,
                    Message: message,
                    RequestedReaderName: requestedReaderName,
                    SelectedReaderName: readerName,
                    SelectedReaderIsMock: false,
                    ReaderName: readerName,
                    Atr: atr,
                    StartedAtUtc: startedAtUtc,
                    FinishedAtUtc: DateTime.UtcNow,
                    IsMock: false,
                    ApduResponses: apduResponses,
                    FileReads: fileReads,
                    ErrorDetails: string.Empty,
                    ExportReadiness: CardExportReadiness.TachographStructure()));
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

    private static CardTechnicalReadResult WriteTechnicalReadFile(CardTechnicalReadPayload payload)
    {
        var outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DriverTime",
            "CardReaderHelper",
            "TechnicalReads");
        Directory.CreateDirectory(outputDirectory);

        var safeReaderName = SanitizeFileName(payload.ReaderName);
        var outputFileName = $"driver-card-technical-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{safeReaderName}.json";
        var outputPath = Path.Combine(outputDirectory, outputFileName);
        var json = System.Text.Json.JsonSerializer.Serialize(
            payload,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(outputPath, json, Encoding.UTF8);

        var fileSizeBytes = new FileInfo(outputPath).Length;

        return new CardTechnicalReadResult(
            Success: payload.Success,
            Message: payload.Message,
            RequestedReaderName: payload.RequestedReaderName,
            SelectedReaderName: payload.SelectedReaderName,
            SelectedReaderIsMock: payload.SelectedReaderIsMock,
            ReaderName: payload.ReaderName,
            Atr: payload.Atr,
            OutputFileName: outputFileName,
            OutputPath: outputPath,
            FileSizeBytes: fileSizeBytes,
            StartedAtUtc: payload.StartedAtUtc,
            FinishedAtUtc: payload.FinishedAtUtc,
            ErrorDetails: payload.ErrorDetails,
            IsMock: payload.IsMock,
            FullDddExportReady: false,
            IsImportable: payload.ExportReadiness.IsImportable,
            ExportFormat: payload.ExportReadiness.ExportFormat,
            NextStepMessage: payload.ExportReadiness.NextStepMessage,
            ApduResponses: payload.ApduResponses);
    }

    private static TachographCardReadResult WriteTachographStructureFile(TachographCardReadPayload payload)
    {
        var outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DriverTime",
            "CardReaderHelper",
            "TachographStructureReads");
        Directory.CreateDirectory(outputDirectory);

        var safeReaderName = SanitizeFileName(payload.ReaderName);
        var outputFileName = $"driver-card-structure-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{safeReaderName}.json";
        var outputPath = Path.Combine(outputDirectory, outputFileName);
        var json = System.Text.Json.JsonSerializer.Serialize(
            payload,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(outputPath, json, Encoding.UTF8);

        var fileSizeBytes = new FileInfo(outputPath).Length;

        return new TachographCardReadResult(
            Success: payload.Success,
            Message: payload.Message,
            RequestedReaderName: payload.RequestedReaderName,
            SelectedReaderName: payload.SelectedReaderName,
            SelectedReaderIsMock: payload.SelectedReaderIsMock,
            ReaderName: payload.ReaderName,
            Atr: payload.Atr,
            OutputFileName: outputFileName,
            OutputPath: outputPath,
            FileSizeBytes: fileSizeBytes,
            StartedAtUtc: payload.StartedAtUtc,
            FinishedAtUtc: payload.FinishedAtUtc,
            ErrorDetails: payload.ErrorDetails,
            IsMock: payload.IsMock,
            IsImportable: payload.ExportReadiness.IsImportable,
            ExportFormat: payload.ExportReadiness.ExportFormat,
            NextStepMessage: payload.ExportReadiness.NextStepMessage,
            ApduResponses: payload.ApduResponses,
            FileReads: payload.FileReads);
    }

    private static string ReadAtrFromConnectedCard(
        IntPtr card,
        uint activeProtocol,
        out string errorMessage)
    {
        var readerNameLength = 0;
        var atr = new byte[64];
        var atrLength = atr.Length;
        var statusResult = NativeMethods.SCardStatus(
            card,
            null,
            ref readerNameLength,
            out _,
            out var protocol,
            atr,
            ref atrLength);

        if (statusResult != NativeMethods.ScardSuccess)
        {
            errorMessage = GetFriendlyError(statusResult);
            return string.Empty;
        }

        if (atrLength <= 0)
        {
            errorMessage = "Karta jest obecna, ale ATR jest pusty lub niedostępny.";
            return string.Empty;
        }

        errorMessage = string.Empty;
        _ = protocol == 0 ? activeProtocol : protocol;
        return ToHex(atr.Take(atrLength));
    }

    private static CardApduResponse TransmitApdu(
        IntPtr card,
        uint activeProtocol,
        string name,
        byte[] command)
    {
        var ioRequest = new NativeMethods.ScardIoRequest
        {
            Protocol = activeProtocol,
            PciLength = Marshal.SizeOf<NativeMethods.ScardIoRequest>()
        };
        var receiveBuffer = new byte[258];
        var receiveLength = receiveBuffer.Length;
        var result = NativeMethods.SCardTransmit(
            card,
            ref ioRequest,
            command,
            command.Length,
            IntPtr.Zero,
            receiveBuffer,
            ref receiveLength);

        if (result != NativeMethods.ScardSuccess)
        {
            return new CardApduResponse(
                Name: name,
                CommandHex: ToHex(command),
                ResponseHex: string.Empty,
                StatusWord: string.Empty,
                Success: false,
                Message: GetFriendlyError(result));
        }

        var response = receiveBuffer.Take(receiveLength).ToArray();
        var statusWord = response.Length >= 2
            ? $"{response[^2]:X2}{response[^1]:X2}"
            : string.Empty;
        var success = statusWord == "9000" || statusWord.StartsWith("61", StringComparison.Ordinal);

        return new CardApduResponse(
            Name: name,
            CommandHex: ToHex(command),
            ResponseHex: ToHex(response),
            StatusWord: statusWord,
            Success: success,
            Message: success ? "Komenda APDU wykonana poprawnie." : $"Karta zwróciła status {statusWord}.");
    }

    private static CardFileReadResult ReadCandidateFile(
        IntPtr card,
        uint activeProtocol,
        TachographFileCandidate file)
    {
        var selectResponse = TransmitStructuredApdu(
            card,
            activeProtocol,
            ApduCommand.SelectEfByFileIdentifier($"SELECT EF {file.Name}", file.FileId));

        ApduResponse? readResponse = null;
        if (selectResponse.Success)
        {
            readResponse = TransmitStructuredApdu(
                card,
                activeProtocol,
                ApduCommand.ReadBinary($"READ {file.Name} first bytes", file.MaxReadBytes));
        }

        var message = selectResponse.Success
            ? readResponse?.Success == true
                ? "Plik wybrany i odczytano próbny fragment."
                : "Plik wybrany, ale próbny odczyt nie zwrócił danych użytkowych."
            : $"Nie udało się wybrać pliku: {selectResponse.StatusMeaning}.";

        return new CardFileReadResult(
            FileName: file.Name,
            FileId: file.FileId,
            Description: file.Description,
            SelectResponse: selectResponse,
            ReadResponse: readResponse,
            SelectSucceeded: selectResponse.Success,
            ReadSucceeded: readResponse?.Success == true,
            Message: message);
    }

    private static ApduResponse TransmitStructuredApdu(
        IntPtr card,
        uint activeProtocol,
        ApduCommand command)
    {
        var ioRequest = new NativeMethods.ScardIoRequest
        {
            Protocol = activeProtocol,
            PciLength = Marshal.SizeOf<NativeMethods.ScardIoRequest>()
        };
        var receiveBuffer = new byte[258];
        var receiveLength = receiveBuffer.Length;
        var commandBytes = FromHex(command.CommandHex);
        var result = NativeMethods.SCardTransmit(
            card,
            ref ioRequest,
            commandBytes,
            commandBytes.Length,
            IntPtr.Zero,
            receiveBuffer,
            ref receiveLength);

        if (result != NativeMethods.ScardSuccess)
        {
            var error = GetFriendlyError(result);
            return new ApduResponse(
                Name: command.Name,
                Description: command.Description,
                CommandHex: command.CommandHex,
                ResponseHex: string.Empty,
                DataHex: string.Empty,
                StatusWord: string.Empty,
                Sw1: string.Empty,
                Sw2: string.Empty,
                Success: false,
                StatusMeaning: error,
                Message: error);
        }

        var response = receiveBuffer.Take(receiveLength).ToArray();
        var data = response.Length > 2
            ? response.Take(response.Length - 2).ToArray()
            : Array.Empty<byte>();
        var statusWord = response.Length >= 2
            ? $"{response[^2]:X2}{response[^1]:X2}"
            : string.Empty;
        var sw1 = statusWord.Length >= 2 ? statusWord[..2] : string.Empty;
        var sw2 = statusWord.Length >= 4 ? statusWord[2..4] : string.Empty;
        var success = statusWord == "9000" || statusWord.StartsWith("61", StringComparison.Ordinal);
        var meaning = InterpretStatusWord(statusWord);

        return new ApduResponse(
            Name: command.Name,
            Description: command.Description,
            CommandHex: command.CommandHex,
            ResponseHex: ToHex(response),
            DataHex: ToHex(data),
            StatusWord: statusWord,
            Sw1: sw1,
            Sw2: sw2,
            Success: success,
            StatusMeaning: meaning,
            Message: success ? "Komenda APDU wykonana poprawnie." : meaning);
    }

    private static IReadOnlyList<TachographFileCandidate> GetTachographFileCandidates()
    {
        return new[]
        {
            new TachographFileCandidate("EF ICC candidate", "0002", "Kandydat pliku ICC. Wymaga potwierdzenia ze specyfikacją karty.", 0x40),
            new TachographFileCandidate("EF IC candidate", "0005", "Kandydat pliku IC. Wymaga potwierdzenia ze specyfikacją karty.", 0x40),
            new TachographFileCandidate("EF Application Identification candidate", "0501", "Kandydat podstawowej identyfikacji aplikacji tachografu.", 0x40),
            new TachographFileCandidate("EF Identification candidate", "0520", "Kandydat danych identyfikacyjnych karty kierowcy.", 0x40),
            new TachographFileCandidate("EF Card Download candidate", "050E", "Kandydat danych pobrania karty. Może wymagać uprawnień lub innej ścieżki APDU.", 0x40)
        };
    }

    private static string InterpretStatusWord(string statusWord)
    {
        return statusWord switch
        {
            "9000" => "OK",
            "6282" => "Koniec pliku lub ostrzeżenie: odczyt może być niepełny.",
            "6700" => "Nieprawidłowa długość komendy.",
            "6982" => "Security status not satisfied - karta wymaga spełnienia warunku bezpieczeństwa.",
            "6985" => "Conditions of use not satisfied - warunki użycia komendy nie są spełnione.",
            "6A82" => "File not found - plik nie istnieje pod podanym identyfikatorem.",
            "6A86" => "Incorrect P1/P2 - nieprawidłowe parametry komendy.",
            "6D00" => "Instruction code not supported - komenda nie jest obsługiwana.",
            "6E00" => "Class not supported - klasa komendy nie jest obsługiwana.",
            _ when statusWord.StartsWith("61", StringComparison.Ordinal) => "Dostępne są kolejne bajty odpowiedzi.",
            _ when statusWord.StartsWith("6C", StringComparison.Ordinal) => "Karta oczekuje innej długości Le.",
            "" => "Brak statusu SW1/SW2.",
            _ => $"Nieobsłużony status karty {statusWord}."
        };
    }

    private static byte[] FromHex(string value)
    {
        var compact = new string(value.Where(Uri.IsHexDigit).ToArray());
        if (compact.Length % 2 != 0)
        {
            compact = $"0{compact}";
        }

        var bytes = new byte[compact.Length / 2];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = Convert.ToByte(compact.Substring(index * 2, 2), 16);
        }

        return bytes;
    }

    private static bool IsMockReaderName(string? readerName)
    {
        return string.Equals(readerName, MockReaderName, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Where(character => !invalidChars.Contains(character))
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized)
            ? "reader"
            : sanitized.Trim().Replace(' ', '-');
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
                Message: GetFriendlyError(result),
                ErrorCode: result,
                ErrorCodeHex: ToHex(result));
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
                Message: "Czytnik jest niedostępny.",
                ErrorCode: NativeMethods.ScardReaderUnavailable,
                ErrorCodeHex: ToHex(NativeMethods.ScardReaderUnavailable));
        }

        if (present)
        {
            return new ReaderInfo(
                Name: readerName,
                CardPresent: true,
                Status: "card-present",
                Message: "Karta jest włożona do czytnika.",
                ErrorCode: null,
                ErrorCodeHex: string.Empty);
        }

        if (empty)
        {
            return new ReaderInfo(
                Name: readerName,
                CardPresent: false,
                Status: "empty",
                Message: "Czytnik działa, ale karta nie jest włożona.",
                ErrorCode: null,
                ErrorCodeHex: string.Empty);
        }

        return new ReaderInfo(
            Name: readerName,
            CardPresent: null,
            Status: "unknown",
            Message: "Nie udało się jednoznacznie ustalić obecności karty.",
            ErrorCode: null,
            ErrorCodeHex: string.Empty);
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
            _ => $"Podsystem PC/SC zwrócił błąd {ToHex(errorCode)}."
        };
    }

    private static string GetProtocolName(uint protocol)
    {
        return protocol switch
        {
            NativeMethods.ScardProtocolT0 => "T=0",
            NativeMethods.ScardProtocolT1 => "T=1",
            _ => protocol == 0 ? "Brak" : $"Nieznany ({protocol})"
        };
    }

    private static string ToHex(uint value)
    {
        return $"0x{value:X8}";
    }

    private static string ToHex(IEnumerable<byte> bytes)
    {
        return string.Join(" ", bytes.Select(value => value.ToString("X2")));
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
    string Message,
    uint? ErrorCode,
    string ErrorCodeHex,
    bool IsMock = false);

internal sealed record ReaderConnectionStatus(
    string ReaderName,
    bool ReaderConnected,
    bool CardPresent,
    string Atr,
    int AtrLength,
    string Status,
    string Message,
    string ErrorMessage,
    bool IsMockReader,
    DateTime CheckedAtUtc)
{
    public static ReaderConnectionStatus Mock()
    {
        return new ReaderConnectionStatus(
            ReaderName: PcscReaderService.MockReaderName,
            ReaderConnected: false,
            CardPresent: false,
            Atr: string.Empty,
            AtrLength: 0,
            Status: "mock",
            Message: "Tryb testowy bez fizycznego czytnika. ATR nie jest dostępny.",
            ErrorMessage: string.Empty,
            IsMockReader: true,
            CheckedAtUtc: DateTime.UtcNow);
    }

    public static ReaderConnectionStatus NoReader(string message)
    {
        return new ReaderConnectionStatus(
            ReaderName: string.Empty,
            ReaderConnected: false,
            CardPresent: false,
            Atr: string.Empty,
            AtrLength: 0,
            Status: "reader-not-connected",
            Message: message,
            ErrorMessage: string.Empty,
            IsMockReader: false,
            CheckedAtUtc: DateTime.UtcNow);
    }
}

internal sealed record PcscDiagnostics(
    string Status,
    bool PcscAvailable,
    string Message,
    DateTime CheckedAtUtc,
    int ReadersCount,
    IReadOnlyList<ReaderDiagnostic> Readers,
    string LastError);

internal sealed record ReaderDiagnostic(
    string Name,
    bool? CardPresent,
    string Status,
    string Message,
    string AtrHex,
    int AtrLength,
    bool Connected,
    string Protocol,
    string ErrorMessage,
    string ErrorCodeHex);

internal sealed record AtrReadResult(
    string ReaderName,
    bool CardPresent,
    bool Connected,
    string AtrHex,
    int AtrLength,
    string Status,
    string ErrorMessage,
    uint? ErrorCode,
    string ErrorCodeHex,
    uint ActiveProtocol,
    string Protocol,
    uint PcscState)
{
    public static AtrReadResult Failure(
        string readerName,
        bool cardPresent,
        bool connected,
        string status,
        string errorMessage,
        uint? errorCode = null,
        uint activeProtocol = 0)
    {
        return new AtrReadResult(
            ReaderName: readerName,
            CardPresent: cardPresent,
            Connected: connected,
            AtrHex: string.Empty,
            AtrLength: 0,
            Status: status,
            ErrorMessage: errorMessage,
            ErrorCode: errorCode,
            ErrorCodeHex: errorCode.HasValue ? $"0x{errorCode.Value:X8}" : string.Empty,
            ActiveProtocol: activeProtocol,
            Protocol: activeProtocol == NativeMethods.ScardProtocolT0
                ? "T=0"
                : activeProtocol == NativeMethods.ScardProtocolT1
                    ? "T=1"
                    : string.Empty,
            PcscState: 0);
    }
}

internal sealed record StartCardReadRequest(
    string? SelectedReaderName,
    string? ReaderName)
{
    public string? GetRequestedReaderName()
    {
        if (!string.IsNullOrWhiteSpace(SelectedReaderName))
        {
            return SelectedReaderName.Trim();
        }

        return string.IsNullOrWhiteSpace(ReaderName)
            ? null
            : ReaderName.Trim();
    }
}

internal sealed record CardExportReadiness(
    bool IsImportable,
    string ExportFormat,
    string NextStepMessage)
{
    public static CardExportReadiness TechnicalJson()
    {
        return new CardExportReadiness(
            IsImportable: false,
            ExportFormat: "TechnicalJson",
            NextStepMessage: "To jest techniczny wynik komunikacji z kartą. Pełny eksport C1B/DDD wymaga kolejnego kroku implementacji APDU.");
    }

    public static CardExportReadiness TachographStructure()
    {
        return new CardExportReadiness(
            IsImportable: false,
            ExportFormat: "TechnicalJson",
            NextStepMessage: "To jest techniczny odczyt struktury karty tachografu. Nie jest jeszcze gotowym plikiem C1B/DDD i nie może być zaimportowany do ewidencji czasu pracy.");
    }

    public static CardExportReadiness Unknown(string nextStepMessage)
    {
        return new CardExportReadiness(
            IsImportable: false,
            ExportFormat: "Unknown",
            NextStepMessage: nextStepMessage);
    }
}

internal sealed record TachographCardReadResult(
    bool Success,
    string Message,
    string RequestedReaderName,
    string SelectedReaderName,
    bool SelectedReaderIsMock,
    string ReaderName,
    string Atr,
    string OutputFileName,
    string OutputPath,
    long FileSizeBytes,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    string ErrorDetails,
    bool IsMock,
    bool IsImportable,
    string ExportFormat,
    string NextStepMessage,
    IReadOnlyList<ApduResponse> ApduResponses,
    IReadOnlyList<CardFileReadResult> FileReads)
{
    public static TachographCardReadResult Failure(
        string readerName,
        DateTime startedAtUtc,
        string message,
        string errorDetails)
    {
        return new TachographCardReadResult(
            Success: false,
            Message: message,
            RequestedReaderName: readerName,
            SelectedReaderName: readerName,
            SelectedReaderIsMock: false,
            ReaderName: readerName,
            Atr: string.Empty,
            OutputFileName: string.Empty,
            OutputPath: string.Empty,
            FileSizeBytes: 0,
            StartedAtUtc: startedAtUtc,
            FinishedAtUtc: DateTime.UtcNow,
            ErrorDetails: errorDetails,
            IsMock: false,
            IsImportable: false,
            ExportFormat: "Unknown",
            NextStepMessage: message,
            ApduResponses: Array.Empty<ApduResponse>(),
            FileReads: Array.Empty<CardFileReadResult>());
    }
}

internal sealed record TachographCardReadPayload(
    bool Success,
    string Message,
    string RequestedReaderName,
    string SelectedReaderName,
    bool SelectedReaderIsMock,
    string ReaderName,
    string Atr,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    bool IsMock,
    IReadOnlyList<ApduResponse> ApduResponses,
    IReadOnlyList<CardFileReadResult> FileReads,
    string ErrorDetails,
    CardExportReadiness ExportReadiness)
{
    public bool IsImportable => ExportReadiness.IsImportable;

    public string ExportFormat => ExportReadiness.ExportFormat;

    public string NextStepMessage => ExportReadiness.NextStepMessage;
}

internal sealed record CardTechnicalReadResult(
    bool Success,
    string Message,
    string RequestedReaderName,
    string SelectedReaderName,
    bool SelectedReaderIsMock,
    string ReaderName,
    string Atr,
    string OutputFileName,
    string OutputPath,
    long FileSizeBytes,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    string ErrorDetails,
    bool IsMock,
    bool FullDddExportReady,
    bool IsImportable,
    string ExportFormat,
    string NextStepMessage,
    IReadOnlyList<CardApduResponse> ApduResponses)
{
    public static CardTechnicalReadResult Failure(
        string readerName,
        DateTime startedAtUtc,
        string message,
        string errorDetails)
    {
        return new CardTechnicalReadResult(
            Success: false,
            Message: message,
            RequestedReaderName: readerName,
            SelectedReaderName: readerName,
            SelectedReaderIsMock: false,
            ReaderName: readerName,
            Atr: string.Empty,
            OutputFileName: string.Empty,
            OutputPath: string.Empty,
            FileSizeBytes: 0,
            StartedAtUtc: startedAtUtc,
            FinishedAtUtc: DateTime.UtcNow,
            ErrorDetails: errorDetails,
            IsMock: false,
            FullDddExportReady: false,
            IsImportable: false,
            ExportFormat: "Unknown",
            NextStepMessage: message,
            ApduResponses: Array.Empty<CardApduResponse>());
    }
}

internal sealed record CardTechnicalReadPayload(
    bool Success,
    string Message,
    string RequestedReaderName,
    string SelectedReaderName,
    bool SelectedReaderIsMock,
    string ReaderName,
    string Atr,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    bool IsMock,
    IReadOnlyList<CardApduResponse> ApduResponses,
    string ErrorDetails,
    CardExportReadiness ExportReadiness)
{
    public bool IsImportable => ExportReadiness.IsImportable;

    public string ExportFormat => ExportReadiness.ExportFormat;

    public string NextStepMessage => ExportReadiness.NextStepMessage;
}

internal sealed record CardApduResponse(
    string Name,
    string CommandHex,
    string ResponseHex,
    string StatusWord,
    bool Success,
    string Message);

internal sealed record ApduCommand(
    string Name,
    string CommandHex,
    string Description)
{
    public static ApduCommand SelectFile(string name, string fileId)
    {
        return new ApduCommand(
            name,
            $"00 A4 00 0C 02 {FormatHex(fileId)}",
            $"SELECT FILE {FormatHex(fileId)}");
    }

    public static ApduCommand SelectEfByFileIdentifier(string name, string fileId)
    {
        return new ApduCommand(
            name,
            $"00 A4 02 0C 02 {FormatHex(fileId)}",
            $"SELECT EF by file identifier {FormatHex(fileId)}");
    }

    public static ApduCommand SelectApplicationByAid(string name, string aid)
    {
        var formattedAid = FormatHex(aid);
        var length = formattedAid.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        return new ApduCommand(
            name,
            $"00 A4 04 0C {length:X2} {formattedAid}",
            $"SELECT APPLICATION {formattedAid}");
    }

    public static ApduCommand ReadBinary(string name, int length)
    {
        var safeLength = Math.Clamp(length, 1, 0xFF);

        return new ApduCommand(
            name,
            $"00 B0 00 00 {safeLength:X2}",
            $"READ BINARY first {safeLength} bytes");
    }

    private static string FormatHex(string value)
    {
        var compact = new string(value.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        return string.Join(' ', Enumerable.Range(0, compact.Length / 2).Select(index => compact.Substring(index * 2, 2)));
    }
}

internal sealed record ApduResponse(
    string Name,
    string Description,
    string CommandHex,
    string ResponseHex,
    string DataHex,
    string StatusWord,
    string Sw1,
    string Sw2,
    bool Success,
    string StatusMeaning,
    string Message)
{
    public static ApduResponse Mock(string name, string commandHex)
    {
        return new ApduResponse(
            Name: name,
            Description: "Symulowana komenda APDU.",
            CommandHex: commandHex,
            ResponseHex: "90 00",
            DataHex: string.Empty,
            StatusWord: "9000",
            Sw1: "90",
            Sw2: "00",
            Success: true,
            StatusMeaning: "OK",
            Message: "Symulowana odpowiedź APDU.");
    }
}

internal sealed record CardFileReadResult(
    string FileName,
    string FileId,
    string Description,
    ApduResponse SelectResponse,
    ApduResponse? ReadResponse,
    bool SelectSucceeded,
    bool ReadSucceeded,
    string Message)
{
    public static CardFileReadResult Mock(string fileName, string fileId)
    {
        return new CardFileReadResult(
            FileName: fileName,
            FileId: fileId,
            Description: "Symulowany kandydat pliku EF.",
            SelectResponse: ApduResponse.Mock($"SELECT {fileName}", $"00 A4 00 0C 02 {fileId[..2]} {fileId[2..]}"),
            ReadResponse: ApduResponse.Mock($"READ {fileName} first bytes", "00 B0 00 00 40"),
            SelectSucceeded: true,
            ReadSucceeded: true,
            Message: "Symulowany wybór i próbny odczyt pliku.");
    }
}

internal sealed record TachographFileCandidate(
    string Name,
    string FileId,
    string Description,
    int MaxReadBytes);

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

    [DllImport("winscard.dll")]
    public static extern uint SCardTransmit(
        IntPtr card,
        ref ScardIoRequest sendPci,
        byte[] sendBuffer,
        int sendLength,
        IntPtr receivePci,
        [Out] byte[] receiveBuffer,
        ref int receiveLength);

    [StructLayout(LayoutKind.Sequential)]
    public struct ScardIoRequest
    {
        public uint Protocol;
        public int PciLength;
    }

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
