package com.qingchuan.linkdrop.ui

import android.content.ActivityNotFoundException
import android.content.Intent
import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.animateContentSize
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.ChevronRight
import androidx.compose.material.icons.rounded.Computer
import androidx.compose.material.icons.rounded.ErrorOutline
import androidx.compose.material.icons.rounded.Folder
import androidx.compose.material.icons.rounded.InsertDriveFile
import androidx.compose.material.icons.rounded.Lock
import androidx.compose.material.icons.rounded.MoreVert
import androidx.compose.material.icons.rounded.Refresh
import androidx.compose.material.icons.rounded.Send
import androidx.compose.material.icons.rounded.Settings
import androidx.compose.material.icons.rounded.Shield
import androidx.compose.material.icons.rounded.Wifi
import androidx.compose.material.icons.rounded.CheckCircle
import androidx.compose.material.icons.rounded.Download
import androidx.compose.material.icons.rounded.Upload
import androidx.compose.material.icons.rounded.Image
import androidx.compose.material.icons.rounded.Movie
import androidx.compose.material.icons.rounded.MusicNote
import androidx.compose.material.icons.rounded.OpenInNew
import androidx.compose.material.icons.rounded.PictureAsPdf
import androidx.compose.material.icons.rounded.Sync
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Snackbar
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.qingchuan.linkdrop.R
import com.qingchuan.linkdrop.data.ConnectionPhase
import com.qingchuan.linkdrop.data.HomeUiState
import com.qingchuan.linkdrop.data.NearbyPc
import com.qingchuan.linkdrop.data.TransferDirection
import com.qingchuan.linkdrop.data.TransferInfo
import com.qingchuan.linkdrop.data.TransferStatus
import com.qingchuan.linkdrop.data.UiEvent
import com.qingchuan.linkdrop.net.formatBytes
import com.qingchuan.linkdrop.ui.theme.Brand

@Composable
fun LinkDropApp(vm: MainViewModel) {
    val state by vm.ui.collectAsStateWithLifecycle()
    val snackbarHostState = remember { SnackbarHostState() }
    val context = LocalContext.current
    var showSettings by remember { mutableStateOf(false) }
    var showManual by remember { mutableStateOf(false) }

    val filePicker = rememberLauncherForActivityResult(
        ActivityResultContracts.OpenMultipleDocuments()
    ) { uris ->
        if (uris.isNotEmpty()) vm.sendUris(uris)
    }

    LaunchedEffect(vm) {
        vm.events.collect { event ->
            when (event) {
                is UiEvent.Message -> snackbarHostState.showSnackbar(event.text)
                is UiEvent.OpenFile -> {
                    try {
                        val intent = Intent(Intent.ACTION_VIEW)
                            .setDataAndType(Uri.parse(event.uri), event.mime ?: "*/*")
                            .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                        context.startActivity(intent)
                    } catch (_: ActivityNotFoundException) {
                        snackbarHostState.showSnackbar("没有可打开此文件的应用")
                    } catch (_: Exception) {
                        snackbarHostState.showSnackbar("无法打开这个文件")
                    }
                }
            }
        }
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .statusBarsPadding()
                .navigationBarsPadding()
        ) {
            AppHeader(
                connected = state.connection == ConnectionPhase.Connected,
                onSettings = { showSettings = true }
            )
            LazyColumn(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                contentPadding = PaddingValues(start = 20.dp, end = 20.dp, top = 6.dp, bottom = 28.dp),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                item {
                    ConnectionHero(
                        state = state,
                        onSend = {
                            if (state.connection == ConnectionPhase.Connected) {
                                filePicker.launch(arrayOf("*/*"))
                            } else {
                                vm.retryConnection()
                            }
                        },
                        onRetry = { vm.retryConnection() },
                        onManual = { showManual = true },
                        onScan = { vm.refreshDiscovery() }
                    )
                }
                if (state.connection != ConnectionPhase.Connected) {
                    item {
                        NearbySection(
                            nearby = state.nearby,
                            paired = state.paired,
                            discoveryActive = state.discoveryActive,
                            onSelect = { vm.startPairing(it) },
                            onManual = { showManual = true },
                            onScan = { vm.refreshDiscovery() }
                        )
                    }
                }
                item {
                    TransfersHeader(
                        count = state.transfers.size,
                        active = state.transfers.count { it.status == TransferStatus.Transferring || it.status == TransferStatus.Waiting || it.status == TransferStatus.Verifying }
                    )
                }
                if (state.transfers.isEmpty()) {
                    item { EmptyTransfers(connected = state.connection == ConnectionPhase.Connected) }
                } else {
                    items(state.transfers, key = { it.id }) { transfer ->
                        TransferCard(transfer = transfer, onOpen = { vm.openTransfer(it) })
                    }
                }
                item { PrivacyFooter() }
            }
        }
        SnackbarHost(
            hostState = snackbarHostState,
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .navigationBarsPadding()
                .padding(horizontal = 16.dp, vertical = 10.dp)
        ) { data ->
            Snackbar(
                snackbarData = data,
                containerColor = Brand.PrimaryDark,
                contentColor = Color.White,
                actionColor = Brand.Mint,
                shape = RoundedCornerShape(18.dp)
            )
        }
    }

    state.pairing?.let { pairing ->
        PairingDialog(
            state = pairing,
            onDismiss = { vm.dismissPairing() }
        )
    }
    if (showSettings) {
        SettingsSheet(
            state = state,
            onDismiss = { showSettings = false },
            onSaveName = { vm.setDeviceName(it) },
            onUnpair = {
                vm.unpair()
                showSettings = false
            },
            onRetry = {
                vm.retryConnection()
                showSettings = false
            },
            onManual = {
                showSettings = false
                showManual = true
            }
        )
    }
    if (showManual) {
        ManualConnectDialog(
            state = state,
            onDismiss = { showManual = false },
            onConnect = { host, port ->
                vm.pairManual(host, port)
                showManual = false
            }
        )
    }
}

@Composable
private fun AppHeader(connected: Boolean, onSettings: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 20.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Surface(
            shape = RoundedCornerShape(13.dp),
            color = Color.Transparent
        ) {
            androidx.compose.foundation.Image(
                painter = painterResource(R.drawable.linkdrop_logo),
                contentDescription = "轻传",
                modifier = Modifier
                    .size(42.dp)
                    .clip(RoundedCornerShape(13.dp)),
                contentScale = ContentScale.Crop
            )
        }
        Spacer(Modifier.width(11.dp))
        Column(Modifier.weight(1f)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = "轻传",
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onBackground
                )
                if (connected) {
                    Spacer(Modifier.width(7.dp))
                    Box(
                        Modifier
                            .size(7.dp)
                            .clip(CircleShape)
                            .background(Brand.MintDeep)
                    )
                }
            }
            Text(
                text = "LinkDrop · 局域网直传",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
        IconButton(onClick = onSettings) {
            Icon(
                imageVector = Icons.Rounded.Settings,
                contentDescription = "设置",
                tint = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

@Composable
private fun ConnectionHero(
    state: HomeUiState,
    onSend: () -> Unit,
    onRetry: () -> Unit,
    onManual: () -> Unit,
    onScan: () -> Unit
) {
    val connected = state.connection == ConnectionPhase.Connected
    val paired = state.paired
    val title = when {
        connected -> state.peerName ?: paired?.name ?: "电脑已连接"
        state.connection == ConnectionPhase.Connecting -> "正在连接电脑…"
        state.connection == ConnectionPhase.Error -> "连接需要处理"
        paired != null -> "等待电脑上线"
        else -> "让文件走一条近路"
    }
    val subtitle = when {
        connected -> "局域网 TLS 加密直连 · 文件不经过云端"
        state.connection == ConnectionPhase.Connecting -> "正在与 " + (paired?.name ?: "电脑") + " 建立安全连接"
        state.connection == ConnectionPhase.Error -> state.connectionMessage.ifBlank { "配对状态可能已变化，请在设置中检查" }
        paired != null -> "已配对 " + paired.name + "，电脑端启动轻传后会自动连接"
        else -> "同一 Wi-Fi 下，与电脑配对后即可直接互传文件"
    }
    val status = when (state.connection) {
        ConnectionPhase.Connected -> "已安全连接"
        ConnectionPhase.Connecting -> "正在连接"
        ConnectionPhase.Searching -> "等待电脑"
        ConnectionPhase.Error -> "需要处理"
        ConnectionPhase.Unpaired -> "尚未配对"
    }
    val statusColor = when (state.connection) {
        ConnectionPhase.Connected -> Brand.Mint
        ConnectionPhase.Connecting, ConnectionPhase.Searching -> Color(0xFFF1C369)
        ConnectionPhase.Error -> Color(0xFFFFB4BA)
        ConnectionPhase.Unpaired -> Color(0xFFA9C0BB)
    }

    Box(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(30.dp))
            .background(
                Brush.linearGradient(
                    colors = listOf(Brand.HeroTop, Brand.HeroBottom),
                    start = androidx.compose.ui.geometry.Offset.Zero,
                    end = androidx.compose.ui.geometry.Offset(900f, 700f)
                )
            )
            .animateContentSize()
    ) {
        Canvas(Modifier.fillMaxSize()) {
            drawCircle(Color.White.copy(alpha = 0.045f), radius = size.width * 0.47f, center = androidx.compose.ui.geometry.Offset(size.width * 0.96f, -size.height * 0.05f))
            drawCircle(Color.White.copy(alpha = 0.05f), radius = size.width * 0.30f, center = androidx.compose.ui.geometry.Offset(size.width * 0.88f, size.height * 0.72f))
            drawCircle(Brand.Mint.copy(alpha = 0.04f), radius = size.width * 0.56f, center = androidx.compose.ui.geometry.Offset(size.width * 0.18f, size.height * 1.1f))
        }
        Icon(
            imageVector = Icons.Rounded.Computer,
            contentDescription = null,
            tint = Color.White.copy(alpha = 0.08f),
            modifier = Modifier
                .align(Alignment.TopEnd)
                .padding(top = 20.dp, end = 10.dp)
                .size(142.dp)
        )
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(22.dp)
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Surface(
                    shape = CircleShape,
                    color = Color.White.copy(alpha = 0.11f)
                ) {
                    Row(
                        modifier = Modifier.padding(horizontal = 11.dp, vertical = 6.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        if (state.connection == ConnectionPhase.Connecting || state.connection == ConnectionPhase.Searching) {
                            CircularProgressIndicator(
                                modifier = Modifier.size(11.dp),
                                strokeWidth = 1.5.dp,
                                color = statusColor
                            )
                        } else {
                            Box(
                                Modifier
                                    .size(7.dp)
                                    .clip(CircleShape)
                                    .background(statusColor)
                            )
                        }
                        Spacer(Modifier.width(7.dp))
                        Text(
                            text = status,
                            style = MaterialTheme.typography.labelMedium,
                            color = Color(0xFFE9F8F3)
                        )
                    }
                }
                Spacer(Modifier.weight(1f))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        imageVector = Icons.Rounded.Lock,
                        contentDescription = null,
                        tint = Color.White.copy(alpha = 0.55f),
                        modifier = Modifier.size(12.dp)
                    )
                    Spacer(Modifier.width(4.dp))
                    Text(
                        text = "TLS",
                        style = MaterialTheme.typography.labelSmall,
                        color = Color.White.copy(alpha = 0.55f)
                    )
                }
            }
            Spacer(Modifier.height(22.dp))
            Text(
                text = title,
                style = if (title.length > 12) MaterialTheme.typography.headlineMedium else MaterialTheme.typography.displaySmall,
                color = Color.White,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis
            )
            Spacer(Modifier.height(7.dp))
            Text(
                text = subtitle,
                style = MaterialTheme.typography.bodyMedium,
                color = Color(0xFFC7E4DD),
                maxLines = 2,
                overflow = TextOverflow.Ellipsis,
                modifier = Modifier.padding(end = 60.dp)
            )
            Spacer(Modifier.height(24.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Button(
                    onClick = when {
                        connected -> onSend
                        paired != null -> onRetry
                        else -> onScan
                    },
                    colors = ButtonDefaults.buttonColors(
                        containerColor = Brand.Mint,
                        contentColor = Brand.PrimaryDark
                    ),
                    shape = RoundedCornerShape(16.dp),
                    contentPadding = PaddingValues(horizontal = 18.dp, vertical = 12.dp)
                ) {
                    Icon(
                        imageVector = when {
                            connected -> Icons.Rounded.Send
                            paired != null -> Icons.Rounded.Refresh
                            else -> Icons.Rounded.Wifi
                        },
                        contentDescription = null,
                        modifier = Modifier.size(17.dp)
                    )
                    Spacer(Modifier.width(7.dp))
                    Text(
                        text = when {
                            connected -> "发送文件"
                            paired != null -> "重新连接"
                            else -> "扫描附近电脑"
                        },
                        style = MaterialTheme.typography.labelLarge
                    )
                }
                Spacer(Modifier.width(6.dp))
                TextButton(onClick = onManual) {
                    Text(
                        text = "手动连接",
                        color = Color.White.copy(alpha = 0.82f),
                        style = MaterialTheme.typography.labelMedium
                    )
                }
            }
        }
    }
}

@Composable
private fun SaveHint() {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 4.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(
            imageVector = Icons.Rounded.Folder,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(16.dp)
        )
        Spacer(Modifier.width(7.dp))
        Text(
            text = "电脑发来的文件会自动保存到 下载/LinkDrop",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}

@Composable
private fun SectionTitle(title: String, subtitle: String? = null, action: (@Composable () -> Unit)? = null) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(Modifier.weight(1f)) {
            Text(
                text = title,
                style = MaterialTheme.typography.titleLarge,
                color = MaterialTheme.colorScheme.onBackground
            )
            if (subtitle != null) {
                Spacer(Modifier.height(2.dp))
                Text(
                    text = subtitle,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
        if (action != null) action()
    }
}

@Composable
private fun NearbySection(
    nearby: List<NearbyPc>,
    paired: com.qingchuan.linkdrop.data.PairedPeer?,
    discoveryActive: Boolean,
    onSelect: (NearbyPc) -> Unit,
    onManual: () -> Unit,
    onScan: () -> Unit
) {
    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
        SectionTitle(
            title = "附近的电脑",
            subtitle = if (discoveryActive) "正在通过局域网寻找已开启的电脑" else "扫描已暂停，点击重新扫描",
            action = {
                TextButton(onClick = onManual) {
                    Text("手动连接", style = MaterialTheme.typography.labelMedium)
                }
            }
        )
        if (nearby.isEmpty()) {
            EmptyNearby(discoveryActive = discoveryActive, onManual = onManual, onScan = onScan)
        } else {
            nearby.take(4).forEach { pc ->
                NearbyCard(
                    pc = pc,
                    alreadyPaired = paired?.id == pc.id,
                    onClick = { onSelect(pc) }
                )
            }
        }
    }
}

@Composable
private fun NearbyCard(pc: NearbyPc, alreadyPaired: Boolean, onClick: () -> Unit) {
    Surface(
        onClick = onClick,
        shape = RoundedCornerShape(22.dp),
        color = MaterialTheme.colorScheme.surface,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
    ) {
        Row(
            modifier = Modifier.padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(46.dp)
                    .clip(RoundedCornerShape(15.dp))
                    .background(if (alreadyPaired) Brand.MintSoft else MaterialTheme.colorScheme.surfaceVariant),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = Icons.Rounded.Computer,
                    contentDescription = null,
                    tint = if (alreadyPaired) Brand.Primary else MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(24.dp)
                )
            }
            Spacer(Modifier.width(13.dp))
            Column(Modifier.weight(1f)) {
                Text(
                    text = pc.name,
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
                Spacer(Modifier.height(3.dp))
                Text(
                    text = pc.host + "  ·  " + if (alreadyPaired) "已配对，点击重连" else if (pc.pairing) "电脑已开启配对" else "电脑未开启配对",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
            }
            Spacer(Modifier.width(8.dp))
            if (pc.pairing && !alreadyPaired) {
                Surface(shape = CircleShape, color = Brand.MintSoft) {
                    Text(
                        text = "可配对",
                        style = MaterialTheme.typography.labelSmall,
                        color = Brand.Primary,
                        modifier = Modifier.padding(horizontal = 9.dp, vertical = 4.dp)
                    )
                }
            } else if (alreadyPaired) {
                Surface(shape = CircleShape, color = Brand.MintSoft) {
                    Text(
                        text = "已配对",
                        style = MaterialTheme.typography.labelSmall,
                        color = Brand.Primary,
                        modifier = Modifier.padding(horizontal = 9.dp, vertical = 4.dp)
                    )
                }
            }
            Icon(
                imageVector = Icons.Rounded.ChevronRight,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.55f),
                modifier = Modifier.size(20.dp)
            )
        }
    }
}

@Composable
private fun EmptyNearby(discoveryActive: Boolean, onManual: () -> Unit, onScan: () -> Unit) {
    Surface(
        shape = RoundedCornerShape(22.dp),
        color = MaterialTheme.colorScheme.surface,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(
            modifier = Modifier.padding(20.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Box(
                modifier = Modifier
                    .size(48.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.primaryContainer),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = Icons.Rounded.Wifi,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(23.dp)
                )
            }
            Spacer(Modifier.height(12.dp))
            Text(
                text = if (discoveryActive) "还没有发现电脑" else "局域网扫描已暂停",
                style = MaterialTheme.typography.titleMedium,
                color = MaterialTheme.colorScheme.onSurface
            )
            Spacer(Modifier.height(5.dp))
            Text(
                text = "在电脑上打开轻传，点击「生成安全码」，电脑就会出现在这里。两台设备需要连接同一个 Wi-Fi。",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = androidx.compose.ui.text.style.TextAlign.Center
            )
            Spacer(Modifier.height(14.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                OutlinedButton(
                    onClick = onScan,
                    shape = RoundedCornerShape(14.dp),
                    contentPadding = PaddingValues(horizontal = 16.dp, vertical = 9.dp)
                ) {
                    Icon(Icons.Rounded.Refresh, contentDescription = null, modifier = Modifier.size(16.dp))
                    Spacer(Modifier.width(6.dp))
                    Text("重新扫描", style = MaterialTheme.typography.labelMedium)
                }
                Spacer(Modifier.width(4.dp))
                TextButton(onClick = onManual) {
                    Text("输入 IP", style = MaterialTheme.typography.labelMedium)
                }
            }
        }
    }
}

@Composable
private fun TransfersHeader(count: Int, active: Int) {
    Column(verticalArrangement = Arrangement.spacedBy(3.dp)) {
        Row(verticalAlignment = Alignment.Bottom) {
            Text(
                text = "传输记录",
                style = MaterialTheme.typography.titleLarge,
                color = MaterialTheme.colorScheme.onBackground
            )
            if (count > 0) {
                Spacer(Modifier.width(8.dp))
                Surface(shape = CircleShape, color = MaterialTheme.colorScheme.surfaceVariant) {
                    Text(
                        text = if (active > 0) active.toString() + " 个进行中" else count.toString() + " 个",
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(horizontal = 9.dp, vertical = 3.dp)
                    )
                }
            }
        }
        Text(
            text = "接收文件自动保存到 下载/LinkDrop",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}

@Composable
private fun EmptyTransfers(connected: Boolean) {
    dashedCard(
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(vertical = 26.dp, horizontal = 18.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Box(
                modifier = Modifier
                    .size(54.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.primaryContainer),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = Icons.Rounded.Send,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(25.dp)
                )
            }
            Spacer(Modifier.height(12.dp))
            Text(
                text = "还没有传输记录",
                style = MaterialTheme.typography.titleMedium,
                color = MaterialTheme.colorScheme.onSurface
            )
            Spacer(Modifier.height(4.dp))
            Text(
                text = if (connected) "点击上方「发送文件」，或等待电脑发来文件。" else "连接电脑后，从上方「发送文件」，或等待电脑发来文件。",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = androidx.compose.ui.text.style.TextAlign.Center
            )
        }
    }
}

@Composable
private fun TransferCard(transfer: TransferInfo, onOpen: (TransferInfo) -> Unit) {
    val canOpen = transfer.direction == TransferDirection.Incoming &&
        transfer.status == TransferStatus.Completed && !transfer.savedUri.isNullOrBlank()
    Surface(
        shape = RoundedCornerShape(20.dp),
        color = MaterialTheme.colorScheme.surface,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline),
        modifier = Modifier
            .fillMaxWidth()
            .then(if (canOpen) Modifier.clickable { onOpen(transfer) } else Modifier)
    ) {
        Column(Modifier.padding(15.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier
                        .size(48.dp)
                        .clip(RoundedCornerShape(15.dp))
                        .then(
                            if (canOpen) Modifier.clickable(
                                role = Role.Button,
                                onClickLabel = "用其他应用打开"
                            ) { onOpen(transfer) } else Modifier
                        ),
                    contentAlignment = Alignment.Center
                ) {
                    Box(
                        modifier = Modifier
                            .size(44.dp)
                            .clip(RoundedCornerShape(14.dp))
                            .background(
                                if (transfer.direction == TransferDirection.Incoming) MaterialTheme.colorScheme.primaryContainer
                                else MaterialTheme.colorScheme.surfaceVariant
                            ),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(
                            imageVector = fileIcon(transfer),
                            contentDescription = if (canOpen) "用其他应用打开" else null,
                            tint = if (transfer.direction == TransferDirection.Incoming) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.size(22.dp)
                        )
                    }
                    if (canOpen) {
                        Box(
                            modifier = Modifier
                                .align(Alignment.BottomEnd)
                                .padding(1.dp)
                                .size(16.dp)
                                .clip(CircleShape)
                                .background(Brand.MintDeep),
                            contentAlignment = Alignment.Center
                        ) {
                            Icon(
                                imageVector = Icons.Rounded.OpenInNew,
                                contentDescription = null,
                                tint = Color.White,
                                modifier = Modifier.size(10.dp)
                            )
                        }
                    }
                }
                Spacer(Modifier.width(10.dp))
                Column(Modifier.weight(1f)) {
                    Text(
                        text = transfer.name,
                        style = MaterialTheme.typography.titleSmall,
                        color = MaterialTheme.colorScheme.onSurface,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                    Spacer(Modifier.height(3.dp))
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(
                            imageVector = if (transfer.direction == TransferDirection.Incoming) Icons.Rounded.Download else Icons.Rounded.Upload,
                            contentDescription = null,
                            tint = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.size(12.dp)
                        )
                        Spacer(Modifier.width(3.dp))
                        Text(
                            text = (if (transfer.direction == TransferDirection.Incoming) "来自电脑" else "发送到电脑") +
                                " · " + formatBytes(transfer.size) +
                                if (transfer.status == TransferStatus.Transferring && transfer.speed > 0) " · " + formatBytes(transfer.speed.toLong()) + "/s" else "",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis
                        )
                    }
                }
                Spacer(Modifier.width(8.dp))
                TransferStateLabel(transfer)
            }
            if (transfer.status == TransferStatus.Transferring || transfer.status == TransferStatus.Verifying) {
                Spacer(Modifier.height(12.dp))
                val progress = if (transfer.size <= 0) 0f else (transfer.bytes.toFloat() / transfer.size).coerceIn(0f, 1f)
                LinearProgressIndicator(
                    progress = { if (transfer.status == TransferStatus.Verifying) 1f else progress },
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(4.dp)
                        .clip(CircleShape),
                    color = if (transfer.status == TransferStatus.Verifying) Brand.Amber else MaterialTheme.colorScheme.primary,
                    trackColor = MaterialTheme.colorScheme.surfaceVariant
                )
            }
            if (transfer.status == TransferStatus.Failed && !transfer.error.isNullOrBlank()) {
                Spacer(Modifier.height(9.dp))
                Text(
                    text = transfer.error,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis
                )
            }
            if (canOpen) {
                Spacer(Modifier.height(9.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        imageVector = Icons.Rounded.OpenInNew,
                        contentDescription = null,
                        tint = Brand.MintDeep,
                        modifier = Modifier.size(12.dp)
                    )
                    Spacer(Modifier.width(4.dp))
                    Text(
                        text = "点击文件图标用其他应用打开",
                        style = MaterialTheme.typography.labelSmall,
                        color = Brand.MintDeep
                    )
                }
            }
        }
    }
}

@Composable
private fun TransferStateLabel(transfer: TransferInfo) {
    val (text, color) = when (transfer.status) {
        TransferStatus.Completed -> (if (transfer.direction == TransferDirection.Incoming) "已保存" else "已送达") to Brand.MintDeep
        TransferStatus.Failed -> "失败" to MaterialTheme.colorScheme.error
        TransferStatus.Verifying -> "校验中" to Brand.Amber
        TransferStatus.Transferring -> {
            val percent = if (transfer.size <= 0) 0 else (transfer.bytes * 100 / transfer.size).toInt()
            ("" + percent + "%") to MaterialTheme.colorScheme.primary
        }
        TransferStatus.Waiting -> "等待中" to MaterialTheme.colorScheme.onSurfaceVariant
    }
    Row(verticalAlignment = Alignment.CenterVertically) {
        Icon(
            imageVector = when (transfer.status) {
                TransferStatus.Completed -> Icons.Rounded.CheckCircle
                TransferStatus.Failed -> Icons.Rounded.ErrorOutline
                TransferStatus.Waiting -> Icons.Rounded.Sync
                else -> Icons.Rounded.Upload
            },
            contentDescription = null,
            tint = color,
            modifier = Modifier.size(15.dp)
        )
        Spacer(Modifier.width(4.dp))
        Text(text = text, style = MaterialTheme.typography.labelSmall, color = color)
    }
}

private fun fileIcon(transfer: TransferInfo) = when {
    transfer.mime?.startsWith("image/") == true -> Icons.Rounded.Image
    transfer.mime?.startsWith("video/") == true -> Icons.Rounded.Movie
    transfer.mime?.startsWith("audio/") == true -> Icons.Rounded.MusicNote
    transfer.name.endsWith(".pdf", true) -> Icons.Rounded.PictureAsPdf
    else -> Icons.Rounded.InsertDriveFile
}

@Composable
private fun PrivacyFooter() {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 4.dp, bottom = 4.dp),
        horizontalArrangement = Arrangement.Center,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(
            imageVector = Icons.Rounded.Shield,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f),
            modifier = Modifier.size(14.dp)
        )
        Spacer(Modifier.width(6.dp))
        Text(
            text = "局域网点对点传输 · 不上传云端 · TLS 1.2 加密",
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.8f)
        )
    }
}

private fun Modifier.dashedCard(color: Color, radius: androidx.compose.ui.unit.Dp): Modifier = this.drawBehind {
    val stroke = Stroke(
        width = 1.2.dp.toPx(),
        pathEffect = PathEffect.dashPathEffect(floatArrayOf(10f, 8f), 0f)
    )
    drawRoundRect(
        color = color,
        style = stroke,
        cornerRadius = CornerRadius(radius.toPx(), radius.toPx())
    )
}

@Composable
private fun dashedCard(modifier: Modifier = Modifier, content: @Composable () -> Unit) {
    val color = MaterialTheme.colorScheme.outline
    Box(
        modifier = modifier
            .clip(RoundedCornerShape(22.dp))
            .dashedCard(color, 22.dp)
            .background(MaterialTheme.colorScheme.surface.copy(alpha = 0.65f))
    ) {
        content()
    }
}
