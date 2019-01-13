﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace HitachiEIP {

   #region Public Enums

   public enum mem {
      BigEndian,
      LittleEndian
   }

   public enum Protocol {
      TCP = 6
   }

   public enum EIP_Type {
      RegisterSession = 0x0065,
      UnRegisterSession = 0x0066,
      SendRRData = 0x006F,
      SendUnitData = 0x0070,
   }

   public enum Data_Type {
      ConnectedAddressItem = 0xa1,
      ConnectedDataItem = 0xb1,
      UnconnectedDataItem = 0xb2,
   }

   public enum EIP_Command {
      Null = 0,
      ForwardOpen = 0x54,
      ForwardClose = 0x4e,
   }

   public enum Segment {
      Class = 0x20,
      Instance = 0x24,
      Attribute = 0x30,
   }

   #endregion

   #region EtherNetIP Definitions

   // Access codes
   public enum eipAccessCode {
      Set = 0x32,
      Get = 0x33,
      Service = 0x34,
   }

   // Class Code enum values
   //   The value of the class code enum is
   //     -- 0x = the values that follow are in Hexadecimal 
   //     -- aa = enums are listed in ascending numerical order.  These two Hex
   //             digits cause the sort order to also be in alphabetical order
   //     -- 0000 = Reserved but not currently used
   //     -- vv = The Hex value assigned in the Hitachi EtherNet/IP document

   // Class codes
   public enum eipClassCode {
      Print_data_management_function = 0x07000066,
      Print_format_function = 0x08000067,
      Print_specification_function = 0x09000068,
      Calendar_function = 0x01000069,
      User_pattern_function = 0x0C00006B,
      Substitution_rules_function = 0x0A00006C,
      Enviroment_setting_function = 0x03000071,
      Unit_Information_function = 0x0B000073,
      Operation_management_function = 0x06000074,
      IJP_operation_function = 0x04000075,
      Count_function = 0x02000079,
      Index_function = 0x0500007A,
   }

   // Attribute enum values
   //   The value of the class code enum is
   //     -- 0x = the values that follow are in Hexadecimal 
   //     -- aa = enums are listed in ascending numerical order.  These two Hex
   //             digits cause the sort order to also be in alphabetical order
   //     -- ll = Length of the data to be sent to the printer
   //     -- cc = 00000abc - Right 3 Bit indicate which access codes are valid for this service
   //             a = Allow Set if a = 1
   //             b = Allow Get if b = 1
   //             c = Allow Service if c = 1
   //     -- vv = The Hex value assigned in the Hitachi EtherNet/IP document

   // Attributes within Print Data Management class 0x66
   public enum eipPrint_Data_Management_function {
      Select_Message = 0x09000464,
      Store_Print_Data = 0x0A000165,
      Delete_Print_Data = 0x03000167,
      Print_Data_Name = 0x07000169,
      List_of_Messages = 0x0600026A,
      Print_Data_Number = 0x0800016B,
      Change_Create_Group_Name = 0x0100016C,
      Group_Deletion = 0x0400016D,
      List_of_Groups = 0x0500026F,
      Change_Group_Number = 0x02000170,
   }

   // Attributes within Print Format class 0x67
   public enum eipPrint_format_function {
      Message_Name = 0x14010264,
      Print_Item = 0x19010265,
      Number_Of_Columns = 0x15010266,
      Format_Type = 0x0E010267,
      Insert_Column = 0x0F010469,
      Delete_Column = 0x0801046A,
      Add_Column = 0x0101046B,
      Number_Of_Print_Line_And_Print_Format = 0x1601016C,
      Format_Setup = 0x0D01016D,
      Adding_Print_Items = 0x0301046E,
      Deleting_Print_Items = 0x0901046F,
      Print_Character_String = 0x18010371,
      Line_Count = 0x12010372,
      Line_Spacing = 0x13010373,
      Dot_Matrix = 0x0B010374,
      InterCharacter_Space = 0x10010375,
      Character_Bold = 0x07010376,
      Barcode_Type = 0x05010377,
      Readable_Code = 0x1B010378,
      Prefix_Code = 0x17010379,
      X_and_Y_Coordinate = 0x1C01037A,
      InterCharacter_SpaceII = 0x1101037B,
      Add_To_End_Of_String = 0x0201018A,
      Calendar_Offset = 0x0601038D,
      DIN_Print = 0x0A01038E,
      EAN_Prefix = 0x0C01038F,
      Barcode_Printing = 0x04010390,
      QR_Error_Correction_Level = 0x1A010391,
   }

   // Attributes within Print Specification class 0x68
   public enum eipPrint_specification_function {
      Character_Height = 0x02010364,
      Ink_Drop_Use = 0x08010365,
      High_Speed_Print = 0x06010366,
      Character_Width = 0x04020367,
      Character_Orientation = 0x03010368,
      Print_Start_Delay_Forward = 0x0B020369,
      Print_Start_Delat_Reverse = 0x0A02036A,
      Product_Speed_Matching = 0x0E01036B,
      Pulse_Rate_Division_Factor = 0x0F02036C,
      Speed_Compensation = 0x1201036D,
      Line_Speed = 0x0902036E,
      Distance_Between_Print_Head_And_Object = 0x0501036F,
      Print_Target_Width = 0x0D010370,
      Actual_Print_Width = 0x01010371,
      Repeat_Count = 0x10020372,
      Repeat_Interval = 0x11030373,
      Target_Sensor_Timer = 0x15020374,
      Target_Sensor_Filter = 0x14010375,
      Targer_Sensor_Filter_Value = 0x13020376,
      Ink_Drop_Charge_Rule = 0x07010377,
      Print_Start_Position_Adjustment_Value = 0x0C020378,
   }

   // Attributes within Calendar class 0x69
   public enum eipCalendar_function {
      Shift_Count_Condition = 0x0A010265,
      First_Calendar_Block_Number = 0x03010266,
      Calendar_Block_Number_In_Item = 0x01010267,
      Offset_Year = 0x08010368,
      Offset_Month = 0x07010369,
      Offset_Day = 0x0402036A,
      Offset_Hour = 0x0502036B,
      Offset_Minute = 0x0602036C,
      Zero_Suppress_Year = 0x2001036D,
      Zero_Suppress_Month = 0x1E01036E,
      Zero_Suppress_Day = 0x1A01036F,
      Zero_Suppress_Hour = 0x1C010370,
      Zero_Suppress_Minute = 0x1D010371,
      Zero_Suppress_Weeks = 0x1F010372,
      Zero_Suppress_Day_Of_Week = 0x1B010373,
      Substitute_Rule_Year = 0x15010374,
      Substitute_Rule_Month = 0x13010375,
      Substitute_Rule_Day = 0x0F010376,
      Substitute_Rule_Hour = 0x11010377,
      Substitute_Rule_Minute = 0x12010378,
      Substitute_Rule_Weeks = 0x14010379,
      Substitute_Rule_Day_Of_Week = 0x1001037A,
      Time_Count_Start_Value = 0x1803037B,
      Time_Count_End_Value = 0x1603037C,
      Time_Count_Reset_Value = 0x1703037D,
      Reset_Time_Value = 0x0901037E,
      Update_Interval_Value = 0x1901037F,
      Shift_Start_Hour = 0x0D010380,
      Shift_Start_Minute = 0x0E010381,
      Shift_End_Hour = 0x0B010382,
      Shift_End_Minute = 0x0C010383,
      Count_String_Value = 0x02010384,
   }

   // Attributes within User Pattern class 0x6B
   public enum eipUser_pattern_function { // 0x6B
      User_Pattern_Fixed = 0x01000364,
      User_Pattern_Free = 0x02000365,
   }

   // Attributes within Substitution Rules class 0x6C
   public enum eipSubstitution_rules_function { // 0x6C
      Number = 0x03000364,
      Name = 0x02000365,
      Start_Year = 0x01000366,
      Year = 0x0A000367,
      Month = 0x08000368,
      Day = 0x04000369,
      Hour = 0x0600036A,
      Minute = 0x0700036B,
      Week = 0x0900036C,
      Day_Of_Week = 0x0500036D,
   }

   // Attributes within Enviroment Setting class 0x71
   public enum eipEnviroment_setting_function {
      Current_Time = 0x05000365,
      Calendar_Date_Time = 0x01000366,
      Calendar_Date_Time_Availibility = 0x02000367,
      Clock_System = 0x04000368,
      User_Environment_Information = 0x08000269,
      Cirulation_Control_Setting_Value = 0x0300026A,
      Usage_Time_Of_Circulation_Control = 0x0700016B,
      Reset_Usage_Time_Of_Citculation_Control = 0x0600016C,
   }

   // Attributes within Unit Information class 0x73
   public enum eipUnit_Information_function {
      Unit_Information = 0x14000264,
      Model_Name = 0x0F00026B,
      Serial_Number = 0x1100026C,
      Ink_Name = 0x0800026D,
      Input_Mode = 0x0900026E,
      Maximum_Character_Count = 0x0B00026F,
      Maximum_Registered_Message_Count = 0x0D000270,
      Barcode_Information = 0x01000271,
      Usable_Character_Size = 0x15000272,
      Maximum_Calendar_And_Count = 0x0A000273,
      Maximum_Substitution_Rule = 0x0E000274,
      Shift_Code_And_Time_Count = 0x12000275,
      Chimney_And_DIN_Print = 0x03000276,
      Maximum_Line_Count = 0x0C000277,
      Basic_Software_Version = 0x02000278,
      Controller_Software_Version = 0x04000279,
      Engine_M_Software_Version = 0x0500027A,
      Engine_S_Software_Version = 0x0600027B,
      First_Language_Version = 0x0700027C,
      Second_Language_Version = 0x1000027D,
      Software_Option_Version = 0x1300027E,
   }

   // Attributes within Operation Management class 0x74
   public enum eipOperation_management_function {
      Operating_Management = 0x0C000264,
      Ink_Operating_Time = 0x09020365,
      Alarm_Time = 0x01020366,
      Print_Count = 0x0D020367,
      Communications_Environment = 0x03000268,
      Cumulative_Operation_Time = 0x04000269,
      Ink_And_Makeup_Name = 0x0800026A,
      Ink_Viscosity = 0x0B00026B,
      Ink_Pressure = 0x0A00026C,
      Ambient_Temperature = 0x0200026D,
      Deflection_Voltage = 0x0500026E,
      Excitation_VRef_Setup_Value = 0x0700026F,
      Excitation_Frequency = 0x06000270,
   }

   // Attributes within IJP Operation class 0x75
   public enum eipIJP_operation_function {
      Remote_operation_information = 0x07000264,
      Fault_and_warning_history = 0x04000266,
      Operating_condition = 0x06000267,
      Warning_condition = 0x0A000268,
      Date_and_time_information = 0x0100026A,
      Error_code = 0x0300026B,
      Start_Remote_Operation = 0x0800046C,
      Stop_Remote_Operation = 0x0900046D,
      Deflection_voltage_control = 0x0200046E,
      Online_Offline = 0x0500036F,
   }

   // Attributes within Count class 0x79
   public enum eipCount_function {
      Number_Of_Count_Block = 0x0C000266,
      Initial_Value = 0x09000367,
      Count_Range_1 = 0x04000368,
      Count_Range_2 = 0x05000369,
      Update_Unit_Halfway = 0x0F00036A,
      Update_Unit_Unit = 0x1000036B,
      Increment_Value = 0x0800036C,
      Direction_Value = 0x0700036D,
      Jump_From = 0x0A00036E,
      Jump_To = 0x0B00036F,
      Reset_Value = 0x0D000370,
      Type_Of_Reset_Signal = 0x0E000371,
      Availibility_Of_External_Count = 0x01000372,
      Availibility_Of_Zero_Suppression = 0x02000373,
      Count_Multiplier = 0x03000374,
      Count_Skip = 0x06000375,
   }

   // Attributes within Index class 0x7A
   public enum eipIndex_function {
      Start_Stop_Management_Flag = 0x0A000364,
      Automatic_reflection = 0x01000365,
      Item_Count = 0x06000366,
      Column = 0x04000367,
      Line = 0x07000368,
      Character_position = 0x03000369,
      Print_Data_Message_Number = 0x0900036A,
      Print_Data_Group_Data = 0x0800036B,
      Substitution_Rules_Setting = 0x0B00036C,
      User_Pattern_Size = 0x0C00036D,
      Count_Block = 0x0500036E,
      Calendar_Block = 0x0200036F,
   }

   #endregion
   
   public class EIP {

      #region Events

      // Event Logging
      internal event LogHandler Log;
      internal delegate void LogHandler(EIP sender, string msg);

      #endregion

      #region Declarations/Properties

      public Int32 port;
      public string IPAddress;

      TcpClient client = null;
      NetworkStream stream = null;

      public uint SessionID { get; set; } = 0;

      public eipAccessCode Access { get; set; }
      public eipClassCode Class { get; set; }
      public byte Instance { get; set; } = 1;
      public byte Attribute { get; set; } = 1;
      public byte DataLength { get; set; } = 1;
      public ulong Data { get; set; } = 1;
      public uint O_T_ConnectionID { get; set; } = 0;
      public uint T_O_ConnectionID { get; set; } = 0;

      public bool IsConnected {
         get { return client != null && stream != null && client.Connected; }
      }

      public bool SessionIsOpen {
         get { return SessionID > 0; }
      }

      public bool ForwardIsOpen {
         get { return O_T_ConnectionID > 0; }
      }

      #endregion

      #region Methods

      // Connect to Hitachi printer
      public bool Connect(string IPAddress, Int32 port) {
         bool result = false;
         try {
            client = new TcpClient(IPAddress, port);
            stream = client.GetStream();
            result = true;
            LogIt("Connect Complete!");
         } catch (Exception e) {
            LogIt(e.Message);
         }
         return result;
      }

      // Disconnect from Hitachi printer
      public bool Disconnect() {
         bool result = false;
         try {
            stream.Close();
            stream = null;
            client.Close();
            client = null;
            result = true;
            LogIt("Disconnect Complete!");
         } catch (Exception e) {
            LogIt(e.Message);
         }
         return result;
      }

      // Start EtherNet/IP Session
      public void StartSession() {
         byte[] ed = EIP_Wrapper(EIP_Type.RegisterSession, EIP_Command.Null);
         Write(ed, 0, ed.Length);

         byte[] data;
         Int32 bytes;
         if (Read(out data, out bytes) && bytes >= 8) {
            SessionID = Utils.Get(data, 4, 4, mem.LittleEndian);
         } else {
            SessionID = 0;
         }
         LogIt("Session Started!");
      }

      // End EtherNet/IP Session
      internal void EndSession() {
         byte[] ed = EIP_Wrapper(EIP_Type.UnRegisterSession, EIP_Command.Null);
         Write(ed, 0, ed.Length);

         byte[] data;
         Int32 bytes;
         if (Read(out data, out bytes)) {

         }
         SessionID = 0;
         LogIt("Session Ended!");
     }

      // Start EtherNet/IP Forward Open
      public void ForwardOpen() {
         byte[] ed = EIP_Wrapper(EIP_Type.SendRRData, EIP_Command.ForwardOpen);
         Write(ed, 0, ed.Length);

         byte[] data;
         Int32 bytes;
         if (Read(out data, out bytes) && bytes >= 52) {
            O_T_ConnectionID = Utils.Get(data, 44, 4, mem.LittleEndian);
            T_O_ConnectionID = Utils.Get(data, 48, 4, mem.LittleEndian);
         } else {
            O_T_ConnectionID = 0;
            T_O_ConnectionID = 0;
         }
         LogIt("Forward Open!");
      }

      // End EtherNet/IP Forward Open
      public void ForwardClose() {
         byte[] ed = EIP_Wrapper(EIP_Type.SendRRData, EIP_Command.ForwardClose);
         Write(ed, 0, ed.Length);

         byte[] data;
         Int32 bytes;
         if (!Read(out data, out bytes)) {

         }
         O_T_ConnectionID = 0;
         T_O_ConnectionID = 0;
         LogIt("Forward Close!");
      }

      // Read response to EtherNet/IP request
      public bool Read(out byte[] data, out int bytes) {
         bool result = false;
         data = new byte[256];
         bytes = -1;
         try {
            for (int t = 0; t < 20; t++) {
               if (stream.DataAvailable) {
                  bytes = stream.Read(data, 0, data.Length);
                  break;
               }
               Thread.Sleep(50);
            }
            result = bytes > 0;
         } catch (Exception e) {
            LogIt(e.Message);
         }
         return result;
      }

      // Issue EtherNet/IP request
      public bool Write(byte[] data, int start, int length) {
         bool result = false;
         try {
            stream.Write(data, start, length);
            result = true;
         } catch (Exception e) {
            LogIt(e.Message);
         }
         return result;
      }

      // Handles Hitachi Get, Set, and Service
      public byte[] EIP_Hitachi(EIP_Type t, eipAccessCode c) {
         List<byte> packet = new List<byte>();
         switch (c) {
            case eipAccessCode.Get:
               Utils.Add(packet, (ulong)t, 2);                                 // Command
               Utils.Add(packet, (ulong)30, 2);                                // Length of added data at end
               Utils.Add(packet, (ulong)SessionID, 4);                         // Session ID
               Utils.Add(packet, (ulong)0, 4);                                 // Success
               Utils.Add(packet, (ulong)0x0200030000008601, 8, mem.BigEndian); // Sender Context
               Utils.Add(packet, (ulong)0, 4);                                 // option flags
               Utils.Add(packet, (ulong)0, 4);                                 // option interface handle
               Utils.Add(packet, (ulong)30, 2);                                // Timeout
               Utils.Add(packet, (ulong)2, 2);                                 // Item count

               // Item #1
               Utils.Add(packet, (ulong)Data_Type.ConnectedAddressItem, 2); // Connected address type
               Utils.Add(packet, (ulong)4, 2);                              // length of 4
               Utils.Add(packet, O_T_ConnectionID, 4);                      // O->T Connection ID

               // Item #2
               Utils.Add(packet, (ulong)Data_Type.ConnectedDataItem, 2);    // data tyoe
               Utils.Add(packet, (ulong)10, 2);                             // length of 10
               Utils.Add(packet, (ulong)2, 2);                              // Count Sequence
               Utils.Add(packet, (byte)c, 3);                               // Hitachi command and count
               Utils.Add(packet, (byte)Segment.Class, (byte)Class);         // Class
               Utils.Add(packet, (byte)Segment.Instance, (byte)Instance);   // Instance
               Utils.Add(packet, (byte)Segment.Attribute, (byte)Attribute); // Attribute
               break;
            case eipAccessCode.Set:
            case eipAccessCode.Service:
               Utils.Add(packet, (ulong)t, 2);                                 // Command
               Utils.Add(packet, (ulong)(30 + DataLength), 2);                 // Length of added data at end
               Utils.Add(packet, (ulong)SessionID, 4);                         // Session ID
               Utils.Add(packet, (ulong)0, 4);                                 // Success
               Utils.Add(packet, (ulong)0x0200030000008601, 8, mem.BigEndian); // Sender Context
               Utils.Add(packet, (ulong)0, 4);                                 // option flags
               Utils.Add(packet, (ulong)0, 4);                                 // option interface handle
               Utils.Add(packet, (ulong)30, 2);                                // Timeout
               Utils.Add(packet, (ulong)2, 2);                                 // Item count

               // Item #1
               Utils.Add(packet, (ulong)Data_Type.ConnectedAddressItem, 2); // Connected address type
               Utils.Add(packet, (ulong)4, 2);                              // length of 4
               Utils.Add(packet, O_T_ConnectionID, 4);                      // O->T Connection ID

               // Item #2
               Utils.Add(packet, (ulong)Data_Type.ConnectedDataItem, 2);    // data tyoe
               Utils.Add(packet, (ulong)(10 + DataLength), 2);              // length of 10 + data length
               Utils.Add(packet, (ulong)2, 2);                              // Count Sequence
               Utils.Add(packet, (byte)c, 3);                               // Hitachi command and count
               Utils.Add(packet, (byte)Segment.Class, (byte)Class);         // Class
               Utils.Add(packet, (byte)Segment.Instance, (byte)Instance);   // Instance
               Utils.Add(packet, (byte)Segment.Attribute, (byte)Attribute); // Attribute
               Utils.Add(packet, Data, DataLength);                         // Data

               break;
         }
         return packet.ToArray<byte>();
      }

      // handles Register Session, Unregister Session, Send RR Data(Forward Open, Forward Close)
      public byte[] EIP_Wrapper(EIP_Type t, EIP_Command c) {
         List<byte> packet = new List<byte>();
         switch (t) {
            case EIP_Type.RegisterSession:
               Utils.Add(packet, (ulong)t, 2); // Command
               Utils.Add(packet, (ulong)4, 2); // Length of added data at end
               Utils.Add(packet, (ulong)0, 4); // Session ID (Not set yet)
               Utils.Add(packet, (ulong)0, 4); // Status to be returned
               Utils.Add(packet, (ulong)0, 8); // Sender Context (Do not understand yet)
               Utils.Add(packet, (ulong)0, 4); // Options
               Utils.Add(packet, (ulong)1, 2); // Protocol Version
               Utils.Add(packet, (ulong)0, 2); // Flags
               break;
            case EIP_Type.UnRegisterSession:
               Utils.Add(packet, (ulong)t, 2);         // Command
               Utils.Add(packet, (ulong)0, 2);         // Length of added data at end
               Utils.Add(packet, (ulong)SessionID, 4); // Session ID (Not set yet)
               Utils.Add(packet, (ulong)0, 4);         // Status to be returned
               Utils.Add(packet, (ulong)0, 8);         // Sender Context (Do not understand yet)
               Utils.Add(packet, (ulong)0, 4);         // Options
               break;
            case EIP_Type.SendRRData:
               switch (c) {
                  case EIP_Command.ForwardOpen:
                     Utils.Add(packet, (ulong)t, 2);         // Command
                     Utils.Add(packet, (ulong)62, 2);        // Length of added data at end
                     Utils.Add(packet, (ulong)SessionID, 4); // Session ID
                     Utils.Add(packet, (ulong)0, 4);         // Success
                     Utils.Add(packet, (ulong)0x0200030000008601, 8, mem.BigEndian); // Pertinant to sender(Unknown for now)
                     Utils.Add(packet, (ulong)0, 4);         // option flags
                     Utils.Add(packet, (ulong)0, 4);         // option interface handle
                     Utils.Add(packet, (ulong)30, 2);        // Timeout
                     Utils.Add(packet, (ulong)2, 2);         // Item count
                     Utils.Add(packet, (ulong)0, 2);         // Null type
                     Utils.Add(packet, (ulong)0, 2);         // length of 0
                     Utils.Add(packet, (ulong)Data_Type.UnconnectedDataItem, 2); // data tyoe
                     Utils.Add(packet, (ulong)46, 2);        // length of 46

                     // Common Packet
                     Utils.Add(packet, (ulong)c, 1);         // Forward open
                     Utils.Add(packet, (ulong)02, 1);        // Requested path size
                     Utils.Add(packet, (byte)Segment.Class, 6);           // Class
                     Utils.Add(packet, (byte)Segment.Instance, Instance); // Instance
                     Utils.Add(packet, (ulong)7, 1);         // Priority/Time
                     Utils.Add(packet, (ulong)0xea, 1);      // Timeout Ticks
                     Utils.Add(packet, (ulong)0, 4);         // O->T Connection ID Random Number
                     Utils.Add(packet, 0x98000340, 4, mem.BigEndian);  // T->O Connection ID Random number
                     Utils.Add(packet, (ulong)0x98, 2);      // Connection Serial Number random number
                     Utils.Add(packet, (ulong)0, 2);         // vendor ID
                     Utils.Add(packet, (ulong)0, 4);         // Originator Serial number
                     Utils.Add(packet, (ulong)0, 1);         // Connection Timeout Multiplier
                     Utils.Add(packet, (ulong)0, 3);         // Reserved

                     Utils.Add(packet, 10000000, 4);         // O->T RPI
                     Utils.Add(packet, 0xff, 0x43);          // O->T Network Connection Parameters
                     Utils.Add(packet, 10000000, 4);         // T->O RPI
                     Utils.Add(packet, 0xff, 0x43);          // T->O Network Connection Parameters
                     Utils.Add(packet, (ulong)0xa3, 1);      // Transport type/trigger
                     Utils.Add(packet, (ulong)2, 1);         // Connection Path Size in 16-bit words
                     Utils.Add(packet, (byte)Segment.Class, 2);           // Class
                     Utils.Add(packet, (byte)Segment.Instance, Instance); // Instance
                     break;
                  case EIP_Command.ForwardClose:
                     Utils.Add(packet, (ulong)t, 2);         // Command
                     Utils.Add(packet, (ulong)34, 2);        // Length of added data at end
                     Utils.Add(packet, (ulong)SessionID, 4); // Session ID
                     Utils.Add(packet, (ulong)0, 4);         // Success
                     Utils.Add(packet, (ulong)0x0100030000008601, 8, mem.BigEndian); // Pertinant to sender(Unknown for now)
                     Utils.Add(packet, (ulong)0, 4);         // option flags
                     Utils.Add(packet, (ulong)0, 4);         // option interface handle
                     Utils.Add(packet, (ulong)30, 2);        // Timeout
                     Utils.Add(packet, (ulong)2, 2);         // Item count
                     Utils.Add(packet, (ulong)0, 2);         // Null type
                     Utils.Add(packet, (ulong)0, 2);         // length of 0
                     Utils.Add(packet, (ulong)Data_Type.UnconnectedDataItem, 2); // data tyoe
                     Utils.Add(packet, (ulong)18, 2);        // length of 46

                     // Common Packet
                     Utils.Add(packet, (ulong)c, 1);         // Forward open
                     Utils.Add(packet, (ulong)02, 1);        // Requested path size
                     Utils.Add(packet, (byte)Segment.Class, 6);           // Class
                     Utils.Add(packet, (byte)Segment.Instance, Instance); // Instance
                     Utils.Add(packet, (ulong)7, 1);         // Priority/Time
                     Utils.Add(packet, (ulong)0xea, 1);      // Timeout Ticks
                     Utils.Add(packet, (ulong)0x98, 2);      // Connection Serial Number random number
                     Utils.Add(packet, (ulong)0, 2);         // vendor ID
                     Utils.Add(packet, (ulong)0, 4);         // Originator Serial number
                     Utils.Add(packet, (ulong)0, 1);         // Connection Path Size
                     Utils.Add(packet, (ulong)0, 1);         // Reserved
                     break;
               }
               break;
         }
         return packet.ToArray<byte>();
      }

      // Get only the Functions(Attributes) that Apply to this Access Code
      public int GetDropDowns(eipAccessCode code, Type EnumType, ComboBox cb, out uint[] values) {

         // Get all names associated with the enumeration
         string[] allNames = EnumType.GetEnumNames();
         int[] allValues = (int[])EnumType.GetEnumValues();

         // Get the mask for selecting the needed ones
         int mask = 0;
         switch (code) {
            case eipAccessCode.Set:
               mask = 1 << 8;
               break;
            case eipAccessCode.Get:
               mask = 2 << 8;
               break;
            case eipAccessCode.Service:
               mask = 4 << 8;
               break;
            default:
               break;
         }

         // Weed out the unused ones
         List<string> name = new List<string>();
         List<uint> value = new List<uint>();
         for (int i = 0; i < allValues.Length; i++) {
            int x = allValues[i];
            if ((x & mask) > 0) {
               name.Add(allNames[i].Replace('_', ' '));
               value.Add((uint)allValues[i]);
            }
         }

         // Fix up the Attributes combo box
         string savedText = cb.Text;
         cb.Text = string.Empty;
         cb.Items.Clear();
         cb.Items.AddRange(name.ToArray());
         if (cb.FindStringExact(savedText) >= 0) {
            cb.Text = savedText;
         }

         // Return the used ones
         values = value.ToArray();
         return values.Length;
      }

      // Get attribute Human readable name
      public string GetAttributeName(eipClassCode c, uint v) {
         switch (c) {
            case eipClassCode.Print_data_management_function:
               return ((eipPrint_Data_Management_function)v).ToString();
            case eipClassCode.Print_format_function:
               return ((eipPrint_format_function)v).ToString();
            case eipClassCode.Print_specification_function:
               return ((eipPrint_specification_function)v).ToString();
            case eipClassCode.Calendar_function:
               return ((eipCalendar_function)v).ToString();
            case eipClassCode.User_pattern_function:
               return ((eipUser_pattern_function)v).ToString();
            case eipClassCode.Substitution_rules_function:
               return ((eipSubstitution_rules_function)v).ToString();
            case eipClassCode.Enviroment_setting_function:
               return ((eipEnviroment_setting_function)v).ToString();
            case eipClassCode.Unit_Information_function:
               return ((eipUnit_Information_function)v).ToString();
            case eipClassCode.Operation_management_function:
               return ((eipOperation_management_function)v).ToString();
            case eipClassCode.IJP_operation_function:
               return ((eipIJP_operation_function)v).ToString();
            case eipClassCode.Count_function:
               return ((eipCount_function)v).ToString();
            case eipClassCode.Index_function:
               return ((eipIndex_function)v).ToString();
            default:
               break;
         }
         return "Unknown";
      }

      #endregion

      #region Service Routines

      private void LogIt(string msg) {
         if (Log != null) {
            Log(this, msg);
         }
      }

      #endregion

   }
}
