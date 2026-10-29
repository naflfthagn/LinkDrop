package com.qingchuan.linkdrop.data

import android.content.Context
import android.os.Build
import java.security.SecureRandom

class AppSettings(context: Context) {
    private val prefs = context.getSharedPreferences("linkdrop", Context.MODE_PRIVATE)

    var deviceName: String
        get() {
            val saved = prefs.getString(KEY_DEVICE_NAME, null)
            if (!saved.isNullOrBlank()) return saved
            val manufacturer = Build.MANUFACTURER.orEmpty().trim()
            val model = Build.MODEL.orEmpty().trim()
            if (manufacturer.isBlank() ||
                manufacturer.equals("unknown", ignoreCase = true) ||
                model.contains("sdk", ignoreCase = true) ||
                Build.FINGERPRINT.contains("generic", ignoreCase = true)
            ) {
                return "Android 手机"
            }
            val name = if (model.startsWith(manufacturer, ignoreCase = true)) {
                model
            } else {
                "$manufacturer $model"
            }
            return name.ifBlank { "Android 手机" }.take(40)
        }
        set(value) {
            prefs.edit().putString(KEY_DEVICE_NAME, value.trim().take(40)).apply()
        }

    val deviceId: String
        get() {
            val saved = prefs.getString(KEY_DEVICE_ID, null)
            if (!saved.isNullOrBlank()) return saved
            val bytes = ByteArray(8)
            SecureRandom().nextBytes(bytes)
            val id = bytes.joinToString("") { (it.toInt() and 0xFF).toString(16).padStart(2, '0') }
            prefs.edit().putString(KEY_DEVICE_ID, id).apply()
            return id
        }

    fun loadPaired(): PairedPeer? {
        val id = prefs.getString(KEY_PEER_ID, null) ?: return null
        val name = prefs.getString(KEY_PEER_NAME, null) ?: return null
        val pin = prefs.getString(KEY_PEER_PIN, null) ?: return null
        val token = prefs.getString(KEY_PEER_TOKEN, null) ?: return null
        val host = prefs.getString(KEY_PEER_HOST, null) ?: return null
        val port = prefs.getInt(KEY_PEER_PORT, 48622)
        if (id.length < 8 || token.length != 64 || pin.length != 64 || port !in 1..65535) return null
        return PairedPeer(id, name, pin, token, host, port)
    }

    fun savePaired(peer: PairedPeer) {
        prefs.edit()
            .putString(KEY_PEER_ID, peer.id)
            .putString(KEY_PEER_NAME, peer.name)
            .putString(KEY_PEER_PIN, peer.pin)
            .putString(KEY_PEER_TOKEN, peer.token)
            .putString(KEY_PEER_HOST, peer.host)
            .putInt(KEY_PEER_PORT, peer.port)
            .apply()
    }

    fun updateEndpoint(host: String, port: Int) {
        prefs.edit().putString(KEY_PEER_HOST, host).putInt(KEY_PEER_PORT, port).apply()
    }

    fun clearPaired() {
        prefs.edit()
            .remove(KEY_PEER_ID)
            .remove(KEY_PEER_NAME)
            .remove(KEY_PEER_PIN)
            .remove(KEY_PEER_TOKEN)
            .remove(KEY_PEER_HOST)
            .remove(KEY_PEER_PORT)
            .apply()
    }

    companion object {
        private const val KEY_DEVICE_NAME = "device_name"
        private const val KEY_DEVICE_ID = "device_id"
        private const val KEY_PEER_ID = "peer_id"
        private const val KEY_PEER_NAME = "peer_name"
        private const val KEY_PEER_PIN = "peer_pin"
        private const val KEY_PEER_TOKEN = "peer_token"
        private const val KEY_PEER_HOST = "peer_host"
        private const val KEY_PEER_PORT = "peer_port"
    }
}
