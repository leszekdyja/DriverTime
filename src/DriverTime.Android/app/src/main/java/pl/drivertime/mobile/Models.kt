package pl.drivertime.mobile

data class CurrentUser(
    val id: String,
    val companyId: String,
    val companyName: String,
    val firstName: String,
    val lastName: String,
    val email: String,
    val role: String
)

data class Driver(
    val id: String,
    val firstName: String,
    val lastName: String,
    val cardNumber: String
) {
    val displayName: String
        get() = "$lastName $firstName".trim()
}

enum class ActivityType(val apiValue: String, val label: String) {
    Driving("Driving", "Jazda"),
    OtherWork("OtherWork", "Inna praca"),
    Availability("Availability", "Dyspozycyjność"),
    BreakRest("BreakRest", "Przerwa/odpoczynek"),
    Vacation("Vacation", "Urlop"),
    SickLeave("SickLeave", "Chorobowe"),
    DayOff("DayOff", "Dzień wolny"),
    OtherAbsence("OtherAbsence", "Inna nieobecność");

    companion object {
        fun fromApi(value: String): ActivityType =
            entries.firstOrNull { it.apiValue == value } ?: Driving
    }
}

enum class EntrySource(val apiValue: String) {
    Manual("Manual"),
    MobileGps("MobileGps");

    companion object {
        fun fromApi(value: String): EntrySource? =
            entries.firstOrNull { it.apiValue == value }
    }
}

data class WorkEvidenceEntry(
    val id: String,
    val date: String,
    val startTime: String,
    val endTime: String,
    val endsNextDay: Boolean,
    val activityType: ActivityType,
    val source: EntrySource?,
    val vehicleRegistration: String?,
    val countryCode: String?,
    val distanceKm: Double?,
    val description: String?,
    val durationMinutes: Int
)

data class WorkEvidenceDay(
    val date: String,
    val drivingMinutes: Int,
    val otherWorkMinutes: Int,
    val availabilityMinutes: Int,
    val breakRestMinutes: Int,
    val absenceMinutes: Int,
    val totalTrackedMinutes: Int,
    val entries: List<WorkEvidenceEntry>
)

data class WorkEvidenceSummary(
    val drivingMinutes: Int,
    val otherWorkMinutes: Int,
    val availabilityMinutes: Int,
    val breakRestMinutes: Int,
    val absenceMinutes: Int,
    val totalTrackedMinutes: Int
)

data class WorkEvidenceMonth(
    val driverId: String,
    val driverFullName: String,
    val year: Int,
    val month: Int,
    val summary: WorkEvidenceSummary,
    val days: List<WorkEvidenceDay>
)

data class MobileAppActivation(
    val apiBaseUrl: String,
    val authToken: String,
    val currentUser: CurrentUser,
    val driver: Driver
)

data class WorkEvidenceEntryRequest(
    val date: String,
    val startTime: String,
    val endTime: String,
    val endsNextDay: Boolean,
    val activityType: ActivityType,
    val source: EntrySource? = null,
    val vehicleRegistration: String?,
    val countryCode: String?,
    val distanceKm: Double?,
    val description: String?
)

data class PendingEntry(
    val id: String,
    val driverId: String,
    val request: WorkEvidenceEntryRequest,
    val createdAt: String,
    val lastError: String?
)

data class AppUiState(
    val isRestoring: Boolean = true,
    val isBusy: Boolean = false,
    val currentUser: CurrentUser? = null,
    val assignedDriver: Driver? = null,
    val drivers: List<Driver> = emptyList(),
    val month: WorkEvidenceMonth? = null,
    val pendingEntries: List<PendingEntry> = emptyList(),
    val errorMessage: String? = null,
    val timerStartedAtMillis: Long? = null,
    val timerActivity: ActivityType = ActivityType.Driving,
    val locationPermissionGranted: Boolean = false,
    val isAutoTrackingEnabled: Boolean = false,
    val autoTrackingStatus: String = "GPS wylaczony",
    val detectedActivityType: ActivityType? = null,
    val detectedSpeedKmh: Double? = null,
    val lastGpsFixAtMillis: Long? = null,
    val autoActivityStartedAtMillis: Long? = null,
    val autoDistanceKm: Double = 0.0
)
