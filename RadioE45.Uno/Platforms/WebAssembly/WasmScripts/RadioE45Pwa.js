// Service worker of the installed web app (PWA, S9b).
// 1. The Uno bootstrapper registers its own service-worker.js, which answers every request with
//    fetch() from the worker: Safari then refuses the Radio E45 stream (direct Icecast port).
//    The app registers radioe45-sw.js instead (wwwroot), which loads the Uno worker after a
//    listener that leaves streams and other origins to the browser. Embedded scripts load before
//    the bootstrapper registers its worker, so its call is redirected here; registering at every
//    start also replaces a Uno worker installed before this fix.
// 2. The Uno worker precaches the bootstrapper's offline file list, which lacks uno-assets.txt
//    (written later by Uno.UI). Offline, Uno's AssetsManager then finds no asset (fonts, images)
//    and the app stays on the splash screen. Once the worker is active, the file is added to its
//    caches; at every start, because a new worker (new build) starts from a fresh cache.
(function () {
    if (!('serviceWorker' in navigator) || !('caches' in globalThis)) {
        return;
    }

    const packageScript = document.querySelector('script[src*="/package_"]');
    if (!packageScript) {
        return;
    }
    const appBase = new URL('../', packageScript.src);
    // Browsers do not reliably notice a change in the modules a worker imports (the Uno worker
    // changes at every build): the package folder (package_<hash>) in the URL makes every build
    // a new worker script.
    const packageName = new URL('./', packageScript.src).pathname.split('/').filter(Boolean).pop();
    const workerUrl = new URL('radioe45-sw.js?v=' + packageName, appBase).href;
    const assetsUrl = new URL('uno-assets.txt', packageScript.src).href;

    const container = navigator.serviceWorker;
    const register = container.register.bind(container);
    container.register = (url, options) => String(url).endsWith('/service-worker.js')
        ? register(workerUrl, Object.assign({}, options, { type: 'module', updateViaCache: 'none' }))
        : register(url, options);
    register(workerUrl, { scope: appBase.pathname, type: 'module', updateViaCache: 'none' })
        .catch((e) => console.warn('[RadioE45Pwa] service worker not registered: ' + e));

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

    container.ready.then(cacheAssetList);
    container.addEventListener('controllerchange', cacheAssetList);
})();
