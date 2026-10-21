# TriAxis Backup & Editor

[Português](README.md) · **English**

A Windows app (C# / .NET 8 / WPF) for the Mesa/Boogie TriAxis. It provides:
- **backup and restore of the unit's memory over MIDI SysEx**;
- a **local library** of backups;
- a **dump viewer**;
- a **visual editor** in the style of the TriAxis front panel;
- a **control mode** that turns the PC into a MIDI pedalboard (programs, banks, CC buttons and learned MIDI inputs).

The editor uses a **preliminary** SysEx map whose parameter order has been checked against the factory presets in the official owner's manual, but not yet every value on every unit. It always writes a new file and never changes the original backup (see [docs/SYSEX.md](docs/SYSEX.md), in Portuguese).

**Download:** get `TriAxisBackupEditor.exe` from the [Releases](https://github.com/palinetes/TriAxisBackupEditor/releases) page. It is a single file, no installation, for 64-bit Windows 10/11.
This is a **beta**: it is being tested with a real TriAxis. Found a problem or have an idea? Open an [issue](https://github.com/palinetes/TriAxisBackupEditor/issues).

**Author:** @palinetes · **License:** [MIT](LICENSE) · **User manual:** [docs/manual/index.en.html](docs/manual/index.en.html), also inside the program (**Manual** button or **F1**, works offline).

**Language:** the program is bilingual (Portuguese / English). It picks the language of Windows on first run, and you can change it any time with the **PT | EN** switch at the top of the window (the program restarts to apply it). The technical documents in `docs/` (SYSEX, ROADMAP, test plan) are in Portuguese; contributions to translate them are welcome.

## Simple mode and advanced mode

There is a **Simple | Advanced** switch at the top.

- **Simple mode** (default, for people who do not know MIDI):
  - **Setup wizard** on first run, in 4 steps:
    1. pick the device (input and output together);
    2. see the cable drawing;
    3. make the **first backup**, which also tests the connection (if nothing arrives, it tells you to swap the plugs);
    4. **find the TriAxis number** (the MIDI channel): either by listening to the TriAxis (you press a PROGRAM arrow on its panel and it announces its channel) or with a send test.
  - **Home screen** with three paths:
    - **My sounds:** the TriAxis panel plus the 90 sounds with **names** given by the user;
    - **Backup:** save the memory or restore a backup;
    - **Play:** an on-screen pedalboard.
  - **Send to the TriAxis** in 3 steps: warning, **mandatory safety backup** and "Yes, replace". Afterwards the program asks whether the display flashed.
- **Advanced mode:** the tabs described below (Connection, Control, Backup, Restore, Library, Editor, View dump, Settings, Log), for people who really use MIDI (e.g. RME + FCB1010).

Protections are the same in both modes. Advanced mode keeps the typed confirmation (`RESTORE`).

> ## ⚠ Warning: risk of overwriting the TriAxis memory
> Restoring a backup **replaces the whole current memory of the TriAxis**: the 90 presets, the MIDI program map and the FX loop/switching settings. This cannot be undone on the unit.
> That is why the program:
> - requires a **two-step confirmation**: tick a box and type `RESTORE`;
> - **offers a safety backup** of the current state before restoring;
> - **blocks** empty files, files without `F0` at the start, without `F7` at the end, or with invalid bytes;
> - **sends nothing to the TriAxis when it opens**: listing ports and testing the connection transmit no bytes.
>
> Even so, keep copies of your `.syx` files outside the computer (USB stick or cloud).
>
> **Tip from the TriAxis manual:** the factory presets 1–20 are loaded again every time the unit is switched on, unless you turn that off on the panel (SHIFT once, then LOOP).

---

## Contents
1. [Windows requirements](#windows-requirements)
2. [Installing the MIDI interface](#installing-the-midi-interface)
3. [Wiring: TriAxis, FCB1010 and G-Major](#wiring-triaxis-fcb1010-and-g-major)
4. [First backup](#first-backup)
5. [Restore](#restore)
6. [Library, comparison and viewer](#library-comparison-and-viewer)
7. [Visual editor](#visual-editor)
8. [Control mode (pedalboard)](#control-mode-pedalboard)
9. [MIDI troubleshooting](#midi-troubleshooting)
10. [Build and publish](#build-and-publish)
11. [Project structure](#project-structure)
12. [Technical decisions](#technical-decisions)
13. [References and credits](#references-and-credits)

---

## Windows requirements

| Item | Requirement |
|---|---|
| System | Windows 10 or 11, 64-bit (x64) |
| To **use** | Only the published `TriAxisBackupEditor.exe`. It is self-contained and needs no .NET installed. |
| To **build** | [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (8.0.x) |
| Hardware | USB MIDI interface with MIDI IN and MIDI OUT, plus 2 five-pin DIN MIDI cables |
| Network | None. The program does not use the internet and has no telemetry. |

Where the program stores its data (all local):

| What | Where |
|---|---|
| `.syx` backups (default) | `Documents\TriAxis Backups\` (the folder can be changed) |
| Incomplete receptions | `...\TriAxis Backups\Incompletos\*.syx.part` |
| Library (SQLite) | `%LOCALAPPDATA%\TriAxisBackupEditor\biblioteca.db` |
| Settings | `%LOCALAPPDATA%\TriAxisBackupEditor\configuracoes.json` |
| Technical logs | `%LOCALAPPDATA%\TriAxisBackupEditor\logs\triaxis-YYYYMMDD.log` |

## Installing the MIDI interface

1. Plug in the USB interface. *Class compliant* interfaces (Roland UM-ONE, M-Audio Uno, ESI, iConnectivity, etc.) are installed automatically by Windows.
2. If the manufacturer offers its own driver (for example the UM-ONE "advanced driver" mode), prefer it: it usually copes better with long SysEx.
3. Open the program, go to **Connection** and check that the interface appears under **MIDI IN** and **MIDI OUT**. The list refreshes by itself every 3 seconds.
4. **Avoid very cheap generic USB-MIDI cables.** Many lose or corrupt long SysEx. The TriAxis dump is about 1.1 KB (1,126 bytes) in a single message.
5. Close other programs that use MIDI (DAW, MIDI-OX, G-Major/pedalboard editors). The standard Windows driver does not let two programs open the same port.

## Wiring: TriAxis, FCB1010 and G-Major

For backup and restore, connect the TriAxis **directly** to the interface:

```
TriAxis MIDI OUT   ──►  Interface MIDI IN     (backup: the TriAxis sends the dump)
Interface MIDI OUT ──►  TriAxis MIDI IN       (restore: the PC sends the dump)
```

In a typical rig with a **Behringer FCB1010** and a **TC Electronic G-Major**:

```
Normal use (gig/rehearsal):
FCB1010 MIDI OUT ──► TriAxis MIDI IN
TriAxis MIDI OUT/THRU ──► G-Major MIDI IN
```

- **During backup/restore**, unplug the cable coming from the FCB1010 and plug the interface MIDI OUT cable into the TriAxis MIDI IN. That way no pedalboard message mixes with the dump.
- Also unplug the G-Major from the TriAxis output during a **restore** if it is in THRU mode. It should ignore SysEx from another manufacturer, but it is safer not to let the dump pass on.
- Afterwards, put the cables back to the normal arrangement.
- The labels on the rear panel (OUT or OUT/THRU) vary between TriAxis revisions. Check your unit.

## First backup

1. **Connection:** select **MIDI IN** and **MIDI OUT** and click **Test connection**. It opens the ports and listens to the input for 5 s, sending nothing. Changing preset on the TriAxis during the test helps confirm the input is alive.
2. **Backup:** check the destination folder and, if you like, add a note (e.g. "Saturday gig sounds").
3. Click **Make backup**. The screen highlights **"Press SHIFT + PRESET DOWN on the TriAxis panel"**.
4. Press **SHIFT + PRESET DOWN** on the TriAxis. The bar advances and the received byte count goes up.
5. When the final `F7` arrives, and after 1.5 s of silence, the program:
   - writes `TriAxis_Backup_YYYY-MM-DD_HH-mm-ss.syx` **exactly as received**;
   - re-reads the file from disk and checks the **SHA-256**;
   - records the name, date, size, hash, note and header in the library.
6. If the size differs from **1,126 bytes**, the size of a real TriAxis dump, the file is saved anyway and the program warns you. That information is valuable for validating the map: note the size and see [docs/SYSEX.md](docs/SYSEX.md).

If reception is interrupted (timeout, cancel or loose cable), what arrived is saved **only** as `Incompletos\..._INCOMPLETO.syx.part`. That file does not enter the library and cannot be restored.

**Safety rules:** a backup only starts with MIDI IN and MIDI OUT selected. The program waits up to 60 s for the dump and accepts at most 10 s without bytes in the middle of reception (both times are configurable). You can cancel at any moment.

## Restore

1. **Restore → Select .syx**, or **Library → Restore**. The file is validated immediately. An empty file, one without `F0`/`F7` or with status bytes in the middle **blocks** the button.
2. Adjust block sending if needed. The default is **128 bytes** with **20 ms** interval (adjustable from 5 to 50 ms). Older interfaces may need longer intervals.
3. Click **Restore backup...** and a red confirmation window appears:
   - **Safety backup:** saves the current TriAxis state before overwriting (recommended).
   - **Step 1:** tick "I understand that restoring will replace the current TriAxis memory".
   - **Step 2:** type `RESTORE`.
   - If the file has warnings (header or size outside the map), you must also acknowledge them.
4. **Send to the TriAxis.** Before sending, the file is re-read and its hash checked: if it changed since validation, the restore is blocked.
5. When it finishes, **check that the TriAxis display flashed**, which confirms reception. Then try a few presets.

Cancelling works **before sending and between messages**. A SysEx message that has started is always sent up to the `F7`, so partial data is never left on the TriAxis. You cannot close the program in the middle of a restore.

Files saved by the web editor at triaxiseditor.com (the real dump followed by zeros, 2,746 bytes) are also accepted: only the 1,126 bytes the TriAxis understands are used, and your file is never changed.

## Library, comparison and viewer

- **Library:** shows name, date, size, SHA-256 (the full hash is in the tooltip), origin, note and whether the file still exists. It lets you:
  - search by name, date (`2026-10-16` or `16/10/2026`), note or hash;
  - import existing `.syx` files;
  - open the folder, view, restore, duplicate, rename and edit the note;
  - delete: the file goes to the Windows Recycle Bin, or you can remove it from the library only;
  - export CSV (`;`-separated, opens directly in Excel).
- **Compare 2 selected** (Ctrl+click): compares the hashes and lists the byte-by-byte differences. When both follow the preliminary map, it also shows the region and the affected presets.
- **View dump** shows:
  - size, SHA-256 and header;
  - validation;
  - hex dump with an ASCII column, in a monospaced font;
  - when the file matches the map, the table of the **90 presets**, read-only. The **Open in editor** button takes the file to the Editor tab.

## Visual editor

The **Editor** tab recreates the front of the TriAxis, drawn from scratch as vector art (inspired by triaxiseditor.com):

| Element | How to use it |
|---|---|
| **PRESET** (red display, ◄ ►) | Chooses the preset being edited (1–90). The mouse wheel over the display also changes it. |
| **PROGRAM / CHANNEL** (green display, ◄ ►) | Browses Program Change 1–128 and shows the associated preset (only when the program map in the backup looks valid). |
| **PGM/CH** | On (red LED): the PRESET arrows choose **which preset the current Program Change calls**. Disabled while the program map has not been confirmed. |
| **GAIN … DYNAMIC VOICE** | ◄ ►, mouse wheel, or click the display to choose from a list (0.0 … 10). An amber dot marks what changed. |
| **RHY / LD1 / LD2 LED matrix** | Click the mode LED (Rhythm G/Y, Lead 1 G/Y/R, Lead 2 G/Y/R). |
| **SW 1–4 and FX LOOP** | Click to turn on/off. Names given in *Settings → Outputs and loops* (e.g. Simul-Class 2:90 functions) appear in the LED tooltips and on the preset card. |
| **MODE / LOOP SW** | MODE moves to the next mode; LOOP SW turns the FX LOOP on/off. |
| **ENTR / EXIT** | ENTR = Save as new .syx; EXIT = undo the current preset (like "write" and "exit without writing" on the unit). |
| **SHIFT, OUTPUT** | Decorative only (the OUTPUT volume is not part of the presets). |

Below the panel:
- **Copy/Paste preset** and **Undo this preset/Undo all**;
- the list of **changes** (e.g. `Preset 03 · Gain: 5.0 → 7.0`);
- the **matrix of the 90 presets** (click a row to edit it).

**Editor safety rules:**
- works on an **in-memory copy**: the source `.syx` is never changed;
- **Save as new .syx** writes `TriAxis_Editado_YYYY-MM-DD_HH-mm-ss.syx` to the library (origin "Edited");
- only preset bytes (5–994) change. The header, the final region (995–1124, where the reference says there is a program map, not yet confirmed), `F7` and unknown bits are preserved bit by bit, and there are automated tests for this;
- **Send to the TriAxis...** saves (if needed) and opens the **Restore** tab, with the two-step confirmation;
- before trusting the editor, run the round-trip test from the [test plan](docs/PLANO_DE_TESTES.md) (section D, in Portuguese) and the editor tests (section F).

## Control mode (pedalboard)

The **Control** tab turns the program into a MIDI controller for the TriAxis. The mode sends **only Program Change and Control Change**. **Nothing is written to the TriAxis memory.**

According to the official TriAxis manual:
- **Program Change is how you change sound.** The unit has 128 *programs* (MIDI locations) that point to the 90 *presets*; each preset stores the mode (Rhythm/Lead channel), the FX Loop and switches 1–4.
- **Control Change does not switch channel, FX Loop or switches.** It only moves the nine sound knobs (Gain, Treble...), on a controller number you assign per preset on the panel (SHIFT three times).
- The unit's **MIDI OUT** sends a Program Change when you use the PROGRAM keys on its panel, which is how the wizard can listen to the channel.

1. In **Connection**, select the **MIDI OUT** connected to the **TriAxis MIDI IN**.
2. In **Control**, choose:
   - the **MIDI channel** the TriAxis receives on (the same one set on the unit);
   - the **pedals per bank** (10, like the FCB1010; or 4, 5 or 8).
3. Click **Activate control mode**. While it is active, backup and restore are blocked, because the MIDI port is in use.

| Control | Action |
|---|---|
| **Pedals 1–10** (click) | Program Change of the current bank. With 10 per bank: bank 0 → programs 1–10, bank 1 → 11–20, …, bank 12 → 121–128. The LED lights on the active pedal. |
| **BANK ▲ ▼** | Changes bank (the FCB1010 also sends nothing when changing bank: only the pedal sends). |
| **PROGRAM −1 / +1** | Previous/next program. |
| **CC buttons 1–4** | Toggle CC = 127/0 on the channel. The CC name and number are editable (default CC 80–83). **Note:** per the TriAxis manual it does not switch channel, FX Loop or switches by CC (programs do that, i.e. Program Change). CC only moves the nine sound knobs, on the controller number you assigned to each on the panel. The buttons are for expression pedals or other devices. |

**Keyboard** (with the tab open): `1`…`9`, `0` = pedals 1–10 (the numeric keypad also works); `↑`/`PgUp` = bank +; `↓`/`PgDn` = bank −; `→`/`←` = program ±1.

**MIDI inputs (learn):** if you have a pedalboard or controller connected to the PC:
1. choose the **Control input**;
2. activate the mode;
3. click **Learn** next to the action (pedal 1–10, bank ▲▼, program ±1, CC buttons);
4. press the pedal.

Program Change, CC (fires on press, i.e. value > 0) and notes can be learned. The **Forward Program Change from the input** option resends to the TriAxis, on the configured channel, the PCs that have no associated action. Everything is saved in the settings.

## MIDI troubleshooting

| Symptom | What to do |
|---|---|
| No interface in the list | Replug the USB, try another USB port and wait for Windows to install the driver. Click **Refresh devices**. |
| "The port is already in use by another program" | Close the DAW, MIDI-OX, G-Major/FCB editors and other instances of this program. |
| "No data received" on backup | Check that the cable goes from the **TriAxis MIDI OUT to the interface MIDI IN**. Check that the selected MIDI IN port is the interface's. Press SHIFT + PRESET DOWN within the time limit. |
| Incomplete backup (no F7) | Use a better cable/interface, avoid USB hubs, increase the **receive timeout** in Settings and repeat. |
| Backup with a size other than 1,126 bytes | The file was saved unchanged. It may be a different firmware/map version. Make 2 or 3 backups in a row and compare the hashes: if they are identical, the capture is reliable. |
| TriAxis display does not flash on restore | Check that the cable goes from the **interface MIDI OUT to the TriAxis MIDI IN**. Increase the interval between blocks (e.g. 40–50 ms) and/or reduce the block to 64 bytes. Take the FCB1010 out of the chain. |
| The TriAxis does not change program | In the wizard (step 4) click **Listen to the TriAxis** and press a PROGRAM arrow on its panel. If nothing is heard, check the cable; otherwise it tells you the channel. |
| My sounds 1–20 come back to the factory ones at power-up | The TriAxis reloads the factory presets 1–20 when switched on. Press SHIFT then LOOP on the panel to turn that off. |
| Interface disconnected midway | The program warns you and does not freeze. Reconnect, select the ports again and repeat the whole operation. |
| I want to see what happened | **Log** tab, or the file in `%LOCALAPPDATA%\TriAxisBackupEditor\logs`. It records devices, bytes received/sent, hashes and errors. |

## Build and publish

Prerequisite: **.NET 8 SDK** installed (`dotnet --list-sdks` should show `8.0.x`).

```bash
dotnet restore TriAxisBackupEditor.sln
```

```bash
dotnet build TriAxisBackupEditor.sln -c Release
```

```bash
dotnet test TriAxisBackupEditor.sln -c Release
```

**Self-contained single-file executable for Windows x64** (does not require .NET on the target machine):

```bash
dotnet publish src/TriAxisBackupEditor.App/TriAxisBackupEditor.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o publish/win-x64
```

Result: `publish/win-x64/TriAxisBackupEditor.exe`, about 65 MB with everything embedded, including native SQLite.

The `publish.ps1` script does restore, runs the tests (and aborts if any fails), publishes and prints the `.exe` SHA-256:

```bash
powershell -ExecutionPolicy Bypass -File publish.ps1
```

Smaller version, which depends on the installed *.NET 8 Desktop Runtime*:

```bash
dotnet publish src/TriAxisBackupEditor.App/TriAxisBackupEditor.App.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/win-x64-fdd
```

## Project structure

```
TriAxisBackupEditor/
├─ src/
│  ├─ TriAxisBackupEditor.App/          WPF (MVVM): Views, ViewModels, 7-segment panel, pedalboard, dark theme
│  ├─ TriAxisBackupEditor.Application/  Use cases: capture, library, restore, control mode, localization, log
│  ├─ TriAxisBackupEditor.Midi/         IMidiDeviceService + WinMM implementation (winmm.dll)
│  ├─ TriAxisBackupEditor.SysEx/        Validation, reassembly, hash, hex, preliminary map, dump editor
│  ├─ TriAxisBackupEditor.Storage/      SQLite (IBackupRepository), files, settings, CSV
│  └─ TriAxisBackupEditor.Tests/        xUnit tests
├─ docs/
│  ├─ manual/             User manual (Portuguese and English)
│  ├─ SYSEX.md            Format, preliminary map, offset validation plan (Portuguese)
│  ├─ ROADMAP.md          Phases 1–7 (Portuguese)
│  └─ PLANO_DE_TESTES.md  Test routine with a real TriAxis (Portuguese)
├─ publish.ps1
├─ README.md / README.en.md
└─ TriAxisBackupEditor.sln
```

Main interfaces:

| Interface | Project | Role |
|---|---|---|
| `IMidiDeviceService` | Midi | Lists ports and opens IN/OUT (opening never sends bytes) |
| `TriAxisDumpEditor` | SysEx | Edits a copy of the dump, only in the mapped regions |
| `ControlSurfaceService` | Application | Control mode: Program Change/CC and control input |
| `TriAxisListener` | Application | Listens to the TriAxis MIDI OUT for a Program Change (finds the channel) |
| `ITriAxisSysExService` | SysEx | Validates, interprets (preliminary map), compares, hashes and formats hex |
| `IBackupRepository` | Storage | Metadata in SQLite |

The tests cover:
- SysEx validation (empty, no F0/F7, status in the middle, multiple messages);
- SHA-256 (FIPS vector and file hash);
- automatic backup naming and unique names;
- fragmentation and reassembly (blocks of 1 to 4096 bytes and random; interleaved real-time bytes);
- **restore blocking without confirmation** (no confirmation, only step 1, only step 2, wrong word, unacknowledged warnings, invalid file, file changed after validation);
- byte-for-byte identical block sending;
- capture with timeout, cancel and partial data;
- SQLite, library, CSV and parser;
- the **real 1,126-byte dump format** and the web-editor files (dump + zeros);
- **dump editor** (only editable regions change, original intact, unknown bits preserved, 500 random edits stay valid);
- **control mode** (Program Change/CC encoding, FCB1010-style banks, MIDI learn, never sends SysEx) and the **TriAxis listener**;
- **simple mode**: automatic input/output pairing by name, safe send that requires a safety backup before allowing the replacement, sound names on the pedals;
- real WinMM port enumeration.

## Technical decisions

- **Native WinMM MIDI instead of DryWetMidi.** The specification asked for DryWetMidi "or another mature library". I chose the Windows MIDI API itself (`winmm.dll`, present from Windows 95 to 11) for two reasons:
  1. **Receiving:** SysEx buffers must be controlled. Here there are 16 buffers of 4 KB, and `SysExAssembler` reassembles any fragmentation.
  2. **Sending:** **raw slices** of a SysEx must be sent in blocks with an interval (`midiOutLongMsg` per block, waiting for `MHDR_DONE` before freeing memory). High-level libraries send the whole event at once.

  Everything sits behind `IMidiDeviceService`. Swapping to another library is a new implementation, without touching the rest.
- **Nothing in the dump is altered.** Capture stores the `F0…F7` messages in arrival order. The only filtering is the one required by the MIDI specification: interleaved real-time bytes (`F8`–`FF`) are not part of the SysEx and are discarded. The count shows in the log and in a warning.
- **Never overwrite.** New files use `FileMode.CreateNew`. Repeated names get `_2`, `_3`. Renaming does not replace. Deleting sends to the Recycle Bin.
- **No dump request by default.** There is no documented *dump request* command for the TriAxis (the unit sends its dump when you press SHIFT + PRESET DOWN). There is an advanced option to send a user-supplied message, off by default and with a warning.

## References and credits

- **markphilpot/triaxiseditor**, a web editor published at triaxiseditor.com ([GitHub](https://github.com/markphilpot/triaxiseditor)), GPL-3.0 license. It is an HTML/JS page that opens and writes `.syx`, with no MIDI communication. From it come the **preliminary map** of the presets (90 × 11 bytes, 128 programs) and the value tables. That reference assumed a 2,746-byte dump; a real TriAxis sends 1,126 (see [docs/SYSEX.md](docs/SYSEX.md)), and the web-editor files are that dump followed by zeros, which this program also opens. The code of this project was written from scratch in C#: only facts about the format were used, credited and marked **not fully validated on hardware**. If any piece of code from that project is ported in the future, the GPL-3.0 will apply to this code.
- The **official Mesa/Boogie TriAxis owner's manual** was used to confirm the factory presets, the MIDI behavior (Program Change, Control Change, channel) and the data dump procedure.
- Mesa/Boogie, TriAxis and Simul-Class are trademarks of their respective owners. This is an independent project, not affiliated with the manufacturer. The editor panel is the project's own vector drawing; no photo or logo of the manufacturer is embedded.
- Developed by **@palinetes**.
