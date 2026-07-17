// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT License was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT.

using System;

namespace FanControl.AXB35.Interop
{
    /// <summary>
    /// Super I/O (SIO) access for ITE IT5570 chip detection and SRAM indirect access.
    /// SIO is accessed via ports 0x4E (index) and 0x4F (data).
    /// This is OPTIONAL — the plugin works without SIO access (fewer temp sensors).
    /// </summary>
    internal sealed class SioAccess : IDisposable
    {
        private const byte SIO_PORT = 0x4E;
        private const byte SIO_DATA = 0x4F;

        private readonly PortIO _port;
        private bool _sioEntered;
        private bool _disposed;

        // SIO config register addresses for SRAM indirect access
        private const byte SIO_REG_ADDR_LO = 0x10;
        private const byte SIO_REG_ADDR_HI = 0x11;
        private const byte SIO_REG_DATA = 0x12;

        public SioAccess(PortIO port)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
        }

        /// <summary>
        /// Enter SIO configuration mode via the standard ITE key sequence.
        /// </summary>
        private void Enter()
        {
            _port.WritePort(SIO_PORT, 0x87);
            _port.WritePort(SIO_PORT, 0x01);
            _port.WritePort(SIO_PORT, 0x55);
            _port.WritePort(SIO_PORT, 0xAA);
            _sioEntered = true;
        }

        /// <summary>
        /// Exit SIO configuration mode.
        /// </summary>
        private void Exit()
        {
            if (_sioEntered)
            {
                _port.WritePort(SIO_PORT, 0x02);
                _port.WritePort(SIO_DATA, 0x02);
                _sioEntered = false;
            }
        }

        private byte ReadRegister(byte reg)
        {
            _port.WritePort(SIO_PORT, reg);
            return _port.ReadPort(SIO_DATA);
        }

        /// <summary>
        /// Try to detect an IT5570 chip via SIO.
        /// Returns true if chip ID 0x5570 was found, false otherwise.
        /// </summary>
        /// <param name="error">If detection fails, details about why.</param>
        public bool TryDetectChip(out string? error)
        {
            try
            {
                Enter();
                ushort devId = (ushort)((ReadRegister(0x20) << 8) | ReadRegister(0x21));
                Exit();
                if (devId == 0x5570)
                {
                    error = null;
                    return true;
                }
                error = $"chip ID 0x{devId:X4} (expected 0x5570)";
                return false;
            }
            catch (Exception ex)
            {
                try { Exit(); } catch { }
                error = $"exception: {ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Scan all SIO configuration registers (0x00-0x3F) and log interesting values.
        /// </summary>
        /// <param name="log">Logging callback.</param>
        public void ScanRegisters(Action<string> log)
        {
            try
            {
                Enter();
                log("SIO register scan (non-zero, non-0xFF):");
                for (int reg = 0; reg <= 0x3F; reg++)
                {
                    byte val = ReadRegister((byte)reg);
                    if (val != 0 && val != 0xFF)
                        log($"  SIO[0x{reg:X2}] = 0x{val:X2} ({val})");
                }
                // Special: read chip ID registers even if zero
                ushort devId = (ushort)((ReadRegister(0x20) << 8) | ReadRegister(0x21));
                log($"  SIO chip ID (0x20-0x21) = 0x{devId:X4}");
                byte rev = ReadRegister(0x22);
                log($"  SIO revision (0x22) = 0x{rev:X2}");
                Exit();
            }
            catch (Exception ex)
            {
                try { Exit(); } catch { }
                log($"SIO register scan error: {ex.Message}");
            }
        }

        /// <summary>
        /// Read a byte from the EC's SRAM space via SIO indirect access.
        /// </summary>
        public byte ReadSram(ushort address)
        {
            Enter();
            try
            {
                // Set address high byte via SMFI registers (0x2E/0x2F -> 0x11)
                _port.WritePort(SIO_PORT, 0x2E);
                _port.WritePort(SIO_DATA, SIO_REG_ADDR_HI);
                _port.WritePort(SIO_PORT, 0x2F);
                _port.WritePort(SIO_DATA, (byte)((address >> 8) & 0xFF));

                // Set address low byte
                _port.WritePort(SIO_PORT, 0x2E);
                _port.WritePort(SIO_DATA, SIO_REG_ADDR_LO);
                _port.WritePort(SIO_PORT, 0x2F);
                _port.WritePort(SIO_DATA, (byte)(address & 0xFF));

                // Read data
                _port.WritePort(SIO_PORT, 0x2E);
                _port.WritePort(SIO_DATA, SIO_REG_DATA);
                _port.WritePort(SIO_PORT, 0x2F);
                return _port.ReadPort(SIO_DATA);
            }
            finally
            {
                Exit();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                try { Exit(); } catch { }
            }
        }
    }
}