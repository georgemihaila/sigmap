package com.sigmap.android

import android.util.Log
import java.net.HttpURLConnection
import java.net.URL
import java.util.UUID
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import sigmap.DetectionOuterClass.Detection
import sigmap.DetectionOuterClass.DetectionBatch
import sigmap.EnvelopeOuterClass.Envelope
import sigmap.DetectionOuterClass.GpsSample

/**
 * Buffers detections and flushes a batch to the backend every few seconds —
 * the HTTPS equivalent of the scanner agent's RabbitMQ flush. Batch ids are
 * unique so the backend's idempotency gate dedupes any redelivery.
 */
class IngestClient(private val host: String, private val deviceId: String) {

    private val buffer = mutableListOf<Detection>()
    private val gpsSamples = mutableListOf<GpsSample>()
    private val mutex = Mutex()
    private var sentTotal = 0L

    suspend fun add(detection: Detection, gps: GpsSample?) {
        mutex.withLock {
            buffer.add(detection)
            gps?.let { gpsSamples.add(it) }
        }
    }

    suspend fun run(active: () -> Boolean) {
        while (active()) {
            val batch = mutex.withLock {
                if (buffer.isEmpty() && gpsSamples.isEmpty()) return@withLock null
                val b = DetectionBatch.newBuilder()
                    .setDeviceId(deviceId)
                    .setBatchId(UUID.randomUUID().toString())
                    .setHasGps(gpsSamples.isNotEmpty())
                    .addAllGpsSamples(gpsSamples)
                    .addAllDetections(buffer)
                    .build()
                buffer.clear()
                gpsSamples.clear()
                b
            }
            if (batch != null) {
                try {
                    post(batch)
                    sentTotal += batch.detectionsCount
                } catch (e: Exception) {
                    Log.w("IngestClient", "Flush failed (will retry next cycle): ${e.message}")
                    // Re-queue so nothing is silently lost.
                    mutex.withLock {
                        buffer.addAll(0, batch.detectionsList)
                        gpsSamples.addAll(0, batch.gpsSamplesList)
                    }
                }
            }
            delay(2000)
        }
    }

    private suspend fun post(batch: DetectionBatch) = withContext(Dispatchers.IO) {
        val url = URL("${host.trimEnd('/')}/api/v1/ingest/batch")
        val connection = (url.openConnection() as HttpURLConnection).apply {
            connectTimeout = 15_000
            readTimeout = 30_000
            requestMethod = "POST"
            doOutput = true
            setRequestProperty("Content-Type", "application/x-protobuf")
        }
        try {
            val envelope = Envelope.newBuilder()
                .setMessageId(batch.batchId)
                .setSchemaVersion("1")
                .setSentAtUnixMs(System.currentTimeMillis())
                .setDetectionBatch(batch)
                .build()
            connection.outputStream.use { it.write(envelope.toByteArray()) }
            val status = connection.responseCode
            if (status !in 200..299) {
                throw IllegalStateException("Ingest HTTP $status")
            }
        } finally {
            connection.disconnect()
        }
    }

    suspend fun sentTotal() = mutex.withLock { sentTotal }
}
