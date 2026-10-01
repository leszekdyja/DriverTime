import Foundation

struct CurrentUser: Codable, Identifiable {
    let id: String
    let companyId: String
    let companyName: String
    let firstName: String
    let lastName: String
    let email: String
    let role: String
}

struct AuthResponse: Codable {
    let token: String
    let expiresAtUtc: String
    let user: CurrentUser
}

struct Driver: Codable, Identifiable, Hashable {
    let id: String
    let firstName: String
    let lastName: String
    let cardNumber: String
    let cardExpiryDate: String?
    let cardIssuingCountry: String?
    let includeInPlanning: Bool?

    var displayName: String {
        "\(lastName) \(firstName)".trimmingCharacters(in: .whitespaces)
    }
}

enum WorkEvidenceActivityType: String, Codable, CaseIterable, Identifiable {
    case driving = "Driving"
    case otherWork = "OtherWork"
    case availability = "Availability"
    case breakRest = "BreakRest"
    case vacation = "Vacation"
    case sickLeave = "SickLeave"
    case dayOff = "DayOff"
    case otherAbsence = "OtherAbsence"

    var id: String { rawValue }

    var label: String {
        switch self {
        case .driving:
            return "Jazda"
        case .otherWork:
            return "Inna praca"
        case .availability:
            return "Dyspozycyjność"
        case .breakRest:
            return "Przerwa/odpoczynek"
        case .vacation:
            return "Urlop"
        case .sickLeave:
            return "Chorobowe"
        case .dayOff:
            return "Dzień wolny"
        case .otherAbsence:
            return "Inna nieobecność"
        }
    }
}

struct WorkEvidenceMonth: Codable {
    let driverId: String
    let driverFullName: String
    let year: Int
    let month: Int
    let summary: WorkEvidenceSummary
    let days: [WorkEvidenceDay]
}

struct WorkEvidenceSummary: Codable {
    let drivingMinutes: Int
    let otherWorkMinutes: Int
    let availabilityMinutes: Int
    let breakRestMinutes: Int
    let absenceMinutes: Int
    let totalTrackedMinutes: Int
}

struct WorkEvidenceDay: Codable, Identifiable {
    let date: String
    let drivingMinutes: Int
    let otherWorkMinutes: Int
    let availabilityMinutes: Int
    let breakRestMinutes: Int
    let absenceMinutes: Int
    let totalTrackedMinutes: Int
    let entries: [WorkEvidenceEntry]

    var id: String { date }
}

struct WorkEvidenceEntry: Codable, Identifiable {
    let id: String
    let date: String
    let startDateTime: String
    let endDateTime: String
    let startTime: String
    let endTime: String
    let endsNextDay: Bool
    let activityType: WorkEvidenceActivityType
    let source: String
    let vehicleRegistration: String?
    let countryCode: String?
    let distanceKm: Double?
    let description: String?
    let durationMinutes: Int
}

struct WorkEvidenceEntryRequest: Codable, Identifiable {
    var id = UUID()
    var date: String
    var startTime: String
    var endTime: String
    var endsNextDay: Bool
    var activityType: WorkEvidenceActivityType
    var vehicleRegistration: String?
    var countryCode: String?
    var distanceKm: Double?
    var description: String?

    enum CodingKeys: String, CodingKey {
        case date
        case startTime
        case endTime
        case endsNextDay
        case activityType
        case vehicleRegistration
        case countryCode
        case distanceKm
        case description
    }
}

struct PendingWorkEvidenceEntry: Codable, Identifiable {
    let id: UUID
    let driverId: String
    let request: WorkEvidenceEntryRequest
    let createdAt: Date
    var lastError: String?
}
