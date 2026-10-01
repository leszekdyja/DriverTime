package pl.drivertime.mobile

import java.time.Instant
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter

object DriverTimeFormatters {
    private val dateFormatter = DateTimeFormatter.ISO_LOCAL_DATE
    private val timeFormatter = DateTimeFormatter.ofPattern("HH:mm")

    fun todayApiDate(): String = LocalDate.now().format(dateFormatter)

    fun apiDate(epochMillis: Long): String =
        Instant.ofEpochMilli(epochMillis)
            .atZone(ZoneId.systemDefault())
            .toLocalDate()
            .format(dateFormatter)

    fun apiTime(epochMillis: Long): String =
        Instant.ofEpochMilli(epochMillis)
            .atZone(ZoneId.systemDefault())
            .toLocalTime()
            .format(timeFormatter)

    fun apiTime(hour: Int, minute: Int): String =
        LocalTime.of(hour, minute).format(timeFormatter)

    fun minutesLabel(minutes: Int): String {
        if (minutes <= 0) return "-"
        return "${minutes / 60}:${(minutes % 60).toString().padStart(2, '0')}"
    }

    fun currentYearMonth(): Pair<Int, Int> {
        val now = LocalDate.now()
        return now.year to now.monthValue
    }
}
