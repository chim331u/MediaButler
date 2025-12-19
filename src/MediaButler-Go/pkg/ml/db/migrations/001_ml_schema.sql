-- ML Training Schema for Go Naive Bayes Classifier
-- SQLite-based incremental training system
-- Compatible with existing MediaButler database

-- ============================================================================
-- Training Samples Table
-- ============================================================================
-- Stores user-confirmed filename categorizations for training
CREATE TABLE IF NOT EXISTS ml_samples (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  filename TEXT NOT NULL,              -- Original filename
  series_name TEXT NOT NULL,           -- Extracted series name (via tokenizer)
  class TEXT NOT NULL,                 -- TV series category (confirmed by user)
  tokens TEXT NOT NULL,                -- JSON array of tokens (for caching)
  created_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

  -- Indexes for fast queries
  CONSTRAINT check_filename_not_empty CHECK (LENGTH(filename) > 0),
  CONSTRAINT check_class_not_empty CHECK (LENGTH(class) > 0)
);

CREATE INDEX IF NOT EXISTS idx_ml_samples_class
ON ml_samples(class);

CREATE INDEX IF NOT EXISTS idx_ml_samples_created
ON ml_samples(created_date DESC);

-- ============================================================================
-- Class Statistics Table
-- ============================================================================
-- Stores document counts and token counts per class for P(class) computation
CREATE TABLE IF NOT EXISTS ml_class_stats (
  class TEXT PRIMARY KEY,              -- TV series category
  doc_count INTEGER NOT NULL DEFAULT 0,     -- Number of training samples
  total_tokens INTEGER NOT NULL DEFAULT 0,  -- Total tokens across all samples

  CONSTRAINT check_doc_count_positive CHECK (doc_count >= 0),
  CONSTRAINT check_total_tokens_positive CHECK (total_tokens >= 0)
);

-- ============================================================================
-- Token Statistics Table
-- ============================================================================
-- Stores token-class co-occurrence counts for P(token|class) computation
CREATE TABLE IF NOT EXISTS ml_token_stats (
  token TEXT NOT NULL,                 -- Token (word)
  class TEXT NOT NULL,                 -- TV series category
  count INTEGER NOT NULL DEFAULT 0,    -- Number of occurrences in this class

  PRIMARY KEY (token, class),
  CONSTRAINT check_count_positive CHECK (count >= 0)
);

CREATE INDEX IF NOT EXISTS idx_ml_token_stats_class
ON ml_token_stats(class);

CREATE INDEX IF NOT EXISTS idx_ml_token_stats_token
ON ml_token_stats(token);

-- ============================================================================
-- IDF Statistics Table
-- ============================================================================
-- Stores document frequency for IDF computation
CREATE TABLE IF NOT EXISTS ml_idf_stats (
  token TEXT PRIMARY KEY,              -- Token (word)
  doc_freq INTEGER NOT NULL DEFAULT 0, -- Number of documents containing token

  CONSTRAINT check_doc_freq_positive CHECK (doc_freq >= 0)
);

-- ============================================================================
-- Model Snapshots Table
-- ============================================================================
-- Stores serialized model versions for rollback and A/B testing
CREATE TABLE IF NOT EXISTS ml_model_snapshots (
  version INTEGER PRIMARY KEY AUTOINCREMENT,
  created_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  accuracy REAL,                       -- Test set accuracy (0.0 to 1.0)
  num_samples INTEGER NOT NULL,        -- Number of samples used for training
  num_classes INTEGER NOT NULL,        -- Number of unique classes
  serialized_model BLOB,               -- JSON or Gob serialized model
  metadata TEXT,                       -- JSON metadata (hyperparameters, etc.)

  CONSTRAINT check_accuracy_range CHECK (accuracy IS NULL OR (accuracy >= 0.0 AND accuracy <= 1.0)),
  CONSTRAINT check_num_samples_positive CHECK (num_samples >= 0),
  CONSTRAINT check_num_classes_positive CHECK (num_classes >= 0)
);

CREATE INDEX IF NOT EXISTS idx_ml_model_snapshots_created
ON ml_model_snapshots(created_date DESC);

-- ============================================================================
-- Training Metrics Table
-- ============================================================================
-- Stores training session metrics for monitoring and analysis
CREATE TABLE IF NOT EXISTS ml_training_metrics (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  model_version INTEGER,               -- FK to ml_model_snapshots.version
  training_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  train_accuracy REAL,                 -- Training set accuracy
  test_accuracy REAL,                  -- Test set accuracy
  train_samples INTEGER,               -- Number of training samples
  test_samples INTEGER,                -- Number of test samples
  training_time_ms INTEGER,            -- Training duration in milliseconds
  metadata TEXT,                       -- JSON metadata (per-class metrics, etc.)

  CONSTRAINT check_train_accuracy_range CHECK (train_accuracy IS NULL OR (train_accuracy >= 0.0 AND train_accuracy <= 1.0)),
  CONSTRAINT check_test_accuracy_range CHECK (test_accuracy IS NULL OR (test_accuracy >= 0.0 AND test_accuracy <= 1.0)),
  FOREIGN KEY (model_version) REFERENCES ml_model_snapshots(version) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_ml_training_metrics_date
ON ml_training_metrics(training_date DESC);

CREATE INDEX IF NOT EXISTS idx_ml_training_metrics_version
ON ml_training_metrics(model_version);

-- ============================================================================
-- Prediction Cache Table (Optional)
-- ============================================================================
-- Caches predictions for frequently seen filenames (performance optimization)
CREATE TABLE IF NOT EXISTS ml_prediction_cache (
  filename TEXT PRIMARY KEY,           -- Filename (hash key)
  predicted_class TEXT NOT NULL,       -- Predicted category
  confidence REAL NOT NULL,            -- Confidence score (0.0 to 1.0)
  model_version INTEGER,               -- Model version used for prediction
  created_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  last_accessed DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

  CONSTRAINT check_confidence_range CHECK (confidence >= 0.0 AND confidence <= 1.0)
);

CREATE INDEX IF NOT EXISTS idx_ml_prediction_cache_accessed
ON ml_prediction_cache(last_accessed DESC);

-- ============================================================================
-- Views for Analytics
-- ============================================================================

-- View: Class distribution (number of samples per class)
CREATE VIEW IF NOT EXISTS v_ml_class_distribution AS
SELECT
  class,
  doc_count,
  ROUND(100.0 * doc_count / SUM(doc_count) OVER(), 2) as percentage
FROM ml_class_stats
ORDER BY doc_count DESC;

-- View: Vocabulary statistics (total unique tokens per class)
CREATE VIEW IF NOT EXISTS v_ml_vocabulary_stats AS
SELECT
  class,
  COUNT(DISTINCT token) as unique_tokens,
  SUM(count) as total_token_occurrences
FROM ml_token_stats
GROUP BY class
ORDER BY total_token_occurrences DESC;

-- View: Top tokens per class (most frequent tokens)
CREATE VIEW IF NOT EXISTS v_ml_top_tokens AS
SELECT
  class,
  token,
  count,
  RANK() OVER (PARTITION BY class ORDER BY count DESC) as rank
FROM ml_token_stats
ORDER BY class, rank;

-- View: Model performance history
CREATE VIEW IF NOT EXISTS v_ml_model_performance AS
SELECT
  s.version,
  s.created_date,
  s.num_samples,
  s.num_classes,
  s.accuracy as snapshot_accuracy,
  m.test_accuracy as test_accuracy,
  m.training_time_ms
FROM ml_model_snapshots s
LEFT JOIN ml_training_metrics m ON s.version = m.model_version
ORDER BY s.created_date DESC;

-- ============================================================================
-- Triggers for Automatic Maintenance
-- ============================================================================

-- Trigger: Update ml_class_stats when sample is inserted
CREATE TRIGGER IF NOT EXISTS trg_ml_samples_insert_update_stats
AFTER INSERT ON ml_samples
BEGIN
  -- Update or insert class stats
  INSERT INTO ml_class_stats (class, doc_count, total_tokens)
  VALUES (NEW.class, 1, 0)
  ON CONFLICT(class) DO UPDATE SET
    doc_count = doc_count + 1;
END;

-- Trigger: Clean old prediction cache entries (keep last 1000)
CREATE TRIGGER IF NOT EXISTS trg_ml_prediction_cache_cleanup
AFTER INSERT ON ml_prediction_cache
BEGIN
  DELETE FROM ml_prediction_cache
  WHERE filename IN (
    SELECT filename
    FROM ml_prediction_cache
    ORDER BY last_accessed ASC
    LIMIT (
      SELECT CASE
        WHEN COUNT(*) > 1000 THEN COUNT(*) - 1000
        ELSE 0
      END
      FROM ml_prediction_cache
    )
  );
END;

-- ============================================================================
-- Initial Data (Optional)
-- ============================================================================
-- Uncomment to insert sample data for testing

-- INSERT INTO ml_samples (filename, series_name, class, tokens) VALUES
--   ('Breaking.Bad.S01E01.mkv', 'Breaking Bad', 'BREAKING BAD', '["breaking", "bad"]'),
--   ('The.Walking.Dead.S11E24.mkv', 'The Walking Dead', 'THE WALKING DEAD', '["the", "walking", "dead"]'),
--   ('Game.Of.Thrones.S08E06.mkv', 'Game Of Thrones', 'GAME OF THRONES', '["game", "of", "thrones"]');

-- ============================================================================
-- Schema Version Tracking
-- ============================================================================
CREATE TABLE IF NOT EXISTS ml_schema_version (
  version INTEGER PRIMARY KEY,
  applied_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  description TEXT
);

INSERT OR IGNORE INTO ml_schema_version (version, description)
VALUES (1, 'Initial ML schema: samples, statistics, model snapshots');

-- ============================================================================
-- Notes
-- ============================================================================
-- This schema supports:
-- 1. Incremental training: Add samples → update statistics → rebuild model
-- 2. Model versioning: Save/load model snapshots
-- 3. Analytics: Built-in views for monitoring training data
-- 4. Performance: Indexes on frequently queried columns
-- 5. Automatic cleanup: Triggers for cache management
--
-- Compatible with existing MediaButler database (no conflicts)
-- Can be applied to temp/mediabutler.dev.db
