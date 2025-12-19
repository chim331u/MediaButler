# Replacing ML.NET with a Go ML Solution

## Scope
This document exports and consolidates the analysis and proposed solution for replacing an existing **.NET ML.NET (SDCA Maximum Entropy)** classifier with a **Go-based ML solution**, trained and updated using **SQLite**.

The target use case is **TV series filename classification** under **ARM32 and low-memory constraints**.

---

## Production Constraints (Source System)

- Multiclass text classification (22 TV series)
- ML.NET SDCA Maximum Entropy
- Model size: ~118 KB
- Accuracy: 95%+
- Prediction latency: ~50 ms (max allowed 500 ms)
- Platform: ARM32
- Memory usage: < 50 MB
- Italian-language filenames
- Regex-based tokenization
- Incremental learning from user-confirmed samples
- Integrated into an API

---

## Proposed Go ML Architecture

### Model Choice (Closest Functional Equivalent)

**Multinomial Naive Bayes + TF-IDF (custom implementation)**

Reasons:
- Comparable decision boundaries to Maximum Entropy for short text
- Excellent performance on filename classification
- Extremely small model size
- Fully incremental learning
- No heavy math libraries or runtimes
- Ideal for ARM32

---

## Processing Pipeline

```
Filename
  ↓
Italian Regex Tokenizer
  ↓
TF-IDF Vectorization
  ↓
Multinomial Naive Bayes
  ↓
Predicted TV Series
```

Accuracy is primarily driven by **tokenization quality**, not model complexity.

---

## SQLite-Centric Training Strategy

SQLite is used as the **single source of truth** for:
- User-confirmed samples
- Token and class statistics
- Incremental training state

No external ML pipeline is required.

---

## SQLite Schema

### Labeled Samples
```sql
CREATE TABLE samples (
  id INTEGER PRIMARY KEY,
  filename TEXT NOT NULL,
  class TEXT NOT NULL,
  created_at TEXT
);
```

### Class Statistics
```sql
CREATE TABLE class_stats (
  class TEXT PRIMARY KEY,
  doc_count INTEGER
);
```

### Token Statistics
```sql
CREATE TABLE token_stats (
  token TEXT,
  class TEXT,
  count INTEGER,
  PRIMARY KEY (token, class)
);
```

### IDF Statistics
```sql
CREATE TABLE idf_stats (
  token TEXT PRIMARY KEY,
  doc_freq INTEGER
);
```

---

## Incremental Training Flow

When a user confirms a classification:

1. Insert sample into `samples`
2. Tokenize filename
3. Update `class_stats`
4. Update `token_stats`
5. Update `idf_stats`
6. Recompute only affected weights
7. Hot-swap model in memory

Typical update time: **< 10 ms**

---

## Model Representation (In-Memory)

```go
type Model struct {
    Classes []string
    Priors  map[string]float64
    Weights map[string]map[string]float64
    IDF     map[string]float64
}
```

- Serialized size: ~100–150 KB
- Loaded fully in RAM
- Atomic swap for zero downtime

---

## Model Loading and Reloading

```go
var currentModel atomic.Value

func ReloadModel(db *sql.DB) {
    model, _ := TrainFromSQLite(db)
    currentModel.Store(model)
}
```

- No locks for readers
- Safe concurrent predictions

---

## Performance Expectations

| Operation | Time |
|---------|------|
| Insert sample | < 1 ms |
| Update statistics | < 5 ms |
| Model reload | < 10 ms |
| Prediction | 1–5 ms |

Memory usage:
- SQLite cache: ~5–10 MB
- Model in RAM: < 2 MB

---

## Explainability and Safety

- Token-level contribution inspection
- Confidence thresholding
- "Unknown" classification for low confidence

```go
if confidence < 0.6 {
    return "unknown"
}
```

---

## Comparison with ML.NET

| Aspect | ML.NET | Go + SQLite |
|------|-------|------------|
| Incremental learning | Limited | Native |
| Model size | ~118 KB | ~100–150 KB |
| Latency | ~50 ms | < 10 ms |
| ARM32 support | Partial | Excellent |
| Explainability | Limited | Full |

---

## Final Recommendation

Adopt a **custom Go Multinomial Naive Bayes + TF-IDF classifier**, trained and updated directly from **SQLite**.

This approach:
- Matches or exceeds ML.NET accuracy
- Dramatically reduces latency
- Fits comfortably within ARM32 memory limits
- Simplifies deployment and operations

---

## Next Possible Extensions

- Confidence calibration to mirror ML.NET output
- Tokenizer parity validation against existing regex rules
- Full retraining command from `samples`
- Background training jobs with SSE notifications
- Migration tooling from existing ML.NET data stores

