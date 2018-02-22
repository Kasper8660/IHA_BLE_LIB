using System;
using System.Collections.Generic;
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
            /*
            List<double> result = new List<double>();

            for (int i = 0; i < 3; i++)
            {
               var resultLoop = ble.ReadSamples(2000);
               foreach (var v in resultLoop)
               {
                    result.Add(v);
               }
            }
            */

            var resultLoop = ble.ReadSamples(200);

            Console.WriteLine("Done");
            ble.Dispose();
            Console.ReadLine();
        }
    }
}
