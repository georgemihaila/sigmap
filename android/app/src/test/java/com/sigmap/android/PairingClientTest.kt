package com.sigmap.android

import org.junit.Assert.assertEquals
import org.junit.Test

class PairingClientTest {
    @Test
    fun tokenHash_is_sha256_hex() {
        // sha256("sigmap-dev-token") hex, matching the backend expectation.
        assertEquals(
            "a6d92c0452adb2c7a613c867dd7da58a03967d78f4b481a8680fbd665faea85e",
            PairingClient.tokenHash("sigmap-dev-token"),
        )
    }

    @Test
    fun tokenHash_is_lowercase_hex() {
        assertEquals(64, PairingClient.tokenHash("anything").length)
        assertEquals(PairingClient.tokenHash("x").lowercase(), PairingClient.tokenHash("x"))
    }
}
