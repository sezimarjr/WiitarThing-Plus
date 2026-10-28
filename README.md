# WiitarThing-Plus

A program that lets you connect Wii Guitar Hero instruments to a Windows PC wirelessly using a wiimote and Bluetooth, so they work in Clone Hero, YARG and other games as Xbox 360 controllers.

**WiitarThing-Plus** is an improved version of [WiitarThing](https://github.com/TheNathannator/WiitarThing) (based on 2.7.0.6). It fixes crashes and connection problems, recognizes guitars reliably, and adds an easy Star Power setup: a live calibration window and an automatic mode that triggers Star Power with a quick flick of the guitar, no calibration needed.

🇧🇷 [Leia em português](README.pt-BR.md) · 📋 [Full list of changes](CHANGELOG.md)

## Table of Contents

- [What's New in WiitarThing-Plus](#whats-new-in-wiitarthing-plus)
- [Star Power (Tilt)](#star-power-tilt)
- [Testing and Diagnostic Tools](#testing-and-diagnostic-tools)
- [About This Version](#about-this-version)
- [Setup](#setup)
  - [Install WiitarThing-Plus](#install-wiitarthing-plus)
  - [Connect Your Wiimote](#connect-your-wiimote)
  - [Calibrating Guitars](#calibrating-guitars)
- [Features](#features)
  - [Supported Devices](#supported-devices)
  - [Guitar Touch Bar](#guitar-touch-bar)
- [Troubleshooting](#troubleshooting)
  - ["My wiimote doesn't sync!"](#my-wiimote-doesnt-sync)
  - ["My guitar is moving my mouse around!"](#my-guitar-is-moving-my-mouse-around)
  - ["A fret sometimes doesn't register"](#a-fret-sometimes-doesnt-register)
- [For Further Assistance](#for-further-assistance)
- [Other Setup](#other-setup)
  - [Using a Dolphinbar](#using-a-dolphinbar)
  - [Using with Guitar Hero 3/Aerosmith/World Tour PC](#using-with-guitar-hero-3aerosmithworld-tour-pc)
- [Building From Source](#building-from-source)
- [Credits](#credits)

## What's New in WiitarThing-Plus

**Stability and connection**
- Fixed crashes: when a low-battery warning clears, when auto-connecting more than 4 controllers, and on startup after the settings file got corrupted (it is now saved safely).
- Fixed the Sync window getting stuck on "Searching..." when a nearby device had never been used.
- Wiimote reads are memory-safe, and a quick Identify-then-Connect can no longer leave two readers fighting over the same controller.
- Guitars are recognized on the first try. Before, they were often detected as a bare Wiimote until you unplugged and replugged the guitar from the Wiimote.
- Auto-refresh keeps working while a game has focus, so a controller that drops in-game reconnects on its own.
- Greedy/Toshiba mode can open the controller (the device handle was being leaked).
- A regular guitar is no longer switched into the Nyko Frontman's encrypted mode by a connector glitch, which made one fret read as several and strumming press frets.

**Gameplay**
- Rumble no longer delays fret and strum input.
- Your button profile is kept when the guitar is detected, and the virtual controller is no longer unplugged and replugged for no reason.
- Tilt and whammy never send invalid values.
- Custom calibration, pointer mode and the default calibration are saved properly.
- Release builds are optimized.

**New features**
- Live tilt calibration window (★ button on a connected guitar), saved per controller.
- Tilting the guitar can press Select, the Star Power button, for games that don't react to the tilt axis (like Clone Hero).
- An automatic Star Power mode that needs no calibration.
- Session monitor and live controller test tools.

## Star Power (Tilt)

Click the **★** button on a connected guitar to open the tilt window. It shows the guitar turning live, a Star Power meter, and lets you choose how Star Power is triggered:

- **Automatic:** WiitarThing-Plus learns your playing pose on its own (even if you play with the guitar diagonal) and triggers Star Power on a quick flick of the guitar, about 15° or more. Slowly changing posture, swaying and strumming hard don't trigger it. You can adjust the angle and minimum speed, see the size and speed of your last move, and record 10 seconds of movement to help tuning.
- **Calibrated:** mark your playing pose (button 1) and your Star Power pose (button 2), then choose how far you have to tilt (default 80%). The tilt must stay past that point for 0.1 s, which ignores strum jolts.

Either way, WiitarThing-Plus presses **Select** (Xbox *Back*, button 7) for Star Power, which works in Clone Hero without binding the tilt axis. The tilt axis (right stick) is still sent too.

The automatic detector was tuned on real recordings of a Les Paul played in Clone Hero and is covered by automated tests (simulated movements plus those recordings).

## Testing and Diagnostic Tools

The release includes helper scripts next to `WiitarThing.exe`:

| Script | What it does |
| --- | --- |
| `TESTAR_CONTROLE.bat` | Live view of what the guitar sends: frets, strum, Select/Start, whammy and tilt bars. |
| `MONITORAR_SESSAO.bat` | Turns on the session monitor (also while WiitarThing is already running): Bluetooth packet timing, dropouts, frets that drop out while held (worn contacts), and read glitches, saved to the `monitor` folder. |
| `RESUMO_MONITOR.bat` | Summarizes the last monitored session, with a verdict for Bluetooth and for each fret. |

You can also start WiitarThing-Plus with `WiitarThing.exe --monitor`.

## About This Version

WiitarThing-Plus is developed by **Sezimar Jr**, a full-stack developer. I used AI assistance ([Claude Code](https://claude.com/claude-code)) to understand the codebase, find bugs, and implement and test the improvements, always checking the results on real hardware: my own Les Paul, Wiimotes and Bluetooth adapter.

All the work of the original authors is kept and credited below. WiitarThing-Plus is released under the same MIT License.

## Setup

### Install WiitarThing-Plus

1. Download and install [ViGEmBus](https://github.com/ViGEm/ViGEmBus/releases).
2. Download WiitarThing-Plus from [the "Releases" tab](https://github.com/OWNER/WiitarThing-Plus/releases), and extract it into a new folder.
3. Requires .NET Framework 4.8 (already included in Windows 10 and 11).

#### A Note About Older Versions

If you're updating from WiitarThing v2.7.0 or earlier, you should uninstall the ScpDriver that it required, as it is no longer necessary. It is not required to be uninstalled however, if you have other programs that use it then you can leave it installed.

### Connect Your Wiimote

1. Start up WiitarThing-Plus, then hit the Sync button in the top-left.
2. Sync your wiimote by pressing either the red sync button underneath the battery cover, or both 1+2 at the same time.
   - Be patient during this step, it may take a few tries.
3. Once your wiimote is synced, close the Sync menu, then hit the Connect button on the entry that appears on the left side of the main menu.
4. You can now connect your wiimote extension.

Once your wiimote has been synced, you shouldn't have to sync it again, and can simply power it on and hit Connect. Some Bluetooth receivers don't handle this properly though, and may not correctly save the pairing.

Please note that third-party wiimotes do not work 99% of the time. This is not something WiitarThing can solve, as a majority of third-party wiimotes cut corners and only implement enough of the Bluetooth stack to connect to a Wii, and cannot connect to a PC in any capacity.

### Calibrating Guitars

Guitars must be calibrated before use, otherwise your tilt or whammy may not work correctly.

- **Tilt:** use the ★ window (see [Star Power](#star-power-tilt)). With the automatic mode no tilt calibration is needed. You can still use the Wiimote buttons: `1` in your playing pose, `2` in your Star Power pose. The calibration is saved and restored when the guitar reconnects.
- **Whammy:** move your whammy bar all the way down and up a few times.
- **Joystick:** move the joystick around in a few full circles.

## Features

### Supported Devices

The following wiimote extensions are supported:

- Guitar Hero Guitar
- Guitar Hero Drumkit
- DJ Hero Turntable
- Nunchuk
- Classic Controller
- Classic Controller Pro

Wiimotes can also be used standalone, currently with mappings that are intended for using a wiimote like you would a gamepad with Gamepad Mode enabled.

The Wii U Pro Controller is also supported, and can be synced the same way as a wiimote can.

### Guitar Touch Bar

On World Tour/GH5 guitars, the touch bar can be enabled and disabled by pressing the + and - buttons on the wiimote, respectively. When enabled, the touchbar will be mapped to the regular frets.

- Please note that the touchbar is not the best and may be very finnicky! WiitarThing has no ability to fix this, as it just simply takes the data it gets and translates it directly.

## Troubleshooting

### "My wiimote doesn't sync!"

Make sure you do NOT have HID Wiimote installed, as it completely overrides the Wiimote's drivers and makes WiitarThing unable to communicate with them. [Uninstallation instructions may be found here](https://www.julianloehr.de/educational-work/hid-wiimote/) (scroll down to "Uninstall Instructions").

If you do not have HID Wiimote installed, then your Bluetooth receiver is most likely to blame. Some receivers don't play well with wiimotes, and there just isn't anything that WiitarThing can do to fix it. Try to avoid cheaper receivers, and check user reviews before purchasing one.

### "My guitar is moving my mouse around!"

This is most likely being caused by Steam's controller configuration settings. Go to the Settings menu inside Steam and click on the Controller tab, then click the General Controller Settings button and uncheck the Xbox controller configuration support option. If you play a game that requires this setting, then you can enable it for that specific game by going to its properties and enabling Steam Input in the Controller tab.

Alternatively, you can instead remove the right stick mapping inside of the Desktop Configuration menu. By default, Steam's desktop configuration maps the right stick of gamepad controllers to the mouse. Removing this mapping will stop the mouse from being controlled by the guitar.

### "A fret sometimes doesn't register"

Run a session with `MONITORAR_SESSAO.bat` and check `RESUMO_MONITOR.bat`. If frets "drop out while held" but Bluetooth timing is fine, the problem is in the guitar: clean the fret contacts and, on the Les Paul, the neck connector pins, with isopropyl alcohol. If several frets fail, suspect the neck connector, since all frets go through it.

## For Further Assistance

If you have any questions or issues not addressed in this readme, open an issue on this repository, or join the [official Clone Hero server on Discord](https://discordapp.com/invite/Hsn4Cgu) and ask in the `#help-line` channel.

## Other Setup

### Using a Dolphinbar

1. Press the mode button on your Dolphinbar until it goes into mode 4, then sync your wiimote to it.
2. Open WiitarThing-Plus. 4 wiimotes will show up on the left, regardless of how many are connected to the Dolphinbar.
3. Click the ID button on each entry until your wiimote vibrates, then click Connect on it.

### Using with Guitar Hero 3/Aerosmith/World Tour PC

WiitarThing will not work directly for the PC versions of Guitar Hero 3, Aerosmith, or World Tour. This is because WiitarThing emulates a standard Xbox 360 *gamepad*, and cannot currently emulate an Xbox 360 *guitar* directly. To work around this, you can use [xinputemu](https://github.com/sanjay900/xinputemu) alongside WiitarThing to make the game see an Xbox 360 guitar instead.

## Building From Source

Requires Windows and the .NET SDK (Visual Studio 2022 also works).

```
dotnet build WiitarThing.sln -c Release
dotnet test Nintroller.Tests/Nintroller.Tests.csproj --filter "Category!=Sweep"
```

The program is built to `WiitarThing/bin/Release/net48/`.

## Credits

**WiitarThing-Plus** is by [Sezimar Jr](https://github.com/OWNER), full-stack developer, built with AI assistance ([Claude Code](https://claude.com/claude-code)).

It is based on WiitarThing and keeps all of its authors' work:

- **[Justin Keys / KeyPuncher](https://github.com/KeyPuncher)**: WiitarThing is built upon [WiinUSoft and WiinUPro](https://github.com/KeyPuncher/WiinUPro), but not forked because the changes are too significant and messy. All credit for connecting Wiimotes in general and most of the UI goes to KeyPuncher.
- **[Myst / Meowmaritus](https://github.com/Meowmaritus/WiitarThing)**: the original WiitarThing and guitar support.
- **[shockdude](https://github.com/shockdude)**: turntable support.
- **MWisBest**: the original ViGEmBus code, in [their fork/issue](https://github.com/Meowmaritus/WiitarThing/issues/9).
- **[Aida-Enna](https://github.com/Aida-Enna)**: merged the ViGEmBus code and built releases for it (2020-2023).
- **[TheNathannator](https://github.com/TheNathannator)**: maintains [WiitarThing](https://github.com/TheNathannator/WiitarThing) (2023-now), the version WiitarThing-Plus is based on.
- And everyone who contributed to these projects.

Released under the [MIT License](LICENSE).
