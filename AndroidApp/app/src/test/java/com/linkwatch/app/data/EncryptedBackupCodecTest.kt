package com.linkwatch.app.data

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertThrows
import org.junit.Test

class EncryptedBackupCodecTest {
    @Test
    fun backupRoundTripsWithTheSamePassphrase() {
        val plaintext = "profiles-and-settings".toByteArray()
        val passphrase = "a-long-private-backup-passphrase".toCharArray()

        val encrypted = EncryptedBackupCodec.encrypt(plaintext, passphrase)
        val restored = EncryptedBackupCodec.decrypt(encrypted, passphrase)

        assertArrayEquals(plaintext, restored)
        assertNotEquals(plaintext.toList(), encrypted.toList())
    }

    @Test
    fun wrongPassphraseCannotDecryptBackup() {
        val encrypted = EncryptedBackupCodec.encrypt(
            "sensitive profile data".toByteArray(),
            "correct-passphrase-123".toCharArray()
        )

        assertThrows(IllegalArgumentException::class.java) {
            EncryptedBackupCodec.decrypt(encrypted, "incorrect-passphrase".toCharArray())
        }
    }

    @Test
    fun tamperedBackupIsRejected() {
        val encrypted = EncryptedBackupCodec.encrypt(
            "sensitive profile data".toByteArray(),
            "correct-passphrase-123".toCharArray()
        )
        encrypted[encrypted.lastIndex] = (encrypted.last().toInt() xor 0x01).toByte()

        assertThrows(IllegalArgumentException::class.java) {
            EncryptedBackupCodec.decrypt(encrypted, "correct-passphrase-123".toCharArray())
        }
    }

    @Test
    fun shortPassphraseIsRejected() {
        assertThrows(IllegalArgumentException::class.java) {
            EncryptedBackupCodec.encrypt("data".toByteArray(), "too-short".toCharArray())
        }
    }

    @Test
    fun unknownBackupHeaderIsRejected() {
        assertThrows(IllegalArgumentException::class.java) {
            EncryptedBackupCodec.decrypt(ByteArray(80), "a-long-private-backup-passphrase".toCharArray())
        }
    }
}