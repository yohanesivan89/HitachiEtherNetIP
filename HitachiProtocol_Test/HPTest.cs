﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HitachiProtocol;

namespace HitachiProtocol_Test {

   public partial class HPTest : Form {

      #region Data Declarations

      HitachiPrinter HP;

      // Ethernet connection parameters
      IPAddress ipAddress;
      int ipPort;

      // Serial connection parameters
      string sPort;
      int sBaudRate;
      Parity sParity;
      int sDataBits;
      StopBits sStopBits;

      #endregion

      #region Constructors and Destructors

      // An instance of an Hitachi printer
      public HPTest() {
         InitializeComponent();
      }

      // Unfortunately, never called
      ~HPTest() {

      }

      #endregion

      #region Form Level Events

      // Program is loaded.  Start the processing
      void HPTest_Load(object sender, EventArgs e) {
         // Load saved settings
         LoadSettings();
         // Initialize communications settings
         string[] Portnames = System.IO.Ports.SerialPort.GetPortNames();
         cbPrinterPortName.Items.Clear();
         cbPrinterPortName.Items.AddRange(Portnames);
         if (Portnames.Length > 0) {
            cbPrinterPortName.Items.AddRange(Portnames);
            cbPrinterPortName.SelectedIndex = 0;
         }
         // Instantiate the printer
         HP = new HitachiPrinter(this, 0) {
            PrinterType = HitachiPrinterType.UX,
            SOP4Enabled = true,
            EventLogging = HPEventLogging.All,
            MessageStyle = FormatSetup.Individual,
            MergeRequests = false,
            Shifts = new string[,]
              {
                 { "0:00", "7:00", "15:00", "23:00", "24:00" },
                 { "3", "1", "2", "3", "3" }
              }
         };
         HP.Log += HP_Log;
         HP.Complete += HP_Complete;
         HP.Unsolicited += HP_Unsolicited;
         SetButtonEnables();
      }

      // Form is closing.  Clean up the application
      void HPTest_FormClosing(object sender, FormClosingEventArgs e) {
         HP.Log -= HP_Log;
         HP.Complete -= HP_Complete;
         HP.Unsolicited -= HP_Unsolicited;
         HP = null;
         SaveSettings();
      }

      #endregion

      #region Form Control Events

      // Connect to the printer
      void cmdConnect_Click(object sender, EventArgs e) {
         if (ConfigureConnection.SelectedIndex == 0) {
            // Ethernet Connection
            HP.Connect(ipAddress, ipPort);
         } else if (ConfigureConnection.SelectedIndex == 1) {
            // Serial connection
            HP.Connect(sPort, sBaudRate, sParity, sDataBits, sStopBits);
         } else {
            HP.Connect();
         }
         HP.IssueControl(ControlOps.ComOn);
      }

      // Disconnect from printer
      void cmdDisconnect_Click(object sender, EventArgs e) {
         HP.Disconnect();
      }

      // Send message to the printer
      void cmdSend_Click(object sender, EventArgs e) {
         // Clear all current items
         HP.IssueControl(ControlOps.ClearAll);
         // Set all items to 7x10 format
         HP.WriteFormat(0, "7x10", 1, 1);
         // Insert text in three items
         HP.WriteText(1,
            $"Hello World\r\n" +
            $"{(char)AC.Month}{(char)AC.Month}/" +
            $"{(char)AC.Day}{(char)AC.Day}/" +
            $"{(char)AC.Year}{(char)AC.Year}\r\n" +
            $"Line 3");
         // Stack the items into a single column
         HP.ColumnSetup("3", "1");
         // Get the printer status
         HP.Fetch(FetchOps.Status);
         HP.Idle("Done");
      }

      // Browse for Message folder
      private void cmdBrowse_Click(object sender, EventArgs e) {
         FolderBrowserDialog dlg = new FolderBrowserDialog() { ShowNewFolderButton = true, SelectedPath = txtMessageFolder.Text };
         if (dlg.ShowDialog() == DialogResult.OK) {
            txtMessageFolder.Text = dlg.SelectedPath;
         }
      }

      // Play it again, Sam
      private void cmdRun_Click(object sender, EventArgs e) {
         int count;
         int n;
         if (File.Exists(txtLogFile.Text)) {
            string[] s = File.ReadAllLines(txtLogFile.Text);
            if (!int.TryParse(txtCount.Text, out count)) {
               count = s.Length;
            }
            for (int i = 0; i < s.Length && count > 0; i++) {
               if ((n = s[i].IndexOf("Output = ")) >= 0) {
                  string ss = s[i].Substring(n + 9);
                  //lbTraffic.Items.Add($"File = {ss}");
                  string sp = tobytes(ss);
                  if (sp != "\x1B\x23" && ss != "\x02\x1F\x72\x30\x03") {
                     HP.PassThru(sp, false);
                     count--;
                  }
               }
            }
            lbTraffic.Items.Add(HP.Translate("Done"));
         }
      }

      // Browse for log file
      private void cmdBrowseForLog_Click(object sender, EventArgs e) {
         using (OpenFileDialog dlg = new OpenFileDialog()) {
            dlg.Title = "Select Log file!";
            dlg.Filter = "TXT (*.txt)|*.txt|All (*.*)|*.*";
            DialogResult dlgResult = DialogResult.Retry;
            if (File.Exists(txtLogFile.Text)) {
               dlg.InitialDirectory = Path.GetDirectoryName(txtLogFile.Text);
               dlg.FileName = Path.GetFileNameWithoutExtension(txtLogFile.Text);
            }
            while (dlgResult == DialogResult.Retry) {
               dlgResult = dlg.ShowDialog();
               if (dlgResult == DialogResult.OK) {
                  txtLogFile.Text = dlg.FileName;
               }
            }
         }
         SetButtonEnables();
      }

      // Goodnight Mortimer  
      void cmdExit_Click(object sender, EventArgs e) {
         this.Close();
      }

      #endregion

      #region Context Menu Routines

      // Clear the traffic display
      void cmTraffic_Click(object sender, EventArgs e) {
         lbTraffic.Items.Clear();
      }

      // Load the traffic display into notepad
      void cmLoadInNotepad_Click(object sender, EventArgs e) {
         StreamWriter outputFileStream = null;
         string[] allLines = new string[lbTraffic.Items.Count];
         string outputPath = @"C:\Temp\traffic.txt";
         lbTraffic.Items.CopyTo(allLines, 0);
         outputFileStream = new StreamWriter(outputPath, false);
         for (int i = 0; i < lbTraffic.Items.Count; i++) {
            outputFileStream.WriteLine(allLines[i]);
         }
         outputFileStream.Flush();
         outputFileStream.Close();
         Process.Start("notepad.exe", outputPath);
      }

      #endregion

      #region Event Processing

      // Hitachi printer object has something noteworthy to say
      public void HP_Log(HitachiPrinter p, HPEventArgs e) {
         lbTraffic.Items.Add(HP.Translate(e.Message));
      }

      // Requested operation has completed
      void HP_Complete(HitachiPrinter p, HPEventArgs e) {
         switch (e.Op) {
            case PrinterOps.Nop:
               break;
            case PrinterOps.Connect:
               break;
            case PrinterOps.Disconnect:
               break;
            case PrinterOps.IssueControl:
               break;
            case PrinterOps.ColumnSetup:
               break;
            case PrinterOps.WriteSpecification:
               break;
            case PrinterOps.WriteFormat:
               break;
            case PrinterOps.WriteText:
               break;
            case PrinterOps.WriteCalendarOffset:
               break;
            case PrinterOps.WriteCalendarSubZS:
               break;
            case PrinterOps.WriteCountCondition:
               break;
            case PrinterOps.WritePattern:
               break;
            case PrinterOps.Message:
               break;
            case PrinterOps.Fetch:
               if ((FetchOps)e.SubOp == FetchOps.Status) {
                  HPStatus status = HP.GetStatus();
               }
               break;
            case PrinterOps.Retrieve:
               break;
            case PrinterOps.RetrievePattern:
               break;
            case PrinterOps.SetClock:
               break;
            case PrinterOps.Idle:
               break;
            case PrinterOps.PassThru:
               break;
            case PrinterOps.ENQ:
               break;
            case PrinterOps.SOP16ClearBuffer:
               break;
            case PrinterOps.SOP16RestartPrinting:
               break;
            case PrinterOps.ChangeInkDropRule:
               break;
            case PrinterOps.ChangeMessageFormat:
               break;
            case PrinterOps.PositionItem:
               break;
            case PrinterOps.WriteCalendarZS:
               break;
            case PrinterOps.WriteCalendarSub:
               break;
            case PrinterOps.WriteCalendarSubRule:
               break;
            case PrinterOps.TimedDelay:
               break;
            case PrinterOps.CreateMessage:
               break;
            case PrinterOps.SendMessage:
               break;
            case PrinterOps.SetNozzle:
               break;
            case PrinterOps.ShutDown:
               break;
            default:
               break;
         }
         SetButtonEnables();
      }

      // Printer Status Unsolicited interrupt arrived
      void HP_Unsolicited(HitachiPrinter p, HPEventArgs e) {
         if (e.Message.StartsWith(HitachiPrinter.sSTX) && e.Message.EndsWith(HitachiPrinter.sETX)) {
            switch (e.Message.Substring(1, 1)) {
               case HitachiPrinter.sBEL:
                  PrintStart(p, e);
                  break;
               case HitachiPrinter.sDLE:
                  PrintEnd(p, e);
                  break;
               case "1":
                  // It is a status
                  break;
               default:
                  // Who knows
                  break;
            }
         } else {
            // Who knows
         }
      }

      // Print Start Unsolicited interrupt arrived
      void PrintStart(HitachiPrinter p, HPEventArgs e) {
         // Add message build here
      }

      // Print End Unsolicited interrupt arrived
      void PrintEnd(HitachiPrinter p, HPEventArgs e) {
         // Send message to printer
      }

      #endregion

      #region Service Routines

      // Load form settings when program starts
      void LoadSettings() {
         // Create a shortcut for settings
         Properties.Settings p = Properties.Settings.Default;
         // Load the user's data
         ConfigureConnection.SelectedIndex = p.Tab;
         tbPrinterIPAddress.Text = p.IPAddress;
         tbPrinterPort.Text = p.IPPort;
         cbPrinterPortName.Text = p.SerialPort;
         cbPrinterBaudRate.Text = p.SerialBaudRate;
         cbPrinterDataBits.Text = p.SerialDataBits;
         cbPrinterParity.Text = p.SerialParity;
         cbPrinterStopBits.Text = p.SerialStopBits;
         txtMessageFolder.Text = p.MessageFolder;
         txtLogFile.Text = p.LogFile;
      }

      // Save form settings on exit
      void SaveSettings() {
         // Create a shortcut for settings
         Properties.Settings p = Properties.Settings.Default;
         // Save the user's data
         p.Tab = ConfigureConnection.SelectedIndex;
         p.IPAddress = tbPrinterIPAddress.Text;
         p.IPPort = tbPrinterPort.Text;
         p.SerialPort = cbPrinterPortName.Text;
         p.SerialBaudRate = cbPrinterBaudRate.Text;
         p.SerialDataBits = cbPrinterDataBits.Text;
         p.SerialParity = cbPrinterParity.Text;
         p.SerialStopBits = cbPrinterStopBits.Text;
         p.MessageFolder = txtMessageFolder.Text;
         p.LogFile = txtLogFile.Text;
         p.Save();
      }

      // Change Log File encripted text to plain text
      private string tobytes(string ss) {
         string s = "";
         while (ss.Length > 0) {
            if (ss.Length >= 4 && ss[0] == '<' && ss[3] == '>'
                && int.TryParse(ss.Substring(1, 2), NumberStyles.HexNumber, null, out int n)) {
               s += (char)n;
               ss = ss.Substring(4);
            } else {
               s += ss.Substring(0, 1);
               ss = ss.Substring(1);
            }

         }
         return s;
      }

      // Enable buttons that can be used
      void SetButtonEnables() {
         bool ConfigOK;
         if (ConfigureConnection.SelectedIndex == 0) {
            // Ethernet Connection
            if (IPAddress.TryParse(tbPrinterIPAddress.Text, out ipAddress)
               && int.TryParse(tbPrinterPort.Text, out ipPort)) {
               ConfigOK = true;
            } else {
               ConfigOK = false;
            }
         } else {
            // Serial connection
            ConfigOK = cbPrinterPortName.SelectedIndex >= 0
               && cbPrinterBaudRate.SelectedIndex >= 0
               && cbPrinterDataBits.SelectedIndex >= 0
               && cbPrinterParity.SelectedIndex >= 0
               && cbPrinterStopBits.SelectedIndex >= 0;
            if (ConfigOK) {
               sPort = cbPrinterPortName.Text;
               sBaudRate = Convert.ToInt32(cbPrinterBaudRate.Text);
               sParity = (Parity)Enum.Parse(typeof(Parity), cbPrinterParity.Text, true);
               sDataBits = Convert.ToInt32(cbPrinterDataBits.Text);
               sStopBits = (StopBits)Enum.Parse(typeof(StopBits), cbPrinterStopBits.Text, true);
            }
         }
         bool connected = HP != null && HP.ConnectionState != ConnectionStates.Closed;
         cmdConnect.Enabled = ConfigOK && !connected;
         cmdDisconnect.Enabled = connected;
         cmdSend.Enabled = connected;
         cmdExit.Enabled = !connected;
         cmdRun.Enabled = File.Exists(txtLogFile.Text) && connected;
      }

      // Enable buttons that can be used
      void SetButtonEnables(object sender, EventArgs e) {
         SetButtonEnables();
      }

      #endregion

   }
}
