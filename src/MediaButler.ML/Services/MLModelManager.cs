using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;

namespace MediaButler.ML.Services;

/// <summary>
/// Manages the lifecycle of the ML model for prediction.
/// Implements the "Micro-Kernel" pattern with Hot Reload support.
/// </summary>
public class MLModelManager : IMLModelManager
{
    private readonly ILogger<MLModelManager> _logger;
    private readonly MLContext _mlContext;
    private readonly string _modelPath;
    
    // Thread-safety lock for model reloading
    private readonly object _lock = new object();
    
    // Lazy-loaded PredictionEngine to support hot reload
    private Lazy<PredictionEngine<FileCategoryInput, FileCategoryPrediction>>? _predictionEngine;
    private ITransformer? _currentModel;

    public MLModelManager(ILogger<MLModelManager> logger, string modelPath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _modelPath = modelPath ?? throw new ArgumentNullException(nameof(modelPath));
        _mlContext = new MLContext(seed: 42); // Seed for consistency
        
        // Initialize the lazy loader
        InitializeLazyEngine();
    }

    public bool IsModelLoaded => _predictionEngine != null && File.Exists(_modelPath);

    /// <summary>
    /// Thread-safe prediction using the current model.
    /// </summary>
    public FileCategoryPrediction Predict(FileCategoryInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        // Use lock to ensure we don't predict while reloading
        // Note: PredictionEngine is NOT thread-safe, so we must lock or use a pool.
        // For simplicity in this micro-kernel, we lock execution. 
        // For high-throughput, ObjectPool<PredictionEngine> would be better.
        lock (_lock)
        {
            try
            {
                if (!IsModelLoaded)
                {
                    _logger.LogWarning("Attempted prediction but model is not loaded/found at {Path}", _modelPath);
                    return new FileCategoryPrediction { Category = "UNKNOWN", Score = Array.Empty<float>() };
                }

                return _predictionEngine!.Value.Predict(input);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during prediction for file {Filename}", input.Filename);
                throw;
            }
        }
    }

    /// <summary>
    /// Triggers a hot reload of the model.
    /// Invalidates the current PredictionEngine so it will be recreated on next use.
    /// </summary>
    public void ReloadModel()
    {
        lock (_lock)
        {
            _logger.LogInformation("Hot Reload triggered. Invalidating current model.");
            
            // Dispose if disposable (PredictionEngine is disposable)
            if (_predictionEngine != null && _predictionEngine.IsValueCreated)
            {
                _predictionEngine.Value.Dispose();
            }

            // Reset the lazy loader
            InitializeLazyEngine();
            
            _logger.LogInformation("Model invalidated. Will reload from disk on next prediction.");
        }
    }

    private void InitializeLazyEngine()
    {
        _predictionEngine = new Lazy<PredictionEngine<FileCategoryInput, FileCategoryPrediction>>(() =>
        {
            try
            {
                _logger.LogInformation("Loading ML model from disk: {Path}", _modelPath);
                
                if (!File.Exists(_modelPath))
                {
                    _logger.LogWarning("Model file not found at {Path}. Prediction will fail.", _modelPath);
                    throw new FileNotFoundException("Model file not found", _modelPath);
                }

                // Load the model
                DataViewSchema modelSchema;
                _currentModel = _mlContext.Model.Load(_modelPath, out modelSchema);

                _logger.LogInformation("Model loaded successfully. Schema: {ColumnCount} columns.", modelSchema.Count);

                // Create the engine
                return _mlContext.Model.CreatePredictionEngine<FileCategoryInput, FileCategoryPrediction>(_currentModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize PredictionEngine.");
                throw;
            }
        });
    }
}
