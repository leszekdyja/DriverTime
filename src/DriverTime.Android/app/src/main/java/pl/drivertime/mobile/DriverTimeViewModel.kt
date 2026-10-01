package pl.drivertime.mobile

import android.app.Application
import android.location.Location
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class DriverTimeViewModel(application: Application) : AndroidViewModel(application) {
    private val storage = LocalStorage(application)
    private val api = DriverTimeApiClient()
    private val _uiState = MutableStateFlow(AppUiState(pendingEntries = storage.loadPendingEntries()))
    private var gpsTracker: GpsActivityTracker? = null
    private var lastGpsLocation: Location? = null
    private var drivingFixes = 0
    private var stoppedFixes = 0
    private var activeAutoActivity: ActivityType? = null
    private var activeAutoStartedAtMillis: Long? = null
    private var activeAutoDistanceMeters = 0.0

    val uiState: StateFlow<AppUiState> = _uiState

    init {
        api.baseUrl = storage.baseUrl
        api.token = storage.authToken
        restore()
    }

    fun restore() {
        viewModelScope.launch {
            if (api.baseUrl.isBlank() || api.token.isNullOrBlank()) {
                _uiState.update { it.copy(isRestoring = false) }
                return@launch
            }

            runBackend(
                busy = false,
                onSuccess = {
                    val user = api.currentUser()
                    val driver = storage.assignedDriver
                    _uiState.update {
                        it.copy(
                            isRestoring = false,
                            currentUser = user,
                            assignedDriver = driver,
                            pendingEntries = storage.loadPendingEntries()
                        )
                    }

                    if (driver == null) {
                        loadDrivers()
                    } else {
                        refresh()
                    }
                },
                onError = {
                    storage.clearSession()
                    _uiState.update { state -> state.copy(isRestoring = false, errorMessage = it.message) }
                }
            )
        }
    }

    fun login(baseUrl: String, email: String, password: String) {
        api.baseUrl = baseUrl.trim().trimEnd('/')

        viewModelScope.launch {
            runBackend {
                val (token, user) = api.login(email, password)
                api.token = token
                storage.baseUrl = api.baseUrl
                storage.authToken = token
                _uiState.update { it.copy(currentUser = user, assignedDriver = null, month = null) }
                loadDrivers()
            }
        }
    }

    fun activateMobileSetup(baseUrl: String, inviteToken: String) {
        api.baseUrl = baseUrl.trim().trimEnd('/')

        viewModelScope.launch {
            runBackend {
                val activation = api.activateMobileInvite(inviteToken)
                api.baseUrl = activation.apiBaseUrl.trim().trimEnd('/')
                api.token = activation.authToken
                storage.baseUrl = api.baseUrl
                storage.authToken = activation.authToken
                storage.assignedDriver = activation.driver
                _uiState.update {
                    it.copy(
                        currentUser = activation.currentUser,
                        assignedDriver = activation.driver,
                        drivers = listOf(activation.driver),
                        month = null,
                        errorMessage = null
                    )
                }
                refresh()
            }
        }
    }

    fun logout() {
        stopAutoTracking()
        api.token = null
        storage.clearSession()
        _uiState.value = AppUiState(isRestoring = false)
    }

    fun loadDrivers() {
        viewModelScope.launch {
            runBackend {
                val drivers = api.drivers()
                _uiState.update { it.copy(drivers = drivers) }
            }
        }
    }

    fun assignDriver(driver: Driver) {
        storage.assignedDriver = driver
        _uiState.update { it.copy(assignedDriver = driver, month = null) }
        refresh()
    }

    fun clearAssignedDriver() {
        stopAutoTracking()
        storage.assignedDriver = null
        _uiState.update { it.copy(assignedDriver = null, month = null) }
        loadDrivers()
    }

    fun setTimerActivity(activityType: ActivityType) {
        _uiState.update { it.copy(timerActivity = activityType) }
    }

    fun startTimer() {
        _uiState.update { it.copy(timerStartedAtMillis = System.currentTimeMillis()) }
    }

    fun stopTimer() {
        val state = _uiState.value
        val started = state.timerStartedAtMillis ?: return
        val finished = System.currentTimeMillis()
        val startDay = DriverTimeFormatters.apiDate(started)
        val endDay = DriverTimeFormatters.apiDate(finished)
        val request = WorkEvidenceEntryRequest(
            date = startDay,
            startTime = DriverTimeFormatters.apiTime(started),
            endTime = DriverTimeFormatters.apiTime(finished),
            endsNextDay = startDay != endDay,
            activityType = state.timerActivity,
            vehicleRegistration = null,
            countryCode = "PL",
            distanceKm = null,
            description = null
        )

        _uiState.update { it.copy(timerStartedAtMillis = null) }
        saveEntry(request)
    }

    fun refresh() {
        val driver = _uiState.value.assignedDriver ?: return
        viewModelScope.launch {
            runBackend {
                syncPendingFor(driver.id)
                val (year, month) = DriverTimeFormatters.currentYearMonth()
                val evidence = api.workEvidence(driver.id, year, month)
                _uiState.update {
                    it.copy(
                        month = evidence,
                        pendingEntries = storage.loadPendingEntries()
                    )
                }
            }
        }
    }

    fun saveEntry(request: WorkEvidenceEntryRequest) {
        val driver = _uiState.value.assignedDriver ?: return
        viewModelScope.launch {
            runBackend(
                onSuccess = {
                    api.createEntry(driver.id, request)
                    refresh()
                },
                onError = { error ->
                    if (error is DriverTimeApiException && error.status in 400..499) {
                        _uiState.update {
                            it.copy(errorMessage = error.message ?: "API odrzucilo wpis ewidencji.")
                        }
                        return@runBackend
                    }

                    storage.addPendingEntry(driver.id, request, error.message)
                    _uiState.update {
                        it.copy(
                            pendingEntries = storage.loadPendingEntries(),
                            errorMessage = "Brak synchronizacji. Wpis zapisano lokalnie i zostanie wysłany później."
                        )
                    }
                }
            )
        }
    }

    fun deleteEntry(entry: WorkEvidenceEntry) {
        viewModelScope.launch {
            runBackend {
                api.deleteEntry(entry.id)
                refresh()
            }
        }
    }

    fun setLocationPermissionGranted(granted: Boolean) {
        _uiState.update { it.copy(locationPermissionGranted = granted) }
    }

    fun startAutoTracking() {
        if (_uiState.value.assignedDriver == null) {
            _uiState.update { it.copy(errorMessage = "Najpierw przypisz telefon do kierowcy.") }
            return
        }

        if (!_uiState.value.locationPermissionGranted) {
            _uiState.update { it.copy(errorMessage = "Nadaj uprawnienie lokalizacji, aby wlaczyc automatyczna rejestracje GPS.") }
            return
        }

        if (gpsTracker != null) {
            return
        }

        gpsTracker = GpsActivityTracker(
            context = getApplication(),
            onLocation = ::handleGpsLocation,
            onError = ::handleGpsError
        )
        gpsTracker?.start()
        _uiState.update {
            it.copy(
                isAutoTrackingEnabled = true,
                autoTrackingStatus = "GPS aktywny. Oczekiwanie na wykrycie jazdy.",
                errorMessage = null
            )
        }
    }

    fun stopAutoTracking() {
        gpsTracker?.stop()
        gpsTracker = null
        finishAutoActivity(System.currentTimeMillis(), "Auto GPS - zakonczono sledzenie")
        lastGpsLocation = null
        drivingFixes = 0
        stoppedFixes = 0
        _uiState.update {
            it.copy(
                isAutoTrackingEnabled = false,
                autoTrackingStatus = "GPS wylaczony",
                detectedActivityType = null,
                detectedSpeedKmh = null,
                lastGpsFixAtMillis = null,
                autoActivityStartedAtMillis = null,
                autoDistanceKm = 0.0
            )
        }
    }

    private fun handleGpsLocation(location: Location) {
        val previous = lastGpsLocation
        val speedKmh = calculateSpeedKmh(previous, location)
        val deltaMeters = previous?.let { old ->
            if (location.time > old.time) old.distanceTo(location).toDouble().coerceAtLeast(0.0) else 0.0
        } ?: 0.0

        if (speedKmh >= DrivingSpeedThresholdKmh) {
            drivingFixes += 1
            stoppedFixes = 0
        } else if (speedKmh <= StoppedSpeedThresholdKmh) {
            stoppedFixes += 1
            drivingFixes = 0
        } else {
            drivingFixes = 0
            stoppedFixes = 0
        }

        if (activeAutoActivity == ActivityType.Driving && deltaMeters > 0.0) {
            activeAutoDistanceMeters += deltaMeters
        }

        val now = location.time.takeIf { it > 0L } ?: System.currentTimeMillis()
        if (drivingFixes >= RequiredDrivingFixes && activeAutoActivity != ActivityType.Driving) {
            switchAutoActivity(ActivityType.Driving, now)
        } else if (stoppedFixes >= RequiredStoppedFixes && activeAutoActivity == ActivityType.Driving) {
            switchAutoActivity(ActivityType.BreakRest, now)
        }

        lastGpsLocation = location
        _uiState.update {
            it.copy(
                detectedSpeedKmh = speedKmh,
                lastGpsFixAtMillis = now,
                detectedActivityType = activeAutoActivity,
                autoActivityStartedAtMillis = activeAutoStartedAtMillis,
                autoDistanceKm = activeAutoDistanceMeters / 1000.0,
                autoTrackingStatus = when (activeAutoActivity) {
                    ActivityType.Driving -> "Wykryto jazde GPS"
                    ActivityType.BreakRest -> "Wykryto postoj / odpoczynek"
                    else -> "GPS aktywny"
                }
            )
        }
    }

    private fun handleGpsError(error: Throwable) {
        _uiState.update {
            it.copy(errorMessage = error.message ?: "Nie udalo sie uruchomic GPS.")
        }
    }

    private fun calculateSpeedKmh(previous: Location?, current: Location): Double {
        if (current.hasSpeed()) {
            return (current.speed * 3.6).toDouble().coerceAtLeast(0.0)
        }

        if (previous == null || current.time <= previous.time) {
            return 0.0
        }

        val seconds = (current.time - previous.time) / 1000.0
        if (seconds <= 0.0) {
            return 0.0
        }

        return (previous.distanceTo(current) / seconds * 3.6).coerceAtLeast(0.0)
    }

    private fun switchAutoActivity(nextActivity: ActivityType, atMillis: Long) {
        finishAutoActivity(atMillis, "Auto GPS")
        beginAutoActivity(nextActivity, atMillis)
    }

    private fun beginAutoActivity(activityType: ActivityType, atMillis: Long) {
        activeAutoActivity = activityType
        activeAutoStartedAtMillis = atMillis
        activeAutoDistanceMeters = 0.0
        _uiState.update {
            it.copy(
                detectedActivityType = activityType,
                autoActivityStartedAtMillis = atMillis,
                autoDistanceKm = 0.0
            )
        }
    }

    private fun finishAutoActivity(finishedAtMillis: Long, description: String) {
        val startedAtMillis = activeAutoStartedAtMillis ?: return
        val activityType = activeAutoActivity ?: return
        val durationMinutes = (finishedAtMillis - startedAtMillis) / 60_000L

        activeAutoActivity = null
        activeAutoStartedAtMillis = null

        if (durationMinutes < MinimumAutoEntryMinutes) {
            activeAutoDistanceMeters = 0.0
            return
        }

        val startDay = DriverTimeFormatters.apiDate(startedAtMillis)
        val endDay = DriverTimeFormatters.apiDate(finishedAtMillis)
        val distanceKm = if (activityType == ActivityType.Driving) {
            (activeAutoDistanceMeters / 1000.0).takeIf { it > 0.05 }
        } else {
            null
        }

        activeAutoDistanceMeters = 0.0
        saveEntry(
            WorkEvidenceEntryRequest(
                date = startDay,
                startTime = DriverTimeFormatters.apiTime(startedAtMillis),
                endTime = DriverTimeFormatters.apiTime(finishedAtMillis),
                endsNextDay = startDay != endDay,
                activityType = activityType,
                source = EntrySource.MobileGps,
                vehicleRegistration = null,
                countryCode = "PL",
                distanceKm = distanceKm,
                description = description
            )
        )
    }

    private suspend fun syncPendingFor(driverId: String) {
        val pending = storage.loadPendingEntries()
        val remaining = mutableListOf<PendingEntry>()

        pending.forEach { entry ->
            if (entry.driverId != driverId) {
                remaining += entry
                return@forEach
            }

            try {
                api.createEntry(driverId, entry.request)
            } catch (error: Throwable) {
                remaining += entry.copy(lastError = error.message)
            }
        }

        storage.savePendingEntries(remaining)
    }

    private fun runBackend(
        busy: Boolean = true,
        onError: (Throwable) -> Unit = { error ->
            _uiState.update { it.copy(errorMessage = error.message ?: "Wystąpił błąd.") }
        },
        onSuccess: suspend () -> Unit
    ) {
        viewModelScope.launch {
            if (busy) {
                _uiState.update { it.copy(isBusy = true, errorMessage = null) }
            }

            try {
                withContext(Dispatchers.IO) {
                    onSuccess()
                }
            } catch (error: Throwable) {
                onError(error)
            } finally {
                if (busy) {
                    _uiState.update { it.copy(isBusy = false, isRestoring = false) }
                }
            }
        }
    }

    companion object {
        private const val DrivingSpeedThresholdKmh = 8.0
        private const val StoppedSpeedThresholdKmh = 3.0
        private const val RequiredDrivingFixes = 2
        private const val RequiredStoppedFixes = 3
        private const val MinimumAutoEntryMinutes = 1L
    }
}
