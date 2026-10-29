package com.qingchuan.linkdrop.net

import android.webkit.MimeTypeMap
import org.json.JSONObject
import java.io.BufferedInputStream
import java.io.BufferedOutputStream
import java.io.DataInputStream
import java.io.DataOutputStream
import java.io.InputStream
import java.io.OutputStream
import java.nio.charset.StandardCharsets
import java.security.MessageDigest

object Wire {
    const val VERSION = 1
    const val DISCOVERY_PORT = 48721
    const val DEFAULT_PORT = 48622
    const val MULTICAST_GROUP = "239.255.47.22"
    const val MAX_FRAME = 4 * 1024 * 1024
    const val CHUNK_SIZE = 256 * 1024
    const val MAGIC = "QCLINK1"
    const val JSON_KIND = 1
    const val BINARY_KIND = 2
}

sealed class Frame {
    data class Json(val obj: JSONObject) : Frame()
    data class Binary(val id: Long, val data: ByteArray) : Frame()
}

class ProtocolException(message: String, cause: Throwable? = null) : Exception(message, cause)

class ProtocolStream(input: InputStream, output: OutputStream) {
    private val input = DataInputStream(BufferedInputStream(input, 64 * 1024))
    private val output = DataOutputStream(BufferedOutputStream(output, 64 * 1024))
    private val writeLock = Any()

    @Volatile
    var closed: Boolean = false
        private set

    fun markClosed() {
        closed = true
    }

    fun sendJson(obj: JSONObject) {
        val payload = obj.toString().toByteArray(StandardCharsets.UTF_8)
        if (payload.size + 1 > Wire.MAX_FRAME) throw ProtocolException("控制消息过大")
        synchronized(writeLock) {
            ensureOpen()
            output.writeInt(payload.size + 1)
            output.writeByte(Wire.JSON_KIND)
            output.write(payload)
            output.flush()
        }
    }

    fun sendChunk(id: Long, data: ByteArray, offset: Int, count: Int) {
        if (count < 0 || count > Wire.CHUNK_SIZE) throw ProtocolException("文件块大小非法")
        synchronized(writeLock) {
            ensureOpen()
            output.writeInt(1 + 8 + 4 + count)
            output.writeByte(Wire.BINARY_KIND)
            output.writeLong(id)
            output.writeInt(count)
            output.write(data, offset, count)
            output.flush()
        }
    }

    fun readFrame(): Frame {
        val length = (input.readInt().toLong() and 0xFFFFFFFFL).toLong()
        if (length < 2 || length > Wire.MAX_FRAME.toLong()) throw ProtocolException("数据帧长度异常: $length")
        val kind = input.readUnsignedByte()
        if (kind == Wire.JSON_KIND) {
            val payload = ByteArray((length - 1).toInt())
            input.readFully(payload)
            try {
                return Frame.Json(JSONObject(String(payload, StandardCharsets.UTF_8)))
            } catch (e: Exception) {
                throw ProtocolException("JSON 消息无效", e)
            }
        }
        if (kind == Wire.BINARY_KIND) {
            if (length < 13) throw ProtocolException("文件块帧过短")
            val id = input.readLong()
            val chunkLength = (input.readInt().toLong() and 0xFFFFFFFFL).toInt()
            if (chunkLength < 0 || chunkLength > length - 13) throw ProtocolException("文件块长度异常")
            val data = ByteArray(chunkLength)
            input.readFully(data)
            return Frame.Binary(id, data)
        }
        throw ProtocolException("未知帧类型")
    }

    private fun ensureOpen() {
        if (closed) throw ProtocolException("连接已关闭")
    }
}

object SecurityCodes {
    fun fromFingerprint(pin: String): String {
        if (pin.length < 8) return "000000"
        val value = pin.substring(0, 8).toLong(16)
        return (value % 1_000_000L).toString().padStart(6, '0')
    }

    fun display(code: String): String {
        if (code.length != 6) return code
        return code.substring(0, 3) + "  " + code.substring(3)
    }
}

object Safety {
    fun sha256Hex(bytes: ByteArray): String {
        val digest = MessageDigest.getInstance("SHA-256").digest(bytes)
        return digest.joinToString("") { "%02x".format(it) }
    }

    fun safeFileName(name: String, fallback: String = "文件"): String {
        val cleaned = name
            .replace('\\', '_')
            .replace('/', '_')
            .filter { it != '\u0000' && it.code >= 32 }
            .trim()
            .trimEnd('.')
            .ifBlank { fallback }
        val limited = if (cleaned.length > 150) {
            val dot = cleaned.lastIndexOf('.')
            if (dot in 1..15) {
                cleaned.substring(0, 140) + cleaned.substring(dot)
            } else {
                cleaned.take(150)
            }
        } else cleaned
        return if (limited == "." || limited == "..") fallback else limited
    }

    fun guessMime(name: String, offered: String?): String {
        if (!offered.isNullOrBlank() && offered != "application/octet-stream") return offered
        val ext = name.substringAfterLast('.', "").lowercase()
        if (ext.isEmpty()) return "application/octet-stream"
        return MimeTypeMap.getSingleton().getMimeTypeFromExtension(ext) ?: "application/octet-stream"
    }
}

fun formatBytes(bytes: Long): String {
    if (bytes < 0) return "—"
    if (bytes < 1024) return "$bytes B"
    val kb = bytes / 1024.0
    if (kb < 1024) return String.format("%.1f KB", kb)
    val mb = kb / 1024.0
    if (mb < 1024) return String.format("%.1f MB", mb)
    return String.format("%.2f GB", mb / 1024.0)
}
