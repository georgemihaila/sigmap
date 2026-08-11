package com.sigmap.android

import org.junit.Assert.assertEquals
import org.junit.Test

class AndroidScannerTest {
    @Test
    fun freqToChannel_maps_2ghz_and_5ghz() {
        assertEquals(1, AndroidScanner.freqToChannel(2412))
        assertEquals(6, AndroidScanner.freqToChannel(2437))
        assertEquals(11, AndroidScanner.freqToChannel(2462))
        assertEquals(36, AndroidScanner.freqToChannel(5180))
        assertEquals(0, AndroidScanner.freqToChannel(999))
    }
}
