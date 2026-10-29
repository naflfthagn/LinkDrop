package com.qingchuan.linkdrop.net

import com.qingchuan.linkdrop.data.PairedPeer
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.io.IOException

data class PairingOutcome(val peer: PairedPeer)

class PairingClient {
    @Volatile private var channel: SecureChannel? = null

    fun close() {
        val ch = channel
        channel = null
        try { ch?.closeQuietly() } catch (_: Exception) {}
    }

    suspend fun pair(
        host: String,
        port: Int,
        deviceId: String,
        deviceName: String,
        onCode: (String) -> Unit,
        onPending: () -> Unit
    ): PairingOutcome = withContext(Dispatchers.IO) {
        val secure = SecureChannel.open(host, port, null, 120_000)
        channel = secure
        try {
            val pin = secure.peerPin
            val code = SecurityCodes.fromFingerprint(pin)
            onCode(code)
            val request = JSONObject()
                .put("op", "pair_request")
                .put("v", Wire.VERSION)
                .put("deviceId", deviceId)
                .put("deviceName", deviceName)
                .put("platform", "android")
            secure.protocol.sendJson(request)
            while (true) {
                val frame = secure.protocol.readFrame()
                if (frame !is Frame.Json) continue
                val obj = frame.obj
                when (obj.optString("op")) {
                    "pair_pending" -> onPending()
                    "pair_result" -> {
                        if (!obj.optBoolean("ok", false)) {
                            throw IOException(obj.optString("error").ifBlank { "电脑拒绝了配对" })
                        }
                        val token = obj.optString("token")
                        val serverPin = obj.optString("serverPin")
                        val serverId = obj.optString("serverId")
                        val serverName = obj.optString("serverName").ifBlank { "Windows 电脑" }
                        if (token.length != 64) throw IOException("电脑返回的配对令牌无效")
                        if (serverPin.isNotBlank() && !serverPin.equals(pin, ignoreCase = true)) {
                            throw IOException("配对安全码不匹配")
                        }
                        val peer = PairedPeer(
                            id = serverId.ifBlank { pin.substring(0, 16) },
                            name = serverName,
                            pin = pin,
                            token = token,
                            host = host,
                            port = port
                        )
                        return@withContext PairingOutcome(peer)
                    }
                    "error" -> throw IOException(obj.optString("message").ifBlank { "电脑拒绝了配对" })
                }
            }
            @Suppress("UNREACHABLE_CODE")
            throw IOException("配对失败")
        } finally {
            try { secure.closeQuietly() } catch (_: Exception) {}
            if (channel === secure) channel = null
        }
    }
}
