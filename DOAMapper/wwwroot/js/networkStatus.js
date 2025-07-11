let dotNetHelper;

export function initializeNetworkStatus(dotNetObjectReference) {
    dotNetHelper = dotNetObjectReference;
    
    // Initial status
    updateNetworkStatus();
    
    // Listen for network changes
    window.addEventListener('online', updateNetworkStatus);
    window.addEventListener('offline', updateNetworkStatus);
}

function updateNetworkStatus() {
    if (dotNetHelper) {
        dotNetHelper.invokeMethodAsync('OnNetworkStatusChanged', navigator.onLine);
    }
}

export function dispose() {
    window.removeEventListener('online', updateNetworkStatus);
    window.removeEventListener('offline', updateNetworkStatus);
    dotNetHelper = null;
}
