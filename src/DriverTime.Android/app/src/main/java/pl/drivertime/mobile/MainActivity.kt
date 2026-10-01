package pl.drivertime.mobile

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Bundle
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.Checkbox
import androidx.compose.material3.Divider
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import androidx.lifecycle.viewmodel.compose.viewModel
import kotlin.math.roundToInt

class MainActivity : ComponentActivity() {
    private var setupLinkState = mutableStateOf<MobileSetupLink?>(null)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setupLinkState.value = intent.toMobileSetupLink()
        setContent {
            MaterialTheme {
                Surface(modifier = Modifier.fillMaxSize()) {
                    DriverTimeApp(setupLinkState.value)
                }
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setupLinkState.value = intent.toMobileSetupLink()
    }
}

@Composable
private fun DriverTimeApp(
    setupLink: MobileSetupLink?,
    viewModel: DriverTimeViewModel = viewModel()
) {
    val state by viewModel.uiState.collectAsState()

    LaunchedEffect(setupLink) {
        if (setupLink != null) {
            viewModel.activateMobileSetup(setupLink.apiUrl, setupLink.token)
        }
    }

    when {
        state.isRestoring -> LoadingScreen()
        state.currentUser == null -> LoginScreen(state, viewModel)
        state.assignedDriver == null -> DriverBindingScreen(state, viewModel)
        else -> ActivityScreen(state, viewModel)
    }
}

private data class MobileSetupLink(
    val apiUrl: String,
    val token: String
)

private fun Intent?.toMobileSetupLink(): MobileSetupLink? {
    val uri = this?.data ?: return null
    if (uri.scheme != "drivertime" || uri.host != "mobile-setup") {
        return null
    }

    val apiUrl = uri.getQueryParameter("apiUrl")?.takeIf { it.isNotBlank() } ?: return null
    val token = uri.getQueryParameter("token")?.takeIf { it.isNotBlank() } ?: return null

    return MobileSetupLink(apiUrl = apiUrl, token = token)
}

@Composable
private fun LoadingScreen() {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        verticalArrangement = Arrangement.Center
    ) {
        Text("DriverTime", style = MaterialTheme.typography.headlineMedium)
        Spacer(Modifier.height(16.dp))
        LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
    }
}

@Composable
private fun LoginScreen(state: AppUiState, viewModel: DriverTimeViewModel) {
    var baseUrl by remember { mutableStateOf("https://") }
    var email by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }

    Scaffold(
        topBar = { DriverTimeTopBar(title = "DriverTime") }
    ) { padding ->
        LazyColumn(
            modifier = Modifier
                .padding(padding)
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            item {
                Text("Logowanie", style = MaterialTheme.typography.headlineSmall)
                Text(
                    "Podaj adres systemu DriverTime i dane konta. Telefon zostanie później przypisany do jednego kierowcy.",
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            item {
                OutlinedTextField(
                    value = baseUrl,
                    onValueChange = { baseUrl = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Adres DriverTime") },
                    singleLine = true
                )
            }
            item {
                OutlinedTextField(
                    value = email,
                    onValueChange = { email = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("E-mail") },
                    singleLine = true
                )
            }
            item {
                OutlinedTextField(
                    value = password,
                    onValueChange = { password = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Hasło") },
                    visualTransformation = PasswordVisualTransformation(),
                    singleLine = true
                )
            }
            item {
                Button(
                    onClick = { viewModel.login(baseUrl, email, password) },
                    modifier = Modifier.fillMaxWidth(),
                    enabled = !state.isBusy && baseUrl.trim().length > 8 && email.isNotBlank() && password.isNotBlank()
                ) {
                    Text(if (state.isBusy) "Łączenie..." else "Zaloguj")
                }
            }
            state.errorMessage?.let { error ->
                item { ErrorCard(error) }
            }
        }
    }
}

@Composable
private fun DriverBindingScreen(state: AppUiState, viewModel: DriverTimeViewModel) {
    var search by remember { mutableStateOf("") }
    val drivers = remember(state.drivers, search) {
        val value = search.trim().lowercase()
        if (value.isBlank()) {
            state.drivers
        } else {
            state.drivers.filter {
                it.displayName.lowercase().contains(value) || it.cardNumber.lowercase().contains(value)
            }
        }
    }

    Scaffold(
        topBar = {
            DriverTimeTopBar(
                title = "Przypisz kierowcę",
                action = { TextButton(onClick = viewModel::logout) { Text("Wyloguj") } }
            )
        }
    ) { padding ->
        LazyColumn(
            modifier = Modifier
                .padding(padding)
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            item {
                Text(
                    "Wybierz kierowcę przypisanego do tego telefonu. Aplikacja będzie synchronizować tylko jego aktywności.",
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            item {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedTextField(
                        value = search,
                        onValueChange = { search = it },
                        modifier = Modifier.weight(1f),
                        label = { Text("Szukaj") },
                        singleLine = true
                    )
                    OutlinedButton(onClick = viewModel::loadDrivers) {
                        Text("Odśwież")
                    }
                }
            }
            items(drivers, key = { it.id }) { driver ->
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    onClick = { viewModel.assignDriver(driver) }
                ) {
                    Column(Modifier.padding(16.dp)) {
                        Text(driver.displayName, fontWeight = FontWeight.Bold)
                        Text("Karta: ${driver.cardNumber}", color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }
            state.errorMessage?.let { error ->
                item { ErrorCard(error) }
            }
        }
    }
}

@Composable
private fun ActivityScreen(state: AppUiState, viewModel: DriverTimeViewModel) {
    var showForm by remember { mutableStateOf(false) }
    var showSettings by remember { mutableStateOf(false) }
    val context = LocalContext.current
    val locationPermissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestMultiplePermissions()
    ) { permissions ->
        val granted = permissions[Manifest.permission.ACCESS_FINE_LOCATION] == true ||
            permissions[Manifest.permission.ACCESS_COARSE_LOCATION] == true
        viewModel.setLocationPermissionGranted(granted)
        if (granted) {
            viewModel.startAutoTracking()
        }
    }
    val today = DriverTimeFormatters.todayApiDate()
    val todayEntries = state.month?.days?.firstOrNull { it.date == today }?.entries.orEmpty()

    LaunchedEffect(Unit) {
        val granted = ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED ||
            ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_COARSE_LOCATION) == PackageManager.PERMISSION_GRANTED
        viewModel.setLocationPermissionGranted(granted)
    }

    Scaffold(
        topBar = {
            DriverTimeTopBar(
                title = "Aktywności",
                action = {
                    Row {
                        TextButton(onClick = { showSettings = true }) { Text("Ustaw.") }
                        TextButton(onClick = { showForm = true }) { Text("Dodaj") }
                    }
                }
            )
        }
    ) { padding ->
        LazyColumn(
            modifier = Modifier
                .padding(padding)
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            item {
                Card(modifier = Modifier.fillMaxWidth()) {
                    Column(Modifier.padding(16.dp)) {
                        Text(state.assignedDriver?.displayName.orEmpty(), style = MaterialTheme.typography.titleLarge)
                        Text("Synchronizacja tylko dla tego kierowcy", color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }

            item {
                AutoTrackingCard(
                    state = state,
                    onStart = {
                        if (state.locationPermissionGranted) {
                            viewModel.startAutoTracking()
                        } else {
                            locationPermissionLauncher.launch(
                                arrayOf(
                                    Manifest.permission.ACCESS_FINE_LOCATION,
                                    Manifest.permission.ACCESS_COARSE_LOCATION
                                )
                            )
                        }
                    },
                    onStop = viewModel::stopAutoTracking
                )
            }

            item {
                TimerCard(state, viewModel)
            }

            if (state.pendingEntries.any { it.driverId == state.assignedDriver?.id }) {
                item {
                    PendingEntriesCard(state)
                }
            }

            item {
                Text("Dzisiejsze wpisy", style = MaterialTheme.typography.titleMedium)
            }

            if (todayEntries.isEmpty()) {
                item {
                    Text("Brak wpisów na dziś.", color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            } else {
                items(todayEntries, key = { it.id }) { entry ->
                    EntryCard(entry, onDelete = { viewModel.deleteEntry(entry) })
                }
            }

            state.month?.summary?.let { summary ->
                item {
                    SummaryCard(summary)
                }
            }

            state.errorMessage?.let { error ->
                item { ErrorCard(error) }
            }
        }
    }

    if (showForm) {
        EntryFormDialog(
            onDismiss = { showForm = false },
            onSave = {
                viewModel.saveEntry(it)
                showForm = false
            }
        )
    }

    if (showSettings) {
        SettingsDialog(
            state = state,
            onDismiss = { showSettings = false },
            onRefresh = viewModel::refresh,
            onChangeDriver = {
                viewModel.clearAssignedDriver()
                showSettings = false
            },
            onLogout = {
                viewModel.logout()
                showSettings = false
            }
        )
    }
}

@Composable
private fun AutoTrackingCard(
    state: AppUiState,
    onStart: () -> Unit,
    onStop: () -> Unit
) {
    val speed = state.detectedSpeedKmh?.let { "${it.roundToInt()}" } ?: "-"
    val activity = state.detectedActivityType?.label ?: "-"
    val started = state.autoActivityStartedAtMillis?.let { DriverTimeFormatters.apiTime(it) } ?: "-"
    val distance = if (state.autoDistanceKm > 0.0) {
        "${((state.autoDistanceKm * 10).roundToInt() / 10.0)} km"
    } else {
        "0.0 km"
    }
    val lastFix = state.lastGpsFixAtMillis?.let { DriverTimeFormatters.apiTime(it) } ?: "-"

    Card(modifier = Modifier.fillMaxWidth()) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Text("Automatyczna rejestracja GPS", style = MaterialTheme.typography.titleMedium)
            Text(
                "Aplikacja wykrywa jazde i postoj na podstawie predkosci GPS.",
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Text(state.autoTrackingStatus, fontWeight = FontWeight.Bold)

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                LiveMetric(
                    label = "Aktywnosc",
                    value = activity,
                    modifier = Modifier.weight(1f)
                )
                LiveMetric(
                    label = "Predkosc",
                    value = speed,
                    suffix = "km/h",
                    modifier = Modifier.weight(1f)
                )
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                LiveMetric(
                    label = "Przebieg",
                    value = distance,
                    modifier = Modifier.weight(1f)
                )
                LiveMetric(
                    label = "Odczyt GPS",
                    value = lastFix,
                    modifier = Modifier.weight(1f)
                )
            }

            Text("Start biezacego wpisu: $started", color = MaterialTheme.colorScheme.onSurfaceVariant)

            if (state.isAutoTrackingEnabled) {
                OutlinedButton(onClick = onStop, modifier = Modifier.fillMaxWidth()) {
                    Text("Zatrzymaj Auto GPS")
                }
            } else {
                Button(onClick = onStart, modifier = Modifier.fillMaxWidth()) {
                    Text("Wlacz Auto GPS")
                }
            }
        }
    }
}

@Composable
private fun LiveMetric(
    label: String,
    value: String,
    modifier: Modifier = Modifier,
    suffix: String? = null
) {
    Column(modifier = modifier.padding(4.dp)) {
        Text(label, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Row {
            Text(value, style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.Bold)
            suffix?.let {
                Text(" $it", modifier = Modifier.padding(top = 10.dp))
            }
        }
    }
}

@Composable
private fun TimerCard(state: AppUiState, viewModel: DriverTimeViewModel) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Text("Szybka rejestracja", style = MaterialTheme.typography.titleMedium)
            ActivityTypeDropdown(
                value = state.timerActivity,
                onChange = viewModel::setTimerActivity
            )

            if (state.timerStartedAtMillis == null) {
                Button(onClick = viewModel::startTimer, modifier = Modifier.fillMaxWidth()) {
                    Text("Start")
                }
            } else {
                Text("Start: ${DriverTimeFormatters.apiTime(state.timerStartedAtMillis)}")
                Button(onClick = viewModel::stopTimer, modifier = Modifier.fillMaxWidth()) {
                    Text("Zakończ i wyślij")
                }
            }
        }
    }
}

@Composable
private fun PendingEntriesCard(state: AppUiState) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Text("Oczekuje na synchronizację", style = MaterialTheme.typography.titleMedium)
            state.pendingEntries
                .filter { it.driverId == state.assignedDriver?.id }
                .forEach { pending ->
                    Text("${pending.request.activityType.label}: ${pending.request.date} ${pending.request.startTime}-${pending.request.endTime}")
                    pending.lastError?.let {
                        Text(it, color = MaterialTheme.colorScheme.error)
                    }
                    Divider()
                }
        }
    }
}

@Composable
private fun EntryCard(entry: WorkEvidenceEntry, onDelete: () -> Unit) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            Row(horizontalArrangement = Arrangement.SpaceBetween, modifier = Modifier.fillMaxWidth()) {
                Text(entry.activityType.label, fontWeight = FontWeight.Bold)
                Text(DriverTimeFormatters.minutesLabel(entry.durationMinutes))
            }
            Text("${entry.startTime.take(5)}-${entry.endTime.take(5)}")
            entry.vehicleRegistration?.let { Text("Pojazd: $it") }
            entry.description?.let { Text(it, color = MaterialTheme.colorScheme.onSurfaceVariant) }
            OutlinedButton(onClick = onDelete) {
                Text("Usuń")
            }
        }
    }
}

@Composable
private fun SummaryCard(summary: WorkEvidenceSummary) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            Text("Miesiąc", style = MaterialTheme.typography.titleMedium)
            Text("Jazda: ${DriverTimeFormatters.minutesLabel(summary.drivingMinutes)}")
            Text("Inna praca: ${DriverTimeFormatters.minutesLabel(summary.otherWorkMinutes)}")
            Text("Dyspozycyjność: ${DriverTimeFormatters.minutesLabel(summary.availabilityMinutes)}")
            Text("Razem: ${DriverTimeFormatters.minutesLabel(summary.totalTrackedMinutes)}")
        }
    }
}

@Composable
private fun EntryFormDialog(
    onDismiss: () -> Unit,
    onSave: (WorkEvidenceEntryRequest) -> Unit
) {
    var date by remember { mutableStateOf(DriverTimeFormatters.todayApiDate()) }
    var startTime by remember { mutableStateOf("08:00") }
    var endTime by remember { mutableStateOf("16:00") }
    var endsNextDay by remember { mutableStateOf(false) }
    var activityType by remember { mutableStateOf(ActivityType.Driving) }
    var vehicle by remember { mutableStateOf("") }
    var country by remember { mutableStateOf("PL") }
    var distance by remember { mutableStateOf("") }
    var description by remember { mutableStateOf("") }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Nowy wpis") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(value = date, onValueChange = { date = it }, label = { Text("Data YYYY-MM-DD") })
                OutlinedTextField(value = startTime, onValueChange = { startTime = it }, label = { Text("Od HH:mm") })
                OutlinedTextField(value = endTime, onValueChange = { endTime = it }, label = { Text("Do HH:mm") })
                Row {
                    Checkbox(
                        checked = endsNextDay,
                        onCheckedChange = { endsNextDay = it }
                    )
                    Text("Kończy się następnego dnia", modifier = Modifier.padding(top = 12.dp))
                }
                ActivityTypeDropdown(value = activityType, onChange = { activityType = it })
                OutlinedTextField(value = vehicle, onValueChange = { vehicle = it }, label = { Text("Pojazd") })
                OutlinedTextField(value = country, onValueChange = { country = it }, label = { Text("Kraj") })
                OutlinedTextField(value = distance, onValueChange = { distance = it }, label = { Text("Kilometry") })
                OutlinedTextField(value = description, onValueChange = { description = it }, label = { Text("Opis") })
            }
        },
        confirmButton = {
            Button(onClick = {
                onSave(
                    WorkEvidenceEntryRequest(
                        date = date,
                        startTime = startTime,
                        endTime = endTime,
                        endsNextDay = endsNextDay,
                        activityType = activityType,
                        vehicleRegistration = vehicle.ifBlank { null },
                        countryCode = country.ifBlank { null },
                        distanceKm = distance.replace(",", ".").toDoubleOrNull(),
                        description = description.ifBlank { null }
                    )
                )
            }) {
                Text("Zapisz")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Anuluj") }
        }
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ActivityTypeDropdown(value: ActivityType, onChange: (ActivityType) -> Unit) {
    var expanded by remember { mutableStateOf(false) }

    ExposedDropdownMenuBox(
        expanded = expanded,
        onExpandedChange = { expanded = !expanded }
    ) {
        OutlinedTextField(
            value = value.label,
            onValueChange = {},
            modifier = Modifier
                .menuAnchor()
                .fillMaxWidth(),
            readOnly = true,
            label = { Text("Aktywność") },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded) }
        )
        ExposedDropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false }
        ) {
            ActivityType.entries.forEach { type ->
                DropdownMenuItem(
                    text = { Text(type.label) },
                    onClick = {
                        onChange(type)
                        expanded = false
                    }
                )
            }
        }
    }
}

@Composable
private fun SettingsDialog(
    state: AppUiState,
    onDismiss: () -> Unit,
    onRefresh: () -> Unit,
    onChangeDriver: () -> Unit,
    onLogout: () -> Unit
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Ustawienia") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("Kierowca: ${state.assignedDriver?.displayName.orEmpty()}")
                Text("Oczekujące wpisy: ${state.pendingEntries.count { it.driverId == state.assignedDriver?.id }}")
                Text("Zmiana kierowcy odblokuje ponowny wybór przypisania telefonu.")
            }
        },
        confirmButton = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Button(onClick = onRefresh) { Text("Synchronizuj") }
                OutlinedButton(onClick = onChangeDriver) { Text("Zmień kierowcę") }
                OutlinedButton(onClick = onLogout) { Text("Wyloguj") }
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Zamknij") }
        }
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun DriverTimeTopBar(
    title: String,
    action: @Composable (() -> Unit)? = null
) {
    TopAppBar(
        title = { Text(title) },
        actions = { action?.invoke() }
    )
}

@Composable
private fun ErrorCard(message: String) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Text(
            text = message,
            modifier = Modifier.padding(16.dp),
            color = MaterialTheme.colorScheme.error
        )
    }
}
