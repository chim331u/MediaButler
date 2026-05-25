package com.example.mediabutler.data

import android.content.Context
import android.content.SharedPreferences

class PreferencesManager(context: Context) {
    private val prefs: SharedPreferences = context.getSharedPreferences("MediaButlerPrefs", Context.MODE_PRIVATE)

    companion object {
        private const val KEY_SERVER_IP = "server_ip"
        private const val DEFAULT_IP = "10.0.2.2:8080" // Loopback to host for Android emulator
    }

    fun getServerIp(): String {
        return prefs.getString(KEY_SERVER_IP, DEFAULT_IP) ?: DEFAULT_IP
    }

    fun setServerIp(ip: String) {
        val sanitized = ip.trim()
            .removePrefix("http://")
            .removePrefix("https://")
            .removeSuffix("/")
        prefs.edit().putString(KEY_SERVER_IP, sanitized).apply()
    }

    fun getServerUrl(): String {
        return "http://${getServerIp()}"
    }
}
