package pl.drivertime.mobile

import org.json.JSONArray
import org.json.JSONObject
import java.io.BufferedReader
import java.io.InputStreamReader
import java.net.HttpURLConnection
import java.net.URL

class DriverTimeApiException(val status: Int, message: String) : Exception(message)

class DriverTimeApiClient {
    var baseUrl: String = ""
    var token: String? = null

    fun login(email: String, password: String): Pair<String, CurrentUser> {
        val json = requestJson(
            path = "api/auth/login",
            method = "POST",
            body = JSONObject()
                .put("email", email)
                .put("password", password),
            authorized = false
        )

        return json.getString("token") to json.getJSONObject("user").toCurrentUser()
    }

    fun currentUser(): CurrentUser =
        requestJson("api/auth/me").toCurrentUser()

    fun activateMobileInvite(token: String): MobileAppActivation {
        val json = requestJson(
            path = "api/mobile/activate",
            method = "POST",
            body = JSONObject().put("token", token),
            authorized = false
        )

        return MobileAppActivation(
            apiBaseUrl = json.optString("apiBaseUrl", baseUrl),
            authToken = json.getJSONObject("auth").getString("token"),
            currentUser = json.getJSONObject("auth").getJSONObject("user").toCurrentUser(),
            driver = json.getJSONObject("driver").toDriver()
        )
    }

    fun drivers(): List<Driver> =
        requestArray("api/drivers").map { it.toDriver() }

    fun workEvidence(driverId: String, year: Int, month: Int): WorkEvidenceMonth =
        requestJson("api/drivers/$driverId/work-evidence?year=$year&month=$month")
            .toWorkEvidenceMonth()

    fun createEntry(driverId: String, request: WorkEvidenceEntryRequest): WorkEvidenceEntry =
        requestJson(
            path = "api/drivers/$driverId/work-evidence/entries",
            method = "POST",
            body = request.toJson()
        ).toWorkEvidenceEntry()

    fun deleteEntry(entryId: String) {
        requestRaw(path = "api/work-evidence/entries/$entryId", method = "DELETE")
    }

    private fun requestJson(
        path: String,
        method: String = "GET",
        body: JSONObject? = null,
        authorized: Boolean = true
    ): JSONObject = JSONObject(requestRaw(path, method, body, authorized))

    private fun requestArray(path: String): List<JSONObject> {
        val array = JSONArray(requestRaw(path))
        return (0 until array.length()).map { array.getJSONObject(it) }
    }

    private fun requestRaw(
        path: String,
        method: String = "GET",
        body: JSONObject? = null,
        authorized: Boolean = true
    ): String {
        val normalizedBaseUrl = baseUrl.trim().trimEnd('/')
        require(normalizedBaseUrl.isNotBlank()) { "Nieprawidłowy adres API DriverTime." }

        val connection = URL("$normalizedBaseUrl/$path").openConnection() as HttpURLConnection
        connection.requestMethod = method
        connection.setRequestProperty("Content-Type", "application/json")

        if (authorized) {
            val authToken = token ?: throw IllegalStateException("Sesja wygasła. Zaloguj się ponownie.")
            connection.setRequestProperty("Authorization", "Bearer $authToken")
        }

        if (body != null) {
            connection.doOutput = true
            connection.outputStream.use { output ->
                output.write(body.toString().toByteArray(Charsets.UTF_8))
            }
        }

        val status = connection.responseCode
        val stream = if (status in 200..299) connection.inputStream else connection.errorStream
        val response = stream?.use { input ->
            BufferedReader(InputStreamReader(input, Charsets.UTF_8)).readText()
        }.orEmpty()

        if (status !in 200..299) {
            throw DriverTimeApiException(status, readErrorMessage(response, status))
        }

        return response.ifBlank { "{}" }
    }

    private fun readErrorMessage(response: String, status: Int): String {
        return runCatching {
            val json = JSONObject(response)
            when {
                json.has("message") -> json.getString("message")
                json.has("title") -> json.getString("title")
                json.has("errors") -> readErrors(json.get("errors"))
                else -> "Błąd API DriverTime ($status)."
            }
        }.getOrElse {
            "Błąd API DriverTime ($status)."
        }
    }
    private fun readErrors(errors: Any): String =
        when (errors) {
            is JSONArray -> (0 until errors.length())
                .joinToString(" ") { index -> errors.getString(index) }
            is JSONObject -> errors
                .keys()
                .asSequence()
                .flatMap { key ->
                    when (val value = errors.get(key)) {
                        is JSONArray -> (0 until value.length()).asSequence().map { value.getString(it) }
                        else -> sequenceOf(value.toString())
                    }
                }
                .joinToString(" ")
            else -> errors.toString()
        }.ifBlank { "Blad API DriverTime." }
}

private fun JSONObject.toCurrentUser() = CurrentUser(
    id = getString("id"),
    companyId = getString("companyId"),
    companyName = optString("companyName"),
    firstName = optString("firstName"),
    lastName = optString("lastName"),
    email = optString("email"),
    role = optString("role")
)

private fun JSONObject.toDriver() = Driver(
    id = getString("id"),
    firstName = optString("firstName"),
    lastName = optString("lastName"),
    cardNumber = optString("cardNumber")
)

private fun JSONObject.toWorkEvidenceMonth() = WorkEvidenceMonth(
    driverId = getString("driverId"),
    driverFullName = optString("driverFullName"),
    year = getInt("year"),
    month = getInt("month"),
    summary = getJSONObject("summary").toWorkEvidenceSummary(),
    days = getJSONArray("days").toObjectList { toWorkEvidenceDay() }
)

private fun JSONObject.toWorkEvidenceSummary() = WorkEvidenceSummary(
    drivingMinutes = optInt("drivingMinutes"),
    otherWorkMinutes = optInt("otherWorkMinutes"),
    availabilityMinutes = optInt("availabilityMinutes"),
    breakRestMinutes = optInt("breakRestMinutes"),
    absenceMinutes = optInt("absenceMinutes"),
    totalTrackedMinutes = optInt("totalTrackedMinutes")
)

private fun JSONObject.toWorkEvidenceDay() = WorkEvidenceDay(
    date = getString("date"),
    drivingMinutes = optInt("drivingMinutes"),
    otherWorkMinutes = optInt("otherWorkMinutes"),
    availabilityMinutes = optInt("availabilityMinutes"),
    breakRestMinutes = optInt("breakRestMinutes"),
    absenceMinutes = optInt("absenceMinutes"),
    totalTrackedMinutes = optInt("totalTrackedMinutes"),
    entries = getJSONArray("entries").toObjectList { toWorkEvidenceEntry() }
)

private fun JSONObject.toWorkEvidenceEntry() = WorkEvidenceEntry(
    id = getString("id"),
    date = getString("date"),
    startTime = optString("startTime"),
    endTime = optString("endTime"),
    endsNextDay = optBoolean("endsNextDay"),
    activityType = ActivityType.fromApi(optString("activityType")),
    source = EntrySource.fromApi(optString("source")),
    vehicleRegistration = optNullableString("vehicleRegistration"),
    countryCode = optNullableString("countryCode"),
    distanceKm = if (isNull("distanceKm")) null else optDouble("distanceKm"),
    description = optNullableString("description"),
    durationMinutes = optInt("durationMinutes")
)

private fun WorkEvidenceEntryRequest.toJson(): JSONObject {
    val json = JSONObject()
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

    return json
}

private fun JSONObject.optNullableString(name: String): String? =
    if (isNull(name)) null else optString(name).takeIf { it.isNotBlank() }

private fun <T> JSONArray.toObjectList(mapper: JSONObject.() -> T): List<T> =
    (0 until length()).map { getJSONObject(it).mapper() }
