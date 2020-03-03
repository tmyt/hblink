using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using hblink.libs;

namespace hblink.Shared.Platform
{
    public abstract class BluetoothBase
    {
        protected const string ServiceUuid = "0000EA90-0000-1000-8000-00805F9B34FB";
        protected const string CharacteristicUuid = "00003F1F-0000-1000-8000-00805F9B34FB";
        protected const string NotifyDescriptorUuid = "00002902-0000-1000-8000-00805f9b34fb";

        public ObservableCollection<AbstractBluetoothDevice> Devices { get; } = new ObservableCollection<AbstractBluetoothDevice>();

        public event EventHandler<string> ConnectionStatusUpdated;

        protected void OnStatusUpdated(string status)
        {
            CoreApplication.MainView.Dispatcher.RunIdleAsync(_ =>
                ConnectionStatusUpdated?.Invoke(this, status)
            );
        }

        public abstract void StartScan();
        public abstract void StopScan();
        public abstract Connection TryConnect(AbstractBluetoothDevice device, TimeSpan timeout);
    }

    public class BluetoothHandler
    {
        private readonly ConnectionSource _connection;
        protected const int MaxMtu = 36;

        public Action<byte[]> WriteValue { get; set; }
        public Action DisconnectDevice { get; set; }
        public Action<string> UpdateStatus { get; set; }

        public BluetoothHandler(ConnectionSource connection)
        {
            _connection = connection;
        }

        public void Connect(string adapterName, byte[] identifier)
        {
            // Pair Request
            // 00 Length 01 30 32 30 42 43 43 <UTF8String>
            var name = Encoding.UTF8.GetBytes(adapterName);
            var length = Math.Min(name.Length, MaxMtu - 12);
            var value = new byte[9 + length];
            value[1] = (byte)(7 + length);
            value[2] = 0x01;
            Buffer.BlockCopy(identifier, 0, value, 3, 6);
            Buffer.BlockCopy(name, 0, value, 9, length);
            UpdateStatusInternal("Initiating session");
            WriteValueInternal(value);
        }

        private void RequestConnectionInfo()
        {
            // Wifi Info Request
            // 01 01 01
            UpdateStatusInternal("Requesting WiFi information");
            WriteValueInternal(new byte[] { 0x01, 0x01, 0x01 });
        }

        private async void DisconnectInternal(bool success)
        {
            // Disconnect?
            // 02 01 01
            await WriteValueInternal(new byte[] { 0x02, 0x01, 0x01 });
            DisconnectDevice?.Invoke();
            _connection.Source.SetResult(success);
        }

        private void CheckCompletion()
        {
            if (!string.IsNullOrEmpty(_connection.ESSID) && !string.IsNullOrEmpty(_connection.Passphrase))
            {
                UpdateStatusInternal("WiFi information received");
                DisconnectInternal(true);
            }
        }

        private async Task WriteValueInternal(byte[] value)
        {
            await Task.Delay(333);
            Debug.WriteLine($">>> {value.Dump()}");
            try
            {
                WriteValue.Invoke(value);
            }
            catch { }
        }

        private void UpdateStatusInternal(string message)
        {
            UpdateStatus?.Invoke(message);
        }

        public void OnReceive(byte[] value)
        {
            /*
             * Packet Format
             * AA BB CC...
             *  ^  ^  ^- Payload
             *  |  +---- Payload Length
             *  +------- Response Type
             */
            Debug.WriteLine($"<<< {value.Dump()}");
            switch (value[0])
            {
                case 0x0D:
                    // Connection Denied
                    // Payload = 0x00
                    UpdateStatusInternal("Connection denied");
                    DisconnectInternal(false);
                    break;
                case 0x0A:
                    // Connection Accepted
                    // Payload = Bluetooth Aadapter MAC Address
                    UpdateStatusInternal("Connection accepted");
                    RequestConnectionInfo();
                    break;
                case 0x0B:
                    // ESSID
                    // Payload = UTF8 String
                    UpdateStatusInternal("SSID received");
                    _connection.ESSID = Encoding.UTF8.GetString(value, 2, value[1]);
                    CheckCompletion();
                    break;
                case 0x0C:
                    // Passphrase
                    // Payload = UTF8 String
                    UpdateStatusInternal("Passphrase received");
                    _connection.Passphrase = Encoding.UTF8.GetString(value, 2, value[1]);
                    CheckCompletion();
                    break;
                case 0x0E: 
                    // Request Authorization
                    // Payload = N/A
                    UpdateStatusInternal("Authorize access from Camera");
                    break;
            }
        }

        public void OnError()
        {
            UpdateStatusInternal("Connection error");
            DisconnectInternal(false);
        }
    }

    public abstract class AbstractBluetoothDevice
    {
        public abstract string Name { get; }
    }

    public abstract class Connection
    {
        public abstract Task<bool> Task { get; }
        public string ESSID { get; set; }
        public string Passphrase { get; set; }
    }

    public class ConnectionSource : Connection
    {
        public TaskCompletionSource<bool> Source { get; } = new TaskCompletionSource<bool>();
        public override Task<bool> Task => Source.Task;
    }
}
