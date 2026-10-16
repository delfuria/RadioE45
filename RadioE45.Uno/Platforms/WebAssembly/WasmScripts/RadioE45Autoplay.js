// Autoplay state for BrowserUserActivation (RadioE45.Uno/Services/Browser) via JSImport.
// Browsers allow play() without a gesture once the user has interacted with the page (sticky
// user activation); before that it is rejected.
globalThis.RadioE45Autoplay = {
    hasBeenActive: () => navigator.userActivation ? navigator.userActivation.hasBeenActive : false
};
