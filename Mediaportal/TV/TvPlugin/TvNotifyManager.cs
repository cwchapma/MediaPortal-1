#region Copyright (C) 2005-2010 Team MediaPortal

// Copyright (C) 2005-2010 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using MediaPortal.GUI.Library;
using MediaPortal.Profile;
using Mediaportal.TV.Server.TVControl;
using Mediaportal.TV.Server.TVDatabase.Gentle;
using Mediaportal.TV.TvPlugin.TCPEventClient;

namespace Mediaportal.TV.TvPlugin
{
  public class TvNotifyManager
  {
    private Timer _timer;
    // flag indicating that notifies have been added/changed/removed
    private static bool _notifiesListChanged;
    private static bool _enableRecNotification;
    private static bool _busy;
    private int _preNotifyConfig;

    //list of all notifies (alert me n minutes before program starts)
    private IList<Program> _notifiesList;

    private User _dummyuser;
    private TCPClient _tcpClient;

    public TvNotifyManager(TCPClient tcpClient)
    {
      using (Settings xmlreader = new MPSettings())
      {
        _enableRecNotification = xmlreader.GetValueAsBool("mytv", "enableRecNotifier", false);
        _preNotifyConfig = xmlreader.GetValueAsInt("mytv", "notifyTVBefore", 300);
      }

      _tcpClient = tcpClient;              

      _busy = false;
      _timer = new Timer();
      _timer.Stop();
      // check every 15 seconds for notifies
      _dummyuser = new User();
      _dummyuser.IsAdmin = false;
      _dummyuser.Name = "Free channel checker";
      _timer.Interval = 15000;
      _timer.Enabled = true;
      _timer.Tick += new EventHandler(_timer_Tick);
    }

    private void _udpEventClient_RecordingFailed(int idSchedule)
    {
      Schedule failedSchedule = Schedule.Retrieve(idSchedule);
      if (failedSchedule != null)
    {
        Log.Debug("TVPlugIn: No free card available for {0}. Notifying user.", failedSchedule.ProgramName);

        Notify(GUILocalizeStrings.Get(1004),
               String.Format("{0}. {1}", failedSchedule.ProgramName, GUILocalizeStrings.Get(200055)),
               TVHome.Navigator.Channel);
    }
    }

    private void _udpEventClient_RecordingStarted(int idRecording)
    {
      Recording startedRec = Recording.Retrieve(idRecording);
      if (startedRec != null)
      {
        Schedule parentSchedule = startedRec.ReferencedSchedule();
        if (parentSchedule != null && parentSchedule.IdSchedule > 0)
        {
          string endTime = string.Empty;
          if (parentSchedule != null)
          {
            endTime = parentSchedule.EndTime.AddMinutes(parentSchedule.PostRecordInterval).ToString("t",
                                                                                                    CultureInfo.
                                                                                                      CurrentCulture.
                                                                                                      DateTimeFormat);
            string text = String.Format("{0} {1}-{2}",
                                        startedRec.Title,
                                        startedRec.StartTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat),
                                        endTime);
            //Recording started                            
            Notify(GUILocalizeStrings.Get(1446), text, startedRec.ReferencedChannel());
      }
    }
    }
    }

    private void _udpEventClient_RecordingEnded(int idRecording)
    {
      Recording stoppedRec = Recording.Retrieve(idRecording);
      if (stoppedRec != null)
      {
        string textPrg = "";
        IList<Program> prgs = Program.RetrieveByTitleAndTimesInterval(stoppedRec.Title, stoppedRec.StartTime,
                                                                      stoppedRec.EndTime);

        Program prg = null;
        if (prgs != null && prgs.Count > 0)
        {
          prg = prgs[0];
          }
        if (prg != null)
        {
          textPrg = String.Format("{0} {1}-{2}",
                                  prg.Title,
                                  prg.StartTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat),
                                  prg.EndTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat));
        }
        else
        {          
          textPrg = String.Format("{0} {1}-{2}",
                                  stoppedRec.Title,
                                  stoppedRec.StartTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat),
                                  DateTime.Now.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat));
        }
        //Recording stopped:                                    
        Notify(GUILocalizeStrings.Get(1447), textPrg, stoppedRec.ReferencedChannel());
      }
    }

    public void Start()
      {
      Log.Info("TvNotify: start");

      if (_enableRecNotification)
      {
        _tcpClient.RecordingEnded += new TCPClient.RecordingEndedDelegate(_udpEventClient_RecordingEnded);
        _tcpClient.RecordingStarted += new TCPClient.RecordingStartedDelegate(_udpEventClient_RecordingStarted);
        _tcpClient.RecordingFailed += new TCPClient.RecordingFailedDelegate(_udpEventClient_RecordingFailed);
      }
      _timer.Start();
    }

    public void Stop()
    {
      Log.Info("TvNotify: stop");

      if (_enableRecNotification)
      {
        _tcpClient.RecordingEnded -= new TCPClient.RecordingEndedDelegate(_udpEventClient_RecordingEnded);
        _tcpClient.RecordingStarted -= new TCPClient.RecordingStartedDelegate(_udpEventClient_RecordingStarted);
        _tcpClient.RecordingFailed -= new TCPClient.RecordingFailedDelegate(_udpEventClient_RecordingFailed);
      }
      _timer.Stop();
    }

    public static bool RecordingNotificationEnabled
      {
      get { return _enableRecNotification; }
        }


    public static void OnNotifiesChanged()
    {
      Log.Info("TvNotify:OnNotifiesChanged");
      _notifiesListChanged = true;
    }

   

   

    private void LoadNotifies()
    {
      try
      {
        Log.Info("TvNotify:LoadNotifies");
        _notifiesList = Program.RetrieveAllNotifications();

        if (_notifiesList != null)
        {
          Log.Info("TvNotify: {0} notifies", _notifiesList.Count);
        }

      }
      catch (Exception e)
      {
        Log.Error("TvNotify:LoadNotifies exception : {0}", e.Message);
      }
    }

    private bool Notify(string heading, string mainMsg, Channel channel)
    {
      Log.Info("send rec notify");
      GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_NOTIFY_REC, 0, 0, 0, 0, 0, null);
      msg.Label = heading;
      msg.Label2 = mainMsg;
      msg.Object = channel;
      GUIGraphicsContext.SendMessage(msg);
      msg = null;
      Log.Info("send rec notify done");
      return true;
    }

    private void ProcessNotifies(DateTime preNotifySecs)
    {
      if (_notifiesListChanged)
      {
        LoadNotifies();
        _notifiesListChanged = false;
      }
      if (_notifiesList != null && _notifiesList.Count > 0)
      {
        foreach (Program program in _notifiesList)
        {
          if (preNotifySecs > program.StartTime)
          {
            Log.Info("Notify {0} on {1} start {2}", program.Title, program.ReferencedChannel().DisplayName,
                     program.StartTime);
            program.Notify = false;
            program.Persist();
            TVProgramDescription tvProg = new TVProgramDescription();
            tvProg.Channel = program.ReferencedChannel();
            tvProg.Title = program.Title;
            tvProg.Description = program.Description;
            tvProg.Genre = program.Genre;
            tvProg.StartTime = program.StartTime;
            tvProg.EndTime = program.EndTime;

            _notifiesList.Remove(program);
            Log.Info("send notify");
            GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_NOTIFY_TV_PROGRAM, 0, 0, 0, 0, 0, null);
            msg.Object = tvProg;
            GUIGraphicsContext.SendMessage(msg);
            msg = null;
            Log.Info("send notify done");
            return;
          }
        }
      }
    }

    private void _timer_Tick(object sender, EventArgs e)
    {
      try
      {
        if (!TVHome.Connected)
        {
          return;
        }
        
        if (_busy)
        {
          return;
        }

        _busy = true;

        if (!TVHome.Connected)
        {
          return;
        }

      
        DateTime preNotifySecs = DateTime.Now.AddSeconds(_preNotifyConfig);
        ProcessNotifies(preNotifySecs);
      }
      catch (Exception ex)
      {        
        Log.Error("Tv NotifyManager: Exception at timer_tick {0} st : {1}", ex.ToString(), Environment.StackTrace);
      }
      finally
      {
        _busy = false;
      }
    }
  }
}