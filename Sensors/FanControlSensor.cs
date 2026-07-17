// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT License was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT.

using FanControl.Plugins;
using FanControl.AXB35.Interop;

namespace FanControl.AXB35.Sensors
{
    /// <summary>
    /// Fan control sensor for the cmetz/ec-su_axb35 level-based EC.
    /// The EC supports 6 discrete fan levels (0=off, 1=20%, 2=40%, 3=60%, 4=80%, 5=100%).
    /// Fan Control's 0-100% is mapped to the nearest level.
    /// </summary>
    internal sealed class FanControlSensor : IPluginControlSensor2
    {
        private readonly EcAccess _ec;
        private readonly int _fanIndex;
        private readonly string _id;
        private readonly string _name;
        private readonly string _pairedFanId;

        // Level nibble values written to the level register (mode_reg + 1)
        // Combined with the base value (0x10/0x20/0x30) for each fan
        private static readonly byte[] LevelNibbles = { 0x7, 0x2, 0x3, 0x4, 0x5, 0x6 };

        public FanControlSensor(EcAccess ec, int fanIndex, string name, string pairedFanSensorId)
        {
            _ec = ec;
            _fanIndex = fanIndex;
            _id = $"IT5570/Fan{fanIndex}_Control";
            _name = name;
            _pairedFanId = pairedFanSensorId;
        }

        public string Id => _id;
        public string Name => _name;
        public string PairedFanSensorId => _pairedFanId;

        public float? Value { get; private set; }

        /// <summary>
        /// Set the fan speed percentage (0-100).
        /// Mapped to one of 6 discrete levels.
        /// </summary>
        public void Set(float val)
        {
            int level = PercentToLevel(val);
            Value = val;

            // Write mode register to manual (varies per fan)
            byte manualMode = _fanIndex switch
            {
                1 => (byte)0x11,
                2 => (byte)0x21,
                3 => (byte)0x31,
                _ => 0x11
            };
            _ec.WriteModeByte(_fanIndex, manualMode);

            // Write level to level register (mode_reg + 1)
            byte baseVal = _fanIndex switch
            {
                1 => (byte)0x10,
                2 => (byte)0x20,
                3 => (byte)0x30,
                _ => 0x10
            };
            _ec.WriteLevelByte(_fanIndex, (byte)(baseVal + LevelNibbles[level]));
        }

        /// <summary>
        /// Reset to automatic (EC-controlled) mode.
        /// </summary>
        public void Reset()
        {
            Value = null;

            byte autoVal = _fanIndex switch
            {
                1 => (byte)0x10,
                2 => (byte)0x20,
                3 => (byte)0x30,
                _ => 0x10
            };
            _ec.WriteModeByte(_fanIndex, autoVal);
        }

        public void Update()
        {
            // Value is tracked from Set() calls
        }

        /// <summary>
        /// Map Fan Control's 0-100% to 6 discrete levels.
        /// </summary>
        private static int PercentToLevel(float percent)
        {
            if (percent <= 0) return 0;
            if (percent <= 10) return 0;  // 0% = off
            if (percent <= 30) return 1;  // 20%
            if (percent <= 50) return 2;  // 40%
            if (percent <= 70) return 3;  // 60%
            if (percent <= 90) return 4;  // 80%
            return 5;                      // 100%
        }
    }
}