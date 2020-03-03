using System.Threading.Tasks;
using hblink.Shared.Platform;

namespace hblink.UWP.Platform
{
    public class Wifi : WiFiBase
    {
        public override Task<bool> Connect(string essid, string passphrase)
        {
            return Task.FromResult(false);
        }
    }
}
