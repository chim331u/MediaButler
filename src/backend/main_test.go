package main

import (
	"bytes"
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"
)

func TestInitDBAndSchema(t *testing.T) {
	// Create a temporary directory for the database
	tempDir, err := os.MkdirTemp("", "mediabutler-test-*")
	if err != nil {
		t.Fatalf("Failed to create temp dir: %v", err)
	}
	defer os.RemoveAll(tempDir)

	dbPath := filepath.Join(tempDir, "test_mediabutler.db")

	// 1. Initialize SQLite Database
	db, err := InitDB(dbPath)
	if err != nil {
		t.Fatalf("InitDB failed: %v", err)
	}
	defer db.Close()

	// 2. Ensure Schema
	if err := EnsureSchema(db); err != nil {
		t.Fatalf("EnsureSchema failed: %v", err)
	}

	// 3. Verify TrackedFiles and UserPreferences tables exist
	var tableName string
	err = db.QueryRow("SELECT name FROM sqlite_master WHERE type='table' AND name='TrackedFiles'").Scan(&tableName)
	if err != nil {
		t.Fatalf("Failed to query sqlite_master for TrackedFiles: %v", err)
	}
	if tableName != "TrackedFiles" {
		t.Errorf("Expected table 'TrackedFiles', got '%s'", tableName)
	}

	err = db.QueryRow("SELECT name FROM sqlite_master WHERE type='table' AND name='UserPreferences'").Scan(&tableName)
	if err != nil {
		t.Fatalf("Failed to query sqlite_master for UserPreferences: %v", err)
	}
	if tableName != "UserPreferences" {
		t.Errorf("Expected table 'UserPreferences', got '%s'", tableName)
	}
}

func TestAPIRoutes(t *testing.T) {
	// Create a temporary directory for the database
	tempDir, err := os.MkdirTemp("", "mediabutler-api-test-*")
	if err != nil {
		t.Fatalf("Failed to create temp dir: %v", err)
	}
	defer os.RemoveAll(tempDir)

	dbPath := filepath.Join(tempDir, "test_api.db")
	db, err := InitDB(dbPath)
	if err != nil {
		t.Fatalf("Failed to initialize database: %v", err)
	}
	defer db.Close()

	if err := EnsureSchema(db); err != nil {
		t.Fatalf("Failed to verify schema: %v", err)
	}

	cfg := LoadConfig()
	sse := NewSSEBroker()
	server := NewServer(cfg, db, sse, nil)

	mux := http.NewServeMux()
	server.RegisterRoutes(mux)

	// 1. Test POST /api/files (Register file)
	reqBody := AddFileRequest{FilePath: "/temp/watch/test_movie.mkv"}
	bodyBytes, _ := json.Marshal(reqBody)
	req := httptest.NewRequest(http.MethodPost, "/api/files", bytes.NewReader(bodyBytes))
	w := httptest.NewRecorder()

	mux.ServeHTTP(w, req)

	if w.Code != http.StatusOK {
		t.Errorf("POST /api/files: expected status 200, got %d. Body: %s", w.Code, w.Body.String())
	}

	var registeredFile TrackedFile
	if err := json.NewDecoder(w.Body).Decode(&registeredFile); err != nil {
		t.Fatalf("Failed to decode response: %v", err)
	}

	if registeredFile.FileName != "test_movie.mkv" {
		t.Errorf("Expected filename 'test_movie.mkv', got '%s'", registeredFile.FileName)
	}

	if registeredFile.Hash == "" {
		t.Errorf("Expected a non-empty SHA256 hash")
	}

	// 2. Test GET /api/files (Get all files)
	req = httptest.NewRequest(http.MethodGet, "/api/files", nil)
	w = httptest.NewRecorder()
	mux.ServeHTTP(w, req)

	if w.Code != http.StatusOK {
		t.Errorf("GET /api/files: expected status 200, got %d", w.Code)
	}

	var filesList []TrackedFile
	if err := json.NewDecoder(w.Body).Decode(&filesList); err != nil {
		t.Fatalf("Failed to decode files list: %v", err)
	}

	if len(filesList) != 1 {
		t.Errorf("Expected 1 file in list, got %d", len(filesList))
	}

	// 3. Test GET /api/files/{hash} (Get specific file)
	req = httptest.NewRequest(http.MethodGet, "/api/files/"+registeredFile.Hash, nil)
	w = httptest.NewRecorder()
	mux.ServeHTTP(w, req)

	if w.Code != http.StatusOK {
		t.Errorf("GET /api/files/{hash}: expected status 200, got %d", w.Code)
	}

	var singleFile TrackedFile
	json.NewDecoder(w.Body).Decode(&singleFile)
	if singleFile.Hash != registeredFile.Hash {
		t.Errorf("Expected hash '%s', got '%s'", registeredFile.Hash, singleFile.Hash)
	}
}

func TestFileWatcher(t *testing.T) {
	// 1. Create a temporary directory for database and watching
	tempDir, err := os.MkdirTemp("", "mediabutler-watcher-test-*")
	if err != nil {
		t.Fatalf("Failed to create temp dir: %v", err)
	}
	defer os.RemoveAll(tempDir)

	dbPath := filepath.Join(tempDir, "test_watcher.db")
	watchFolder := filepath.Join(tempDir, "watch")
	
	// Create watch folder
	if err := os.MkdirAll(watchFolder, 0755); err != nil {
		t.Fatalf("Failed to create watch folder: %v", err)
	}

	// 2. Initialize database
	db, err := InitDB(dbPath)
	if err != nil {
		t.Fatalf("Failed to initialize database: %v", err)
	}
	defer db.Close()

	if err := EnsureSchema(db); err != nil {
		t.Fatalf("Failed to verify schema: %v", err)
	}

	// 3. Initialize and start the Watcher
	cfg := Config{
		DatabasePath: dbPath,
		WatchFolders: []string{watchFolder},
		MLThreshold:  0.85,
	}

	watcher, err := NewWatcher(cfg, db, nil)
	if err != nil {
		t.Fatalf("Failed to create watcher: %v", err)
	}

	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	watcher.Start(ctx)

	// Wait for watcher routine to bind and initialize
	time.Sleep(500 * time.Millisecond)

	// 4. Simulate a slow file write into the watch folder
	testFilePath := filepath.Join(watchFolder, "stability_test_movie.mp4")
	file, err := os.Create(testFilePath)
	if err != nil {
		t.Fatalf("Failed to create test file: %v", err)
	}

	// Write first block of bytes
	_, err = file.Write([]byte("Sample video content part 1... "))
	if err != nil {
		t.Fatalf("Failed to write to file: %v", err)
	}
	file.Close()

	// Wait 2 seconds (less than stability interval of 5s) and write more content
	time.Sleep(2 * time.Second)
	file, err = os.OpenFile(testFilePath, os.O_APPEND|os.O_WRONLY, 0644)
	if err != nil {
		t.Fatalf("Failed to reopen test file: %v", err)
	}
	_, err = file.Write([]byte("Sample video content final block!"))
	if err != nil {
		t.Fatalf("Failed to write final block to file: %v", err)
	}
	file.Close()

	// The stability check interval is 5 seconds. Since we wrote the final block,
	// let's wait 9.5 seconds for size stability to be verified (requires two consecutive ticks of 5s) and the file to be processed.
	time.Sleep(9500 * time.Millisecond)

	// 5. Query the database to verify the file was registered
	var dbFileName string
	var dbFileSize int64
	var dbStatus int
	var dbHash string
	
	err = db.QueryRow("SELECT FileName, FileSize, Status, Hash FROM TrackedFiles LIMIT 1").Scan(&dbFileName, &dbFileSize, &dbStatus, &dbHash)
	if err != nil {
		t.Fatalf("Failed to query TrackedFiles from database: %v. Watcher might not have completed processing.", err)
	}

	if dbFileName != "stability_test_movie.mp4" {
		t.Errorf("Expected database record FileName 'stability_test_movie.mp4', got '%s'", dbFileName)
	}

	expectedSize := int64(len("Sample video content part 1... Sample video content final block!"))
	if dbFileSize != expectedSize {
		t.Errorf("Expected registered FileSize %d, got %d", expectedSize, dbFileSize)
	}

	// Since we don't have training data yet, classification will fall back to UNKNOWN, so status will be FileStatusNew (0)
	if dbStatus != int(FileStatusNew) {
		t.Errorf("Expected registered Status 0 (FileStatusNew), got %d", dbStatus)
	}

	// Calculate local hash to match database hash
	localHash, _ := CalculateSHA256(testFilePath)
	if dbHash != localHash {
		t.Errorf("Expected db Hash '%s', got '%s'", localHash, dbHash)
	}
}

func TestClassifier(t *testing.T) {
	// 1. Title cleaning
	cleaned1 := CleanFilename("Breaking.Bad.S01E01.1080p.bluray.x264.mkv")
	if cleaned1 != "breaking bad" {
		t.Errorf("CleanFilename: expected 'breaking bad', got '%s'", cleaned1)
	}

	cleaned2 := CleanFilename("[AnimeGroup] Frieren - 05 [1080p HEVC].mkv")
	if cleaned2 != "frieren" {
		t.Errorf("CleanFilename: expected 'frieren', got '%s'", cleaned2)
	}

	// 2. Jaro-Winkler
	score1 := JaroWinkler("Breaking Bad", "breaking bad")
	if score1 != 1.0 {
		t.Errorf("JaroWinkler exact: expected 1.0, got %f", score1)
	}

	score2 := JaroWinkler("Breaking Bad", "Breaking Bd")
	if score2 < 0.90 {
		t.Errorf("JaroWinkler fuzzy: expected high score, got %f", score2)
	}

	// 3. Naive Bayes & Incremental Learning
	tempDir, err := os.MkdirTemp("", "mediabutler-classifier-test-*")
	if err != nil {
		t.Fatalf("Failed to create temp dir: %v", err)
	}
	defer os.RemoveAll(tempDir)

	dbPath := filepath.Join(tempDir, "test_classifier.db")
	db, err := InitDB(dbPath)
	if err != nil {
		t.Fatalf("Failed to open DB: %v", err)
	}
	defer db.Close()

	if err := EnsureSchema(db); err != nil {
		t.Fatalf("Failed to ensure schema: %v", err)
	}
	if err := EnsureModelSchema(db); err != nil {
		t.Fatalf("Failed to ensure model schema: %v", err)
	}

	// Train model
	LearnClassification(db, "breaking bad", "TV SHOWS")
	LearnClassification(db, "better call saul", "TV SHOWS")
	LearnClassification(db, "inception", "MOVIES")
	LearnClassification(db, "interstellar", "MOVIES")

	// Wait for background training transactions to finish
	time.Sleep(200 * time.Millisecond)

	// Predict
	cat, conf, err := ClassifyNaiveBayes(db, "breaking bad")
	if err != nil {
		t.Fatalf("ClassifyNaiveBayes failed: %v", err)
	}
	if cat != "TV SHOWS" {
		t.Errorf("ClassifyNaiveBayes: expected 'TV SHOWS', got '%s' (conf: %f)", cat, conf)
	}

	cat2, conf2, err := ClassifyNaiveBayes(db, "interstellar movie")
	if err != nil {
		t.Fatalf("ClassifyNaiveBayes failed: %v", err)
	}
	if cat2 != "MOVIES" {
		t.Errorf("ClassifyNaiveBayes: expected 'MOVIES', got '%s' (conf: %f)", cat2, conf2)
	}
}

func TestSSEBroker(t *testing.T) {
	sse := NewSSEBroker()

	// Create test client channel
	ch := make(chan string, 5)
	sse.Register(ch)

	// Test registration
	sse.mu.RLock()
	clientsCount := len(sse.clients)
	sse.mu.RUnlock()
	if clientsCount != 1 {
		t.Errorf("Expected 1 client registered, got %d", clientsCount)
	}

	// Test Broadcast
	sse.Broadcast("test.event", `{"key":"value"}`)

	select {
	case msg := <-ch:
		expected := "event: test.event\ndata: {\"key\":\"value\"}\n\n"
		if msg != expected {
			t.Errorf("Expected message '%s', got '%s'", expected, msg)
		}
	case <-time.After(500 * time.Millisecond):
		t.Errorf("Timed out waiting for broadcast event")
	}

	// Test Unregister
	sse.Unregister(ch)
	sse.mu.RLock()
	clientsCount = len(sse.clients)
	sse.mu.RUnlock()
	if clientsCount != 0 {
		t.Errorf("Expected 0 clients registered after unregister, got %d", clientsCount)
	}
}

func TestAsyncFileMove(t *testing.T) {
	tempDir, err := os.MkdirTemp("", "mediabutler-move-test-*")
	if err != nil {
		t.Fatalf("Failed to create temp dir: %v", err)
	}
	defer os.RemoveAll(tempDir)

	dbPath := filepath.Join(tempDir, "test_move.db")
	watchFolder := filepath.Join(tempDir, "watch")
	destFolder := filepath.Join(tempDir, "dest")

	_ = os.MkdirAll(watchFolder, 0755)
	_ = os.MkdirAll(destFolder, 0755)

	db, err := InitDB(dbPath)
	if err != nil {
		t.Fatalf("InitDB failed: %v", err)
	}
	defer db.Close()

	_ = EnsureSchema(db)
	_ = EnsureModelSchema(db)

	sse := NewSSEBroker()
	cfg := Config{
		DatabasePath: dbPath,
		WatchFolders: []string{watchFolder},
		DestFolder:   destFolder,
		MLThreshold:  0.85,
	}
	server := NewServer(cfg, db, sse, nil)
	mux := http.NewServeMux()
	server.RegisterRoutes(mux)

	// Create dummy video file and subtitle file
	videoPath := filepath.Join(watchFolder, "breaking_bad_s01e01.mkv")
	srtPath := filepath.Join(watchFolder, "breaking_bad_s01e01.srt")

	// Make it about 2MB to ensure progress updates trigger (2 blocks of 1MB)
	videoData := bytes.Repeat([]byte("A"), 2*1024*1024)
	if err := os.WriteFile(videoPath, videoData, 0644); err != nil {
		t.Fatalf("Failed to write video file: %v", err)
	}
	if err := os.WriteFile(srtPath, []byte("1\n00:00:01,000 -> 00:00:04,000\nHello Breaking Bad!"), 0644); err != nil {
		t.Fatalf("Failed to write srt file: %v", err)
	}

	hash, err := CalculateSHA256(videoPath)
	if err != nil {
		t.Fatalf("Failed to hash: %v", err)
	}

	// Insert into DB as Classified
	now := time.Now()
	_, err = db.Exec(`
		INSERT INTO TrackedFiles (Hash, FileName, OriginalPath, FileSize, Status, CreatedDate, LastUpdateDate, IsActive)
		VALUES (?, ?, ?, ?, ?, ?, ?, 1)
	`, hash, "breaking_bad_s01e01.mkv", videoPath, int64(len(videoData)), FileStatusClassified, now, now)
	if err != nil {
		t.Fatalf("Failed to insert file: %v", err)
	}

	// Register a client channel to listen to SSE events
	sseChan := make(chan string, 10)
	sse.Register(sseChan)
	defer sse.Unregister(sseChan)

	// Trigger confirm category POST /api/files/{hash}/confirm (only confirms, doesn't move)
	confirmReq := ConfirmCategoryRequest{Category: "TV SHOWS"}
	confirmBytes, _ := json.Marshal(confirmReq)
	req := httptest.NewRequest(http.MethodPost, "/api/files/"+hash+"/confirm", bytes.NewReader(confirmBytes))
	w := httptest.NewRecorder()

	mux.ServeHTTP(w, req)

	if w.Code != http.StatusOK {
		t.Errorf("Expected 200 OK, got %d. Body: %s", w.Code, w.Body.String())
	}

	// Read and verify response JSON
	var resp map[string]string
	if err := json.NewDecoder(w.Body).Decode(&resp); err != nil {
		t.Fatalf("Failed to decode response: %v", err)
	}
	if resp["status"] != "Confirmed" {
		t.Errorf("Expected status 'Confirmed', got '%s'", resp["status"])
	}

	// Trigger actual organization move POST /api/files/"+hash+"/move
	reqMove := httptest.NewRequest(http.MethodPost, "/api/files/"+hash+"/move", nil)
	wMove := httptest.NewRecorder()
	mux.ServeHTTP(wMove, reqMove)

	if wMove.Code != http.StatusAccepted {
		t.Errorf("Expected 202 Accepted, got %d. Body: %s", wMove.Code, wMove.Body.String())
	}

	var respMove map[string]string
	if err := json.NewDecoder(wMove.Body).Decode(&respMove); err != nil {
		t.Fatalf("Failed to decode move response: %v", err)
	}
	if respMove["status"] != "Moving" {
		t.Errorf("Expected status 'Moving', got '%s'", respMove["status"])
	}

	// Collect SSE events to verify we receive progress and completion events
	progressCount := 0
	completedReceived := false

	timeout := time.After(5 * time.Second)
OuterLoop:
	for {
		select {
		case msg := <-sseChan:
			if strings.Contains(msg, "file.move.progress") {
				progressCount++
			}
			if strings.Contains(msg, "file.move.completed") {
				completedReceived = true
			}
		case <-time.After(200 * time.Millisecond):
			// Check DB to see if state is Moved
			var status int
			_ = db.QueryRow("SELECT Status FROM TrackedFiles WHERE Hash = ?", hash).Scan(&status)
			if status == int(FileStatusMoved) {
				// Let's drain any remaining SSE messages and break
				time.Sleep(100 * time.Millisecond)
				for len(sseChan) > 0 {
					msg := <-sseChan
					if strings.Contains(msg, "file.move.progress") {
						progressCount++
					}
					if strings.Contains(msg, "file.move.completed") {
						completedReceived = true
					}
				}
				break OuterLoop
			}
		case <-timeout:
			t.Fatal("Timeout waiting for async move to complete")
		}
	}

	// Verify SSE broadcasts
	if progressCount == 0 {
		t.Errorf("Expected at least one progress event, got %d", progressCount)
	}
	if !completedReceived {
		t.Errorf("Expected completed event, but did not receive one")
	}

	// Verify files moved in filesystem
	targetVideoPath := filepath.Join(destFolder, "TV SHOWS", "breaking_bad_s01e01.mkv")
	targetSrtPath := filepath.Join(destFolder, "TV SHOWS", "breaking_bad_s01e01.srt")

	if _, err := os.Stat(targetVideoPath); os.IsNotExist(err) {
		t.Errorf("Target video file does not exist: %s", targetVideoPath)
	}
	if _, err := os.Stat(targetSrtPath); os.IsNotExist(err) {
		t.Errorf("Target srt file does not exist: %s", targetSrtPath)
	}

	if _, err := os.Stat(videoPath); !os.IsNotExist(err) {
		t.Errorf("Original video file still exists at source path: %s", videoPath)
	}
	if _, err := os.Stat(srtPath); !os.IsNotExist(err) {
		t.Errorf("Original srt file still exists at source path: %s", srtPath)
	}

	// Verify DB record matches
	var dbStatus int
	var dbMovedToPath string
	err = db.QueryRow("SELECT Status, MovedToPath FROM TrackedFiles WHERE Hash = ?", hash).Scan(&dbStatus, &dbMovedToPath)
	if err != nil {
		t.Fatalf("Failed to query DB for final status: %v", err)
	}
	if dbStatus != int(FileStatusMoved) {
		t.Errorf("Expected DB Status 5 (Moved), got %d", dbStatus)
	}
	if dbMovedToPath != targetVideoPath {
		t.Errorf("Expected DB MovedToPath '%s', got '%s'", targetVideoPath, dbMovedToPath)
	}
}

func TestManualRescan(t *testing.T) {
	tempDir, err := os.MkdirTemp("", "mediabutler_rescan_*")
	if err != nil {
		t.Fatalf("Failed to create temp dir: %v", err)
	}
	defer os.RemoveAll(tempDir)

	dbPath := filepath.Join(tempDir, "test.db")
	db, err := InitDB(dbPath)
	if err != nil {
		t.Fatalf("Failed to initialize DB: %v", err)
	}
	defer db.Close()
	_ = EnsureSchema(db)

	watchFolder := filepath.Join(tempDir, "watch")
	_ = os.MkdirAll(watchFolder, 0755)

	cfg := Config{
		DatabasePath: dbPath,
		WatchFolders: []string{watchFolder},
		DestFolder:   filepath.Join(tempDir, "dest"),
		MLThreshold:  0.85,
	}

	watcher, err := NewWatcher(cfg, db, nil)
	if err != nil {
		t.Fatalf("Failed to initialize watcher: %v", err)
	}

	// 1. Create a file when watcher is NOT running
	testFile := filepath.Join(watchFolder, "orphan_movie.mkv")
	if err := os.WriteFile(testFile, []byte("movie content"), 0644); err != nil {
		t.Fatalf("Failed to write test file: %v", err)
	}

	// 2. Trigger Manual Scan
	watcher.ManualScan(context.Background())

	// Wait briefly for stability check and db registration (needs at least 10s for 2 ticks of 5s)
	time.Sleep(11 * time.Second)

	// 3. Verify it is registered in the database!
	var count int
	err = db.QueryRow("SELECT COUNT(*) FROM TrackedFiles WHERE FileName = ?", "orphan_movie.mkv").Scan(&count)
	if err != nil {
		t.Fatalf("Database query failed: %v", err)
	}
	if count == 0 {
		t.Errorf("Expected file 'orphan_movie.mkv' to be registered in DB after manual rescan, but it was not")
	}
}

func TestClassifierRetrain(t *testing.T) {
	tempDir, err := os.MkdirTemp("", "mediabutler_retrain_*")
	if err != nil {
		t.Fatalf("Failed to create temp dir: %v", err)
	}
	defer os.RemoveAll(tempDir)

	dbPath := filepath.Join(tempDir, "test.db")
	db, err := InitDB(dbPath)
	if err != nil {
		t.Fatalf("Failed to initialize DB: %v", err)
	}
	defer db.Close()
	_ = EnsureSchema(db)
	_ = EnsureModelSchema(db)

	// 1. Insert some historical Moved records manually
	_, _ = db.Exec(`
		INSERT INTO TrackedFiles (Hash, FileName, OriginalPath, FileSize, Status, Category, CreatedDate, LastUpdateDate, IsActive)
		VALUES ('hash1', 'interstellar.mkv', '/path/interstellar.mkv', 1000, 5, 'MOVIES', datetime('now'), datetime('now'), 1),
		       ('hash2', 'breaking_bad_s01e01.mkv', '/path/breaking_bad_s01e01.mkv', 2000, 5, 'TV SHOWS', datetime('now'), datetime('now'), 1)
	`)

	// 2. Trigger Retrain
	err = RetrainModel(db)
	if err != nil {
		t.Fatalf("Failed to retrain model: %v", err)
	}

	// 3. Verify word frequencies are populated!
	var count int
	err = db.QueryRow("SELECT COUNT(*) FROM model_word_frequencies").Scan(&count)
	if err != nil {
		t.Fatalf("Failed to query model frequencies: %v", err)
	}
	if count == 0 {
		t.Errorf("Expected word frequencies to be populated after retraining, but got 0 records")
	}

	// 4. Test predictions!
	cat, conf, err := PredictCategory(db, "interstellar_1080p.mkv")
	if err != nil {
		t.Fatalf("Prediction failed: %v", err)
	}
	if cat != "MOVIES" {
		t.Errorf("Expected predicted category 'MOVIES', got '%s' (conf: %f)", cat, conf)
	}
}

func TestCategoriesAndMoveEndpoints(t *testing.T) {
	tempDir, err := os.MkdirTemp("", "mediabutler_catmove_*")
	if err != nil {
		t.Fatalf("Failed to create temp dir: %v", err)
	}
	defer os.RemoveAll(tempDir)

	dbPath := filepath.Join(tempDir, "test.db")
	db, err := InitDB(dbPath)
	if err != nil {
		t.Fatalf("Failed to initialize DB: %v", err)
	}
	defer db.Close()
	_ = EnsureSchema(db)
	_ = EnsureModelSchema(db)

	cfg := Config{
		DatabasePath: dbPath,
		WatchFolders: []string{filepath.Join(tempDir, "watch")},
		DestFolder:   filepath.Join(tempDir, "dest"),
		MLThreshold:  0.85,
		Port:         "9999",
	}

	sse := NewSSEBroker()
	server := NewServer(cfg, db, sse, nil)

	watchPath := filepath.Join(tempDir, "watch", "movie1.mkv")

	// 1. Insert some file records with category and without category
	_, _ = db.Exec(`
		INSERT INTO TrackedFiles (Hash, FileName, OriginalPath, FileSize, Status, Category, SuggestedCategory, Confidence, CreatedDate, LastUpdateDate, IsActive)
		VALUES ('hash1', 'movie1.mkv', ?, 100, 2, NULL, 'MOVIES', 0.90, datetime('now'), datetime('now'), 1),
		       ('hash2', 'movie2.mkv', '/watch/movie2.mkv', 200, 5, 'MOVIES', 'MOVIES', 0.95, datetime('now'), datetime('now'), 1),
		       ('hash3', 'show1.mkv', '/watch/show1.mkv', 300, 5, 'TV SHOWS', 'TV SHOWS', 0.98, datetime('now'), datetime('now'), 1)
	`, watchPath)

	// Create files on disk so move won't fail with path error
	_ = os.MkdirAll(filepath.Join(tempDir, "watch"), 0755)
	_ = os.MkdirAll(filepath.Join(tempDir, "dest"), 0755)
	_ = os.WriteFile(watchPath, []byte("data"), 0644)

	// 2. Test handleCategories endpoint
	req, _ := http.NewRequest("GET", "/api/categories", nil)
	rr := httptest.NewRecorder()
	handler := http.HandlerFunc(server.handleCategories)
	handler.ServeHTTP(rr, req)

	if rr.Code != http.StatusOK {
		t.Errorf("Expected status 200 for categories, got %d", rr.Code)
	}

	var categories []string
	if err := json.NewDecoder(rr.Body).Decode(&categories); err != nil {
		t.Fatalf("Failed to decode categories response: %v", err)
	}

	if len(categories) != 2 || categories[0] != "MOVIES" || categories[1] != "TV SHOWS" {
		t.Errorf("Expected distinct sorted categories ['MOVIES', 'TV SHOWS'], got %v", categories)
	}

	// 3. Test confirm endpoint (only updates status to Confirmed/ReadyToMove, does not move)
	confirmReqBody := `{"category":"MOVIES"}`
	req, _ = http.NewRequest("POST", "/api/files/hash1/confirm", strings.NewReader(confirmReqBody))
	rr = httptest.NewRecorder()
	server.confirmFileCategory(rr, req, "hash1")

	if rr.Code != http.StatusOK {
		t.Errorf("Expected status 200 for confirm, got %d, body: %s", rr.Code, rr.Body.String())
	}

	// Verify status in DB is 3 (ReadyToMove/Confirmed)
	var status int
	var cat string
	err = db.QueryRow("SELECT Status, Category FROM TrackedFiles WHERE Hash = 'hash1'").Scan(&status, &cat)
	if err != nil || status != 3 || cat != "MOVIES" {
		t.Errorf("Expected status 3 and category 'MOVIES' in DB, got status %d, category %s, err: %v", status, cat, err)
	}

	// Verify file is still in watch folder (not moved yet!)
	if _, err := os.Stat(filepath.Join(tempDir, "watch", "movie1.mkv")); os.IsNotExist(err) {
		t.Errorf("File should NOT have been moved by confirm endpoint")
	}

	// 4. Test move endpoint
	req, _ = http.NewRequest("POST", "/api/files/hash1/move", nil)
	rr = httptest.NewRecorder()
	server.moveFile(rr, req, "hash1")

	if rr.Code != http.StatusAccepted {
		t.Errorf("Expected status 202 Accepted for move, got %d, body: %s", rr.Code, rr.Body.String())
	}

	// Wait for goroutine move to complete (up to 2 seconds)
	success := false
	for i := 0; i < 40; i++ {
		if _, err := os.Stat(filepath.Join(tempDir, "dest", "MOVIES", "movie1.mkv")); err == nil {
			success = true
			break
		}
		time.Sleep(50 * time.Millisecond)
	}
	if !success {
		t.Fatalf("Timed out waiting for file to be moved to destination path")
	}

	// 5. Test classify endpoint
	// First insert a new file without a suggested category
	_, _ = db.Exec(`
		INSERT INTO TrackedFiles (Hash, FileName, OriginalPath, FileSize, Status, Category, SuggestedCategory, Confidence, CreatedDate, LastUpdateDate, IsActive)
		VALUES ('hash_new', 'interstellar_2014.mkv', '/watch/interstellar_2014.mkv', 150, 0, NULL, NULL, 0.0, datetime('now'), datetime('now'), 1)
	`)

	// Prime classifier weights for MOVIES
	LearnClassification(db, "interstellar", "MOVIES")
	time.Sleep(300 * time.Millisecond)

	req, _ = http.NewRequest("POST", "/api/files/hash_new/classify", nil)
	rr = httptest.NewRecorder()
	server.classifyFile(rr, req, "hash_new")

	if rr.Code != http.StatusOK {
		t.Errorf("Expected status 200 for classify, got %d, body: %s", rr.Code, rr.Body.String())
	}

	var resData map[string]interface{}
	if err := json.Unmarshal(rr.Body.Bytes(), &resData); err != nil {
		t.Fatalf("Failed to parse classify response: %v", err)
	}

	if resData["suggestedCategory"] != "MOVIES" {
		t.Errorf("Expected suggestedCategory to be 'MOVIES', got %v", resData["suggestedCategory"])
	}

	// Verify status in DB became 2 (Classified)
	var newStatus int
	var newSugCat string
	err = db.QueryRow("SELECT Status, SuggestedCategory FROM TrackedFiles WHERE Hash = 'hash_new'").Scan(&newStatus, &newSugCat)
	if err != nil || newStatus != 2 || newSugCat != "MOVIES" {
		t.Errorf("Expected newStatus to be 2 and SuggestedCategory to be 'MOVIES' in DB, got status %d, suggestedCategory %s, err: %v", newStatus, newSugCat, err)
	}
}

func TestFSList(t *testing.T) {
	// Create a temporary directory structure
	tempDir, err := os.MkdirTemp("", "mediabutler_fslist_*")
	if err != nil {
		t.Fatalf("Failed to create temp dir: %v", err)
	}
	defer os.RemoveAll(tempDir)

	watchFolder := filepath.Join(tempDir, "watch")
	destFolder := filepath.Join(tempDir, "dest")
	outsideFolder := filepath.Join(tempDir, "outside")

	// Create directories
	_ = os.MkdirAll(filepath.Join(watchFolder, "folderB"), 0755)
	_ = os.MkdirAll(filepath.Join(watchFolder, "folderA"), 0755)
	_ = os.MkdirAll(filepath.Join(watchFolder, "@Recycle"), 0755)
	_ = os.MkdirAll(filepath.Join(destFolder, "Movies"), 0755)
	_ = os.MkdirAll(outsideFolder, 0755)

	// Create files
	_ = os.WriteFile(filepath.Join(watchFolder, "fileB.mkv"), []byte("20 bytes of dummy data"), 0644)
	_ = os.WriteFile(filepath.Join(watchFolder, "fileA.mp4"), []byte("10 bytes!!"), 0644)
	_ = os.WriteFile(filepath.Join(watchFolder, ".hiddenFile"), []byte("hidden"), 0644)
	_ = os.WriteFile(filepath.Join(outsideFolder, "secret.txt"), []byte("secret"), 0644)

	cfg := Config{
		WatchFolders: []string{watchFolder},
		DestFolder:   destFolder,
	}

	server := NewServer(cfg, nil, nil, nil)
	mux := http.NewServeMux()
	server.RegisterRoutes(mux)

	// 1. Authorized path calls - normal request (showHidden=false by default)
	req, _ := http.NewRequest("GET", "/api/fs/list?path="+watchFolder, nil)
	rr := httptest.NewRecorder()
	mux.ServeHTTP(rr, req)

	if rr.Code != http.StatusOK {
		t.Fatalf("Expected status 200, got %d. Body: %s", rr.Code, rr.Body.String())
	}

	var items []FSItem
	if err := json.Unmarshal(rr.Body.Bytes(), &items); err != nil {
		t.Fatalf("Failed to decode response: %v", err)
	}

	// Should contain: folderA, folderB (directories), fileA.mp4, fileB.mkv (files).
	// Sorted: folderA, folderB, fileA.mp4, fileB.mkv.
	// Hidden files (.hiddenFile, @Recycle) should be filtered out by default.
	if len(items) != 4 {
		t.Errorf("Expected 4 items, got %d. Items: %+v", len(items), items)
	}

	expectedNames := []string{"folderA", "folderB", "fileA.mp4", "fileB.mkv"}
	for i, expected := range expectedNames {
		if i < len(items) && items[i].Name != expected {
			t.Errorf("At index %d: expected name '%s', got '%s'", i, expected, items[i].Name)
		}
	}

	// Verify size and Dir flag
	if len(items) >= 4 {
		if !items[0].IsDir {
			t.Errorf("Expected folderA to be a directory")
		}
		if items[0].SizeBytes != 0 {
			t.Errorf("Expected folder size to be 0, got %d", items[0].SizeBytes)
		}
		if items[2].IsDir {
			t.Errorf("Expected fileA.mp4 to be a file")
		}
		if items[2].SizeBytes != 10 {
			t.Errorf("Expected fileA.mp4 size to be 10, got %d", items[2].SizeBytes)
		}
	}

	// 2. Test showHidden=true (must show hidden files and @ folders)
	req, _ = http.NewRequest("GET", "/api/fs/list?path="+watchFolder+"&showHidden=true", nil)
	rr = httptest.NewRecorder()
	mux.ServeHTTP(rr, req)

	if rr.Code != http.StatusOK {
		t.Fatalf("Expected status 200, got %d", rr.Code)
	}

	var itemsWithHidden []FSItem
	_ = json.Unmarshal(rr.Body.Bytes(), &itemsWithHidden)

	// Should contain: @Recycle, folderA, folderB (directories), .hiddenFile, fileA.mp4, fileB.mkv
	// Sorted case-insensitive directories first, then files.
	foundHiddenFile := false
	foundRecycle := false
	for _, item := range itemsWithHidden {
		if item.Name == ".hiddenFile" {
			foundHiddenFile = true
		}
		if item.Name == "@Recycle" {
			foundRecycle = true
		}
	}

	if !foundHiddenFile || !foundRecycle {
		t.Errorf("Expected to find hidden items .hiddenFile and @Recycle when showHidden=true. Got: %+v", itemsWithHidden)
	}

	// 3. Traversal protection - try to access outside allowed directories (e.g. outsideFolder)
	req, _ = http.NewRequest("GET", "/api/fs/list?path="+outsideFolder, nil)
	rr = httptest.NewRecorder()
	mux.ServeHTTP(rr, req)

	if rr.Code != http.StatusForbidden {
		t.Errorf("Expected status 403 Forbidden for outside path, got %d", rr.Code)
	}

	// 4. Traversal protection - try with directory traversal path (e.g. watchFolder/../outside)
	traversalPath := filepath.Join(watchFolder, "..", "outside")
	req, _ = http.NewRequest("GET", "/api/fs/list?path="+traversalPath, nil)
	rr = httptest.NewRecorder()
	mux.ServeHTTP(rr, req)

	if rr.Code != http.StatusForbidden {
		t.Errorf("Expected status 403 Forbidden for traversal path, got %d", rr.Code)
	}

	// 5. Dest folder access (authorized)
	req, _ = http.NewRequest("GET", "/api/fs/list?path="+destFolder, nil)
	rr = httptest.NewRecorder()
	mux.ServeHTTP(rr, req)

	if rr.Code != http.StatusOK {
		t.Errorf("Expected status 200 for dest folder, got %d", rr.Code)
	}
}

