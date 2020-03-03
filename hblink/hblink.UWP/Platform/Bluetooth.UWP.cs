using System;
using hblink.Shared.Platform;

namespace hblink.UWP.Platform
{
    class Bluetooth : BluetoothBase
    {
        public override void StartScan()
        {
        }

        public override void StopScan()
        {
        }

        public override Connection TryConnect(AbstractBluetoothDevice device, TimeSpan timeout)
        {
            var source = new ConnectionSource();
            source.Source.SetResult(false);
            return source;
        }
    }
}
