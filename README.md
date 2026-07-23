# FanControl.AXB35

Fan Control plugin for the Sixunited AXB35 motherboard. Provides temperature monitoring, fan RPM sensing, and fan speed control for mini PCs with an ITE IT5570 EC using the AXB35 firmware layout.

> **⚠️ Warning**
> This plugin communicates directly with the embedded controller (EC) via reverse-engineered registers. Compatibility is not guaranteed across all firmware versions. Setting a fan to manual mode overrides the EC’s automatic thermal management for that fan. Level 0 stops the fan completely.
>
> **Use at your own risk.** The author accepts no responsibility for any damage to hardware, data loss, system instability, or other issues that may arise from using this software. This software is provided “as is”, without warranty of any kind.

## Features

- **CPU Temp** — temperature sensor (°C)
- **3x Fan RPM** — CPU Fan 1, CPU Fan 2, System Fan
- **3x Fan Control** — paired control sensors, 6 discrete levels (0/20/40/60/80/100%)

## Installation

1. If you don’t already have it, install [Fan Control](https://github.com/Rem0o/FanControl.Releases) **V271 or later**.
2. Download the latest `FanControl.AXB35.dll` from the [Releases](https://github.com/pajtony/FanControl.AXB35/releases) page.
3. Either:
   - Copy the DLL into Fan Control’s `Plugins` folder  
     (usually `C:\Program Files\FanControl\Plugins` or next to `FanControl.exe`), **or**
   - In the Fan Control app, go to **Settings → Plugins → Install plugin** and select the downloaded DLL.
4. Restart Fan Control.
5. The new sensors and controls should appear under the **AXB35** group.

## Fan Calibration Reference

These are measured RPM values for each level on a Bosgame M5 (Sixunited AXB35). Your values may vary.

| Level | CPU Fan 1 | CPU Fan 2 | System Fan |
|-------|-----------|-----------|------------|
| 0% | 0 RPM | 0 RPM | 0 RPM |
| 20% | 1530 RPM | 1520 RPM | 600 RPM |
| 40% | 2570 RPM | 2540 RPM | 1330 RPM |
| 60% | 3390 RPM | 3340 RPM | 1970 RPM |
| 80% | 4020 RPM | 4000 RPM | 2370 RPM |
| 100% | 4600 RPM | 4600 RPM | 2530 RPM |

## Tested Hardware

| Device | Status | Notes |
|--------|--------|-------|
| Bosgame M5 | ✅ Confirmed working | Sixunited AXB35 |
| GMKtec EVO-X2 (V21 & V22) | ⬜ Untested, may work | Same AXB35 board |
| FEVM FA-EX9 (V22 & V30) | ⬜ Untested, may work | Same AXB35 board |
| NIMO AI MiniPC | ⬜ Untested, may work | Same AXB35 board |

## Build (for developers)

```bash
git clone https://github.com/pajtony/FanControl.AXB35.git
cd FanControl.AXB35

# Copy reference DLLs from your Fan Control installation
cp /path/to/FanControl/FanControl.Plugins.dll lib/
cp /path/to/FanControl/LibreHardwareMonitorLib.dll lib/

dotnet build -c Release
```

Requires .NET 8 SDK.

## How It Works

The plugin communicates with the IT5570 EC via the ACPI EC interface (ports `0x62`/`0x66`) using the PawnIO driver built into LibreHardwareMonitorLib. The EC firmware on these systems exposes fan control as 6 discrete levels through registers `0x21`-`0x26`. Fan Control's 0-100% is mapped to the nearest level.

## Register Map

| Register | Function |
|----------|----------|
| `0x35/0x36` | Fan 1 RPM |
| `0x37/0x38` | Fan 2 RPM |
| `0x28/0x29` | Fan 3 RPM |
| `0x21/0x23/0x25` | Fan mode (auto/manual) |
| `0x22/0x24/0x26` | Fan level (0-5) |
| `0x70` | CPU temperature |

Based on the [cmetz/ec-su_axb35-linux](https://github.com/cmetz/ec-su_axb35-linux) driver.

## Credits

- Register map from [cmetz/ec-su_axb35-linux](https://github.com/cmetz/ec-su_axb35-linux)
- Port I/O via LibreHardwareMonitor's PawnIO driver

## License

MIT
