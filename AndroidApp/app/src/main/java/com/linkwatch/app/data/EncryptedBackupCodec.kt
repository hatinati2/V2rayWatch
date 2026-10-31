package com.linkwatch.app.data

import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.security.SecureRandom
import java.util.Arrays
import javax.crypto.AEADBadTagException
import javax.crypto.Cipher
import javax.crypto.SecretKeyFactory
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.PBEKeySpec
import javax.crypto.spec.SecretKeySpec

/**
 * Portable authenticated backup encryption. The backup key is derived from a user
 * passphrase and is deliberately independent of Android Keystore so backups can
 * be restored on another device.
 */
object EncryptedBackupCodec {
    private val magic = "V2VWBAK1".toByteArray(Charsets.US_ASCII)
    private const val FORMAT_VERSION: Byte = 1
    private const val SALT_SIZE = 16
    private const val NONCE_SIZE = 12
    private const val TAG_BITS = 128
    private const val MIN_ITERATIONS = 300_000
    private const val ITERATIONS = 600_000
    private const val MAX_ITERATIONS = 1_000_000
    private const val HEADER_SIZE = 8 + 1 + 4 + SALT_SIZE + NONCE_SIZE
    const val MAX_BACKUP_BYTES = 25 * 1024 * 1024
    const val MIN_PASSPHRASE_LENGTH = 12

    private val random = SecureRandom()

    fun encrypt(plaintext: ByteArray, passphrase: CharArray): ByteArray {
        requirePassphrase(passphrase)
        require(plaintext.isNotEmpty() && plaintext.size <= MAX_BACKUP_BYTES) {
            "Backup content is empty or too large"
        }

        val salt = ByteArray(SALT_SIZE).also(random::nextBytes)
        val nonce = ByteArray(NONCE_SIZE).also(random::nextBytes)
        val header = ByteBuffer.allocate(HEADER_SIZE).order(ByteOrder.BIG_ENDIAN)
            .put(magic)
            .put(FORMAT_VERSION)
            .putInt(ITERATIONS)
            .put(salt)
            .put(nonce)
            .array()

        val key = deriveKey(passphrase, salt, ITERATIONS)
        return try {
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(Cipher.ENCRYPT_MODE, key, GCMParameterSpec(TAG_BITS, nonce))
            cipher.updateAAD(header)
            val ciphertext = cipher.doFinal(plaintext)
            require(header.size + ciphertext.size <= MAX_BACKUP_BYTES) { "Encrypted backup is too large" }
            header + ciphertext
        } finally {
            key.encoded?.let { Arrays.fill(it, 0) }
        }
    }

    fun decrypt(backup: ByteArray, passphrase: CharArray): ByteArray {
        requirePassphrase(passphrase)
        require(backup.size in (HEADER_SIZE + TAG_BITS / 8)..MAX_BACKUP_BYTES) {
            "Backup file is truncated or too large"
        }

        val header = backup.copyOfRange(0, HEADER_SIZE)
        val input = ByteBuffer.wrap(header).order(ByteOrder.BIG_ENDIAN)
        val actualMagic = ByteArray(magic.size).also(input::get)
        require(actualMagic.contentEquals(magic)) { "This is not a V2rayWatch backup file" }
        val version = input.get()
        require(version == FORMAT_VERSION) { "This backup format version is not supported" }
        val iterations = input.int
        require(iterations in MIN_ITERATIONS..MAX_ITERATIONS) { "Invalid backup encryption parameters" }
        val salt = ByteArray(SALT_SIZE).also(input::get)
        val nonce = ByteArray(NONCE_SIZE).also(input::get)
        val ciphertext = backup.copyOfRange(HEADER_SIZE, backup.size)

        val key = deriveKey(passphrase, salt, iterations)
        return try {
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(Cipher.DECRYPT_MODE, key, GCMParameterSpec(TAG_BITS, nonce))
            cipher.updateAAD(header)
            cipher.doFinal(ciphertext)
        } catch (_: AEADBadTagException) {
            throw IllegalArgumentException("Wrong passphrase or damaged backup file")
        } finally {
            key.encoded?.let { Arrays.fill(it, 0) }
        }
    }

    private fun requirePassphrase(passphrase: CharArray) {
        require(passphrase.size >= MIN_PASSPHRASE_LENGTH) {
            "Use a backup passphrase with at least $MIN_PASSPHRASE_LENGTH characters"
        }
    }

    private fun deriveKey(passphrase: CharArray, salt: ByteArray, iterations: Int): SecretKeySpec {
        val spec = PBEKeySpec(passphrase, salt, iterations, 256)
        return try {
            val bytes = SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256")
                .generateSecret(spec).encoded
            try {
                SecretKeySpec(bytes, "AES")
            } finally {
                Arrays.fill(bytes, 0)
            }
        } finally {
            spec.clearPassword()
        }
    }
}