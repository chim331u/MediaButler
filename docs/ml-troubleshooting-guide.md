# ML Troubleshooting Guide

This comprehensive guide helps diagnose and resolve machine learning-specific issues in MediaButler. All solutions are optimized for ARM32 deployment and Italian TV series classification workloads.

## Table of Contents

1. [Quick Diagnostics](#quick-diagnostics)
2. [Model Loading Issues](#model-loading-issues)
3. [Prediction Accuracy Problems](#prediction-accuracy-problems)
4. [Performance Issues](#performance-issues)
5. [Memory Problems](#memory-problems)
6. [Training and Evaluation Issues](#training-and-evaluation-issues)
7. [API Integration Problems](#api-integration-problems)
8. [ARM32-Specific Issues](#arm32-specific-issues)
9. [Configuration Issues](#configuration-issues)
10. [Logging and Diagnostics](#logging-and-diagnostics)

## Quick Diagnostics

### Health Check Commands

```bash
# Check ML service health
curl http://localhost:5000/api/health/ml

# Verify model loading
curl http://localhost:5000/api/ml/status

# Test basic prediction
curl -X POST http://localhost:5000/api/predict \
  -H "Content-Type: application/json" \
  -d '{"filename": "Breaking.Bad.S01E01.mkv"}'

# Check system resources
free -h && df -h && top -bn1 | head -20
```

### Common Symptoms and Quick Fixes

```yaml
Symptom: "Model failed to load"
Quick Check: ls -la models/ && du -sh models/*
Quick Fix: Verify model file exists and is <25MB

Symptom: "Out of memory during prediction"
Quick Check: free -h
Quick Fix: Restart service, check for memory leaks

Symptom: "Predictions taking >5 seconds"
Quick Check: top -p $(pgrep dotnet)
Quick Fix: Check CPU throttling, reduce batch size

Symptom: "All predictions return 'UNKNOWN'"
Quick Check: Check model training status
Quick Fix: Retrain model with more data
```

## Model Loading Issues

### Model File Not Found

```yaml
Error: "Could not load FastText model from path: /models/fasttext_v2.bin"

Diagnosis Steps:
  1. Check file existence: ls -la /path/to/models/
  2. Verify file permissions: ls -l /path/to/models/fasttext_v2.bin
  3. Check disk space: df -h
  4. Validate file integrity: file /path/to/models/fasttext_v2.bin

Common Causes:
  - Model file missing or corrupted
  - Incorrect file path in configuration
  - Permission issues
  - Insufficient disk space

Solutions:
  # Download/restore model file
  wget https://releases.mediabutler.io/models/fasttext_v2.bin -O models/fasttext_v2.bin
  
  # Fix permissions
  chmod 644 models/fasttext_v2.bin
  chown mediabutler:mediabutler models/fasttext_v2.bin
  
  # Update configuration
  "ML": {
    "ModelPath": "/absolute/path/to/models/fasttext_v2.bin"
  }
```

### Model Corruption Issues

```yaml
Error: "Invalid FastText model format"

Diagnosis:
  # Check file size (should be ~20MB)
  ls -lh models/fasttext_v2.bin
  
  # Verify file type
  file models/fasttext_v2.bin
  
  # Check MD5 hash
  md5sum models/fasttext_v2.bin

Expected Output:
  - Size: 19-22MB
  - Type: "data" or binary file
  - Hash: [Compare with known good hash]

Solutions:
  # Re-download model
  rm models/fasttext_v2.bin
  wget https://releases.mediabutler.io/models/fasttext_v2.bin -O models/fasttext_v2.bin
  
  # Verify download
  md5sum models/fasttext_v2.bin
```

### Model Version Incompatibility

```yaml
Error: "Model version mismatch: expected v2.x, found v1.x"

Diagnosis:
  # Check model metadata
  strings models/fasttext_v2.bin | head -20
  
  # Check application version
  dotnet MediaButler.API.dll --version

Solutions:
  # Upgrade model to latest version
  wget https://releases.mediabutler.io/models/latest/fasttext.bin -O models/fasttext_v2.bin
  
  # Or downgrade application if needed
  git checkout tags/v2.1.0
  dotnet build
```

### Memory Issues During Model Loading

```yaml
Error: "OutOfMemoryException during model initialization"

Diagnosis:
  # Check available memory
  free -h
  
  # Check model size
  ls -lh models/fasttext_v2.bin
  
  # Monitor memory during loading
  while true; do free -h; sleep 1; done &
  # Start application in another terminal

Solutions:
  # Increase available memory
  sudo systemctl stop unnecessary-services
  
  # Use smaller model
  wget https://releases.mediabutler.io/models/compact/fasttext_compact.bin
  
  # Enable swap (temporary, not recommended for production)
  sudo fallocate -l 1G /swapfile
  sudo chmod 600 /swapfile
  sudo mkswap /swapfile
  sudo swapon /swapfile
```

## Prediction Accuracy Problems

### All Predictions Return "UNKNOWN"

```yaml
Symptoms:
  - Every filename classified as "UNKNOWN"
  - Confidence scores very low (<0.3)
  - No errors in logs

Diagnosis:
  # Test with known patterns
  curl -X POST http://localhost:5000/api/predict \
    -H "Content-Type: application/json" \
    -d '{"filename": "Breaking.Bad.S01E01.1080p.mkv"}'
  
  # Check training data
  curl http://localhost:5000/api/ml/training-stats
  
  # Verify tokenization
  curl -X POST http://localhost:5000/api/ml/tokenize \
    -H "Content-Type: application/json" \
    -d '{"filename": "Breaking.Bad.S01E01.mkv"}'

Common Causes:
  - Model not properly trained
  - Insufficient training data
  - Tokenization issues
  - Wrong confidence thresholds

Solutions:
  # Retrain model with more data
  curl -X POST http://localhost:5000/api/ml/train
  
  # Lower confidence threshold temporarily
  "ML": {
    "ConfidenceThreshold": 0.3  // Lower from 0.5
  }
  
  # Add training data manually
  curl -X POST http://localhost:5000/api/ml/training-data \
    -H "Content-Type: application/json" \
    -d '{
      "filename": "Breaking.Bad.S01E01.mkv",
      "category": "BREAKING BAD",
      "source": "Manual"
    }'
```

### Inconsistent Predictions

```yaml
Symptoms:
  - Same filename returns different results
  - Confidence scores vary significantly
  - Random performance drops

Diagnosis:
  # Test prediction consistency
  for i in {1..10}; do
    curl -X POST http://localhost:5000/api/predict \
      -H "Content-Type: application/json" \
      -d '{"filename": "Breaking.Bad.S01E01.mkv"}'
    echo ""
  done
  
  # Check for concurrent processing issues
  curl http://localhost:5000/api/ml/diagnostics

Common Causes:
  - Thread safety issues
  - Memory pressure affecting model
  - Caching problems
  - Non-deterministic algorithms

Solutions:
  # Restart service to clear state
  sudo systemctl restart mediabutler
  
  # Disable caching temporarily
  "ML": {
    "EnablePredictionCache": false
  }
  
  # Reduce concurrency
  "ML": {
    "MaxConcurrentPredictions": 1
  }
```

### Poor Accuracy for Italian Content

```yaml
Symptoms:
  - English series classified correctly
  - Italian series often misclassified
  - Special characters causing issues

Diagnosis:
  # Test with Italian filenames
  curl -X POST http://localhost:5000/api/predict \
    -H "Content-Type: application/json" \
    -d '{"filename": "La.Casa.di.Carta.S02E08.ITA.mkv"}'
  
  # Check tokenization of Italian text
  curl -X POST http://localhost:5000/api/ml/tokenize \
    -H "Content-Type: application/json" \
    -d '{"filename": "La.Casa.di.Carta.S02E08.ITA.mkv"}'

Solutions:
  # Add more Italian training data
  curl -X POST http://localhost:5000/api/ml/training-data \
    -H "Content-Type: application/json" \
    -d '{
      "filename": "La.Casa.di.Carta.S02E08.ITA.mkv",
      "category": "LA CASA DI CARTA",
      "source": "Manual"
    }'
  
  # Enable Italian-specific tokenization
  "ML": {
    "LanguageSupport": ["en", "it"],
    "NormalizeAccents": true
  }
  
  # Retrain with language-specific features
  curl -X POST http://localhost:5000/api/ml/train \
    -H "Content-Type: application/json" \
    -d '{"includeLanguageFeatures": true}'
```

### Confidence Scores Too Low/High

```yaml
Symptoms:
  - All predictions have very low confidence (<0.4)
  - All predictions have very high confidence (>0.95)
  - Confidence doesn't match actual accuracy

Diagnosis:
  # Analyze confidence distribution
  curl http://localhost:5000/api/ml/confidence-analysis
  
  # Test calibration
  curl -X POST http://localhost:5000/api/ml/evaluate \
    -H "Content-Type: application/json" \
    -d '{"analyzeConfidence": true}'

Solutions:
  # Calibrate confidence scores
  curl -X POST http://localhost:5000/api/ml/calibrate-confidence
  
  # Adjust confidence thresholds
  "ML": {
    "ConfidenceThreshold": 0.6,
    "HighConfidenceThreshold": 0.85
  }
  
  # Retrain with confidence calibration
  curl -X POST http://localhost:5000/api/ml/train \
    -H "Content-Type: application/json" \
    -d '{"enableConfidenceCalibration": true}'
```

## Performance Issues

### Slow Prediction Response Times

```yaml
Symptoms:
  - Predictions taking >2 seconds
  - API timeouts
  - High CPU usage during predictions

Diagnosis:
  # Measure prediction time
  time curl -X POST http://localhost:5000/api/predict \
    -H "Content-Type: application/json" \
    -d '{"filename": "Breaking.Bad.S01E01.mkv"}'
  
  # Check CPU usage
  top -p $(pgrep dotnet)
  
  # Profile performance
  dotnet-trace collect --process-id $(pgrep dotnet) --duration 00:00:30

Common Causes:
  - Model too large for ARM32
  - Inefficient tokenization
  - Memory pressure causing GC
  - No model caching

Solutions:
  # Use compact model
  wget https://releases.mediabutler.io/models/compact/fasttext_compact.bin
  
  # Enable model caching
  "ML": {
    "EnableModelCaching": true,
    "ModelCacheSize": 100
  }
  
  # Optimize tokenization
  "ML": {
    "TokenizerOptimization": "Fast",
    "CacheTokens": true
  }
  
  # Reduce model precision
  "ML": {
    "UseLowPrecisionMode": true
  }
```

### High Memory Usage

```yaml
Symptoms:
  - Memory usage >300MB
  - OutOfMemoryException
  - System becomes unresponsive

Diagnosis:
  # Monitor memory usage
  watch -n 1 'free -h && ps aux | grep dotnet'
  
  # Check for memory leaks
  dotnet-dump collect --process-id $(pgrep dotnet)
  
  # Analyze GC behavior
  dotnet-counters monitor --process-id $(pgrep dotnet) \
    System.Runtime[gen-0-gc-count,gen-1-gc-count,gen-2-gc-count]

Solutions:
  # Enable aggressive garbage collection
  export DOTNET_gcServer=1
  export DOTNET_GCRetainVM=0
  
  # Implement object pooling
  "ML": {
    "EnableObjectPooling": true,
    "PoolSize": 50
  }
  
  # Reduce cache sizes
  "ML": {
    "PredictionCacheSize": 50,
    "TokenCacheSize": 100
  }
  
  # Use streaming predictions
  "ML": {
    "UseStreamingPredictions": true
  }
```

### Queue Buildup

```yaml
Symptoms:
  - Prediction queue growing continuously
  - Increasing response times
  - Memory usage climbing

Diagnosis:
  # Check queue status
  curl http://localhost:5000/api/ml/queue-status
  
  # Monitor queue depth
  watch -n 5 'curl -s http://localhost:5000/api/ml/queue-status | jq .queueDepth'

Solutions:
  # Increase worker threads
  "ML": {
    "MaxConcurrentPredictions": 3,
    "QueueCapacity": 100
  }
  
  # Implement batch processing
  "ML": {
    "EnableBatchProcessing": true,
    "BatchSize": 10,
    "BatchTimeout": 1000
  }
  
  # Add circuit breaker
  "ML": {
    "CircuitBreakerEnabled": true,
    "FailureThreshold": 5,
    "ResetTimeout": 30000
  }
```

## Memory Problems

### Memory Leaks

```yaml
Symptoms:
  - Memory usage increases over time
  - Eventually leads to OutOfMemoryException
  - Performance degrades gradually

Diagnosis:
  # Monitor memory growth
  while true; do
    echo "$(date): $(free -h | grep Mem)"
    sleep 300  # Check every 5 minutes
  done
  
  # Take memory dumps
  dotnet-dump collect --process-id $(pgrep dotnet)
  
  # Analyze with diagnostic tools
  dotnet-dump analyze core_dump

Common Causes:
  - Event handler leaks
  - Cached objects not being released
  - Circular references
  - Large object heap growth

Solutions:
  # Force garbage collection
  curl -X POST http://localhost:5000/api/ml/gc-collect
  
  # Clear caches
  curl -X POST http://localhost:5000/api/ml/clear-cache
  
  # Restart service periodically
  # Add to crontab:
  0 3 * * * systemctl restart mediabutler
  
  # Enable memory monitoring
  "Monitoring": {
    "MemoryThreshold": 250,
    "AutoRestartOnMemoryLimit": true
  }
```

### Out of Memory During Training

```yaml
Symptoms:
  - OutOfMemoryException during model training
  - Training process fails partway through
  - System becomes unresponsive

Diagnosis:
  # Check available memory
  free -h
  
  # Check training data size
  curl http://localhost:5000/api/ml/training-stats
  
  # Monitor during training
  watch -n 1 'free -h' &
  curl -X POST http://localhost:5000/api/ml/train

Solutions:
  # Train with smaller batches
  "ML": {
    "TrainingBatchSize": 100,
    "MaxTrainingMemory": 150
  }
  
  # Use incremental training
  "ML": {
    "EnableIncrementalTraining": true,
    "TrainingChunkSize": 1000
  }
  
  # Offload training to external system
  # Copy training data to more powerful machine
  scp training_data.json powerful-machine:/tmp/
  # Train there and copy model back
```

### Garbage Collection Issues

```yaml
Symptoms:
  - Frequent long GC pauses
  - Application appears frozen periodically
  - High CPU usage during GC

Diagnosis:
  # Monitor GC events
  dotnet-counters monitor --process-id $(pgrep dotnet) \
    System.Runtime[gc-heap-size,gen-0-gc-count,gen-1-gc-count,gen-2-gc-count,time-in-gc]
  
  # Enable GC logging
  export DOTNET_EnableEventLog=1

Solutions:
  # Optimize GC configuration
  "Runtime": {
    "GCMode": "Workstation",
    "GCConcurrent": true,
    "GCRetainVM": false
  }
  
  # Reduce allocation pressure
  "ML": {
    "EnableObjectPooling": true,
    "ReuseStringBuilders": true,
    "MinimizeAllocations": true
  }
  
  # Tune GC thresholds
  export DOTNET_GCHeapCount=2
  export DOTNET_GCGen0MaxBudget=100000
```

## Training and Evaluation Issues

### Training Fails to Start

```yaml
Error: "Training service unavailable"

Diagnosis:
  # Check training service status
  curl http://localhost:5000/api/ml/training-status
  
  # Verify training data
  curl http://localhost:5000/api/ml/training-data | jq '.count'
  
  # Check locks/dependencies
  ps aux | grep fasttext

Solutions:
  # Restart training service
  curl -X POST http://localhost:5000/api/ml/restart-training-service
  
  # Clear training locks
  rm -f /tmp/mediabutler_training.lock
  
  # Verify training data format
  curl http://localhost:5000/api/ml/validate-training-data
```

### Training Never Completes

```yaml
Symptoms:
  - Training starts but never finishes
  - High CPU usage continues indefinitely
  - No progress updates

Diagnosis:
  # Check training progress
  curl http://localhost:5000/api/ml/training-progress
  
  # Monitor training process
  ps aux | grep fasttext
  strace -p $(pgrep fasttext)

Solutions:
  # Set training timeout
  "ML": {
    "TrainingTimeout": 1800000,  // 30 minutes
    "EnableTrainingProgress": true
  }
  
  # Kill and restart training
  pkill -f fasttext
  curl -X POST http://localhost:5000/api/ml/train
  
  # Use smaller training dataset
  curl -X POST http://localhost:5000/api/ml/train \
    -H "Content-Type: application/json" \
    -d '{"maxSamples": 1000}'
```

### Evaluation Failures

```yaml
Error: "Model evaluation failed with unknown error"

Diagnosis:
  # Check evaluation configuration
  curl http://localhost:5000/api/ml/evaluation-config
  
  # Test with minimal dataset
  curl -X POST http://localhost:5000/api/ml/evaluate \
    -H "Content-Type: application/json" \
    -d '{"testSampleCount": 10}'

Solutions:
  # Reset evaluation configuration
  curl -X POST http://localhost:5000/api/ml/reset-evaluation-config
  
  # Use default evaluation parameters
  curl -X POST http://localhost:5000/api/ml/evaluate \
    -H "Content-Type: application/json" \
    -d '{}'
  
  # Check evaluation dependencies
  curl http://localhost:5000/api/ml/evaluation-dependencies
```

## API Integration Problems

### Prediction API Timeouts

```yaml
Symptoms:
  - HTTP 408 Request Timeout
  - Client connections timing out
  - Inconsistent response times

Diagnosis:
  # Test with curl timeout
  curl --max-time 10 -X POST http://localhost:5000/api/predict \
    -H "Content-Type: application/json" \
    -d '{"filename": "test.mkv"}'
  
  # Check API performance
  curl http://localhost:5000/api/metrics

Solutions:
  # Increase timeout values
  "API": {
    "RequestTimeout": 30000,
    "KeepAliveTimeout": 60000
  }
  
  # Implement async processing
  "ML": {
    "EnableAsyncPredictions": true,
    "AsyncTimeoutMs": 5000
  }
  
  # Add request queuing
  "API": {
    "MaxConcurrentRequests": 10,
    "QueueTimeout": 15000
  }
```

### Invalid Prediction Responses

```yaml
Symptoms:
  - Malformed JSON responses
  - Missing fields in response
  - Unexpected data types

Diagnosis:
  # Test response format
  curl -X POST http://localhost:5000/api/predict \
    -H "Content-Type: application/json" \
    -d '{"filename": "test.mkv"}' | jq .
  
  # Validate against schema
  curl http://localhost:5000/api/schema/prediction

Solutions:
  # Reset API configuration
  curl -X POST http://localhost:5000/api/reset-config
  
  # Validate response serialization
  "API": {
    "ValidateResponses": true,
    "StrictJsonSerialization": true
  }
  
  # Update API contracts
  dotnet build src/MediaButler.API.Contracts/
```

### Authentication/Authorization Issues

```yaml
Error: "Unauthorized access to ML endpoints"

Note: MediaButler is designed for single-user, no-auth deployment,
but some reverse proxy setups may add authentication.

Solutions:
  # Check reverse proxy configuration
  curl -H "Authorization: Bearer token" http://localhost:5000/api/predict
  
  # Bypass proxy for testing
  curl http://localhost:5001/api/predict  # Direct to app
  
  # Configure proxy correctly
  # Nginx example:
  location /api/ {
    proxy_pass http://localhost:5000/api/;
    proxy_set_header Host $host;
  }
```

## ARM32-Specific Issues

### .NET Runtime Issues

```yaml
Symptoms:
  - Application fails to start
  - "Platform not supported" errors
  - Native library loading failures

Diagnosis:
  # Check .NET runtime
  dotnet --info
  
  # Verify ARM32 support
  dotnet --list-runtimes
  
  # Check native dependencies
  ldd MediaButler.API.dll

Solutions:
  # Install correct .NET runtime
  wget https://dot.net/v1/dotnet-install.sh
  chmod +x dotnet-install.sh
  ./dotnet-install.sh --architecture arm --runtime aspnetcore
  
  # Set runtime identifier
  dotnet publish -r linux-arm --self-contained
  
  # Install missing native libraries
  sudo apt-get install libc6-dev libgdiplus
```

### ARM32 Performance Issues

```yaml
Symptoms:
  - Much slower than expected
  - High CPU usage for simple operations
  - Thermal throttling

Diagnosis:
  # Check CPU frequency
  cat /sys/devices/system/cpu/cpu*/cpuinfo_cur_freq
  
  # Monitor temperature
  vcgencmd measure_temp
  
  # Check for throttling
  vcgencmd get_throttled

Solutions:
  # Optimize for ARM32
  "Runtime": {
    "OptimizeForARM32": true,
    "UseHardwareIntrinsics": false
  }
  
  # Reduce workload
  "ML": {
    "ARM32OptimizedMode": true,
    "ReducedPrecisionMode": true
  }
  
  # Add cooling
  # Install heat sinks or fan
  
  # Reduce clock speed if overheating
  echo "arm_freq=1200" >> /boot/config.txt
```

### FastText ARM32 Compatibility

```yaml
Error: "Failed to load FastText native library"

Diagnosis:
  # Check native library
  ls -la /usr/lib/arm-linux-gnueabihf/
  
  # Test manual loading
  python3 -c "import fasttext; print('OK')"

Solutions:
  # Compile FastText for ARM32
  git clone https://github.com/facebookresearch/fastText.git
  cd fastText
  make -j4
  sudo cp fasttext /usr/local/bin/
  
  # Use managed implementation
  "ML": {
    "UseManagedFastText": true,
    "NativeLibraryPath": null
  }
  
  # Precompiled ARM32 binaries
  wget https://releases.mediabutler.io/arm32/fasttext-arm32.tar.gz
  tar -xzf fasttext-arm32.tar.gz -C /usr/local/bin/
```

## Configuration Issues

### Invalid Configuration Values

```yaml
Error: "Configuration validation failed"

Diagnosis:
  # Validate configuration
  curl http://localhost:5000/api/config/validate
  
  # Check configuration file
  cat appsettings.json | jq .
  
  # Verify environment variables
  env | grep MEDIABUTLER

Solutions:
  # Reset to defaults
  cp appsettings.default.json appsettings.json
  
  # Validate specific sections
  "ML": {
    "ModelPath": "/absolute/path/to/model.bin",
    "ConfidenceThreshold": 0.5,  // 0.0-1.0
    "MaxConcurrentPredictions": 2  // 1-10
  }
  
  # Use environment variables
  export MediaButler__ML__ModelPath="/path/to/model.bin"
```

### Model Path Issues

```yaml
Error: "Model path not found or inaccessible"

Diagnosis:
  # Check current configuration
  curl http://localhost:5000/api/config | jq .ML.ModelPath
  
  # Test file access
  sudo -u mediabutler ls -la $(curl -s http://localhost:5000/api/config | jq -r .ML.ModelPath)

Solutions:
  # Use absolute paths
  "ML": {
    "ModelPath": "/home/mediabutler/models/fasttext_v2.bin"
  }
  
  # Fix permissions
  sudo chown -R mediabutler:mediabutler /home/mediabutler/models/
  sudo chmod -R 644 /home/mediabutler/models/*
  
  # Create symbolic link
  ln -s /actual/model/path/fasttext_v2.bin /expected/path/fasttext_v2.bin
```

### Environment-Specific Configuration

```yaml
Issues:
  - Configuration works in development but not production
  - Environment variables not being read
  - Wrong configuration file being used

Solutions:
  # Check environment
  echo $ASPNETCORE_ENVIRONMENT
  
  # Verify configuration precedence
  # 1. Environment variables
  # 2. appsettings.{Environment}.json
  # 3. appsettings.json
  
  # Set production configuration
  export ASPNETCORE_ENVIRONMENT=Production
  cp appsettings.Production.json appsettings.json
  
  # Debug configuration loading
  "Logging": {
    "LogLevel": {
      "Microsoft.Extensions.Configuration": "Debug"
    }
  }
```

## Logging and Diagnostics

### Enable Detailed ML Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "MediaButler.ML": "Debug",
      "MediaButler.ML.Services": "Trace"
    }
  }
}
```

### Diagnostic Endpoints

```bash
# Health check with detailed ML status
curl http://localhost:5000/health/ml

# ML service diagnostics
curl http://localhost:5000/api/ml/diagnostics

# Performance metrics
curl http://localhost:5000/api/ml/metrics

# Configuration dump
curl http://localhost:5000/api/config/ml

# Training statistics
curl http://localhost:5000/api/ml/training-stats

# Model information
curl http://localhost:5000/api/ml/model-info
```

### Log Analysis Commands

```bash
# View ML-specific logs
journalctl -u mediabutler -f | grep -E "(ML|Prediction|Training)"

# Search for errors
journalctl -u mediabutler --since "1 hour ago" | grep -i error

# Analyze performance logs
tail -f /var/log/mediabutler/performance.log | grep "Prediction"

# Monitor memory usage in logs
tail -f /var/log/mediabutler/app.log | grep -E "(Memory|GC|OutOfMemory)"
```

### Structured Logging Queries

```bash
# Filter by log level
cat logs/app.log | jq 'select(.Level == "Error")'

# Filter ML operations
cat logs/app.log | jq 'select(.SourceContext | contains("ML"))'

# Performance metrics
cat logs/app.log | jq 'select(.EventId.Name == "PredictionCompleted") | .Properties'

# Error analysis
cat logs/app.log | jq 'select(.Level == "Error") | {Time: .Timestamp, Message: .MessageTemplate, Exception: .Exception}'
```

### Performance Monitoring

```bash
# Real-time performance monitoring
watch -n 1 'curl -s http://localhost:5000/api/ml/metrics | jq "{predictions_per_sec: .throughput, avg_latency_ms: .averageLatency, memory_mb: .memoryUsage}"'

# Memory trend analysis
while true; do
  echo "$(date),$(free -m | grep Mem | awk '{print $3}')" >> memory_usage.csv
  sleep 60
done

# CPU usage for ML operations
top -p $(pgrep dotnet) -b -n 1 | grep dotnet | awk '{print $9}' >> cpu_usage.log
```

### Emergency Diagnostics

```bash
# Emergency system information collection
echo "=== System Info ===" > debug_info.txt
uname -a >> debug_info.txt
free -h >> debug_info.txt
df -h >> debug_info.txt
cat /proc/cpuinfo >> debug_info.txt

echo "=== MediaButler Status ===" >> debug_info.txt
systemctl status mediabutler >> debug_info.txt
curl -s http://localhost:5000/health >> debug_info.txt

echo "=== ML Diagnostics ===" >> debug_info.txt
curl -s http://localhost:5000/api/ml/diagnostics >> debug_info.txt

echo "=== Recent Logs ===" >> debug_info.txt
journalctl -u mediabutler --since "1 hour ago" >> debug_info.txt

# Package for support
tar -czf mediabutler_debug_$(date +%Y%m%d_%H%M%S).tar.gz debug_info.txt logs/
```

---

**Note**: This troubleshooting guide covers the most common issues encountered in ARM32 deployments. For complex issues not covered here, enable detailed logging, collect diagnostic information using the emergency diagnostics script, and consult the [MediaButler GitHub Issues](https://github.com/mediabutler/mediabutler/issues) page for additional support.