package pl.drivertime.mobile

import android.content.Context
import org.json.JSONArray
import org.json.JSONObject
import java.time.Instant
import java.util.UUID

class LocalStorage(context: Context) {
    private val preferences = context.getSharedPreferences("drivertime-mobile", Context.MODE_PRIVATE)

    var baseUrl: String
        get() = preferences.getString("baseUrl", "").orEmpty()
        set(value) = preferences.edit().putString("baseUrl", value).apply()

    var authToken: String?
        get() = preferences.getString("authToken", null)
        set(value) = preferences.edit().putString("authToken", value).apply()

    var assignedDriver: Driver?
        get() = preferences.getString("assignedDriver", null)?.let {
            runCatching { JSONObject(it).toStoredDriver() }.getOrNull()
        }
        set(value) {
            preferences.edit()
                .putString("assignedDriver", value?.toJson()?.toString())
                .apply()
        }

    fun loadPendingEntries(): List<PendingEntry> {
        val raw = preferences.getString("pendingEntries", "[]").orEmpty()
        val array = JSONArray(raw)
        return (0 until array.length()).map { array.getJSONObject(it).toPendingEntry() }
    }

    fun savePendingEntries(entries: List<PendingEntry>) {
        val array = JSONArray()
        entries.forEach { array.put(it.toJson()) }
        preferences.edit().putString("pendingEntries", array.toString()).apply()
    }

    fun addPendingEntry(driverId: String, request: WorkEvidenceEntryRequest, error: String?) {
        val current = loadPendingEntries().toMutableList()
        current += PendingEntry(
            id = UUID.randomUUID().toString(),
            driverId = driverId,
            request = request,
            createdAt = Instant.now().toString(),
            lastError = error
        )
        savePendingEntries(current)
    }

    fun clearSession() {
        preferences.edit()
            .remove("authToken")
            .remove("assignedDriver")
            .apply()
    }
}

private fun Driver.toJson() = JSONObject()
    .put("id", id)
    .put("firstName", firstName)
    .put("lastName", lastName)
    .put("cardNumber", cardNumber)

private fun JSONObject.toStoredDriver() = Driver(
    id = getString("id"),
    firstName = optString("firstName"),
    lastName = optString("lastName"),
    cardNumber = optString("cardNumber")
)

private fun PendingEntry.toJson() = JSONObject()
    .put("id", id)
    .put("driverId", driverId)
    .put("request", request.toPendingJson())
    .put("createdAt", createdAt)
    .put("lastError", lastError)

private fun JSONObject.toPendingEntry() = PendingEntry(
    id = getString("id"),
    driverId = getString("driverId"),
    request = getJSONObject("request").toPendingRequest(),
    createdAt = optString("createdAt"),
    lastError = if (isNull("lastError")) null else optString("lastError")
)

private fun WorkEvidenceEntryRequest.toPendingJson() = JSONObject()
    .put("date", date)
    .put("startTime", startTime)
    .put("endTime", endTime)
    .put("endsNextDay", endsNextDay)
    .put("activityType", activityType.apiValue)
    .put("source", source?.apiValue)
    .put("vehicleRegistration", vehicleRegistration)
    .put("countryCode", countryCode)
    .put("distanceKm", distanceKm)
    .put("description", description)

private fun JSONObject.toPendingRequest() = WorkEvidenceEntryRequest(
    date = getString("date"),
    startTime = getString("startTime"),
    endTime = getString("endTime"),
    endsNextDay = optBoolean("endsNextDay"),
    activityType = ActivityType.fromApi(getString("activityType")),
    source = EntrySource.fromApi(optString("source")),
    vehicleRegistration = optNullableString("vehicleRegistration"),
    countryCode = optNullableString("countryCode"),
    distanceKm = if (isNull("distanceKm")) null else optDouble("distanceKm"),
    description = optNullableString("description")
)

private fun JSONObject.optNullableString(name: String): String? =
    if (isNull(name)) null else optString(name).takeIf { it.isNotBlank() }
