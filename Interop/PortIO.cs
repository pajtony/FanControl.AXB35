// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT License was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT.

using System;
using LibreHardwareMonitor.PawnIo;

namespace FanControl.AXB35.Interop
{
    /// <summary>
    /// Wraps the LpcAcpiEc (PawnIO) driver for direct port I/O.
    /// Provides ReadPort/WritePort for ACPI EC ports (0x62/0x66) and SIO ports (0x4E/0x4F).
    /// </summary>
    internal sealed class PortIO : IDisposable
    {
        private LpcAcpiEc? _pawn;
        private bool _disposed;
        private bool _initialized;

        /// <summary>
        /// Initialize the PawnIO driver for port I/O access.
        /// </summary>
        public void Initialize()
        {
            _pawn = new LpcAcpiEc();
            _initialized = true;
        }

        /// <summary>
        /// Whether the PawnIO driver was successfully initialized.
        /// </summary>
        public bool IsInitialized => _initialized && _pawn != null;

        /// <summary>
        /// Read a byte from the specified I/O port.
        /// </summary>
        public byte ReadPort(byte port)
        {
            if (_pawn == null)
                throw new InvalidOperationException("PortIO not initialized.");
            return _pawn.ReadPort(port);
        }

        /// <summary>
        /// Write a byte to the specified I/O port.
        /// </summary>
        public void WritePort(byte port, byte value)
        {
            if (_pawn == null)
                throw new InvalidOperationException("PortIO not initialized.");
            _pawn.WritePort(port, value);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _pawn?.Close();
                _pawn = null;
                _initialized = false;
            }
        }
    }
}