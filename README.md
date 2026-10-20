# Cap Avalonia

A cross-platform Avalonia port of Cap's desktop app. It recreates the compact recorder window and all 13 Settings pages, with local Recordings and Screenshots libraries. Local workflows include display recording, screenshot capture, selectable displays and crop areas, camera and microphone inputs, circular webcam picture-in-picture, video trimming and export, screenshot annotation with pen, arrows, rectangles, highlights and text, a scrolling teleprompter, unread activity, media import, clipboard copy, deletion to the system Trash, configurable shortcuts, and local backup automations. Recorder mode, capture devices, library paths, trim points, teleprompter text, shortcuts, and settings persist between launches.

This is a local recorder and library; Cap's account, upload, share-link, and other online services are not connected. Camera and microphone capture uses FFmpeg AVFoundation on macOS, DirectShow on Windows, and V4L2/PulseAudio on Linux. Screen recordings can include a circular webcam overlay and mix selected microphone and system-audio inputs. System audio depends on an available loopback source, such as BlackHole on macOS or a monitor/Stereo Mix source on Windows and Linux. Windows and X11/XWayland Linux can capture a selected open window by its native handle, independent of its position; Linux window selection uses `xwininfo`. macOS currently uses a selected screen rectangle for the Window target, so windows layered over that rectangle remain visible. Linux display capture uses X11/XWayland; native Wayland portal capture is not implemented. The editor previews recordings, sets trim points, exports MP4 clips with selectable file-size/quality tradeoffs, and keeps the original recording unchanged. Recordings and screenshots can be renamed in the local library. Captions, transitions, cursor effects, and multi-track editing are not included. Screenshot annotations export as a new PNG. The Automations page can copy new recordings and screenshots to separate local backup folders. Shortcut registration uses Carbon on macOS; app-focused shortcuts work on other platforms. Cap's CLI, automatic updates, and online license services are not connected. The floating stop control is marked capture-excluded on macOS and may appear in recordings on other platforms.

The reference application is [Cap](https://github.com/CapSoftware/Cap), an open-source screen recorder by Cap Software. This project adapts its desktop interface under the AGPL-3.0 license. See [LICENSE](LICENSE) for the license and the upstream copyright notice.

## Run

Requires the .NET 9 SDK or newer.

```sh
dotnet run
```

The UI is built with Avalonia 12.1.3 and targets macOS, Windows, and Linux.

To build a self-contained macOS app bundle on Apple Silicon, run `scripts/package-macos.sh`. It creates `.build/CapAvalonia-final.app`; open it with `open .build/CapAvalonia-final.app`. The script accepts an optional runtime identifier and output bundle path, for example `scripts/package-macos.sh osx-arm64 "$PWD/.build/CapAvalonia-preview.app"`. Build Windows and Linux release archives with `scripts/package-portable.sh win-x64` and `scripts/package-portable.sh linux-x64`. These include the .NET runtime. FFmpeg can be bundled by setting `CAPAVALONIA_FFMPEG_DIR` to a directory containing `ffmpeg`, `ffprobe`, and `ffplay` (use `.exe` names for Windows). Without bundled tools, the user must install FFmpeg on `PATH` for recording and editing.

The macOS packager uses ad hoc signing by default, so macOS may tie privacy permissions to the exact build. If Screen & System Audio Recording is enabled but capture still reports permission required, toggle this build's entry off and on, then quit and reopen Cap Avalonia. If you have a stable Apple signing identity in your keychain, set `CAPAVALONIA_CODESIGN_IDENTITY` to its name before packaging so macOS can recognize the app between builds.

Recording and video editing require FFmpeg. Video preview uses `ffplay`, and clip inspection uses `ffprobe`; these are searched beside the app, in its `Helpers` directory, and on `PATH`. macOS full-display screenshots use the system `screencapture` utility; area screenshots and Windows/Linux screenshots use FFmpeg. Capture may require granting screen, camera, or microphone access to Cap Avalonia in System Settings. Media and preferences are saved under the platform's application support directory in `CapAvalonia/`; the recordings folder can be changed in Settings.

Install FFmpeg separately if a build does not include it: `brew install ffmpeg` on macOS, a current Windows build from the [FFmpeg download page](https://ffmpeg.org/download.html), or your distribution's `ffmpeg` package on Linux. Use an FFmpeg build that includes AVFoundation, DirectShow, V4L2, and PulseAudio support for the capture devices you plan to use.

## Recorder window comparison

The recorder window recreates Cap's 330×395 home screen. This comparison shows the current packaged macOS build captured on 2026-10-19.

| Cap desktop reference | Avalonia current build |
| --- | --- |
| ![Cap desktop recorder reference](screenshots/cap-desktop-reference.jpg) | ![Avalonia recorder current build](screenshots/avalonia-final.jpg) |

## Port challenge write-up

### What was ported

Cap is an existing open-source desktop recording product. This project recreates its compact recorder and settings interface in Avalonia and carries over the local workflows that do not need Cap's hosted service: screen and camera recording, a recording library, local trim/export, and screenshot annotation. The hosted account, upload, and share-link flows were intentionally left out so recordings remain on the user's device.

### How the implementation changed

The recorder and settings views are built in Avalonia XAML and C#, so one UI codebase targets macOS, Windows, and Linux. FFmpeg provides the media pipeline, with AVFoundation on macOS, DirectShow on Windows, and X11/XWayland plus V4L2/PulseAudio on Linux. Platform-specific permission and capture rules stay behind small platform-aware services. Media, preferences, previews, and backups are stored locally.

The biggest rework was replacing Cap's online recording workflow with a local file lifecycle. A recording must start quickly, stop cleanly, survive an interrupted write when the recoverable-recording option is enabled, appear in the library, and open in the local editor. Reproducing the compact window also required explicit sizing, custom typography, spacing, dark controls, and platform-specific title-bar adjustments rather than relying on a default Avalonia theme.

### What went well and what remains different

Avalonia made it practical to keep the recorder and settings in one C# UI project while using native capture backends through FFmpeg. The local library, recording pipeline, PiP composition, screenshot markup, and basic trim/export do not require a Cap account. System audio still depends on an available loopback or monitor source. Linux screen capture currently needs X11/XWayland; native Wayland portal capture is not implemented. The editor does not include captions, transitions, cursor effects, or multi-track editing, and Cap's online services are not part of this build.

### Effort and validation

The work was not tracked as billable hours, so this report does not invent an hour estimate. The first project commit was made on 6 October 2026; release package preparation began on 19 October, a 13-day calendar span. Self-contained packages were built for macOS arm64, Windows x64, and Linux x64. On macOS, screen-only capture and a screen recording with the built-in camera overlay and microphone were recorded and checked as playable H.264/AAC MP4 files after granting Screen, Camera, and Microphone access. The webcam-composition and audio-mix filters were also smoke-tested with synthetic media. Windows and Linux builds have been compiled and packaged, but device capture still needs validation on those operating systems because FFmpeg devices, privacy grants, and available system-audio sources depend on the machine. The latest macOS build's screen-capture permission was refreshed and verified.
