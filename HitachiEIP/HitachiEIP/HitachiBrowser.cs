﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace HitachiEIP {

   public partial class HitachiBrowser : Form {

      #region Data declarations

      string IPAddress;
      int port;

      EIP EIP;

      eipAccessCode[] AccessCodes;

      eipClassCode[] ClassCodes;
      string[] ClassNames;

      int[] ClassAttr;
      AttrData attr;

      // Traffic/Log files
      string TrafficFilename;
      StreamWriter TrafficFileStream = null;
      string LogFilename;
      StreamWriter LogFileStream = null;

      ResizeInfo R;
      bool initComplete = false;

      public bool AllGood { get; set; } = true;

      // Attribute Screens
      Attributes<eipIndex> indexAttr;               // 0x7A
      Attributes<eipIJP_operation> oprAttr;         // 0x75
      Attributes<eipPrint_Data_Management> pdmAttr; // 0x66
      Attributes<eipPrint_specification> psAttr;    // 0x68
      Attributes<eipPrint_format> pFmtAttr;         // 0x67
      Attributes<eipCalendar> calAttr;              // 0x69
      Attributes<eipSubstitution_rules> sRulesAttr; // 0x6C
      Attributes<eipCount> countAttr;               // 0x79
      Attributes<eipUnit_Information> unitInfoAttr; // 0x73
      Attributes<eipEnviroment_setting> envirAttr;  // 0x71
      Attributes<eipOperation_management> mgmtAttr; // 0x74
      XML processXML;                               // xml processing

      //Attributes<eipUser_pattern> userPatAttr;    // 0x6B Not implemented here

      public bool ComIsOn = false;
      public bool MgmtIsOn = false;
      public bool AutoReflIsOn = false;

      // Flags for adding extra controls to set Index functions
      public const int AddNone = 0;
      public const int AddItem = 0x01;
      public const int AddColumn = 0x02;
      public const int AddLine = 0x04;
      public const int AddPosition = 0x08;
      public const int AddCalendar = 0x10;
      public const int AddCount = 0x20;
      public const int AddSubstitution = 0x40;
      public const int AddAll = 0x7F;

      #endregion

      #region Constructors and Destructors

      public HitachiBrowser() {
         InitializeComponent();

         this.Text += " - " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

         VerifyAddressAndPort();
         EIP = new EIP(txtIPAddress.Text, port);
         EIP.Log += EIP_Log;
         EIP.Error += EIP_Error;
         EIP.IOComplete += EIP_ReadComplete;
         EIP.StateChanged += EIP_StateChanged;

      }

      #endregion

      #region Form Level events

      private void HitachiBrowser_Load(object sender, EventArgs e) {
         Utils.PositionForm(this, 0.75f, 0.9f);
         AccessCodes = (eipAccessCode[])Enum.GetValues(typeof(eipAccessCode));

         ClassNames = Enum.GetNames(typeof(eipClassCode));
         ClassCodes = (eipClassCode[])Enum.GetValues(typeof(eipClassCode));

         BuildAttributeDictionary(ClassCodes);

         cbClassCode.Items.Clear();
         for (int i = 0; i < ClassNames.Length; i++) {
            cbClassCode.Items.Add($"{ClassNames[i].Replace('_', ' ')} (0x{(byte)ClassCodes[i]:X2})");
         }

         BuildTrafficFile();
         BuildLogFile();

         // Load all the tabbed control data
         indexAttr = new Attributes<eipIndex>
            (this, EIP, tabIndex, eipClassCode.Index, Data.Index);
         oprAttr = new Attributes<eipIJP_operation>
            (this, EIP, tabIJPOperation, eipClassCode.IJP_operation, Data.IJPOperation);
         pdmAttr = new Attributes<eipPrint_Data_Management>
            (this, EIP, tabPrintManagement, eipClassCode.Print_data_management, Data.PrintDataManagement);
         psAttr = new Attributes<eipPrint_specification>
            (this, EIP, tabPrintSpec, eipClassCode.Print_specification, Data.PrintSpecification);
         pFmtAttr = new Attributes<eipPrint_format>
            (this, EIP, tabPrintFormat, eipClassCode.Print_format, Data.PrintFormat,
            AddItem | AddPosition | AddColumn);
         calAttr = new Attributes<eipCalendar>
            (this, EIP, tabCalendar, eipClassCode.Calendar, Data.Calendar,
            AddCalendar | AddItem);
         sRulesAttr = new Attributes<eipSubstitution_rules>
            (this, EIP, tabSubstitution, eipClassCode.Substitution_rules, Data.SubstitutionRules,
            AddSubstitution);
         countAttr = new Attributes<eipCount>
            (this, EIP, tabCount, eipClassCode.Count, Data.Count,
            AddItem | AddCount);
         unitInfoAttr = new Attributes<eipUnit_Information>
            (this, EIP, tabUnitInformation, eipClassCode.Unit_Information, Data.UnitInformation);
         envirAttr = new Attributes<eipEnviroment_setting>
            (this, EIP, tabEnviroment, eipClassCode.Enviroment_setting, Data.EnviromentSetting);
         mgmtAttr = new Attributes<eipOperation_management>
            (this, EIP, tabOpMgmt, eipClassCode.Operation_management, Data.OperationManagement);
         processXML = new XML(this, EIP, tabXML);

         //userPatAttr = new Attributes<eipUser_pattern>(this, EIP, tabUserPattern, eipClassCode.User_pattern);

         // Force a resize
         initComplete = true;
         HitachiBrowser_Resize(null, null);

         //Start out connected to the printer
         btnStartSession_Click(null, null);

         if (EIP.SessionIsOpen) {
            // These three flags control all traffic to/from the printer
            if (EIP.ForwardOpen()) {
               EIP.WriteOneAttribute(eipClassCode.IJP_operation, (byte)eipIJP_operation.Online_Offline, new byte[] { 1 });
               GetComSetting();
               if (ComIsOn) {
                  GetAutoReflectionSetting();
                  GetMgmtSetting();
               }
               EIP.ForwardClose();
            }
         }
         SetButtonEnables();
      }

      private void HitachiBrowser_FormClosing(object sender, FormClosingEventArgs e) {

         // Cleanup the open classes
         indexAttr = null;
         oprAttr = null;
         pdmAttr = null;
         psAttr = null;
         pFmtAttr = null;
         calAttr = null;
         sRulesAttr = null;
         countAttr = null;
         unitInfoAttr = null;
         envirAttr = null;
         mgmtAttr = null;
         processXML = null;
         //userPatAttr = null;

         // Stop logging
         EIP.Log -= EIP_Log;
         EIP.Error -= EIP_Error;

         // Close log/traffic files
         CloseTrafficFile(false);
         CloseLogFile(false);
      }

      private void HitachiBrowser_Resize(object sender, EventArgs e) {
         //
         // Avoid resize on screen minimize
         if (initComplete && ClientRectangle.Height > 0) {
            //
            this.SuspendLayout();
            // Build local parameters
            R = Utils.InitializeResize(this, 49, 47, true);

            #region Left Column

            Utils.ResizeObject(ref R, lblIPAddress, 1, 1, 2, 4);
            Utils.ResizeObject(ref R, txtIPAddress, 1, 5, 2, 4);
            Utils.ResizeObject(ref R, lblPort, 3, 1, 2, 4);
            Utils.ResizeObject(ref R, txtPort, 3, 5, 2, 4);
            Utils.ResizeObject(ref R, lblSessionID, 5, 1, 2, 4);
            Utils.ResizeObject(ref R, txtSessionID, 5, 5, 2, 4);

            Utils.ResizeObject(ref R, btnStartSession, 7.5f, 0.5f, 2, 4);
            Utils.ResizeObject(ref R, btnEndSession, 7.5f, 5, 2, 4);
            Utils.ResizeObject(ref R, btnForwardOpen, 10, 0.5f, 2, 4);
            Utils.ResizeObject(ref R, btnForwardClose, 10, 5, 2, 4);

            Utils.ResizeObject(ref R, lblClassCode, 13, 0.5f, 1, 8.5f);
            Utils.ResizeObject(ref R, cbClassCode, 14, 0.5f, 2, 8.5f);
            Utils.ResizeObject(ref R, lblFunction, 16, 0.5f, 1, 8.5f);
            Utils.ResizeObject(ref R, cbFunction, 17, 0.5f, 2, 8.5f);
            Utils.ResizeObject(ref R, btnIssueGet, 19, 0.5f, 2, 4);
            Utils.ResizeObject(ref R, btnIssueSet, 19, 5, 2, 4);
            Utils.ResizeObject(ref R, btnIssueService, 19, 0.5f, 2, 8.5f);

            Utils.ResizeObject(ref R, lblStatus, 22, 0.5f, 1, 8.5f);
            Utils.ResizeObject(ref R, txtStatus, 23, 0.5f, 2, 8.5f);
            Utils.ResizeObject(ref R, lblCount, 25, 0.5f, 1, 1);
            Utils.ResizeObject(ref R, lbldata, 25, 2, 1, 7);
            Utils.ResizeObject(ref R, txtCount, 26, 0.5f, 2, 1);
            Utils.ResizeObject(ref R, txtData, 26, 2, 2, 7);
            Utils.ResizeObject(ref R, txtDataBytes, 28, 0.5f, 2, 8.5f);

            Utils.ResizeObject(ref R, lblSaveFolder, 30, 0.5f, 1, 8.5f);
            Utils.ResizeObject(ref R, txtSaveFolder, 31, 0.5f, 2, 8.5f);
            Utils.ResizeObject(ref R, btnProperties, 33, 0.5f, 2, 8.5f);
            //Utils.ResizeObject(ref R, btnBrowse, 33, 0.5f, 2, 8.5f);

            Utils.ResizeObject(ref R, lstErrors, 36, 0.5f, 12, 8.5f);

            #endregion

            #region  Classes

            Utils.ResizeObject(ref R, tclClasses, 1, 10, 44, 36);

            indexAttr.ResizeControls(ref R);
            oprAttr.ResizeControls(ref R);
            pdmAttr.ResizeControls(ref R);
            psAttr.ResizeControls(ref R);
            pFmtAttr.ResizeControls(ref R);
            calAttr.ResizeControls(ref R);
            sRulesAttr.ResizeControls(ref R);
            countAttr.ResizeControls(ref R);
            unitInfoAttr.ResizeControls(ref R);
            envirAttr.ResizeControls(ref R);
            mgmtAttr.ResizeControls(ref R);
            processXML.ResizeControls(ref R);

            #endregion

            #region Bottom Row

            Utils.ResizeObject(ref R, btnCom, 45.5f, 10, 3, 5);
            Utils.ResizeObject(ref R, btnAutoReflection, 45.5f, 15.5f, 3, 5);
            Utils.ResizeObject(ref R, btnManagementFlag, 45.5f, 21, 3, 5);

            Utils.ResizeObject(ref R, btnStop, 46, 29.5f, 2, 3);
            Utils.ResizeObject(ref R, btnViewTraffic, 46, 33, 2, 3);
            Utils.ResizeObject(ref R, btnViewLog, 46, 36.5f, 2, 3);
            Utils.ResizeObject(ref R, btnReadAll, 46, 40, 2, 3);
            Utils.ResizeObject(ref R, btnExit, 46, 43.5f, 2, 3);

            #endregion

            this.Refresh();
            this.ResumeLayout();
         }
      }

      #endregion

      #region Form control events

      private void btnStartSession_Click(object sender, EventArgs e) {
         VerifyAddressAndPort();
         EIP.IPAddress = txtIPAddress.Text;
         EIP.port = port;
         EIP.StartSession();
         txtSessionID.Text = EIP.SessionID.ToString();
         SetButtonEnables();
      }

      private void btnEndSession_Click(object sender, EventArgs e) {
         EIP.EndSession();
         txtSessionID.Text = string.Empty;
         //EIP.Disconnect();
         SetButtonEnables();
      }

      private void btnForwardOpen_Click(object sender, EventArgs e) {
         EIP.ForwardOpen();
         SetButtonEnables();
      }

      private void btnForwardClose_Click(object sender, EventArgs e) {
         EIP.ForwardClose();
         SetButtonEnables();
      }

      private void btnExit_Click(object sender, EventArgs e) {
         if (EIP.ForwardIsOpen) {
            btnForwardClose_Click(null, null);
         }
         if (EIP.SessionIsOpen) {
            btnEndSession_Click(null, null);
         }
         this.Close();
      }

      private void btnIssueGet_Click(object sender, EventArgs e) {
         bool Success = false;
         if (cbClassCode.SelectedIndex >= 0
            && cbFunction.SelectedIndex >= 0) {
            try {
               Success = EIP.ReadOneAttribute(ClassCodes[cbClassCode.SelectedIndex], (byte)ClassAttr[cbFunction.SelectedIndex], out string val, attr.Fmt);
               string trafficText = $"{EIP.LastIO}\t";
               trafficText += $"{EIP.Access}\t{EIP.Class}\t{EIP.Instance}\t{EIP.GetAttributeName(EIP.Class, ClassAttr[cbFunction.SelectedIndex])}\t";
               if (Success) {
                  string hdr = EIP.GetBytes(EIP.ReadData, 46, 4);
                  trafficText += $"{hdr}\t{EIP.GetStatus}\t";
                  string s = EIP.GetBytes(EIP.GetData, 0, EIP.GetDataLength);
                  trafficText += $"{txtCount.Text}\t{txtData.Text}\t{txtDataBytes.Text}";
               }
               TrafficFileStream.WriteLine(trafficText);
            } catch (Exception e2) {
               AllGood = false;
            }
         }
      }

      private void btnIssueSet_Click(object sender, EventArgs e) {
         bool Success = false;
         if (cbClassCode.SelectedIndex >= 0
            && cbFunction.SelectedIndex >= 0) {
            try {
               byte[] Data = EIP.FormatOutput(txtData.Text, attr);
               Success = EIP.WriteOneAttribute(ClassCodes[cbClassCode.SelectedIndex], (byte)ClassAttr[cbFunction.SelectedIndex], Data);
               txtStatus.Text = EIP.GetStatus;
            } catch {
               AllGood = false;
            }
         }
      }

      private void btnIssueService_Click(object sender, EventArgs e) {
         bool Success = false;
         if (cbClassCode.SelectedIndex >= 0
            && cbFunction.SelectedIndex >= 0) {
            try {
               byte[] data = EIP.FormatOutput(txtData.Text, attr);
               Success = EIP.ServiceAttribute(ClassCodes[cbClassCode.SelectedIndex], (byte)ClassAttr[cbFunction.SelectedIndex], data);
               if (Success) {
                  string hdr = EIP.GetBytes(EIP.ReadData, 46, 4);
                  int status = (int)EIP.Get(EIP.ReadData, 48, 2, mem.LittleEndian);
                  string text = "Unknown!";
                  switch (status) {
                     case 0:
                        text = "O.K.";
                        break;
                     case 0x14:
                        text = "Attribute Not Supported!";
                        break;
                  }
                  txtStatus.Text = $"{status:X2} -- {text} -- {EIP.Access:X2} {EIP.Class:X2} {EIP.Instance:X2} {EIP.Attribute:X2}";
               }
            } catch (Exception e2) {
               AllGood = false;
            }
         }
      }

      private void cbClassCode_SelectedIndexChanged(object sender, EventArgs e) {
         cbFunction.Items.Clear();
         ClassAttr = null;

         btnIssueGet.Visible = false;
         btnIssueSet.Visible = false;
         btnIssueService.Visible = false;

         if (cbClassCode.SelectedIndex >= 0) {
            // Get all names associated with the enumeration
            string[] names = Data.ClassCodeAttributes[cbClassCode.SelectedIndex].GetEnumNames();
            ClassAttr = (int[])Data.ClassCodeAttributes[cbClassCode.SelectedIndex].GetEnumValues();
            for (int i = 0; i < names.Length; i++) {
               cbFunction.Items.Add($"{names[i].Replace('_', ' ')}  (0x{ClassAttr[i]:X2})");
            }

         }

         SetButtonEnables();
      }

      private void cbFunction_SelectedIndexChanged(object sender, EventArgs e) {
         btnIssueGet.Visible = false;
         btnIssueSet.Visible = false;
         btnIssueService.Visible = false;
         if(cbClassCode.SelectedIndex >= 0 && cbFunction.SelectedIndex >= 0) {
            attr = new AttrData(Data.ClassCodeData[cbClassCode.SelectedIndex][cbFunction.SelectedIndex]);
            btnIssueGet.Visible = attr.HasGet;
            btnIssueSet.Visible = attr.HasSet;
            btnIssueService.Visible = attr.HasService;
         }

         SetButtonEnables();
      }

      private void btnViewTraffic_Click(object sender, EventArgs e) {
         CloseTrafficFile(true);
      }

      private void btnViewLog_Click(object sender, EventArgs e) {
         CloseLogFile(true);
      }

      private void btnReadAll_Click(object sender, EventArgs e) {
         // Get is assumed for read all request
         AllGood = true;
         for (int i = 0; i < cbClassCode.Items.Count && AllGood; i++) {
            cbClassCode.SelectedIndex = i;
            this.Refresh();
            // Establish the connection
            btnForwardOpen_Click(null, null);
            // Issue commands for this group
            for (int j = 0; j < cbFunction.Items.Count && AllGood; j++) {
               cbFunction.SelectedIndex = j;
               if (attr.HasGet && !attr.Ignore) {
                  this.Refresh();
                  btnIssueGet_Click(null, null);
               }
            }
            // Close out the connection
            btnForwardClose_Click(null, null);
         }


      }

      private void btnCom_Click(object sender, EventArgs e) {
         if (EIP.SessionIsOpen) {
            bool OpenCloseForward = !EIP.ForwardIsOpen;
            if (OpenCloseForward) {
               EIP.ForwardOpen();
            }
            int val = ComIsOn ? 0 : 1;
            if (EIP.WriteOneAttribute(eipClassCode.IJP_operation, (byte)eipIJP_operation.Online_Offline, EIP.ToBytes((uint)val, 1))) {
               GetComSetting();
               if (ComIsOn) {
                  GetAutoReflectionSetting();
                  GetMgmtSetting();
               }
            }
            if (OpenCloseForward) {
               EIP.ForwardClose();
            }
         }
         SetButtonEnables();
      }

      private void btnManagementFlag_Click(object sender, EventArgs e) {
         if (EIP.SessionIsOpen) {
            int val = MgmtIsOn ? 0 : 2;
            if (EIP.WriteOneAttribute(eipClassCode.Index, (byte)eipIndex.Start_Stop_Management_Flag, EIP.ToBytes((uint)val, 1))) {
               GetMgmtSetting();
            }
         }
         SetButtonEnables();
      }

      private void btnAutoReflection_Click(object sender, EventArgs e) {
         if (EIP.SessionIsOpen) {
            int val = AutoReflIsOn ? 0 : 1;
            if (EIP.WriteOneAttribute(eipClassCode.Index, (byte)eipIndex.Automatic_reflection, EIP.ToBytes((uint)val, 1))) {
               GetAutoReflectionSetting();
            }
         }
         SetButtonEnables();
      }

      private void tclClasses_SelectedIndexChanged(object sender, EventArgs e) {
         HitachiBrowser_Resize(null, null);
         indexAttr.RefreshExtras();
         oprAttr.RefreshExtras();
         pdmAttr.RefreshExtras();
         psAttr.RefreshExtras();
         pFmtAttr.RefreshExtras();
         calAttr.RefreshExtras();
         sRulesAttr.RefreshExtras();
         countAttr.RefreshExtras();
         unitInfoAttr.RefreshExtras();
         envirAttr.RefreshExtras();
         mgmtAttr.RefreshExtras();
         //userPatAttr.RefreshExtras();

      }

      private void cmLogClear_Click(object sender, EventArgs e) {
         lstErrors.Items.Clear();
      }

      private void cmLogView_Click(object sender, EventArgs e) {
         string ViewFilename = CreateFileName(txtSaveFolder.Text, "View");
         StreamWriter ViewStream = new StreamWriter(ViewFilename, false);
         for (int i = 0; i < lstErrors.Items.Count; i++) {
            ViewStream.WriteLine(lstErrors.Items[i].ToString());
         }
         ViewStream.Flush();
         ViewStream.Close();
         Process.Start("notepad.exe", ViewFilename);
      }

      private void lstErrors_MouseDoubleClick(object sender, MouseEventArgs e) {
         cmLogView_Click(null, null);
      }

      private void btnStop_Click(object sender, EventArgs e) {
         AllGood = false;
      }

      private void EIP_Error(EIP sender, string msg) {
         AllGood = false;
         lstErrors.Items.Add(msg);
      }

      public void EIP_Log(EIP sender, string msg) {
         LogFileStream.WriteLine(msg);
         lstErrors.Items.Add(msg);
         lstErrors.SelectedIndex = lstErrors.Items.Count - 1;
      }

      private void EIP_StateChanged(EIP sender, string msg) {
         if (!EIP.SessionIsOpen) {
            AllGood = false;
         }
         SetButtonEnables();
      }

      private void EIP_ReadComplete(EIP sender, EIPEventArg e) {
         txtStatus.Text = EIP.GetStatus;
         switch (e.Access) {
            case eipAccessCode.Get:
               txtCount.Text = EIP.GetDataLength.ToString();
               txtData.Text = EIP.GetDataValue;
               txtDataBytes.Text = EIP.GetBytes(EIP.GetData, 0, EIP.GetDataLength);
               break;
            case eipAccessCode.Set:
            case eipAccessCode.Service:
               txtCount.Text = EIP.SetDataLength.ToString();
               txtData.Text = EIP.GetBytes(EIP.SetData, 0, EIP.SetDataLength);
               txtDataBytes.Text = EIP.GetBytes(EIP.SetData, 0, EIP.SetDataLength);
               break;
         }
         EIP_Log(sender, $"{EIP.LastIO} -- {e.Access}/{e.Class}/{EIP.GetAttributeName(EIP.Class, e.Attribute)} Complete");
      }

      private void btnProperties_Click(object sender, EventArgs e) {
         using (AttrProperties p = new AttrProperties(this, EIP, cbClassCode.SelectedIndex, cbFunction.SelectedIndex)) {
            p.ShowDialog(this);
         }
      }

      #endregion

      #region Service Routines

      private void BuildAttributeDictionary(eipClassCode[] cc) {
         Data.AttrDict = new Dictionary<byte, byte, AttrData>();
         for (int i = 0; i < cc.Length; i++) {
            int[] ClassAttr = (int[])Data.ClassCodeAttributes[i].GetEnumValues();
            for (int j = 0; j < ClassAttr.Length; j++) {
               Data.AttrDict.Add((byte)cc[i], (byte)ClassAttr[j], Data.GetAttrData((byte)cc[i], (Byte)ClassAttr[j]));
            }

         }

      }

      private void VerifyAddressAndPort() {
         if (!Int32.TryParse(txtPort.Text, out port)) {
            port = 44818;
            txtPort.Text = port.ToString();
         }
         if (!System.Net.IPAddress.TryParse(txtIPAddress.Text, out System.Net.IPAddress IPAddress)) {
            txtIPAddress.Text = "192.168.0.1";
            IPAddress = IPAddress.Parse(txtIPAddress.Text);
         }
         this.IPAddress = IPAddress.ToString();
      }

      private void BuildTrafficFile() {
         TrafficFilename = CreateFileName(txtSaveFolder.Text, "Traffic");
         TrafficFileStream = new StreamWriter(TrafficFilename, false);
      }

      private void BuildLogFile() {
         LogFilename = CreateFileName(txtSaveFolder.Text, "Log");
         LogFileStream = new StreamWriter(LogFilename, false);
      }

      private void CloseTrafficFile(bool view) {
         TrafficFileStream.Flush();
         TrafficFileStream.Close();
         if (view) {
            Process.Start("notepad.exe", TrafficFilename);
            BuildTrafficFile();
         }
      }

      private void CloseLogFile(bool view) {
         LogFileStream.Flush();
         LogFileStream.Close();
         if (view) {
            Process.Start("notepad.exe", LogFilename);
            BuildLogFile();
         }
      }

      private string CreateFileName(string directory, string s) {
         return Path.Combine(directory, $"{s}{DateTime.Now.ToString("yyMMdd-HHmmss")}.csv");
      }

      private bool GetComSetting() {
         bool result;
         if (EIP.ReadOneAttribute(eipClassCode.IJP_operation, (byte)eipIJP_operation.Online_Offline, out string val, DataFormats.Decimal)) {
            if (val == "1") {
               btnCom.Text = "COM\n1";
               btnCom.BackColor = Color.LightGreen;
               ComIsOn = true;
            } else {
               btnCom.Text = "COM\n0";
               btnCom.BackColor = Color.Pink;
               ComIsOn = false;
            }
            result = true;
         } else {
            btnCom.Text = "COM\n?";
            btnCom.BackColor = SystemColors.Control;
            ComIsOn = false;
            result = false;
         }
         SetButtonEnables();
         return result;
      }

      private bool GetMgmtSetting() {
         bool result;
         if (EIP.ReadOneAttribute(eipClassCode.Index, (byte)eipIndex.Start_Stop_Management_Flag, out string val, DataFormats.Decimal)) {
            if (val != "0") {
               btnManagementFlag.Text = $"S/S Management\n{val}";
               btnManagementFlag.BackColor = Color.Pink;
               MgmtIsOn = true;
            } else {
               btnManagementFlag.Text = "S/S Management\n0";
               btnManagementFlag.BackColor = Color.LightGreen;
               MgmtIsOn = false;
            }
            result = true;
         } else {
            btnManagementFlag.Text = "S/S Management\n?";
            btnManagementFlag.BackColor = SystemColors.Control;
            MgmtIsOn = false;
            result = false;
         }
         SetButtonEnables();
         return result;
      }

      private bool GetAutoReflectionSetting() {
         bool result;
         if (EIP.ReadOneAttribute(eipClassCode.Index, (byte)eipIndex.Automatic_reflection, out string val, DataFormats.Decimal)) {
            if (val == "1") {
               btnAutoReflection.Text = "Auto Reflection\n1";
               btnAutoReflection.BackColor = Color.Pink;
               AutoReflIsOn = true;
            } else {
               btnAutoReflection.Text = "Auto Reflection\n0";
               btnAutoReflection.BackColor = Color.LightGreen;
               AutoReflIsOn = false;
            }
            result = true;
         } else {
            btnAutoReflection.Text = "Auto Reflection\n?";
            btnAutoReflection.BackColor = SystemColors.Control;
            AutoReflIsOn = false;
            result = false;
         }
         SetButtonEnables();
         return result;
      }

      void SetButtonEnables() {
         btnStartSession.Enabled = !EIP.SessionIsOpen;
         btnEndSession.Enabled = EIP.SessionIsOpen;
         btnForwardOpen.Enabled = EIP.SessionIsOpen && !EIP.ForwardIsOpen;
         btnForwardClose.Enabled = EIP.SessionIsOpen && EIP.ForwardIsOpen;
         btnIssueGet.Enabled = btnIssueSet.Enabled = btnIssueService.Enabled = 
            EIP.SessionIsOpen && cbClassCode.SelectedIndex >= 0 && cbFunction.SelectedIndex >= 0;

         btnCom.Enabled = EIP.SessionIsOpen;
         btnAutoReflection.Enabled = EIP.SessionIsOpen && ComIsOn;
         btnManagementFlag.Enabled = EIP.SessionIsOpen && ComIsOn;

         btnReadAll.Enabled = EIP.SessionIsOpen && ComIsOn;

         if (initComplete) {
            indexAttr.SetButtonEnables();
            oprAttr.SetButtonEnables();
            pdmAttr.SetButtonEnables();
            psAttr.SetButtonEnables();
            pFmtAttr.SetButtonEnables();
            calAttr.SetButtonEnables();
            sRulesAttr.SetButtonEnables();
            countAttr.SetButtonEnables();
            unitInfoAttr.SetButtonEnables();
            envirAttr.SetButtonEnables();
            mgmtAttr.SetButtonEnables();
            processXML.SetButtonEnables();
            //userPatAttr.SetButtonEnables();
         }

      }

      #endregion

   }
}