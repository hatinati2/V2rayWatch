package com.linkwatch.app.data

import org.junit.Assert.assertEquals
import org.junit.Test

class UptimeHistoryTest {
    @Test
    fun emptyHistoryRendersFiftyUnknownCells() {
        val slots = uptimeHistorySlots(emptyList())
        assertEquals(UPTIME_HISTORY_LIMIT, slots.size)
        assertEquals(List(UPTIME_HISTORY_LIMIT) { UptimeMark.UNKNOWN }, slots)
    }

    @Test
    fun newResultsAreAppendedOnTheRight() {
        val history = appendUptimeMark(
            appendUptimeMark(emptyList(), UptimeMark.CONNECTED),
            UptimeMark.DISCONNECTED
        )
        val slots = uptimeHistorySlots(history)

        assertEquals(UPTIME_HISTORY_LIMIT, slots.size)
        assertEquals(UptimeMark.UNKNOWN, slots[UPTIME_HISTORY_LIMIT - 3])
        assertEquals(UptimeMark.CONNECTED, slots[UPTIME_HISTORY_LIMIT - 2])
        assertEquals(UptimeMark.DISCONNECTED, slots[UPTIME_HISTORY_LIMIT - 1])
    }

    @Test
    fun historyKeepsOnlyTheFiftyMostRecentResultsInOrder() {
        val source = (1..65).map {
            if (it % 2 == 0) UptimeMark.CONNECTED else UptimeMark.DISCONNECTED
        }
        val history = source.fold(emptyList<UptimeMark>()) { acc, mark ->
            appendUptimeMark(acc, mark)
        }

        assertEquals(UPTIME_HISTORY_LIMIT, history.size)
        assertEquals(source.takeLast(UPTIME_HISTORY_LIMIT), history)
        assertEquals(source.last(), history.last())
    }
}