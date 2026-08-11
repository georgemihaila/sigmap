package com.sigmap.android

import android.annotation.SuppressLint
import android.content.Context
import android.location.Location
import android.location.LocationManager
import sigmap.DetectionOuterClass.GpsSample

/** Emits a GPS fix from the platform location providers. */
class GpsProvider(private val context: Context) {

    @SuppressLint("MissingPermission")
    fun currentFix(): GpsSample? {
        val manager = context.applicationContext.getSystemService(Context.LOCATION_SERVICE) as LocationManager
        val providers = listOf(LocationManager.GPS_PROVIDER, LocationManager.NETWORK_PROVIDER)
        val best = providers.mapNotNull { runCatching { manager.getLastKnownLocation(it) }.getOrNull() }
            .maxByOrNull { it.accuracy }

        return best?.let(::toSample)
    }

    private fun toSample(location: Location): GpsSample = GpsSample.newBuilder()
        .setLat(location.latitude)
        .setLon(location.longitude)
        .setAccuracyM(location.accuracy.toDouble())
        .setAtUnixMs(System.currentTimeMillis())
        .build()
}
