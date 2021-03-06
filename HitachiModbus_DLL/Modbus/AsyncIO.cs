﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Serialization;

namespace Modbus_DLL {
   public class AsyncIO {

      #region Events

      // Event Logging
      public event LogHandler Log;
      public delegate void LogHandler(object sender, string msg);

      // I/O Complete
      public event CompleteHandler Complete;
      public delegate void CompleteHandler(object sender, AsyncComplete status);

      #endregion

      #region Data Declarations

      Form parent;

      public enum TaskType {
         Connect,
         Disconnect,
         Send,
         SendLabel,
         Retrieve,
         WriteData,
         ReadData,
         RecallMessage,
         AddMessage,
         Substitutions,
         DeleteMessage,
         IssueccIJP,
         GetStatus,
         GetMessages,
         GetErrors,
         AckOnly,
         Specification,
         WritePattern,
         TimedDelay,
         Exit,
      }

      // Do the work in the background
      Thread t;

      // Use Blocking Collection to avoid spin waits
      public BlockingCollection<ModbusPkt> Tasks = new BlockingCollection<ModbusPkt>();
      ModbusPkt pkt;

      // Printer to use for I/O
      Modbus MB = null;

      public bool LogIO { get; set; }

      // Remote Operations
      private enum RemoteOps {
         Start = 0,
         Stop = 1,
         Ready = 2,
         StandBy = 3,
         ClearFault = 4,
      }

      #endregion

      #region Constructors and Destructors

      public AsyncIO(Form parent, Modbus MB) {
         // 
         this.parent = parent;
         this.MB = MB;
         t = new Thread(processTasks);
         t.Start();
      }

      #endregion

      #region Task Processing

      private void processTasks() {
         bool done = false;
         // Just one big loop
         while (!done) {
            try {
               // Wait for the next request
               pkt = Tasks.Take();
               switch (pkt.Type) {
                  case TaskType.Connect:
                     Connect(pkt);
                     break;
                  case TaskType.Disconnect:
                     Disconnect(pkt);
                     break;
                  case TaskType.Send:
                     Send(pkt);
                     break;
                  case TaskType.SendLabel:
                     SendLabel(pkt);
                     break;
                  case TaskType.Retrieve:
                     Retrieve(pkt);
                     break;
                  case TaskType.WriteData:
                     WriteData(pkt);
                     break;
                  case TaskType.ReadData:
                     ReadData(pkt);
                     break;
                  case TaskType.RecallMessage:
                     RecallMessage(pkt);
                     break;
                  case TaskType.Substitutions:
                     DoSubstitutions(pkt);
                     break;
                  case TaskType.AddMessage:
                     AddMessage(pkt);
                     break;
                  case TaskType.DeleteMessage:
                     DeleteMessage(pkt);
                     break;
                  case TaskType.IssueccIJP:
                     IssueccIJP(pkt);
                     break;
                  case TaskType.GetStatus:
                     GetStatus(pkt);
                     break;
                  case TaskType.GetMessages:
                     GetMessages(pkt);
                     break;
                  case TaskType.GetErrors:
                     GetErrors(pkt);
                     break;
                  case TaskType.AckOnly:
                     AckOnly(pkt);
                     break;
                  case TaskType.Specification:
                     Specification(pkt);
                     break;
                  case TaskType.WritePattern:
                     WritePattern(pkt);
                     break;
                  case TaskType.TimedDelay:
                     Thread.Sleep(pkt.TimeDelay);
                     AckOnly(pkt);
                     break;
                  case TaskType.Exit:
                     done = true;
                     break;
                  default:
                     return;
               }
            } catch {
               AsyncComplete ac = new AsyncComplete(MB, pkt) { Success = false };
               parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
            }
         }
      }

      private void WritePattern(ModbusPkt pkt) {
         bool success = MB.SendFixedLogo(pkt.DotMatrix, pkt.Addr, pkt.DataA);
         AsyncComplete ac = new AsyncComplete(MB, pkt) { Success = success };
         parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
      }

      private void Specification(ModbusPkt pkt) {
         bool success = false;
         switch (pkt.PrinterSpec) {
            case ccPS.Character_Height:
            case ccPS.Character_Width:
            case ccPS.Repeat_Interval:
               int st = MB.GetDecAttribute(ccUS.Operation_Status);
               if (st >= 0x32) { // 0x30 and 0x31 indicate already in standby
                  success = MB.SetAttribute(ccIJP.Remote_operation, (int)RemoteOps.StandBy);
                  success = MB.SetAttribute(pkt.PrinterSpec, pkt.Data);
                  success = MB.SetAttribute(ccIJP.Remote_operation, (int)RemoteOps.Ready);
               } else {
                  success = MB.SetAttribute(pkt.PrinterSpec, pkt.Data);
               }
               break;
            default:
               break;
         }
         AsyncComplete ac = new AsyncComplete(MB, pkt) { Success = success };
         parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
      }

      private void AckOnly(ModbusPkt pkt) {
         AsyncComplete ac = new AsyncComplete(MB, pkt) { Success = true };
         parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
      }

      private void Connect(ModbusPkt pkt) {
         bool success = MB.Connect(pkt.IpAddress, pkt.IpPort);
         AsyncComplete ac = new AsyncComplete(MB, pkt) { Success = success };
         parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
      }

      private void Disconnect(ModbusPkt pkt) {
         MB.Disconnect();
         AsyncComplete ac = new AsyncComplete(MB, pkt) { Success = true };
         parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
      }

      private void Send(ModbusPkt pkt) {
         SendRetrieveXML send = new SendRetrieveXML(MB);
         send.Log += Modbus_Log;
         try {
            send.SendXML(pkt.Data);
         } finally {
            string logXML = send.LogXML;
            AsyncComplete ac = new AsyncComplete(MB, pkt) { Resp2 = logXML };
            parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
            send.Log -= Modbus_Log;
            send = null;
         }
      }

      private void SendLabel(ModbusPkt pkt) {
         SendRetrieveXML send = new SendRetrieveXML(MB);
         send.Log += Modbus_Log;
         try {
            send.SendXML(pkt.Label);
         } finally {
            string logXML = send.LogXML;
            AsyncComplete ac = new AsyncComplete(MB, pkt) { Resp2 = logXML };
            parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
            send.Log -= Modbus_Log;
            send = null;
         }
      }

      private void Retrieve(ModbusPkt pkt) {
         string msgXML = string.Empty;
         string logXML = string.Empty;
         SendRetrieveXML retrieve = new SendRetrieveXML(MB);
         retrieve.Log += Modbus_Log;
         try {
            msgXML = retrieve.Retrieve();
         } finally {
            logXML = retrieve.LogXML;
            AsyncComplete ac = new AsyncComplete(MB, pkt) { Resp1 = msgXML, Resp2 = logXML };
            parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
            retrieve.Log -= Modbus_Log;
            retrieve = null;
         }
      }

      private void WriteData(ModbusPkt pkt) {
         MB.SetAttribute(ccIDX.Start_Stop_Management_Flag, 1);
         bool success = MB.SetAttribute(pkt.DevAddr, pkt.Addr, pkt.DataA);
         MB.SetAttribute(ccIDX.Start_Stop_Management_Flag, 2);
         AsyncComplete ac = new AsyncComplete(MB, pkt) { Success = success };
         parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
      }

      private void ReadData(ModbusPkt pkt) {
         MB.GetAttribute(pkt.fc, pkt.DevAddr, pkt.Addr, pkt.Len, out byte[] data);
         AsyncComplete ac = new AsyncComplete(MB, pkt) { DataA = data };
         parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
      }

      private void RecallMessage(ModbusPkt pkt) {
         bool success = MB.SetAttribute(ccPDR.Recall_Message, pkt.Value);
         AsyncComplete ac = new AsyncComplete(MB, pkt) { Success = success };
         parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
      }

      private void AddMessage(ModbusPkt pkt) {
         bool success = MB.DeleteMessage(pkt.Value);
         MB.SetAttribute(ccIDX.Start_Stop_Management_Flag, 1);
         success &= MB.SetAttribute(ccPDR.MessageName, pkt.Data);
         success &= MB.SetAttribute(ccPDR.Message_Number, pkt.Value);
         MB.SetAttribute(ccIDX.Start_Stop_Management_Flag, 2);
         AsyncComplete ac = new AsyncComplete(MB, pkt) { Success = success };
         parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
      }

      private void DeleteMessage(ModbusPkt pkt) {
         bool success = MB.DeleteMessage(pkt.Value);
         AsyncComplete ac = new AsyncComplete(MB, pkt) { Success = success };
         parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
      }

      private void GetMessages(ModbusPkt pkt) {
         List<string> msgs = new List<string>();
         string[] s = new string[3];
         // For now, look at the first 48 only.  Need to implement block read
         AttrData attrCount = MB.GetAttrData(ccMM.Registration);
         for (int i = 0; i < Math.Min(3, attrCount.Count); i++) {
            int reg = MB.GetDecAttribute(ccMM.Registration, i);
            if (reg == 0) {
               continue;
            }
            for (int j = 15; j >= 0; j--) {
               if ((reg & (1 << j)) > 0) {
                  int n = i * 16 - j + 15; // 1-origin
                  MB.SetAttribute(ccIDX.Start_Stop_Management_Flag, 1);
                  MB.SetAttribute(ccIDX.Message_Number, n + 1);         // Load the message into input registers
                  MB.SetAttribute(ccIDX.Start_Stop_Management_Flag, 2);
                  s[0] = MB.GetHRAttribute(ccMM.Group_Number);
                  s[1] = MB.GetHRAttribute(ccMM.Message_Number);
                  s[2] = MB.GetHRAttribute(ccMM.Message_Name);
                  msgs.Add(string.Join(",", s));
               }
            }
         }
         AsyncComplete ac = new AsyncComplete(MB, pkt) { MultiLine = msgs.ToArray() };
         parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
      }

      private void GetErrors(ModbusPkt pkt) {
         int errCount = MB.GetDecAttribute(ccAH.Message_Count);
         string[] errs = new string[errCount];
         AttrData attr = MB.GetAttrData(ccAH.Year);
         int len = attr.Stride;
         for (int i = 0; i < errCount; i++) {
            MB.GetAttribute(Modbus.FunctionCode.ReadInput, 1, attr.Val + i * len, len * 2, out byte[] data);
            int year = (data[0] << 8) + data[1];
            int month = data[3];
            int day = data[5];
            int hour = data[7];
            int minute = data[9];
            int second = data[11];
            int fault = (data[12] << 8) + data[13];
            errs[i] = $"{fault:###} {year}/{month:##}/{day:##} {hour:##}:{minute:##}:{second:##}";
         }
         AsyncComplete ac = new AsyncComplete(MB, pkt) { MultiLine = errs, Value = errCount };
         parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
      }

      private void GetStatus(ModbusPkt pkt) {
         char c = (char)MB.GetDecAttribute(ccUS.Communication_Status);
         char r = (char)MB.GetDecAttribute(ccUS.Receive_Status);
         char o = (char)MB.GetDecAttribute(ccUS.Operation_Status);
         char w = (char)MB.GetDecAttribute(ccUS.Warning_Status);
         AsyncComplete ac = new AsyncComplete(MB, pkt) {
            Status = new string(new char[] { (char)2, (char)0x31, c, r, o, w, (char)3 })
         };
         parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
      }

      private void IssueccIJP(ModbusPkt pkt) {
         MB.SetAttribute(pkt.Attribute, pkt.Value);
         char c = (char)MB.GetDecAttribute(ccUS.Communication_Status);
         char r = (char)MB.GetDecAttribute(ccUS.Receive_Status);
         char o = (char)MB.GetDecAttribute(ccUS.Operation_Status);
         char w = (char)MB.GetDecAttribute(ccUS.Warning_Status);
         AsyncComplete ac = new AsyncComplete(MB, pkt) {
            Attribute = pkt.Attribute,
            Value = pkt.Value,
            Status = new string(new char[] { (char)2, (char)0x31, c, r, o, w, (char)3 })
         };
         parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
      }

      private void Modbus_Log(object sender, string msg) {
         if (Log != null) {
            parent.BeginInvoke(new EventHandler(delegate { Log(sender, msg); }));
         }
      }

      private void DoSubstitutions(ModbusPkt pkt) {
         bool success = false;
         Substitution sub = null;
         SendRetrieveXML sr = new SendRetrieveXML(MB);
         if (pkt.substitution == null) {
            sub = sr.RetrieveAllSubstitutions(1);
         } else {
            sr.SendSubstitution(pkt.substitution);
         }
         sr = null;
         AsyncComplete ac = new AsyncComplete(MB, pkt) { Success = success, substitution = sub };
         parent.Invoke(new EventHandler(delegate { Complete(this, ac); }));
      }

      #endregion

      #region Service Routines

      #endregion

   }

   public class ModbusPkt {

      #region Constructors, Destructors, and Properties

      public AsyncIO.TaskType Type { get; set; }
      public int DotMatrix { get; set; }
      public string Data { get; set; } = string.Empty;
      public byte[] DataA { get; set; }
      public DateTime When { get; set; } = DateTime.Now;
      public bool View { get; set; } = false;
      public ccIJP Attribute { get; set; }
      public int Value { get; set; }
      public string IpAddress { get; set; }
      public string IpPort { get; set; }
      public Modbus.FunctionCode fc { get; set; }
      public byte DevAddr { get; set; }
      public int Addr { get; set; }
      public int Len { get; set; }
      public object Packet { get; set; }
      public Serialization.Lab Label { get; set; }
      public Serialization.Substitution substitution { get; set; }
      public Modbus_DLL.ccPS PrinterSpec { get; set; }
      public int TimeDelay { get; set; }

      public ModbusPkt(AsyncIO.TaskType Type) {
         this.Type = Type;
      }

      public ModbusPkt(AsyncIO.TaskType Type, string Data) {
         this.Type = Type;
         this.Data = Data;
      }

      public ModbusPkt(AsyncIO.TaskType Type, ccIJP Attribute, int value) {
         this.Type = Type;
         this.Attribute = Attribute;
         this.Value = value;
      }

      #endregion

   }

   public class AsyncComplete {

      #region Constructors, Destructors, and Properties

      public Modbus Printer { get; set; }
      public AsyncIO.TaskType Type { get; set; }
      public string Resp1 { get; set; }
      public string Resp2 { get; set; }
      public ccIJP Attribute { get; set; }
      public int Value { get; set; }
      public string[] MultiLine { get; set; }
      public bool Success { get; set; } = true;
      public byte[] DataA { get; set; }
      public object Packet { get; set; }
      public string Status { get; set; }
      public Substitution substitution { get; set; }

      public AsyncComplete(Modbus p, ModbusPkt pkt) {
         this.Printer = p;
         this.Type = pkt.Type;
         this.Packet = pkt.Packet;
      }

      #endregion

   }

}
