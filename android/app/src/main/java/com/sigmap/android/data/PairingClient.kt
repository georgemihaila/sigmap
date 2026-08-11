package com.sigmap.android

import java.net.HttpURLConnection
import java.net.URL
import java.security.MessageDigest
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import sigmap.EnvelopeOuterClass.Envelope
import sigmap.Pairing.PairingRequest
import sigmap.Pairing.PairingResponse

/**
 * Pairs this device with the backend over the same protobuf Envelope used on
 * the message bus (HTTPS ingest path for Android).
 */
class PairingClient {

    suspend fun request(host: String, token: String, deviceId: String, deviceName: String): PairingResponse =
        withContext(Dispatchers.IO) {
            val url = URL("${host.trimEnd('/')}/api/v1/pairing/request")
            val connection = (url.openConnection() as HttpURLConnection).apply {
                connectTimeout = 15_000
                readTimeout = 30_000
                requestMethod = "POST"
                doOutput = true
                setRequestProperty("Content-Type", "application/x-protobuf")
            }
            try {
                val request = PairingRequest.newBuilder()
                    .setDeviceId(deviceId)
                    .setDeviceName(deviceName)
                    .setTokenHash(tokenHash(token))
                    .setSentAtUnixMs(System.currentTimeMillis())
                    .build()
                val envelope = Envelope.newBuilder()
                    .setMessageId(deviceId)
                    .setSchemaVersion("1")
                    .setSentAtUnixMs(System.currentTimeMillis())
                    .setPairingRequest(request)
                    .build()

                connection.outputStream.use { it.write(envelope.toByteArray()) }
                val status = connection.responseCode
                if (status !in 200..299) {
                    val error = connection.errorStream?.bufferedReader()?.readText()
                    throw IllegalStateException("Pairing failed (HTTP $status): ${error?.take(200)}")
                }
                val bytes = connection.inputStream.use { it.readBytes() }
                Envelope.parseFrom(bytes).pairingResponse
            } finally {
                connection.disconnect()
            }
        }

    companion object {
        fun tokenHash(token: String): String {
            val digest = MessageDigest.getInstance("SHA-256").digest(token.toByteArray())
            return digest.joinToString("") { "%02x".format(it) }
        }
    }
}
