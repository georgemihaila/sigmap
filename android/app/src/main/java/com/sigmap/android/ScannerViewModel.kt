package com.sigmap.android

import android.app.Application
import android.content.Context
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import sigmap.Pairing.PairingResponse
import sigmap.Pairing.PairingStatus

enum class ScannerStatus { NotPaired, Paired, Scanning, Error }

data class ScannerUiState(
    val status: ScannerStatus = ScannerStatus.NotPaired,
    val host: String = "",
    val token: String = "",
    val deviceId: String = "",
    val deviceName: String = "",
    val sessionId: String = "",
    val pairedStatus: String = "",
    val error: String? = null,
    val detectionsSent: Long = 0,
    val scanning: Boolean = false,
)

class ScannerViewModel(application: Application) : AndroidViewModel(application) {

    private val prefs = application.getSharedPreferences("sigmap", Context.MODE_PRIVATE)
    private val pairingClient = PairingClient()

    private val _state = MutableStateFlow(ScannerUiState().loadPrefs(prefs))
    val state: StateFlow<ScannerUiState> = _state.asStateFlow()

    private var scanJob: Job? = null
    private var ingest: IngestClient? = null

    fun setHost(host: String) { _state.value = _state.value.copy(host = host) }
    fun setToken(token: String) { _state.value = _state.value.copy(token = token) }
    fun setDeviceName(name: String) { _state.value = _state.value.copy(deviceName = name) }

    fun pair() {
        val s = _state.value
        if (s.host.isBlank() || s.token.isBlank()) {
            _state.value = s.copy(error = "Host and token are required")
            return
        }
        viewModelScope.launch {
            val deviceId = s.deviceId.ifBlank { java.util.UUID.randomUUID().toString() }
            _state.value = s.copy(deviceId = deviceId, error = null, status = ScannerStatus.Paired)
            try {
                val response: PairingResponse =
                    pairingClient.request(s.host, s.token, deviceId, s.deviceName.ifBlank { "android-${deviceId.take(8)}" })
                val status = when (response.status) {
                    PairingStatus.PAIRING_APPROVED -> "Approved"
                    PairingStatus.PAIRING_REJECTED -> "Rejected"
                    PairingStatus.PAIRING_TOKEN_EXPIRED -> "Token expired"
                    else -> "Pending approval"
                }
                _state.value = _state.value.copy(
                    deviceId = deviceId,
                    sessionId = response.sessionId,
                    pairedStatus = status,
                    status = ScannerStatus.Paired,
                    error = null,
                )
                savePrefs()
            } catch (e: Exception) {
                _state.value = _state.value.copy(status = ScannerStatus.Error, error = e.message)
            }
        }
    }

    fun startScanning() {
        if (scanJob?.isActive == true) return
        val app = getApplication<Application>()
        val s = _state.value
        val ingestClient = IngestClient(s.host, s.deviceId)
        ingest = ingestClient
        val scanner = AndroidScanner(app)
        val gps = GpsProvider(app)

        scanJob = viewModelScope.launch {
            _state.value = _state.value.copy(scanning = true, error = null)
            scanner.startBleScan { detection ->
                viewModelScope.launch {
                    val gpsFix = gps.currentFix()
                    ingestClient.add(detection, gpsFix)
                }
            }
            launch {
                while (true) {
                    val gpsFix = gps.currentFix()
                    for (detection in scanner.scanWifi()) {
                        ingestClient.add(detection, gpsFix)
                    }
                    delay(5000)
                }
            }
            ingestClient.run { _state.value.scanning }
            scanner.stopBleScan()
        }
    }

    fun stopScanning() {
        scanJob?.cancel()
        scanJob = null
        _state.value = _state.value.copy(scanning = false)
    }

    private fun savePrefs() {
        val s = _state.value
        prefs.edit().putString("host", s.host).putString("token", s.token)
            .putString("deviceId", s.deviceId).putString("deviceName", s.deviceName).apply()
    }

    private fun ScannerUiState.loadPrefs(p: android.content.SharedPreferences): ScannerUiState =
        if (p.getString("host", null) != null) {
            copy(
                host = p.getString("host", "") ?: "",
                token = p.getString("token", "") ?: "",
                deviceId = p.getString("deviceId", "") ?: "",
                deviceName = p.getString("deviceName", "") ?: "",
                status = ScannerStatus.Paired,
            )
        } else this

    override fun onCleared() {
        scanJob?.cancel()
        super.onCleared()
    }
}
