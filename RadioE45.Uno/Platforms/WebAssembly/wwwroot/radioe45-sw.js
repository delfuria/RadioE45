// Service worker of the web app (PWA, S9b), registered by WasmScripts/RadioE45Pwa.js in place of
// the Uno one. Module imports run in order: the bypass listener comes before the Uno worker's
// listeners (precache, network first, offline fallback for the app's own files).
import './radioe45-sw-bypass.js';
import './service-worker.js';
