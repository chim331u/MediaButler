package com.example.mediabutler.data

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.Response
import okhttp3.sse.EventSource
import okhttp3.sse.EventSourceListener
import okhttp3.sse.EventSources
import java.io.IOException
import java.util.concurrent.TimeUnit

class NetworkClient {
    val okHttpClient = OkHttpClient.Builder()
        .connectTimeout(10, TimeUnit.SECONDS)
        .readTimeout(0, TimeUnit.SECONDS) // Infinite read timeout for SSE stream
        .writeTimeout(10, TimeUnit.SECONDS)
        .build()

    val json = Json {
        ignoreUnknownKeys = true
        coerceInputValues = true
        encodeDefaults = true
    }

    private val jsonMediaType = "application/json; charset=utf-8".toMediaType()

    suspend fun fetchConfig(baseUrl: String): ConfigResponse? = withContext(Dispatchers.IO) {
        val request = Request.Builder()
            .url("$baseUrl/api/config")
            .get()
            .build()

        executeRequest(request)
    }

    suspend fun fetchPendingFiles(baseUrl: String): List<TrackedFile> = withContext(Dispatchers.IO) {
        val request = Request.Builder()
            .url("$baseUrl/api/files/pending")
            .get()
            .build()

        executeRequest<List<TrackedFile>>(request) ?: emptyList()
    }

    suspend fun fetchHistoryFiles(baseUrl: String, skip: Int, take: Int, search: String? = null): List<TrackedFile> = withContext(Dispatchers.IO) {
        val searchParam = if (!search.isNullOrEmpty()) "&search=${java.net.URLEncoder.encode(search, "UTF-8")}" else ""
        val request = Request.Builder()
            .url("$baseUrl/api/files?skip=$skip&take=$take$searchParam")
            .get()
            .build()

        executeRequest<List<TrackedFile>>(request) ?: emptyList()
    }

    suspend fun confirmCategory(baseUrl: String, hash: String, category: String): Boolean = withContext(Dispatchers.IO) {
        val bodyJson = json.encodeToString(ConfirmCategoryRequest.serializer(), ConfirmCategoryRequest(category))
        val body = bodyJson.toRequestBody(jsonMediaType)

        val request = Request.Builder()
            .url("$baseUrl/api/files/$hash/confirm")
            .post(body)
            .build()

        try {
            okHttpClient.newCall(request).execute().use { response ->
                response.isSuccessful || response.code == 202
            }
        } catch (e: IOException) {
            false
        }
    }

    fun listenEvents(baseUrl: String): Flow<SSEEvent> = callbackFlow {
        val request = Request.Builder()
            .url("$baseUrl/api/events")
            .header("Accept", "text/event-stream")
            .build()

        val listener = object : EventSourceListener() {
            override fun onOpen(eventSource: EventSource, response: Response) {
                trySend(SSEEvent.Connected)
            }

            override fun onEvent(eventSource: EventSource, id: String?, type: String?, data: String) {
                val event = when (type) {
                    "connected" -> SSEEvent.Connected
                    "file.move.progress" -> {
                        try {
                            val payload = json.decodeFromString<MoveProgressPayload>(data)
                            SSEEvent.Progress(payload)
                        } catch (e: Exception) {
                            null
                        }
                    }
                    "file.move.completed" -> {
                        try {
                            val payload = json.decodeFromString<MoveCompletedPayload>(data)
                            SSEEvent.Completed(payload)
                        } catch (e: Exception) {
                            null
                        }
                    }
                    "file.move.error" -> {
                        try {
                            val payload = json.decodeFromString<MoveErrorPayload>(data)
                            SSEEvent.Error(payload)
                        } catch (e: Exception) {
                            null
                        }
                    }
                    "file.ignored" -> {
                        try {
                            val payload = json.decodeFromString<FileIgnoredPayload>(data)
                            SSEEvent.FileIgnored(payload.hash)
                        } catch (e: Exception) {
                            null
                        }
                    }
                    "files.reclassified" -> SSEEvent.Reclassified
                    else -> null
                }
                if (event != null) {
                    trySend(event)
                }
            }

            override fun onFailure(eventSource: EventSource, t: Throwable?, response: Response?) {
                // Do not crash the flow, just close it or notify disconnection, okhttp-sse will naturally retry or we will reconnect
                trySend(SSEEvent.Error(MoveErrorPayload("", "SSE Connection Lost", t?.message ?: "Disconnected")))
            }

            override fun onClosed(eventSource: EventSource) {
                close()
            }
        }

        val sseFactory = EventSources.createFactory(okHttpClient)
        val eventSource = sseFactory.newEventSource(request, listener)

        awaitClose {
            eventSource.cancel()
        }
    }

    suspend fun ignoreFile(baseUrl: String, hash: String): Boolean = withContext(Dispatchers.IO) {
        val request = Request.Builder()
            .url("$baseUrl/api/files/$hash/ignore")
            .post("".toRequestBody(null))
            .build()
        try {
            okHttpClient.newCall(request).execute().use { response ->
                response.isSuccessful || response.code == 202
            }
        } catch (e: IOException) {
            false
        }
    }

    suspend fun fetchCategoryPresets(baseUrl: String): List<String> = withContext(Dispatchers.IO) {
        val request = Request.Builder()
            .url("$baseUrl/api/categories/presets")
            .get()
            .build()
        executeRequest<List<String>>(request) ?: emptyList()
    }

    suspend fun reclassifyUnconfirmed(baseUrl: String): Boolean = withContext(Dispatchers.IO) {
        val request = Request.Builder()
            .url("$baseUrl/api/files/reclassify-unconfirmed")
            .post("".toRequestBody(null))
            .build()
        try {
            okHttpClient.newCall(request).execute().use { response ->
                response.isSuccessful || response.code == 202
            }
        } catch (e: IOException) {
            false
        }
    }

    suspend fun updateConfig(baseUrl: String, mlThreshold: Double): Boolean = withContext(Dispatchers.IO) {
        val reqObj = UpdateConfigRequest(mlThreshold = mlThreshold)
        val bodyJson = json.encodeToString(UpdateConfigRequest.serializer(), reqObj)
        val body = bodyJson.toRequestBody(jsonMediaType)
        val request = Request.Builder()
            .url("$baseUrl/api/config")
            .post(body)
            .build()
        try {
            okHttpClient.newCall(request).execute().use { response ->
                response.isSuccessful
            }
        } catch (e: IOException) {
            false
        }
    }

    suspend fun moveFile(baseUrl: String, hash: String): Boolean = withContext(Dispatchers.IO) {
        val request = Request.Builder()
            .url("$baseUrl/api/files/$hash/move")
            .post("".toRequestBody(null))
            .build()
        try {
            okHttpClient.newCall(request).execute().use { response ->
                response.isSuccessful || response.code == 202
            }
        } catch (e: IOException) {
            false
        }
    }

    suspend fun updateFile(baseUrl: String, hash: String, category: String, status: Int): Boolean = withContext(Dispatchers.IO) {
        val reqObj = UpdateFileRequest(category = category, status = status)
        val bodyJson = json.encodeToString(UpdateFileRequest.serializer(), reqObj)
        val body = bodyJson.toRequestBody(jsonMediaType)
        val request = Request.Builder()
            .url("$baseUrl/api/files/$hash/update")
            .post(body)
            .build()
        try {
            okHttpClient.newCall(request).execute().use { response ->
                response.isSuccessful || response.code == 200
            }
        } catch (e: IOException) {
            false
        }
    }

    suspend fun fetchFSList(baseUrl: String, path: String, showHidden: Boolean): List<FSItem> = withContext(Dispatchers.IO) {
        val encodedPath = java.net.URLEncoder.encode(path, "UTF-8")
        val request = Request.Builder()
            .url("$baseUrl/api/fs/list?path=$encodedPath&showHidden=$showHidden")
            .get()
            .build()
        executeRequest<List<FSItem>>(request) ?: emptyList()
    }

    private inline fun <reified T> executeRequest(request: Request): T? {
        return try {
            okHttpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return null
                val bodyStr = response.body?.string() ?: return null
                json.decodeFromString<T>(bodyStr)
            }
        } catch (e: IOException) {
            null
        }
    }
}
