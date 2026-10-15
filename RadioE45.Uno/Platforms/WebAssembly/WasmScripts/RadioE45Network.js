// Network state for BrowserNetworkMonitor (RadioE45.Uno/Services/Browser) via JSImport.
globalThis.RadioE45Network = {
    isOnline: () => navigator.onLine,
    subscribe: (callback) => {
        window.addEventListener("online", () => callback(true));
        window.addEventListener("offline", () => callback(false));
    }
};
