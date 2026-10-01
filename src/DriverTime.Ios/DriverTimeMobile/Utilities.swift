import Foundation

enum DriverTimeFormatters {
    static let day: DateFormatter = {
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "yyyy-MM-dd"
        return formatter
    }()

    static let time: DateFormatter = {
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "HH:mm"
        return formatter
    }()

    static func apiDate(_ date: Date) -> String {
        day.string(from: date)
    }

    static func apiTime(_ date: Date) -> String {
        time.string(from: date)
    }

    static func minutesLabel(_ minutes: Int) -> String {
        guard minutes > 0 else { return "-" }
        return "\(minutes / 60):\(String(format: "%02d", minutes % 60))"
    }
}

extension String {
    var normalizedBaseURL: String {
        trimmingCharacters(in: .whitespacesAndNewlines)
            .trimmingCharacters(in: CharacterSet(charactersIn: "/"))
    }
}

struct ApiErrorResponse: Decodable {
    let title: String?
    let message: String?
    let errors: [String: [String]]?

    var combinedMessage: String? {
        if let message, !message.isEmpty { return message }
        if let title, !title.isEmpty { return title }
        let validationErrors = errors?.values.flatMap { $0 }.joined(separator: " ")
        return validationErrors?.isEmpty == false ? validationErrors : nil
    }
}
