﻿using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace EIP_Lib {

   public partial class EIP {

      #region Data Declarations

      enum ItemType {
         Unknown = 0,
         Text = 1,
         Date = 2,
         Counter = 3,
         Logo = 4,
         Link = 5,     // Not supported in the printer
         Prompt = 6,   // Not supported in the printer
      }

      // Braced Characters (count, date, half-size, logos
      char[] bc = new char[] { 'C', 'Y', 'M', 'D', 'h', 'm', 's', 'T', 'W', '7', 'E', 'F', ' ', '\'', '.', ';', ':', '!', ',', 'X', 'Z' };

      // Attributes of braced characters
      enum ba {
         Count = 1 << 0,
         Year = 1 << 1,
         Month = 1 << 2,
         Day = 1 << 3,
         Hour = 1 << 4,
         Minute = 1 << 5,
         Second = 1 << 6,
         Julian = 1 << 7,
         Week = 1 << 8,
         DayOfWeek = 1 << 9,
         Shift = 1 << 10,
         TimeCount = 1 << 11,
         Space = 1 << 12,
         Quote = 1 << 13,
         Period = 1 << 14,
         SemiColon = 1 << 15,
         Colon = 1 << 16,
         Exclamation = 1 << 17,
         Comma = 1 << 18,
         FixedPattern = 1 << 19,
         FreePattern = 1 << 20,
         Unknown = 1 << 21,
         //DateCode = (1 << 12) - 2, // All the date codes combined
      }

      const int DateCode =
         (int)ba.Year | (int)ba.Month | (int)ba.Day | (int)ba.Hour | (int)ba.Minute | (int)ba.Second |
         (int)ba.Julian | (int)ba.Week | (int)ba.DayOfWeek | (int)ba.Shift | (int)ba.TimeCount;

      const int DateSubZS =
         (int)ba.Year | (int)ba.Month | (int)ba.Day | (int)ba.Hour | (int)ba.Minute |
         (int)ba.Week | (int)ba.DayOfWeek;

      #endregion

      // Flag for Attribute Not Present
      const string N_A = "N!A";

      #region Send XML to Printer

      // Send xml file to printer
      public bool SendXmlToPrinter(string FileName) {
         XmlDocument xmlDoc = new XmlDocument();
         xmlDoc.PreserveWhitespace = true;
         xmlDoc.Load(FileName);
         return SendXmlToPrinter(xmlDoc);
      }

      // Send xlmDoc from file to printer
      public bool SendXmlToPrinter(XmlDocument xmlDoc) {
         // Need a XMP Document to continue
            if (xmlDoc == null) {
               return false;
            }
         bool success = true;
         if (StartSession()) {
            if (ForwardOpen()) {
               try {
                  // Set to only one item in printer
                  CleanDisplay();
                  XmlNode lab = xmlDoc.SelectSingleNode("Label");
                  foreach (XmlNode l in lab.ChildNodes) {
                     if (l is XmlWhitespace)
                        continue;
                     switch (l.Name) {
                        case "Printer":
                           SendPrinterSettings(l);            // Send printer wide settings
                           break;
                        case "Objects":
                           AllocateRowsColumns(l.ChildNodes); // Allocate rows and columns
                           LoadObjects(l.ChildNodes);         // Send the objects one at a time
                           break;
                     }
                  }
               } catch (EIPIOException e1) {
                  // In case of an EIP I/O error
                  string name = $"{GetAttributeName(e1.ClassCode, e1.Attribute)}";
                  string msg = $"EIP I/O Error on {e1.AccessCode}/{e1.ClassCode}/{name}";
                  MessageBox.Show(msg, "EIP I/O Error", MessageBoxButtons.OK);
                  success = false;
               } catch (Exception e2) {
                  // You are on your own here
               }
            }
            ForwardClose();
         }
         EndSession();
         return success;
      }

      // Simulate Delete All But One
      public void CleanDisplay() {
         GetAttribute(ccPF.Number_Of_Columns, out int cols); // Get the number of columns
         if (cols > 1) {                                     // No need to delete columns if there is only one
            SetAttribute(ccIDX.Column, 1);                   // Select to continuously delete column 2 (0 origin on deletes)
            while (--cols > 0) {                             // Delete all but one column
               ServiceAttribute(ccPF.Delete_Column, 0);
            }
         }
         SetAttribute(ccIDX.Item, 1);                    // Select item 1 (1 origin on Line Count)
         SetAttribute(ccPF.Line_Count, 1);               // Set line count to 1. (In case column 1 has multiple lines)
         SetAttribute(ccPF.Dot_Matrix, "5x8");           // Clear any barcodes
         SetAttribute(ccPF.Barcode_Type, "None");
         SetAttribute(ccPF.Print_Character_String, "1"); // Set simple text in case Calendar or Counter was used
      }

      // Send the Printer Wide Settings
      private bool SendPrinterSettings(XmlNode pr) {
         bool success = true;
         foreach (XmlNode c in pr.ChildNodes) {
            switch (c.Name) {
               case "PrintHead":
                  SetAttribute(ccPS.Character_Orientation, GetXmlAttr(c, "Orientation"));
                  break;
               case "ContinuousPrinting":
                  SetAttribute(ccPS.Repeat_Interval, GetXmlAttr(c, "RepeatInterval"));
                  SetAttribute(ccPS.Repeat_Count, GetXmlAttr(c, "PrintsPerTrigger"));
                  break;
               case "TargetSensor":
                  SetAttribute(ccPS.Target_Sensor_Filter, GetXmlAttr(c, "Filter"));
                  SetAttribute(ccPS.Targer_Sensor_Filter_Value, GetXmlAttr(c, "SetupValue"));
                  SetAttribute(ccPS.Target_Sensor_Timer, GetXmlAttr(c, "Timer"));
                  break;
               case "CharacterSize":
                  SetAttribute(ccPS.Character_Width, GetXmlAttr(c, "Width"));
                  SetAttribute(ccPS.Character_Width, GetXmlAttr(c, "Height"));
                  break;
               case "PrintStartDelay":
                  SetAttribute(ccPS.Print_Start_Delay_Reverse, GetXmlAttr(c, "Reverse"));
                  SetAttribute(ccPS.Print_Start_Delay_Forward, GetXmlAttr(c, "Forward"));
                  break;
               case "EncoderSettings":
                  SetAttribute(ccPS.High_Speed_Print, GetXmlAttr(c, "HighSpeedPrinting"));
                  SetAttribute(ccPS.Pulse_Rate_Division_Factor, GetXmlAttr(c, "Divisor"));
                  SetAttribute(ccPS.Product_Speed_Matching, GetXmlAttr(c, "ExternalEncoder"));
                  break;
               case "InkStream":
                  SetAttribute(ccPS.Ink_Drop_Use, GetXmlAttr(c, "InkDropUse"));
                  SetAttribute(ccPS.Ink_Drop_Charge_Rule, GetXmlAttr(c, "ChargeRule"));
                  break;
               case "TwinNozzle":
                  // Not supported in EtherNet/IP
                  //this.LeadingCharacterControl = GetAttr(c, "LeadingCharControl", 0);
                  //this.LeadingCharacterControlWidth1 = GetAttr(c, "LeadingCharControlWidth1", 32);
                  //this.LeadingCharacterControlWidth1 = GetAttr(c, "LeadingCharControlWidth2", 32);
                  //this.NozzleSpaceAlignment = GetAttr(c, "NozzleSpaceAlignment", 0);
                  break;
               case "Substitution":
                  SendSubstitution(c);
                  break;
            }
         }
         return success;
      }

      // Set all the values for a single substitution rule
      private bool SendSubstitution(XmlNode p) {
         bool success = true;
         AttrData attr;
         byte[] data;

         // Get the standard attributes for substitution
         string rule = GetXmlAttr(p, "Rule");
         string startYear = GetXmlAttr(p, "StartYear");
         string delimiter = GetXmlAttr(p, "Delimiter");

         // Avoid user errors
         if (int.TryParse(rule, out int ruleNumber) && int.TryParse(startYear, out int year) && delimiter.Length == 1) {

            // Sub Substitution rule in Index class
            attr = EIP.AttrDict[ClassCode.Index, (byte)ccIDX.Substitution_Rule];
            data = FormatOutput(attr.Set, ruleNumber);
            SetAttribute(ClassCode.Index, (byte)ccIDX.Substitution_Rule, data);

            // Set the start year in the substitution rule
            attr = AttrDict[ClassCode.Index, (byte)ccSR.Start_Year];
            data = FormatOutput(attr.Set, year);
            SetAttribute(ClassCode.Substitution_rules, (byte)ccSR.Start_Year, data);

            // Load the individual rules
            foreach (XmlNode c in p.ChildNodes) {
               switch (c.Name) {
                  case "Year":
                     SetSubValues(ccSR.Year, c, delimiter);
                     break;
                  case "Month":
                     SetSubValues(ccSR.Month, c, delimiter);
                     break;
                  case "Day":
                     SetSubValues(ccSR.Day, c, delimiter);
                     break;
                  case "Hour":
                     SetSubValues(ccSR.Hour, c, delimiter);
                     break;
                  case "Minute":
                     SetSubValues(ccSR.Minute, c, delimiter);
                     break;
                  case "Week":
                     SetSubValues(ccSR.Week, c, delimiter);
                     break;
                  case "DayOfWeek":
                     SetSubValues(ccSR.Day_Of_Week, c, delimiter);
                     break;
                  case "Skip":
                     // Do not process these nodes
                     break;
               }
            }
         }
         return success;
      }

      // Set the substitution values for a class
      private bool SetSubValues(ccSR attribute, XmlNode c, string delimeter) {
         bool success = true;
         // Avoid user errors
         if (int.TryParse(GetXmlAttr(c, "Base"), out int b)) {
            Prop prop = EIP.AttrDict[ClassCode.Substitution_rules, (byte)attribute].Set;
            string[] s = GetXmlValue(c).Split(delimeter[0]);
            for (int i = 0; i < s.Length && success; i++) {
               int n = b + i;
               // Avoid user errors
               if (n >= prop.Min && n <= prop.Max) {
                  byte[] data = FormatOutput(prop, n, 1, s[i]);
                  SetAttribute(ClassCode.Substitution_rules, (byte)attribute, data);
               }
            }
         }
         return success;
      }

      // Allocate rows, columns, and inner-line spacing 
      private bool AllocateRowsColumns(XmlNodeList objs) {
         bool success = true;
         int[] columns = new int[100];
         int[] ILS = new int[100];
         int maxCol = 0;
         // Collect information about rows and columns (both 1-origin in XML file)
         foreach (XmlNode obj in objs) {
            if (obj is XmlWhitespace)
               continue;
            XmlNode l = obj.SelectSingleNode("Location");
            if (int.TryParse(GetXmlAttr(l, "Row"), out int row)
               && int.TryParse(GetXmlAttr(l, "Column"), out int col)
               && int.TryParse(GetXmlAttr(obj.SelectSingleNode("Font"), "InterLineSpace"), out int ils)) {
               columns[col] = Math.Max(columns[col], row);
               ILS[col] = Math.Max(ILS[col], ils);
               maxCol = Math.Max(maxCol, col);
            } else {
               return false;
            }
         }
         // Allocate the rows and columns
         for (int col = 1; col <= maxCol && success; col++) {
            if (columns[col] == 0) {
               return false;
            }
            if (col > 1) {
               ServiceAttribute(ccPF.Add_Column);
            }
            // Should this be Column and not Item?
            SetAttribute(ccIDX.Item, col);
            SetAttribute(ccPF.Line_Count, columns[col]);
            if (columns[col] > 1) {
               SetAttribute(ccIDX.Column, col);
               SetAttribute(ccPF.Line_Spacing, ILS[col]);
            }
         }
         return success;
      }

      // Load objects
      private bool LoadObjects(XmlNodeList objs) {
         bool success = true;
         XmlNode n;
         foreach (XmlNode obj in objs) {
            if (obj is XmlWhitespace)
               continue;

            // Get the item number of the object
            n = obj.SelectSingleNode("Location");
            if (!int.TryParse(GetXmlAttr(n, "ItemNumber"), out int item)) {
               return false;
            }
            SetAttribute(ccIDX.Item, item);
            SetAttribute(ccPF.Print_Character_String, GetXmlValue(obj.SelectSingleNode("Text"))); // Load the text

            n = obj.SelectSingleNode("Font");
            SetAttribute(ccPF.Dot_Matrix, n.InnerText);
            SetAttribute(ccPF.InterCharacter_Space, GetXmlAttr(n, "InterCharacterSpace"));
            SetAttribute(ccPF.Character_Bold, GetXmlAttr(n, "IncreasedWidth"));

            ItemType type = (ItemType)Enum.Parse(typeof(ItemType), GetXmlAttr(obj, "Type"), true);
            switch (type) {
               case ItemType.Date:
                  LoadCalendar(obj);
                  break;
               case ItemType.Counter:
                  LoadCount(obj);
                  break;
            }
         }
         return success;
      }

      // Send Calendar related information
      private bool LoadCalendar(XmlNode obj) {
         bool success = true;
         XmlNode n;

         // Get data assigned by the printer
         GetAttribute(ccCal.First_Calendar_Block, out int FirstCalBlock);
         GetAttribute(ccCal.Number_of_Calendar_Blocks, out int CalBlockCount);

         for (int block = 0; block < CalBlockCount && success; block++) {
            foreach (XmlNode d in obj) {
               if (d is XmlWhitespace)
                  continue;
               if (d.Name == "Date") {
                  if (int.TryParse(GetXmlAttr(d, "Block"), out int b)) {
                     if (b == block + 1) {
                        SetAttribute(ccIDX.Calendar_Block, FirstCalBlock + block);
                        n = d.SelectSingleNode("Offset");
                        if (n != null) {
                           foreach (XmlAttribute a in n.Attributes) {
                              switch (a.Name) {
                                 case "Year":
                                    SetAttribute(ccCal.Offset_Year, a.Value);
                                    break;
                                 case "Month":
                                    SetAttribute(ccCal.Offset_Month, a.Value);
                                    break;
                                 case "Day":
                                    SetAttribute(ccCal.Offset_Day, a.Value);
                                    break;
                                 case "Hour":
                                    SetAttribute(ccCal.Offset_Hour, a.Value);
                                    break;
                                 case "Minute":
                                    SetAttribute(ccCal.Offset_Minute, a.Value);
                                    break;
                              }
                           }
                        }

                        n = d.SelectSingleNode("ZeroSuppress");
                        if (n != null) {
                           foreach (XmlAttribute a in n.Attributes) {
                              switch (a.Name) {
                                 case "Year":
                                    SetAttribute(ccCal.Zero_Suppress_Year, a.Value);
                                    break;
                                 case "Month":
                                    SetAttribute(ccCal.Zero_Suppress_Month, a.Value);
                                    break;
                                 case "Day":
                                    SetAttribute(ccCal.Zero_Suppress_Day, a.Value);
                                    break;
                                 case "Hour":
                                    SetAttribute(ccCal.Zero_Suppress_Hour, a.Value);
                                    break;
                                 case "Minute":
                                    SetAttribute(ccCal.Zero_Suppress_Minute, a.Value);
                                    break;
                                 case "Week":
                                    SetAttribute(ccCal.Zero_Suppress_Weeks, a.Value);
                                    break;
                                 case "DayOfWeek":
                                    SetAttribute(ccCal.Zero_Suppress_Day_Of_Week, a.Value);
                                    break;
                              }
                           }
                        }

                        n = d.SelectSingleNode("EnableSubstitution");
                        if (n != null) {
                           foreach (XmlAttribute a in n.Attributes) {
                              switch (a.Name) {
                                 case "Year":
                                    SetAttribute(ccCal.Substitute_Year, a.Value);
                                    break;
                                 case "Month":
                                    SetAttribute(ccCal.Substitute_Month, a.Value);
                                    break;
                                 case "Day":
                                    SetAttribute(ccCal.Substitute_Day, a.Value);
                                    break;
                                 case "Hour":
                                    SetAttribute(ccCal.Substitute_Hour, a.Value);
                                    break;
                                 case "Minute":
                                    SetAttribute(ccCal.Substitute_Minute, a.Value);
                                    break;
                                 case "Week":
                                    SetAttribute(ccCal.Substitute_Weeks, a.Value);
                                    break;
                                 case "DayOfWeek":
                                    SetAttribute(ccCal.Substitute_Day_Of_Week, a.Value);
                                    break;
                              }
                           }
                        }

                        n = d.SelectSingleNode("TimeCount");
                        if (n != null) {
                           foreach (XmlAttribute a in n.Attributes) {
                              switch (a.Name) {
                                 case "Start":
                                    SetAttribute(ccCal.Time_Count_Start_Value, a.Value);
                                    break;
                                 case "End":
                                    SetAttribute(ccCal.Time_Count_End_Value, a.Value);
                                    break;
                                 case "Reset":
                                    SetAttribute(ccCal.Time_Count_Reset_Value, a.Value);
                                    break;
                                 case "ResetTime":
                                    SetAttribute(ccCal.Reset_Time_Value, a.Value);
                                    break;
                                 case "RenewalPeriod":
                                    SetAttribute(ccCal.Update_Interval_Value, a.Value);
                                    break;
                              }
                           }
                        }

                        n = d.SelectSingleNode("Shift");
                        if (n != null) {
                           if (int.TryParse(GetXmlAttr(n, "Shift"), out int shift)) {
                              SetAttribute(ccIDX.Calendar_Block, shift);
                              foreach (XmlAttribute a in n.Attributes) {
                                 switch (a.Name) {
                                    case "StartHour":
                                       SetAttribute(ccCal.Shift_Start_Hour, a.Value);
                                       break;
                                    case "StartMinute":
                                       SetAttribute(ccCal.Shift_Start_Minute, a.Value);
                                       break;
                                    case "EndHour": // Read Only
                                       //SetAttribute(ccCal.Shift_End_Hour, a.Value);
                                       break;
                                    case "EndMinute": // Read Only
                                       //SetAttribute(ccCal.Shift_End_Minute, a.Value);
                                       break;
                                    case "ShiftCode":
                                       SetAttribute(ccCal.Shift_Code_Condition, a.Value);
                                       break;
                                 }
                              }
                           }
                        }
                     }
                  }
               }
            }
         }
         return success;
      }

      // Send counter related information
      private bool LoadCount(XmlNode obj) {
         bool success = true;

         // Get data assigned by the printer
         GetAttribute(ccCount.First_Count_Block, out int FirstCountBlock);
         GetAttribute(ccCount.Number_Of_Count_Blocks, out int CountBlockCount);

         for (int block = 0; block < FirstCountBlock && success; block++) {
            foreach (XmlNode c in obj) {
               if (c is XmlWhitespace)
                  continue;
               if (c.Name == "Counter") {
                  if (int.TryParse(GetXmlAttr(c, "Block"), out int b)) {
                     if (b == block + 1) {
                        SetAttribute(ccIDX.Count_Block, CountBlockCount + block);
                        foreach (XmlAttribute a in c.Attributes) {
                           switch (a.Name) {
                              case "InitialValue":
                                 SetAttribute(ccCount.Initial_Value, a.Value);
                                 break;
                              case "Range1":
                                 SetAttribute(ccCount.Count_Range_1, a.Value);
                                 break;
                              case "Range2":
                                 SetAttribute(ccCount.Count_Range_2, a.Value);
                                 break;
                              case "UpdateIP":
                                 SetAttribute(ccCount.Update_Unit_Halfway, a.Value);
                                 break;
                              case "UpdateUnit":
                                 SetAttribute(ccCount.Update_Unit_Unit, a.Value);
                                 break;
                              case "Increment":
                                 SetAttribute(ccCount.Increment_Value, a.Value);
                                 break;
                              case "CountUp":
                                 string s = bool.TryParse(a.Value, out bool dir) && dir ? "Up" : "Down";
                                 SetAttribute(ccCount.Direction_Value, s);
                                 break;
                              case "JumpFrom":
                                 SetAttribute(ccCount.Jump_From, a.Value);
                                 break;
                              case "JumpTo":
                                 SetAttribute(ccCount.Jump_To, a.Value);
                                 break;
                              case "Reset":
                                 SetAttribute(ccCount.Reset_Value, a.Value);
                                 break;
                              case "ResetSignal":
                                 SetAttribute(ccCount.Type_Of_Reset_Signal, a.Value);
                                 break;
                              case "ExternalSignal":
                                 SetAttribute(ccCount.External_Count, a.Value);
                                 break;
                              case "ZeroSuppression":
                                 SetAttribute(ccCount.Zero_Suppression, a.Value);
                                 break;
                              case "Multiplier":
                                 SetAttribute(ccCount.Count_Multiplier, a.Value);
                                 break;
                              case "Skip":
                                 SetAttribute(ccCount.Count_Skip, a.Value);
                                 break;
                           }
                        }
                     }
                  }
               }
            }
         }
         return success;
      }

      #endregion

      #region Retrieve XML from the printer

      // Generate an XMP Doc form the current printer settings
      public string ConvertLayoutToXML() {
         string xml = string.Empty;
         ItemType itemType = ItemType.Text;
         using (MemoryStream ms = new MemoryStream()) {
            using (XmlTextWriter writer = new XmlTextWriter(ms, Encoding.GetEncoding("UTF-8"))) {
               writer.Formatting = Formatting.Indented;
               writer.WriteStartDocument();
               if (StartSession(true)) {
                  if (ForwardOpen()) {
                     try {
                        writer.WriteStartElement("Label"); // Start Label
                        {
                           writer.WriteAttributeString("Version", "1");
                           WritePrinterSettings(writer);
                           writer.WriteStartElement("Objects"); // Start Objects
                           {
                              int item = 0;
                              GetAttribute(ccPF.Number_Of_Columns, out int colCount);
                              for (int col = 1; col <= colCount; col++) {
                                 SetAttribute(ccIDX.Column, col);
                                 GetAttribute(ccPF.Line_Count, out int LineCount);
                                 for (int row = LineCount; row > 0; row--) {
                                    SetAttribute(ccIDX.Item, ++item);
                                    GetAttribute(ccPF.Print_Character_String, out string text);
                                    GetAttribute(ccCal.Number_of_Calendar_Blocks, out int calBlocks);
                                    GetAttribute(ccCount.Number_Of_Count_Blocks, out int cntBlocks);
                                    int[] mask = new int[1 + Math.Max(calBlocks, cntBlocks)];
                                    itemType = GetItemType(text, ref mask);
                                    writer.WriteStartElement("Object"); // Start Object
                                    {
                                       writer.WriteAttributeString("Type", Enum.GetName(typeof(ItemType), itemType));

                                       WriteFont(writer);

                                       WriteLocation(writer, item, row, col);

                                       switch (itemType) {
                                          case ItemType.Text:
                                             break;
                                          case ItemType.Date:
                                             // Missing multiple calendar block logic
                                             WriteCalendarSettings(writer, mask);
                                             break;
                                          case ItemType.Counter:
                                             // Missing multiple counter block logic
                                             WriteCounterSettings(writer);
                                             break;
                                          case ItemType.Logo:
                                             WriteUserPatternSettings(writer);
                                             break;
                                          default:
                                             break;
                                       }

                                       writer.WriteElementString("Text", text);
                                    }
                                    writer.WriteEndElement(); // End Object
                                 }
                              }
                           }
                           writer.WriteEndElement(); // End Objects
                        }
                        writer.WriteEndElement(); // End Label
                     } catch (EIPIOException e1) {
                        // In case of an EIP I/O error
                        string name = $"{GetAttributeName(e1.ClassCode, e1.Attribute)}";
                        string msg = $"EIP I/O Error on {e1.AccessCode}/{e1.ClassCode}/{name}";
                        MessageBox.Show(msg, "EIP I/O Error", MessageBoxButtons.OK);
                     } catch (Exception e2) {
                        // You are on your own here
                     }
                  }
                  ForwardClose();
               }
               EndSession();
               writer.WriteEndDocument();
               writer.Flush();
               ms.Position = 0;

               xml = new StreamReader(ms).ReadToEnd();
               int xmlStart = 0;
               int xmlEnd = 0;
               // Can be called with a Filename or XML text
               xmlStart = xml.IndexOf("<Label");
               if (xmlStart == -1) {
                  xml = File.ReadAllText(xml);
                  xmlStart = xml.IndexOf("<Label");
               }
               // No label found, exit
               if (xmlStart == -1) {
                  return string.Empty;
               }
               xmlEnd = xml.IndexOf("</Label>", xmlStart + 7);
               if (xmlEnd > 0) {
                  xml = xml.Substring(xmlStart, xmlEnd - xmlStart + 8);
               }
            }
         }
         return xml;
      }

      // Write the global printer settings
      private void WritePrinterSettings(XmlTextWriter writer) {

         writer.WriteStartElement("Printer");
         {
            {
               writer.WriteAttributeString("Make", "Hitachi");
               writer.WriteAttributeString("Model", GetAttribute(ccUI.Model_Name));
            }

            writer.WriteStartElement("PrintHead");
            {
               writer.WriteAttributeString("Orientation", GetAttribute(ccPS.Character_Orientation));
            }
            writer.WriteEndElement(); // PrintHead

            writer.WriteStartElement("ContinuousPrinting");
            {
               writer.WriteAttributeString("RepeatInterval", GetAttribute(ccPS.Repeat_Interval));
               writer.WriteAttributeString("PrintsPerTrigger", GetAttribute(ccPS.Repeat_Count));
            }
            writer.WriteEndElement(); // ContinuousPrinting

            writer.WriteStartElement("TargetSensor");
            {
               writer.WriteAttributeString("Filter", GetAttribute(ccPS.Target_Sensor_Filter));
               writer.WriteAttributeString("SetupValue", GetAttribute(ccPS.Targer_Sensor_Filter_Value));
               writer.WriteAttributeString("Timer", GetAttribute(ccPS.Target_Sensor_Timer));
            }
            writer.WriteEndElement(); // TargetSensor

            writer.WriteStartElement("CharacterSize");
            {
               writer.WriteAttributeString("Height", GetAttribute(ccPS.Character_Width));
               writer.WriteAttributeString("Width", GetAttribute(ccPS.Character_Height));
            }
            writer.WriteEndElement(); // CharacterSize

            writer.WriteStartElement("PrintStartDelay");
            {
               writer.WriteAttributeString("Reverse", GetAttribute(ccPS.Print_Start_Delay_Forward));
               writer.WriteAttributeString("Forward", GetAttribute(ccPS.Print_Start_Delay_Reverse));
            }
            writer.WriteEndElement(); // PrintStartDelay

            writer.WriteStartElement("EncoderSettings");
            {
               writer.WriteAttributeString("HighSpeedPrinting", GetAttribute(ccPS.High_Speed_Print));
               writer.WriteAttributeString("Divisor", GetAttribute(ccPS.Pulse_Rate_Division_Factor));
               writer.WriteAttributeString("ExternalEncoder", GetAttribute(ccPS.Product_Speed_Matching));
            }
            writer.WriteEndElement(); // EncoderSettings

            writer.WriteStartElement("InkStream");
            {
               writer.WriteAttributeString("InkDropUse", GetAttribute(ccPS.Ink_Drop_Use));
               writer.WriteAttributeString("ChargeRule", GetAttribute(ccPS.Ink_Drop_Charge_Rule));
            }
            writer.WriteEndElement(); // InkStream

            writer.WriteStartElement("TwinNozzle");
            {
               //writer.WriteAttributeString("LeadingCharControl", this.LeadingCharacterControl.ToString());
               //writer.WriteAttributeString("LeadingCharControlWidth1", this.LeadingCharacterControlWidth1.ToString());
               //writer.WriteAttributeString("LeadingCharControlWidth2", this.LeadingCharacterControlWidth2.ToString());
               //writer.WriteAttributeString("NozzleSpaceAlignment", this.NozzleSpaceAlignment.ToString());
            }
            writer.WriteEndElement(); // TwinNozzle

            WriteSubstitutions(writer);
         }
         writer.WriteEndElement(); // Printer
      }

      // This is a work in progress
      private void WriteSubstitutions(XmlTextWriter writer) {
         // We need to figure out what substitution rules are being used
         // and which substitutions within the rule are needed.
         writer.WriteStartElement("Substitution");
         {
            writer.WriteAttributeString("Delimiter", "/");
            writer.WriteAttributeString("StartYear", "2019");
            writer.WriteAttributeString("Rule", "1");
            //WriteSubstitution(writer, ccSR.Year, 0, 23);
            WriteSubstitution(writer, ccSR.Month, 1, 12);
            //WriteSubstitution(writer, ccSR.Day, 1, 31);
            //WriteSubstitution(writer, ccSR.Hour, 0, 23);
            //WriteSubstitution(writer, ccSR.Minute, 0, 59);
            //WriteSubstitution(writer, ccSR.Week, 1, 53);
            WriteSubstitution(writer, ccSR.Day_Of_Week, 1, 7);
         }
         writer.WriteEndElement(); // Substitution
      }

      // Write a single rule
      private void WriteSubstitution(XmlTextWriter writer, ccSR attr, int start, int end) {
         int n = end - start + 1;
         string[] subCode = new string[n];
         for (int i = 0; i < n; i++) {
            subCode[i] = GetAttribute(attr, i + start);
         }
         for (int i = 0; i < n; i += 10) {
            writer.WriteStartElement(attr.ToString().Replace("_", ""));
            writer.WriteAttributeString("Base", (i + start).ToString());
            writer.WriteString(string.Join("/", subCode, i, Math.Min(10, n - i)));
            writer.WriteEndElement(); // Element
         }
      }

      // Write the Font XML
      private void WriteFont(XmlTextWriter writer) {
         writer.WriteStartElement("Font"); // Start Font
         {
            string BarCode = GetAttribute(ccPF.Barcode_Type);
            writer.WriteAttributeString("BarCode", BarCode);
            if (BarCode != "None") {
               writer.WriteAttributeString("HumanReadableFont", GetAttribute(ccPF.Readable_Code));
               writer.WriteAttributeString("EANPrefix", GetAttribute(ccPF.Prefix_Code));
            }
            writer.WriteAttributeString("IncreasedWidth", GetAttribute(ccPF.Character_Bold));
            writer.WriteAttributeString("InterLineSpace", GetAttribute(ccPF.Line_Spacing));
            writer.WriteAttributeString("InterCharacterSpace", GetAttribute(ccPF.InterCharacter_Space));
            writer.WriteString(GetAttribute(ccPF.Dot_Matrix));
         }
         writer.WriteEndElement(); // End Font
      }

      private void WriteLocation(XmlTextWriter writer, int item, int row, int col) {
         writer.WriteStartElement("Location"); // Start Location
         {
            writer.WriteAttributeString("ItemNumber", item.ToString());
            writer.WriteAttributeString("Row", row.ToString());
            writer.WriteAttributeString("Column", col.ToString());
         }
         writer.WriteEndElement(); // End Location
      }

      // Output the Calendar Settings
      private void WriteCalendarSettings(XmlTextWriter writer, int[] mask) {
         bool success = true;
         int FirstBlock = 0;
         int BlockCount = 0;
         GetAttribute(ccCal.First_Calendar_Block, out FirstBlock);
         GetAttribute(ccCal.Number_of_Calendar_Blocks, out BlockCount);
         for (int i = 0; success && i < BlockCount; i++) {
            SetAttribute(ccIDX.Calendar_Block, FirstBlock + i);
            writer.WriteStartElement("Date"); // Start Date
            {
               writer.WriteAttributeString("Block", (i + 1).ToString());
               // Offsets are always required
               writer.WriteStartElement("Offset"); // Start Offset
               {
                  writer.WriteAttributeString("Year", GetAttribute(ccCal.Offset_Year));
                  writer.WriteAttributeString("Month", GetAttribute(ccCal.Offset_Month));
                  writer.WriteAttributeString("Day", GetAttribute(ccCal.Offset_Day));
                  writer.WriteAttributeString("Hour", GetAttribute(ccCal.Offset_Hour));
                  writer.WriteAttributeString("Minute", GetAttribute(ccCal.Offset_Minute));
               }
               writer.WriteEndElement(); // End Offset

               if ((mask[i] & DateSubZS) > 0) {
                  writer.WriteStartElement("ZeroSuppress"); // Start ZeroSuppress
                  {
                     if ((mask[i] & (int)ba.Year) > 0)
                        writer.WriteAttributeString("Year", GetAttribute(ccCal.Zero_Suppress_Year));
                     if ((mask[i] & (int)ba.Month) > 0)
                        writer.WriteAttributeString("Month", GetAttribute(ccCal.Zero_Suppress_Month));
                     if ((mask[i] & (int)ba.Day) > 0)
                        writer.WriteAttributeString("Day", GetAttribute(ccCal.Zero_Suppress_Day));
                     if ((mask[i] & (int)ba.Hour) > 0)
                        writer.WriteAttributeString("Hour", GetAttribute(ccCal.Zero_Suppress_Hour));
                     if ((mask[i] & (int)ba.Minute) > 0)
                        writer.WriteAttributeString("Minute", GetAttribute(ccCal.Zero_Suppress_Minute));
                     if ((mask[i] & (int)ba.Week) > 0)
                        writer.WriteAttributeString("Week", GetAttribute(ccCal.Zero_Suppress_Weeks));
                     if ((mask[i] & (int)ba.DayOfWeek) > 0)
                        writer.WriteAttributeString("DayOfWeek", GetAttribute(ccCal.Zero_Suppress_Day_Of_Week));
                  }
                  writer.WriteEndElement(); // End ZeroSuppress

                  writer.WriteStartElement("EnableSubstitution"); // Start EnableSubstitution
                  {
                     if ((mask[i] & (int)ba.Year) > 0)
                        writer.WriteAttributeString("Year", GetAttribute(ccCal.Substitute_Year));
                     if ((mask[i] & (int)ba.Month) > 0)
                        writer.WriteAttributeString("Month", GetAttribute(ccCal.Substitute_Month));
                     if ((mask[i] & (int)ba.Day) > 0)
                        writer.WriteAttributeString("Day", GetAttribute(ccCal.Substitute_Day));
                     if ((mask[i] & (int)ba.Hour) > 0)
                        writer.WriteAttributeString("Hour", GetAttribute(ccCal.Substitute_Hour));
                     if ((mask[i] & (int)ba.Minute) > 0)
                        writer.WriteAttributeString("Minute", GetAttribute(ccCal.Substitute_Minute));
                     if ((mask[i] & (int)ba.Week) > 0)
                        writer.WriteAttributeString("Week", GetAttribute(ccCal.Substitute_Weeks));
                     if ((mask[i] & (int)ba.DayOfWeek) > 0)
                        writer.WriteAttributeString("DayOfWeek", GetAttribute(ccCal.Substitute_Day_Of_Week));
                  }
                  writer.WriteEndElement(); // End EnableSubstitution
               }

               if ((mask[i] & (int)ba.Shift) > 0) {
                  string endHour = "0";
                  string endMinute = "0";
                  int shift = 1;
                  do {
                     writer.WriteStartElement("ShiftCode"); // Start ShiftCode
                     {
                        SetAttribute(ccIDX.Item, shift);
                        writer.WriteAttributeString("Shift", shift.ToString());
                        writer.WriteAttributeString("StartHour", GetAttribute(ccCal.Shift_Start_Hour));
                        writer.WriteAttributeString("StartMinute", GetAttribute(ccCal.Shift_Start_Minute));
                        writer.WriteAttributeString("EndHour", endHour = GetAttribute(ccCal.Shift_End_Hour));
                        writer.WriteAttributeString("EndMinute", endMinute = GetAttribute(ccCal.Shift_End_Minute));
                        writer.WriteAttributeString("ShiftCode", GetAttribute(ccCal.Shift_String_Value));
                     }
                     writer.WriteEndElement(); // End ShiftCode
                     shift++;
                  } while (endHour != "23" || endMinute != "59");
               }
               if ((mask[i] & (int)ba.TimeCount) > 0) {
                  writer.WriteStartElement("TimeCount"); // Start TimeCount
                  {
                     writer.WriteAttributeString("Interval", GetAttribute(ccCal.Update_Interval_Value));
                     writer.WriteAttributeString("Start", GetAttribute(ccCal.Time_Count_Start_Value));
                     writer.WriteAttributeString("End", GetAttribute(ccCal.Time_Count_End_Value));
                     writer.WriteAttributeString("ResetTime", GetAttribute(ccCal.Reset_Time_Value));
                     writer.WriteAttributeString("ResetValue", GetAttribute(ccCal.Time_Count_Reset_Value));
                  }
                  writer.WriteEndElement(); // End TimeCount
               }
            }
            writer.WriteEndElement(); // End Date
         }
      }

      // Output the Counter Settings
      private void WriteCounterSettings(XmlTextWriter writer) {
         bool success = true;
         int FirstBlock = 0;
         int BlockCount = 0;
         GetAttribute(ccCount.First_Count_Block, out FirstBlock);
         GetAttribute(ccCount.Number_Of_Count_Blocks, out BlockCount);
         for (int i = 0; success && i < BlockCount; i++) {
            SetAttribute(ccIDX.Count_Block, FirstBlock + i);
            writer.WriteStartElement("Counter"); // Start Counter
            {
               writer.WriteAttributeString("Block", (i + 1).ToString());
               writer.WriteAttributeString("Reset", GetAttribute(ccCount.Reset_Value));
               //writer.WriteAttributeString("ExternalSignal", p.CtExternalSignal);
               //writer.WriteAttributeString("ResetSignal", p.CtResetSignal);
               writer.WriteAttributeString("CountUp", GetAttribute(ccCount.Direction_Value));
               writer.WriteAttributeString("Increment", GetAttribute(ccCount.Increment_Value));
               writer.WriteAttributeString("JumpTo", GetAttribute(ccCount.Jump_To));
               writer.WriteAttributeString("JumpFrom", GetAttribute(ccCount.Jump_From));
               writer.WriteAttributeString("UpdateUnit", GetAttribute(ccCount.Update_Unit_Unit));
               writer.WriteAttributeString("UpdateIP", GetAttribute(ccCount.Update_Unit_Halfway));
               writer.WriteAttributeString("Range2", GetAttribute(ccCount.Count_Range_2));
               writer.WriteAttributeString("Range1", GetAttribute(ccCount.Count_Range_1));
               writer.WriteAttributeString("InitialValue", GetAttribute(ccCount.Initial_Value));
               writer.WriteAttributeString("Multiplier", GetAttribute(ccCount.Count_Multiplier));
               writer.WriteAttributeString("ZeroSuppression", GetAttribute(ccCount.Zero_Suppression));
            }
            writer.WriteEndElement(); //  End Counter
         }
      }

      // Output the User Pattern Settings
      private void WriteUserPatternSettings(XmlTextWriter writer) {
         writer.WriteStartElement("Logo"); // Start Logo
         {
            //writer.WriteAttributeString("Variable", p.WlxVariableName);
            //writer.WriteAttributeString("HAlignment", Enum.GetName(typeof(Utils.HAlignment), p.LogoHAlignment));
            //writer.WriteAttributeString("VAlignment", Enum.GetName(typeof(Utils.VAlignment), p.LogoVAlignment));
            //writer.WriteAttributeString("Filter", p.LogoFilter.ToString());
            //writer.WriteAttributeString("ReverseVideo", p.LogoReverseVideo.ToString());
            //writer.WriteAttributeString("Source", p.LogoSource);
            //writer.WriteAttributeString("LogoLength", p.LogoLength.ToString());
            //writer.WriteAttributeString("Registration", p.LogoRegistration.ToString());

            //using (MemoryStream ms2 = new MemoryStream()) {
            //   // bm.Save does not like transparent pixels so make them white
            //   Bitmap bm2 = new Bitmap(p.ItemWidth, p.ItemHeight);
            //   using (Graphics g = Graphics.FromImage(bm2)) {
            //      g.Clear(Color.White);
            //      for (int x = 0; x < p.ItemWidth; x++) {
            //         for (int y = 0; y < p.ItemHeight; y++) {
            //            if (p.ScaledImage.GetPixel(x, y).ToArgb() != 0) {
            //               bm2.SetPixel(x, y, Color.Black);
            //            }
            //         }
            //      }
            //   }
            //   Bitmap bm = Utils.BitmapTo1Bpp(bm2);
            //   bm.Save(ms2, ImageFormat.Bmp);
            //   using (BinaryReader br = new BinaryReader(ms2)) {
            //      ms2.Position = 0;
            //      byte[] b = br.ReadBytes((int)ms2.Length);
            //      int length = Utils.LittleEndian(b, 2, 4);
            //      string data = "";
            //      writer.WriteAttributeString("size", length.ToString());
            //      for (int j = 0; j < length; j++) {
            //         data = data + string.Format("{0:X2} ", b[j]);
            //      }
            //      writer.WriteStartElement("Data"); // Start Data
            //      writer.WriteAttributeString("Length", length.ToString());
            //      writer.WriteString(data.TrimEnd());
            //      writer.WriteEndElement(); // End Data
            //   }
            //}
         }
         writer.WriteEndElement(); // End Logo
         //string LogoText = "";
         //for (int j = 0; j < p.ItemText.Length; j++) {
         //   LogoText += ((short)p.ItemText[j]).ToString("X4");
         //}
      }

      #endregion

      #region Service Routines

      // Get XML Text
      private string GetXmlValue(XmlNode node) {
         if (node != null) {
            return node.InnerText;
         } else {
            return N_A;
         }
      }

      // Get XML Attribute Value
      private string GetXmlAttr(XmlNode node, string AttrName) {
         XmlNode n;
         if (node != null && (n = node.Attributes[AttrName]) != null) {
            return n.Value;
         } else {
            return N_A;
         }
      }

      // Get the contents of one attribute
      private string GetAttribute<T>(T Attribute, int n) where T : Enum {
         string val = string.Empty;
         AttrData attr = GetAttrData(Attribute);
         if (GetAttribute(attr.Class, attr.Val, FormatOutput(attr.Get, n))) {
            val = GetDataValue;
            if (attr.Data.Fmt == DataFormats.UTF8 || attr.Data.Fmt == DataFormats.UTF8N) {
               val = FromQuoted(val);
            }
         }
         return val;
      }

      // Get the contents of one attribute
      private string GetAttribute<T>(T Attribute) where T : Enum {
         string val = string.Empty;
         AttrData attr = GetAttrData(Attribute);
         if (GetAttribute(attr.Class, attr.Val, Nodata)) {
            val = GetDataValue;
            if (attr.Data.Fmt == DataFormats.UTF8 || attr.Data.Fmt == DataFormats.UTF8N) {
               val = FromQuoted(val);
            } else if (attr.Data.DropDown != fmtDD.None) {
               string[] dd = EIP.DropDowns[(int)attr.Data.DropDown];
               long n = GetDecValue - attr.Data.Min;
               if (n >= 0 && n < dd.Length) {
                  val = dd[n];
               }
            }
         }
         return val;
      }

      // Examine the contents of a print message to determine its type
      private ItemType GetItemType(string text, ref int[] mask) {
         int l = 0;
         mask[l] = 0;
         string[] s = text.Split('{');
         for (int i = 0; i < s.Length; i++) {
            int n = s[i].IndexOf('}');
            if (n >= 0) {
               for (int j = 0; j < n; j++) {
                  int k = Array.IndexOf(bc, s[i][j]);
                  if (k >= 0) {
                     mask[l] |= 1 << k;
                  } else {
                     mask[l] |= (int)ba.Unknown;
                  }
               }
            }
            if (s[i].IndexOf('}', n + 1) > 0) {
               l++;
            }
         }
         // Calendar and Count cannot appear in the same item
         if ((mask[0] & (int)ba.Count) > 0) {
            return ItemType.Counter;
         } else if ((mask[0] & DateCode) > 0) {
            return ItemType.Date;
         } else {
            return ItemType.Text;
         }
      }

      #endregion

   }

}