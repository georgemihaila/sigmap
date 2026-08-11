package com.sigmap.android

import android.annotation.SuppressLint
import android.bluetooth.BluetoothAdapter
import android.bluetooth.le.ScanCallback
import android.bluetooth.le.ScanResult
import android.content.Context
import android.net.wifi.WifiManager
import android.util.Log
import sigmap.DetectionOuterClass.Detection
import sigmap.DetectionOuterClass.DeviceType

/**
 * Android-side scanner: periodic WiFi scan results and BT/BLE advertisement
 * callbacks, mapped onto the same protobuf Detection contract used by the
 * scanner agent. Android 9+ WiFi scans require location permission; BT scanning
 * requires BLUETOOTH_SCAN.
 */
class AndroidScanner(private val context: Context) {

    @SuppressLint("MissingPermission")
    fun scanWifi(): List<Detection> {
        val wifi = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
        return wifi.scanResults.map { result ->
            Detection.newBuilder()
                .setDeviceType(DeviceType.AP)
                .setMac(result.BSSID ?: "")
                .setSsid(result.SSID ?: "")
                .setSignalDbm(result.level)
                .setChannel(freqToChannel(result.frequency))
                .setDetectedAtUnixMs(System.currentTimeMillis())
                .build()
        }.filter { it.mac.isNotEmpty() }
    }

    @SuppressLint("MissingPermission")
    fun startBleScan(onResult: (Detection) -> Unit) {
        val adapter = BluetoothAdapter.getDefaultAdapter() ?: return
        val scanner = adapter.bluetoothLeScanner ?: return
        val callback = object : ScanCallback() {
            override fun onScanResult(callbackType: Int, result: ScanResult) {
                val device = result.device
                onResult(
                    Detection.newBuilder()
                        .setDeviceType(DeviceType.BT_LE)
                        .setMac(device.address ?: "")
                        .setBtUuid(device.uuids?.firstOrNull()?.uuid?.toString() ?: "")
                        .setSignalDbm(result.rssi)
                        .setDetectedAtUnixMs(System.currentTimeMillis())
                        .build()
                )
            }
        }
        scanner.startScan(callback)
        bleCallback = callback
        bleScanner = scanner
    }

    @SuppressLint("MissingPermission")
    fun stopBleScan() {
        bleCallback?.let { bleScanner?.stopScan(it) }
        bleCallback = null
        bleScanner = null
    }

    private var bleCallback: ScanCallback? = null
    private var bleScanner: android.bluetooth.le.BluetoothLeScanner? = null

    companion object {
        private const val TAG = "AndroidScanner"

        fun freqToChannel(freqMhz: Int): Int =
            when {
                freqMhz in 2412..2484 -> (freqMhz - 2412) / 5 + 1
                freqMhz in 5180..5905 -> (freqMhz - 5180) / 5 + 36
                else -> 0
            }
    }
}
