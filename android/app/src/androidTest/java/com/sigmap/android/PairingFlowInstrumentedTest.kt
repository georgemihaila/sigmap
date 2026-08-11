package com.sigmap.android

import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.net.HttpURLConnection
import java.net.URL
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Instrumented pairing-flow test: exercises the pairing client against a
 * real backend. Runs in CI on an emulator with the backend up.
 */
@RunWith(AndroidJUnit4::class)
class PairingFlowInstrumentedTest {

    @Test
    fun pairingRequest_reaches_backend() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        // Overridable so CI can point at the compose backend.
        val host = System.getProperty("sigmap.test.host") ?: "http://10.0.2.2:5080"
        val token = System.getProperty("sigmap.test.token") ?: "sigmap-dev-token"

        val deviceId = "test-" + java.util.UUID.randomUUID()
        val client = PairingClient()
        val response = kotlinx.coroutines.runBlocking {
            client.request(host, token, deviceId, "instrumented-test")
        }
        // Approved devices return a session; pending/rejected still yield a status.
        assertTrue(response.statusNumber in 0..4)
    }
}
