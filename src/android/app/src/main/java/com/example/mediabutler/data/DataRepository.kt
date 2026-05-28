package com.example.mediabutler.data

import android.content.Context
import kotlinx.coroutines.flow.Flow

interface DataRepository {
    fun getServerIp(): String
    fun setServerIp(ip: String)
    fun getServerUrl(): String
    suspend fun getConfig(): ConfigResponse?
    suspend fun getPendingFiles(): List<TrackedFile>
    suspend fun getHistoryFiles(skip: Int, take: Int): List<TrackedFile>
    suspend fun confirmCategory(hash: String, category: String): Boolean
    suspend fun moveFile(hash: String): Boolean
    suspend fun ignoreFile(hash: String): Boolean
    suspend fun getCategoryPresets(): List<String>
    suspend fun reclassifyUnconfirmed(): Boolean
    suspend fun updateMlThreshold(threshold: Double): Boolean
    suspend fun getFSList(path: String, showHidden: Boolean): List<FSItem>
    fun listenEvents(): Flow<SSEEvent>
}

class DefaultDataRepository(context: Context) : DataRepository {
    private val prefs = PreferencesManager(context)
    private val client = NetworkClient()

    override fun getServerIp(): String = prefs.getServerIp()

    override fun setServerIp(ip: String) {
        prefs.setServerIp(ip)
    }

    override fun getServerUrl(): String = prefs.getServerUrl()

    override suspend fun getConfig(): ConfigResponse? {
        return client.fetchConfig(prefs.getServerUrl())
    }

    override suspend fun getPendingFiles(): List<TrackedFile> {
        return client.fetchPendingFiles(prefs.getServerUrl())
    }

    override suspend fun getHistoryFiles(skip: Int, take: Int): List<TrackedFile> {
        return client.fetchHistoryFiles(prefs.getServerUrl(), skip, take)
    }

    override suspend fun confirmCategory(hash: String, category: String): Boolean {
        return client.confirmCategory(prefs.getServerUrl(), hash, category)
    }

    override suspend fun moveFile(hash: String): Boolean {
        return client.moveFile(prefs.getServerUrl(), hash)
    }

    override suspend fun ignoreFile(hash: String): Boolean {
        return client.ignoreFile(prefs.getServerUrl(), hash)
    }

    override suspend fun getCategoryPresets(): List<String> {
        return client.fetchCategoryPresets(prefs.getServerUrl())
    }

    override suspend fun reclassifyUnconfirmed(): Boolean {
        return client.reclassifyUnconfirmed(prefs.getServerUrl())
    }

    override suspend fun updateMlThreshold(threshold: Double): Boolean {
        return client.updateConfig(prefs.getServerUrl(), threshold)
    }

    override suspend fun getFSList(path: String, showHidden: Boolean): List<FSItem> {
        return client.fetchFSList(prefs.getServerUrl(), path, showHidden)
    }

    override fun listenEvents(): Flow<SSEEvent> {
        return client.listenEvents(prefs.getServerUrl())
    }
}
