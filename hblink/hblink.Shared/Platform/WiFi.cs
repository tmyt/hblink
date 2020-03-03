using System;
using System.Threading.Tasks;

namespace hblink.Shared.Platform
{
    public abstract class WiFiBase
    {
        public event EventHandler<string> StatusUpdated;

        protected void OnStatusUpdated(string status)
        {
            StatusUpdated?.Invoke(this, status);
        }

        protected bool IsHasselbladNetwork(int ipv4Address)
        {
            return (ipv4Address & 0x00ffffff) == 0x0002A8C0;
        }

        public abstract Task<bool> Connect(string essid, string passphrase);
        public abstract bool IsConnected();
    }
}
