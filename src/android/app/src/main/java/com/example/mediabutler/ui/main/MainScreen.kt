package com.example.mediabutler.ui.main

import android.widget.Toast
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.BorderStroke
import androidx.compose.ui.draw.scale
import androidx.compose.material3.Slider
import androidx.compose.material3.SliderDefaults
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.TextButton
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.List
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.NavigationBarItemDefaults
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation3.runtime.NavKey
import androidx.compose.foundation.layout.PaddingValues
import com.example.mediabutler.data.DefaultDataRepository
import com.example.mediabutler.data.FileStatus
import com.example.mediabutler.data.MoveProgressPayload
import com.example.mediabutler.data.TrackedFile
import com.example.mediabutler.data.ConfigResponse
import com.example.mediabutler.data.SSEEvent
import com.example.mediabutler.theme.CobaltBlue
import com.example.mediabutler.theme.DarkBackground
import com.example.mediabutler.theme.DarkSurface
import com.example.mediabutler.theme.DarkSurfaceElevated
import com.example.mediabutler.theme.MediaButlerTheme
import com.example.mediabutler.theme.NeonPink
import com.example.mediabutler.theme.NeonPurple
import com.example.mediabutler.theme.StateClassified
import com.example.mediabutler.theme.StateError
import com.example.mediabutler.theme.StateIgnored
import com.example.mediabutler.theme.StateMoved
import com.example.mediabutler.theme.StateNew
import com.example.mediabutler.theme.StateProcessing
import com.example.mediabutler.theme.StateReady
import com.example.mediabutler.theme.TextPrimary
import com.example.mediabutler.theme.TextSecondary
import com.example.mediabutler.theme.TextMuted
import kotlinx.serialization.Serializable

@Composable
fun MainScreen(
    onItemClick: (NavKey) -> Unit,
    modifier: Modifier = Modifier,
) {
    val context = LocalContext.current.applicationContext
    val viewModel: MainScreenViewModel = viewModel {
        MainScreenViewModel(DefaultDataRepository(context))
    }
    MainScreen(onItemClick = onItemClick, modifier = modifier, viewModel = viewModel)
}

@Composable
fun MainScreen(
    onItemClick: (NavKey) -> Unit,
    modifier: Modifier = Modifier,
    viewModel: MainScreenViewModel
) {
    val context = LocalContext.current
    val currentTab by viewModel.currentTab.collectAsStateWithLifecycle()
    val pendingFiles by viewModel.pendingFiles.collectAsStateWithLifecycle()
    val historyFiles by viewModel.historyFiles.collectAsStateWithLifecycle()
    val config by viewModel.config.collectAsStateWithLifecycle()
    val sseConnected by viewModel.sseConnected.collectAsStateWithLifecycle()
    val activeProgresses by viewModel.activeProgresses.collectAsStateWithLifecycle()
    val isLoading by viewModel.isLoading.collectAsStateWithLifecycle()
    val isReclassifying by viewModel.isReclassifying.collectAsStateWithLifecycle()
    val selectedFile by viewModel.selectedFile.collectAsStateWithLifecycle()

    var ignoreConfirmTarget by remember { mutableStateOf<TrackedFile?>(null) }

    // Handle incoming backend toast alerts as standard Android Toasts
    LaunchedEffect(Unit) {
        viewModel.notifications.collect { (title, message) ->
            Toast.makeText(context, "$title: $message", Toast.LENGTH_LONG).show()
        }
    }

    MediaButlerTheme {
        Scaffold(
            bottomBar = {
                BottomNavBar(currentTab = currentTab, onTabSelect = { viewModel.setTab(it) })
            },
            containerColor = DarkBackground,
            modifier = modifier
                .fillMaxSize()
                .statusBarsPadding()
                .navigationBarsPadding()
        ) { paddingValues ->
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
            ) {
                // Background Glowing Accents for premium aesthetics
                GlowingAccentBg()

                Column(modifier = Modifier.fillMaxSize()) {
                    // Header Bar
                    HeaderBar(sseConnected = sseConnected, onRefresh = {
                        if (currentTab == "dashboard") viewModel.refreshDashboard()
                        else viewModel.loadHistory()
                    })

                    when (currentTab) {
                        "dashboard" -> DashboardView(
                            pendingFiles = pendingFiles,
                            activeProgresses = activeProgresses,
                            isLoading = isLoading,
                            isReclassifying = isReclassifying,
                            onReclassifyClick = { viewModel.reclassifyUnconfirmed() },
                            onIgnoreClick = { ignoreConfirmTarget = it },
                            onOrganizeClick = { viewModel.openConfirmModal(it) },
                            onMoveClick = { viewModel.moveFile(it.hash) }
                        )
                        "history" -> HistoryView(viewModel = viewModel)
                        "settings" -> SettingsView(
                            viewModel = viewModel,
                            config = config
                        )
                    }
                }

                // Ignore File Confirmation Dialog
                ignoreConfirmTarget?.let { file ->
                    AlertDialog(
                        onDismissRequest = { ignoreConfirmTarget = null },
                        title = { Text("Sei sicuro?", color = TextPrimary, fontWeight = FontWeight.Bold) },
                        text = {
                            Text(
                                text = "Non mostrare più il file \"${file.fileName}\"? Verrà escluso permanentemente dalla coda attiva.",
                                color = TextSecondary,
                                fontSize = 14.sp
                            )
                        },
                        confirmButton = {
                            TextButton(
                                onClick = {
                                    viewModel.ignoreFile(file.hash)
                                    ignoreConfirmTarget = null
                                }
                            ) {
                                Text("Sì, Ignora", color = StateError, fontWeight = FontWeight.Bold)
                            }
                        },
                        dismissButton = {
                            TextButton(onClick = { ignoreConfirmTarget = null }) {
                                Text("Annulla", color = TextPrimary)
                            }
                        },
                        containerColor = DarkSurface,
                        shape = RoundedCornerShape(12.dp)
                    )
                }

                // AI Category Confirmation Modal Overlay
                selectedFile?.let { file ->
                    ConfirmCategoryModal(
                        file = file,
                        viewModel = viewModel,
                        onDismiss = { viewModel.closeConfirmModal() }
                    )
                }
            }
        }
    }
}

@Composable
fun GlowingAccentBg() {
    Box(modifier = Modifier.fillMaxSize()) {
        Box(
            modifier = Modifier
                .size(300.dp)
                .align(Alignment.TopEnd)
                .alpha(0.08f)
                .background(Brush.radialGradient(listOf(CobaltBlue, Color.Transparent)))
        )
        Box(
            modifier = Modifier
                .size(350.dp)
                .align(Alignment.BottomStart)
                .alpha(0.08f)
                .background(Brush.radialGradient(listOf(NeonPurple, Color.Transparent)))
        )
    }
}

@Composable
fun HeaderBar(sseConnected: Boolean, onRefresh: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(16.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = "Media",
                    color = TextPrimary,
                    fontSize = 24.sp,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = "Butler",
                    color = CobaltBlue,
                    fontSize = 24.sp,
                    fontWeight = FontWeight.Bold
                )
            }
            Text(
                text = "AI-Powered Media Hub Organizer",
                color = TextSecondary,
                fontSize = 11.sp
            )
        }

        Row(verticalAlignment = Alignment.CenterVertically) {
            // Live SSE Connection Status Indicator
            Row(
                modifier = Modifier
                    .clip(RoundedCornerShape(20.dp))
                    .background(if (sseConnected) Color(0x1534D399) else Color(0x15F59E0B))
                    .border(
                        1.dp,
                        if (sseConnected) StateMoved.copy(alpha = 0.3f) else StateProcessing.copy(alpha = 0.3f),
                        RoundedCornerShape(20.dp)
                    )
                    .padding(horizontal = 10.dp, vertical = 4.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Pulsing indicator dot
                val infiniteTransition = rememberInfiniteTransition(label = "pulse")
                val alpha by infiniteTransition.animateFloat(
                    initialValue = 0.4f,
                    targetValue = 1f,
                    animationSpec = infiniteRepeatable(
                        animation = tween(1000, easing = LinearEasing),
                        repeatMode = RepeatMode.Reverse
                    ),
                    label = "alpha"
                )
                Box(
                    modifier = Modifier
                        .size(6.dp)
                        .alpha(alpha)
                        .clip(CircleShape)
                        .background(if (sseConnected) StateMoved else StateProcessing)
                )
                Spacer(modifier = Modifier.width(6.dp))
                Text(
                    text = if (sseConnected) "SSE Stream Live" else "SSE Disconnected",
                    color = if (sseConnected) StateMoved else StateProcessing,
                    fontSize = 10.sp,
                    fontWeight = FontWeight.SemiBold
                )
            }

            Spacer(modifier = Modifier.width(8.dp))

            IconButton(
                onClick = onRefresh,
                modifier = Modifier
                    .size(36.dp)
                    .clip(CircleShape)
                    .background(DarkSurfaceElevated)
            ) {
                Icon(
                    imageVector = Icons.Default.Refresh,
                    contentDescription = "Refresh",
                    tint = TextPrimary,
                    modifier = Modifier.size(18.dp)
                )
            }
        }
    }
}

@Composable
fun DashboardView(
    pendingFiles: List<TrackedFile>,
    activeProgresses: Map<String, MoveProgressPayload>,
    isLoading: Boolean,
    isReclassifying: Boolean,
    onReclassifyClick: () -> Unit,
    onIgnoreClick: (TrackedFile) -> Unit,
    onOrganizeClick: (TrackedFile) -> Unit,
    onMoveClick: (TrackedFile) -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 16.dp)
    ) {
        // Glowing Stat Dashboard Counters Row
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            val newCount = pendingFiles.count { it.status == 0 || it.status == 7 }
            val procCount = pendingFiles.count { it.status == 1 || it.status == 4 }
            val actionCount = pendingFiles.count { it.status == 2 }

            StatCard(
                title = "New",
                value = newCount.toString(),
                color = StateNew,
                modifier = Modifier.weight(1f)
            )
            StatCard(
                title = "Processing",
                value = procCount.toString(),
                color = StateProcessing,
                modifier = Modifier.weight(1f)
            )
            StatCard(
                title = "Actions Pending",
                value = actionCount.toString(),
                color = StateClassified,
                modifier = Modifier.weight(1f)
            )
        }

        Spacer(modifier = Modifier.height(16.dp))

        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = "Tracked Watch-Folder Files",
                    color = TextPrimary,
                    fontSize = 16.sp,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = "Real-time state of media files",
                    color = TextSecondary,
                    fontSize = 11.sp
                )
            }

            Button(
                onClick = onReclassifyClick,
                enabled = !isReclassifying && !isLoading,
                colors = ButtonDefaults.buttonColors(
                    containerColor = CobaltBlue.copy(alpha = 0.12f),
                    disabledContainerColor = CobaltBlue.copy(alpha = 0.05f)
                ),
                border = BorderStroke(1.dp, CobaltBlue.copy(alpha = 0.4f)),
                shape = RoundedCornerShape(20.dp),
                contentPadding = PaddingValues(horizontal = 12.dp, vertical = 6.dp),
                modifier = Modifier.height(32.dp)
            ) {
                Text(
                    text = if (isReclassifying) "🧠 Recalculating..." else "🧠 Re-classify",
                    color = CobaltBlue,
                    fontSize = 11.sp,
                    fontWeight = FontWeight.Bold
                )
            }
        }

        Spacer(modifier = Modifier.height(12.dp))

        if (isLoading && pendingFiles.isEmpty()) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                contentAlignment = Alignment.Center
            ) {
                LinearProgressIndicator(color = CobaltBlue, modifier = Modifier.width(150.dp))
            }
        } else if (pendingFiles.isEmpty()) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                contentAlignment = Alignment.Center
            ) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Text("📂", fontSize = 48.sp)
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        "No Active Files Found",
                        color = TextPrimary,
                        fontSize = 16.sp,
                        fontWeight = FontWeight.Bold
                    )
                    Spacer(modifier = Modifier.height(4.dp))
                    Text(
                        "Your watch folders are clean.",
                        color = TextSecondary,
                        fontSize = 12.sp
                    )
                }
            }
        } else {
            LazyColumn(
                modifier = Modifier.weight(1f),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                items(pendingFiles, key = { it.hash }) { file ->
                    FileQueueCard(
                        file = file,
                        progress = activeProgresses[file.hash],
                        onIgnoreClick = { onIgnoreClick(file) },
                        onOrganizeClick = { onOrganizeClick(file) },
                        onMoveClick = { onMoveClick(file) }
                    )
                }
            }
        }
    }
}

@Composable
fun StatCard(title: String, value: String, color: Color, modifier: Modifier = Modifier) {
    Box(
        modifier = modifier
            .clip(RoundedCornerShape(12.dp))
            .background(DarkSurface)
            .border(1.dp, color.copy(alpha = 0.15f), RoundedCornerShape(12.dp))
            .padding(12.dp)
    ) {
        Column {
            Text(text = title, color = TextSecondary, fontSize = 10.sp, fontWeight = FontWeight.SemiBold)
            Spacer(modifier = Modifier.height(4.dp))
            Text(text = value, color = color, fontSize = 20.sp, fontWeight = FontWeight.Bold)
        }
    }
}

@Composable
fun FileQueueCard(
    file: TrackedFile,
    progress: MoveProgressPayload?,
    onIgnoreClick: () -> Unit,
    onOrganizeClick: () -> Unit,
    onMoveClick: () -> Unit
) {
    val status = FileStatus.fromInt(file.status)
    val statusColor = when (status) {
        FileStatus.NEW -> StateNew
        FileStatus.PROCESSING -> StateProcessing
        FileStatus.CLASSIFIED -> StateClassified
        FileStatus.READY_TO_MOVE -> StateReady
        FileStatus.MOVING -> StateReady
        FileStatus.MOVED -> StateMoved
        FileStatus.ERROR -> StateError
        FileStatus.RETRY -> StateProcessing
        FileStatus.IGNORED -> StateIgnored
    }

    Box(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(DarkSurface)
            .border(
                1.dp,
                if (status == FileStatus.CLASSIFIED) StateClassified.copy(alpha = 0.3f) else Color(0xFF1E293B),
                RoundedCornerShape(12.dp)
            )
            .padding(14.dp)
    ) {
        Column {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Status Badge
                Box(
                    modifier = Modifier
                        .clip(RoundedCornerShape(4.dp))
                        .background(statusColor.copy(alpha = 0.12f))
                        .padding(horizontal = 6.dp, vertical = 2.dp)
                ) {
                    Text(
                        text = status.name.replace("_", " "),
                        color = statusColor,
                        fontSize = 9.sp,
                        fontWeight = FontWeight.Bold
                    )
                }

                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        text = formatBytes(file.fileSize),
                        color = TextSecondary,
                        fontSize = 11.sp
                    )

                    Spacer(modifier = Modifier.width(8.dp))

                    IconButton(
                        onClick = onIgnoreClick,
                        modifier = Modifier
                            .size(24.dp)
                            .clip(CircleShape)
                            .background(StateError.copy(alpha = 0.1f))
                    ) {
                        Icon(
                            imageVector = Icons.Default.Close,
                            contentDescription = "Ignore File",
                            tint = StateError,
                            modifier = Modifier.size(12.dp)
                        )
                    }
                }
            }

            Spacer(modifier = Modifier.height(8.dp))

            Text(
                text = file.fileName,
                color = TextPrimary,
                fontSize = 14.sp,
                fontWeight = FontWeight.Bold,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )

            Spacer(modifier = Modifier.height(6.dp))

            Text(
                text = "Source: ${file.originalPath}",
                color = TextSecondary,
                fontSize = 11.sp,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )

            // Dynamic progress indicators from SSE payload
            if (progress != null) {
                Spacer(modifier = Modifier.height(10.dp))
                Column {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        Text("Organizing...", color = TextSecondary, fontSize = 10.sp)
                        Text("${progress.progress.toInt()}%", color = CobaltBlue, fontSize = 11.sp, fontWeight = FontWeight.Bold)
                    }
                    Spacer(modifier = Modifier.height(4.dp))
                    LinearProgressIndicator(
                        progress = { (progress.progress / 100f).toFloat() },
                        color = CobaltBlue,
                        trackColor = DarkSurfaceElevated,
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(4.dp)
                            .clip(CircleShape)
                    )
                    Spacer(modifier = Modifier.height(2.dp))
                    Text(
                        text = "${formatBytes(progress.bytesCopied)} / ${formatBytes(progress.totalBytes)}",
                        color = TextSecondary,
                        fontSize = 9.sp
                    )
                }
            } else if (file.status == FileStatus.MOVING.value || file.status == FileStatus.READY_TO_MOVE.value) {
                Spacer(modifier = Modifier.height(10.dp))
                Column {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        Text("Queued for transfer...", color = TextSecondary, fontSize = 10.sp)
                        Text("0%", color = TextSecondary, fontSize = 11.sp)
                    }
                    Spacer(modifier = Modifier.height(4.dp))
                    LinearProgressIndicator(
                        color = CobaltBlue,
                        trackColor = DarkSurfaceElevated,
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(4.dp)
                            .clip(CircleShape)
                    )
                }
            }

            // Error display if failed
            if (status == FileStatus.ERROR && !file.lastError.isNullOrEmpty()) {
                Spacer(modifier = Modifier.height(8.dp))
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(6.dp))
                        .background(StateError.copy(alpha = 0.08f))
                        .border(1.dp, StateError.copy(alpha = 0.15f), RoundedCornerShape(6.dp))
                        .padding(8.dp)
                ) {
                    Text(
                        text = "Error: ${file.lastError}",
                        color = StateError,
                        fontSize = 11.sp
                    )
                }
            }

            // Confirm organize action button if classified
            if (status == FileStatus.CLASSIFIED) {
                Spacer(modifier = Modifier.height(12.dp))
                Button(
                    onClick = onOrganizeClick,
                    colors = ButtonDefaults.buttonColors(containerColor = CobaltBlue),
                    shape = RoundedCornerShape(6.dp),
                    modifier = Modifier.fillMaxWidth(),
                    contentPadding = PaddingValues(vertical = 8.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.Check,
                        contentDescription = null,
                        modifier = Modifier.size(14.dp)
                    )
                    Spacer(modifier = Modifier.width(6.dp))
                    Text("Organize", fontSize = 13.sp, fontWeight = FontWeight.Bold)
                }
            }

            // Move or retry action buttons if ready to move or error
            if (status == FileStatus.READY_TO_MOVE || status == FileStatus.ERROR) {
                Spacer(modifier = Modifier.height(12.dp))
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Button(
                        onClick = onMoveClick,
                        colors = ButtonDefaults.buttonColors(
                            containerColor = if (status == FileStatus.ERROR) StateError else CobaltBlue
                        ),
                        shape = RoundedCornerShape(6.dp),
                        modifier = Modifier.weight(1f),
                        contentPadding = PaddingValues(vertical = 8.dp)
                    ) {
                        Icon(
                            imageVector = if (status == FileStatus.ERROR) Icons.Default.Refresh else Icons.Default.Check,
                            contentDescription = null,
                            modifier = Modifier.size(14.dp)
                        )
                        Spacer(modifier = Modifier.width(6.dp))
                        Text(
                            text = if (status == FileStatus.ERROR) "Retry Move" else "Move",
                            fontSize = 13.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }

                    IconButton(
                        onClick = onOrganizeClick,
                        modifier = Modifier
                            .size(36.dp)
                            .clip(RoundedCornerShape(6.dp))
                            .background(DarkSurfaceElevated)
                            .border(1.dp, Color(0xFF1E293B), RoundedCornerShape(6.dp))
                    ) {
                        Icon(
                            imageVector = Icons.Default.Edit,
                            contentDescription = "Edit Category",
                            tint = TextPrimary,
                            modifier = Modifier.size(16.dp)
                        )
                    }
                }
            }
        }
    }
}

@Composable
fun HistoryView(viewModel: MainScreenViewModel) {
    val historyFiles by viewModel.historyFiles.collectAsStateWithLifecycle()
    val searchQuery by viewModel.historySearchQuery.collectAsStateWithLifecycle()
    val page by viewModel.historyPage.collectAsStateWithLifecycle()
    val editingFileHash by viewModel.editingFileHash.collectAsStateWithLifecycle()
    val editingCategory by viewModel.editingCategory.collectAsStateWithLifecycle()
    val editingStatus by viewModel.editingStatus.collectAsStateWithLifecycle()
    val isLoading by viewModel.isLoading.collectAsStateWithLifecycle()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 16.dp)
    ) {
        Text(
            text = "Global Archive",
            color = TextPrimary,
            fontSize = 16.sp,
            fontWeight = FontWeight.Bold
        )
        Text(
            text = "Search, edit and monitor all database files",
            color = TextSecondary,
            fontSize = 12.sp
        )

        Spacer(modifier = Modifier.height(12.dp))

        // Search Bar (Cobalt Blue Premium Style with search icon and close/clear button)
        OutlinedTextField(
            value = searchQuery,
            onValueChange = { viewModel.updateHistorySearchQuery(it) },
            modifier = Modifier
                .fillMaxWidth()
                .padding(vertical = 4.dp),
            placeholder = { Text("Search by filename...", color = TextMuted) },
            leadingIcon = {
                Text("🔍", fontSize = 16.sp, modifier = Modifier.padding(start = 4.dp))
            },
            trailingIcon = {
                if (searchQuery.isNotEmpty()) {
                    IconButton(onClick = { viewModel.updateHistorySearchQuery("") }) {
                        Icon(
                            imageVector = Icons.Default.Close,
                            contentDescription = "Clear search",
                            tint = TextSecondary
                        )
                    }
                }
            },
            singleLine = true,
            colors = OutlinedTextFieldDefaults.colors(
                focusedTextColor = TextPrimary,
                unfocusedTextColor = TextPrimary,
                focusedContainerColor = DarkSurface,
                unfocusedContainerColor = DarkSurface,
                focusedBorderColor = CobaltBlue,
                unfocusedBorderColor = Color(0xFF1E293B)
            ),
            shape = RoundedCornerShape(8.dp)
        )

        Spacer(modifier = Modifier.height(8.dp))

        if (historyFiles.isEmpty()) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                contentAlignment = Alignment.Center
            ) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Text(if (isLoading) "⏳" else "🕰️", fontSize = 48.sp)
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        if (isLoading) "Loading records..." else "No Database Records",
                        color = TextPrimary,
                        fontSize = 16.sp,
                        fontWeight = FontWeight.Bold
                    )
                    Spacer(modifier = Modifier.height(4.dp))
                    Text(
                        if (isLoading) "Fetching from server..." else "Try searching for a different file name.",
                        color = TextSecondary,
                        fontSize = 12.sp
                    )
                }
            }
        } else {
            LazyColumn(
                modifier = Modifier.weight(1f),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                items(historyFiles) { log ->
                    val isEditing = editingFileHash == log.hash
                    val statusColor = when (log.fileStatus) {
                        FileStatus.NEW -> StateNew
                        FileStatus.PROCESSING -> StateProcessing
                        FileStatus.CLASSIFIED -> StateClassified
                        FileStatus.READY_TO_MOVE -> StateReady
                        FileStatus.MOVING -> StateReady
                        FileStatus.MOVED -> StateMoved
                        FileStatus.ERROR -> StateError
                        FileStatus.RETRY -> StateProcessing
                        FileStatus.IGNORED -> StateIgnored
                    }

                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(8.dp))
                            .background(DarkSurface)
                            .border(
                                1.dp,
                                if (isEditing) CobaltBlue else Color(0xFF1E293B),
                                RoundedCornerShape(8.dp)
                            )
                            .padding(12.dp)
                    ) {
                        if (isEditing) {
                            // Inline Edit Mode
                            Column {
                                Text(
                                    text = "Edit Inline: ${log.fileName}",
                                    color = TextPrimary,
                                    fontSize = 12.sp,
                                    fontWeight = FontWeight.Bold,
                                    maxLines = 1,
                                    overflow = TextOverflow.Ellipsis
                                )
                                Spacer(modifier = Modifier.height(8.dp))

                                // Category text input field
                                OutlinedTextField(
                                    value = editingCategory,
                                    onValueChange = { viewModel.updateEditingCategory(it) },
                                    modifier = Modifier.fillMaxWidth(),
                                    label = { Text("Category", color = TextSecondary) },
                                    singleLine = true,
                                    colors = OutlinedTextFieldDefaults.colors(
                                        focusedTextColor = TextPrimary,
                                        unfocusedTextColor = TextPrimary,
                                        focusedContainerColor = DarkSurface,
                                        unfocusedContainerColor = DarkSurface,
                                        focusedBorderColor = CobaltBlue,
                                        unfocusedBorderColor = Color(0xFF1E293B)
                                    ),
                                    shape = RoundedCornerShape(6.dp)
                                )

                                Spacer(modifier = Modifier.height(8.dp))

                                // Status Dropdown / Selector
                                Text("Status", color = TextSecondary, fontSize = 11.sp)
                                Spacer(modifier = Modifier.height(4.dp))

                                var showDropdown by remember { mutableStateOf(false) }
                                Box(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .clip(RoundedCornerShape(6.dp))
                                        .background(Color(0xFF1E293B))
                                        .clickable { showDropdown = true }
                                        .padding(horizontal = 12.dp, vertical = 8.dp)
                                ) {
                                    Row(
                                        modifier = Modifier.fillMaxWidth(),
                                        horizontalArrangement = Arrangement.SpaceBetween,
                                        verticalAlignment = Alignment.CenterVertically
                                    ) {
                                        Text(editingStatus.name, color = TextPrimary, fontSize = 13.sp)
                                        Text("▼", color = TextSecondary, fontSize = 10.sp)
                                    }

                                    if (showDropdown) {
                                        Dialog(onDismissRequest = { showDropdown = false }) {
                                            Card(
                                                colors = CardDefaults.cardColors(containerColor = DarkSurface),
                                                border = BorderStroke(1.dp, Color(0xFF1E293B)),
                                                shape = RoundedCornerShape(12.dp),
                                                modifier = Modifier.padding(16.dp)
                                            ) {
                                                Column(modifier = Modifier.padding(16.dp)) {
                                                    Text(
                                                        text = "Select Status",
                                                        color = TextPrimary,
                                                        fontWeight = FontWeight.Bold,
                                                        fontSize = 14.sp
                                                    )
                                                    Spacer(modifier = Modifier.height(12.dp))
                                                    LazyColumn(
                                                        modifier = Modifier.height(250.dp),
                                                        verticalArrangement = Arrangement.spacedBy(4.dp)
                                                    ) {
                                                        items(FileStatus.values()) { statusOption ->
                                                            Row(
                                                                modifier = Modifier
                                                                    .fillMaxWidth()
                                                                    .clip(RoundedCornerShape(6.dp))
                                                                    .background(
                                                                        if (editingStatus == statusOption) Color(0xFF1E293B)
                                                                        else Color.Transparent
                                                                    )
                                                                    .clickable {
                                                                        viewModel.updateEditingStatus(statusOption)
                                                                        showDropdown = false
                                                                    }
                                                                    .padding(10.dp),
                                                                verticalAlignment = Alignment.CenterVertically
                                                            ) {
                                                                Box(
                                                                    modifier = Modifier
                                                                        .size(8.dp)
                                                                        .clip(CircleShape)
                                                                        .background(
                                                                            when (statusOption) {
                                                                                FileStatus.NEW -> StateNew
                                                                                FileStatus.PROCESSING -> StateProcessing
                                                                                FileStatus.CLASSIFIED -> StateClassified
                                                                                FileStatus.READY_TO_MOVE -> StateReady
                                                                                FileStatus.MOVING -> StateReady
                                                                                FileStatus.MOVED -> StateMoved
                                                                                FileStatus.ERROR -> StateError
                                                                                FileStatus.RETRY -> StateProcessing
                                                                                FileStatus.IGNORED -> StateIgnored
                                                                            }
                                                                        )
                                                                )
                                                                Spacer(modifier = Modifier.width(8.dp))
                                                                Text(
                                                                    statusOption.name,
                                                                    color = TextPrimary,
                                                                    fontSize = 13.sp
                                                                )
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                Spacer(modifier = Modifier.height(12.dp))

                                // Action Buttons (Save/Cancel)
                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.End,
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    IconButton(
                                        onClick = { viewModel.cancelInlineEdit() },
                                        modifier = Modifier
                                            .size(36.dp)
                                            .clip(CircleShape)
                                            .background(Color(0xFF1E293B))
                                    ) {
                                        Icon(
                                            imageVector = Icons.Default.Close,
                                            contentDescription = "Cancel Edit",
                                            tint = StateError,
                                            modifier = Modifier.size(16.dp)
                                        )
                                    }
                                    Spacer(modifier = Modifier.width(8.dp))
                                    IconButton(
                                        onClick = { viewModel.saveInlineEdit() },
                                        modifier = Modifier
                                            .size(36.dp)
                                            .clip(CircleShape)
                                            .background(CobaltBlue)
                                    ) {
                                        Icon(
                                            imageVector = Icons.Default.Check,
                                            contentDescription = "Save Edit",
                                            tint = TextPrimary,
                                            modifier = Modifier.size(16.dp)
                                        )
                                    }
                                }
                            }
                        } else {
                            // Standard Row Mode
                            Column {
                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    // Row showing Category Badge AND Status Badge
                                    Row(
                                        verticalAlignment = Alignment.CenterVertically,
                                        horizontalArrangement = Arrangement.spacedBy(6.dp)
                                    ) {
                                        // Category Badge
                                        Box(
                                            modifier = Modifier
                                                .clip(RoundedCornerShape(4.dp))
                                                .background(statusColor.copy(alpha = 0.12f))
                                                .padding(horizontal = 6.dp, vertical = 2.dp)
                                        ) {
                                            Text(
                                                text = log.category ?: "MEDIA",
                                                color = statusColor,
                                                fontSize = 9.sp,
                                                fontWeight = FontWeight.Bold
                                            )
                                        }

                                        // Status Badge
                                        Box(
                                            modifier = Modifier
                                                .clip(RoundedCornerShape(4.dp))
                                                .background(statusColor.copy(alpha = 0.08f))
                                                .border(1.dp, statusColor.copy(alpha = 0.3f), RoundedCornerShape(4.dp))
                                                .padding(horizontal = 6.dp, vertical = 2.dp)
                                        ) {
                                            Text(
                                                text = log.fileStatus.name,
                                                color = statusColor,
                                                fontSize = 8.sp,
                                                fontWeight = FontWeight.Bold
                                            )
                                        }
                                    }

                                    // Inline Edit trigger button
                                    IconButton(
                                        onClick = { viewModel.startInlineEdit(log) },
                                        modifier = Modifier.size(24.dp)
                                    ) {
                                        Icon(
                                            imageVector = Icons.Default.Edit,
                                            contentDescription = "Edit file inline",
                                            tint = TextSecondary,
                                            modifier = Modifier.size(14.dp)
                                        )
                                    }
                                }

                                Spacer(modifier = Modifier.height(6.dp))

                                Text(
                                    text = log.fileName,
                                    color = TextPrimary,
                                    fontSize = 13.sp,
                                    fontWeight = FontWeight.Bold,
                                    maxLines = 1,
                                    overflow = TextOverflow.Ellipsis
                                )

                                Spacer(modifier = Modifier.height(4.dp))

                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Text(
                                        text = if (log.movedToPath != null) "To: ${log.movedToPath}"
                                        else if (log.targetPath != null) "Suggested To: ${log.targetPath}"
                                        else "Original: ${log.originalPath}",
                                        color = TextSecondary,
                                        fontSize = 11.sp,
                                        maxLines = 1,
                                        overflow = TextOverflow.Ellipsis,
                                        modifier = Modifier.weight(1f)
                                    )
                                    Spacer(modifier = Modifier.width(8.dp))
                                    Text(
                                        text = formatBytes(log.fileSize),
                                        color = TextMuted,
                                        fontSize = 10.sp
                                    )
                                }
                            }
                        }
                    }
                }
            }
        }

        Spacer(modifier = Modifier.height(8.dp))

        // Paginator Controls (Prev / Next Buttons)
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(vertical = 8.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Button(
                onClick = { viewModel.prevHistoryPage() },
                enabled = page > 0 && !isLoading,
                colors = ButtonDefaults.buttonColors(
                    containerColor = Color(0xFF1E293B),
                    disabledContainerColor = Color(0xFF0F172A),
                    contentColor = TextPrimary,
                    disabledContentColor = TextSecondary
                ),
                shape = RoundedCornerShape(8.dp),
                contentPadding = PaddingValues(horizontal = 14.dp, vertical = 6.dp)
            ) {
                Text("◀ Prev", fontSize = 12.sp, fontWeight = FontWeight.Bold)
            }

            Text(
                text = "Page ${page + 1}",
                color = TextSecondary,
                fontSize = 12.sp,
                fontWeight = FontWeight.Bold
            )

            Button(
                onClick = { viewModel.nextHistoryPage() },
                enabled = historyFiles.size >= 20 && !isLoading,
                colors = ButtonDefaults.buttonColors(
                    containerColor = Color(0xFF1E293B),
                    disabledContainerColor = Color(0xFF0F172A),
                    contentColor = TextPrimary,
                    disabledContentColor = TextSecondary
                ),
                shape = RoundedCornerShape(8.dp),
                contentPadding = PaddingValues(horizontal = 14.dp, vertical = 6.dp)
            ) {
                Text("Next ▶", fontSize = 12.sp, fontWeight = FontWeight.Bold)
            }
        }
    }
}

@Composable
fun DirectoryNode(
    item: com.example.mediabutler.data.FSItem,
    viewModel: MainScreenViewModel,
    directoryCache: Map<String, List<com.example.mediabutler.data.FSItem>>,
    depth: Int
) {
    var isExpanded by remember { mutableStateOf(false) }

    LaunchedEffect(isExpanded) {
        if (isExpanded && item.isDir) {
            viewModel.loadFSList(item.path)
        }
    }

    Column(modifier = Modifier.padding(start = (depth * 8).dp)) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .clickable(enabled = item.isDir) { isExpanded = !isExpanded }
                .padding(vertical = 4.dp, horizontal = 6.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            if (item.isDir) {
                Text(
                    text = if (isExpanded) "▼" else "▶",
                    color = CobaltBlue,
                    fontSize = 10.sp,
                    modifier = Modifier.width(12.dp)
                )
                Spacer(modifier = Modifier.width(4.dp))
                Text(text = "📁", fontSize = 14.sp)
            } else {
                Spacer(modifier = Modifier.width(16.dp))
                Text(text = "📄", fontSize = 14.sp)
            }

            Spacer(modifier = Modifier.width(6.dp))

            Text(
                text = item.name,
                color = if (item.isDir) TextPrimary else TextSecondary,
                fontSize = 12.sp,
                fontWeight = if (item.isDir) FontWeight.Bold else FontWeight.Normal,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
                modifier = Modifier.weight(1f)
            )

            if (!item.isDir) {
                Text(
                    text = formatBytes(item.sizeBytes),
                    color = TextSecondary.copy(alpha = 0.7f),
                    fontSize = 10.sp
                )
            }
        }

        if (isExpanded && item.isDir) {
            val children = directoryCache[item.path]
            if (children != null) {
                children.forEach { child ->
                    DirectoryNode(
                        item = child,
                        viewModel = viewModel,
                        directoryCache = directoryCache,
                        depth = depth + 1
                    )
                }
                if (children.isEmpty()) {
                    Text(
                        text = "Empty Directory",
                        color = TextSecondary.copy(alpha = 0.5f),
                        fontSize = 11.sp,
                        modifier = Modifier.padding(start = 24.dp, top = 2.dp, bottom = 2.dp)
                    )
                }
            } else {
                LinearProgressIndicator(
                    color = CobaltBlue,
                    modifier = Modifier
                        .padding(start = 24.dp, top = 4.dp, bottom = 4.dp)
                        .width(100.dp)
                        .height(2.dp)
                )
            }
        }
    }
}

@Composable
fun SettingsView(
    viewModel: MainScreenViewModel,
    config: ConfigResponse?
) {
    val serverIpInput by viewModel.serverIpInput.collectAsStateWithLifecycle()
    val showHiddenFiles by viewModel.showHiddenFiles.collectAsStateWithLifecycle()
    val directoryCache by viewModel.directoryCache.collectAsStateWithLifecycle()
    val mlThresholdInput by viewModel.mlThresholdInput.collectAsStateWithLifecycle()

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        item {
            Text(
                text = "System Settings & Preferences",
                color = TextPrimary,
                fontSize = 16.sp,
                fontWeight = FontWeight.Bold
            )
            Text(
                text = "Configure API connection parameters to target QNAP NAS",
                color = TextSecondary,
                fontSize = 12.sp
            )
        }

        // Connection Card
        item {
            Card(
                colors = CardDefaults.cardColors(containerColor = DarkSurface),
                shape = RoundedCornerShape(12.dp),
                modifier = Modifier
                    .fillMaxWidth()
                    .border(1.dp, Color(0xFF1E293B), RoundedCornerShape(12.dp))
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        text = "🔌 Backend Server Connection",
                        color = TextPrimary,
                        fontSize = 14.sp,
                        fontWeight = FontWeight.Bold
                    )
                    Spacer(modifier = Modifier.height(4.dp))
                    Text(
                        text = "Enter the host IP and port where Go API is running.",
                        color = TextSecondary,
                        fontSize = 11.sp
                    )

                    Spacer(modifier = Modifier.height(14.dp))

                    OutlinedTextField(
                        value = serverIpInput,
                        onValueChange = { viewModel.updateServerIp(it) },
                        placeholder = { Text("e.g. 192.168.1.100:8080") },
                        singleLine = true,
                        colors = OutlinedTextFieldDefaults.colors(
                            focusedTextColor = TextPrimary,
                            unfocusedTextColor = TextPrimary,
                            focusedBorderColor = CobaltBlue,
                            unfocusedBorderColor = Color(0xFF1E293B),
                            focusedContainerColor = DarkSurfaceElevated,
                            unfocusedContainerColor = DarkSurfaceElevated
                        ),
                        modifier = Modifier.fillMaxWidth()
                    )

                    Spacer(modifier = Modifier.height(12.dp))

                    Button(
                        onClick = { viewModel.saveServerIp() },
                        colors = ButtonDefaults.buttonColors(containerColor = CobaltBlue),
                        shape = RoundedCornerShape(6.dp),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text("Save & Reconnect", fontWeight = FontWeight.Bold, fontSize = 13.sp)
                    }
                }
            }
        }

        // Backend Directory Settings Loaded from Server
        if (config != null) {
            item {
                Card(
                    colors = CardDefaults.cardColors(containerColor = DarkSurface),
                    shape = RoundedCornerShape(12.dp),
                    modifier = Modifier
                        .fillMaxWidth()
                        .border(1.dp, Color(0xFF1E293B), RoundedCornerShape(12.dp))
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text(
                                text = "📁 Active Directories Explorer",
                                color = TextPrimary,
                                fontSize = 14.sp,
                                fontWeight = FontWeight.Bold
                            )

                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Text("Show Hidden", color = TextSecondary, fontSize = 10.sp)
                                Spacer(modifier = Modifier.width(4.dp))
                                Switch(
                                    checked = showHiddenFiles,
                                    onCheckedChange = { viewModel.toggleShowHiddenFiles() },
                                    colors = SwitchDefaults.colors(
                                        checkedThumbColor = CobaltBlue,
                                        checkedTrackColor = CobaltBlue.copy(alpha = 0.3f),
                                        uncheckedThumbColor = TextSecondary,
                                        uncheckedTrackColor = DarkSurfaceElevated
                                    ),
                                    modifier = Modifier.scale(0.6f)
                                )
                            }
                        }
                        Spacer(modifier = Modifier.height(12.dp))

                        Text(
                            text = "Watch Directories",
                            color = TextSecondary,
                            fontSize = 11.sp,
                            fontWeight = FontWeight.SemiBold
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                        config.watchFolders.forEach { folder ->
                            val rootItem = com.example.mediabutler.data.FSItem(
                                name = folder.substringAfterLast('/'),
                                path = folder,
                                isDir = true
                            )
                            DirectoryNode(
                                item = rootItem,
                                viewModel = viewModel,
                                directoryCache = directoryCache,
                                depth = 0
                            )
                            Spacer(modifier = Modifier.height(6.dp))
                        }

                        Spacer(modifier = Modifier.height(10.dp))

                        Text(
                            text = "Destination Folder",
                            color = TextSecondary,
                            fontSize = 11.sp,
                            fontWeight = FontWeight.SemiBold
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                        val destItem = com.example.mediabutler.data.FSItem(
                            name = config.destFolder.substringAfterLast('/'),
                            path = config.destFolder,
                            isDir = true
                        )
                        DirectoryNode(
                            item = destItem,
                            viewModel = viewModel,
                            directoryCache = directoryCache,
                            depth = 0
                        )
                    }
                }
            }

            item {
                Card(
                    colors = CardDefaults.cardColors(containerColor = DarkSurface),
                    shape = RoundedCornerShape(12.dp),
                    modifier = Modifier
                        .fillMaxWidth()
                        .border(1.dp, Color(0xFF1E293B), RoundedCornerShape(12.dp))
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Text(
                            text = "🧠 Naive Bayes Classification",
                            color = TextPrimary,
                            fontSize = 14.sp,
                            fontWeight = FontWeight.Bold
                        )
                        Spacer(modifier = Modifier.height(8.dp))
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            Text("ML Threshold", color = TextSecondary, fontSize = 11.sp)
                            val currentThreshold = mlThresholdInput ?: config.mlThreshold.toFloat()
                            Text("${(currentThreshold * 100).toInt()}% Confidence", color = CobaltBlue, fontSize = 12.sp, fontWeight = FontWeight.Bold)
                        }
                        Spacer(modifier = Modifier.height(4.dp))
                        val currentThreshold = mlThresholdInput ?: config.mlThreshold.toFloat()
                        Slider(
                            value = currentThreshold,
                            onValueChange = { viewModel.updateLocalMlThreshold(it) },
                            onValueChangeFinished = { viewModel.saveMlThreshold() },
                            valueRange = 0.5f..0.98f,
                            colors = SliderDefaults.colors(
                                thumbColor = CobaltBlue,
                                activeTrackColor = CobaltBlue,
                                inactiveTrackColor = DarkSurfaceElevated
                            ),
                            modifier = Modifier.fillMaxWidth()
                        )
                        Text(
                            text = "I file con una confidenza stimata superiore a questa soglia vengono considerati affidabili ed approvati automaticamente dal sistema. I file con confidenza inferiore richiederanno invece la tua conferma manuale nell'interfaccia per prevenire catalogazioni errate.",
                            color = TextSecondary,
                            fontSize = 10.sp,
                            style = MaterialTheme.typography.bodySmall.copy(fontStyle = androidx.compose.ui.text.font.FontStyle.Italic),
                            modifier = Modifier.padding(top = 4.dp)
                        )
                        Spacer(modifier = Modifier.height(10.dp))
                        Text(
                            text = "Database: ${config.databasePath}",
                            color = TextSecondary,
                            fontSize = 10.sp
                        )
                    }
                }
            }
        } else {
            item {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(24.dp),
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        text = "Loading NAS configuration from server...",
                        color = TextSecondary,
                        fontSize = 12.sp
                    )
                }
            }
        }
    }
}

// Dialog for confirming/modifying categorizations matching Svelte 5 Autocomplete
@OptIn(ExperimentalLayoutApi::class, ExperimentalMaterial3Api::class)
@Composable
fun ConfirmCategoryModal(
    file: TrackedFile,
    viewModel: MainScreenViewModel,
    onDismiss: () -> Unit
) {
    val isSubmitting by viewModel.isSubmitting.collectAsStateWithLifecycle()
    val customCategory by viewModel.customCategory.collectAsStateWithLifecycle()
    val presetCategories by viewModel.presets.collectAsStateWithLifecycle()

    Dialog(onDismissRequest = onDismiss) {
        Surface(
            shape = RoundedCornerShape(16.dp),
            color = DarkSurface,
            modifier = Modifier
                .fillMaxWidth()
                .border(1.dp, Color(0xFF1E293B), RoundedCornerShape(16.dp))
        ) {
            Column(
                modifier = Modifier.padding(20.dp)
            ) {
                // Header
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column {
                        Text(
                            text = "🧠 Confirm Category",
                            color = TextPrimary,
                            fontSize = 16.sp,
                            fontWeight = FontWeight.Bold
                        )
                        Text(
                            text = "Review AI classification suggestion",
                            color = TextSecondary,
                            fontSize = 11.sp
                        )
                    }
                    IconButton(onClick = onDismiss, modifier = Modifier.size(24.dp)) {
                        Icon(imageVector = Icons.Default.Close, contentDescription = "Close", tint = TextSecondary)
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                // File Details Box
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(8.dp))
                        .background(DarkSurfaceElevated)
                        .padding(12.dp)
                ) {
                    Column {
                        Text(
                            text = file.fileName,
                            color = TextPrimary,
                            fontSize = 12.sp,
                            fontWeight = FontWeight.Bold,
                            maxLines = 2,
                            overflow = TextOverflow.Ellipsis
                        )
                        Spacer(modifier = Modifier.height(6.dp))
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            Text(
                                text = "Size: ${formatBytes(file.fileSize)}",
                                color = TextSecondary,
                                fontSize = 10.sp
                            )
                            Text(
                                text = "Suggested: ${file.suggestedCategory ?: "UNKNOWN"}",
                                color = CobaltBlue,
                                fontSize = 10.sp,
                                fontWeight = FontWeight.Bold
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                // Suggestion glowing badge row
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(10.dp))
                        .background(
                            Brush.linearGradient(listOf(CobaltBlue.copy(alpha = 0.08f), NeonPurple.copy(alpha = 0.08f)))
                        )
                        .border(
                            1.dp,
                            Brush.linearGradient(listOf(CobaltBlue.copy(alpha = 0.2f), NeonPurple.copy(alpha = 0.2f))),
                            RoundedCornerShape(10.dp)
                        )
                        .padding(12.dp)
                ) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column {
                            Text(
                                text = "Suggested Destination",
                                color = TextSecondary,
                                fontSize = 10.sp
                            )
                            Text(
                                text = file.suggestedCategory ?: "UNKNOWN",
                                color = StateClassified,
                                fontSize = 16.sp,
                                fontWeight = FontWeight.Bold
                            )
                        }

                        Column(horizontalAlignment = Alignment.End) {
                            Text(
                                text = "Confidence",
                                color = TextSecondary,
                                fontSize = 10.sp
                            )
                            Text(
                                text = "${(file.confidence * 100).toInt()}%",
                                color = TextPrimary,
                                fontSize = 16.sp,
                                fontWeight = FontWeight.Bold
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(16.dp))

                // Custom editable category input
                Text(
                    text = "Target Destination Category",
                    color = TextPrimary,
                    fontSize = 12.sp,
                    fontWeight = FontWeight.Bold
                )

                Spacer(modifier = Modifier.height(6.dp))

                OutlinedTextField(
                    value = customCategory,
                    onValueChange = { viewModel.updateCustomCategory(it) },
                    singleLine = true,
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedTextColor = TextPrimary,
                        unfocusedTextColor = TextPrimary,
                        focusedBorderColor = CobaltBlue,
                        unfocusedBorderColor = Color(0xFF1E293B),
                        focusedContainerColor = DarkSurfaceElevated,
                        unfocusedContainerColor = DarkSurfaceElevated
                    ),
                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Done),
                    keyboardActions = KeyboardActions(onDone = {
                        if (customCategory.isNotEmpty()) viewModel.confirmCategory(file.hash, customCategory)
                    }),
                    modifier = Modifier.fillMaxWidth()
                )

                Spacer(modifier = Modifier.height(10.dp))

                // Quick presets buttons
                Text(
                    text = "Quick Presets:",
                    color = TextSecondary,
                    fontSize = 11.sp,
                    fontWeight = FontWeight.SemiBold
                )
                Spacer(modifier = Modifier.height(4.dp))
                FlowRow(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(6.dp)
                ) {
                    presetCategories.forEach { cat ->
                        Box(
                            modifier = Modifier
                                .clip(RoundedCornerShape(6.dp))
                                .background(if (customCategory == cat) CobaltBlue else DarkSurfaceElevated)
                                .clickable { viewModel.updateCustomCategory(cat) }
                                .padding(horizontal = 8.dp, vertical = 5.dp)
                        ) {
                            Text(
                                text = cat,
                                color = if (customCategory == cat) Color.White else TextPrimary,
                                fontSize = 10.sp,
                                fontWeight = FontWeight.Bold
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(18.dp))

                // Action Buttons
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    Button(
                        onClick = onDismiss,
                        colors = ButtonDefaults.buttonColors(containerColor = DarkSurfaceElevated),
                        shape = RoundedCornerShape(6.dp),
                        modifier = Modifier.weight(1f)
                    ) {
                        Text("Cancel", color = TextPrimary, fontSize = 13.sp)
                    }

                    Button(
                        onClick = { viewModel.confirmCategory(file.hash, customCategory) },
                        enabled = !isSubmitting && customCategory.isNotEmpty(),
                        colors = ButtonDefaults.buttonColors(containerColor = CobaltBlue),
                        shape = RoundedCornerShape(6.dp),
                        modifier = Modifier.weight(1.5f)
                    ) {
                        Text(
                            text = if (isSubmitting) "Confirming..." else "Confirm Move",
                            color = Color.White,
                            fontSize = 13.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }
                }
            }
        }
    }
}

@Composable
fun BottomNavBar(currentTab: String, onTabSelect: (String) -> Unit) {
    NavigationBar(
        containerColor = DarkSurface,
        tonalElevation = 8.dp,
        modifier = Modifier.border(1.dp, Color(0xFF1E293B).copy(alpha = 0.5f), RoundedCornerShape(topStart = 16.dp, topEnd = 16.dp))
    ) {
        NavigationBarItem(
            selected = currentTab == "dashboard",
            onClick = { onTabSelect("dashboard") },
            icon = { Icon(imageVector = Icons.Default.List, contentDescription = "Dashboard") },
            label = { Text("Dashboard", fontSize = 10.sp) },
            colors = NavigationBarItemDefaults.colors(
                selectedIconColor = CobaltBlue,
                unselectedIconColor = TextSecondary,
                selectedTextColor = CobaltBlue,
                unselectedTextColor = TextSecondary,
                indicatorColor = Color.Transparent
            )
        )

        NavigationBarItem(
            selected = currentTab == "history",
            onClick = { onTabSelect("history") },
            icon = { Icon(imageVector = Icons.Default.Info, contentDescription = "Global Archive") },
            label = { Text("Archivio", fontSize = 10.sp) },
            colors = NavigationBarItemDefaults.colors(
                selectedIconColor = CobaltBlue,
                unselectedIconColor = TextSecondary,
                selectedTextColor = CobaltBlue,
                unselectedTextColor = TextSecondary,
                indicatorColor = Color.Transparent
            )
        )

        NavigationBarItem(
            selected = currentTab == "settings",
            onClick = { onTabSelect("settings") },
            icon = { Icon(imageVector = Icons.Default.Settings, contentDescription = "Settings") },
            label = { Text("Settings", fontSize = 10.sp) },
            colors = NavigationBarItemDefaults.colors(
                selectedIconColor = CobaltBlue,
                unselectedIconColor = TextSecondary,
                selectedTextColor = CobaltBlue,
                unselectedTextColor = TextSecondary,
                indicatorColor = Color.Transparent
            )
        )
    }
}

fun formatBytes(bytes: Long): String {
    if (bytes <= 0) return "0 B"
    val units = arrayOf("B", "KB", "MB", "GB", "TB")
    val digitGroups = (Math.log10(bytes.toDouble()) / Math.log10(1024.0)).toInt()
    return String.format("%.2f %s", bytes / Math.pow(1024.0, digitGroups.toDouble()), units[digitGroups])
}
