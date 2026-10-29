package com.qingchuan.linkdrop.net

import android.content.Context
import android.net.Uri
import android.os.StatFs
import android.provider.OpenableColumns
import android.util.Log
import com.qingchuan.linkdrop.data.PairedPeer
import com.qingchuan.linkdrop.data.TransferDirection
import com.qingchuan.linkdrop.data.TransferInfo
import com.qingchuan.linkdrop.data.TransferStatus
import com.qingchuan.linkdrop.storage.DownloadStore
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withTimeoutOrNull
import org.json.JSONObject
import java.io.BufferedInputStream
import java.io.BufferedOutputStream
import java.io.File
import java.io.FileInputStream
import java.io.FileOutputStream
import java.io.IOException
import java.io.InputStream
import java.security.MessageDigest
import java.security.SecureRandom
import java.util.concurrent.ConcurrentHashMap

class AuthException(message: String) : IOException(message)

class PeerClient(
    context: Context,
    private val listener: Listener
) {
    interface Listener {
        fun onDisconnected(reason: String)
        fun onTransferChanged(info: TransferInfo)
        fun onNotice(message: String, error: Boolean)
    }

    private data class Acceptance(val accepted: Boolean, val reason: String)

    private data class FileAck(val ok: Boolean, val reason: String)

    private class Incoming(
        val id: Long,
        val name: String,
        val mime: String,
        val size: Long,
        val temp: File,
        val output: BufferedOutputStream,
        val digest: MessageDigest
    ) {
        var received: Long = 0
        var speedClock: Long = System.nanoTime()
        var lastUpdate: Long = 0
        var item: TransferInfo? = null
    }

    private class SendSource(
        val name: String,
        val size: Long,
        val mime: String,
        val uri: Uri?,
        val tempFile: File?
    )

    private val appContext = context.applicationContext
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val sendMutex = Mutex()
    private val offerWaiters = ConcurrentHashMap<Long, CompletableDeferred<Acceptance>>()
    private val ackWaiters = ConcurrentHashMap<Long, CompletableDeferred<FileAck>>()
    private val incoming = ConcurrentHashMap<Long, Incoming>()
    private val random = SecureRandom()

    @Volatile private var channel: SecureChannel? = null
    @Volatile private var readerJob: Job? = null
    @Volatile private var pingJob: Job? = null
    @Volatile private var connected = false
    @Volatile private var intentionalClose = false

    @Volatile var localDeviceId: String = ""
    @Volatile var localDeviceName: String = ""

    val isConnected: Boolean get() = connected

    suspend fun connect(peer: PairedPeer, host: String, port: Int): String {
        disconnect()
        intentionalClose = false
        val secure = SecureChannel.open(host, port, peer.pin, 15_000)
        channel = secure
        try {
            val hello = JSONObject()
                .put("op", "hello")
                .put("v", Wire.VERSION)
                .put("deviceId", localDeviceId)
                .put("deviceName", localDeviceName)
                .put("platform", "android")
                .put("token", peer.token)
            secure.protocol.sendJson(hello)
            val reply = secure.protocol.readFrame()
            if (reply !is Frame.Json) throw IOException("电脑返回了无效的握手响应")
            val obj = reply.obj
            if (obj.optString("op") == "error") {
                throw AuthException(obj.optString("message").ifBlank { "电脑拒绝了连接" })
            }
            if (obj.optString("op") != "welcome") throw IOException("电脑返回了未知的握手响应")
            secure.socket.soTimeout = 60_000
            connected = true
            startLoops(secure)
            return obj.optString("deviceName").ifBlank { peer.name }
        } catch (e: Exception) {
            try { secure.closeQuietly() } catch (_: Exception) {}
            if (channel === secure) channel = null
            throw e
        }
    }

    fun disconnect() {
        intentionalClose = true
        shutdown("手机已断开连接", false, false)
    }

    fun enqueueSend(uris: List<Uri>) {
        if (!connected) {
            listener.onNotice("电脑未连接，暂时不能发送文件", true)
            return
        }
        if (uris.isEmpty()) return
        scope.launch {
            sendMutex.withLock {
                for (uri in uris) {
                    if (!connected) {
                        listener.onNotice("连接已断开，发送已取消", true)
                        return@withLock
                    }
                    try {
                        sendOne(uri)
                    } catch (e: Exception) {
                        Log.w(TAG, "Send failed", e)
                        listener.onNotice("发送失败：" + (e.message ?: "未知错误"), true)
                    }
                }
            }
        }
    }

    private suspend fun sendOne(uri: Uri) {
        val source = prepareSource(uri)
        var lastItem: TransferInfo? = null
        var transferId = 0L
        try {
            transferId = newTransferId()
            var item = TransferInfo(
                id = transferId,
                name = source.name,
                size = source.size,
                direction = TransferDirection.Outgoing,
                status = TransferStatus.Waiting,
                mime = source.mime
            )
            lastItem = item
            listener.onTransferChanged(item)
            val offer = JSONObject()
                .put("op", "offer")
                .put("id", transferId.toString())
                .put("name", source.name)
                .put("size", source.size)
                .put("mime", source.mime)
            val acceptance = CompletableDeferred<Acceptance>()
            offerWaiters[transferId] = acceptance
            try {
                sendJson(offer)
                val accepted = withTimeoutOrNull(45_000) { acceptance.await() }
                    ?: throw IOException("等待电脑接受文件超时")
                if (!accepted.accepted) throw IOException(accepted.reason.ifBlank { "电脑拒绝了文件" })
            } finally {
                offerWaiters.remove(transferId)
            }

            item = item.copy(status = TransferStatus.Transferring)
            listener.onTransferChanged(item)
            val digest = MessageDigest.getInstance("SHA-256")
            var sent = 0L
            var lastUpdate = 0L
            val started = System.nanoTime()
            openSource(source).use { input ->
                val buffer = ByteArray(Wire.CHUNK_SIZE)
                while (true) {
                    if (!connected) throw IOException("连接已断开")
                    val read = input.read(buffer)
                    if (read <= 0) break
                    digest.update(buffer, 0, read)
                    sendChunk(transferId, buffer, read)
                    sent += read
                    val now = System.nanoTime()
                    if (now - lastUpdate > 120_000_000L || sent == source.size) {
                        val speed = if (now > started) sent / ((now - started) / 1_000_000_000.0) else 0.0
                        item = item.copy(bytes = sent, speed = speed)
                        listener.onTransferChanged(item)
                        lastUpdate = now
                    }
                }
            }
            if (sent != source.size) throw IOException("文件大小在发送过程中发生变化")
            val sha = hashHex(digest.digest())
            item = item.copy(bytes = source.size, status = TransferStatus.Verifying, speed = 0.0)
            listener.onTransferChanged(item)
            val doneWaiter = CompletableDeferred<FileAck>()
            ackWaiters[transferId] = doneWaiter
            try {
                val done = JSONObject()
                    .put("op", "done")
                    .put("id", transferId.toString())
                    .put("sha256", sha)
                sendJson(done)
                val ack = withTimeoutOrNull(30_000) { doneWaiter.await() }
                    ?: throw IOException("等待电脑确认超时")
                if (!ack.ok) throw IOException(ack.reason.ifBlank { "电脑校验文件失败" })
            } finally {
                ackWaiters.remove(transferId)
            }
            item = item.copy(status = TransferStatus.Completed, bytes = source.size, speed = 0.0)
            listener.onTransferChanged(item)
        } catch (e: Exception) {
            lastItem?.let {
                listener.onTransferChanged(it.copy(status = TransferStatus.Failed, error = e.message ?: "发送失败", speed = 0.0))
            }
            if (transferId != 0L) sendFail(transferId, e.message ?: "发送失败")
            throw e
        } finally {
            cleanupSource(source)
        }
    }

    private fun prepareSource(uri: Uri): SendSource {
        var name = "文件"
        var declaredSize = -1L
        try {
            appContext.contentResolver.query(
                uri,
                arrayOf(OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE),
                null, null, null
            )?.use { cursor ->
                if (cursor.moveToFirst()) {
                    val ni = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME)
                    val si = cursor.getColumnIndex(OpenableColumns.SIZE)
                    if (ni >= 0 && !cursor.isNull(ni)) name = cursor.getString(ni)
                    if (si >= 0 && !cursor.isNull(si)) declaredSize = cursor.getLong(si)
                }
            }
        } catch (_: Exception) {}
        val mime = Safety.guessMime(name, appContext.contentResolver.getType(uri))
        val safeName = Safety.safeFileName(name)
        if (declaredSize >= 0) {
            return SendSource(safeName, declaredSize, mime, uri, null)
        }
        val tempDir = File(appContext.cacheDir, "outgoing").apply { mkdirs() }
        val temp = File(tempDir, "send-" + System.nanoTime() + ".tmp")
        appContext.contentResolver.openInputStream(uri)?.use { input ->
            BufferedOutputStream(FileOutputStream(temp)).use { output ->
                input.copyTo(output, 128 * 1024)
            }
        } ?: throw IOException("无法读取所选文件")
        return SendSource(safeName, temp.length(), mime, null, temp)
    }

    private fun openSource(source: SendSource): InputStream {
        val temp = source.tempFile
        val stream = if (source.uri != null) {
            appContext.contentResolver.openInputStream(source.uri)
        } else if (temp != null) {
            FileInputStream(temp)
        } else {
            null
        }
        if (stream == null) throw IOException("无法读取文件")
        return BufferedInputStream(stream, 64 * 1024)
    }

    private fun cleanupSource(source: SendSource) {
        val temp = source.tempFile
        if (temp != null) {
            try { temp.delete() } catch (_: Exception) {}
        }
    }

    private fun startLoops(secure: SecureChannel) {
        readerJob = scope.launch {
            try {
                while (connected && channel === secure) {
                    val frame = secure.protocol.readFrame()
                    if (channel !== secure) break
                    if (frame is Frame.Json) dispatch(frame.obj) else handleBinary(frame as Frame.Binary)
                }
            } catch (e: Exception) {
                if (!intentionalClose && channel === secure) {
                    Log.w(TAG, "Reader stopped", e)
                    shutdown("电脑连接已断开", true, true)
                }
            }
        }
        pingJob = scope.launch {
            try {
                while (connected && channel === secure) {
                    delay(15_000)
                    if (channel !== secure) break
                    val ping = JSONObject()
                        .put("op", "ping")
                        .put("t", System.currentTimeMillis())
                    sendJson(ping)
                }
            } catch (_: Exception) {
                if (!intentionalClose && channel === secure) shutdown("网络连接已断开", true, true)
            }
        }
    }

    private fun dispatch(obj: JSONObject) {
        when (obj.optString("op")) {
            "ping" -> {
                try {
                    sendJson(JSONObject().put("op", "pong").put("t", obj.optLong("t")))
                } catch (_: Exception) {}
            }
            "pong" -> Unit
            "offer" -> handleOffer(obj)
            "accept" -> {
                val id = obj.optString("id").toLongOrNull() ?: return
                offerWaiters[id]?.complete(Acceptance(true, ""))
            }
            "reject" -> {
                val id = obj.optString("id").toLongOrNull() ?: return
                offerWaiters[id]?.complete(Acceptance(false, obj.optString("reason")))
            }
            "done" -> handleDone(obj)
            "complete" -> {
                val id = obj.optString("id").toLongOrNull() ?: return
                ackWaiters[id]?.complete(FileAck(true, ""))
            }
            "fail" -> {
                val id = obj.optString("id").toLongOrNull() ?: return
                val reason = obj.optString("message").ifBlank { "传输失败" }
                ackWaiters[id]?.complete(FileAck(false, reason))
                abortIncoming(id, reason)
            }
            "error" -> listener.onNotice(obj.optString("message").ifBlank { "电脑返回错误" }, true)
        }
    }

    private fun handleOffer(obj: JSONObject) {
        val id = obj.optString("id").toLongOrNull() ?: return
        val name = Safety.safeFileName(obj.optString("name"))
        val size = obj.optLong("size", -1L)
        val mime = Safety.guessMime(name, obj.optString("mime"))
        if (size < 0 || size > 1024L * 1024 * 1024 * 1024) {
            sendReject(id, "文件信息无效")
            return
        }
        try {
            val stat = StatFs(appContext.filesDir.absolutePath)
            if (stat.availableBytes < size + 32L * 1024 * 1024) {
                sendReject(id, "手机存储空间不足")
                return
            }
        } catch (_: Exception) {}
        if (incoming.containsKey(id)) {
            sendReject(id, "传输编号冲突")
            return
        }
        var entry: Incoming? = null
        try {
            val dir = File(appContext.cacheDir, "incoming").apply { mkdirs() }
            val temp = File(dir, "recv-" + id + ".part")
            val output = BufferedOutputStream(FileOutputStream(temp), 128 * 1024)
            entry = Incoming(id, name, mime, size, temp, output, MessageDigest.getInstance("SHA-256"))
            entry.item = TransferInfo(
                id = id,
                name = name,
                size = size,
                direction = TransferDirection.Incoming,
                status = TransferStatus.Transferring,
                mime = mime
            )
            incoming[id] = entry
            listener.onTransferChanged(entry.item!!)
            sendJson(JSONObject().put("op", "accept").put("id", id.toString()))
        } catch (e: Exception) {
            if (entry != null) {
                incoming.remove(id)
                try { entry.output.close() } catch (_: Exception) {}
                try { entry.temp.delete() } catch (_: Exception) {}
                entry.item?.let {
                    listener.onTransferChanged(it.copy(status = TransferStatus.Failed, error = "无法准备接收", speed = 0.0))
                }
            }
            sendReject(id, "无法准备接收文件")
        }
    }

    private fun handleBinary(frame: Frame.Binary) {
        val entry = incoming[frame.id] ?: return
        if (frame.data.isEmpty()) return
        val received = entry.received + frame.data.size
        if (received > entry.size) {
            abortIncoming(entry.id, "接收数据超过文件大小")
            return
        }
        try {
            entry.output.write(frame.data)
            entry.digest.update(frame.data)
            entry.received = received
            val now = System.nanoTime()
            if (now - entry.lastUpdate > 120_000_000L || received == entry.size) {
                val speed = if (now > entry.speedClock) received / ((now - entry.speedClock) / 1_000_000_000.0) else 0.0
                entry.item = entry.item?.copy(bytes = received, speed = speed)
                entry.item?.let { listener.onTransferChanged(it) }
                entry.lastUpdate = now
            }
        } catch (e: Exception) {
            abortIncoming(entry.id, "写入缓存失败：" + (e.message ?: "未知错误"))
        }
    }

    private fun handleDone(obj: JSONObject) {
        val id = obj.optString("id").toLongOrNull() ?: return
        val expected = obj.optString("sha256")
        val entry = incoming.remove(id) ?: return
        try {
            if (entry.received != entry.size) throw IOException("接收大小不一致")
            val actual = hashHex(entry.digest.digest())
            if (!actual.equals(expected, ignoreCase = true)) throw IOException("文件校验失败")
            entry.output.flush()
            entry.output.close()
            val saved = DownloadStore.publish(appContext, entry.temp, entry.name, entry.mime)
            try { entry.temp.delete() } catch (_: Exception) {}
            val finished = entry.item!!.copy(
                bytes = entry.size,
                status = TransferStatus.Completed,
                savedUri = saved.uri.toString(),
                mime = entry.mime,
                speed = 0.0
            )
            listener.onTransferChanged(finished)
            sendJson(JSONObject().put("op", "complete").put("id", id.toString()))
        } catch (e: Exception) {
            try { entry.output.close() } catch (_: Exception) {}
            try { entry.temp.delete() } catch (_: Exception) {}
            val failed = entry.item!!.copy(status = TransferStatus.Failed, error = e.message ?: "接收失败", speed = 0.0)
            listener.onTransferChanged(failed)
            sendFail(id, e.message ?: "接收失败")
        }
    }

    private fun abortIncoming(id: Long, reason: String) {
        val entry = incoming.remove(id) ?: return
        try { entry.output.close() } catch (_: Exception) {}
        try { entry.temp.delete() } catch (_: Exception) {}
        entry.item?.let {
            listener.onTransferChanged(it.copy(status = TransferStatus.Failed, error = reason, speed = 0.0))
        }
        sendFail(id, reason)
    }

    private fun sendJson(obj: JSONObject) {
        val ch = channel ?: throw IOException("连接已关闭")
        ch.protocol.sendJson(obj)
    }

    private fun sendChunk(id: Long, data: ByteArray, count: Int) {
        val ch = channel ?: throw IOException("连接已关闭")
        ch.protocol.sendChunk(id, data, 0, count)
    }

    private fun sendReject(id: Long, reason: String) {
        try { sendJson(JSONObject().put("op", "reject").put("id", id.toString()).put("reason", reason)) } catch (_: Exception) {}
    }

    private fun sendFail(id: Long, reason: String) {
        try { sendJson(JSONObject().put("op", "fail").put("id", id.toString()).put("message", reason)) } catch (_: Exception) {}
    }

    private fun hashHex(bytes: ByteArray): String {
        return bytes.joinToString("") { (it.toInt() and 0xFF).toString(16).padStart(2, '0') }
    }

    private fun shutdown(reason: String, notify: Boolean, fromReader: Boolean) {
        val wasConnected = connected
        connected = false
        val ch = channel
        channel = null
        try { ch?.closeQuietly() } catch (_: Exception) {}
        if (!fromReader) readerJob?.cancel()
        readerJob = null
        pingJob?.cancel()
        pingJob = null
        for (waiter in offerWaiters.values) waiter.complete(Acceptance(false, reason))
        offerWaiters.clear()
        for (waiter in ackWaiters.values) waiter.complete(FileAck(false, reason))
        ackWaiters.clear()
        val current = incoming.keys.toList()
        for (id in current) abortIncoming(id, reason)
        if (wasConnected && notify) listener.onDisconnected(reason)
    }

    fun close() {
        intentionalClose = true
        shutdown("应用已关闭", false, false)
        scope.cancel()
    }

    private fun newTransferId(): Long {
        var id = random.nextLong() and Long.MAX_VALUE
        if (id == 0L) id = 1L
        return id
    }

    private companion object {
        const val TAG = "LinkDropPeer"
    }
}
