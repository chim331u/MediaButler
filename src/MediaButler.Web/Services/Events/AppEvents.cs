using MediaButler.Core.Entities;

namespace MediaButler.Web.Services.Events;

/// <summary>
/// Application events following "Simple Made Easy" principles.
/// Pure data structures representing things that happened.
/// </summary>
public abstract record AppEvent
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

// File-related events
public record FileDiscoveredEvent(TrackedFile File) : AppEvent;
public record FileClassifiedEvent(string FileHash, string Category, decimal Confidence) : AppEvent;
public record FileMovedEvent(string FileHash, string NewPath) : AppEvent;
public record FileErrorEvent(string FileHash, string Error) : AppEvent;

// System events
public record ScanStartedEvent(string[] Paths) : AppEvent;
public record ScanCompletedEvent(int FilesFound) : AppEvent;
public record SystemErrorEvent(string Error, Exception? Exception = null) : AppEvent;

// UI events
public record NavigationEvent(string Page) : AppEvent;
public record RefreshRequestedEvent(string Component) : AppEvent;
public record UserActionEvent(string Action, string? Target = null) : AppEvent;