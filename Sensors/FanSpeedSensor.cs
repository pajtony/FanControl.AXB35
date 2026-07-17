// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT License was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT.

using FanControl.Plugins;
using FanControl.AXB35.Interop;

namespace FanControl.AXB35.Sensors
{
    /// <summary>
    /// Fan speed (RPM) sensor for a single fan, reading from cmetz register map.
    /// </summary>
    internal sealed class FanSpeedSensor : IPluginSensor
    {
        private readonly EcAccess _ec;
        private readonly int _fanIndex;
        private readonly string _id;
        private readonly string _name;

        public FanSpeedSensor(EcAccess ec, int fanIndex, string name)
        {
            _ec = ec;
            _fanIndex = fanIndex;
            _id = $"IT5570/Fan{fanIndex}_RPM";
            _name = name;
        }

        public string Id => _id;
        public string Name => _name;

        public float? Value { get; private set; }

        public void Update()
        {
            try
            {
                ushort rpm = _fanIndex switch
                {
                    1 => _ec.ReadFan1Rpm(),
                    2 => _ec.ReadFan2Rpm(),
                    3 => _ec.ReadFan3Rpm(),
                    _ => 0
                };
                if (rpm == 8000) rpm = 0; // fan3 quirk from cmetz driver
                Value = rpm;
            }
            catch
            {
            }
        }
    }
}