package main

import (
	"log/slog"
	"os"
	"strconv"
	"strings"
)

type Config struct {
	Port          string
	DatabasePath  string
	WatchFolders  []string
	DestFolder    string
	LogLevel      slog.Level
	MLThreshold   float64
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

	return Config{
		Port:          port,
		DatabasePath:  dbPath,
		WatchFolders:  watchFolders,
		DestFolder:    destFolder,
		LogLevel:      logLevel,
		MLThreshold:   mlThreshold,
	}
}

func getEnv(key, defaultValue string) string {
	if value, exists := os.LookupEnv(key); exists {
		return value
	}
	return defaultValue
}
