using FluentAssertions;
using MediaButler.Core.Common;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using MediaButler.ML.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace MediaButler.Tests.Unit.ML;

/// <summary>
/// Unit tests for ModelEvaluationService covering all evaluation capabilities.
/// Tests accuracy evaluation, confusion matrix generation, performance benchmarking,
/// cross-validation, confidence analysis, and model comparison functionality.
/// Follows "Simple Made Easy" principles with isolated, focused test scenarios.
/// </summary>
public class ModelEvaluationServiceTests
{
    private readonly Mock<IPredictionService> _mockPredictionService;
    private readonly Mock<ITokenizerService> _mockTokenizerService;
    private readonly Mock<ILogger<ModelEvaluationService>> _mockLogger;
    private readonly ModelEvaluationService _service;

    public ModelEvaluationServiceTests()
    {
        _mockPredictionService = new Mock<IPredictionService>();
        _mockTokenizerService = new Mock<ITokenizerService>();
        _mockLogger = new Mock<ILogger<ModelEvaluationService>>();
        _service = new ModelEvaluationService(_mockLogger.Object, _mockPredictionService.Object, _mockTokenizerService.Object);
    }

    #region Accuracy Evaluation Tests

    [Fact]
    public async Task EvaluateAccuracyAsync_WithValidTestData_ShouldReturnComprehensiveMetrics()
    {
        // Given - Test dataset with known correct/incorrect predictions
        var testCases = new List<EvaluationTestCase>
        {
            // Breaking Bad: 3/4 correct (75% accuracy)
            new() { Filename = "Breaking.Bad.S01E01.mkv", ExpectedCategory = "BREAKING BAD", PredictedCategory = "BREAKING BAD", PredictionConfidence = 0.95 },
            new() { Filename = "Breaking.Bad.S01E02.mkv", ExpectedCategory = "BREAKING BAD", PredictedCategory = "BREAKING BAD", PredictionConfidence = 0.88 },
            new() { Filename = "Breaking.Bad.S01E03.mkv", ExpectedCategory = "BREAKING BAD", PredictedCategory = "BREAKING BAD", PredictionConfidence = 0.92 },
            new() { Filename = "Breaking.Bad.S01E04.mkv", ExpectedCategory = "BREAKING BAD", PredictedCategory = "THE OFFICE", PredictionConfidence = 0.65 }, // Incorrect
            
            // The Office: 2/2 correct (100% accuracy)
            new() { Filename = "The.Office.S01E01.mkv", ExpectedCategory = "THE OFFICE", PredictedCategory = "THE OFFICE", PredictionConfidence = 0.91 },
            new() { Filename = "The.Office.S01E02.mkv", ExpectedCategory = "THE OFFICE", PredictedCategory = "THE OFFICE", PredictionConfidence = 0.89 },
            
            // Naruto: 1/2 correct (50% accuracy) 
            new() { Filename = "Naruto.S01E01.mkv", ExpectedCategory = "NARUTO", PredictedCategory = "NARUTO", PredictionConfidence = 0.78 },
            new() { Filename = "Naruto.S01E02.mkv", ExpectedCategory = "NARUTO", PredictedCategory = "ONE PIECE", PredictionConfidence = 0.72 } // Incorrect
        }.AsReadOnly();

        // When - Evaluate accuracy
        var result = await _service.EvaluateAccuracyAsync(testCases);

        // Then - Verify comprehensive accuracy metrics
        result.IsSuccess.Should().BeTrue();
        var metrics = result.Value;
        
        // Overall accuracy: 6 correct out of 8 total = 75%
        metrics.OverallAccuracy.Should().BeApproximately(0.75, 0.001);
        metrics.TotalTestCases.Should().Be(8);
        metrics.CorrectPredictions.Should().Be(6);
        metrics.CategoryCount.Should().Be(3);
        
        // Category-specific metrics
        metrics.PrecisionByCategory.Should().HaveCount(3);
        metrics.RecallByCategory.Should().HaveCount(3);
        metrics.F1ScoreByCategory.Should().HaveCount(3);
        
        // Breaking Bad metrics: 3 TP, 1 FN, 1 FP (from The Office misclassification)
        // Precision = TP / (TP + FP) = 3 / (3 + 1) = 0.75
        // Recall = TP / (TP + FN) = 3 / (3 + 1) = 0.75
        metrics.PrecisionByCategory["BREAKING BAD"].Should().BeApproximately(0.75, 0.001);
        metrics.RecallByCategory["BREAKING BAD"].Should().BeApproximately(0.75, 0.001);
        
        // The Office metrics: 2 TP, 0 FN, 0 FP
        // Precision = 2 / (2 + 0) = 1.0, Recall = 2 / (2 + 0) = 1.0
        metrics.PrecisionByCategory["THE OFFICE"].Should().Be(1.0);
        metrics.RecallByCategory["THE OFFICE"].Should().Be(1.0);
        
        // Verify average confidence calculation
        var expectedAvgConfidence = testCases.Where(t => t.PredictionConfidence.HasValue)
                                            .Average(t => t.PredictionConfidence!.Value);
        metrics.AverageConfidence.Should().BeApproximately(expectedAvgConfidence, 0.001);
        
        // Verify macro averages are calculated correctly
        metrics.MacroPrecision.Should().BeGreaterThan(0.0);
        metrics.MacroRecall.Should().BeGreaterThan(0.0);
        metrics.MacroF1Score.Should().BeGreaterThan(0.0);
        
        // Verify weighted averages account for class distribution
        metrics.WeightedPrecision.Should().BeGreaterThan(0.0);
        metrics.WeightedRecall.Should().BeGreaterThan(0.0);
        metrics.WeightedF1Score.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public async Task EvaluateAccuracyAsync_WithEmptyTestData_ShouldReturnError()
    {
        // Given - Empty test dataset
        var emptyTestCases = new List<EvaluationTestCase>().AsReadOnly();

        // When - Evaluate accuracy with empty data
        var result = await _service.EvaluateAccuracyAsync(emptyTestCases);

        // Then - Should return error for empty dataset
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task EvaluateAccuracyAsync_WithPerfectAccuracy_ShouldReturn100Percent()
    {
        // Given - Test dataset with all correct predictions
        var perfectTestCases = new List<EvaluationTestCase>
        {
            new() { Filename = "Test1.mkv", ExpectedCategory = "CATEGORY_A", PredictedCategory = "CATEGORY_A", PredictionConfidence = 0.95 },
            new() { Filename = "Test2.mkv", ExpectedCategory = "CATEGORY_B", PredictedCategory = "CATEGORY_B", PredictionConfidence = 0.88 },
            new() { Filename = "Test3.mkv", ExpectedCategory = "CATEGORY_A", PredictedCategory = "CATEGORY_A", PredictionConfidence = 0.92 },
            new() { Filename = "Test4.mkv", ExpectedCategory = "CATEGORY_B", PredictedCategory = "CATEGORY_B", PredictionConfidence = 0.89 }
        }.AsReadOnly();

        // When - Evaluate perfect accuracy
        var result = await _service.EvaluateAccuracyAsync(perfectTestCases);

        // Then - Should return 100% accuracy metrics
        result.IsSuccess.Should().BeTrue();
        var metrics = result.Value;
        
        metrics.OverallAccuracy.Should().Be(1.0);
        metrics.TotalTestCases.Should().Be(4);
        metrics.CorrectPredictions.Should().Be(4);
        
        // All categories should have perfect precision and recall
        metrics.PrecisionByCategory.Values.Should().OnlyContain(p => p == 1.0);
        metrics.RecallByCategory.Values.Should().OnlyContain(r => r == 1.0);
        metrics.F1ScoreByCategory.Values.Should().OnlyContain(f => f == 1.0);
        
        // Macro and weighted averages should be 1.0
        metrics.MacroPrecision.Should().Be(1.0);
        metrics.MacroRecall.Should().Be(1.0);
        metrics.MacroF1Score.Should().Be(1.0);
        metrics.WeightedPrecision.Should().Be(1.0);
        metrics.WeightedRecall.Should().Be(1.0);
        metrics.WeightedF1Score.Should().Be(1.0);
    }

    #endregion

    #region Confusion Matrix Tests

    [Fact]
    public async Task GenerateConfusionMatrixAsync_WithValidTestData_ShouldGenerateCorrectMatrix()
    {
        // Given - Test dataset with known misclassifications
        var testCases = new List<EvaluationTestCase>
        {
            // Category A: 2 correct, 1 misclassified as B
            new() { Filename = "A1.mkv", ExpectedCategory = "A", PredictedCategory = "A", PredictionConfidence = 0.95 },
            new() { Filename = "A2.mkv", ExpectedCategory = "A", PredictedCategory = "A", PredictionConfidence = 0.88 },
            new() { Filename = "A3.mkv", ExpectedCategory = "A", PredictedCategory = "B", PredictionConfidence = 0.65 },
            
            // Category B: 1 correct, 1 misclassified as C
            new() { Filename = "B1.mkv", ExpectedCategory = "B", PredictedCategory = "B", PredictionConfidence = 0.89 },
            new() { Filename = "B2.mkv", ExpectedCategory = "B", PredictedCategory = "C", PredictionConfidence = 0.72 },
            
            // Category C: 2 correct
            new() { Filename = "C1.mkv", ExpectedCategory = "C", PredictedCategory = "C", PredictionConfidence = 0.91 },
            new() { Filename = "C2.mkv", ExpectedCategory = "C", PredictedCategory = "C", PredictionConfidence = 0.87 }
        }.AsReadOnly();

        // When - Generate confusion matrix
        var result = await _service.GenerateConfusionMatrixAsync(testCases);

        // Then - Verify confusion matrix structure and values
        result.IsSuccess.Should().BeTrue();
        var matrix = result.Value;
        
        matrix.Categories.Should().HaveCount(3);
        matrix.Categories.Should().Contain("A");
        matrix.Categories.Should().Contain("B");
        matrix.Categories.Should().Contain("C");
        matrix.TotalPredictions.Should().Be(7);
        
        // Verify matrix dimensions
        matrix.Matrix.GetLength(0).Should().Be(3); // Rows (actual)
        matrix.Matrix.GetLength(1).Should().Be(3); // Columns (predicted)
        
        // Verify specific matrix values based on test data
        var aIndex = Array.IndexOf(matrix.Categories.ToArray(), "A");
        var bIndex = Array.IndexOf(matrix.Categories.ToArray(), "B");
        var cIndex = Array.IndexOf(matrix.Categories.ToArray(), "C");
        
        // A->A: 2, A->B: 1, A->C: 0
        matrix.Matrix[aIndex, aIndex].Should().Be(2);
        matrix.Matrix[aIndex, bIndex].Should().Be(1);
        matrix.Matrix[aIndex, cIndex].Should().Be(0);
        
        // B->A: 0, B->B: 1, B->C: 1
        matrix.Matrix[bIndex, aIndex].Should().Be(0);
        matrix.Matrix[bIndex, bIndex].Should().Be(1);
        matrix.Matrix[bIndex, cIndex].Should().Be(1);
        
        // C->A: 0, C->B: 0, C->C: 2
        matrix.Matrix[cIndex, aIndex].Should().Be(0);
        matrix.Matrix[cIndex, bIndex].Should().Be(0);
        matrix.Matrix[cIndex, cIndex].Should().Be(2);
        
        // Verify TP/FP/FN/TN calculations
        matrix.TruePositives["A"].Should().Be(2);
        matrix.FalsePositives["A"].Should().Be(0); // No other category predicted as A
        matrix.FalseNegatives["A"].Should().Be(1); // One A predicted as B
        matrix.TrueNegatives["A"].Should().Be(4); // Remaining predictions
        
        matrix.TruePositives["B"].Should().Be(1);
        matrix.FalsePositives["B"].Should().Be(1); // One A predicted as B
        matrix.FalseNegatives["B"].Should().Be(1); // One B predicted as C
        matrix.TrueNegatives["B"].Should().Be(4);
        
        matrix.TruePositives["C"].Should().Be(2);
        matrix.FalsePositives["C"].Should().Be(1); // One B predicted as C
        matrix.FalseNegatives["C"].Should().Be(0); // No C misclassified
        matrix.TrueNegatives["C"].Should().Be(4);
    }

    [Fact]
    public void ConfusionMatrix_ToFormattedString_ShouldGenerateReadableOutput()
    {
        // Given - Simple confusion matrix
        var categories = new[] { "A", "B", "C" }.ToList().AsReadOnly();
        var matrix = new int[3, 3];
        matrix[0, 0] = 10; matrix[0, 1] = 2; matrix[0, 2] = 1;
        matrix[1, 0] = 1; matrix[1, 1] = 8; matrix[1, 2] = 2;
        matrix[2, 0] = 0; matrix[2, 1] = 1; matrix[2, 2] = 12;

        var confusionMatrix = new EvaluationConfusionMatrix
        {
            Categories = categories,
            Matrix = matrix,
            TotalPredictions = 37,
            TruePositives = new Dictionary<string, int> { ["A"] = 10, ["B"] = 8, ["C"] = 12 }.AsReadOnly(),
            FalsePositives = new Dictionary<string, int> { ["A"] = 1, ["B"] = 3, ["C"] = 3 }.AsReadOnly(),
            FalseNegatives = new Dictionary<string, int> { ["A"] = 3, ["B"] = 4, ["C"] = 1 }.AsReadOnly(),
            TrueNegatives = new Dictionary<string, int> { ["A"] = 23, ["B"] = 22, ["C"] = 21 }.AsReadOnly()
        };

        // When - Format confusion matrix as string
        var formattedString = confusionMatrix.ToFormattedString();

        // Then - Verify formatted output contains expected elements
        formattedString.Should().NotBeNullOrEmpty();
        formattedString.Should().Contain("Confusion Matrix:");
        formattedString.Should().Contain("Predicted →");
        formattedString.Should().Contain("Actual ↓");
        formattedString.Should().Contain("A");
        formattedString.Should().Contain("B");
        formattedString.Should().Contain("C");
        formattedString.Should().Contain("10"); // Diagonal elements
        formattedString.Should().Contain("8");
        formattedString.Should().Contain("12");
    }

    #endregion

    #region Performance Benchmarking Tests

    [Fact]
    public async Task BenchmarkPerformanceAsync_WithValidConfiguration_ShouldReturnComprehensiveMetrics()
    {
        // Given - Benchmark configuration for ARM32 constraints
        var benchmarkConfig = new BenchmarkConfiguration
        {
            PredictionCount = 100,
            WarmupCount = 10,
            MonitorMemoryUsage = true,
            MonitorCpuUsage = true,
            TimeoutMs = 30000,
            BenchmarkFilenames = new List<string>
            {
                "Breaking.Bad.S01E01.mkv",
                "The.Office.S01E01.mkv",
                "Naruto.S01E01.mkv"
            }.AsReadOnly()
        };

        // Setup mock prediction service to simulate varying prediction times
        var predictionTimes = new Queue<double>(new[] { 45.0, 52.0, 38.0, 67.0, 41.0 });
        _mockPredictionService
            .Setup(x => x.PredictAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var time = predictionTimes.Count > 0 ? predictionTimes.Dequeue() : 50.0;
                Thread.Sleep((int)time); // Simulate processing time
                return Result<ClassificationResult>.Success(new ClassificationResult
                {
                    Filename = "test.mkv",
                    PredictedCategory = "TEST CATEGORY",
                    Confidence = 0.85f,
                    ProcessingTimeMs = (long)time
                });
            });

        // When - Run performance benchmark
        var result = await _service.BenchmarkPerformanceAsync(benchmarkConfig);

        // Then - Verify comprehensive benchmark metrics
        result.IsSuccess.Should().BeTrue();
        var benchmark = result.Value;
        
        // Timing metrics validation
        benchmark.AveragePredictionTimeMs.Should().BeGreaterThan(0);
        benchmark.MedianPredictionTimeMs.Should().BeGreaterThan(0);
        benchmark.P95PredictionTimeMs.Should().BeGreaterOrEqualTo(benchmark.MedianPredictionTimeMs);
        benchmark.P99PredictionTimeMs.Should().BeGreaterOrEqualTo(benchmark.P95PredictionTimeMs);
        
        // Throughput validation
        benchmark.ThroughputPredictionsPerSecond.Should().BeGreaterThan(0);
        benchmark.BenchmarkPredictionCount.Should().Be(100);
        
        // Memory usage validation (ARM32 constraints)
        benchmark.PeakMemoryUsageMB.Should().BeLessThan(300); // ARM32 memory limit
        benchmark.AverageMemoryUsageMB.Should().BeLessThan(250);
        benchmark.AverageMemoryUsageMB.Should().BeLessOrEqualTo(benchmark.PeakMemoryUsageMB);
        
        // Timing consistency validation
        benchmark.TotalBenchmarkTimeMs.Should().BeGreaterThan(0);
        var expectedMinTime = benchmark.AveragePredictionTimeMs * benchmarkConfig.PredictionCount;
        benchmark.TotalBenchmarkTimeMs.Should().BeGreaterThan(expectedMinTime * 0.8); // Allow some variance
        
        // Performance requirements validation
        if (benchmark.AveragePredictionTimeMs <= 100 && benchmark.PeakMemoryUsageMB <= 300)
        {
            benchmark.PassedPerformanceRequirements.Should().BeTrue();
            benchmark.PerformanceViolations.Should().BeEmpty();
        }
        else
        {
            benchmark.PassedPerformanceRequirements.Should().BeFalse();
            benchmark.PerformanceViolations.Should().NotBeEmpty();
        }
        
        // CPU statistics validation (if monitored)
        if (benchmarkConfig.MonitorCpuUsage && benchmark.CpuStats != null)
        {
            benchmark.CpuStats.AverageCpuUsagePercent.Should().BeInRange(0, 100);
            benchmark.CpuStats.PeakCpuUsagePercent.Should().BeInRange(0, 100);
            benchmark.CpuStats.PeakCpuUsagePercent.Should().BeGreaterOrEqualTo(benchmark.CpuStats.AverageCpuUsagePercent);
        }
    }

    [Theory]
    [InlineData(50, 200, true, "Good performance should pass requirements")]
    [InlineData(150, 280, false, "Slow predictions should fail requirements")]
    [InlineData(80, 320, false, "High memory usage should fail requirements")]
    [InlineData(120, 350, false, "Both slow and high memory should fail requirements")]
    public void ValidatePerformanceRequirements_WithVariousMetrics_ShouldAssessCorrectly(
        double avgPredictionTime, double peakMemory, bool shouldPass, string scenario)
    {
        // Given - Performance thresholds for ARM32 deployment
        var maxPredictionTimeMs = 100.0;
        var maxMemoryUsageMB = 300.0;
        
        var benchmark = new PerformanceBenchmark
        {
            AveragePredictionTimeMs = avgPredictionTime,
            MedianPredictionTimeMs = avgPredictionTime * 0.9,
            P95PredictionTimeMs = avgPredictionTime * 1.5,
            P99PredictionTimeMs = avgPredictionTime * 2.0,
            ThroughputPredictionsPerSecond = 1000.0 / avgPredictionTime,
            PeakMemoryUsageMB = peakMemory,
            AverageMemoryUsageMB = peakMemory * 0.8,
            TotalBenchmarkTimeMs = avgPredictionTime * 100,
            BenchmarkPredictionCount = 100,
            PassedPerformanceRequirements = shouldPass,
            PerformanceViolations = shouldPass ? Array.Empty<string>() : new[] { "Performance violation" }
        };

        // When - Validate performance requirements
        var timePassed = avgPredictionTime <= maxPredictionTimeMs;
        var memoryPassed = peakMemory <= maxMemoryUsageMB;
        var overallPassed = timePassed && memoryPassed;

        // Then - Verify performance assessment
        overallPassed.Should().Be(shouldPass, scenario);
        benchmark.PassedPerformanceRequirements.Should().Be(shouldPass);
        
        if (shouldPass)
        {
            benchmark.PerformanceViolations.Should().BeEmpty();
        }
        else
        {
            benchmark.PerformanceViolations.Should().NotBeEmpty();
        }
        
        // Verify individual constraints
        if (!timePassed)
        {
            avgPredictionTime.Should().BeGreaterThan(maxPredictionTimeMs);
        }
        
        if (!memoryPassed)
        {
            peakMemory.Should().BeGreaterThan(maxMemoryUsageMB);
        }
    }

    #endregion

    #region Cross-Validation Tests

    [Fact]
    public async Task PerformCrossValidationAsync_WithValidDataset_ShouldReturnConsistentResults()
    {
        // Given - Training dataset for cross-validation
        var trainingDataset = new List<TrainingSample>
        {
            new() 
            { 
                Filename = "Breaking.Bad.S01E01.mkv", 
                Category = "BREAKING BAD",
                Confidence = 0.95,
                Source = TrainingSampleSource.UserFeedback,
                CreatedAt = DateTime.UtcNow
            },
            new() 
            { 
                Filename = "Breaking.Bad.S01E02.mkv", 
                Category = "BREAKING BAD",
                Confidence = 0.93,
                Source = TrainingSampleSource.UserFeedback,
                CreatedAt = DateTime.UtcNow
            },
            new() 
            { 
                Filename = "Breaking.Bad.S01E03.mkv", 
                Category = "BREAKING BAD",
                Confidence = 0.92,
                Source = TrainingSampleSource.UserFeedback,
                CreatedAt = DateTime.UtcNow
            },
            new() 
            { 
                Filename = "The.Office.S01E01.mkv", 
                Category = "THE OFFICE",
                Confidence = 0.91,
                Source = TrainingSampleSource.UserFeedback,
                CreatedAt = DateTime.UtcNow
            },
            new() 
            { 
                Filename = "The.Office.S01E02.mkv", 
                Category = "THE OFFICE",
                Confidence = 0.89,
                Source = TrainingSampleSource.UserFeedback,
                CreatedAt = DateTime.UtcNow
            },
            new() 
            { 
                Filename = "The.Office.S01E03.mkv", 
                Category = "THE OFFICE",
                Confidence = 0.88,
                Source = TrainingSampleSource.UserFeedback,
                CreatedAt = DateTime.UtcNow
            },
            new() 
            { 
                Filename = "Naruto.S01E01.mkv", 
                Category = "NARUTO",
                Confidence = 0.87,
                Source = TrainingSampleSource.UserFeedback,
                CreatedAt = DateTime.UtcNow
            },
            new() 
            { 
                Filename = "Naruto.S01E02.mkv", 
                Category = "NARUTO",
                Confidence = 0.86,
                Source = TrainingSampleSource.UserFeedback,
                CreatedAt = DateTime.UtcNow
            },
            new() 
            { 
                Filename = "Naruto.S01E03.mkv", 
                Category = "NARUTO",
                Confidence = 0.85,
                Source = TrainingSampleSource.UserFeedback,
                CreatedAt = DateTime.UtcNow
            },
            new() 
            { 
                Filename = "One.Piece.E001.mkv", 
                Category = "ONE PIECE",
                Confidence = 0.90,
                Source = TrainingSampleSource.UserFeedback,
                CreatedAt = DateTime.UtcNow
            }
        }.AsReadOnly();

        const int folds = 5;

        // When - Perform cross-validation
        var result = await _service.PerformCrossValidationAsync(trainingDataset, folds);

        // Then - Verify cross-validation results
        result.IsSuccess.Should().BeTrue();
        var cvResults = result.Value;
        
        // Basic validation
        cvResults.FoldCount.Should().Be(folds);
        cvResults.FoldAccuracyScores.Should().HaveCount(folds);
        cvResults.FoldDetails.Should().HaveCount(folds);
        
        // Statistical validation
        cvResults.MeanAccuracy.Should().BeInRange(0.0, 1.0);
        cvResults.AccuracyStandardDeviation.Should().BeGreaterOrEqualTo(0.0);
        cvResults.AccuracyCoefficientsOfVariation.Should().BeGreaterOrEqualTo(0.0);
        
        // Confidence interval validation
        var (lower, upper) = cvResults.AccuracyConfidenceInterval;
        lower.Should().BeLessOrEqualTo(cvResults.MeanAccuracy);
        upper.Should().BeGreaterOrEqualTo(cvResults.MeanAccuracy);
        lower.Should().BeLessOrEqualTo(upper);
        
        // Verify fold details
        foreach (var fold in cvResults.FoldDetails)
        {
            fold.FoldNumber.Should().BeInRange(1, folds);
            fold.Accuracy.Should().BeInRange(0.0, 1.0);
            fold.Precision.Should().BeInRange(0.0, 1.0);
            fold.Recall.Should().BeInRange(0.0, 1.0);
            fold.F1Score.Should().BeInRange(0.0, 1.0);
            fold.TestSampleCount.Should().BeGreaterThan(0);
        }
        
        // Mathematical consistency
        var calculatedMean = cvResults.FoldAccuracyScores.Average();
        cvResults.MeanAccuracy.Should().BeApproximately(calculatedMean, 0.001);
        
        // Quality assessment validation
        cvResults.Quality.Should().BeOneOf(
            CrossValidationQuality.Poor,
            CrossValidationQuality.BelowAverage,
            CrossValidationQuality.Average,
            CrossValidationQuality.Good,
            CrossValidationQuality.Excellent
        );
        
        // Consistency indicators
        if (cvResults.AccuracyStandardDeviation <= 0.02 && cvResults.AccuracyCoefficientsOfVariation <= 0.03)
        {
            cvResults.Quality.Should().BeOneOf(CrossValidationQuality.Good, CrossValidationQuality.Excellent);
        }
    }

    [Theory]
    [InlineData(new double[] { 0.85, 0.84, 0.86, 0.85, 0.84 }, CrossValidationQuality.Excellent)]
    [InlineData(new double[] { 0.82, 0.88, 0.85, 0.87, 0.83 }, CrossValidationQuality.Good)]
    [InlineData(new double[] { 0.75, 0.85, 0.80, 0.82, 0.78 }, CrossValidationQuality.Average)]
    [InlineData(new double[] { 0.70, 0.90, 0.75, 0.85, 0.65 }, CrossValidationQuality.BelowAverage)]
    [InlineData(new double[] { 0.60, 0.95, 0.45, 0.88, 0.72 }, CrossValidationQuality.Poor)]
    public void AssessCrossValidationQuality_WithVariousConsistencyLevels_ShouldClassifyCorrectly(
        double[] accuracyScores, CrossValidationQuality expectedQuality)
    {
        // Given - Cross-validation accuracy scores
        var mean = accuracyScores.Average();
        var variance = accuracyScores.Select(x => Math.Pow(x - mean, 2)).Average();
        var stdDev = Math.Sqrt(variance);
        var coefficientOfVariation = stdDev / mean;

        // When - Assess cross-validation quality based on consistency
        var assessedQuality = coefficientOfVariation switch
        {
            <= 0.02 => CrossValidationQuality.Excellent,  // Very consistent
            <= 0.05 => CrossValidationQuality.Good,       // Consistent
            <= 0.08 => CrossValidationQuality.Average,    // Moderately consistent
            <= 0.12 => CrossValidationQuality.BelowAverage, // Inconsistent
            _ => CrossValidationQuality.Poor               // Very inconsistent
        };

        // Then - Verify quality assessment matches expected
        assessedQuality.Should().Be(expectedQuality);
        
        // Verify mathematical properties
        mean.Should().BeInRange(0.0, 1.0, "Mean accuracy should be valid");
        stdDev.Should().BeGreaterOrEqualTo(0.0, "Standard deviation should be non-negative");
        coefficientOfVariation.Should().BeGreaterOrEqualTo(0.0, "Coefficient of variation should be non-negative");
        
        // Verify consistency relationship with quality
        if (expectedQuality >= CrossValidationQuality.Good)
        {
            coefficientOfVariation.Should().BeLessOrEqualTo(0.06, "Good quality should have low variation");
        }
        
        if (expectedQuality == CrossValidationQuality.Poor)
        {
            coefficientOfVariation.Should().BeGreaterThan(0.10, "Poor quality should have high variation");
        }
    }

    #endregion

    #region Confidence Analysis Tests

    [Fact]
    public async Task AnalyzeConfidenceDistributionAsync_WithWellCalibratedPredictions_ShouldShowGoodCalibration()
    {
        // Given - Well-calibrated test cases where confidence matches accuracy
        var wellCalibratedTestCases = new List<EvaluationTestCase>
        {
            // High confidence, high accuracy
            new() { Filename = "High1.mkv", ExpectedCategory = "A", PredictedCategory = "A", PredictionConfidence = 0.95 },
            new() { Filename = "High2.mkv", ExpectedCategory = "A", PredictedCategory = "A", PredictionConfidence = 0.92 },
            new() { Filename = "High3.mkv", ExpectedCategory = "A", PredictedCategory = "A", PredictionConfidence = 0.88 },
            
            // Medium confidence, medium accuracy
            new() { Filename = "Med1.mkv", ExpectedCategory = "B", PredictedCategory = "B", PredictionConfidence = 0.75 },
            new() { Filename = "Med2.mkv", ExpectedCategory = "B", PredictedCategory = "B", PredictionConfidence = 0.72 },
            new() { Filename = "Med3.mkv", ExpectedCategory = "B", PredictedCategory = "C", PredictionConfidence = 0.68 }, // One error
            
            // Low confidence, low accuracy
            new() { Filename = "Low1.mkv", ExpectedCategory = "C", PredictedCategory = "C", PredictionConfidence = 0.55 },
            new() { Filename = "Low2.mkv", ExpectedCategory = "C", PredictedCategory = "A", PredictionConfidence = 0.52 }, // Error
            new() { Filename = "Low3.mkv", ExpectedCategory = "C", PredictedCategory = "B", PredictionConfidence = 0.48 }  // Error
        }.AsReadOnly();

        // When - Analyze confidence distribution
        var result = await _service.AnalyzeConfidenceDistributionAsync(wellCalibratedTestCases);

        // Then - Verify good calibration indicators
        result.IsSuccess.Should().BeTrue();
        var analysis = result.Value;
        
        // Basic validation
        analysis.AverageConfidence.Should().BeInRange(0.0, 1.0);
        analysis.CalibrationError.Should().BeInRange(0.0, 1.0);
        analysis.BrierScore.Should().BeInRange(0.0, 1.0);
        analysis.ReliabilityIndex.Should().BeInRange(0.0, 1.0);
        
        // Well-calibrated model indicators
        analysis.CalibrationError.Should().BeLessOrEqualTo(0.15, "Well-calibrated model should have low calibration error");
        analysis.BrierScore.Should().BeLessOrEqualTo(0.25, "Well-calibrated model should have reasonable Brier score");
        analysis.ReliabilityIndex.Should().BeGreaterOrEqualTo(0.70, "Reliable model should have high reliability index");
        
        // Confidence distribution validation
        analysis.ConfidenceDistribution.Should().NotBeEmpty();
        analysis.ConfidenceDistribution.Values.Sum().Should().Be(wellCalibratedTestCases.Count);
        
        // Calibration curve validation
        analysis.CalibrationCurve.Should().NotBeEmpty();
        foreach (var point in analysis.CalibrationCurve)
        {
            point.AverageConfidence.Should().BeInRange(0.0, 1.0);
            point.ActualAccuracy.Should().BeInRange(0.0, 1.0);
            point.SampleCount.Should().BeGreaterThan(0);
            
            // For well-calibrated model, confidence should be close to accuracy
            var calibrationGap = Math.Abs(point.AverageConfidence - point.ActualAccuracy);
            calibrationGap.Should().BeLessOrEqualTo(0.20, $"Calibration gap in bucket {point.ConfidenceBucket} should be reasonable");
        }
        
        // Bias assessment
        analysis.ConfidenceBias.Should().BeOneOf(
            ConfidenceBias.WellCalibrated,
            ConfidenceBias.UnderConfident,
            ConfidenceBias.OverConfident
        );
        
        // Quality assessment
        analysis.Quality.Should().BeOneOf(
            ConfidenceQuality.Average,
            ConfidenceQuality.Good,
            ConfidenceQuality.Excellent
        );
    }

    [Fact]
    public async Task AnalyzeConfidenceDistribution_WithOverconfidentModel_ShouldDetectBias()
    {
        // Given - Overconfident test cases (high confidence, low accuracy)
        var overconfidentTestCases = new List<EvaluationTestCase>
        {
            // High confidence but wrong predictions
            new() { Filename = "Over1.mkv", ExpectedCategory = "A", PredictedCategory = "B", PredictionConfidence = 0.95 },
            new() { Filename = "Over2.mkv", ExpectedCategory = "B", PredictedCategory = "C", PredictionConfidence = 0.92 },
            new() { Filename = "Over3.mkv", ExpectedCategory = "C", PredictedCategory = "A", PredictionConfidence = 0.89 },
            new() { Filename = "Over4.mkv", ExpectedCategory = "A", PredictedCategory = "C", PredictionConfidence = 0.87 },
            
            // Some correct high confidence predictions to make it realistic
            new() { Filename = "Correct1.mkv", ExpectedCategory = "A", PredictedCategory = "A", PredictionConfidence = 0.94 },
            new() { Filename = "Correct2.mkv", ExpectedCategory = "B", PredictedCategory = "B", PredictionConfidence = 0.91 }
        }.AsReadOnly();

        // When - Analyze overconfident predictions
        var result = await _service.AnalyzeConfidenceDistributionAsync(overconfidentTestCases);

        // Then - Verify overconfidence detection
        result.IsSuccess.Should().BeTrue();
        var analysis = result.Value;
        
        // Overconfidence indicators
        analysis.CalibrationError.Should().BeGreaterThan(0.10, "Overconfident model should have high calibration error");
        analysis.ConfidenceBias.Should().BeOneOf(ConfidenceBias.OverConfident, ConfidenceBias.SignificantlyOverConfident);
        analysis.Quality.Should().BeOneOf(ConfidenceQuality.Poor, ConfidenceQuality.BelowAverage);
        
        // High confidence distribution should show many high-confidence predictions
        analysis.AverageConfidence.Should().BeGreaterThan(0.80, "Overconfident model should have high average confidence");
        
        // Reliability should be lower due to overconfidence
        analysis.ReliabilityIndex.Should().BeLessThan(0.80, "Overconfident model should have lower reliability");
        
        // Brier score should be higher due to confident wrong predictions
        analysis.BrierScore.Should().BeGreaterThan(0.20, "Overconfident wrong predictions should increase Brier score");
    }

    #endregion

    #region Model Quality Report Tests

    [Fact]
    public async Task GenerateQualityReportAsync_WithComprehensiveEvaluation_ShouldProduceDetailedReport()
    {
        // Given - Comprehensive evaluation configuration
        var evaluationConfig = new ModelEvaluationConfiguration
        {
            TestDataset = CreateComprehensiveTestDataset(),
            PerformCrossValidation = true,
            CrossValidationFolds = 5,
            PerformBenchmarking = true,
            BenchmarkConfig = new BenchmarkConfiguration
            {
                PredictionCount = 100,
                WarmupCount = 10,
                MonitorMemoryUsage = true,
                MonitorCpuUsage = true
            },
            QualityThresholds = new QualityThresholds
            {
                MinAccuracy = 0.80,
                MinF1Score = 0.75,
                MaxPredictionTimeMs = 100.0,
                MaxMemoryUsageMB = 300.0
            },
            AnalyzeConfidence = true
        };

        // When - Generate comprehensive quality report
        var result = await _service.GenerateQualityReportAsync(evaluationConfig);

        // Then - Verify comprehensive quality report
        result.IsSuccess.Should().BeTrue();
        var report = result.Value;
        
        // Basic report structure
        report.ModelVersion.Should().NotBeNullOrEmpty();
        report.OverallQualityScore.Should().BeInRange(0.0, 1.0);
        report.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        
        // Required components
        report.AccuracyMetrics.Should().NotBeNull();
        report.PerformanceBenchmark.Should().NotBeNull();
        report.QualityAssessment.Should().NotBeNull();
        
        // Optional components based on configuration
        if (evaluationConfig.PerformCrossValidation)
        {
            report.CrossValidationResults.Should().NotBeNull();
        }
        
        if (evaluationConfig.AnalyzeConfidence)
        {
            report.ConfidenceAnalysis.Should().NotBeNull();
        }
        
        // Quality assessment validation
        var assessment = report.QualityAssessment;
        assessment.AccuracyRating.Should().BeOneOf(QualityRating.Poor, QualityRating.BelowAverage, 
            QualityRating.Average, QualityRating.Good, QualityRating.Excellent);
        assessment.PerformanceRating.Should().BeOneOf(QualityRating.Poor, QualityRating.BelowAverage,
            QualityRating.Average, QualityRating.Good, QualityRating.Excellent);
        assessment.ProductionReadiness.Should().BeOneOf(ModelReadiness.NotReady, ModelReadiness.DevelopmentOnly,
            ModelReadiness.StagingReady, ModelReadiness.ProductionReady, ModelReadiness.ExceedsRequirements);
        
        // Overall quality score should reflect component ratings
        if (assessment.AccuracyRating >= QualityRating.Good && assessment.PerformanceRating >= QualityRating.Good)
        {
            report.OverallQualityScore.Should().BeGreaterThan(0.70);
            assessment.ProductionReadiness.Should().BeOneOf(ModelReadiness.StagingReady, 
                ModelReadiness.ProductionReady, ModelReadiness.ExceedsRequirements);
        }
        
        // Recommendations should be provided
        report.ImprovementRecommendations.Should().NotBeNull();
        
        // Issues tracking
        assessment.CriticalIssues.Should().NotBeNull();
        assessment.Warnings.Should().NotBeNull();
        
        if (assessment.ProductionReadiness == ModelReadiness.NotReady)
        {
            assessment.CriticalIssues.Should().NotBeEmpty("Not ready model should have critical issues listed");
        }
    }

    #endregion

    #region Helper Methods

    private static IReadOnlyList<EvaluationTestCase> CreateComprehensiveTestDataset()
    {
        return new List<EvaluationTestCase>
        {
            // High-performing Western series
            new() { Filename = "Breaking.Bad.S01E01.mkv", ExpectedCategory = "BREAKING BAD", PredictedCategory = "BREAKING BAD", PredictionConfidence = 0.95, PredictionTimeMs = 42.5 },
            new() { Filename = "Breaking.Bad.S05E16.mkv", ExpectedCategory = "BREAKING BAD", PredictedCategory = "BREAKING BAD", PredictionConfidence = 0.92, PredictionTimeMs = 38.2 },
            new() { Filename = "The.Office.S02E01.mkv", ExpectedCategory = "THE OFFICE", PredictedCategory = "THE OFFICE", PredictionConfidence = 0.89, PredictionTimeMs = 45.1 },
            new() { Filename = "The.Office.S09E23.mkv", ExpectedCategory = "THE OFFICE", PredictedCategory = "THE OFFICE", PredictionConfidence = 0.87, PredictionTimeMs = 41.8 },
            
            // Anime series with Italian patterns
            new() { Filename = "Naruto.Shippuden.S01E01.Sub.ITA.mkv", ExpectedCategory = "NARUTO", PredictedCategory = "NARUTO", PredictionConfidence = 0.82, PredictionTimeMs = 52.3 },
            new() { Filename = "One.Piece.E1089.Sub.ITA.1080p.mkv", ExpectedCategory = "ONE PIECE", PredictedCategory = "ONE PIECE", PredictionConfidence = 0.78, PredictionTimeMs = 48.7 },
            new() { Filename = "Attack.on.Titan.S04E28.Sub.ITA.mkv", ExpectedCategory = "ATTACK ON TITAN", PredictedCategory = "ATTACK ON TITAN", PredictionConfidence = 0.84, PredictionTimeMs = 46.9 },
            
            // Some misclassifications to make realistic
            new() { Filename = "Obscure.Series.S01E01.mkv", ExpectedCategory = "OBSCURE SERIES", PredictedCategory = "UNKNOWN", PredictionConfidence = 0.35, PredictionTimeMs = 51.2 },
            new() { Filename = "Similar.Name.To.Office.S01E01.mkv", ExpectedCategory = "SIMILAR SERIES", PredictedCategory = "THE OFFICE", PredictionConfidence = 0.67, PredictionTimeMs = 44.6 },
            
            // Unknown category
            new() { Filename = "Random.Unknown.File.mkv", ExpectedCategory = "UNKNOWN", PredictedCategory = "UNKNOWN", PredictionConfidence = 0.25, PredictionTimeMs = 39.8 }
        }.AsReadOnly();
    }

    #endregion
}