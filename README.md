# CROWNFRONT

CROWNFRONT is a casual mobile formation-defense game built around tactical unit
placement, direct control, persistent progression, and tiered augments. The
production game is a native Unity Android project.

## Repository layout

- `unity-jelly-gate/` — Unity 6 production project and Android release tooling
- `android-app/` — archived Android/WebView prototype kept for reference
- `tools/` — top-level QA runners for release verification

Generated Unity caches, Gradle caches, APK/AAB files, QA captures, build logs,
and signing keys are intentionally excluded from Git.

## Unity requirements

- Unity `6000.0.34f1`
- Android Build Support, SDK/NDK Tools, and OpenJDK
- The Android package is `com.toykingdom.jellygate`
- Current public version name is `1.00`

Open `unity-jelly-gate/` in Unity Hub. Detailed build and Google Play setup are
documented in:

- `unity-jelly-gate/README.md`
- `unity-jelly-gate/GOOGLE_PLAY_SETUP.md`

Release signing secrets must be supplied through environment variables and
must never be committed. Local upload keystores belong under `release-keys/`.

## Version-control policy

- Commit source assets together with their Unity `.meta` files.
- Do not commit `Library/`, `Temp/`, `Logs/`, `obj/`, Gradle build folders, or
  generated Android packages.
- Keep production credentials and signing material outside the repository.
