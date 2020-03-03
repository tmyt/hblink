using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using hblink.libs;
using hblink.Shared.Platform;

namespace hblink
{
    public sealed partial class CameraFinderPage : Page
    {
        private BluetoothBase _bluetooth;
        private WiFiBase _wifi;

        public CameraFinderPage()
        {
            this.InitializeComponent();
#if __IOS__
            _bluetooth = new iOS.Platform.Bluetooth();
            _wifi = new iOS.Platform.Wifi();
#elif __ANDROID__
            _bluetooth = new Droid.Platform.Bluetooth(Context);
            _wifi = new Droid.Platform.Wifi(Context);
#else
            _bluetooth = new UWP.Platform.Bluetooth();
            _wifi = new UWP.Platform.Wifi();
#endif
            _bluetooth.ConnectionStatusUpdated += StatusUpdated;
            _wifi.StatusUpdated += StatusUpdated;
            deviceList.ItemsSource = _bluetooth.Devices;
            Loaded += CameraFinderPage_Loaded;
        }

        private async void CameraFinderPage_Loaded(object sender, RoutedEventArgs e)
        {
#if __IOS__
            Platform.Injection.RequestPermission();
#endif
#if __ANDROID__
            var activity = (Droid.MainActivity)Uno.UI.BaseActivity.Current;
            if (activity.IsAllPermissionsGranted() || await activity.RequestPermissions())
#endif
            {
                Dispatcher.RunIdleAsync(__ => _bluetooth.StartScan());
            }
            // check wifi state
            CheckCurrentConnection();
        }

        private async void CheckCurrentConnection()
        {
            if (!_wifi.IsConnected()) return;
            System.Diagnostics.Debug.WriteLine("Seems to be connected to camera");
            if (!await ConnectionManager.CheckServerAvailable()) return;
            System.Diagnostics.Debug.WriteLine("Available camera");
            System.Diagnostics.Debug.WriteLine("Goto gallery directly");
            _bluetooth.StopScan();
            Frame.Navigate(typeof(GalleryPage));
        }

        private async void Connect(object sender, RoutedEventArgs e)
        {
            var device = (AbstractBluetoothDevice)deviceList.SelectedItem;
            if (device == null) return;
            _bluetooth.StopScan();
            var conn = _bluetooth.TryConnect(device, TimeSpan.FromSeconds(60));
            if (!await conn.Task) return;
            if (await _wifi.Connect(conn.ESSID, conn.Passphrase))
            {
                Frame.Navigate(typeof(GalleryPage));
            }
            else
            {
                statusText.Text = "Status: Connection Failed";
            }
        }

        private void StatusUpdated(object sender, string e)
        {
            statusText.Text = $"Status: {e}";
        }
    }
}
