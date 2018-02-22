# IHA_BLE_LIB
BLE project for IHA 

This project has 2 folders. A safe solution which will work safe, but is slower. (This solution is polling on data received)
The other project is a little hack to make the last windows update work with BLE_Callback, which is ALOT faster but if changes happens to ble library it might have an impact. (This solution uses event on data received)
To get the DLL, build the project and find it in the debug/release folder. 
The DLL found in the outter folder should always be the newest DLL version of the latest build with the latest changes. But make the DLL youself to always be sure to have the latest version.

Max value for retrieving values is 2000 in parameter. If more is required, call the method multiple times.

On setup with analog discovery, remmember to make an offset, which makes sure theres no negative values in the graph. For testing i used 2V offset, so all values were between 1V-3V.


For the ble library to work in a Class Library project following needs to be added to the .csproj file.     

This has been done for this project, it is only on NEW projects this is needed. This tells the program to include this librarys on WinRT.

<Reference Include="Windows.Foundation.FoundationContract">
      <HintPath>..\..\..\..\..\..\..\Program Files (x86)\Windows Kits\10\References\10.0.15063.0\Windows.Foundation.FoundationContract\3.0.0.0\Windows.Foundation.FoundationContract.winmd</HintPath>
    </Reference>
    <Reference Include="Windows.Foundation.UniversalApiContract">
      <HintPath>..\..\..\..\..\..\..\Program Files (x86)\Windows Kits\10\References\10.0.15063.0\Windows.Foundation.UniversalApiContract\4.0.0.0\Windows.Foundation.UniversalApiContract.winmd</HintPath>
    </Reference>