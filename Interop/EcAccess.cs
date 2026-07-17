// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT License was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT.

using System;
using System.Threading;

namespace FanControl.AXB35.Interop
{
    /// <summary>
    /// ACPI Embedded Controller (EC) access via standard ACPI EC ports 0x62/0x66.
    /// Register map based on the Sixunited AXB35 (cmetz/ec-su_axb35-linux) EC layout,
    /// which supports 3 fans and a single temperature sensor.
    /// </summary>
    internal sealed class EcAccess : IDisposable
    {
        // ACPI EC ports
        private const byte EC_SC = 0x66;
        private const byte EC_DATA = 0x62;

        // EC status bits
        private const byte EC_SC_IBF = 0x02;
        private const byte EC_SC_OBF = 0x01;

        // EC commands
        private const byte EC_CMD_READ = 0x80;
        private const byte EC_CMD_WRITE = 0x81;

        // --- cmetz/ec-su_axb35 register map (3 fans) ---
        public const byte REG_FAN1_RPM_HI = 0x35;
        public const byte REG_FAN1_RPM_LO = 0x36;
        public const byte REG_FAN2_RPM_HI = 0x37;
        public const byte REG_FAN2_RPM_LO = 0x38;
        public const byte REG_FAN3_RPM_HI = 0x28;
        public const byte REG_FAN3_RPM_LO = 0x29;

        // Fan mode registers: 0x10/0x20/0x30 = auto, 0x11/0x21/0x31 = manual
        public const byte REG_FAN1_MODE = 0x21;
        public const byte REG_FAN2_MODE = 0x23;
        public const byte REG_FAN3_MODE = 0x25;

        // Fan level registers (mode_reg + 1): 0x7=off, 0x2=20%, 0x3=40%, 0x4=60%, 0x5=80%, 0x6=100%
        public const byte REG_FAN1_LEVEL = 0x22;
        public const byte REG_FAN2_LEVEL = 0x24;
        public const byte REG_FAN3_LEVEL = 0x26;

        // Temperature sensor
        public const byte REG_TEMP = 0x70;

        // Power mode (0/1/2 = quiet/balanced/performance)
        public const byte REG_POWER_MODE = 0x31;

        // Also check passiveEndeavour registers for compatibility
        public const byte REG_FAN_LEGACY_RPM_HI = 0x22; // Same as fan1 level on cmetz!
        public const byte REG_FAN_LEGACY_RPM_LO = 0x23;
        public const byte REG_LEGACY_CPU_TEMP = 0x26;   // Same as fan3 level on cmetz!

        // EC SRAM addresses (via SIO)
        public const ushort SRAM_CPU_DIE_TEMP = 0x05B9;
        public const ushort SRAM_HEATSINK_TEMP = 0x0C44;
        public const ushort SRAM_CHIPSET_TEMP = 0x0C4A;
        public const ushort SRAM_EC_TEMP = 0x086A;

        private const int MAX_RETRIES = 10000;

        private readonly PortIO _port;
        private readonly SioAccess? _sio;
        private bool _disposed;

        public EcAccess(PortIO port, SioAccess? sio)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
            _sio = sio;
        }

        private void WaitIbfClear()
        {
            for (int i = 0; i < MAX_RETRIES; i++)
            {
                if ((_port.ReadPort(EC_SC) & EC_SC_IBF) == 0)
                    return;
                Thread.SpinWait(1);
            }
            throw new TimeoutException("EC IBF did not clear (EC busy).");
        }

        private void WaitObfSet()
        {
            for (int i = 0; i < MAX_RETRIES; i++)
            {
                if ((_port.ReadPort(EC_SC) & EC_SC_OBF) != 0)
                    return;
                Thread.SpinWait(1);
            }
            throw new TimeoutException("EC OBF was not set (EC unresponsive).");
        }

        public byte ReadByte(byte offset)
        {
            WaitIbfClear();
            _port.WritePort(EC_SC, EC_CMD_READ);
            WaitIbfClear();
            _port.WritePort(EC_DATA, offset);
            WaitObfSet();
            return _port.ReadPort(EC_DATA);
        }

        public void WriteByte(byte offset, byte value)
        {
            WaitIbfClear();
            _port.WritePort(EC_SC, EC_CMD_WRITE);
            WaitIbfClear();
            _port.WritePort(EC_DATA, offset);
            WaitIbfClear();
            _port.WritePort(EC_DATA, value);
        }

        /// <summary>
        /// Probe the EC by checking cmetz register map for plausible fan RPM values.
        /// </summary>
        public bool ProbeEC()
        {
            try
            {
                // Check fan1 RPM (0x35/0x36) - should be non-zero if fan is spinning
                byte hi = ReadByte(REG_FAN1_RPM_HI);
                byte lo = ReadByte(REG_FAN1_RPM_LO);
                ushort rpm = (ushort)((hi << 8) | lo);

                // Check temperature
                byte temp = ReadByte(REG_TEMP);

                // If any fan RPM is non-zero and temp is reasonable, EC is present
                if (rpm > 0 && rpm < 20000 && temp >= 10 && temp <= 110)
                    return true;

                // Fallback: check if fan mode registers look valid
                byte mode1 = ReadByte(REG_FAN1_MODE);
                if ((mode1 == 0x10 || mode1 == 0x11) && temp >= 10 && temp <= 110)
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        // --- Fan RPM sensors ---
        public ushort ReadFan1Rpm()
        {
            byte hi = ReadByte(REG_FAN1_RPM_HI);
            byte lo = ReadByte(REG_FAN1_RPM_LO);
            return (ushort)((hi << 8) | lo);
        }

        public ushort ReadFan2Rpm()
        {
            byte hi = ReadByte(REG_FAN2_RPM_HI);
            byte lo = ReadByte(REG_FAN2_RPM_LO);
            return (ushort)((hi << 8) | lo);
        }

        public ushort ReadFan3Rpm()
        {
            byte hi = ReadByte(REG_FAN3_RPM_HI);
            byte lo = ReadByte(REG_FAN3_RPM_LO);
            return (ushort)((hi << 8) | lo);
        }

        // --- Fan mode (auto/manual) ---
        public byte ReadFan1Mode() => ReadByte(REG_FAN1_MODE);
        public byte ReadFan2Mode() => ReadByte(REG_FAN2_MODE);
        public byte ReadFan3Mode() => ReadByte(REG_FAN3_MODE);

        // --- Fan level (0-5) ---
        public byte ReadFan1Level() => ReadByte(REG_FAN1_LEVEL);
        public byte ReadFan2Level() => ReadByte(REG_FAN2_LEVEL);
        public byte ReadFan3Level() => ReadByte(REG_FAN3_LEVEL);

        // --- Temperature ---
        public byte ReadTemperature() => ReadByte(REG_TEMP);

        // --- Power mode ---
        public byte ReadPowerMode() => ReadByte(REG_POWER_MODE);

        // --- Write mode register for fan control ---
        public void WriteModeByte(int fanIndex, byte value)
        {
            byte reg = fanIndex switch
            {
                1 => REG_FAN1_MODE,
                2 => REG_FAN2_MODE,
                3 => REG_FAN3_MODE,
                _ => throw new ArgumentOutOfRangeException(nameof(fanIndex))
            };
            WriteByte(reg, value);
        }

        /// <summary>
        /// Write a value to the fan level register (mode_reg + 1).
        /// Combined value: base (0x10/0x20/0x30) + level nibble (0x7/0x2-0x6).
        /// </summary>
        public void WriteLevelByte(int fanIndex, byte value)
        {
            byte reg = fanIndex switch
            {
                1 => REG_FAN1_LEVEL,
                2 => REG_FAN2_LEVEL,
                3 => REG_FAN3_LEVEL,
                _ => throw new ArgumentOutOfRangeException(nameof(fanIndex))
            };
            WriteByte(reg, value);
        }

        // --- SRAM-based sensors (optional, requires SIO access) ---
        public bool HasSramAccess => _sio != null;

        public byte ReadCpuDieTemp()
        {
            if (_sio == null) throw new InvalidOperationException("SIO not available.");
            return _sio.ReadSram(SRAM_CPU_DIE_TEMP);
        }

        public byte ReadHeatsinkTemp()
        {
            if (_sio == null) throw new InvalidOperationException("SIO not available.");
            return _sio.ReadSram(SRAM_HEATSINK_TEMP);
        }

        public byte ReadChipsetTemp()
        {
            if (_sio == null) throw new InvalidOperationException("SIO not available.");
            return _sio.ReadSram(SRAM_CHIPSET_TEMP);
        }

        public byte ReadEcTemp()
        {
            if (_sio == null) throw new InvalidOperationException("SIO not available.");
            return _sio.ReadSram(SRAM_EC_TEMP);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}