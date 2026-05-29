package com.example.mediabutler.data

import kotlinx.serialization.Serializable

@Serializable
enum class FileStatus(val value: Int) {
    NEW(0),
    PROCESSING(1),
    CLASSIFIED(2),
    READY_TO_MOVE(3),
    MOVING(4),
    MOVED(5),
    ERROR(6),
    RETRY(7),
    IGNORED(8);

    companion object {
        fun fromInt(value: Int) = values().firstOrNull { it.value == value } ?: NEW
    }
}

@Serializable
data class TrackedFile(
    val hash: String,
    val fileName: String,
    val originalPath: String,
    val fileSize: Long,
    val status: Int, // Map to FileStatus
    val suggestedCategory: String? = null,
    val confidence: Double = 0.0,
    val category: String? = null,
    val targetPath: String? = null,
    val classifiedAt: String? = null,
    val movedAt: String? = null,
    val lastError: String? = null,
    val lastErrorAt: String? = null,
    val retryCount: Int = 0,
    val createdDate: String,
    val lastUpdateDate: String,
    val note: String? = null,
    val isActive: Boolean = true,
    val movedToPath: String? = null
) {
    val fileStatus: FileStatus
        get() = FileStatus.fromInt(status)
}

@Serializable
data class ConfigResponse(
    val watchFolders: List<String>,
    val destFolder: String,
    val databasePath: String,
    val mlThreshold: Double
)

@Serializable
data class ConfirmCategoryRequest(
    val category: String
)

@Serializable
data class MoveProgressPayload(
    val hash: String,
    val fileName: String,
    val progress: Double,
    val bytesCopied: Long,
    val totalBytes: Long
)

@Serializable
data class MoveCompletedPayload(
    val hash: String,
    val fileName: String,
    val targetPath: String
)

@Serializable
data class MoveErrorPayload(
    val hash: String,
    val fileName: String,
    val error: String
)

@Serializable
data class UpdateConfigRequest(
    val mlThreshold: Double
)

@Serializable
data class UpdateFileRequest(
    val category: String,
    val status: Int
)

@Serializable
data class FileIgnoredPayload(
    val hash: String
)

@Serializable
data class FSItem(
    val name: String,
    val path: String,
    val isDir: Boolean,
    val sizeBytes: Long = 0
)

sealed interface SSEEvent {
    data object Connected : SSEEvent
    data class Progress(val payload: MoveProgressPayload) : SSEEvent
    data class Completed(val payload: MoveCompletedPayload) : SSEEvent
    data class Error(val payload: MoveErrorPayload) : SSEEvent
    data class FileIgnored(val hash: String) : SSEEvent
    data object Reclassified : SSEEvent
}
