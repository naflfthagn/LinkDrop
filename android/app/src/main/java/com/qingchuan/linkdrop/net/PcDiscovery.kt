package com.qingchuan.linkdrop.net

import android.content.Context
import android.net.wifi.WifiManager
import android.util.Log
import com.qingchuan.linkdrop.data.NearbyPc
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import org.json.JSONObject
import java.net.DatagramPacket
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.MulticastSocket
import java.nio.charset.StandardCharsets

class PcDiscovery(
    private val context: Context,
    private val onPeer: (NearbyPc) -> Unit,
    private val onActiveChanged: (Boolean) -> Unit
) {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private var job: Job? = null
    private var multicastLock: WifiManager.MulticastLock? = null

    fun start() {
        if (job?.isActive == true) return
        acquireLock()
        job = scope.launch {
            while (currentCoroutineContext().isActive) {
                var socket: MulticastSocket? = null
                try {
                    socket = MulticastSocket(null)
                    socket.reuseAddress = true
                    // Force an IPv4 wildcard listener. Without an explicit IPv4 bind
                    // Android may create an IPv6-only socket and miss Windows beacons.
                    socket.bind(InetSocketAddress(InetAddress.getByName("0.0.0.0"), Wire.DISCOVERY_PORT))
                    try {
                        val group = InetAddress.getByName(Wire.MULTICAST_GROUP)
                        socket.joinGroup(InetSocketAddress(group, Wire.DISCOVERY_PORT), null)
                    } catch (e: Exception) {
                        Log.w(TAG, "Multicast join failed; broadcast fallback remains", e)
                    }
                    socket.soTimeout = 1200
                    onActiveChanged(true)
                    val buffer = ByteArray(4096)
                    while (currentCoroutineContext().isActive) {
                        try {
                            val packet = DatagramPacket(buffer, buffer.size)
                            socket.receive(packet)
                            parse(packet)
                        } catch (_: java.net.SocketTimeoutException) {
                            // Keep listening.
                        }
                    }
                } catch (e: CancellationException) {
                    throw e
                } catch (e: Exception) {
                    onActiveChanged(false)
                    Log.w(TAG, "Discovery socket error", e)
                    delay(2400)
                } finally {
                    try { socket?.close() } catch (_: Exception) {}
                }
            }
        }
    }

    fun stop() {
        job?.cancel()
        job = null
        onActiveChanged(false)
        releaseLock()
    }

    fun restart() {
        stop()
        start()
    }

    private fun parse(packet: DatagramPacket) {
        try {
            val text = String(packet.data, packet.offset, packet.length, StandardCharsets.UTF_8)
            val json = JSONObject(text)
            if (json.optString("m") != Wire.MAGIC) return
            if (json.optString("kind") != "beacon") return
            if (json.optString("platform") != "windows") return
            val id = json.optString("id")
            val port = json.optInt("port", 0)
            if (id.length < 8 || port !in 1..65535) return
            val host = packet.address?.hostAddress ?: return
            if (host.contains(':')) return
            val peer = NearbyPc(
                id = id,
                name = json.optString("name").ifBlank { "Windows 电脑" }.take(40),
                host = host,
                port = port,
                pairing = json.optBoolean("pairing", false),
                lastSeen = System.currentTimeMillis()
            )
            Log.i(TAG, "discovered " + peer.name + " @ " + host + ":" + port + " pairing=" + peer.pairing)
            onPeer(peer)
        } catch (_: Exception) {
            // Ignore unrelated LAN traffic.
        }
    }

    private fun acquireLock() {
        if (multicastLock?.isHeld == true) return
        try {
            val wifi = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager
            val lock = wifi?.createMulticastLock("LinkDropDiscovery")
            lock?.setReferenceCounted(false)
            lock?.acquire()
            multicastLock = lock
        } catch (e: Exception) {
            Log.w(TAG, "Could not acquire multicast lock", e)
        }
    }

    private fun releaseLock() {
        try { if (multicastLock?.isHeld == true) multicastLock?.release() } catch (_: Exception) {}
        multicastLock = null
    }

    private companion object {
        const val TAG = "LinkDropDiscovery"
    }
}
