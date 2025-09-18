// MediaButler Web Application JavaScript Functions

// File download functionality for export features
window.downloadFile = (base64Data, filename, mimeType) => {
    try {
        // Convert base64 to blob
        const byteCharacters = atob(base64Data);
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        const blob = new Blob([byteArray], { type: mimeType });

        // Create download link
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        
        // Trigger download
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        
        // Clean up
        window.URL.revokeObjectURL(url);
    } catch (error) {
        console.error('Error downloading file:', error);
        throw error;
    }
};

// Theme management
window.setTheme = (theme) => {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('mediabutler-theme', theme);
};

window.getTheme = () => {
    return localStorage.getItem('mediabutler-theme') || 'light';
};

// Initialize theme on page load
document.addEventListener('DOMContentLoaded', () => {
    const savedTheme = window.getTheme();
    window.setTheme(savedTheme);
});

// Copy to clipboard functionality
window.copyToClipboard = async (text) => {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch (error) {
        // Fallback for older browsers
        const textArea = document.createElement('textarea');
        textArea.value = text;
        document.body.appendChild(textArea);
        textArea.select();
        try {
            document.execCommand('copy');
            return true;
        } catch (fallbackError) {
            console.error('Failed to copy text:', fallbackError);
            return false;
        } finally {
            document.body.removeChild(textArea);
        }
    }
};

// Auto-resize textarea
window.autoResizeTextarea = (element) => {
    element.style.height = 'auto';
    element.style.height = element.scrollHeight + 'px';
};

// Scroll to element with smooth animation
window.scrollToElement = (elementId, offset = 0) => {
    const element = document.getElementById(elementId);
    if (element) {
        const elementPosition = element.offsetTop - offset;
        window.scrollTo({
            top: elementPosition,
            behavior: 'smooth'
        });
    }
};

// Local storage helpers with error handling
window.setLocalStorage = (key, value) => {
    try {
        localStorage.setItem(key, JSON.stringify(value));
        return true;
    } catch (error) {
        console.error('Failed to save to localStorage:', error);
        return false;
    }
};

window.getLocalStorage = (key, defaultValue = null) => {
    try {
        const item = localStorage.getItem(key);
        return item ? JSON.parse(item) : defaultValue;
    } catch (error) {
        console.error('Failed to read from localStorage:', error);
        return defaultValue;
    }
};

// Debounce function for search inputs
window.debounce = (func, delay) => {
    let timeoutId;
    return (...args) => {
        clearTimeout(timeoutId);
        timeoutId = setTimeout(() => func.apply(null, args), delay);
    };
};

// Format file size display
window.formatFileSize = (bytes) => {
    if (bytes === 0) return '0 Bytes';
    
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
};

// Show toast notification
window.showToast = (message, type = 'info', duration = 3000) => {
    // Create toast element
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.textContent = message;
    
    // Add to page
    const toastContainer = document.querySelector('.toast-container') || document.body;
    toastContainer.appendChild(toast);
    
    // Animate in
    setTimeout(() => toast.classList.add('show'), 100);
    
    // Remove after duration
    setTimeout(() => {
        toast.classList.remove('show');
        setTimeout(() => toastContainer.removeChild(toast), 300);
    }, duration);
};

// File drag and drop helpers
window.initFileDrop = (elementId, onFilesDropped) => {
    const element = document.getElementById(elementId);
    if (!element) return;
    
    element.addEventListener('dragover', (e) => {
        e.preventDefault();
        element.classList.add('drag-over');
    });
    
    element.addEventListener('dragleave', (e) => {
        e.preventDefault();
        element.classList.remove('drag-over');
    });
    
    element.addEventListener('drop', (e) => {
        e.preventDefault();
        element.classList.remove('drag-over');
        
        const files = Array.from(e.dataTransfer.files);
        if (files.length > 0) {
            onFilesDropped(files);
        }
    });
};

// Keyboard shortcut handler
window.addKeyboardShortcut = (combination, callback) => {
    document.addEventListener('keydown', (e) => {
        const keys = combination.toLowerCase().split('+');
        const pressedKeys = [];
        
        if (e.ctrlKey || e.metaKey) pressedKeys.push('ctrl');
        if (e.shiftKey) pressedKeys.push('shift');
        if (e.altKey) pressedKeys.push('alt');
        pressedKeys.push(e.key.toLowerCase());
        
        if (keys.every(key => pressedKeys.includes(key)) && keys.length === pressedKeys.length) {
            e.preventDefault();
            callback();
        }
    });
};