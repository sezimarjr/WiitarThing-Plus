# Changelog

## WiitarThing-Plus 1.0.0

Based on [WiitarThing 2.7.0.6](https://github.com/TheNathannator/WiitarThing). Every change has its own commit with the details.

### Crashes and freezes
- Sync window no longer gets stuck on "Searching..." when a nearby Bluetooth device has never been used (zeroed "last seen/used" dates crashed the scan).
- No crash when a low-battery warning clears (a color was cast to the wrong type, from a background thread).
- A truncated or unreadable settings file no longer makes the app crash on every launch: settings are written to a temporary file and swapped in, and a broken file is kept as `prefs.config.broken`.
- Auto-connect no longer crashes with more than four controllers, and no longer skips controllers.
- A failure while updating the virtual controller can no longer stop a detected extension from being set up.

### Connection and detection
- The Wiimote device handle was leaked when the stream was created, so greedy/Toshiba mode (exclusive access) could not open the controller.
- Wiimote reads use memory the garbage collector can't move, and never return while a read is still pending; reads happen on the reader thread instead of a thread-pool hop per report.
- Each read session has its own thread and token, so a quick Identify-then-Connect can't leave two threads reading the same controller.
- The first status report after reconnecting is no longer ignored (the guitar stayed silent until a Wiimote button was pressed).
- Guitars are recognized on the first try: when the extension isn't ready yet (it answers with an error, zeros or "partially inserted"), detection waits and asks again instead of settling on a bare Wiimote or getting stuck.
- Auto-refresh no longer stops for good when a controller drops while a game has focus; it also starts when launched minimized.
- A regular guitar is no longer switched into the Nyko Frontman's encrypted mode by a brief connector glitch (it now needs about 0.3 s of all-zero reports), all-zero reports are no longer passed on as "every fret pressed", and the decryption key is picked once instead of guessed on every report.
- The device card always shows the detected extension (icon and tilt button), even if a detection notification is missed.

### Gameplay
- Rumble is written from the thread pool instead of the input thread, so it no longer delays frets and strums.
- Your button profile is kept when the extension is detected, and the virtual controller is only re-plugged when its identity actually changes.
- Tilt and whammy never output invalid (NaN) values.
- Custom calibration, pointer mode and the default calibration are saved and restored correctly; editing one device no longer changes the defaults for all of them.
- Release builds are compiled with optimizations.

### Star Power and tilt
- Tilt calibration window (★ on a connected guitar): live guitar picture and meter, "mark now" buttons for the playing and Star Power poses (the Wiimote's 1 and 2 buttons still work), a warning when the poses are too close, Save and Cancel.
- Tilt calibration is saved per controller and restored on reconnect.
- Tilting can press Select (Xbox Back, button 7), so Star Power works in games that don't read the tilt axis, such as Clone Hero. On by default, per controller.
- Calibrated mode: adjustable Star Power point (default 80%), and the tilt must stay past it for 100 ms, which ignores strum jolts.
- Automatic mode: learns the playing pose by itself and triggers on a quick flick (default 15°), ignoring slow posture changes, swaying and strumming. Adjustable angle and minimum speed, a readout of your last move, and a 10-second movement recorder.

### Diagnostics
- Session monitor (`--monitor`, or `MONITORAR_SESSAO.bat` even while running): Bluetooth packet timing, gaps and dropouts, frets that drop out while held, read glitches and strums, saved per second to the `monitor` folder; `RESUMO_MONITOR.bat` summarizes it.
- Live controller test (`TESTAR_CONTROLE.bat`).
- `WiitarLog.log` records extension detection steps and errors.

### Tests
- New `Nintroller.Tests` project: an accelerometer simulator and tests for the Star Power flick detector, plus three real recordings of a Les Paul in Clone Hero used as regression tests.

### Branding and credits
- Named WiitarThing-Plus, with its own version in the title bar and a Credits entry in Advanced Config. All original authors are credited in the app, the README and the license.
