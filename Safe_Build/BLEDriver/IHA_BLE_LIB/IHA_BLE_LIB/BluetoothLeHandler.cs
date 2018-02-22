using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding;

namespace IHA_BLE_LIB
{
    /// <summary>
    /// This class has built in methods to use - to communicate with nRF52832 bluefruit microcontroller over BLE. It handles the bluetooth connection and data transfer.
    /// </summary>
    public class BluetoothLeHandler
    {
        private string _macAddress;
        private BluetoothLEDevice _currentDevice;
        private readonly double _sampleRate;

        private GattDeviceService _service;
        private GattCharacteristic _txCharacteristic;
        private GattCharacteristic _rxCharacteristic;

        // Service UUID to discover the service to use, and 2 characteristic UUID to communicate over TX and RX just like an UART.
        private static readonly Guid ServiceUuid = new Guid("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
        private static readonly Guid TxUuid = new Guid("6E400002-B5A3-F393-E0A9-E50E24DCCA9E");
        private static readonly Guid RxUuid = new Guid("6E400003-B5A3-F393-E0A9-E50E24DCCA9E");

        private bool IsConnected { get; set; }
        private bool _notDone;

        /// <summary>
        /// Constructor to initialize the samplerate.
        /// </summary>
        /// <param name="hz">Sample rate in hertz</param>
        public BluetoothLeHandler(int hz)
        {
            _sampleRate = hz;
        }

        /// <summary>
        /// This method sets up advertisement discover, so for each time an advertisement from Bluetooth low energy has received it calls the method OnAdvertisementReceived callback.
        /// </summary>
        /// <param name="mac">Part of MAC address from microcontroller</param>
        public void ConnectToDevice(string mac)
        {
            _macAddress = mac;
            // Create Bluetooth Listener
            var watcher = new BluetoothLEAdvertisementWatcher();

            DeviceInformation.CreateWatcher();

            watcher.ScanningMode = BluetoothLEScanningMode.Active;

            // Only activate the watcher when we're recieving values >= -80
            watcher.SignalStrengthFilter.InRangeThresholdInDBm = -80;

            // Stop watching if the value drops below -90 (user walked away)
            watcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -90;

            // Register callback for when we see an advertisements
            watcher.Received += OnAdvertisementReceived;


            // Wait 5 seconds to make sure the device is really out of range
            watcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(5000);
            watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(2000);

            // Starting watching for advertisements
            watcher.Start();

            // To hold the method, so the user dont call further before the connection has been etablished.
            while (!IsConnected) { }
            SetSamplerate();
        }

        /// <summary>
        /// Tells the microcontroller to stop read data and send it.
        /// </summary>
        public void StopSampling()
        {
            if (_currentDevice == null)
                return;
            WriteData("0x12");
        }

        /// <summary>
        /// This method is public accessed and calls async method, and hold itself from returning untill the async method is done, this way we make sure the user wont call other methods that will disturb the transfer.
        /// </summary>
        /// <param name="amountOfSamples">Amount of data the user wants from the microcontroller. Maximum 2000! If more is required, call the method multiple times.</param>
        /// <returns>List of data samples.</returns>
        public List<double> ReadSamples(int amountOfSamples)
        {
            if (amountOfSamples > 2000)
                amountOfSamples = 2000;

            var data = ReadXData(amountOfSamples);
            _notDone = false;
            while (!_notDone)
            { }
            return data.Result;
        }

        /// <summary>
        /// Cleaning up connections and variables.
        /// </summary>
        public void Dispose()
        {
            _currentDevice.DeviceInformation.Pairing.UnpairAsync();
            Thread.Sleep(3000);
            _rxCharacteristic = null;
            _txCharacteristic = null;
            _service.Dispose();
            _currentDevice.Dispose();
            _currentDevice = null;
            _service = null;
            try
            {
                GC.Collect();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// Read data from the bluetooth connection.
        /// </summary>
        /// <param name="amountOfSamples">Amount of data to read</param>
        /// <returns>List with data</returns>
        private async Task<List<double>> ReadXData(int amountOfSamples)
        {
            List<double> samples = new List<double>();
            // Sending command 0x14 which indicates the following number is the amount of samples.
            WriteData("0x14" + amountOfSamples);
            // While loop that keeps reading data untill the amountOfSamples has been reached.
            while (samples.Count < amountOfSamples)
            {
                // Data message indicates for the microcontroller to send new data, since the callback method does not work with the current version of windows.
                WriteData("DATA");
                var result = await WaitForData();
                if (result != null)
                {
                    var resultInString = System.Text.Encoding.Default.GetString(result);
                    if (resultInString.Contains(";"))
                    {
                        var resultInByteArray = resultInString.Split(';');
                        foreach (var value in resultInByteArray)
                        {
                            samples.Add(double.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
                        }
                    }
                    else
                    {
                        samples.Add(double.Parse(resultInString, System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
            }

            // Send message that we are done reading data
            WriteData("0x12");
            _notDone = true;
            return samples;
        }

        /// <summary>
        /// Setting sample rate on microcontroller.
        /// </summary>
        private void SetSamplerate()
        {
            if (_currentDevice == null)
                return;
            // Converting hertz to miliseconds, which is what the microcontroller uses.
            var samplerateInMs = 1 / _sampleRate * 1000;
            // 0x13 command indiates that the following number is the amount of ms the loop should run in.
            WriteData("0x13" + samplerateInMs);
        }
        
        /// <summary>
        /// Waits for data and reads it. When data has been read it will return the data.
        /// </summary>
        /// <returns>Byte of data from ble communication.</returns>
        private async Task<byte[]> WaitForData()
        {
            var ready = false;
            while (!ready)
            {
                try
                {
                    // Remmember to use .Uncached, otherwise it will keep reading the same data all the time, since it will use the cached data by windows which will not be updated with the new data.
                    var dataFromMicrocontroller = await _rxCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);

                    if (dataFromMicrocontroller.Status == GattCommunicationStatus.Success)
                    {
                        byte[] input = new byte[DataReader.FromBuffer(dataFromMicrocontroller.Value).UnconsumedBufferLength];
                        DataReader.FromBuffer(dataFromMicrocontroller.Value).ReadBytes(input);

                        // Utilize the data as needed
                        DataReader.FromBuffer(dataFromMicrocontroller.Value).Dispose();
                        return input;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            return null;
        }

        /// <summary>
        /// Advertisement method, which will be called each time the device gets an advertisement request.
        /// </summary>
        /// <param name="watcher"></param>
        /// <param name="btAdv"></param>
        private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs btAdv)
        {
            if (_currentDevice != null && _rxCharacteristic != null && _txCharacteristic != null)
            {
                return;
            }
            // Tell the user we see an advertisement and print some properties
            Console.WriteLine("Advertisement:");
            Console.WriteLine("BT_ADDR: {0}", btAdv.BluetoothAddress);
            Console.WriteLine("FR_NAME: {0}", btAdv.Advertisement.LocalName);

            // Converting bluetoothaddress to macaddress
            // Then compares if the macaddress equals the expected, if true connect
            ulong input = btAdv.BluetoothAddress;
            var tempMac = input.ToString("X");
            var regex = "(.{2})(.{2})(.{2})(.{2})(.{2})(.{2})";
            var replace = "$1:$2:$3:$4:$5:$6";
            var macAddress = Regex.Replace(tempMac, regex, replace);

            if (macAddress == _macAddress)
            {
                Debug.WriteLine($"---------------------- {btAdv.Advertisement.LocalName} ----------------------");
                Debug.WriteLine($"Advertisement Data: {btAdv.Advertisement.ServiceUuids.Count}");
                var device = await BluetoothLEDevice.FromBluetoothAddressAsync(btAdv.BluetoothAddress);
                if (device != null)
                {
                    var result = await device.DeviceInformation.Pairing.PairAsync(DevicePairingProtectionLevel.None);
                    Debug.WriteLine($"Pairing Result: {result.Status}");
                    Debug.WriteLine($"Connected Data: {device.GattServices.Count}");
                }

                _currentDevice = await BluetoothLEDevice.FromIdAsync(device.DeviceId);

                if (_currentDevice != null)
                {
                    GattDeviceServicesResult serviceResult = await _currentDevice.GetGattServicesAsync();
                    if (serviceResult.Status == GattCommunicationStatus.Success)
                    {
                        var services = serviceResult.Services;
                        Debug.WriteLine("Getting services");

                        foreach (var serv in services)
                        {
                            // Searching for the correct service to use.
                            if (serv.Uuid == ServiceUuid)
                            {
                                Debug.WriteLine("Found Service!");
                                _service = serv;
                            }
                        }

                        GattCharacteristicsResult characteristicsResult = await _service.GetCharacteristicsAsync();
                        Debug.WriteLine("Getting characteristics");
                        Debug.WriteLine(characteristicsResult.Status);
                        if (characteristicsResult.Status == GattCommunicationStatus.Success)
                        {
                            var characteristics = characteristicsResult.Characteristics;
                            foreach (var chara in characteristics)
                            {
                                // Searching for the correct characteristics in the service to use.
                                if (chara.Uuid == RxUuid)
                                {
                                    Debug.WriteLine("Found RX");
                                    _rxCharacteristic = chara;
                                }
                                if (chara.Uuid == TxUuid)
                                {
                                    Debug.WriteLine("Found TX");
                                    _txCharacteristic = chara;
                                }
                            }

                            // Below code is enableing the data callback which does not work at the current windows version.
                            /*
                            if (_rxCharacteristic != null)
                            {
                                // Register the event handler for receiving notifications
                                _rxCharacteristic.ValueChanged += Characteristic_ValueChanged;

                                // In order to avoid unnecessary communication with the device, determine if the device is already 
                                // correctly configured to send notifications.
                                // By default ReadClientCharacteristicConfigurationDescriptorAsync will attempt to get the current
                                // value from the system cache and communication with the device is not typically required.
                                var currentDescriptorValue = await _rxCharacteristic.ReadClientCharacteristicConfigurationDescriptorAsync();
                                var status = await _rxCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                            }
                            */
                        }
                    }

                    if (_rxCharacteristic != null && _txCharacteristic != null)
                    {
                        Debug.WriteLine("Setting IsConnected to true");
                        IsConnected = true;
                    }
                }
            }
            Console.WriteLine();
        }

        /// <summary>
        /// This is the callback method that will never be executed since the callback does not work on the current windows version.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// 
        void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            Debug.WriteLine("Callback on data received!");
            // An Indicate or Notify reported that the value has changed.
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            // Parse the data however required.
            // The encoding and byte order need to match the settings of the writer 
            // we previously used.
            reader.UnicodeEncoding = UnicodeEncoding.Utf8;
            reader.ByteOrder = ByteOrder.LittleEndian;

            var receivedStrings = "";

            // Keep reading until we consume the complete stream.
            while (reader.UnconsumedBufferLength > 0)
            {
                // Note that the call to readString requires a length of "code units" 
                // to read. This is the reason each string is preceded by its length 
                // when "on the wire".
                uint bytesToRead = reader.ReadUInt32();
                receivedStrings += reader.ReadString(bytesToRead) + "\n";
            }
            reader.Dispose();
            Console.WriteLine(receivedStrings);
        }

        /// <summary>
        /// This method writes data to microcontroller over bluetooth low energy protocol.
        /// </summary>
        /// <param name="msg">The message in string to send. Max 20 characters.</param>
        private async void WriteData(string msg)
        {
            if (_currentDevice == null)
                return;

            var writer = new DataWriter();
            // WriteByte used for simplicity. Other commmon functions - WriteInt16 and WriteSingle
            writer.WriteString(msg);

            GattCommunicationStatus test = await _txCharacteristic.WriteValueAsync(writer.DetachBuffer());
            if (test == GattCommunicationStatus.Success)
            {
                // Successfully wrote to device
            }
            writer.Dispose();
            _notDone = true;
        }
    }
}

