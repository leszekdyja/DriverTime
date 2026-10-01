package pl.drivertime.mobile

import android.annotation.SuppressLint
import android.content.Context
import android.location.Location
import android.os.Looper
import com.google.android.gms.location.FusedLocationProviderClient
import com.google.android.gms.location.LocationCallback
import com.google.android.gms.location.LocationRequest
import com.google.android.gms.location.LocationResult
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority

class GpsActivityTracker(
    context: Context,
    private val onLocation: (Location) -> Unit,
    private val onError: (Throwable) -> Unit
) {
    private val client: FusedLocationProviderClient =
        LocationServices.getFusedLocationProviderClient(context.applicationContext)

    private val request = LocationRequest.Builder(Priority.PRIORITY_HIGH_ACCURACY, 10_000L)
        .setMinUpdateIntervalMillis(5_000L)
        .setMinUpdateDistanceMeters(10f)
        .build()

    private val callback = object : LocationCallback() {
        override fun onLocationResult(result: LocationResult) {
            result.locations.forEach(onLocation)
        }
    }

    @SuppressLint("MissingPermission")
    fun start() {
        client.requestLocationUpdates(request, callback, Looper.getMainLooper())
            .addOnFailureListener(onError)
    }

    fun stop() {
        client.removeLocationUpdates(callback)
    }
}
