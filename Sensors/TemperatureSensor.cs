// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT License was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT.

using System;
using FanControl.Plugins;
using FanControl.AXB35.Interop;

namespace FanControl.AXB35.Sensors
{
    /// <summary>
    /// A temperature sensor that reads from either an ACPI EC register or an EC SRAM address.
    /// </summary>
    internal sealed class TemperatureSensor : IPluginSensor
    {
        private readonly EcAccess _ec;
        private readonly Func<byte> _readFunc;
        private readonly string _id;
        private readonly string _name;

        /// <summary>
        /// Creates a temperature sensor.
        /// </summary>
        /// <param name="ec">EC access instance.</param>
        /// <param name="id">Unique sensor ID.</param>
        /// <param name="name">Display name (e.g. "CPU").</param>
        /// <param name="readFunc">Function that reads the temperature in °C.</param>
        public TemperatureSensor(EcAccess ec, string id, string name, Func<byte> readFunc)
        {
            _ec = ec ?? throw new ArgumentNullException(nameof(ec));
            _id = id ?? throw new ArgumentNullException(nameof(id));
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _readFunc = readFunc ?? throw new ArgumentNullException(nameof(readFunc));
        }

        public string Id => _id;
        public string Name => _name;
        public float? Value { get; private set; }

        public void Update()
        {
            try
            {
                byte raw = _readFunc();
                Value = raw;
            }
            catch
            {
                // If reading fails, keep the previous value
                // Fan Control will show "---" if Value is null at startup
            }
        }
    }
}