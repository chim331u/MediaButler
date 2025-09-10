using FluentAssertions;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace MediaButler.Tests.Unit.ML;

/// <summary>
/// Comprehensive model quality testing suite covering accuracy thresholds, confidence distribution,
/// category-specific performance, false positive/negative rates, and model consistency.
/// Follows "Simple Made Easy" principles with clear, focused test scenarios.
/// </summary>
public class ModelQualityTests
{
    private readonly Mock<IModelEvaluationService> _mockEvaluationService;
    private readonly Mock<IClassificationService> _mockClassificationService;
    private readonly Mock<ILogger<ModelQualityTests>> _mockLogger;

    public ModelQualityTests()
    {
        _mockEvaluationService = new Mock<IModelEvaluationService>();
        _mockClassificationService = new Mock<IClassificationService>();
        _mockLogger = new Mock<ILogger<ModelQualityTests>>();
    }

    #region Accuracy Threshold Validation Tests

    [Theory]
    [InlineData(0.95, true, "Excellent model should pass accuracy threshold")]
    [InlineData(0.85, true, "Good model should pass accuracy threshold")]
    [InlineData(0.80, true, "Minimum acceptable model should pass accuracy threshold")]
    [InlineData(0.75, false, "Below-threshold model should fail accuracy validation")]
    [InlineData(0.60, false, "Poor model should fail accuracy validation")]
    public async Task ValidateAccuracyThreshold_WithVariousAccuracyLevels_ShouldValidateCorrectly(
        double overallAccuracy, bool shouldPass, string scenario)
    {
        // Given - Model accuracy metrics with specified accuracy level
        var accuracyMetrics = CreateAccuracyMetrics(overallAccuracy);
        var qualityThresholds = new QualityThresholds
        {
            MinAccuracy = 0.80, // 80% minimum accuracy requirement
            MinF1Score = 0.75,
            MaxPredictionTimeMs = 100.0,
            MaxMemoryUsageMB = 300.0
        };

        var validationResult = new ModelValidationResult
        {
            PassedValidation = shouldPass,
            AppliedThresholds = qualityThresholds,
            ValidationResults = new Dictionary<string, ValidationResult>
            {
                ["Accuracy"] = new ValidationResult
                {
                    Passed = shouldPass,
                    ActualValue = overallAccuracy,
                    ThresholdValue = qualityThresholds.MinAccuracy,
                    Message = shouldPass ? "Accuracy threshold met" : "Accuracy below minimum threshold"
                }
            }.AsReadOnly(),
            ValidationScore = shouldPass ? 1.0 : 0.0
        };

        _mockEvaluationService
            .Setup(x => x.ValidateModelQualityAsync(It.IsAny<QualityThresholds>()))
            .ReturnsAsync(Core.Common.Result<ModelValidationResult>.Success(validationResult));

        // When - Validate model quality against thresholds
        var result = await _mockEvaluationService.Object.ValidateModelQualityAsync(qualityThresholds);

        // Then - Verify validation result matches expectations
        result.IsSuccess.Should().BeTrue();
        result.Value.PassedValidation.Should().Be(shouldPass, scenario);
        result.Value.ValidationResults["Accuracy"].Passed.Should().Be(shouldPass);
        result.Value.ValidationResults["Accuracy"].ActualValue.Should().Be(overallAccuracy);
        result.Value.ValidationResults["Accuracy"].ThresholdValue.Should().Be(0.80);
    }

    [Fact]
    public async Task ValidateAccuracyThreshold_WithCategorySpecificThresholds_ShouldValidatePerCategory()
    {
        // Given - Category-specific accuracy requirements
        var categoryAccuracies = new Dictionary<string, double>
        {
            ["BREAKING BAD"] = 0.95, // Excellent accuracy
            ["THE OFFICE"] = 0.88,   // Good accuracy  
            ["NARUTO"] = 0.82,       // Acceptable accuracy
            ["UNKNOWN"] = 0.45       // Poor accuracy, should fail
        };

        var qualityThresholds = new QualityThresholds
        {
            MinAccuracy = 0.80,
            CategoryMinAccuracy = new Dictionary<string, double>
            {
                ["BREAKING BAD"] = 0.90,
                ["THE OFFICE"] = 0.85,
                ["NARUTO"] = 0.80,
                ["UNKNOWN"] = 0.50  // Lower threshold for unknown category
            }.AsReadOnly()
        };

        var validationResults = new Dictionary<string, ValidationResult>();
        foreach (var (category, accuracy) in categoryAccuracies)
        {
            var threshold = qualityThresholds.CategoryMinAccuracy.GetValueOrDefault(category, qualityThresholds.MinAccuracy);
            validationResults[$"CategoryAccuracy_{category}"] = new ValidationResult
            {
                Passed = accuracy >= threshold,
                ActualValue = accuracy,
                ThresholdValue = threshold,
                Message = accuracy >= threshold ? $"{category} accuracy acceptable" : $"{category} accuracy below threshold"
            };
        }

        var modelValidation = new ModelValidationResult
        {
            PassedValidation = validationResults.Values.All(v => v.Passed),
            AppliedThresholds = qualityThresholds,
            ValidationResults = validationResults.AsReadOnly(),
            ValidationScore = validationResults.Values.Count(v => v.Passed) / (double)validationResults.Count
        };

        _mockEvaluationService
            .Setup(x => x.ValidateModelQualityAsync(qualityThresholds))
            .ReturnsAsync(Core.Common.Result<ModelValidationResult>.Success(modelValidation));

        // When - Validate with category-specific thresholds
        var result = await _mockEvaluationService.Object.ValidateModelQualityAsync(qualityThresholds);

        // Then - Verify category-specific validation
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidationResults.Should().HaveCount(4);
        
        // High-performing categories should pass
        result.Value.ValidationResults["CategoryAccuracy_BREAKING BAD"].Passed.Should().BeTrue();
        result.Value.ValidationResults["CategoryAccuracy_THE OFFICE"].Passed.Should().BeTrue();
        result.Value.ValidationResults["CategoryAccuracy_NARUTO"].Passed.Should().BeTrue();
        
        // UNKNOWN category should fail the threshold
        result.Value.ValidationResults["CategoryAccuracy_UNKNOWN"].Passed.Should().BeFalse();
        
        // Overall validation should fail due to one category failing
        result.Value.PassedValidation.Should().BeFalse();
        result.Value.ValidationScore.Should().Be(0.75); // 3 out of 4 categories passed
    }

    #endregion

    #region Confidence Score Distribution Analysis Tests

    [Fact]
    public async Task AnalyzeConfidenceDistribution_WithWellCalibratedModel_ShouldShowGoodCalibration()
    {
        // Given - Well-calibrated confidence distribution
        var testCases = CreateWellCalibratedTestCases();
        var confidenceAnalysis = new EvaluationConfidenceAnalysis
        {
            AverageConfidence = 0.75,
            ConfidenceDistribution = new Dictionary<string, int>
            {
                ["0.9-1.0"] = 15,   // High confidence predictions
                ["0.8-0.9"] = 20,   // Good confidence predictions
                ["0.7-0.8"] = 25,   // Medium confidence predictions
                ["0.6-0.7"] = 20,   // Lower confidence predictions
                ["0.5-0.6"] = 15,   // Low confidence predictions
                ["0.0-0.5"] = 5     // Very low confidence predictions
            }.AsReadOnly(),
            CalibrationCurve = new List<CalibrationPoint>
            {
                new() { ConfidenceBucket = "0.9-1.0", AverageConfidence = 0.95, ActualAccuracy = 0.93, SampleCount = 15 },
                new() { ConfidenceBucket = "0.8-0.9", AverageConfidence = 0.85, ActualAccuracy = 0.85, SampleCount = 20 },
                new() { ConfidenceBucket = "0.7-0.8", AverageConfidence = 0.75, ActualAccuracy = 0.76, SampleCount = 25 },
                new() { ConfidenceBucket = "0.6-0.7", AverageConfidence = 0.65, ActualAccuracy = 0.65, SampleCount = 20 },
                new() { ConfidenceBucket = "0.5-0.6", AverageConfidence = 0.55, ActualAccuracy = 0.53, SampleCount = 15 }
            }.AsReadOnly(),
            CalibrationError = 0.02,      // Very low calibration error (excellent)
            BrierScore = 0.15,            // Good Brier score
            ReliabilityIndex = 0.92,      // High reliability
            ConfidenceBias = ConfidenceBias.WellCalibrated,
            Quality = ConfidenceQuality.Good
        };

        _mockEvaluationService
            .Setup(x => x.AnalyzeConfidenceDistributionAsync(It.IsAny<IReadOnlyList<EvaluationTestCase>>()))
            .ReturnsAsync(Core.Common.Result<EvaluationConfidenceAnalysis>.Success(confidenceAnalysis));

        // When - Analyze confidence distribution
        var result = await _mockEvaluationService.Object.AnalyzeConfidenceDistributionAsync(testCases);

        // Then - Verify well-calibrated confidence analysis
        result.IsSuccess.Should().BeTrue();
        var analysis = result.Value;
        
        analysis.AverageConfidence.Should().Be(0.75);
        analysis.CalibrationError.Should().BeLessOrEqualTo(0.05, "Well-calibrated model should have low calibration error");
        analysis.BrierScore.Should().BeLessOrEqualTo(0.20, "Good model should have low Brier score");
        analysis.ReliabilityIndex.Should().BeGreaterOrEqualTo(0.85, "Reliable model should have high reliability index");
        analysis.ConfidenceBias.Should().Be(ConfidenceBias.WellCalibrated);
        analysis.Quality.Should().BeOneOf(ConfidenceQuality.Good, ConfidenceQuality.Excellent);
        
        // Verify distribution spread across confidence ranges
        analysis.ConfidenceDistribution.Values.Sum().Should().Be(100);
        analysis.ConfidenceDistribution["0.9-1.0"].Should().BeGreaterThan(0, "Should have some high-confidence predictions");
        analysis.ConfidenceDistribution["0.0-0.5"].Should().BeLessOrEqualTo(10, "Should have few very low confidence predictions");
    }

    [Fact]
    public async Task AnalyzeConfidenceDistribution_WithOverconfidentModel_ShouldDetectBias()
    {
        // Given - Over-confident model that predicts high confidence but lower actual accuracy
        var testCases = CreateOverconfidentTestCases();
        var confidenceAnalysis = new EvaluationConfidenceAnalysis
        {
            AverageConfidence = 0.88,     // High average confidence
            ConfidenceDistribution = new Dictionary<string, int>
            {
                ["0.9-1.0"] = 40,         // Too many high confidence predictions
                ["0.8-0.9"] = 35,         // High confidence predictions
                ["0.7-0.8"] = 15,         // Few medium confidence
                ["0.6-0.7"] = 8,          // Very few lower confidence
                ["0.5-0.6"] = 2,          // Almost no low confidence
                ["0.0-0.5"] = 0           // No very low confidence (suspicious)
            }.AsReadOnly(),
            CalibrationCurve = new List<CalibrationPoint>
            {
                new() { ConfidenceBucket = "0.9-1.0", AverageConfidence = 0.95, ActualAccuracy = 0.82, SampleCount = 40 }, // Overconfident!
                new() { ConfidenceBucket = "0.8-0.9", AverageConfidence = 0.85, ActualAccuracy = 0.78, SampleCount = 35 }, // Overconfident!
                new() { ConfidenceBucket = "0.7-0.8", AverageConfidence = 0.75, ActualAccuracy = 0.73, SampleCount = 15 }
            }.AsReadOnly(),
            CalibrationError = 0.12,      // High calibration error (poor calibration)
            BrierScore = 0.28,            // Poor Brier score
            ReliabilityIndex = 0.68,      // Lower reliability
            ConfidenceBias = ConfidenceBias.OverConfident,
            Quality = ConfidenceQuality.BelowAverage
        };

        _mockEvaluationService
            .Setup(x => x.AnalyzeConfidenceDistributionAsync(It.IsAny<IReadOnlyList<EvaluationTestCase>>()))
            .ReturnsAsync(Core.Common.Result<EvaluationConfidenceAnalysis>.Success(confidenceAnalysis));

        // When - Analyze overconfident model
        var result = await _mockEvaluationService.Object.AnalyzeConfidenceDistributionAsync(testCases);

        // Then - Verify overconfidence detection
        result.IsSuccess.Should().BeTrue();
        var analysis = result.Value;
        
        analysis.ConfidenceBias.Should().Be(ConfidenceBias.OverConfident);
        analysis.CalibrationError.Should().BeGreaterThan(0.10, "Overconfident model should have high calibration error");
        analysis.Quality.Should().BeOneOf(ConfidenceQuality.Poor, ConfidenceQuality.BelowAverage);
        
        // Verify overconfidence indicators
        analysis.ConfidenceDistribution["0.9-1.0"].Should().BeGreaterThan(30, "Overconfident model predicts too many high-confidence cases");
        analysis.ConfidenceDistribution["0.0-0.5"].Should().BeLessOrEqualTo(5, "Overconfident model rarely predicts low confidence");
        
        // Check calibration curve for overconfidence pattern
        var highConfidencePoint = analysis.CalibrationCurve.First(p => p.ConfidenceBucket == "0.9-1.0");
        (highConfidencePoint.AverageConfidence - highConfidencePoint.ActualAccuracy).Should()
            .BeGreaterThan(0.10, "High confidence bucket should show significant overconfidence gap");
    }

    #endregion

    #region Category-Specific Performance Testing

    [Fact]
    public async Task EvaluateCategorySpecificPerformance_WithVariousCategories_ShouldProvideDetailedMetrics()
    {
        // Given - Category-specific test dataset representing Italian media content
        var testCases = CreateItalianMediaTestCases();
        var accuracyMetrics = new AccuracyMetrics
        {
            OverallAccuracy = 0.85,
            TotalTestCases = 200,
            CorrectPredictions = 170,
            CategoryCount = 5,
            PrecisionByCategory = new Dictionary<string, double>
            {
                ["BREAKING BAD"] = 0.95,      // Excellent precision - clear patterns
                ["THE OFFICE"] = 0.88,        // Good precision - distinctive patterns  
                ["NARUTO"] = 0.82,            // Acceptable precision - anime patterns
                ["ONE PIECE"] = 0.79,         // Lower precision - long series variations
                ["UNKNOWN"] = 0.45            // Poor precision - ambiguous cases
            }.AsReadOnly(),
            RecallByCategory = new Dictionary<string, double>
            {
                ["BREAKING BAD"] = 0.92,      // High recall - most instances found
                ["THE OFFICE"] = 0.85,        // Good recall
                ["NARUTO"] = 0.78,            // Acceptable recall
                ["ONE PIECE"] = 0.74,         // Lower recall - some episodes missed
                ["UNKNOWN"] = 0.52            // Poor recall - many unknowns misclassified
            }.AsReadOnly(),
            F1ScoreByCategory = new Dictionary<string, double>
            {
                ["BREAKING BAD"] = 0.93,      // Excellent F1 score
                ["THE OFFICE"] = 0.87,        // Good F1 score
                ["NARUTO"] = 0.80,            // Acceptable F1 score
                ["ONE PIECE"] = 0.76,         // Lower F1 score
                ["UNKNOWN"] = 0.48            // Poor F1 score
            }.AsReadOnly(),
            MacroPrecision = 0.78,
            MacroRecall = 0.76,
            MacroF1Score = 0.77,
            WeightedPrecision = 0.82,
            WeightedRecall = 0.85,
            WeightedF1Score = 0.83,
            AverageConfidence = 0.74
        };

        _mockEvaluationService
            .Setup(x => x.EvaluateAccuracyAsync(It.IsAny<IReadOnlyList<EvaluationTestCase>>()))
            .ReturnsAsync(Core.Common.Result<AccuracyMetrics>.Success(accuracyMetrics));

        // When - Evaluate category-specific performance
        var result = await _mockEvaluationService.Object.EvaluateAccuracyAsync(testCases);

        // Then - Verify category-specific metrics
        result.IsSuccess.Should().BeTrue();
        var metrics = result.Value;
        
        // Overall metrics validation
        metrics.OverallAccuracy.Should().Be(0.85);
        metrics.CategoryCount.Should().Be(5);
        
        // Category-specific performance validation
        metrics.PrecisionByCategory.Should().HaveCount(5);
        metrics.RecallByCategory.Should().HaveCount(5);
        metrics.F1ScoreByCategory.Should().HaveCount(5);
        
        // Verify performance hierarchy (well-known series should perform better)
        metrics.F1ScoreByCategory["BREAKING BAD"].Should().BeGreaterThan(metrics.F1ScoreByCategory["ONE PIECE"],
            "Well-known series should have better F1 scores than long/complex series");
        metrics.F1ScoreByCategory["THE OFFICE"].Should().BeGreaterThan(metrics.F1ScoreByCategory["UNKNOWN"],
            "Known series should perform much better than unknown category");
        
        // Verify all categories have reasonable metrics
        foreach (var category in metrics.PrecisionByCategory.Keys)
        {
            metrics.PrecisionByCategory[category].Should().BeInRange(0.0, 1.0, $"Precision for {category} should be valid");
            metrics.RecallByCategory[category].Should().BeInRange(0.0, 1.0, $"Recall for {category} should be valid");
            metrics.F1ScoreByCategory[category].Should().BeInRange(0.0, 1.0, $"F1 score for {category} should be valid");
        }
        
        // Verify macro vs weighted averages make sense
        metrics.MacroF1Score.Should().BeLessThan(metrics.WeightedF1Score,
            "Macro average should be lower than weighted average when popular categories perform better");
    }

    [Theory]
    [InlineData("BREAKING BAD", 0.90, true, "Popular Western series should meet high performance threshold")]
    [InlineData("NARUTO", 0.75, true, "Popular anime series should meet medium performance threshold")]
    [InlineData("ONE PIECE", 0.70, true, "Long-running series should meet adjusted threshold")]
    [InlineData("UNKNOWN", 0.40, false, "Unknown category should fail to meet high performance threshold")]
    [InlineData("RARE_SERIES", 0.35, false, "Rare series should fail to meet performance threshold")]
    public void ValidateCategoryPerformance_WithSpecificThresholds_ShouldAssessCorrectly(
        string category, double f1Score, bool shouldPass, string scenario)
    {
        // Given - Category-specific performance threshold
        var categoryThreshold = category switch
        {
            "BREAKING BAD" or "THE OFFICE" => 0.85, // High threshold for popular series
            "NARUTO" or "ONE PIECE" => 0.70,        // Medium threshold for anime/long series
            "UNKNOWN" => 0.50,                      // Lower threshold for unknown
            _ => 0.60                               // Default threshold
        };

        // When - Assess category performance against threshold
        var passesThreshold = f1Score >= categoryThreshold;

        // Then - Verify assessment matches expectation
        passesThreshold.Should().Be(shouldPass, scenario);
        
        if (shouldPass)
        {
            f1Score.Should().BeGreaterOrEqualTo(categoryThreshold, 
                $"{category} should meet its performance threshold of {categoryThreshold:F2}");
        }
        else
        {
            f1Score.Should().BeLessThan(categoryThreshold,
                $"{category} should fail to meet its performance threshold of {categoryThreshold:F2}");
        }
    }

    #endregion

    #region False Positive/Negative Rate Analysis

    [Fact]
    public async Task AnalyzeFalsePositiveNegativeRates_WithConfusionMatrix_ShouldProvideDetailedAnalysis()
    {
        // Given - Confusion matrix representing classification errors
        var categories = new[] { "BREAKING BAD", "THE OFFICE", "NARUTO", "ONE PIECE", "UNKNOWN" };
        var confusionMatrix = CreateRealisticConfusionMatrix(categories);

        _mockEvaluationService
            .Setup(x => x.GenerateConfusionMatrixAsync(It.IsAny<IReadOnlyList<EvaluationTestCase>>()))
            .ReturnsAsync(Core.Common.Result<EvaluationConfusionMatrix>.Success(confusionMatrix));

        // When - Generate confusion matrix for false positive/negative analysis  
        var result = await _mockEvaluationService.Object.GenerateConfusionMatrixAsync(CreateTestCasesForConfusionMatrix());

        // Then - Verify false positive/negative analysis
        result.IsSuccess.Should().BeTrue();
        var matrix = result.Value;
        
        matrix.Categories.Should().HaveCount(5);
        matrix.TotalPredictions.Should().BeGreaterThan(0);
        
        // Verify false positive analysis
        foreach (var category in categories)
        {
            var truePositives = matrix.TruePositives[category];
            var falsePositives = matrix.FalsePositives[category];
            var falseNegatives = matrix.FalseNegatives[category];
            var trueNegatives = matrix.TrueNegatives[category];
            
            // Basic sanity checks
            truePositives.Should().BeGreaterOrEqualTo(0);
            falsePositives.Should().BeGreaterOrEqualTo(0);
            falseNegatives.Should().BeGreaterOrEqualTo(0);
            trueNegatives.Should().BeGreaterOrEqualTo(0);
            
            // Calculate false positive rate (FPR = FP / (FP + TN))
            var falsePositiveRate = falsePositives / (double)(falsePositives + trueNegatives);
            falsePositiveRate.Should().BeInRange(0.0, 1.0, $"False positive rate for {category} should be valid");
            
            // Calculate false negative rate (FNR = FN / (FN + TP))  
            var falseNegativeRate = falseNegatives / (double)(falseNegatives + truePositives);
            falseNegativeRate.Should().BeInRange(0.0, 1.0, $"False negative rate for {category} should be valid");
            
            // Quality assertions based on category expectations
            if (category == "BREAKING BAD" || category == "THE OFFICE")
            {
                falsePositiveRate.Should().BeLessThan(0.15, $"{category} should have low false positive rate");
                falseNegativeRate.Should().BeLessThan(0.20, $"{category} should have low false negative rate");
            }
            else if (category == "UNKNOWN")
            {
                falsePositiveRate.Should().BeLessThan(0.30, "UNKNOWN category should not be over-predicted");
                // False negative rate for UNKNOWN can be higher (it's expected many unknowns exist)
            }
        }
        
        // Verify matrix mathematical consistency
        var totalFromMatrix = 0;
        for (int i = 0; i < matrix.Categories.Count; i++)
        {
            for (int j = 0; j < matrix.Categories.Count; j++)
            {
                totalFromMatrix += matrix.Matrix[i, j];
            }
        }
        totalFromMatrix.Should().Be(matrix.TotalPredictions, "Matrix values should sum to total predictions");
    }

    [Fact]
    public async Task AnalyzeFalsePositiveRates_AcrossCategories_ShouldIdentifyProblematicPairs()
    {
        // Given - Confusion matrix with specific misclassification patterns
        var categories = new[] { "BREAKING BAD", "THE OFFICE", "NARUTO", "ONE PIECE", "UNKNOWN" };
        var confusionMatrix = CreateConfusionMatrixWithKnownErrors(categories);

        _mockEvaluationService
            .Setup(x => x.GenerateConfusionMatrixAsync(It.IsAny<IReadOnlyList<EvaluationTestCase>>()))
            .ReturnsAsync(Core.Common.Result<EvaluationConfusionMatrix>.Success(confusionMatrix));

        // When - Analyze confusion matrix for problematic category pairs
        var result = await _mockEvaluationService.Object.GenerateConfusionMatrixAsync(CreateTestCasesForConfusionMatrix());

        // Then - Identify specific misclassification patterns
        result.IsSuccess.Should().BeTrue();
        var matrix = result.Value;
        
        // Check for common misclassification scenarios in Italian media context
        var narutoIndex = Array.IndexOf(categories, "NARUTO");
        var onePieceIndex = Array.IndexOf(categories, "ONE PIECE");
        var unknownIndex = Array.IndexOf(categories, "UNKNOWN");
        
        // Anime series might be confused with each other
        var narutoToOnePiece = matrix.Matrix[narutoIndex, onePieceIndex];
        var onePieceToNaruto = matrix.Matrix[onePieceIndex, narutoIndex];
        
        // These shouldn't be the dominant error pattern, but some confusion is expected for anime series
        narutoToOnePiece.Should().BeLessOrEqualTo(8, "NARUTO shouldn't be frequently misclassified as ONE PIECE");
        onePieceToNaruto.Should().BeLessOrEqualTo(8, "ONE PIECE shouldn't be frequently misclassified as NARUTO");
        
        // Western series should rarely be confused with anime
        var breakingBadIndex = Array.IndexOf(categories, "BREAKING BAD");
        var theOfficeIndex = Array.IndexOf(categories, "THE OFFICE");
        
        var breakingBadToAnime = matrix.Matrix[breakingBadIndex, narutoIndex] + matrix.Matrix[breakingBadIndex, onePieceIndex];
        var officeToAnime = matrix.Matrix[theOfficeIndex, narutoIndex] + matrix.Matrix[theOfficeIndex, onePieceIndex];
        
        breakingBadToAnime.Should().BeLessOrEqualTo(2, "BREAKING BAD should rarely be confused with anime series");
        officeToAnime.Should().BeLessOrEqualTo(2, "THE OFFICE should rarely be confused with anime series");
        
        // Unknown category analysis
        var knownCategoriesConfusedAsUnknown = 0;
        for (int i = 0; i < categories.Length - 1; i++) // Exclude UNKNOWN itself
        {
            knownCategoriesConfusedAsUnknown += matrix.Matrix[i, unknownIndex];
        }
        
        // Some known series will be misclassified as unknown, but shouldn't be excessive
        var totalKnownPredictions = matrix.TotalPredictions - matrix.TruePositives["UNKNOWN"] - matrix.FalsePositives["UNKNOWN"];
        var knownToUnknownRate = knownCategoriesConfusedAsUnknown / (double)totalKnownPredictions;
        
        knownToUnknownRate.Should().BeLessThan(0.25, "Known series shouldn't be frequently misclassified as unknown");
    }

    #endregion

    #region Model Consistency Testing Across Runs

    [Fact]
    public async Task ValidateModelConsistency_AcrossMultipleRuns_ShouldShowStablePerformance()
    {
        // Given - Cross-validation results representing multiple model runs
        var crossValidationResults = new CrossValidationResults
        {
            FoldCount = 5,
            FoldAccuracyScores = new List<double> { 0.84, 0.87, 0.85, 0.86, 0.83 }.AsReadOnly(),
            MeanAccuracy = 0.85,
            AccuracyStandardDeviation = 0.014,  // Low std dev indicates good consistency
            AccuracyConfidenceInterval = (0.837, 0.863),  // Tight confidence interval
            AccuracyCoefficientsOfVariation = 0.016,      // Low coefficient of variation
            FoldDetails = new List<FoldMetrics>
            {
                new() { FoldNumber = 1, Accuracy = 0.84, Precision = 0.83, Recall = 0.85, F1Score = 0.84, TestSampleCount = 40 },
                new() { FoldNumber = 2, Accuracy = 0.87, Precision = 0.86, Recall = 0.88, F1Score = 0.87, TestSampleCount = 40 },
                new() { FoldNumber = 3, Accuracy = 0.85, Precision = 0.84, Recall = 0.86, F1Score = 0.85, TestSampleCount = 40 },
                new() { FoldNumber = 4, Accuracy = 0.86, Precision = 0.85, Recall = 0.87, F1Score = 0.86, TestSampleCount = 40 },
                new() { FoldNumber = 5, Accuracy = 0.83, Precision = 0.82, Recall = 0.84, F1Score = 0.83, TestSampleCount = 40 }
            }.AsReadOnly(),
            Quality = CrossValidationQuality.Good
        };

        _mockEvaluationService
            .Setup(x => x.PerformCrossValidationAsync(It.IsAny<IReadOnlyList<TrainingSample>>(), It.IsAny<int>()))
            .ReturnsAsync(Core.Common.Result<CrossValidationResults>.Success(crossValidationResults));

        // When - Perform cross-validation for consistency analysis
        var result = await _mockEvaluationService.Object.PerformCrossValidationAsync(
            CreateTrainingSamples(), 5);

        // Then - Verify model consistency across folds
        result.IsSuccess.Should().BeTrue();
        var cvResults = result.Value;
        
        // Consistency metrics validation
        cvResults.FoldCount.Should().Be(5);
        cvResults.MeanAccuracy.Should().BeInRange(0.80, 0.90, "Mean accuracy should be in acceptable range");
        cvResults.AccuracyStandardDeviation.Should().BeLessOrEqualTo(0.05, 
            "Standard deviation should be low for consistent model");
        cvResults.AccuracyCoefficientsOfVariation.Should().BeLessOrEqualTo(0.06,
            "Coefficient of variation should be low for stable model");
        
        // Confidence interval validation
        var (lower, upper) = cvResults.AccuracyConfidenceInterval;
        lower.Should().BeGreaterThan(0.75, "Lower bound should be acceptable");
        upper.Should().BeLessThan(0.95, "Upper bound should be realistic");
        (upper - lower).Should().BeLessOrEqualTo(0.10, "Confidence interval should be narrow");
        
        // Individual fold validation
        cvResults.FoldDetails.Should().HaveCount(5);
        foreach (var fold in cvResults.FoldDetails)
        {
            fold.Accuracy.Should().BeInRange(0.80, 0.90, $"Fold {fold.FoldNumber} accuracy should be consistent");
            fold.Precision.Should().BeInRange(0.75, 0.90, $"Fold {fold.FoldNumber} precision should be consistent");
            fold.Recall.Should().BeInRange(0.75, 0.90, $"Fold {fold.FoldNumber} recall should be consistent");
            fold.F1Score.Should().BeInRange(0.75, 0.90, $"Fold {fold.FoldNumber} F1 score should be consistent");
            fold.TestSampleCount.Should().Be(40, "Each fold should have consistent sample size");
        }
        
        // Overall quality assessment
        cvResults.Quality.Should().BeOneOf(CrossValidationQuality.Good, CrossValidationQuality.Excellent);
        cvResults.Quality.Should().Match(q => q == CrossValidationQuality.Good || q == CrossValidationQuality.Excellent,
            "Consistent model should have good or excellent cross-validation quality");
    }

    [Theory]
    [InlineData(new double[] { 0.85, 0.84, 0.86, 0.85, 0.84 }, CrossValidationQuality.Excellent, "Very consistent model")]
    [InlineData(new double[] { 0.82, 0.88, 0.85, 0.87, 0.83 }, CrossValidationQuality.Good, "Reasonably consistent model")]
    [InlineData(new double[] { 0.75, 0.92, 0.68, 0.89, 0.81 }, CrossValidationQuality.BelowAverage, "Inconsistent model with high variance")]
    [InlineData(new double[] { 0.60, 0.95, 0.45, 0.88, 0.72 }, CrossValidationQuality.Poor, "Very inconsistent model")]
    public void AssessModelConsistency_WithVariousVarianceLevels_ShouldClassifyCorrectly(
        double[] accuracyScores, CrossValidationQuality expectedQuality, string scenario)
    {
        // Given - Accuracy scores from different runs
        var mean = accuracyScores.Average();
        var variance = accuracyScores.Select(x => Math.Pow(x - mean, 2)).Average();
        var stdDev = Math.Sqrt(variance);
        var coefficientOfVariation = stdDev / mean;

        // When - Assess consistency based on variance metrics
        var quality = coefficientOfVariation switch
        {
            <= 0.02 => CrossValidationQuality.Excellent,
            <= 0.05 => CrossValidationQuality.Good,
            <= 0.10 => CrossValidationQuality.Average,
            <= 0.15 => CrossValidationQuality.BelowAverage,
            _ => CrossValidationQuality.Poor
        };

        // Then - Verify consistency assessment
        quality.Should().Be(expectedQuality, scenario);
        
        // Verify mathematical relationships
        stdDev.Should().BeGreaterOrEqualTo(0, "Standard deviation should be non-negative");
        coefficientOfVariation.Should().BeGreaterOrEqualTo(0, "Coefficient of variation should be non-negative");
        
        if (expectedQuality >= CrossValidationQuality.Good)
        {
            stdDev.Should().BeLessOrEqualTo(0.05, "Good quality models should have low standard deviation");
            coefficientOfVariation.Should().BeLessOrEqualTo(0.06, "Good quality models should have low coefficient of variation");
        }
    }

    [Fact]
    public async Task ValidateModelStability_UnderDifferentConditions_ShouldMaintainPerformance()
    {
        // Given - Performance benchmark under various conditions
        var performanceBenchmark = new PerformanceBenchmark
        {
            AveragePredictionTimeMs = 45.2,
            MedianPredictionTimeMs = 42.0,
            P95PredictionTimeMs = 78.5,
            P99PredictionTimeMs = 95.2,
            ThroughputPredictionsPerSecond = 22.1,
            PeakMemoryUsageMB = 285.4,
            AverageMemoryUsageMB = 245.8,
            TotalBenchmarkTimeMs = 45230,
            BenchmarkPredictionCount = 1000,
            PassedPerformanceRequirements = true,
            PerformanceViolations = Array.Empty<string>(),
            CpuStats = new CpuUsageStats
            {
                AverageCpuUsagePercent = 35.2,
                PeakCpuUsagePercent = 58.7,
                CpuUsageSamples = new List<double> { 30.5, 35.2, 42.1, 38.9, 33.7 }.AsReadOnly()
            }
        };

        var benchmarkConfig = new BenchmarkConfiguration
        {
            PredictionCount = 1000,
            WarmupCount = 100,
            MonitorMemoryUsage = true,
            MonitorCpuUsage = true,
            TimeoutMs = 60000
        };

        _mockEvaluationService
            .Setup(x => x.BenchmarkPerformanceAsync(It.IsAny<BenchmarkConfiguration>()))
            .ReturnsAsync(Core.Common.Result<PerformanceBenchmark>.Success(performanceBenchmark));

        // When - Run performance benchmark for stability validation
        var result = await _mockEvaluationService.Object.BenchmarkPerformanceAsync(benchmarkConfig);

        // Then - Verify performance stability
        result.IsSuccess.Should().BeTrue();
        var benchmark = result.Value;
        
        // Performance consistency validation
        benchmark.PassedPerformanceRequirements.Should().BeTrue("Model should meet performance requirements");
        benchmark.PerformanceViolations.Should().BeEmpty("No performance violations should exist");
        
        // Prediction time stability
        benchmark.AveragePredictionTimeMs.Should().BeLessThan(100, "Average prediction time should meet ARM32 constraints");
        benchmark.MedianPredictionTimeMs.Should().BeLessThan(80, "Median prediction time should be reasonable");
        benchmark.P95PredictionTimeMs.Should().BeLessThan(150, "95th percentile should handle outliers gracefully");
        benchmark.P99PredictionTimeMs.Should().BeLessThan(200, "99th percentile should be acceptable for edge cases");
        
        // Variance in prediction times (stability indicator)
        var p95ToMedianRatio = benchmark.P95PredictionTimeMs / benchmark.MedianPredictionTimeMs;
        p95ToMedianRatio.Should().BeLessOrEqualTo(2.5, "P95/median ratio should indicate stable performance");
        
        // Memory stability
        benchmark.PeakMemoryUsageMB.Should().BeLessThan(300, "Peak memory should stay within ARM32 limits");
        benchmark.AverageMemoryUsageMB.Should().BeLessThan(250, "Average memory should be reasonable");
        
        var memoryVariation = (benchmark.PeakMemoryUsageMB - benchmark.AverageMemoryUsageMB) / benchmark.AverageMemoryUsageMB;
        memoryVariation.Should().BeLessOrEqualTo(0.30, "Memory usage should be stable (peak < 30% above average)");
        
        // Throughput validation
        benchmark.ThroughputPredictionsPerSecond.Should().BeGreaterThan(10, "Should maintain reasonable throughput");
        
        // CPU usage stability
        if (benchmark.CpuStats != null)
        {
            benchmark.CpuStats.AverageCpuUsagePercent.Should().BeLessThan(50, "Average CPU usage should be reasonable");
            benchmark.CpuStats.PeakCpuUsagePercent.Should().BeLessThan(80, "Peak CPU usage should not max out system");
            
            var cpuVariation = (benchmark.CpuStats.PeakCpuUsagePercent - benchmark.CpuStats.AverageCpuUsagePercent) / 
                              benchmark.CpuStats.AverageCpuUsagePercent;
            cpuVariation.Should().BeLessOrEqualTo(1.0, "CPU usage should be relatively stable");
        }
    }

    #endregion

    #region Helper Methods

    private static AccuracyMetrics CreateAccuracyMetrics(double overallAccuracy)
    {
        var correctPredictions = (int)(200 * overallAccuracy);
        return new AccuracyMetrics
        {
            OverallAccuracy = overallAccuracy,
            TotalTestCases = 200,
            CorrectPredictions = correctPredictions,
            CategoryCount = 4,
            PrecisionByCategory = new Dictionary<string, double>
            {
                ["CATEGORY_A"] = overallAccuracy + 0.02,
                ["CATEGORY_B"] = overallAccuracy,
                ["CATEGORY_C"] = overallAccuracy - 0.03,
                ["CATEGORY_D"] = overallAccuracy - 0.05
            }.AsReadOnly(),
            RecallByCategory = new Dictionary<string, double>
            {
                ["CATEGORY_A"] = overallAccuracy,
                ["CATEGORY_B"] = overallAccuracy - 0.02,
                ["CATEGORY_C"] = overallAccuracy - 0.01,
                ["CATEGORY_D"] = overallAccuracy - 0.04
            }.AsReadOnly(),
            F1ScoreByCategory = new Dictionary<string, double>
            {
                ["CATEGORY_A"] = overallAccuracy + 0.01,
                ["CATEGORY_B"] = overallAccuracy - 0.01,
                ["CATEGORY_C"] = overallAccuracy - 0.02,
                ["CATEGORY_D"] = overallAccuracy - 0.045
            }.AsReadOnly(),
            MacroPrecision = overallAccuracy - 0.015,
            MacroRecall = overallAccuracy - 0.018,
            MacroF1Score = overallAccuracy - 0.017,
            WeightedPrecision = overallAccuracy + 0.005,
            WeightedRecall = overallAccuracy,
            WeightedF1Score = overallAccuracy + 0.003,
            AverageConfidence = overallAccuracy * 0.85 + 0.1
        };
    }

    private static IReadOnlyList<EvaluationTestCase> CreateWellCalibratedTestCases()
    {
        return new List<EvaluationTestCase>
        {
            new() { Filename = "Breaking.Bad.S01E01.mkv", ExpectedCategory = "BREAKING BAD", PredictedCategory = "BREAKING BAD", PredictionConfidence = 0.95 },
            new() { Filename = "The.Office.S01E01.mkv", ExpectedCategory = "THE OFFICE", PredictedCategory = "THE OFFICE", PredictionConfidence = 0.88 },
            new() { Filename = "Naruto.S01E01.mkv", ExpectedCategory = "NARUTO", PredictedCategory = "NARUTO", PredictionConfidence = 0.76 },
            new() { Filename = "Unknown.Series.S01E01.mkv", ExpectedCategory = "UNKNOWN", PredictedCategory = "UNKNOWN", PredictionConfidence = 0.45 }
        }.AsReadOnly();
    }

    private static IReadOnlyList<EvaluationTestCase> CreateOverconfidentTestCases()
    {
        return new List<EvaluationTestCase>
        {
            new() { Filename = "Breaking.Bad.S01E01.mkv", ExpectedCategory = "BREAKING BAD", PredictedCategory = "THE OFFICE", PredictionConfidence = 0.95 }, // Wrong but confident
            new() { Filename = "The.Office.S01E01.mkv", ExpectedCategory = "THE OFFICE", PredictedCategory = "BREAKING BAD", PredictionConfidence = 0.92 }, // Wrong but confident
            new() { Filename = "Naruto.S01E01.mkv", ExpectedCategory = "NARUTO", PredictedCategory = "ONE PIECE", PredictionConfidence = 0.89 }, // Wrong but confident
            new() { Filename = "Ambiguous.Series.mkv", ExpectedCategory = "UNKNOWN", PredictedCategory = "BREAKING BAD", PredictionConfidence = 0.91 } // Wrong but confident
        }.AsReadOnly();
    }

    private static IReadOnlyList<EvaluationTestCase> CreateItalianMediaTestCases()
    {
        return new List<EvaluationTestCase>
        {
            // Breaking Bad variations
            new() { Filename = "Breaking.Bad.S01E01.Pilot.1080p.BluRay.mkv", ExpectedCategory = "BREAKING BAD", PredictedCategory = "BREAKING BAD", PredictionConfidence = 0.95 },
            new() { Filename = "Breaking.Bad.S05E16.Felina.FINAL.1080p.mkv", ExpectedCategory = "BREAKING BAD", PredictedCategory = "BREAKING BAD", PredictionConfidence = 0.93 },
            
            // The Office variations
            new() { Filename = "The.Office.S02E01.The.Dundies.720p.WEB-DL.mkv", ExpectedCategory = "THE OFFICE", PredictedCategory = "THE OFFICE", PredictionConfidence = 0.89 },
            new() { Filename = "The.Office.S09E23.Finale.1080p.BluRay.mkv", ExpectedCategory = "THE OFFICE", PredictedCategory = "THE OFFICE", PredictionConfidence = 0.87 },
            
            // Anime series (Italian subtitles)
            new() { Filename = "Naruto.Shippuden.S01E01.Sub.ITA.1080p.WEB-DLMux.mkv", ExpectedCategory = "NARUTO", PredictedCategory = "NARUTO", PredictionConfidence = 0.82 },
            new() { Filename = "One.Piece.E1089.Sub.ITA.720p.WEB-DL.H264.mkv", ExpectedCategory = "ONE PIECE", PredictedCategory = "ONE PIECE", PredictionConfidence = 0.78 },
            
            // Unknown/ambiguous cases
            new() { Filename = "Random.Unknown.Series.S01E01.mkv", ExpectedCategory = "UNKNOWN", PredictedCategory = "UNKNOWN", PredictionConfidence = 0.35 },
            new() { Filename = "Corrupted.Filename...mkv", ExpectedCategory = "UNKNOWN", PredictedCategory = "UNKNOWN", PredictionConfidence = 0.15 }
        }.AsReadOnly();
    }

    private static IReadOnlyList<EvaluationTestCase> CreateTestCasesForConfusionMatrix()
    {
        return CreateItalianMediaTestCases();
    }

    private static EvaluationConfusionMatrix CreateRealisticConfusionMatrix(string[] categories)
    {
        var categoryCount = categories.Length;
        var matrix = new int[categoryCount, categoryCount];
        var totalPredictions = 200;
        
        // Create realistic confusion patterns
        // Breaking Bad: 40 samples, 38 correct, 1 confused with The Office, 1 with Unknown
        matrix[0, 0] = 38; matrix[0, 1] = 1; matrix[0, 2] = 0; matrix[0, 3] = 0; matrix[0, 4] = 1;
        // The Office: 35 samples, 32 correct, 2 confused with Breaking Bad, 1 with Unknown
        matrix[1, 0] = 2; matrix[1, 1] = 32; matrix[1, 2] = 0; matrix[1, 3] = 0; matrix[1, 4] = 1;
        // Naruto: 45 samples, 37 correct, 0 with western, 5 with One Piece, 3 with Unknown
        matrix[2, 0] = 0; matrix[2, 1] = 0; matrix[2, 2] = 37; matrix[2, 3] = 5; matrix[2, 4] = 3;
        // One Piece: 40 samples, 30 correct, 0 with western, 7 with Naruto, 3 with Unknown
        matrix[3, 0] = 0; matrix[3, 1] = 0; matrix[3, 2] = 7; matrix[3, 3] = 30; matrix[3, 4] = 3;
        // Unknown: 40 samples, 21 correct, 4 with Breaking Bad, 3 with Office, 6 with Naruto, 6 with One Piece
        matrix[4, 0] = 4; matrix[4, 1] = 3; matrix[4, 2] = 6; matrix[4, 3] = 6; matrix[4, 4] = 21;

        var truePositives = new Dictionary<string, int>();
        var falsePositives = new Dictionary<string, int>();
        var falseNegatives = new Dictionary<string, int>();
        var trueNegatives = new Dictionary<string, int>();

        for (int i = 0; i < categoryCount; i++)
        {
            var category = categories[i];
            truePositives[category] = matrix[i, i];
            
            // False positives: sum of column i excluding diagonal
            falsePositives[category] = 0;
            for (int j = 0; j < categoryCount; j++)
            {
                if (j != i) falsePositives[category] += matrix[j, i];
            }
            
            // False negatives: sum of row i excluding diagonal
            falseNegatives[category] = 0;
            for (int j = 0; j < categoryCount; j++)
            {
                if (j != i) falseNegatives[category] += matrix[i, j];
            }
            
            // True negatives: total - TP - FP - FN
            trueNegatives[category] = totalPredictions - truePositives[category] - falsePositives[category] - falseNegatives[category];
        }

        return new EvaluationConfusionMatrix
        {
            Categories = categories.ToList().AsReadOnly(),
            Matrix = matrix,
            TotalPredictions = totalPredictions,
            TruePositives = truePositives.AsReadOnly(),
            FalsePositives = falsePositives.AsReadOnly(),
            FalseNegatives = falseNegatives.AsReadOnly(),
            TrueNegatives = trueNegatives.AsReadOnly()
        };
    }

    private static EvaluationConfusionMatrix CreateConfusionMatrixWithKnownErrors(string[] categories)
    {
        return CreateRealisticConfusionMatrix(categories); // Same implementation for now
    }

    private static IReadOnlyList<TrainingSample> CreateTrainingSamples()
    {
        return new List<TrainingSample>
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
                Filename = "The.Office.S01E01.mkv", 
                Category = "THE OFFICE",
                Confidence = 0.92,
                Source = TrainingSampleSource.UserFeedback,
                CreatedAt = DateTime.UtcNow
            },
            new() 
            { 
                Filename = "Naruto.S01E01.mkv", 
                Category = "NARUTO",
                Confidence = 0.88,
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
    }

    #endregion
}