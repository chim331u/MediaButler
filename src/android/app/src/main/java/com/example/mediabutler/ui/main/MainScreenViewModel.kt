package com.example.mediabutler.ui.main

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.mediabutler.data.ConfigResponse
import com.example.mediabutler.data.DataRepository
import com.example.mediabutler.data.FileStatus
import com.example.mediabutler.data.MoveProgressPayload
import com.example.mediabutler.data.SSEEvent
import com.example.mediabutler.data.TrackedFile
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.launch

class MainScreenViewModel(private val repository: DataRepository) : ViewModel() {

    private val _currentTab = MutableStateFlow("dashboard")
    val currentTab: StateFlow<String> = _currentTab.asStateFlow()

    private val _pendingFiles = MutableStateFlow<List<TrackedFile>>(emptyList())
    val pendingFiles: StateFlow<List<TrackedFile>> = _pendingFiles.asStateFlow()

    private val _historyFiles = MutableStateFlow<List<TrackedFile>>(emptyList())
    val historyFiles: StateFlow<List<TrackedFile>> = _historyFiles.asStateFlow()

    private val _config = MutableStateFlow<ConfigResponse?>(null)
    val config: StateFlow<ConfigResponse?> = _config.asStateFlow()

    private val _sseConnected = MutableStateFlow(false)
    val sseConnected: StateFlow<Boolean> = _sseConnected.asStateFlow()

    private val _activeProgresses = MutableStateFlow<Map<String, MoveProgressPayload>>(emptyMap())
    val activeProgresses: StateFlow<Map<String, MoveProgressPayload>> = _activeProgresses.asStateFlow()

    private val _isLoading = MutableStateFlow(false)
    val isLoading: StateFlow<Boolean> = _isLoading.asStateFlow()

    private val _isSubmitting = MutableStateFlow(false)
    val isSubmitting: StateFlow<Boolean> = _isSubmitting.asStateFlow()

    // Preferences IP editing state
    private val _serverIpInput = MutableStateFlow(repository.getServerIp())
    val serverIpInput: StateFlow<String> = _serverIpInput.asStateFlow()

    // Confirmation Modal States
    private val _selectedFile = MutableStateFlow<TrackedFile?>(null)
    val selectedFile: StateFlow<TrackedFile?> = _selectedFile.asStateFlow()

    private val _customCategory = MutableStateFlow("")
    val customCategory: StateFlow<String> = _customCategory.asStateFlow()

    // Dynamic Preset Categories State
    private val _presets = MutableStateFlow<List<String>>(listOf("MOVIES", "TV SHOWS", "MUSIC", "DOCS", "PHOTOS"))
    val presets: StateFlow<List<String>> = _presets.asStateFlow()

    // Bulk Reclassification State
    private val _isReclassifying = MutableStateFlow(false)
    val isReclassifying: StateFlow<Boolean> = _isReclassifying.asStateFlow()

    // ML Threshold local editing state
    private val _mlThresholdInput = MutableStateFlow<Float?>(null)
    val mlThresholdInput: StateFlow<Float?> = _mlThresholdInput.asStateFlow()

    // File Explorer State
    private val _directoryCache = MutableStateFlow<Map<String, List<com.example.mediabutler.data.FSItem>>>(emptyMap())
    val directoryCache: StateFlow<Map<String, List<com.example.mediabutler.data.FSItem>>> = _directoryCache.asStateFlow()

    private val _showHiddenFiles = MutableStateFlow(false)
    val showHiddenFiles: StateFlow<Boolean> = _showHiddenFiles.asStateFlow()

    // Toasts/Notifications Events
    private val _notifications = MutableSharedFlow<Pair<String, String>>() // Title to Message
    val notifications: SharedFlow<Pair<String, String>> = _notifications.asSharedFlow()

    // Trigger for SSE stream connection/reconnection
    private val sseTrigger = MutableStateFlow(0)

    init {
        loadConfig()
        refreshDashboard()
        observeSSEStream()
    }

    fun setTab(tab: String) {
        _currentTab.value = tab
        if (tab == "dashboard") {
            refreshDashboard()
        } else if (tab == "history") {
            loadHistory()
        }
    }

    fun updateServerIp(ip: String) {
        _serverIpInput.value = ip
    }

    fun saveServerIp() {
        viewModelScope.launch {
            repository.setServerIp(_serverIpInput.value)
            _notifications.emit("Config Saved" to "Server URL updated to http://${repository.getServerIp()}")
            loadConfig()
            refreshDashboard()
            // Re-trigger SSE connection on new URL
            sseTrigger.value++
        }
    }

    fun loadConfig() {
        viewModelScope.launch {
            val cfg = repository.getConfig()
            if (cfg != null) {
                _config.value = cfg
            }
        }
    }

    fun refreshDashboard() {
        viewModelScope.launch {
            _isLoading.value = true
            try {
                val pending = repository.getPendingFiles()
                _pendingFiles.value = pending
            } catch (e: Exception) {
                _notifications.emit("Fetch Error" to "Failed to load watch-folder queue.")
            } finally {
                _isLoading.value = false
            }
        }
    }

    fun loadHistory(skip: Int = 0, take: Int = 20) {
        viewModelScope.launch {
            try {
                val history = repository.getHistoryFiles(skip, take)
                _historyFiles.value = history
            } catch (e: Exception) {
                _notifications.emit("History Error" to "Failed to load moved history.")
            }
        }
    }

    // Modal confirmed organization
    fun openConfirmModal(file: TrackedFile) {
        _selectedFile.value = file
        _customCategory.value = file.suggestedCategory ?: ""
        loadPresets()
    }

    fun closeConfirmModal() {
        _selectedFile.value = null
        _customCategory.value = ""
    }

    fun loadPresets() {
        viewModelScope.launch {
            try {
                val prs = repository.getCategoryPresets()
                if (prs.isNotEmpty()) {
                    _presets.value = prs
                }
            } catch (e: Exception) {
                // Fail silently, use defaults
            }
        }
    }

    fun ignoreFile(hash: String) {
        viewModelScope.launch {
            val ok = repository.ignoreFile(hash)
            if (ok) {
                _notifications.emit("File Ignored" to "File successfully ignored.")
                // Optimistically remove from active list
                _pendingFiles.value = _pendingFiles.value.filter { it.hash != hash }
            } else {
                _notifications.emit("Action Failed" to "Could not ignore file.")
            }
        }
    }

    fun reclassifyUnconfirmed() {
        viewModelScope.launch {
            _isReclassifying.value = true
            val ok = repository.reclassifyUnconfirmed()
            if (ok) {
                _notifications.emit("Reclassification Started" to "Recalculating AI suggestions in the background...")
            } else {
                _notifications.emit("Reclassification Failed" to "Could not trigger bulk reclassification.")
                _isReclassifying.value = false
            }
        }
    }

    fun updateLocalMlThreshold(value: Float) {
        _mlThresholdInput.value = value
    }

    fun saveMlThreshold() {
        val value = _mlThresholdInput.value ?: return
        viewModelScope.launch {
            val ok = repository.updateMlThreshold(value.toDouble())
            if (ok) {
                _notifications.emit("Config Updated" to "ML threshold updated successfully.")
                loadConfig() // Reload from server
            } else {
                _notifications.emit("Update Failed" to "Could not update ML threshold.")
            }
            _mlThresholdInput.value = null
        }
    }

    fun toggleShowHiddenFiles() {
        _showHiddenFiles.value = !_showHiddenFiles.value
        // Clear directory cache to force reload
        _directoryCache.value = emptyMap()
    }

    fun loadFSList(path: String) {
        if (_directoryCache.value.containsKey(path)) return
        viewModelScope.launch {
            try {
                val list = repository.getFSList(path, _showHiddenFiles.value)
                _directoryCache.value = _directoryCache.value + (path to list)
            } catch (e: Exception) {
                // Fail silently
            }
        }
    }

    fun updateCustomCategory(cat: String) {
        _customCategory.value = cat
    }

    fun confirmCategory(hash: String, category: String) {
        viewModelScope.launch {
            _isSubmitting.value = true
            val categoryUpper = category.trim().uppercase()
            val ok = repository.confirmCategory(hash, categoryUpper)
            if (ok) {
                _notifications.emit("Category Confirmed" to "File category confirmed manually as \"$categoryUpper\". Ready to move.")
                closeConfirmModal()

                // Optimistically update file status to ReadyToMove (3) in local list
                _pendingFiles.value = _pendingFiles.value.map {
                    if (it.hash == hash) {
                        it.copy(status = FileStatus.READY_TO_MOVE.value, category = categoryUpper)
                    } else it
                }
            } else {
                _notifications.emit("Action Failed" to "Could not confirm category.")
            }
            _isSubmitting.value = false
        }
    }

    fun moveFile(hash: String) {
        viewModelScope.launch {
            val file = _pendingFiles.value.firstOrNull { it.hash == hash } ?: return@launch
            val previousStatus = file.status

            // Optimistically update file status to Moving (4) in local list
            _pendingFiles.value = _pendingFiles.value.map {
                if (it.hash == hash) {
                    it.copy(status = FileStatus.MOVING.value)
                } else it
            }

            // Pre-populate progress tracker placeholder
            _activeProgresses.value = _activeProgresses.value + (hash to MoveProgressPayload(
                hash = hash,
                fileName = file.fileName,
                progress = 0.0,
                bytesCopied = 0,
                totalBytes = file.fileSize
            ))

            val ok = repository.moveFile(hash)
            if (ok) {
                _notifications.emit("Move Started" to "Organization of file triggered asynchronously.")
            } else {
                _notifications.emit("Move Failed" to "Could not start file organization.")
                // Revert status
                _pendingFiles.value = _pendingFiles.value.map {
                    if (it.hash == hash) {
                        it.copy(status = previousStatus)
                    } else it
                }
                _activeProgresses.value = _activeProgresses.value - hash
            }
        }
    }

    // Reactively observe Server-Sent Events from QNAP Go Backend
    @OptIn(ExperimentalCoroutinesApi::class)
    private fun observeSSEStream() {
        viewModelScope.launch {
            sseTrigger.flatMapLatest {
                repository.listenEvents()
            }.catch {
                _sseConnected.value = false
            }.collect { event ->
                when (event) {
                    SSEEvent.Connected -> {
                        _sseConnected.value = true
                    }
                    is SSEEvent.Progress -> {
                        _sseConnected.value = true
                        val payload = event.payload
                        // Update progress map reactively
                        _activeProgresses.value = _activeProgresses.value + (payload.hash to payload)

                        // Update status in pending files list to Moving (4)
                        _pendingFiles.value = _pendingFiles.value.map {
                            if (it.hash == payload.hash) {
                                it.copy(status = FileStatus.MOVING.value)
                            } else it
                        }
                    }
                    is SSEEvent.Completed -> {
                        val payload = event.payload
                        _notifications.emit("Move Completed" to "\"${payload.fileName}\" organized successfully.")

                        // Remove from active progresses
                        _activeProgresses.value = _activeProgresses.value - payload.hash

                        // Remove from pending files list
                        _pendingFiles.value = _pendingFiles.value.filter { it.hash != payload.hash }

                        // Refresh history list automatically if we are in it
                        if (currentTab.value == "history") {
                            loadHistory()
                        }
                    }
                    is SSEEvent.Error -> {
                        val payload = event.payload
                        if (payload.hash.isNotEmpty()) {
                            _notifications.emit("Move Error" to "Failed to move \"${payload.fileName}\": ${payload.error}")

                            // Remove progress indicator
                            _activeProgresses.value = _activeProgresses.value - payload.hash

                            // Mark file status as error (6) locally
                            _pendingFiles.value = _pendingFiles.value.map {
                                if (it.hash == payload.hash) {
                                    it.copy(status = FileStatus.ERROR.value, lastError = payload.error)
                                } else it
                            }
                        } else {
                            // General SSE stream connection error
                            _sseConnected.value = false
                        }
                    }
                    is SSEEvent.FileIgnored -> {
                        val ignoredHash = event.hash
                        _pendingFiles.value = _pendingFiles.value.filter { it.hash != ignoredHash }
                        _notifications.emit("File Ignored" to "File successfully ignored.")
                    }
                    SSEEvent.Reclassified -> {
                        _isReclassifying.value = false
                        refreshDashboard()
                        _notifications.emit("Classification Updated" to "AI suggestions recalculated.")
                    }
                }
            }
        }
    }
}
