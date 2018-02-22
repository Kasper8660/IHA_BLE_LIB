#include <bluefruit.h>
#include <string.h>
#include <SPI.h>

//Below used to print out macaddress for device in serial debugger.
typedef volatile uint32_t REG32;
#define pREG32 (REG32 *)


#define DEVICE_ID_HIGH    (*(pREG32 (0x10000060)))
#define DEVICE_ID_LOW     (*(pREG32 (0x10000064)))
#define MAC_ADDRESS_HIGH  (*(pREG32 (0x100000a8)))
#define MAC_ADDRESS_LOW   (*(pREG32 (0x100000a4)))

// BLEuart is how the communication will work. As an TX and RX - use this instance.
BLEUart bleuart;

// ADC values

// float mv_per_lsb = 3600.0F / 256.0F; // 8-bit
// float mv_per_lsb = 3600.0F / 1024.0F; // 10-bit ADC with 3.6V input range
// float mv_per_lsb = 3600.0F / 4096.0F; // 12-bit
float mv_per_lsb = 3600.0F / 16384.0F; // 14-bit

String fullRequestMessage;
int delayTime;
int dataRequestCounter = 0;
int counter;
int amountOfSamples;
int dataToSend = 0;
float analogData;
float measurementData[2080];
boolean beginSendSeqence = false;
boolean stopSending = false;

void setup() {
  Serial.begin(115200);
  StopSending();
  String mac = GetMAC();
  
  // ADC setup  
  analogReference(AR_INTERNAL);
  analogReadResolution(14);  

  Bluefruit.begin();
  // Setting device name to MAC address.
  char __dataFileName[sizeof(mac)];
  mac.toCharArray(__dataFileName, sizeof(__dataFileName));
   
  Bluefruit.setName("IHA!!");
  Bluefruit.setTxPower(4);
  bleuart.begin();

  // Setup callbacks
  SetupCallbacks();
}

void loop()
{
  delay(delayTime);

    if(beginSendSeqence)
    {
      if(counter < 2080)
      {
        analogData = analogRead(A0);
        float measurementDataDouble = (float)analogData * mv_per_lsb;
        measurementDataDouble = measurementDataDouble/1000;
        Serial.println(String(measurementDataDouble));
        measurementData[counter] = measurementDataDouble;
        if(counter > 80 && counter < 85)
        {
          bleuart.write("DATA");
        }
        counter++;        
       }   
     }
}
  

void SetupCallbacks()
{
  bleuart.setRxCallback(Bleuart_RX_Callback);
  Bluefruit.setConnectCallback(Connect_Callback);
  Bluefruit.setDisconnectCallback(Disconnect_Callback);
  StartAdv();
  Bluefruit.Advertising.start();
}


void StartAdv()
{
  Bluefruit.Advertising.addFlags(BLE_GAP_ADV_FLAGS_LE_ONLY_GENERAL_DISC_MODE);
  Bluefruit.Advertising.addTxPower();
  Bluefruit.Advertising.addService(bleuart); // Adding NUS service
  Bluefruit.ScanResponse.addName();
  Bluefruit.Advertising.start(0);              
}

void Connect_Callback(uint16_t conn_handle)
{
  Serial.println("Start of connect_callback");
  char peer_name[32] = { 0 };
  Bluefruit.Gap.getPeerName(conn_handle, peer_name, sizeof(peer_name));
  Serial.print("Connected to: ");
  Serial.println(peer_name);
  Serial.println("-----------------------------");
}

void Disconnect_Callback(uint16_t conn_handle, uint8_t reason)
{
  Serial.println("Disconnected");
}

void Bleuart_RX_Callback(void)
{
  // Read data
  fullRequestMessage = ReadData();
  Serial.println("Received message: " + fullRequestMessage);
  
  if (fullRequestMessage == "0x12")
  {
    // Stop reading data from analog pin
    // Stop command
    stopSending = true;
    StopSending();
    dataToSend = 0;
    counter = 0;
  }
  if (fullRequestMessage.substring(0,4) == "0x13")
  {
    // Sample rate      
    fullRequestMessage.remove(0, 4);
    
    delayTime = fullRequestMessage.toInt();
    Serial.println(String(delayTime));
  }
  
  if(fullRequestMessage.substring(0,4) == "0x14")
  {
    counter = 0;
    stopSending = false;
    fullRequestMessage.remove(0, 4);
    amountOfSamples = fullRequestMessage.toInt();
    // Begin to sample
    beginSendSeqence = true;    
  }
  
  if (fullRequestMessage = "DATA")
  {   
    // Return if it shouldnt send any more data
    if(stopSending)
    {
      return;
    }

    // Check if all 4 packets has been send, since each tranmission from MC is 4 packets on the C# driver. Which means we get DATA request 4 times before sending new data.
    if(dataRequestCounter < 3)
    {
      dataRequestCounter++;
      return;
    }
    String dataFirst;
    int arraySize = 0;
    Serial.println(dataToSend);
    if(dataToSend < 2080)
    {        
        dataFirst = String((measurementData[dataToSend]), 4) + ";" + String((measurementData[dataToSend+1]), 4) + ";" + String((measurementData[dataToSend+2]), 4)
        + ";" + String((measurementData[dataToSend+3]), 4) + ";" + String((measurementData[dataToSend+4]), 4) + ";" + String((measurementData[dataToSend+5]), 4)
        + ";" + String((measurementData[dataToSend+6]), 4) + ";" + String((measurementData[dataToSend+7]), 4) + ";" + String((measurementData[dataToSend+8]), 4)
        + ";" + String((measurementData[dataToSend+9]), 4) + ";" + String((measurementData[dataToSend+10]), 4);
        arraySize = dataFirst.length() + 1;                

        //Copy the string to an char array, which can be send over ble.      
        char copy[arraySize];
        dataFirst.toCharArray(copy, arraySize);
        Serial.println(copy);
        dataRequestCounter = 0;
        dataToSend += 11;              
        bleuart.write(copy); 
    }         
  }
}

// Reads data from bleuart buffer
String ReadData()
{
  char tempData[20] = { 0 };
  bleuart.read(tempData, 20);
  return String(tempData);
}

void StopSending()
{
  beginSendSeqence = false;
  delayTime = 100;
}

String GetMAC()
{
  // MAC Address
  uint32_t addr_high = ((MAC_ADDRESS_HIGH) & 0x0000ffff) | 0x0000c000;
  uint32_t addr_low  = MAC_ADDRESS_LOW;
  String MAC;
  MAC = String((addr_high >> 8) & 0xFF, HEX);
  MAC += ":";
  MAC += String((addr_high) & 0xFF, HEX);
  MAC += ":";
  MAC += String((addr_low >> 24) & 0xFF, HEX);
  MAC += ":";
  MAC += String((addr_low >> 16) & 0xFF, HEX);
  MAC += ":";
  MAC += String((addr_low >> 8) & 0xFF, HEX);
  MAC += ":";
  MAC += String((addr_low) & 0xFF, HEX);
  MAC.toUpperCase();

  Serial.println(MAC);
  return MAC;
}
