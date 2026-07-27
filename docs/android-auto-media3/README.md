# Android Auto / Bluetooth → Media3 (branch `android-auto-media3`)

Documentazione della ricostruzione del livello audio Android: migrazione da
`CommunityToolkit.Maui.MediaElement` ad **AndroidX Media3 / ExoPlayer** con una
singola `MediaLibraryService`, per far funzionare Android Auto, i comandi
Bluetooth/AVRCP e la riproduzione in background.

Documentation of the Android audio layer rebuild: migration from
`CommunityToolkit.Maui.MediaElement` to **AndroidX Media3 / ExoPlayer** with a
single `MediaLibraryService`, so that Android Auto, Bluetooth/AVRCP controls and
background playback work.

## Ordine di lettura / Reading order

| # | Documento / Document | IT | EN |
|---|---|---|---|
| 1 | Analisi tecnica del problema (stato v0.20) — *technical analysis of the problem* | [ResumeRadioE45_IT.md](ResumeRadioE45_IT.md) | [ResumeRadioE45_EN.md](ResumeRadioE45_EN.md) |
| 2 | Report della ricostruzione — *rebuild report: what changed and why* | [RefactorReport_RadioE45_IT.md](RefactorReport_RadioE45_IT.md) | [RefactorReport_RadioE45_EN.md](RefactorReport_RadioE45_EN.md) |

## Note

- Il documento precedente [`../carplay/android-auto-carplay-implementation.md`](../carplay/android-auto-carplay-implementation.md)
  descrive l'implementazione originale (pre-Media3) e resta come riferimento storico:
  alcune sue conclusioni sono superate dall'analisi al punto 1.
- *The earlier document `../carplay/android-auto-carplay-implementation.md` describes the
  original (pre-Media3) implementation and is kept for historical reference; some of its
  conclusions are superseded by the analysis in step 1.*
