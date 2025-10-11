using Microsoft.Extensions.Configuration;
using MediaButler.Core.Configuration;

namespace MediaButler.Services.Configuration;

/// <summary>
/// Implementation of centralized configuration for MediaButler application settings.
/// Reads from IConfiguration (appsettings.json) with sensible defaults.
/// Following "Simple Made Easy" principles - single source of truth for configuration.
/// </summary>
public class MediaButlerConfiguration : IMediaButlerConfiguration
{
    private readonly IConfiguration _config;

    public MediaButlerConfiguration(IConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public string MediaLibraryPath =>
        _config["MediaButler:Paths:MediaLibrary"] ?? "/library";

    public string WatchFolderPath =>
        _config["MediaButler:FileDiscovery:WatchFolders:0"] ?? "../../temp/watch";

    public string PendingReviewPath =>
        _config["MediaButler:Paths:PendingReview"] ?? "/tmp/mediabutler/pending";

    public int MaxRetryCount =>
        _config.GetValue<int>("MediaButler:FileProcessing:MaxRetryCount", 3);

    public decimal AutoClassifyThreshold =>
        _config.GetValue<decimal>("MediaButler:ML:AutoClassifyThreshold", 0.85m);

    public decimal SuggestionThreshold =>
        _config.GetValue<decimal>("MediaButler:ML:SuggestionThreshold", 0.50m);

    public int MaxClassificationTimeMs =>
        _config.GetValue<int>("MediaButler:ML:MaxClassificationTimeMs", 500);

    public int MaxBatchSize =>
        _config.GetValue<int>("MediaButler:ML:MaxBatchSize", 50);
}
