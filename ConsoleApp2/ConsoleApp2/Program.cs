using System;
using System.Threading;
using IHA_BLE_LIB;

namespace ConsoleApp2
{
    class Program
    {
        static void Main(string[] args)
        {
            BluetoothLeHandler ble = new BluetoothLeHandler(500);
            ble.ConnectToDevice("DA:8C:4A:86:04:84");

            Console.WriteLine("Connected");

            Thread.Sleep(2000);

            var result = ble.ReadSamples(100);

            var result2 = ble.ReadSamples(200);
            
            // Should the driver handle how much data it has or should the MC handle how much data to send?
            
            
            Console.ReadLine();
            ble.Dispose();
        }
    }
}
