using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using SystemConfiguration;
using hblink.Shared.Platform;
using NetworkExtension;

namespace hblink.iOS.Platform
{
    public class Wifi : WiFiBase
    {
        public override async Task<bool> Connect(string essid, string passphrase)
        {
            OnStatusUpdated("Connecting to WiFi");
            var manager = NEHotspotConfigurationManager.SharedManager;
            var conf = new NEHotspotConfiguration(essid, passphrase, false);
            conf.JoinOnce = false;
            if (!await manager.ApplyConfigurationAsync(conf).ContinueWith(t => t.Exception == null))
            {
                return false;
            }
            OnStatusUpdated("Waiting for connection");
            await WaitForConnection();
            return true;
        }

        public override bool IsConnected()
        {
            return GetActiveInterfaces().Any(IsHasselbladNetwork);
        }

        private IEnumerable<NetworkInterface> GetActiveInterfaces()
        {
            CaptiveNetwork.TryGetSupportedInterfaces(out var names);
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(x => x.OperationalStatus == OperationalStatus.Up)
                .Where(x => x.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                .Where(x => names?.Contains(x.Name) ?? false);
        }

        private async Task WaitForConnection()
        {
            while (true)
            {
                if (IsConnected()) return;
                await Task.Delay(333);
            }
        }

        private bool IsHasselbladNetwork(NetworkInterface iface)
        {
            var prop = iface.GetIPProperties();
            foreach (var inf in prop.UnicastAddresses)
            {
                if (!IsHasselbladNetwork(ParseAddress(inf.Address, inf.IPv4Mask))) continue;
                Debug.WriteLine(inf.Address.ToString());
                Debug.WriteLine(inf.IPv4Mask.ToString());
                return true;
            }
            return false;
        }

        private int ParseAddress(IPAddress address, IPAddress mask)
        {
            var b1 = address.GetAddressBytes();
            var b2 = mask.GetAddressBytes();
            return ((b1[0] & b2[0]) << 0) | ((b1[1] & b2[1]) << 8)
                   | ((b1[2] & b2[2]) << 16) | ((b1[3] & b2[3]) << 24);
        }
    }
}