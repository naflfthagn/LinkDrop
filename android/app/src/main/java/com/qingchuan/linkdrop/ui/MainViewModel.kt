package com.qingchuan.linkdrop.ui

import android.app.Application
import android.content.Intent
import android.net.ConnectivityManager
import android.net.Network
import android.net.Uri
import android.os.Handler
import android.os.Looper
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.qingchuan.linkdrop.data.AppSettings
import com.qingchuan.linkdrop.data.ConnectionPhase
import com.qingchuan.linkdrop.data.HomeUiState
import com.qingchuan.linkdrop.data.NearbyPc
import com.qingchuan.linkdrop.data.PairedPeer
import com.qingchuan.linkdrop.data.PairingPhase
import com.qingchuan.linkdrop.data.PairingState
import com.qingchuan.linkdrop.data.TransferInfo
import com.qingchuan.linkdrop.data.TransferStatus
import com.qingchuan.linkdrop.data.UiEvent
import com.qingchuan.linkdrop.net.AuthException
import com.qingchuan.linkdrop.net.PairingClient
import com.qingchuan.linkdrop.net.PairingOutcome
import com.qingchuan.linkdrop.net.PcDiscovery
import com.qingchuan.linkdrop.net.PeerClient
import com.qingchuan.linkdrop.net.PinnedCertificateException
import com.qingchuan.linkdrop.net.SecurityCodes
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.channels.BufferOverflow
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import java.util.concurrent.ConcurrentHashMap

class MainViewModel(application: Application) : AndroidViewModel(application) {
    private val app = application.applicationContext
    private val settings = AppSettings(app)
    private val mainHandler = Handler(Looper.getMainLooper())
    private val nearby = ConcurrentHashMap<String, NearbyPc>()
    private val _ui = MutableStateFlow(HomeUiState())
    val ui = _ui.asStateFlow()
    private val _events = MutableSharedFlow<UiEvent>(
        extraBufferCapacity = 16,
        onBufferOverflow = BufferOverflow.DROP_OLDEST
    )
    val events = _events.asSharedFlow()

    private val client = PeerClient(app, object : PeerClient.Listener {
        override fun onDisconnected(reason: String) {
            onMain {
                val current = _ui.value
                if (current.paired == null) return@onMain
                _ui.value = current.copy(
                    connection = ConnectionPhase.Searching,
                    connectionMessage = "连接已断开，正在自动重连",
                    peerName = null,
                    peerAddress = null
                )
                emitEvent(UiEvent.Message("电脑连接已断开，正在重连", true))
            }
        }

        override fun onTransferChanged(info: TransferInfo) {
            onMain { mergeTransfer(info) }
        }

        override fun onNotice(message: String, error: Boolean) {
            emitEvent(UiEvent.Message(message, error))
        }
    })

    private var discovery: PcDiscovery? = null
    private var pairingClient: PairingClient? = null
    private var pairingJob: Job? = null
    private var autoJob: Job? = null
    private var pruneJob: Job? = null
    private var networkCallback: ConnectivityManager.NetworkCallback? = null
    private val connectGate = Mutex()

    @Volatile private var autoPaused = false
    @Volatile private var pausedReason: String? = null

    init {
        val paired = settings.loadPaired()
        client.localDeviceId = settings.deviceId
        client.localDeviceName = settings.deviceName
        _ui.value = HomeUiState(
            deviceName = settings.deviceName,
            paired = paired,
            connection = if (paired == null) ConnectionPhase.Unpaired else ConnectionPhase.Searching,
            connectionMessage = if (paired == null) "还没有配对电脑" else "正在寻找已配对的电脑…"
        )
        startDiscovery()
        startAutoConnect()
        startPruning()
        registerNetworkMonitor()
    }

    fun setDeviceName(name: String) {
        val clean = name.trim().take(40)
        if (clean.isBlank()) return
        settings.deviceName = clean
        client.localDeviceName = clean
        _ui.value = _ui.value.copy(deviceName = clean)
    }

    fun startPairing(pc: NearbyPc) {
        val paired = _ui.value.paired
        if (paired != null) {
            if (paired.id == pc.id) {
                retryConnection()
                emitEvent(UiEvent.Message("正在重新连接 " + pc.name))
            } else {
                emitEvent(UiEvent.Message("请先在设置中解除当前配对，再连接新的电脑", true))
            }
            return
        }
        pair(pc.host, pc.port, pc.name)
    }

    fun pairManual(host: String, port: Int) {
        val trimmed = host.trim()
        if (trimmed.isBlank() || port !in 1..65535) {
            emitEvent(UiEvent.Message("IP 地址或端口无效", true))
            return
        }
        if (_ui.value.paired != null) {
            val peer = _ui.value.paired!!
            settings.updateEndpoint(trimmed, port)
            _ui.value = _ui.value.copy(paired = peer.copy(host = trimmed, port = port))
            retryConnection()
            return
        }
        pair(trimmed, port, "指定电脑")
    }

    private fun pair(host: String, port: Int, name: String) {
        if (pairingJob?.isActive == true) return
        autoPaused = false
        val pc = PairingClient()
        pairingClient = pc
        _ui.value = _ui.value.copy(
            pairing = PairingState(pcName = name, phase = PairingPhase.Connecting, message = "正在建立安全连接…"),
            lastError = null
        )
        pairingJob = viewModelScope.launch {
            try {
                val outcome = pc.pair(
                    host = host,
                    port = port,
                    deviceId = settings.deviceId,
                    deviceName = settings.deviceName,
                    onCode = { code ->
                        onMain {
                            if (pairingClient !== pc) return@onMain
                            val state = _ui.value
                            if (state.pairing != null) {
                                _ui.value = state.copy(
                                    pairing = state.pairing.copy(
                                        phase = PairingPhase.Waiting,
                                        code = code,
                                        message = "请确认电脑屏幕上的安全码与上方一致，然后在电脑上点击“允许配对”。"
                                    )
                                )
                            }
                        }
                    },
                    onPending = {
                        onMain {
                            if (pairingClient !== pc) return@onMain
                            val state = _ui.value
                            if (state.pairing != null) {
                                _ui.value = state.copy(
                                    pairing = state.pairing.copy(
                                        phase = PairingPhase.Waiting,
                                        message = "电脑已收到请求，请在电脑上核对并批准。"
                                    )
                                )
                            }
                        }
                    }
                )
                if (pairingClient !== pc) return@launch
                settings.savePaired(outcome.peer)
                _ui.value = _ui.value.copy(
                    paired = outcome.peer,
                    connection = ConnectionPhase.Searching,
                    connectionMessage = "配对成功，正在连接 " + outcome.peer.name + "…",
                    pairing = PairingState(
                        pcName = outcome.peer.name,
                        phase = PairingPhase.Success,
                        code = SecurityCodes.fromFingerprint(outcome.peer.pin),
                        message = "配对成功。以后两端启动会自动连接。"
                    ),
                    autoPaused = false,
                    lastError = null
                )
                autoPaused = false
                pausedReason = null
                delay(1600)
                val current = _ui.value
                if (current.pairing?.phase == PairingPhase.Success) {
                    _ui.value = current.copy(pairing = null, connection = ConnectionPhase.Searching)
                }
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                if (pairingClient !== pc) return@launch
                val msg = e.message ?: "配对失败，请重试"
                _ui.value = _ui.value.copy(
                    connection = if (_ui.value.paired == null) ConnectionPhase.Unpaired else _ui.value.connection,
                    pairing = PairingState(
                        pcName = name,
                        phase = PairingPhase.Failed,
                        code = null,
                        message = "配对没有完成",
                        error = msg
                    ),
                    lastError = msg
                )
            } finally {
                if (pairingClient === pc) pairingClient = null
            }
        }
    }

    fun dismissPairing() {
        val pc = pairingClient
        pairingClient = null
        try { pc?.close() } catch (_: Exception) {}
        pairingJob?.cancel()
        pairingJob = null
        val current = _ui.value
        if (current.pairing != null) {
            _ui.value = current.copy(pairing = null)
        }
    }

    fun retryConnection() {
        autoPaused = false
        pausedReason = null
        val paired = _ui.value.paired ?: return
        _ui.value = _ui.value.copy(
            connection = ConnectionPhase.Searching,
            connectionMessage = "正在连接 " + paired.name + "…",
            autoPaused = false,
            lastError = null
        )
    }

    fun unpair() {
        dismissPairing()
        client.disconnect()
        settings.clearPaired()
        autoPaused = false
        pausedReason = null
        val retained = _ui.value.nearby
        _ui.value = _ui.value.copy(
            paired = null,
            connection = ConnectionPhase.Unpaired,
            connectionMessage = "还没有配对电脑",
            peerName = null,
            peerAddress = null,
            transfers = _ui.value.transfers,
            autoPaused = false,
            lastError = null,
            nearby = retained
        )
        emitEvent(UiEvent.Message("已解除配对"))
    }

    fun sendUris(uris: List<Uri>) {
        if (uris.isEmpty()) return
        if (!client.isConnected) {
            emitEvent(UiEvent.Message("电脑未连接，暂时不能发送", true))
            return
        }
        var granted = 0
        for (uri in uris) {
            try {
                app.contentResolver.takePersistableUriPermission(uri, Intent.FLAG_GRANT_READ_URI_PERMISSION)
                granted++
            } catch (_: Exception) {}
        }
        client.enqueueSend(uris)
        emitEvent(UiEvent.Message("已加入发送队列（" + uris.size + " 个文件）"))
    }

    fun openTransfer(item: TransferInfo) {
        val uri = item.savedUri
        if (item.status != TransferStatus.Completed || uri.isNullOrBlank()) {
            if (item.status == TransferStatus.Completed) {
                emitEvent(UiEvent.Message("文件已保存到 下载/LinkDrop"))
            }
            return
        }
        _events.tryEmit(UiEvent.OpenFile(uri, item.mime))
    }

    fun refreshDiscovery() {
        if (pairingJob?.isActive == true) return
        discovery?.restart()
        emitEvent(UiEvent.Message("正在重新扫描附近的电脑"))
    }

    private fun startDiscovery() {
        if (discovery != null) return
        discovery = PcDiscovery(
            context = app,
            onPeer = { peer -> onPeerSeen(peer) },
            onActiveChanged = { active ->
                onMain { _ui.value = _ui.value.copy(discoveryActive = active) }
            }
        ).also { it.start() }
    }

    private fun onPeerSeen(peer: NearbyPc) {
        val previous = nearby.put(peer.id, peer)
        val changed = previous == null ||
            previous.name != peer.name ||
            previous.host != peer.host ||
            previous.port != peer.port ||
            previous.pairing != peer.pairing
        if (changed) {
            val list = nearby.values.sortedWith(compareBy<NearbyPc> { it.name.lowercase() })
            onMain {
                val current = _ui.value
                _ui.value = current.copy(nearby = list)
            }
        }
    }

    private fun startPruning() {
        pruneJob = viewModelScope.launch(Dispatchers.IO) {
            while (isActive) {
                delay(2000)
                val cutoff = System.currentTimeMillis() - 7000
                val removed = nearby.entries.filter { it.value.lastSeen < cutoff }
                if (removed.isNotEmpty()) {
                    for (entry in removed) nearby.remove(entry.key, entry.value)
                    val list = nearby.values.sortedWith(compareBy<NearbyPc> { it.name.lowercase() })
                    onMain { _ui.value = _ui.value.copy(nearby = list) }
                }
            }
        }
    }

    private fun startAutoConnect() {
        autoJob?.cancel()
        autoJob = viewModelScope.launch(Dispatchers.Main) {
            var lastStateNotice = 0L
            while (isActive) {
                try {
                    val current = _ui.value
                    val paired = current.paired
                    if (paired == null || current.pairing != null || client.isConnected || autoPaused) {
                        delay(1200)
                        continue
                    }
                    if (!connectGate.tryLock()) {
                        delay(500)
                        continue
                    }
                    try {
                        val target = nearby.values
                            .filter { it.id == paired.id }
                            .maxByOrNull { it.lastSeen }
                        val host = target?.host ?: paired.host
                        val port = target?.port ?: paired.port
                        if (host.isBlank() || port !in 1..65535) {
                            updateConnection(ConnectionPhase.Searching, "正在寻找已配对的电脑…")
                            delay(1500)
                            continue
                        }
                        updateConnection(ConnectionPhase.Connecting, "正在与 " + paired.name + " 建立安全连接…")
                        try {
                            val serverName = withContext(Dispatchers.IO) { client.connect(paired, host, port) }
                            val stillPaired = _ui.value.paired
                            if (stillPaired == null || stillPaired.id != paired.id) {
                                client.disconnect()
                                continue
                            }
                            settings.updateEndpoint(host, port)
                            val updatedPair = paired.copy(host = host, port = port, name = serverName)
                            settings.savePaired(updatedPair)
                            _ui.value = _ui.value.copy(
                                paired = updatedPair,
                                connection = ConnectionPhase.Connected,
                                connectionMessage = "局域网 TLS 加密直连",
                                peerName = serverName,
                                peerAddress = host,
                                autoPaused = false,
                                lastError = null
                            )
                        } catch (e: PinnedCertificateException) {
                            autoPaused = true
                            pausedReason = "电脑的安全证书发生了变化，请重新配对"
                            _ui.value = _ui.value.copy(
                                connection = ConnectionPhase.Error,
                                connectionMessage = pausedReason!!,
                                autoPaused = true,
                                lastError = pausedReason
                            )
                            emitEvent(UiEvent.Message(pausedReason!!, true))
                        } catch (e: AuthException) {
                            autoPaused = true
                            pausedReason = e.message ?: "电脑已解除配对，请重新配对"
                            _ui.value = _ui.value.copy(
                                connection = ConnectionPhase.Error,
                                connectionMessage = pausedReason!!,
                                autoPaused = true,
                                lastError = pausedReason
                            )
                            emitEvent(UiEvent.Message(pausedReason!!, true))
                        } catch (e: CancellationException) {
                            throw e
                        } catch (e: Exception) {
                            if (_ui.value.connection != ConnectionPhase.Searching) {
                                updateConnection(ConnectionPhase.Searching, "电脑未上线，正在等待自动重连…")
                            }
                            val now = System.currentTimeMillis()
                            if (now - lastStateNotice > 30_000 && _ui.value.paired != null) {
                                lastStateNotice = now
                            }
                            delay(2600)
                        }
                    } finally {
                        connectGate.unlock()
                    }
                } catch (e: CancellationException) {
                    throw e
                } catch (e: Exception) {
                    delay(2000)
                }
                delay(1100)
            }
        }
    }

    private fun updateConnection(phase: ConnectionPhase, message: String) {
        val current = _ui.value
        if (current.connection == phase && current.connectionMessage == message) return
        _ui.value = current.copy(connection = phase, connectionMessage = message)
    }

    private fun mergeTransfer(info: TransferInfo) {
        val current = _ui.value
        val existing = current.transfers.firstOrNull { it.id == info.id }
        val updated = current.transfers.toMutableList()
        val index = updated.indexOfFirst { it.id == info.id }
        if (index >= 0) updated[index] = info else updated.add(0, info)
        if (existing == null && info.direction == com.qingchuan.linkdrop.data.TransferDirection.Incoming) {
            emitEvent(UiEvent.Message("正在接收 " + info.name))
        }
        if (info.status == TransferStatus.Completed && existing?.status != TransferStatus.Completed) {
            if (info.direction == com.qingchuan.linkdrop.data.TransferDirection.Incoming) {
                emitEvent(UiEvent.Message("已保存到 下载/LinkDrop：" + info.name))
            } else {
                emitEvent(UiEvent.Message("已送达电脑：" + info.name))
            }
        }
        if (info.status == TransferStatus.Failed && existing?.status != TransferStatus.Failed) {
            emitEvent(UiEvent.Message(info.error ?: "传输失败", true))
        }
        _ui.value = current.copy(transfers = updated.take(80))
    }

    private fun emitEvent(event: UiEvent) {
        _events.tryEmit(event)
    }

    private fun onMain(block: () -> Unit) {
        if (Looper.myLooper() == Looper.getMainLooper()) block() else mainHandler.post(block)
    }

    private fun registerNetworkMonitor() {
        try {
            val cm = app.getSystemService(ConnectivityManager::class.java) ?: return
            val callback = object : ConnectivityManager.NetworkCallback() {
                override fun onAvailable(network: Network) {
                    discovery?.restart()
                    retryConnectionQuietly()
                }

                override fun onLost(network: Network) {
                    // The reader will notice the dead socket; keep the UI in searching state.
                }
            }
            cm.registerDefaultNetworkCallback(callback)
            networkCallback = callback
        } catch (_: Exception) {}
    }

    private fun retryConnectionQuietly() {
        autoPaused = false
        pausedReason = null
    }

    override fun onCleared() {
        try { networkCallback?.let { app.getSystemService(ConnectivityManager::class.java)?.unregisterNetworkCallback(it) } } catch (_: Exception) {}
        try { pairingClient?.close() } catch (_: Exception) {}
        try { discovery?.stop() } catch (_: Exception) {}
        discovery = null
        try { client.close() } catch (_: Exception) {}
        super.onCleared()
    }
}
