package com.qingchuan.linkdrop.data

data class PairedPeer(
    val id: String,
    val name: String,
    val pin: String,
    val token: String,
    val host: String,
    val port: Int
)

data class NearbyPc(
    val id: String,
    val name: String,
    val host: String,
    val port: Int,
    val pairing: Boolean,
    val lastSeen: Long = System.currentTimeMillis()
)

enum class ConnectionPhase {
    Unpaired,
    Searching,
    Connecting,
    Connected,
    Error
}

enum class TransferDirection { Incoming, Outgoing }

enum class TransferStatus { Waiting, Transferring, Verifying, Completed, Failed }

data class TransferInfo(
    val id: Long,
    val name: String,
    val size: Long,
    val bytes: Long = 0L,
    val direction: TransferDirection,
    val status: TransferStatus = TransferStatus.Waiting,
    val error: String? = null,
    val savedUri: String? = null,
    val mime: String? = null,
    val speed: Double = 0.0,
    val startedAt: Long = System.currentTimeMillis()
)

enum class PairingPhase { Connecting, Waiting, Success, Failed }

data class PairingState(
    val pcName: String,
    val phase: PairingPhase,
    val code: String? = null,
    val message: String = "",
    val error: String? = null
)

data class HomeUiState(
    val deviceName: String = "",
    val paired: PairedPeer? = null,
    val nearby: List<NearbyPc> = emptyList(),
    val connection: ConnectionPhase = ConnectionPhase.Unpaired,
    val connectionMessage: String = "",
    val peerName: String? = null,
    val peerAddress: String? = null,
    val transfers: List<TransferInfo> = emptyList(),
    val pairing: PairingState? = null,
    val discoveryActive: Boolean = false,
    val autoPaused: Boolean = false,
    val lastError: String? = null
)

sealed interface UiEvent {
    data class Message(val text: String, val error: Boolean = false) : UiEvent
    data class OpenFile(val uri: String, val mime: String?) : UiEvent
}
