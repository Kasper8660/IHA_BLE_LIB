using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace IHA_BLE_LIB
{
    // From following stackoverflow showing it is nessesary for the callback to work with the current version.
    // https://stackoverflow.com/questions/46271156/ble-win-api-can-not-get-notification
    // Issue addressed here https://social.msdn.microsoft.com/Forums/en-US/58da3fdb-a0e1-4161-8af3-778b6839f4e1/bluetooth-bluetoothledevicefromidasync-does-not-complete-on-10015063?forum=wdk

    class CoInitializeSec
    {
        [DllImport("ole32.dll")]
        public static extern int CoInitializeSecurity(IntPtr pVoid, int
        cAuthSvc, IntPtr asAuthSvc, IntPtr pReserved1, RPCAUTHNLEVEL level,
        RPCIMPLEVEL impers, IntPtr pAuthList, EOAUTHNCAP dwCapabilities, IntPtr
        pReserved3);

        public enum RPCAUTHNLEVEL
        {
            Default = 0,
            None = 1,
            Connect = 2,
            Call = 3,
            Pkt = 4,
            PktIntegrity = 5,
            PktPrivacy = 6
        }

        public enum RPCIMPLEVEL
        {
            Default = 0,
            Anonymous = 1,
            Identify = 2,
            Impersonate = 3,
            Delegate = 4
        }

        public enum EOAUTHNCAP
        {
            None = 0x00,
            MutualAuth = 0x01,
            StaticCloaking = 0x20,
            DynamicCloaking = 0x40,
            AnyAuthority = 0x80,
            MakeFullSIC = 0x100,
            Default = 0x800,
            SecureRefs = 0x02,
            AccessControl = 0x04,
            AppID = 0x08,
            Dynamic = 0x10,
            RequireFullSIC = 0x200,
            AutoImpersonate = 0x400,
            NoCustomMarshal = 0x2000,
            DisableAAA = 0x1000
        }
    }
}
