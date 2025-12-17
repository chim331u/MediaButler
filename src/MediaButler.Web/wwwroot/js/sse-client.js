// sse-client.js - Server-Sent Events client for MediaButler Web UI

let eventSource = null;
let dotNetHelper = null;
let reconnectAttempts = 0;
const MAX_RECONNECT_ATTEMPTS = 5;
const RECONNECT_DELAY = 3000; // 3 seconds base delay

/**
 * Connects to the SSE endpoint and sets up event listeners
 * @param {string} url - The SSE endpoint URL
 * @param {any} dotNetRef - .NET object reference for callbacks
 */
export function connectSSE(url, dotNetRef) {
    console.log('[SSE Client] Connecting to:', url);
    dotNetHelper = dotNetRef;

    // Close existing connection if any
    if (eventSource) {
        eventSource.close();
    }

    // Create new EventSource
    eventSource = new EventSource(url);

    // Connection opened
    eventSource.onopen = () => {
        console.log('[SSE Client] Connection opened successfully');
        reconnectAttempts = 0;

        // Notify .NET of successful connection
        dotNetHelper.invokeMethodAsync('HandleConnectionStateChanged', 'Connected');
    };

    // Connection error
    eventSource.onerror = (error) => {
        console.error('[SSE Client] Connection error:', error);

        if (eventSource.readyState === EventSource.CLOSED) {
            console.log('[SSE Client] Connection closed, attempting reconnect...');
            dotNetHelper.invokeMethodAsync('HandleConnectionStateChanged', 'Disconnected');
            attemptReconnect(url);
        } else if (eventSource.readyState === EventSource.CONNECTING) {
            console.log('[SSE Client] Reconnecting...');
            dotNetHelper.invokeMethodAsync('HandleConnectionStateChanged', 'Reconnecting');
        }
    };

    // Register event listeners for all supported event types
    const eventTypes = [
        // Scan events
        'scan.started', 'scan.found', 'scan.completed',
        // Move events
        'move.started', 'move.progress', 'move.completed',
        // Training events
        'training.started', 'training.completed',
        // Batch events
        'batch.started', 'batch.progress', 'batch.completed', 'batch.failed',
        // Error events
        'error.move_failed', 'error.classification_failed',
        // Connection events
        'connected'
    ];

    eventTypes.forEach(eventType => {
        eventSource.addEventListener(eventType, (event) => {
            console.log(`[SSE Client] Received event: ${eventType}`, event.data);

            try {
                // Pass event to .NET handler
                dotNetHelper.invokeMethodAsync('HandleSseEvent', eventType, event.data);
            } catch (err) {
                console.error(`[SSE Client] Error handling event ${eventType}:`, err);
            }
        });
    });

    // Handle generic messages (fallback)
    eventSource.onmessage = (event) => {
        console.log('[SSE Client] Generic message received:', event.data);
    };
}

/**
 * Attempts to reconnect to the SSE endpoint with exponential backoff
 * @param {string} url - The SSE endpoint URL
 */
function attemptReconnect(url) {
    if (reconnectAttempts >= MAX_RECONNECT_ATTEMPTS) {
        console.error('[SSE Client] Max reconnection attempts reached');
        dotNetHelper.invokeMethodAsync('HandleConnectionStateChanged', 'Failed');
        return;
    }

    reconnectAttempts++;
    const delay = RECONNECT_DELAY * reconnectAttempts; // Exponential backoff

    console.log(`[SSE Client] Reconnect attempt ${reconnectAttempts}/${MAX_RECONNECT_ATTEMPTS} in ${delay}ms...`);

    setTimeout(() => {
        connectSSE(url, dotNetHelper);
    }, delay);
}

/**
 * Disconnects from the SSE endpoint
 */
export function disconnectSSE() {
    console.log('[SSE Client] Disconnecting...');

    if (eventSource) {
        eventSource.close();
        eventSource = null;
    }

    if (dotNetHelper) {
        dotNetHelper.invokeMethodAsync('HandleConnectionStateChanged', 'Disconnected');
    }
}

/**
 * Returns the current connection state
 * @returns {string} Connection state: 'Connecting', 'Open', 'Closed'
 */
export function getConnectionState() {
    if (!eventSource) {
        return 'Closed';
    }

    switch (eventSource.readyState) {
        case EventSource.CONNECTING:
            return 'Connecting';
        case EventSource.OPEN:
            return 'Open';
        case EventSource.CLOSED:
            return 'Closed';
        default:
            return 'Unknown';
    }
}

// Export for debugging in browser console
window.sseClient = {
    connect: connectSSE,
    disconnect: disconnectSSE,
    getState: getConnectionState
};
