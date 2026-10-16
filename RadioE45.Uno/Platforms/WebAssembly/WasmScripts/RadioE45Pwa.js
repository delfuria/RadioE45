// Offline start of the installed web app (PWA, S9b).
// The Uno service worker precaches the bootstrapper's offline file list, which lacks
// uno-assets.txt (written later by Uno.UI). Offline, Uno's AssetsManager then finds no asset
// (fonts, images) and the app stays on the splash screen. Once the worker is active, the file is
// added to its caches; the worker answers from them when the network fails. Runs at every start
// because a new worker (new build) starts from a fresh cache.
(function () {
    if (!('serviceWorker' in navigator) || !('caches' in globalThis)) {
        return;
    }

    const packageScript = document.querySelector('script[src*="/package_"]');
    if (!packageScript) {
        return;
    }
    const assetsUrl = new URL('uno-assets.txt', packageScript.src).href;

    async function cacheAssetList() {
        try {
            const names = await caches.keys();
            const missing = [];
            for (const name of names) {
                const cache = await caches.open(name);
                if (!(await cache.match(assetsUrl))) {
                    missing.push(cache);
                }
            }
            if (missing.length === 0) {
                return;
            }
            const response = await fetch(assetsUrl, { cache: 'no-cache' });
            if (!response.ok) {
                return;
            }
            for (const cache of missing) {
                await cache.put(assetsUrl, response.clone());
            }
        } catch (e) {
            // Offline or storage unavailable: nothing to add now, retried at the next start.
            console.debug('[RadioE45Pwa] uno-assets.txt not cached: ' + e);
        }
    }

    navigator.serviceWorker.ready.then(cacheAssetList);
    navigator.serviceWorker.addEventListener('controllerchange', cacheAssetList);
})();
