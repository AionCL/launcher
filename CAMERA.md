# Camera preview 2.5.41

Release: https://github.com/AionCL/launcher/releases/tag/launcher-v2.5.41
Build commit: 02cc3415da553a85a43be23cb330cc9d465bf281.
Windows CI: 35604087975, successful. ZIP SHA-256:
7fe3bf0ea2c7d8a904e0c86b7c04c955c5d69683d3bd54fc4bd601c32f6595c3.

Settings > Camera / FOV opens opt-in controls for integer FOV 60-170 and distance
5-100. Defaults are disabled, 60/32. Save applies at the next game launch; cancel
does not change preferences. Preferences live in LocalAppData/AionCL/camera.json.
The helper is verified against the trusted client manifest before starting.
Only the game PID and camera values are passed; no login credentials.

Client 2.4.5 appends package 017 (tools/AionCL.Camera.exe) to the unchanged 16
existing packages. Installation and repair use the usual launcher downloader,
hash checks and extraction. Original version.dll files are never replaced.
The runtime is x64 only, matching the configured launcher executable.

Validated: 14 Windows test groups, preference bounds/roundtrip, missing/corrupt
helper rejection, UI bounds and rendered dialog, public manifest validation,
package download/hash/extraction, invalid helper argument rejection, external
memory apply/readback and engine reset reapplication, helper exit with game.
The earlier in-game DLL test was visually confirmed by the user at 80/100.

Remaining real recipe: update launcher/client, enable camera with 80/100, launch
through the launcher, verify zoom in/out, rotate near walls, change zone and
relaunch. Check that disabling the option launches without a helper. External
transport has not yet been visually confirmed in-game; this is a pre-release,
not full stability qualification. No x86 production support claimed.

Detailed runtime recovery journal: client-2.4/patches/camera/WORKLOG.md.
