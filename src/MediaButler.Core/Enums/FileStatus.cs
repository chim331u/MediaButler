namespace MediaButler.Core.Enums;

/// <summary>
/// Represents the current processing status of a tracked file in the MediaButler system.
/// This enum defines clear state transitions following "Simple Made Easy" principles 
/// with explicit, single-purpose states that don't complect multiple concerns.
/// </summary>
/// <remarks>
/// The typical workflow follows this progression:
/// New → Processing → Classified → ReadyToMove → Moving → Moved
/// 
/// Error states (Error, Retry) can occur at any point and may transition back to Processing.
/// The Ignored state is a terminal state for files the user chooses to skip.
/// </remarks>
public enum FileStatus
{
    // Summary:
    // New = 0,           // Just discovered
    // Processing = 1,    // Being processed
    // Classified = 2,    // ML classification complete
    // ReadyToMove = 3,   // Confirmed, ready for organization
    // Moving = 4,        // File move in progress
    // Moved = 5,         // Successfully organized
    // Error = 6,         // Processing failed
    // Retry = 7,         // Queued for retry
    // Ignored = 8        // User marked as ignored
    
    /// <summary>
    /// File has been discovered but not yet processed.
    /// This is the initial state when a file is first detected by the file watcher.
    /// </summary>
    New = 0,

    /// <summary>
    /// File is currently being analyzed (hash calculation, ML classification, etc.).
    /// This state indicates active processing is occurring.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// ML classification has been completed and results are available.
    /// The file has a suggested category and confidence score but awaits user confirmation.
    /// </summary>
    Classified = 2,

    /// <summary>
    /// User has confirmed the category and the file is ready to be moved.
    /// This state indicates user approval has been received and file operation can proceed.
    /// </summary>
    ReadyToMove = 3,

    /// <summary>
    /// File is currently being moved to its target location.
    /// This state indicates an active file system operation is in progress.
    /// </summary>
    Moving = 4,

    /// <summary>
    /// File has been successfully moved to its final organized location.
    /// This is a terminal success state indicating the workflow is complete.
    /// </summary>
    Moved = 5,

    /// <summary>
    /// An error occurred during processing that requires attention.
    /// The file may be queued for retry or require manual intervention.
    /// </summary>
    Error = 6,

    /// <summary>
    /// File is queued for retry after a previous error.
    /// The system will attempt to process the file again based on retry policy.
    /// </summary>
    Retry = 7,

    /// <summary>
    /// User has chosen to skip this file permanently.
    /// This is a terminal state - the file will not be processed further.
    /// </summary>
    Ignored = 8
}