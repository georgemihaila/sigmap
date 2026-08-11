package com.sigmap.android.ui

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.sigmap.android.ScannerStatus
import com.sigmap.android.ScannerViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SigmapApp(viewModel: ScannerViewModel) {
    val state by viewModel.state.collectAsState()
    MaterialTheme {
        Surface {
            when (state.status) {
                ScannerStatus.NotPaired, ScannerStatus.Error -> PairingScreen(viewModel, state)
                else -> ScannerScreen(viewModel, state)
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun PairingScreen(viewModel: ScannerViewModel, state: com.sigmap.android.ScannerUiState) {
    Column(
        modifier = Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("Pair with your Sigmap server", style = MaterialTheme.typography.headlineSmall)
        OutlinedTextField(
            value = state.host,
            onValueChange = viewModel::setHost,
            label = { Text("Host (e.g. http://192.168.1.5:5080)") },
            singleLine = true,
            modifier = Modifier.fillMaxWidth(),
        )
        OutlinedTextField(
            value = state.token,
            onValueChange = viewModel::setToken,
            label = { Text("Pairing token (from the QR code)") },
            singleLine = true,
            modifier = Modifier.fillMaxWidth(),
        )
        OutlinedTextField(
            value = state.deviceName,
            onValueChange = viewModel::setDeviceName,
            label = { Text("Device name") },
            singleLine = true,
            modifier = Modifier.fillMaxWidth(),
        )
        state.error?.let {
            Text(it, color = MaterialTheme.colorScheme.error)
        }
        Button(onClick = viewModel::pair, modifier = Modifier.fillMaxWidth()) {
            Text("Pair device")
        }
        Text(
            "Tip: point the server's Pairing page at this device and approve it after pairing.",
            style = MaterialTheme.typography.bodySmall,
            modifier = Modifier.align(Alignment.CenterHorizontally),
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ScannerScreen(viewModel: ScannerViewModel, state: com.sigmap.android.ScannerUiState) {
    Column(
        modifier = Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        Text("Sigmap scanner", style = MaterialTheme.typography.headlineSmall)
        ListItem(
            headlineContent = { Text(state.deviceName.ifBlank { "Android device" }) },
            supportingContent = { Text("Session ${state.sessionId.ifBlank { "—" }} · ${state.pairedStatus}") },
        )
        ListItem(
            headlineContent = { Text("Status: ${if (state.scanning) "Scanning" else "Idle"}") },
            supportingContent = { Text("Detections sent: ${state.detectionsSent}") },
        )
        Button(
            onClick = { if (state.scanning) viewModel.stopScanning() else viewModel.startScanning() },
            modifier = Modifier.fillMaxWidth(),
        ) {
            Text(if (state.scanning) "Stop scanning" else "Start scanning")
        }
        Text(
            "Reports WiFi/BT/BLE detections and GPS fixes to the backend every few seconds.",
            style = MaterialTheme.typography.bodySmall,
        )
    }
}
