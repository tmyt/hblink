using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using Android.Provider;
using hblink.Platform;
using hblink.Shared.Platform;
using Java.Lang;
using Java.Util;

namespace hblink.Droid.Platform
{
    public class Bluetooth : BluetoothBase
    {
        class DeviceHolder : AbstractBluetoothDevice
        {
            public BluetoothDevice Device { get; set; }
            public override string Name => Device.Name;
        }

        private BluetoothManager _manager;
        private BluetoothLeScanner _scanner;
        private ScanCallback _scanCallback;
        private string _identifier;

        public Bluetooth(Context context)
        {
            _manager = (BluetoothManager)context.GetSystemService(Class.FromType(typeof(BluetoothManager)));
            _scanner = _manager.Adapter.BluetoothLeScanner;
            _scanCallback = new ScanCallback(this);
            _identifier = Injection.GetDeviceId(context);
        }

        public override void StartScan()
        {
            _scanner.StartScan(new[]{
                    new ScanFilter.Builder().SetServiceUuid(ParcelUuid.FromString(ServiceUuid)).Build()
                },
                new ScanSettings.Builder().SetScanMode(Android.Bluetooth.LE.ScanMode.LowLatency).Build(),
                _scanCallback);
        }

        public override void StopScan()
        {
            _scanner.StopScan(_scanCallback);
        }

        public override Connection TryConnect(AbstractBluetoothDevice device, TimeSpan timeout)
        {
            OnStatusUpdated("Connecting");
            var conn = new ConnectionSource();
            ((DeviceHolder)device).Device.ConnectGatt(null, false, new GattCallback(this, conn));
            Task.Delay(timeout).ContinueWith(_ => conn.Source.SetCanceled());
            return conn;
        }
        
        private class ScanCallback : Android.Bluetooth.LE.ScanCallback
        {
            private readonly Bluetooth _parent;

            public ScanCallback(Bluetooth parent)
            {
                _parent = parent;
            }

            public override void OnScanResult(ScanCallbackType callbackType, ScanResult result)
            {
                var device = result.Device;
                if (_parent.Devices.FirstOrDefault(d => ((DeviceHolder)d).Device.Address == device.Address) == null)
                {
                    _parent.Devices.Add(new DeviceHolder { Device = device });
                }
            }
        }

        private class GattCallback : BluetoothGattCallback
        {
            private readonly Bluetooth _parent;
            private readonly byte[] _identifier;
            private readonly BluetoothHandler _handler;

            private BluetoothGatt _gatt;
            private BluetoothGattCharacteristic _characteristic;

            public GattCallback(Bluetooth parent, ConnectionSource connection)
            {
                _parent = parent;
                _identifier = Encoding.UTF8.GetBytes(parent._identifier);
                _handler = new BluetoothHandler(connection)
                {
                    DisconnectDevice = Disconnect,
                    WriteValue = WriteValue,
                    UpdateStatus = _parent.OnStatusUpdated,
                };
            }

            public override void OnConnectionStateChange(BluetoothGatt gatt, GattStatus status, ProfileState newState)
            {
                if (newState == ProfileState.Connected)
                {
                    _parent.OnStatusUpdated("Requesting MTU");
                    _gatt = gatt;
                    gatt.RequestMtu(138);
                }
                else if (newState == ProfileState.Disconnected && status != GattStatus.Success)
                {
                    _handler.OnError();
                }
            }

            public override void OnMtuChanged(BluetoothGatt gatt, int mtu, GattStatus status)
            {
                _parent.OnStatusUpdated("Discovering services");
                gatt.DiscoverServices();
            }

            public override void OnServicesDiscovered(BluetoothGatt gatt, GattStatus status)
            {
                if (status == GattStatus.Success)
                {
                    _parent.OnStatusUpdated("Discovering characteristics");
                    var service = gatt.GetService(UUID.FromString(ServiceUuid));
                    _characteristic = service.GetCharacteristic(UUID.FromString(CharacteristicUuid));
                    EnableNotification();
                    _handler.Connect(_parent._manager.Adapter.Name, _identifier);
                }
            }

            private void EnableNotification()
            {
                _gatt.SetCharacteristicNotification(_characteristic, true);
                var descriptor = _characteristic.GetDescriptor(UUID.FromString(NotifyDescriptorUuid));
                descriptor.SetValue(BluetoothGattDescriptor.EnableNotificationValue.ToArray());
                _gatt.WriteDescriptor(descriptor);
            }

            private void Disconnect()
            {
                _gatt?.Disconnect();
            }

            private void WriteValue(byte[] value)
            {
                _characteristic?.SetValue(value);
                _gatt?.WriteCharacteristic(_characteristic);
            }

            public override void OnCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic)
            {
                _handler.OnReceive(characteristic.GetValue());
            }
        }
    }
}