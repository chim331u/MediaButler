window.sseInterop = {
    eventSource: null,
    dotnetHelper: null,

    start: function (url, dotnetHelper) {
        console.log("[SSE-JS] Starting connection to:", url);
        if (this.eventSource) {
            console.log("[SSE-JS] Closing existing connection before restart");
            this.eventSource.close();
        }

        this.dotnetHelper = dotnetHelper;
        this.eventSource = new EventSource(url);

        this.eventSource.onopen = function () {
            console.log("[SSE-JS] Connection Opened (onopen)");
            dotnetHelper.invokeMethodAsync('OnConnected');
        };

        this.eventSource.onerror = function (error) {
            console.error("[SSE-JS] Connection Error:", error);
            console.log("[SSE-JS] ReadyState:", this.readyState);

            // Check if readyState is CLOSED (2)
            if (this.readyState === 2) {
                console.log("[SSE-JS] State is CLOSED, invoking OnDisconnected");
                dotnetHelper.invokeMethodAsync('OnDisconnected');
            } else {
                console.log("[SSE-JS] State is CONNECTING (0) or OPEN (1), invoking OnError");
                dotnetHelper.invokeMethodAsync('OnError', "EventSource error");
            }
        };

        // Generic event listener?
        // EventSource requires explicit listeners for named events.
        // We will register a list of known events.
        const events = [
            "BatchStarted", "BatchProgress", "BatchCompleted", "BatchFailed", "Error",
            "MoveFileNotification", "FileDiscoveryNotification", "ClassificationNotification",
            "JobProgressNotification", "SystemStatusNotification", "ErrorNotification",
            "HealthCheckPing", // Added for Health Check
            "connected" // Initial connection event
        ];

        events.forEach(eventName => {
            this.eventSource.addEventListener(eventName, function (e) {
                console.log("[SSE-JS] Received event:", eventName, e.data);
                dotnetHelper.invokeMethodAsync('OnMessage', eventName, e.data);
            });
        });
    },

    stop: function () {
        if (this.eventSource) {
            this.eventSource.close();
            this.eventSource = null;
        }
    }
};
