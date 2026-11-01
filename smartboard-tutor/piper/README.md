Phonemisierung für die Piper-Stimme im Browser.

- phonemize.worker.js, piper_phonemize.wasm: aus dem npm-Paket piper-tts-web 1.1.2 (MIT, Jonas Plamann), basierend auf piper-phonemize und espeak-ng (GPL-3.0).
- piper_phonemize_data.wasm: gekürztes espeak-ng-Datenpaket (nur de, en, fr, es, it, tr, nl, pt, pl, sv), wegen der Dateityp-Prüfung des Artifact-Dienstes unter der Endung .wasm abgelegt.
- ../ort: onnxruntime-web 1.17.3 (MIT).
