import Foundation

final class DriverTimeApiClient {
    var baseURL: String
    var token: String?

    init(baseURL: String = "", token: String? = nil) {
        self.baseURL = baseURL.normalizedBaseURL
        self.token = token
    }

    func login(email: String, password: String) async throws -> AuthResponse {
        try await send(
            "api/auth/login",
            method: "POST",
            body: ["email": email, "password": password],
            authorized: false
        )
    }

    func currentUser() async throws -> CurrentUser {
        try await send("api/auth/me")
    }

    func drivers() async throws -> [Driver] {
        try await send("api/drivers")
    }

    func workEvidence(driverId: String, year: Int, month: Int) async throws -> WorkEvidenceMonth {
        try await send("api/drivers/\(driverId)/work-evidence?year=\(year)&month=\(month)")
    }

    func createEntry(driverId: String, request: WorkEvidenceEntryRequest) async throws -> WorkEvidenceEntry {
        try await send(
            "api/drivers/\(driverId)/work-evidence/entries",
            method: "POST",
            body: request
        )
    }

    func deleteEntry(entryId: String) async throws {
        let _: EmptyResponse = try await send(
            "api/work-evidence/entries/\(entryId)",
            method: "DELETE"
        )
    }

    private func send<T: Decodable>(
        _ path: String,
        method: String = "GET",
        body: Encodable? = nil,
        authorized: Bool = true
    ) async throws -> T {
        guard !baseURL.isEmpty, let url = URL(string: "\(baseURL)/\(path)") else {
            throw DriverTimeApiError.invalidBaseURL
        }

        var request = URLRequest(url: url)
        request.httpMethod = method
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")

        if authorized {
            guard let token, !token.isEmpty else {
                throw DriverTimeApiError.unauthorized
            }
            request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        }

        if let body {
            request.httpBody = try JSONEncoder.driverTime.encode(AnyEncodable(body))
        }

        let (data, response) = try await URLSession.shared.data(for: request)
        let statusCode = (response as? HTTPURLResponse)?.statusCode ?? 0

        guard (200..<300).contains(statusCode) else {
            if statusCode == 401 {
                throw DriverTimeApiError.unauthorized
            }

            let message = try? JSONDecoder.driverTime
                .decode(ApiErrorResponse.self, from: data)
                .combinedMessage
            throw DriverTimeApiError.server(message ?? "Błąd API DriverTime (\(statusCode)).")
        }

        if T.self == EmptyResponse.self {
            return EmptyResponse() as! T
        }

        return try JSONDecoder.driverTime.decode(T.self, from: data)
    }
}

struct EmptyResponse: Decodable {}

enum DriverTimeApiError: LocalizedError {
    case invalidBaseURL
    case unauthorized
    case server(String)

    var errorDescription: String? {
        switch self {
        case .invalidBaseURL:
            "Nieprawidłowy adres API DriverTime."
        case .unauthorized:
            "Sesja wygasła. Zaloguj się ponownie."
        case .server(let message):
            message
        }
    }
}

struct AnyEncodable: Encodable {
    private let encodeAction: (Encoder) throws -> Void

    init(_ value: Encodable) {
        encodeAction = value.encode
    }

    func encode(to encoder: Encoder) throws {
        try encodeAction(encoder)
    }
}

extension JSONEncoder {
    static var driverTime: JSONEncoder {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        return encoder
    }
}

extension JSONDecoder {
    static var driverTime: JSONDecoder {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        return decoder
    }
}
