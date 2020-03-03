using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CoreBluetooth;
using Foundation;
using hblink.Platform;
using hblink.Shared.Platform;
using UIKit;

namespace hblink.iOS.Platform
{
    public class Bluetooth : BluetoothBase
    {
        class DeviceHolder : AbstractBluetoothDevice
        {
            public CBPeripheral Device { get; set; }
            public override string Name => Device.Name;
        }

        private CBCentralManager _manager;

        public Bluetooth()
        {
            _manager = new CBCentralManager(new CentralDelegate(this), null);
        }

        public override void StartScan()
        {
            _manager.ScanForPeripherals(CBUUID.FromString(ServiceUuid));
        }

        public override void StopScan()
        {
            _manager.StopScan();
        }

        public override Connection TryConnect(AbstractBluetoothDevice device, TimeSpan timeout)
        {
            OnStatusUpdated("Connecting");
            var peripheral = ((DeviceHolder)device).Device;
            var connection = new ConnectionSource();
            peripheral.Delegate = new PeripheralDelegate(this, connection);
            _manager.ConnectPeripheral(peripheral);
            return connection;
        }

        class CentralDelegate : CBCentralManagerDelegate
        {
            private readonly Bluetooth _parent;

            public CentralDelegate(Bluetooth parent)
            {
                _parent = parent;
            }

            public override void UpdatedState(CBCentralManager central)
            {
            }

            public override void DiscoveredPeripheral(CBCentralManager central, CBPeripheral peripheral, NSDictionary advertisementData,
                NSNumber RSSI)
            {
                var identifier = peripheral.Identifier.AsString();
                if (_parent.Devices.FirstOrDefault(d => ((DeviceHolder)d).Device.Identifier.AsString() == identifier) == null)
                {
                    _parent.Devices.Add(new DeviceHolder { Device = peripheral });
                }
            }

            public override void ConnectedPeripheral(CBCentralManager central, CBPeripheral peripheral)
            {
                _parent.OnStatusUpdated("Discovering services");
                peripheral.DiscoverServices(new[] { CBUUID.FromString(ServiceUuid) });
            }
        }

        class PeripheralDelegate : CBPeripheralDelegate
        {
            private readonly CBCentralManager _manager;
            private readonly Bluetooth _parent;
            private readonly string _adapterName;
            private readonly BluetoothHandler _handler;
            private CBPeripheral _peripheral;
            private CBCharacteristic _characteristic;

            public PeripheralDelegate(Bluetooth parent, ConnectionSource connection)
            {
                _manager = parent._manager;
                _parent = parent;
                _adapterName = UIDevice.CurrentDevice.Name;
                _handler = new BluetoothHandler(connection)
                {
                    WriteValue = WriteValue,
                    DisconnectDevice = Disconnect,
                    UpdateStatus = _parent.OnStatusUpdated,
                };
            }

            public override void DiscoveredService(CBPeripheral peripheral, NSError error)
            {
                _parent.OnStatusUpdated("Discovering characteristics");
                _peripheral = peripheral;
                var service = peripheral.GetService(ServiceUuid);
                peripheral.DiscoverCharacteristics(service);
            }

            public override void DiscoveredCharacteristic(CBPeripheral peripheral, CBService service, NSError error)
            {
                _characteristic = service.GetCharacteristic(CharacteristicUuid);
                EnableNotification();
                _handler.Connect(_adapterName, Encoding.UTF8.GetBytes(Injection.GetDeviceId()));
            }

            private void EnableNotification()
            {
                _peripheral.SetNotifyValue(true, _characteristic);
            }

            private void Disconnect()
            {
                _manager.CancelPeripheralConnection(_peripheral);
            }

            private void WriteValue(byte[] value)
            {
                _peripheral.WriteValue(NSData.FromArray(value), _characteristic, CBCharacteristicWriteType.WithResponse);
            }

            public override void UpdatedCharacterteristicValue(CBPeripheral peripheral, CBCharacteristic characteristic, NSError error)
            {
                _handler.OnReceive(characteristic.Value.ToArray());
            }
        }
    }

    static class CBExt
    {
        public static CBService GetService(this CBPeripheral peripheral, string uuid)
        {
            var cbuuid = CBUUID.FromString(uuid);
            return peripheral.Services.FirstOrDefault(s => s.UUID == cbuuid);
        }

        public static CBCharacteristic GetCharacteristic(this CBService service, string uuid)
        {
            var cbuuid = CBUUID.FromString(uuid);
            return service.Characteristics.FirstOrDefault(c => c.UUID == cbuuid);
        }
    }
}