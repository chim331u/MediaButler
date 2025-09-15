namespace MediaButler.Web.Services.Icons;

/// <summary>
/// Simple icon service using emoji and text-based icons.
/// Following "Simple Made Easy" - no external dependencies, just clean mappings.
/// </summary>
public interface IIconService
{
    string GetIcon(string iconName);
    string GetStatusIcon(string status);
    string GetFileTypeIcon(string extension);
    string GetActionIcon(string action);
}

public class IconService : IIconService
{
    private readonly Dictionary<string, string> _iconMappings = new()
    {
        // Common UI icons
        ["dashboard"] = "🏠",
        ["files"] = "📁",
        ["pending"] = "⏳",
        ["statistics"] = "📊",
        ["settings"] = "⚙️",
        ["refresh"] = "🔄",
        ["search"] = "🔍",
        ["filter"] = "🔽",
        ["sort"] = "🔄",
        ["menu"] = "☰",
        ["close"] = "✕",
        ["expand"] = "▼",
        ["collapse"] = "▲",
        ["next"] = "▶",
        ["previous"] = "◀",
        
        // Actions
        ["confirm"] = "✓",
        ["reject"] = "✗",
        ["delete"] = "🗑️",
        ["edit"] = "✏️",
        ["view"] = "👁️",
        ["download"] = "⬇️",
        ["upload"] = "⬆️",
        ["move"] = "📤",
        ["copy"] = "📋",
        ["save"] = "💾",
        ["cancel"] = "❌",
        ["add"] = "➕",
        ["remove"] = "➖",
        
        // Status indicators
        ["success"] = "✅",
        ["error"] = "❌",
        ["warning"] = "⚠️",
        ["info"] = "ℹ️",
        ["loading"] = "⏳",
        ["processing"] = "🔄",
        
        // File types
        ["video"] = "🎬",
        ["audio"] = "🎵",
        ["image"] = "🖼️",
        ["document"] = "📄",
        ["archive"] = "📦",
        ["subtitle"] = "📝",
        ["unknown"] = "📄",
        
        // System
        ["health"] = "💚",
        ["performance"] = "⚡",
        ["memory"] = "🧠",
        ["storage"] = "💾",
        ["network"] = "🌐",
        ["cpu"] = "⚡",
        
        // Navigation
        ["home"] = "🏠",
        ["back"] = "← ",
        ["forward"] = "→ ",
        ["up"] = "↑",
        ["down"] = "↓"
    };

    private readonly Dictionary<string, string> _statusIcons = new()
    {
        ["discovered"] = "🔍",
        ["new"] = "🔍", 
        ["classified"] = "📊",
        ["pending"] = "⏳",
        ["confirmed"] = "✅",
        ["ready"] = "✅",
        ["moved"] = "📁",
        ["complete"] = "✅",
        ["error"] = "❌",
        ["failed"] = "❌",
        ["processing"] = "⏳",
        ["inprogress"] = "⏳",
        ["retry"] = "🔄"
    };

    private readonly Dictionary<string, string> _fileTypeIcons = new()
    {
        [".mkv"] = "🎬",
        [".mp4"] = "🎬",
        [".avi"] = "🎬",
        [".mov"] = "🎬",
        [".wmv"] = "🎬",
        [".m4v"] = "🎬",
        [".webm"] = "🎬",
        [".srt"] = "📝",
        [".sub"] = "📝",
        [".ass"] = "📝",
        [".vtt"] = "📝",
        [".nfo"] = "📄",
        [".txt"] = "📄",
        [".jpg"] = "🖼️",
        [".jpeg"] = "🖼️",
        [".png"] = "🖼️",
        [".gif"] = "🖼️",
        [".mp3"] = "🎵",
        [".flac"] = "🎵",
        [".wav"] = "🎵",
        [".zip"] = "📦",
        [".rar"] = "📦",
        [".7z"] = "📦"
    };

    private readonly Dictionary<string, string> _actionIcons = new()
    {
        ["confirm"] = "✓",
        ["reject"] = "✗",
        ["delete"] = "🗑️",
        ["move"] = "📤",
        ["view"] = "👁️",
        ["edit"] = "✏️",
        ["refresh"] = "🔄",
        ["scan"] = "🔍",
        ["train"] = "🎯",
        ["classify"] = "📊",
        ["approve"] = "✅",
        ["cancel"] = "❌",
        ["retry"] = "🔄"
    };

    public string GetIcon(string iconName)
    {
        return _iconMappings.TryGetValue(iconName.ToLowerInvariant(), out var icon) 
            ? icon 
            : "📄";
    }

    public string GetStatusIcon(string status)
    {
        return _statusIcons.TryGetValue(status.ToLowerInvariant(), out var icon) 
            ? icon 
            : "📄";
    }

    public string GetFileTypeIcon(string extension)
    {
        return _fileTypeIcons.TryGetValue(extension.ToLowerInvariant(), out var icon) 
            ? icon 
            : "📄";
    }

    public string GetActionIcon(string action)
    {
        return _actionIcons.TryGetValue(action.ToLowerInvariant(), out var icon) 
            ? icon 
            : "▶";
    }
}