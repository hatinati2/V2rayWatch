package com.linkwatch.app.data

import android.content.ContentValues
import android.content.Context
import android.database.sqlite.SQLiteDatabase
import android.database.sqlite.SQLiteOpenHelper

private const val HISTORY_RETENTION_MS = 7L * 24L * 60L * 60L * 1000L
private const val HISTORY_PRUNE_INTERVAL_MS = 60L * 60L * 1000L

/**
 * Local-only probe history. It stores profile IDs and probe outcomes, never proxy
 * links, credentials, hosts, or configuration JSON. Data remains inside app storage.
 */
class ConnectionHistoryStore(context: Context) :
    SQLiteOpenHelper(context.applicationContext, "connection_history.db", null, 1) {

    @Volatile private var lastPruneAtEpochMs: Long = 0L

    override fun onCreate(db: SQLiteDatabase) {
        db.execSQL(
            """
            CREATE TABLE connection_checks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id TEXT NOT NULL,
                session_id TEXT NOT NULL,
                checked_at_ms INTEGER NOT NULL,
                success INTEGER NOT NULL CHECK(success IN (0, 1)),
                delay_ms INTEGER
            )
            """.trimIndent()
        )
        db.execSQL(
            "CREATE INDEX index_connection_checks_profile_time " +
                "ON connection_checks(profile_id, checked_at_ms)"
        )
    }

    override fun onUpgrade(db: SQLiteDatabase, oldVersion: Int, newVersion: Int) {
        // Schema version 1 is the first durable history schema.
    }

    @Synchronized
    fun record(sample: ConnectionHistorySample, profileId: String) {
        require(profileId.isNotBlank()) { "Profile ID is required" }
        require(sample.sessionId.isNotBlank()) { "Monitoring session ID is required" }
        val values = ContentValues().apply {
            put("profile_id", profileId)
            put("session_id", sample.sessionId)
            put("checked_at_ms", sample.checkedAtEpochMs)
            put("success", if (sample.success) 1 else 0)
            if (sample.success && sample.delayMs != null && sample.delayMs >= 0L) {
                put("delay_ms", sample.delayMs)
            } else {
                putNull("delay_ms")
            }
        }
        writableDatabase.insertOrThrow("connection_checks", null, values)

        val now = System.currentTimeMillis()
        if (now - lastPruneAtEpochMs >= HISTORY_PRUNE_INTERVAL_MS) {
            pruneExpired(now - HISTORY_RETENTION_MS)
            lastPruneAtEpochMs = now
        }
    }

    @Synchronized
    fun read(profileId: String, fromEpochMs: Long, toEpochMs: Long): List<ConnectionHistorySample> {
        val samples = mutableListOf<ConnectionHistorySample>()
        readableDatabase.query(
            "connection_checks",
            arrayOf("session_id", "checked_at_ms", "success", "delay_ms"),
            "profile_id = ? AND checked_at_ms >= ? AND checked_at_ms <= ?",
            arrayOf(profileId, fromEpochMs.toString(), toEpochMs.toString()),
            null,
            null,
            "checked_at_ms ASC, id ASC"
        ).use { cursor ->
            while (cursor.moveToNext()) {
                samples += ConnectionHistorySample(
                    sessionId = cursor.getString(0),
                    checkedAtEpochMs = cursor.getLong(1),
                    success = cursor.getInt(2) == 1,
                    delayMs = if (cursor.isNull(3)) null else cursor.getLong(3)
                )
            }
        }
        return samples
    }

    @Synchronized
    fun deleteProfile(profileId: String) {
        writableDatabase.delete("connection_checks", "profile_id = ?", arrayOf(profileId))
    }

    @Synchronized
    fun deleteAll() {
        writableDatabase.delete("connection_checks", null, null)
    }

    @Synchronized
    fun pruneExpired(cutoffEpochMs: Long) {
        writableDatabase.delete(
            "connection_checks",
            "checked_at_ms < ?",
            arrayOf(cutoffEpochMs.toString())
        )
        lastPruneAtEpochMs = System.currentTimeMillis()
    }
}
