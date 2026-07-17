// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT License was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT.

using System;
using System.Collections.Generic;
using FanControl.Plugins;
using FanControl.AXB35.Interop;
using FanControl.AXB35.Sensors;

namespace FanControl.AXB35
{
    /// <summary>
    /// Fan Control plugin for the Sixunited AXB35 motherboard.
    /// Uses the cmetz/ec-su_axb35 register map for 3 fans.
    /// </summary>
    public class AXB35Plugin : IPlugin3
    {
        private readonly IPluginLogger _logger;

        private PortIO? _portIO;
        private SioAccess? _sio;
        private EcAccess? _ec;

        private readonly List<IPluginSensor> _tempSensors = new();
        private readonly List<IPluginSensor> _fanSensors = new();

        private bool _chipDetected;
        private bool _sramAccessible;
        private readonly List<IPluginControlSensor> _controlSensors = new();

        public AXB35Plugin(IPluginLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Name => "Sixunited_AXB35";

        public void Initialize()
        {
            try
            {
                _logger.Log("AXB35Plugin: Initializing...");

                _portIO = new PortIO();
                _portIO.Initialize();
                _logger.Log("AXB35Plugin: PawnIO driver initialized.");

                // Try SIO detection (may work on some systems)
                _sio = new SioAccess(_portIO);
                bool sioChipFound = false;
                try
                {
                    sioChipFound = _sio.TryDetectChip(out string? sioError);
                    if (sioChipFound)
                        _logger.Log("AXB35Plugin: IT5570 detected via SIO (ID 0x5570).");
                    else
                        _logger.Log($"AXB35Plugin: SIO detection failed: {sioError ?? "chip ID mismatch"}");
                }
                catch (Exception ex)
                {
                    _logger.Log($"AXB35Plugin: SIO detection threw exception: {ex.GetType().Name}: {ex.Message}");
                }

                // Initialize EC access
                _ec = new EcAccess(_portIO, sioChipFound ? _sio : null);

                // Probe EC using cmetz register map
                bool ecProbeOk = _ec.ProbeEC();
                if (ecProbeOk)
                {
                    _logger.Log("AXB35Plugin: ACPI EC probe successful (cmetz register map).");
                    _chipDetected = true;

                    // Test SRAM access
                    if (sioChipFound)
                    {
                        try
                        {
                            _ec.ReadCpuDieTemp();
                            _sramAccessible = true;
                            _logger.Log("AXB35Plugin: SRAM access working.");
                        }
                        catch
                        {
                            _sramAccessible = false;
                            _logger.Log("AXB35Plugin: SRAM access failed.");
                        }
                    }
                }
                else
                {
                    _logger.Log("AXB35Plugin: ACPI EC probe failed — no compatible EC found.");
                }

                _logger.Log("AXB35Plugin: Initialization complete.");
            }
            catch (Exception ex)
            {
                _logger.Log($"AXB35Plugin: Initialization failed: {ex.Message}");
                Close();
                throw;
            }
        }

        public void Load(IPluginSensorsContainer container)
        {
            if (!_chipDetected || _ec == null)
            {
                _logger.Log("AXB35Plugin: Load skipped.");
                return;
            }

            _logger.Log("AXB35Plugin: Loading sensors...");

            // Temperature sensor (register 0x70)
            var tempSensor = new TemperatureSensor(_ec, "IT5570/Temp_CPU", "CPU Temp", _ec.ReadTemperature);
            _tempSensors.Add(tempSensor);
            container.TempSensors.Add(tempSensor);

            // SRAM temperature sensors (if available)
            if (_sramAccessible)
            {
                var cpuDie = new TemperatureSensor(_ec, "IT5570/Temp_CPU_Die", "CPU Die", _ec.ReadCpuDieTemp);
                var heatsink = new TemperatureSensor(_ec, "IT5570/Temp_Heatsink", "Heatsink", _ec.ReadHeatsinkTemp);
                var chipset = new TemperatureSensor(_ec, "IT5570/Temp_Chipset", "Chipset", _ec.ReadChipsetTemp);
                var ecTemp = new TemperatureSensor(_ec, "IT5570/Temp_EC", "EC Internal", _ec.ReadEcTemp);
                _tempSensors.AddRange(new[] { cpuDie, heatsink, chipset, ecTemp });
                foreach (var s in new[] { cpuDie, heatsink, chipset, ecTemp })
                    container.TempSensors.Add(s);
            }

            // Fan RPM sensors (3 fans from cmetz register map)
            var fan1 = new FanSpeedSensor(_ec, 1, "CPU Fan 1");
            var fan2 = new FanSpeedSensor(_ec, 2, "CPU Fan 2");
            var fan3 = new FanSpeedSensor(_ec, 3, "System Fan");
            _fanSensors.AddRange(new[] { fan1, fan2, fan3 });
            foreach (var s in new[] { fan1, fan2, fan3 })
                container.FanSensors.Add(s);

            // Fan control sensors — pass the actual sensor Id for pairing (match names too)
            var ctrl1 = new FanControlSensor(_ec, 1, "CPU Fan 1", fan1.Id);
            var ctrl2 = new FanControlSensor(_ec, 2, "CPU Fan 2", fan2.Id);
            var ctrl3 = new FanControlSensor(_ec, 3, "System Fan", fan3.Id);
            _controlSensors.AddRange(new[] { ctrl1, ctrl2, ctrl3 });
            foreach (var s in new[] { ctrl1, ctrl2, ctrl3 })
                container.ControlSensors.Add(s);

            _logger.Log($"AXB35Plugin: Loaded {_tempSensors.Count} temp sensors, {_fanSensors.Count} fan sensors, {_controlSensors.Count} control sensors.");
        }

        public void Update()
        {
            try
            {
                foreach (var s in _tempSensors)
                    s.Update();
                foreach (var s in _fanSensors)
                    s.Update();
                foreach (var s in _controlSensors)
                    s.Update();
            }
            catch (Exception ex)
            {
                _logger.Log($"AXB35Plugin: Update error: {ex.Message}");
            }
        }

        public event Action? RefreshRequested;

        public void Close()
        {
            _logger.Log("AXB35Plugin: Closing...");
            _chipDetected = false;
            _sramAccessible = false;
            _tempSensors.Clear();
            _fanSensors.Clear();
            _controlSensors.Clear();
            _ec?.Dispose();
            _ec = null;
            _sio?.Dispose();
            _sio = null;
            _portIO?.Dispose();
            _portIO = null;
            _logger.Log("AXB35Plugin: Closed.");
        }
    }
}