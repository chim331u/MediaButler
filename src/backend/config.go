package main

import (
	"database/sql"
	"log/slog"
	"os"
	"strconv"
	"strings"
)

type NotifyHubConfig struct {
	URL     string `json:"url"`
	APIKey  string `json:"apiKey"`
	Channel string `json:"channel"`
}

type Config struct {
	Port          string
	DatabasePath  string
	WatchFolders  []string
	DestFolder    string
	LogLevel      slog.Level
	MLThreshold   float64
	NotifyHub     NotifyHubConfig
}

func LoadConfig() Config {
	port := getEnv("PORT", "8080")
	dbPath := getEnv("DATABASE_PATH", "../temp/mediabutler.db")
	destFolder := getEnv("DEST_FOLDER", "../temp/dest")

	watchFoldersStr := getEnv("WATCH_FOLDERS", "../temp/watch")
	var watchFolders []string
	if watchFoldersStr != "" {
		watchFolders = strings.Split(watchFoldersStr, ",")
		for i, folder := range watchFolders {
			watchFolders[i] = strings.TrimSpace(folder)
		}
	}

	logLevelStr := getEnv("LOG_LEVEL", "info")
	var logLevel slog.Level
	switch strings.ToLower(logLevelStr) {
	case "debug":
		logLevel = slog.LevelDebug
	case "warn":
		logLevel = slog.LevelWarn
	case "error":
		logLevel = slog.LevelError
	default:
		logLevel = slog.LevelInfo
	}

	mlThresholdStr := getEnv("ML_THRESHOLD", "0.85")
	mlThreshold, err := strconv.ParseFloat(mlThresholdStr, 64)
	if err != nil {
		mlThreshold = 0.85
	}

	notifyHubURL := getEnv("NOTIFYHUB_URL", "http://localhost:30180")
	notifyHubAPIKey := getEnv("NOTIFYHUB_APIKEY", "")
	notifyHubChannel := getEnv("NOTIFYHUB_CHANNEL", "none")

	return Config{
		Port:          port,
		DatabasePath:  dbPath,
		WatchFolders:  watchFolders,
		DestFolder:    destFolder,
		LogLevel:      logLevel,
		MLThreshold:   mlThreshold,
		NotifyHub: NotifyHubConfig{
			URL:     notifyHubURL,
			APIKey:  notifyHubAPIKey,
			Channel: notifyHubChannel,
		},
	}
}

func getEnv(key, defaultValue string) string {
	if value, exists := os.LookupEnv(key); exists {
		return value
	}
	return defaultValue
}

func LoadNotifyHubConfigFromDB(db *sql.DB, defaultCfg NotifyHubConfig) NotifyHubConfig {
	cfg := defaultCfg
	rows, err := db.Query("SELECT Key, Value FROM UserPreferences WHERE Category = 'notifyhub' AND IsActive = 1")
	if err != nil {
		slog.Warn("Failed to query NotifyHub configs from DB, using defaults", "err", err)
		return cfg
	}
	defer rows.Close()

	for rows.Next() {
		var key, value string
		if err := rows.Scan(&key, &value); err != nil {
			continue
		}
		switch key {
		case "notifyhub_url":
			cfg.URL = value
		case "notifyhub_apikey":
			cfg.APIKey = value
		case "notifyhub_channel":
			cfg.Channel = value
		}
	}
	return cfg
}
