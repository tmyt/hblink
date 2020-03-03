using System.Linq;
using System.Threading.Tasks;
using Android.Content;
using Android.Net.Wifi;
using hblink.Shared.Platform;

namespace hblink.Droid.Platform
{
    public class Wifi : WiFiBase
    {
        private readonly Context _context;
        private readonly WifiManager _manager;

        public Wifi(Context context)
        {
            _context = context;
            _manager = (WifiManager)context.GetSystemService(Context.WifiService);
        }

        public override async Task<bool> Connect(string essid, string passphrase)
        {
            OnStatusUpdated("Enabling WiFi");
            await EnableWifi();
            OnStatusUpdated("Connecting to WiFi");
            // create configuration parameter
            var conf = new WifiConfiguration();
            conf.Ssid = $"\"{essid}\"";
            conf.PreSharedKey = $"\"{passphrase}\"";
            conf.AllowedProtocols.Set((int)ProtocolType.Rsn);
            conf.AllowedProtocols.Set((int)ProtocolType.Wpa);
            conf.AllowedKeyManagement.Set((int)KeyManagementType.WpaPsk);
            conf.AllowedPairwiseCiphers.Set((int)PairwiseCipherType.Ccmp);
            conf.AllowedPairwiseCiphers.Set((int)PairwiseCipherType.Tkip);
            conf.AllowedGroupCiphers.Set((int)GroupCipherType.Wep40);
            conf.AllowedGroupCiphers.Set((int)GroupCipherType.Wep104);
            conf.AllowedGroupCiphers.Set((int)GroupCipherType.Ccmp);
            conf.AllowedGroupCiphers.Set((int)GroupCipherType.Tkip);
            _manager.AddNetwork(conf);
            _manager.SaveConfiguration();
            _manager.UpdateNetwork(conf);
            // try connect
            var network = _manager.ConfiguredNetworks.FirstOrDefault(i => i.Ssid == conf.Ssid);
            _manager.Disconnect();
            await Task.Delay(333);
            _manager.EnableNetwork(network.NetworkId, true);
            // wait for connection
            OnStatusUpdated("Waiting for connection");
            await WaitForWifiEstablished(conf.Ssid);
            return true;
        }

        public override bool IsConnected()
        {
            var info = _manager.ConnectionInfo;
            return _manager.IsWifiEnabled && IsHasselbladNetwork(info?.IpAddress ?? 0);
        }

        private Task<bool> EnableWifi()
        {
            var source = new TaskCompletionSource<bool>();
            if (!_manager.IsWifiEnabled)
            {
                _manager.SetWifiEnabled(true);
                WaitForWifiEnabled().ContinueWith(_ => source.SetResult(true));
            }
            else
            {
                source.SetResult(true);
            }
            return source.Task;
        }

        private async Task WaitForWifiEnabled()
        {
            while (!_manager.IsWifiEnabled)
            {
                await Task.Delay(333);
            }
        }

        private async Task WaitForWifiEstablished(string essid)
        {
            while (true)
            {
                var info = _manager.ConnectionInfo;
                if (info != null && info.SSID == essid && IsHasselbladNetwork(info.IpAddress))
                {
                    break;
                }
                await Task.Delay(333);
            }
        }
    }
}