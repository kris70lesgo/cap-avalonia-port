# Cap Avalonia

A cross-platform Avalonia port of Cap's desktop app. It recreates the compact recorder window and all 13 Settings pages, with local Recordings and Screenshots libraries. The local workflows include display recording, screenshot capture, selectable display and crop areas, camera and microphone inputs on macOS, video trimming/export, screenshot annotation with pen, arrows, rectangles, highlights, and text, a scrolling teleprompter, unread activity, media import, clipboard copy, deletion to the system Trash, configurable focused-app shortcuts, and local backup automations. Recorder mode, capture devices, library paths, trim points, teleprompter text, shortcuts, and settings persist between launches.

This build does not include Cap's online services or its full Studio editor. Instant mode is local-only; there is no account sign-in, upload, or share-link generation. Camera and microphone device selection currently uses FFmpeg AVFoundation on macOS. System audio requires an installed loopback device (for example, BlackHole). Window capture selects a screen rectangle, so overlapping windows remain visible. The video editor supports preview, trim points, and MP4 export; Cap's captions, transitions, cursor effects, and multi-track editing are not included. Screenshot annotations can be exported as a new PNG. Shortcuts work while the app is focused; system-wide hotkeys, Cap's CLI, cloud integrations, automatic updates, and online license services are not connected. A visible stop control stays on screen during capture; depending on the selected display and capture backend, that control may appear in the recording.

The reference application is [Cap](https://github.com/CapSoftware/Cap), an open-source screen recorder by Cap Software. This project adapts its desktop interface under the AGPL-3.0 license. See [LICENSE](LICENSE) for the license and the upstream copyright notice.

## Run

Requires the .NET 9 SDK or newer.

```sh
dotnet run
```

The UI is built with Avalonia 12.1.3 and targets macOS, Windows, and Linux.

To build a self-contained macOS app bundle on Apple Silicon, run `scripts/package-macos.sh`. It creates `.build/CapAvalonia-final.app`; open it with `open .build/CapAvalonia-final.app`. The script accepts an optional runtime identifier and output bundle path, for example `scripts/package-macos.sh osx-arm64 "$PWD/.build/CapAvalonia-preview.app"`. A self-contained bundle still expects FFmpeg tools on `PATH` for capture and video editing.

Recording and video editing require FFmpeg installed and available on `PATH`. Video preview uses `ffplay`, and clip inspection uses `ffprobe`; both ship with standard FFmpeg packages. macOS full-display screenshots use the system `screencapture` utility; area screenshots and Windows/Linux screenshots use FFmpeg. macOS camera/microphone capture uses FFmpeg's AVFoundation input. Capture may require granting screen, camera, or microphone access to Cap Avalonia in system settings. By default, media and preferences are saved under the platform's application support directory in `CapAvalonia/`; the recordings folder can be changed in Settings.

## Recorder window comparison

The recorder window recreates Cap's 330×395 home screen. These comparison screenshots show the initial visual pass captured on 2026-10-05.

| Cap desktop reference | Avalonia first pass |
| --- | --- |
| ![Cap desktop recorder reference](screenshots/cap-desktop-reference.jpg) | ![Avalonia recorder first pass](screenshots/avalonia-first-pass.jpg) |
