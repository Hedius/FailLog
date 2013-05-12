﻿/* FailLog.cs

by PapaCharlie9, MorpheusX(AUT)

Permission is hereby granted, free of charge, to any person or organization
obtaining a copy of the software and accompanying documentation covered by
this license (the "Software") to use, reproduce, display, distribute,
execute, and transmit the Software, and to prepare derivative works of the
Software, and to permit third-parties to whom the Software is furnished to
do so, without restriction.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE, TITLE AND NON-INFRINGEMENT. IN NO EVENT
SHALL THE COPYRIGHT HOLDERS OR ANYONE DISTRIBUTING THE SOFTWARE BE LIABLE
FOR ANY DAMAGES OR OTHER LIABILITY, WHETHER IN CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.

*/

using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Timers;
using System.Web;
using System.Windows.Forms;
using System.Xml;

using PRoCon.Core;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Players;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
    /* Aliases */

    using EventType = PRoCon.Core.Events.EventType;
    using CapturableEvent = PRoCon.Core.Events.CapturableEvents;

    /* Main Class */

    public class FailLog : PRoConPluginAPI, IPRoConPluginInterface
    {
        /* Enums */

        public enum MessageType { Warning, Error, Exception, Normal, Debug };

        /* Constants & Statics */

        public const int CRASH_COUNT_HEURISTIC = 24; // player count difference signifies a blaze dump

        public const double CHECK_FOR_UPDATES_MINS = 12 * 60; // every 12 hours

        public const double MAX_LIST_PLAYERS_SECS = 80; // should be at least every 30 seconds

        public const int MIN_UPDATE_USAGE_COUNT = 10; // minimum number of plugin updates in use

        /* Classes */

        /* Inherited:
            this.PunkbusterPlayerInfoList = new Dictionary<String, CPunkbusterInfo>();
            this.FrostbitePlayerInfoList = new Dictionary<String, CPlayerInfo>();
        */

        // General
        private bool fIsEnabled;
        private int fServerUptime = -1;
        private bool fServerCrashed = false; // because fServerUptime >  fServerInfo.ServerUptime
        private DateTime fEnabledTimestamp = DateTime.MinValue;
        private bool fGotLogin;
        private CServerInfo fServerInfo;
        private DateTime fLastVersionCheckTimestamp;
        private Dictionary<String, String> fFriendlyMaps = null;
        private Dictionary<String, String> fFriendlyModes = null;
        private int fLastPlayerCount;
        private DateTime fLastListPlayersTimestamp;
        private String fHost;
        private String fPort;
        private int fMaxPlayers;
        private bool fJustConnected;
        private int fAfterPlayers;
        private int fLastUptime;
        private int fLastMaxPlayers;
        private double fSumOfSeconds;
        private int fHighPlayerCount;
        private bool fRestartInitiated;
        private Hashtable fConfigSettings;
        private Dictionary<String, DateTime> fConfigTimestamp;

        // Settings support
        private Dictionary<int, Type> fEasyTypeDict = null;
        private Dictionary<int, Type> fBoolDict = null;
        private Dictionary<int, Type> fListStrDict = null;

        // Settings

        /* ===== SECTION 1 - Settings ===== */

        public int DebugLevel;
        public bool EnableLogToFile;  // if true, sandbox must not be in sandbox!
        public String LogFile;
        public bool EnableWebLog;
        public int BlazeDisconnectHeuristic; // deprecated
        public double BlazeDisconnectHeuristicPercent;
        public double BlazeDisconnectWindowSeconds;
        public bool EnableRestartOnBlaze;
        public int RestartOnBlazeDelay;
        public bool EnableEmailOnBlaze;

        /* ===== SECTION 2 - Server Description ===== */

        public String GameServerType;
        public String RankedServerProvider;
        public String ServerOwnerOrCommunity;
        public String ContactInfo;
        public String ServerRegion; // Should this be an enum?
        public String BattlelogLink;
        public String AdditionalInformation;

        /* ===== SECTION 3 - Email Settings ===== */

        public List<String> EmailRecipients;
        public String EmailSender;
        public String EmailSubject;
        public List<String> EmailMessage;
        public String SMTPHostname;
        public int SMTPPort;
        public bool SMTPUseSSL;
        public String SMTPUsername;
        public String SMTPPassword;

        /* Constructor */

        public FailLog()
        {
            /* Private members */
            fIsEnabled = false;
            fServerUptime = 0;
            fServerCrashed = false;
            fGotLogin = false;
            fServerInfo = null;
            fLastVersionCheckTimestamp = DateTime.MinValue;
            fFriendlyMaps = new Dictionary<String, String>();
            fFriendlyModes = new Dictionary<String, String>();
            fLastPlayerCount = 0;
            fLastListPlayersTimestamp = DateTime.MinValue;
            fAfterPlayers = 0;
            fLastUptime = 0;
            fMaxPlayers = 0;
            fLastMaxPlayers = 0;
            fSumOfSeconds = 0;
            fHighPlayerCount = 0;
            fRestartInitiated = false;
            fConfigSettings = new Hashtable();
            fConfigTimestamp = new Dictionary<String, DateTime>();

            fEasyTypeDict = new Dictionary<int, Type>();
            fEasyTypeDict.Add(0, typeof(int));
            fEasyTypeDict.Add(1, typeof(Int16));
            fEasyTypeDict.Add(2, typeof(Int32));
            fEasyTypeDict.Add(3, typeof(Int64));
            fEasyTypeDict.Add(4, typeof(float));
            fEasyTypeDict.Add(5, typeof(long));
            fEasyTypeDict.Add(6, typeof(String));
            fEasyTypeDict.Add(7, typeof(string));
            fEasyTypeDict.Add(8, typeof(double));

            fBoolDict = new Dictionary<int, Type>();
            fBoolDict.Add(0, typeof(Boolean));
            fBoolDict.Add(1, typeof(bool));

            fListStrDict = new Dictionary<int, Type>();
            fListStrDict.Add(0, typeof(String[]));

            /* Settings */

            /* ===== SECTION 1 - Settings ===== */

            DebugLevel = 2;
            EnableLogToFile = false;
            LogFile = "fail.log";
            BlazeDisconnectHeuristic = CRASH_COUNT_HEURISTIC;
            BlazeDisconnectHeuristicPercent = 75.0;
            BlazeDisconnectWindowSeconds = 30;
            EnableWebLog = true;
            EnableRestartOnBlaze = false;
            RestartOnBlazeDelay = 0;
            EnableEmailOnBlaze = false;

            /* ===== SECTION 2 - Server Description ===== */

            GameServerType = "BF3";
            RankedServerProvider = String.Empty;
            ServerOwnerOrCommunity = String.Empty;
            ContactInfo = String.Empty;
            ServerRegion = String.Empty;
            BattlelogLink = String.Empty;
            AdditionalInformation = String.Empty;

            /* ===== SECTION 3 - Email Settings ===== */

            EmailRecipients = new List<String>();
            EmailSender = String.Empty;
            EmailSubject = "FailLog - Server %servername% blazed (%time%)!";

            EmailMessage = new List<String>();
            EmailMessage.Add("<h2 align=\"center\">FailLog - BlazeReport</h2>");
            EmailMessage.Add("<p>Your server '%servername%' (%serverip%:%serverport%) just blazed!<br />");
            EmailMessage.Add("Here's some information about the Blaze:</p>");
            EmailMessage.Add("<table border=\"1\">");
            EmailMessage.Add("<tr><th>Field</th><th>Value</th></tr>");
            EmailMessage.Add("<tr><td align=\"center\">UTC</td><td align=\"center\">%time%</td></tr>");
            EmailMessage.Add("<tr><td align=\"center\">Server</td><td align=\"center\">%servername%</td></tr>");
            EmailMessage.Add("<tr><td align=\"center\">Players</td><td align=\"center\">%playercount%</td></tr>");
            EmailMessage.Add("<tr><td align=\"center\">Map</td><td align=\"center\">%map%</td></tr>");
            EmailMessage.Add("<tr><td align=\"center\">Gamemode</td><td align=\"center\">%gamemode%</td></tr>");
            EmailMessage.Add("<tr><td align=\"center\">Round</td><td align=\"center\">%round%</td></tr>");
            EmailMessage.Add("<tr><td align=\"center\">Uptime</td><td align=\"center\">%uptime%</td></tr>");
            EmailMessage.Add("</table>");

            SMTPHostname = String.Empty;
            SMTPPort = 25;
            SMTPUseSSL = false;
            SMTPUsername = String.Empty;
            SMTPPassword = String.Empty;
        }

        // Properties

        public String FriendlyMap
        {
            get
            {
                if (fServerInfo == null) return "???";
                String r = null;
                return (fFriendlyMaps.TryGetValue(fServerInfo.Map, out r)) ? r : fServerInfo.Map;
            }
        }
        public String FriendlyMode
        {
            get
            {
                if (fServerInfo == null) return "???";
                String r = null;
                return (fFriendlyModes.TryGetValue(fServerInfo.GameMode, out r)) ? r : fServerInfo.GameMode;
            }
        }

        public String GetPluginName()
        {
            return "FailLog";
        }

        public String GetPluginVersion()
        {
            return "1.0.0.8";
        }

        public String GetPluginAuthor()
        {
            return "PapaCharlie9, MorpheusX(AUT)";
        }

        public String GetPluginWebsite()
        {
            return "https://github.com/PapaCharlie9/fail-log";
        }

        public String GetPluginDescription()
        {
            return FailLogUtils.HTML_DOC;
        }

        /* ======================== SETTINGS ============================= */

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            List<CPluginVariable> lstReturn = new List<CPluginVariable>();

            try
            {
                /* ===== SECTION 1 - Settings ===== */

                lstReturn.Add(new CPluginVariable("1 - Settings|Debug Level", DebugLevel.GetType(), DebugLevel));

                lstReturn.Add(new CPluginVariable("1 - Settings|Enable Log To File", EnableLogToFile.GetType(), EnableLogToFile));

                if (EnableLogToFile)
                {
                    lstReturn.Add(new CPluginVariable("1 - Settings|Log File", LogFile.GetType(), LogFile));
                }

                lstReturn.Add(new CPluginVariable("1 - Settings|Enable Web Log", EnableWebLog.GetType(), EnableWebLog));

                // deprecated: lstReturn.Add(new CPluginVariable("1 - Settings|Blaze Disconnect Heuristic", BlazeDisconnectHeuristic.GetType(), BlazeDisconnectHeuristic));
                lstReturn.Add(new CPluginVariable("1 - Settings|Blaze Disconnect Heuristic Percent", BlazeDisconnectHeuristicPercent.GetType(), BlazeDisconnectHeuristicPercent));

                lstReturn.Add(new CPluginVariable("1 - Settings|Blaze Disconnect Window Seconds", BlazeDisconnectWindowSeconds.GetType(), BlazeDisconnectWindowSeconds));

                lstReturn.Add(new CPluginVariable("1 - Settings|Enable Restart On Blaze", EnableRestartOnBlaze.GetType(), EnableRestartOnBlaze));

                if (EnableRestartOnBlaze)
                {
                    lstReturn.Add(new CPluginVariable("1 - Settings|Restart On Blaze Delay", RestartOnBlazeDelay.GetType(), RestartOnBlazeDelay));
                }

                lstReturn.Add(new CPluginVariable("1 - Settings|Enable Email On Blaze", EnableEmailOnBlaze.GetType(), EnableEmailOnBlaze));

                /* ===== SECTION 2 - Server Description ===== */

                lstReturn.Add(new CPluginVariable("2 - Server Description|Game Server Type", GameServerType.GetType(), GameServerType));

                lstReturn.Add(new CPluginVariable("2 - Server Description|Ranked Server Provider", RankedServerProvider.GetType(), RankedServerProvider));

                lstReturn.Add(new CPluginVariable("2 - Server Description|Server Owner Or Community", ServerOwnerOrCommunity.GetType(), ServerOwnerOrCommunity));

                lstReturn.Add(new CPluginVariable("2 - Server Description|Contact Info", ContactInfo.GetType(), ContactInfo));

                lstReturn.Add(new CPluginVariable("2 - Server Description|Server Region", ServerRegion.GetType(), ServerRegion));

                lstReturn.Add(new CPluginVariable("2 - Server Description|Battlelog Link", BattlelogLink.GetType(), BattlelogLink));

                lstReturn.Add(new CPluginVariable("2 - Server Description|Additional Information", AdditionalInformation.GetType(), AdditionalInformation));

                /* ===== SECTION 3 - Email Settings ===== */

                if (EnableEmailOnBlaze)
                {
                    lstReturn.Add(new CPluginVariable("3 - Email Settings|Email Recipients", typeof(String[]), EmailRecipients.ToArray()));

                    lstReturn.Add(new CPluginVariable("3 - Email Settings|Email Sender", EmailSender.GetType(), EmailSender));

                    lstReturn.Add(new CPluginVariable("3 - Email Settings|Email Subject", EmailSubject.GetType(), EmailSubject));

                    lstReturn.Add(new CPluginVariable("3 - Email Settings|Email Message", typeof(String[]), EmailMessage.ToArray()));

                    lstReturn.Add(new CPluginVariable("3 - Email Settings|SMTP Hostname", SMTPHostname.GetType(), SMTPHostname));

                    lstReturn.Add(new CPluginVariable("3 - Email Settings|SMTP Port", SMTPPort.GetType(), SMTPPort));

                    lstReturn.Add(new CPluginVariable("3 - Email Settings|SMTP Use SSL", SMTPUseSSL.GetType(), SMTPUseSSL));

                    lstReturn.Add(new CPluginVariable("3 - Email Settings|SMTP Username", SMTPUsername.GetType(), SMTPUsername));

                    lstReturn.Add(new CPluginVariable("3 - Email Settings|SMTP Password", SMTPPassword.GetType(), SMTPPassword));
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }

            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            return GetDisplayPluginVariables();
        }

        public void SetPluginVariable(String strVariable, String strValue)
        {
            if (fIsEnabled) DebugWrite(strVariable + " <- " + strValue, 6);

            try
            {
                String tmp = strVariable;
                int pipeIndex = strVariable.IndexOf('|');
                if (pipeIndex >= 0)
                {
                    pipeIndex++;
                    tmp = strVariable.Substring(pipeIndex, strVariable.Length - pipeIndex);
                }

                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                String propertyName = Regex.Replace(tmp, @"[^a-zA-Z_0-9]", String.Empty);

                FieldInfo field = this.GetType().GetField(propertyName, flags);

                Type fieldType = null;

                if (field != null)
                {
                    fieldType = field.GetValue(this).GetType();
                    if (fEasyTypeDict.ContainsValue(fieldType))
                    {
                        field.SetValue(this, TypeDescriptor.GetConverter(fieldType).ConvertFromString(strValue));
                    }
                    else if (fListStrDict.ContainsValue(fieldType))
                    {
                        if (DebugLevel >= 8) ConsoleDebug("String array " + propertyName + " <- " + strValue);
                        field.SetValue(this, CPluginVariable.DecodeStringArray(strValue));
                    }
                    else if (fBoolDict.ContainsValue(fieldType))
                    {
                        if (fIsEnabled) DebugWrite(propertyName + " strValue = " + strValue, 6);
                        if (Regex.Match(strValue, "true", RegexOptions.IgnoreCase).Success)
                        {
                            field.SetValue(this, true);
                        }
                        else
                        {
                            field.SetValue(this, false);
                        }
                    }
                    else if (fieldType.Equals(typeof(List<String>)))
                    {
                        String[] decodedArray = CPluginVariable.DecodeStringArray(strValue);
                        List<String> decodedList = new List<String>();
                        foreach (String arrayLine in decodedArray)
                        {
                            decodedList.Add(arrayLine);
                        }
                        field.SetValue(this, decodedList);
                    }
                    else
                    {
                        if (DebugLevel >= 8) ConsoleDebug("Unknown var " + propertyName + " with type " + fieldType);
                    }
                }
            }
            catch (System.Exception e)
            {
                ConsoleException(e);
            }
            finally
            {
                // Validate all values and correct if needed
                ValidateSettings(strVariable, strValue);
            }
        }

        private bool ValidateSettings(String strVariable, String strValue)
        {
            try
            {
                /* ===== SECTION 1 - Settings ===== */

                if (strVariable.Contains("Debug Level")) ValidateIntRange(ref DebugLevel, "Debug Level", 0, 9, 2, false);

                if (strVariable.Contains("Blaze Disconnect Heuristic Percent")) ValidateDoubleRange(ref BlazeDisconnectHeuristicPercent, "Blaze Disconnect Heuristic Percent", 33, 100, 75, false);

                if (strVariable.Contains("Blaze Disconnect Window Seconds")) ValidateDoubleRange(ref BlazeDisconnectWindowSeconds, "Blaze Disconnect Window Seconds", 30, 90, 30, false);

                if (strVariable.Contains("SMTP Port")) ValidateIntRange(ref SMTPPort, "SMTP Port", 0, 65535, 25, false);

                /* ===== SECTION 2 - Exclusions ===== */

                // All strings, no validation needed so far
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
            return true;
        }

        /* ======================== OVERRIDES ============================= */

        public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion)
        {
            fHost = strHostName;
            fPort = strPort;

            this.RegisterEvents(this.GetType().Name,
                "OnLogin",
                "OnServerInfo",
                "OnListPlayers",
                "OnMaxPlayers",

                // Configuration events
                "OnVersion",
                "OnServerName",
                "OnServerDescription",
                "OnServerMessage",
                "OnPunkbuster",
                "OnRanked",
                "OnIdleTimeout",
                "OnIdleBanRounds",
                "OnRoundRestartPlayerCount",
                "OnRoundStartPlayerCount",
                "OnGameModeCounter",
                "OnCtfRoundTimeModifier",
                "OnRoundLockdownCountdown",
                "OnRoundWarmupTimeout",
                "OnPremiumStatus",
                "OnGunMasterWeaponsPreset",
                "OnVehicleSpawnAllowed",
                "OnVehicleSpawnDelay",
                "OnBulletDamage",
                "OnOnlySquadLeaderSpawn",
                "OnSoldierHealth",
                "OnPlayerManDownTime",
                "OnPlayerRespawnTime",
                "OnHud",
                "OnNameTag",
                "OnFriendlyFire",
                "OnUnlockMode",
                "OnTeamBalance",
                "OnKillCam",
                "OnMiniMap",
                "OnCrossHair",
                "On3dSpotting",
                "OnMiniMapSpotting",
                "OnThirdPersonVehicleCameras",
                "OnTeamKillCountForKick",
                "OnTeamKillValueIncrease",
                "OnTeamKillValueDecreasePerSecond",
                "OnTeamKillValueForKick"
            );
        }

        public void OnPluginEnable()
        {
            fIsEnabled = true;
            fEnabledTimestamp = DateTime.Now;
            fJustConnected = true;

            ConsoleWrite("^bEnabled!^n Version = " + GetPluginVersion());

            GatherProconGoodies();

            ServerCommand("serverInfo");
            ServerCommand("admin.listPlayers", "all");

            CheckForPluginUpdate();
        }

        public void OnPluginDisable()
        {
            fIsEnabled = false;

            try
            {
                fEnabledTimestamp = DateTime.MinValue;

                Reset();
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnLogin()
        {
            if (!fIsEnabled) return;

            DebugWrite("^9Got ^bOnLogin^n", 8);
            try
            {
                if (fJustConnected) return;
                fGotLogin = true;
                fLastListPlayersTimestamp = DateTime.MinValue;
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset)
        {
            if (!fIsEnabled) return;

            DebugWrite("^9^bGot OnListPlayers^n, " + players.Count, 8);

            try
            {
                if (subset.Subset != CPlayerSubset.PlayerSubsetType.All) return;

                fJustConnected = false;

                bool resetWindow = false;
                bool blazed = false;

                // Check conditions
                if (fServerCrashed)
                { // serverInfo uptime decreased more than 2 seconds?
                    Failure("GAME_SERVER_RESTART", fLastPlayerCount);
                    resetWindow = true;
                }
                else if (fGotLogin)
                { // got initial login event?
                    Failure("PROCON_RECONNECTED", fLastPlayerCount);
                    resetWindow = true;
                }
                else if (fLastListPlayersTimestamp != DateTime.MinValue)
                {
                    double seconds = DateTime.Now.Subtract(fLastListPlayersTimestamp).TotalSeconds;
                    fSumOfSeconds = fSumOfSeconds + seconds;
                    if (seconds > MAX_LIST_PLAYERS_SECS)
                    {
                        Failure("NETWORK_CONGESTION", fLastPlayerCount);
                        resetWindow = true;
                    }
                    else
                    {
                        double current = players.Count;
                        double dLost = 0;
                        if (current < Convert.ToDouble(fLastPlayerCount))
                        {
                            dLost = fLastPlayerCount - current;
                        }
                        double dHighLost = 0;
                        if (current < fHighPlayerCount)
                        {
                            dHighLost = fHighPlayerCount - current;
                        }
                        double dLast = Math.Max(1, fLastPlayerCount); // make sure divisor is never 0
                        double dHigh = Math.Max(1.0, fHighPlayerCount); // make sure divisor is never 0

                        DebugWrite("^9Last = " + fLastPlayerCount + ", " + " current = " + current + ", lost = " + dLost + ", ratio = " + (dLost * 100.0 / dLast).ToString("F1") + ", window = " + fSumOfSeconds.ToString("F0") + ", high  = " + fHighPlayerCount + ", high lost = " + dHighLost + ", window ratio = " + (dHighLost * 100.0 / dHigh).ToString("F1"), 4);

                        if (dLast >= 12 && dLost <= dLast && (dLost * 100.0 / dLast) >= BlazeDisconnectHeuristicPercent)
                        {
                            // Single interval drop is big enough to detect
                            fAfterPlayers = players.Count;
                            Failure("BLAZE_DISCONNECT", fLastPlayerCount);
                            blazed = true;
                            resetWindow = true;
                        }
                        else if (fSumOfSeconds >= BlazeDisconnectWindowSeconds)
                        {
                            if (dHigh >= 12 && dHighLost <= dHigh && (dHighLost * 100.0 / dHigh) >= BlazeDisconnectHeuristicPercent)
                            {
                                // Time window based sum is big enough to detect
                                fAfterPlayers = players.Count;
                                Failure("BLAZE_DISCONNECT", fHighPlayerCount);
                                blazed = true;
                            }
                            resetWindow = true;
                        }
                    }
                }

                if (resetWindow)
                {
                    fSumOfSeconds = 0;
                    fHighPlayerCount = players.Count;
                }

                // Update counters and flags
                fLastPlayerCount = players.Count;
                if (players.Count > fHighPlayerCount) fHighPlayerCount = players.Count;
                fLastListPlayersTimestamp = DateTime.Now;
                fServerCrashed = false;
                fGotLogin = false;

                // Check for shutdown
                if (blazed && EnableRestartOnBlaze && players.Count == 0)
                {
                    if (!fRestartInitiated)
                    {
                        ConsoleWarn(" ");
                        ConsoleWarn("^8RESTARTING GAME SERVER WITH ADMIN SHUTDOWN" + (RestartOnBlazeDelay >= 0 ? " AFTER " + RestartOnBlazeDelay + " SECONDS" : String.Empty) + "!");
                        ConsoleWarn(" ");

                        Thread restartThread = new Thread(
                            delegate()
                            {
                                if (RestartOnBlazeDelay > 0)
                                {
                                    Thread.Sleep(RestartOnBlazeDelay * 1000);
                                }
                                ConsoleWarn(" ");
                                ConsoleWarn("^8RESTARTING GAME SERVER WITH ADMIN SHUTDOWN!");
                                ConsoleWarn(" ");
                                ServerCommand("admin.shutDown");
                            }
                        );
                        restartThread.IsBackground = true;
                        restartThread.Name = "FailLog - RestartThread";
                        restartThread.Start();
                    }
                    else
                    {
                        ConsoleWarn(" ");
                        ConsoleWarn("^8SERVER RESTART ALREADY INITIATED!");
                        ConsoleWarn(" ");
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnServerInfo(CServerInfo serverInfo)
        {
            if (!fIsEnabled || serverInfo == null) return;

            DebugWrite("^9^bGot OnServerInfo^n: Debug level = " + DebugLevel, 8);

            try
            {
                bool newMapMode = false;

                if (fServerInfo == null || fServerInfo.GameMode != serverInfo.GameMode || fServerInfo.Map != serverInfo.Map)
                {
                    newMapMode = true;
                }

                // Check if serverInfo up-time is inconsistent
                fLastUptime = fServerUptime;
                if (fServerUptime > 0 && fServerUptime > serverInfo.ServerUptime + 2)
                { // +2 for rounding error on server side!
                    DebugWrite("OnServerInfo fServerUptime = " + fServerUptime + ", serverInfo.ServerUptime = " + serverInfo.ServerUptime, 3);
                    fServerCrashed = true;
                    ServerCommand("admin.listPlayers", "all");
                }

                fServerInfo = serverInfo;
                fServerUptime = serverInfo.ServerUptime;

                if (newMapMode)
                {
                    DebugWrite("New map/mode: " + this.FriendlyMap + "/" + this.FriendlyMode, 3);
                }

                // Check for plugin updates periodically
                if (fLastVersionCheckTimestamp != DateTime.MinValue
                && DateTime.Now.Subtract(fLastVersionCheckTimestamp).TotalMinutes > CHECK_FOR_UPDATES_MINS)
                {
                    CheckForPluginUpdate();
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnMaxPlayers(int limit)
        {
            UpdateConfig("vars_maxPlayers", limit);
            if (!fIsEnabled) return;

            DebugWrite("^9Got ^bOnMaxPlayers^n", 8);
            try
            {
                if (limit > fLastMaxPlayers) fLastMaxPlayers = limit;
                fMaxPlayers = limit;
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        #region Configuration

        public override void OnVersion(string serverType, string version) { UpdateConfig("version", serverType + "/" + version); }

        public override void OnServerName(string serverName) { UpdateConfig("vars_serverName", serverName); }

        public override void OnServerDescription(string serverDescription) { UpdateConfig("vars_serverDescription", serverDescription); }

        public override void OnServerMessage(string serverMessage) { UpdateConfig("vars_serverMessage", serverMessage); } // BF3

        public override void OnPunkbuster(bool isEnabled) { UpdateConfig("punkBuster_activate", isEnabled); } // punkBuster.activate?

        public override void OnRanked(bool isEnabled) { UpdateConfig("vars_ranked", isEnabled); }

        public override void OnIdleTimeout(int limit) { UpdateConfig("vars_idleTimeout", limit); }

        public override void OnIdleBanRounds(int limit) { UpdateConfig("vars_idleBanRounds", limit); } //BF3

        public override void OnRoundRestartPlayerCount(int limit) { UpdateConfig("vars_roundRestartPlayerCount", limit); }

        public override void OnRoundStartPlayerCount(int limit) { UpdateConfig("vars_roundStartPlayerCount", limit); }

        public override void OnGameModeCounter(int limit) { UpdateConfig("vars_gameModeCounter", limit); }

        public override void OnCtfRoundTimeModifier(int limit) { UpdateConfig("vars_ctfRoundTimeModifier", limit); } // BF3

        public override void OnRoundLockdownCountdown(int limit) { UpdateConfig("vars_roundLockdownCountdown", limit); }

        public override void OnRoundWarmupTimeout(int limit) { UpdateConfig("vars_roundWarmupTimeout", limit); }

        public override void OnPremiumStatus(bool isEnabled) { UpdateConfig("vars_premiumStatus", isEnabled); }

        public override void OnGunMasterWeaponsPreset(int preset) { UpdateConfig("vars_gunMasterWeaponsPreset", preset); }

        public override void OnVehicleSpawnAllowed(bool isEnabled) { UpdateConfig("vars_vehicleSpawnAllowed", isEnabled); }

        public override void OnVehicleSpawnDelay(int limit) { UpdateConfig("vars_vehicleSpawnDelay", limit); }

        public override void OnBulletDamage(int limit) { UpdateConfig("vars_bulletDamage", limit); }

        public override void OnOnlySquadLeaderSpawn(bool isEnabled) { UpdateConfig("vars_onlySquadLeaderSpawn", isEnabled); }

        public override void OnSoldierHealth(int limit) { UpdateConfig("vars_solderHealth", limit); }

        public override void OnPlayerManDownTime(int limit) { UpdateConfig("vars_playerManDownTime", limit); }

        public override void OnPlayerRespawnTime(int limit) { UpdateConfig("vars_playerRespawnTime", limit); }

        public override void OnHud(bool isEnabled) { UpdateConfig("vars_hud", isEnabled); }

        public override void OnNameTag(bool isEnabled) { UpdateConfig("vars_nameTag", isEnabled); }

        public override void OnFriendlyFire(bool isEnabled) { UpdateConfig("vars_friendlyFire", isEnabled); }

        public override void OnUnlockMode(string mode) { UpdateConfig("vars_unlockMode", mode); } //BF3

        public override void OnTeamBalance(bool isEnabled) { UpdateConfig("vars_autoBalance", isEnabled); } // vars.autoBalance too

        public override void OnKillCam(bool isEnabled) { UpdateConfig("vars_killCam", isEnabled); }

        public override void OnMiniMap(bool isEnabled) { UpdateConfig("vars_miniMap", isEnabled); }

        public override void OnCrossHair(bool isEnabled) { UpdateConfig("vars_crossHair", isEnabled); } // not supported?

        public override void On3dSpotting(bool isEnabled) { UpdateConfig("vars_3dSpotting", isEnabled); }

        public override void OnMiniMapSpotting(bool isEnabled) { UpdateConfig("vars_miniMapSpotting", isEnabled); }

        public override void OnThirdPersonVehicleCameras(bool isEnabled) { UpdateConfig("vars_3pCam", isEnabled); }

        public override void OnTeamKillCountForKick(int limit) { UpdateConfig("vars_teamKillCountForKick", limit); }

        public override void OnTeamKillValueIncrease(int limit) { UpdateConfig("vars_teamKillValueIncrease", limit); }

        public override void OnTeamKillValueDecreasePerSecond(int limit) { UpdateConfig("vars_teamKillValueDecreasePerSecond", limit); }

        public override void OnTeamKillValueForKick(int limit) { UpdateConfig("vars_teamKillValueForKick", limit); }

        #endregion Configuration

        /* ======================== SUPPORT FUNCTIONS ============================= */

        private void UpdateConfig(String key, String val)
        {
            InnerUpdateConfig(key, val);
        }

        private void UpdateConfig(String key, bool val)
        {
            InnerUpdateConfig(key, val);
        }

        private void UpdateConfig(String key, int val)
        {
            InnerUpdateConfig(key, Convert.ToDouble(val));
        }

        private void InnerUpdateConfig(String key, Object val)
        {
            if (fConfigSettings == null || fConfigTimestamp == null) return;

            try
            {
                fConfigSettings[key] = val;
                fConfigTimestamp[key] = DateTime.Now;
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        private void Failure(String type, int lastPlayerCount)
        {
            if (fServerInfo == null)
            {
                if (DebugLevel >= 3) ConsoleWarn("Failure: fServerInfo == null!");
                return;
            }
            String utcTime = DateTime.UtcNow.ToString("yyyyMMdd_HH:mm:ss");
            String upTime = TimeSpan.FromSeconds(fLastUptime).ToString();
            String round = String.Format("{0}/{1}", (fServerInfo.CurrentRound + 1), fServerInfo.TotalRounds);
            String players = Math.Max(fMaxPlayers, fLastMaxPlayers).ToString() + "/" + lastPlayerCount + "/" + fAfterPlayers;
            String details = String.Format("\"{0},{1},{2},{3},{4},{5},{6},{7}\"",
                EscapeLogField(GameServerType),
                EscapeLogField(RankedServerProvider),
                EscapeLogField(ServerOwnerOrCommunity),
                EscapeLogField(ContactInfo),
                EscapeLogField(ServerRegion),
                EscapeLogField(fServerInfo.ServerRegion + "/" + fServerInfo.ServerCountry),
                EscapeLogField(BattlelogLink),
                EscapeLogField(AdditionalInformation));
            String line = String.Format("Type:{0}, UTC:{1}, Server:\"{2}\", Map:{3}, Mode:{4}, Round:{5}, Players:{6}, Uptime:{7}, Details:{8}",
                type,
                utcTime,
                EscapeLogField(fServerInfo.ServerName),
                this.FriendlyMap,
                this.FriendlyMode,
                round,
                players,
                upTime,
                details);

            ConsoleWrite("^8" + line);
            if (EnableLogToFile)
            {
                ServerLog(LogFile, line);
            }

            if (EnableWebLog && type.CompareTo("BLAZE_DISCONNECT") == 0)
            {
                Thread webLogThread = new Thread(
                    delegate()
                    {
                        String phpQuery = String.Format("http://dev.myrcon.com/procon/blazereport/report.php?key=HhcF93olvLgHh9UTYlqs&ver={0}&gsp={1}&owner={2}&contactinfo={3}&region={4}&game={5}&servername={6}&serverhost={7}&serverport={8}&map={9}&gamemode={10}&players={11}&uptime={12}&additionalinfo={13}",
                                EscapeRequestString(GetPluginVersion()),
                                EscapeRequestString(RankedServerProvider),
                                EscapeRequestString(ServerOwnerOrCommunity),
                                EscapeRequestString(ContactInfo),
                                EscapeRequestString(fServerInfo.ServerRegion + "/" + fServerInfo.ServerCountry + ((!String.IsNullOrEmpty(ServerRegion)) ? "/" + ServerRegion : String.Empty)),
                                EscapeRequestString(GameServerType),
                                EscapeRequestString(fServerInfo.ServerName),
                                EscapeRequestString(fHost),
                                EscapeRequestString(fPort),
                                EscapeRequestString(this.FriendlyMap),
                                EscapeRequestString(this.FriendlyMode),
                                EscapeRequestString(players),
                                fLastUptime,
                                EscapeRequestString(AdditionalInformation));

                        SendBlazeReport(phpQuery);
                    });
                webLogThread.IsBackground = true;
                webLogThread.Name = "FailLog - WebLogThread";
                webLogThread.Start();
            }

            if (EnableEmailOnBlaze && type.CompareTo("BLAZE_DISCONNECT") == 0)
            {
                Thread mailSendThread = new Thread(
                    delegate()
                    {
                        try
                        {
                            if (DebugLevel >= 4) ConsoleWrite("Preparing BlazeReport-email...");

                            SmtpClient smtpClient = new SmtpClient(SMTPHostname, SMTPPort);
                            smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                            smtpClient.UseDefaultCredentials = false;
                            smtpClient.Credentials = new NetworkCredential(SMTPUsername, SMTPPassword);
                            smtpClient.EnableSsl = SMTPUseSSL;
                            smtpClient.Timeout = 30000;

                            MailMessage mailMessage = new MailMessage();
                            mailMessage.From = new MailAddress(EmailSender, "FailLog - " + EmailSender);
                            foreach (String address in EmailRecipients)
                            {
                                mailMessage.To.Add(new MailAddress(address, address));
                            }
                            mailMessage.Subject = EmailSubject.Replace("%servername%", fServerInfo.ServerName).Replace("%serverip%", fHost).Replace("%serverport%", fPort).Replace("%time%", utcTime).Replace("%playercount%", players).Replace("%map%", this.FriendlyMap).Replace("%gamemode%", this.FriendlyMode).Replace("%round%", round).Replace("%uptime%", upTime);
                            mailMessage.SubjectEncoding = System.Text.Encoding.UTF8;
                            mailMessage.Body = String.Empty;
                            foreach (String bodyLine in EmailMessage)
                            {
                                mailMessage.Body += bodyLine.Replace("%servername%", fServerInfo.ServerName).Replace("%serverip%", fHost).Replace("%serverport%", fPort).Replace("%time%", utcTime).Replace("%playercount%", players).Replace("%map%", this.FriendlyMap).Replace("%gamemode%", this.FriendlyMode).Replace("%round%", round).Replace("%uptime%", upTime);
                            }
                            mailMessage.BodyEncoding = System.Text.Encoding.UTF8;
                            mailMessage.IsBodyHtml = true;
                            mailMessage.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;
                            mailMessage.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(mailMessage.Body, new ContentType("text/html")));

                            if (DebugLevel >= 7)
                            {
                                ConsoleWrite("BlazeReport-email:");
                                ConsoleWrite("Subject: " + mailMessage.Subject);
                                ConsoleWrite("Body: " + mailMessage.Body);
                            }

                            smtpClient.Send(mailMessage);

                            if (DebugLevel >= 3) ConsoleWrite("BlazeReport-email sent successfully!");
                        }
                        catch (Exception e)
                        {
                            if (DebugLevel >= 3) ConsoleError("Exception while sending BlazeReport-email");
                            ConsoleException(e);
                        }
                    });
                mailSendThread.IsBackground = true;
                mailSendThread.Name = "FailLog - MailSendThread";
                mailSendThread.Start();
            }
        }

        private String FormatMessage(String msg, MessageType type)
        {
            String prefix = "[^b" + GetPluginName() + "^n] ";

            if (Thread.CurrentThread.Name != null) prefix += "Thread(^b" + Thread.CurrentThread.Name + "^n): ";

            if (type.Equals(MessageType.Warning))
                prefix += "^1^bWARNING^0^n: ";
            else if (type.Equals(MessageType.Error))
                prefix += "^1^bERROR^0^n: ";
            else if (type.Equals(MessageType.Exception))
                prefix += "^1^bEXCEPTION^0^n: ";
            else if (type.Equals(MessageType.Debug))
                prefix += "^9^bDEBUG^n: ";

            return prefix + msg;
        }

        public void LogWrite(String msg)
        {
            this.ExecuteCommand("procon.protected.pluginconsole.write", msg);
        }

        public void ConsoleWrite(String msg, MessageType type)
        {
            LogWrite(FormatMessage(msg, type));
        }

        public void ConsoleWrite(String msg)
        {
            ConsoleWrite(msg, MessageType.Normal);
        }

        public void ConsoleWarn(String msg)
        {
            ConsoleWrite(msg, MessageType.Warning);
        }

        public void ConsoleError(String msg)
        {
            ConsoleWrite(msg, MessageType.Error);
        }

        public void ConsoleException(Exception e)
        {
            if (DebugLevel >= 3) ConsoleWrite(e.ToString(), MessageType.Exception);
        }

        public void DebugWrite(String msg, int level)
        {
            if (DebugLevel >= level) ConsoleWrite(msg, MessageType.Normal);
        }

        public void ConsoleDebug(String msg)
        {
            if (DebugLevel >= 4) ConsoleWrite(msg, MessageType.Debug);
        }

        public void Log(String path, String line)
        {
            try
            {
                if (!Path.IsPathRooted(path))
                    path = Path.Combine(Directory.GetParent(Application.ExecutablePath).FullName, path);

                // Add newline
                line = line + "\n";

                using (FileStream fs = File.Open(path, FileMode.Append))
                {
                    Byte[] info = new UTF8Encoding(true).GetBytes(line);
                    fs.Write(info, 0, info.Length);
                }
            }
            catch (Exception ex)
            {
                ConsoleError("unable to append data to file " + path);
                ConsoleException(ex);
            }
        }

        public void ServerLog(String file, String line)
        {
            String entry = "[" + DateTime.Now.ToString("yyyyMMdd_HH:mm:ss") + "] "; // TBD: procon instance local time, not game server time!
            entry = entry + fHost + "_" + fPort + ": " + line;
            String path = Path.Combine("Logs", file);
            Log(path, entry);
        }

        private String EscapeLogField(String input)
        {
            return input.Replace("\"", "'").Replace(",", ";");
        }

        private String EscapeRequestString(String input)
        {
            return input.Replace("=", "").Replace("?", "").Replace("&", "").Replace("#", "").Trim();
        }

        private String EncodeBase64(String input)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
        }

        private void SendBlazeReport(String query)
        {
            try
            {
                WebClient webClient = new WebClient();
                String userAgent = "Mozilla/5.0 (compatible; Procon 1; FailLog)";
                webClient.Headers.Add("user-agent", userAgent);

                StreamReader streamReader = new StreamReader(webClient.OpenRead(query));
                String response = streamReader.ReadToEnd();

                if (String.IsNullOrEmpty(response) || !response.Contains("Thank you for your Blaze Report!"))
                {
                    if (DebugLevel >= 3) ConsoleWarn("BlazeReport didn't contain valid response!");
                }
                else if (!String.IsNullOrEmpty(response) && response.Contains("Thank you for your Blaze Report!"))
                {
                    if (DebugLevel >= 3) ConsoleWrite("BlazeReport sent successfully!");
                }
            }
            catch (Exception e)
            {
                if (DebugLevel >= 3) ConsoleError("Exception during SendBlazeReport");
                ConsoleException(e);
            }
        }

        private void ServerCommand(params String[] args)
        {
            List<String> list = new List<String>();
            list.Add("procon.protected.send");
            list.AddRange(args);
            this.ExecuteCommand(list.ToArray());
        }

        private void TaskbarNotify(String title, String msg)
        {
            this.ExecuteCommand("procon.protected.notification.write", title, msg);
        }

        private void Reset()
        {
            fServerInfo = null; // release Procon reference
            fIsEnabled = false;
            fServerUptime = 0;
            fServerCrashed = false;
            fGotLogin = false;
            fLastVersionCheckTimestamp = DateTime.MinValue;
            fFriendlyMaps.Clear();
            fFriendlyModes.Clear();
            fLastPlayerCount = 0;
            fLastListPlayersTimestamp = DateTime.MinValue;
            fAfterPlayers = 0;
            fLastUptime = 0;
            fMaxPlayers = 0;
            fLastMaxPlayers = 0;
            fSumOfSeconds = 0;
            fHighPlayerCount = 0;
            fRestartInitiated = false;
        }

        private void GatherProconGoodies()
        {
            fFriendlyMaps.Clear();
            fFriendlyModes.Clear();
            List<CMap> bf3_defs = this.GetMapDefines();
            foreach (CMap m in bf3_defs)
            {
                if (!fFriendlyMaps.ContainsKey(m.FileName)) fFriendlyMaps[m.FileName] = m.PublicLevelName;
                if (!fFriendlyModes.ContainsKey(m.PlayList)) fFriendlyModes[m.PlayList] = m.GameMode;
            }
            if (DebugLevel >= 8)
            {
                foreach (KeyValuePair<String, String> pair in fFriendlyMaps)
                {
                    DebugWrite("friendlyMaps[" + pair.Key + "] = " + pair.Value, 8);
                }
                foreach (KeyValuePair<String, String> pair in fFriendlyModes)
                {
                    DebugWrite("friendlyModes[" + pair.Key + "] = " + pair.Value, 8);
                }
            }
            DebugWrite("Friendly names loaded", 6);
        }

        private void ValidateInt(ref int val, String propName, int def)
        {
            if (val < 0)
            {
                ConsoleError("^b" + propName + "^n must be greater than or equal to 0, was set to " + val + ", corrected to " + def);
                val = def;
                return;
            }
        }

        private void ValidateIntRange(ref int val, String propName, int min, int max, int def, bool zeroOK)
        {
            if (zeroOK && val == 0) return;
            if (val < min || val > max)
            {
                String zero = (zeroOK) ? " or equal to 0" : String.Empty;
                ConsoleError("^b" + propName + "^n must be greater than or equal to " + min + " and less than or equal to " + max + zero + ", was set to " + val + ", corrected to " + def);
                val = def;
            }
        }

        private void ValidateDouble(ref double val, String propName, double def)
        {
            if (val < 0)
            {
                ConsoleError("^b" + propName + "^n must be greater than or equal to 0, was set to " + val + ", corrected to " + def);
                val = def;
                return;
            }
        }

        private void ValidateDoubleRange(ref double val, String propName, double min, double max, double def, bool zeroOK)
        {
            if (zeroOK && val == 0.0) return;
            if (val < min || val > max)
            {
                String zero = (zeroOK) ? " or equal to 0" : String.Empty;
                ConsoleError("^b" + propName + "^n must be greater than or equal to " + min + " and less than or equal to " + max + zero + ", was set to " + val + ", corrected to " + def);
                val = def;
                return;
            }
        }

        public void CheckForPluginUpdate()
        {
            try
            {
                XmlDocument xml = new XmlDocument();
                try
                {
                    xml.Load("https://myrcon.com/procon/plugins/report/format/xml/plugin/FailLog");
                }
                catch (System.Security.SecurityException e)
                {
                    if (DebugLevel >= 8) ConsoleException(e);
                    ConsoleWrite(" ");
                    ConsoleWrite("^8^bNOTICE! Unable to check for plugin update!");
                    ConsoleWrite("Tools => Options... => Plugins tab: ^bPlugin security^n is set to ^bRun plugins in a sandbox^n.");
                    //ConsoleWrite("Please add ^bmyrcon.com^n to your trusted ^bOutgoing connections^n");
                    ConsoleWrite("Consider changing to ^bRun plugins with no restrictions.^n");
                    ConsoleWrite("Alternatively, check the ^bPlugins^n forum for an update to this plugin.");
                    ConsoleWrite(" ");
                    fLastVersionCheckTimestamp = DateTime.MaxValue;
                    return;
                }
                if (DebugLevel >= 8) ConsoleDebug("CheckForPluginUpdate: Got " + xml.BaseURI);

                /*
                Example:
                <report>
                    <id>5132671</id>
                    <plugin>
                        <id>217948094</id>
                        <uid>FailLog</uid>
                        <name>FailLog</name>
                    </plugin>
                    <version>
                        <id>965536</id>
                        <major>1</major>
                        <minor>0</minor>
                        <maintenance>0</maintenance>
                        <build>6</build>
                    </version>
                    <sum_in_use>22</sum_in_use>
                    <avg_in_use>22.0000</avg_in_use>
                    <max_in_use>22</max_in_use>
                    <min_in_use>22</min_in_use>
                    <stamp>2013-05-10 10:00:04</stamp>
                </report>
                */

                XmlNodeList rows = xml.SelectNodes("//report");
                if (DebugLevel >= 8) ConsoleDebug("CheckForPluginUpdate: # rows = " + rows.Count);
                if (rows.Count == 0) return;
                Dictionary<String, int> versions = new Dictionary<String, int>();
                foreach (XmlNode tr in rows)
                {
                    XmlNode ver = tr.SelectSingleNode("version");
                    XmlNode count = tr.SelectSingleNode("sum_in_use");
                    if (ver != null && count != null)
                    {
                        int test = 0;
                        XmlNode major = ver.SelectSingleNode("major");
                        if (!Int32.TryParse(major.InnerText, out test)) continue;
                        XmlNode minor = ver.SelectSingleNode("minor");
                        if (!Int32.TryParse(minor.InnerText, out test)) continue;
                        XmlNode maint = ver.SelectSingleNode("maintenance");
                        if (!Int32.TryParse(maint.InnerText, out test)) continue;
                        XmlNode build = ver.SelectSingleNode("build");
                        if (!Int32.TryParse(build.InnerText, out test)) continue;
                        String vt = major.InnerText + "." + minor.InnerText + "." + maint.InnerText + "." + build.InnerText;
                        if (DebugLevel >= 8) ConsoleDebug("CheckForPluginUpdate: Version: " + vt + ", Count: " + count.InnerText);
                        int n = 0;
                        if (!Int32.TryParse(count.InnerText, out n)) continue;
                        versions[vt] = n;
                    }
                }

                // Select current version and any "later" versions
                int usage = 0;
                String myVersion = GetPluginVersion();
                if (!versions.TryGetValue(myVersion, out usage))
                {
                    DebugWrite("CheckForPluginUpdate: my version " + myVersion + " not found!", 8);
                    return;
                }

                // Update check time
                fLastVersionCheckTimestamp = DateTime.Now;

                // numeric sort
                List<String> byNumeric = new List<String>();
                byNumeric.AddRange(versions.Keys);
                // Sort numerically descending
                byNumeric.Sort(delegate(String lhs, String rhs)
                {
                    if (lhs == rhs) return 0;
                    if (String.IsNullOrEmpty(lhs)) return 1;
                    if (String.IsNullOrEmpty(rhs)) return -1;
                    uint l = VersionToNumeric(lhs);
                    uint r = VersionToNumeric(rhs);
                    if (l < r) return 1;
                    if (l > r) return -1;
                    return 0;
                });
                DebugWrite("CheckForPluginUpdate: sorted version list:", 7);
                foreach (String u in byNumeric)
                {
                    DebugWrite(u + " (" + String.Format("{0:X8}", VersionToNumeric(u)) + "), count = " + versions[u], 7);
                }

                int position = byNumeric.IndexOf(myVersion);

                DebugWrite("CheckForPluginUpdate: found " + position + " newer versions", 5);

                if (position != 0)
                {
                    // Newer versions found
                    // Find the newest version with the largest number of usages
                    int hasMost = -1;
                    int most = 0;
                    for (int i = position - 1; i >= 0; --i)
                    {
                        int newerVersionCount = versions[byNumeric[i]];
                        if (hasMost == -1 || most < newerVersionCount)
                        {
                            // Skip newer versions that don't have enough usage yet
                            if (most > 0 && newerVersionCount < MIN_UPDATE_USAGE_COUNT) continue;
                            hasMost = i;
                            most = versions[byNumeric[i]];
                        }
                    }

                    if (hasMost != -1 && hasMost < byNumeric.Count && most >= MIN_UPDATE_USAGE_COUNT)
                    {
                        String newVersion = byNumeric[hasMost];
                        ConsoleWrite("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                        ConsoleWrite(" ");
                        ConsoleWrite("^8^bA NEW VERSION OF THIS PLUGIN IS AVAILABLE!");
                        ConsoleWrite(" ");
                        ConsoleWrite("^8^bPLEASE UPDATE TO VERSION: ^0" + newVersion);
                        ConsoleWrite(" ");
                        ConsoleWrite("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");

                        TaskbarNotify(GetPluginName() + ": new version available!", "Please download and install " + newVersion);
                    }
                }
            }
            catch (Exception e)
            {
                if (DebugLevel >= 8) ConsoleException(e);
            }
        }

        private uint VersionToNumeric(String ver)
        {
            uint numeric = 0;
            byte part = 0;
            Match m = Regex.Match(ver, @"^\s*([0-9]+)\.([0-9]+)\.([0-9]+)\.([0-9]+)(\w*)\s*$");
            if (m.Success)
            {
                for (int i = 1; i < 5; ++i)
                {
                    if (!Byte.TryParse(m.Groups[i].Value, out part))
                    {
                        part = 0;
                    }
                    numeric = (numeric << 8) | part;
                }
            }
            return numeric;
        }
    } // end FailLog

    /* ======================== UTILITIES ============================= */

    #region UTILITIES

    static class FailLogUtils
    {
        public static String ArrayToString(double[] a)
        {
            String ret = String.Empty;
            bool first = true;
            if (a == null || a.Length == 0) return ret;
            for (int i = 0; i < a.Length; ++i)
            {
                if (first)
                {
                    ret = a[i].ToString("F0");
                    first = false;
                }
                else
                {
                    ret = ret + ", " + a[i].ToString("F0");
                }
            }
            return ret;
        }

        public static double[] ParseNumArray(String s)
        {
            double[] nums = new double[3] { -1, -1, -1 }; // -1 indicates a syntax error
            if (String.IsNullOrEmpty(s)) return nums;
            if (!s.Contains(",")) return nums;
            String[] strs = s.Split(new Char[] { ',' });
            if (strs.Length != 3) return nums;
            for (int i = 0; i < nums.Length; ++i)
            {
                bool parsedOk = Double.TryParse(strs[i], out nums[i]);
                if (!parsedOk)
                {
                    nums[i] = -1;
                    return nums;
                }
            }
            return nums;
        }

        #region HTML_DOC

        public const String HTML_DOC = @"
<h1>Fail Log</h1>

<p>For BF3, this plugin logs game server crashes, layer disconnects and Blaze dumps.</p>

<h2>Description</h2>
<p>Each failure event generates a single log line. The log line is written to plugin.log. Optionally, it may also be written to a file in procon/Logs and/or to a web logging database on myrcon.com, controled by plugin settings (see below). Note that this plugin must be run without restrictions (<b>not</b> in sandbox mode) in order to use either the optional separate log file or web log features. The plugin may be run in sandbox mode if both of the optional logging features are disabled.</p>

<p>The contents of a log line are divided into fields. The following table describes each of the fields and shows an example:
<table>
<tr><th>Field</th><th>Description</th><th>Example</th></tr>
<tr><td>Type</td><td>A label that describes the type of failure. The types tracked are game server restarts, Procon disconnects, blaze disconnects, and network congestion to/from Procon.</td><td>BLAZE_DISCONNECT</td></tr>
<tr><td>UTC</t><td>UTC time stamp of the plugin's detection of the failure; the actual event might have happened earlier</td><td>20130507_01:52:58</td></tr>
<tr><td>Server</td><td>Server name per vars.serverName</td><td>&quot;CTF Noobs welcome!&quot;</td></tr>
<tr><td>Map</td><td>Friendly map name</td><td>Noshahr Canals</td></tr>
<tr><td>Mode</td><td>Friendly mode name</td><td>TDM</td></tr>
<tr><td>Round</td><td>Current round/total rounds</td><td>1/2</td></tr>
<tr><td>Players</td><td>vars.maxPlayers/previous known player count/current player count</td><td>64/63/0</td></tr>
<tr><td>Uptime</td><td>Uptime of game server as days.hh:mm:ss</td><td>6.09:01:35</td></tr>
<tr><td>Details</td><td>All of the information you entered in Section 2 of the settings, plus the Region and Country from serverInfo</td><td>&quot;Ranked Server Provider,Server Owner Or Community,Myrcon Forum User Name,Server Region,NAm/US,Additional Information&quot;</td></tr>
</table></p>

<h3>Blaze Disconnect Failures</h3>
<p>This plugin uses a heuristic (a guess) to decide if a loss of players indicates a Blaze disconnect failure. The loss of players is calculated on every admin.listPlayers event. These events happen at least once every 30 seconds, but may happen more frequently if you run other plugins. This means that detection of Blaze events is very dependent on your configuration. You may need to adjust the settings of this plugin to detect Blaze disconnects accurately.</p>

<h2>Settings</h2>
<p>Plugin settings are described in this section.</p>

<h3>Section 1</h3>
<p>General plugin settings.</p>

<p><b>Debug Level</b>: Number from 0 to 9, default 2. Sets the amount of debug messages sent to plugin.log. Caught exceptions are logged at 3 or higher. Raw event handling is logged at 8 or higher.</p>

<p><b>Enable Log To File</b>: True or False, default False. If False, logging is only to plugin.log. If True, logging is also written to the file specified in <b>Log File</b>.</p>

<p><b>Log File</b>: Name of the file to use for logging. Defaults to &quot;fail.log&quot; and is stored in procon/Logs.</p>

<p><b>Enable Web Log</b>: True or False, default True. If False, no logging is sent to the web database. If True, logging is also sent to the web database.</p>

<p><b>Blaze Disconnect Heuristic Percent</b>: Number from 33 to 100, default 75. Not every sudden drop in players is a Blaze disconnect. Also, sometimes a Blaze disconnect does not disconnect all players or they reconnect before the next listPlayers event happens. This heuristic (guess) percentage accounts for those facts. The percentage is based on the ratio of the count of lost players to the last known count of players. For example, if you set this value to 75, it means any loss of 75% or more players should be treated as a Blaze disconnect. If there were 32 players before and now there are 10 players, (32-10)/32 = 69%, which is not greater than or equal to 75%, so no Blaze failure. If there were 32 players before and now there are no players, (32-0)/32 = 100%, a Blaze failure. If you want to only detect drops to zero players, set this value to 100. If the last known player count was less than 12, no detection is logged, even though a Blaze disconnect may have happened. See also <b>Blaze Disconnect Window Seconds</b>.</p>

<p><b>Blaze Disconnect Window Seconds</b>: Number from 30 to 90, default 30. Normally, listPlayers events happen every 30 seconds and that is normally enough time to detect a Blaze disconnect. However, if you have lots of other plugins running, listPlayer events may happen more frequently than every 30 seconds, which may not be enough time to detect a large enough loss of players. Even if the interval between events is 30 seconds, sometimes a Blaze disconnect takes longer than 30 seconds to complete. This setting allows you to adjust the plugin to handle those situations. If you notice loss of players that you suspect are Blaze disconnects but no failure is registered, increase this value. Try 60 at first and if that isn't enough, add 15 seconds and try again, until you get to the max of 90 seconds.</p>

<p><b>Enable Restart On Blaze</b>: True or False, default False. If True, the game server will be restarted with an admin.shutDown command when a Blaze disconnect is detected <b>and</b> the remaining number of players is zero. Use with caution!</p>

<p><b>Restart On Blaze Delay</b>: Number, default 0. Time in seconds to wait before invoking the admin.shutDown command after a Blaze disconnect. Use with caution, since most servers get messed up or don't save progress properly after a Blaze disconnect, so instant restarts would be advised. Setting it to 0 instantly executes the command.</p>

<p><b>Enable Email On Blaze</b>: True or False, default False. If True, the plugin will send a notification-email if you server blazes (see settings below). Make sure to disable the sandbox or allow SMTP-connections and your mailserver + mailserver-port in the trusted hosts.</p>

<h3>Section 2</h3>
<p>These settings fully describe your server for logging purposes. Information important for tracking global outages and that can't be extracted from known data is included. All of this information is optional.</p>

<p><b>Game Server Type</b>: Type of game server, defaults to BF3.</p>

<p><b>Ranked Server Provider</b>: Name of the RSP/GSP that hosts your game server.</p>

<p><b>Server Owner Or Community</b>: Your name or the name of your clan, whoever runs the server. Technically you are a 'renter' rather than an owner, but you are responsible for the game server.</p>

<p><b>Contact Info</b>: Email address, clan website or your myrcon.com forum user name, to follow-up with you if additional information is needed.</p>

<p><b>Server Region</b>: The Country and Region known by serverInfo in automatically included, but that information is very high level, su as NAm/US. Use this setting to specify the city, state or to narrow down the region further.</p>

<p><b>Battlelog Link</b>: Link to your server at Battlelog (e.g. http://battlelog.battlefield.com/bf3/servers/show/5474dcd7-6331-402e-a874-c0861c646162/PRoCon-Plugin-Testserver/). Will be added to the BlazeReport-overview page if available.</p>

<p><b>Additional Information</b>: These are additional details that were not anticpated at the time of writing of this plugin but that may prove useful in the future.</p>

<h3>Section 3</h3>
<p>These settings configure the BlazeReport-mail being sent. The following values can be entered as wildcards at the email-subject and email-body and will be replaced: %servername%, %serverip%, %serverport%, %utc%, %players%, %map%, %gamemode%, %round%, %uptime%.</p>

<p><b>Email Recipients</b>: List of email-addresses to send the notifications to, one each line.</p>

<p><b>Email Sender</b>: Email-Address being displayed in the 'From:' field.</p>

<p><b>Email Subject</b>: Subject of the notification-email. You can use the values listed above to add information about the BlazeReport.</p>

<p><b>Email Message</b>: Body of the BlazeReport-email, can be fully styled with HTML. You can use the values listed above to add information about the BlazeReport.</p>

<p><b>SMTP Hostname</b>: Hostname/IP-Address of the SMTP-server used to send email.</p>

<p><b>SMTP Port</b>: Number between 0 and 65535, default 25. Port of the SMTP-Server used to send email.</p>

<p><b>SMTP Use SSL</b>: True of False, default false. Toggles the usage of SSL for the connection to your SMTP-server.</p>

<p><b>SMTP Username</b>: Username used to identify with your SMTP-server.</p>

<p><b>SMTP Password</b>: Password used to identify with your SMTP-server.</p>

<h2>Development</h2>
<p>This plugin is an open source project hosted on GitHub.com. The repo is located at
<a href='https://github.com/PapaCharlie9/fail-log'>https://github.com/PapaCharlie9/fail-log</a> and
the master branch is used for public distributions. See the <a href='https://github.com/PapaCharlie9/fail-log/tags'>Tags</a> tab for the latest ZIP distributions. If you would like to offer bug fixes or new features, feel
free to fork the repo and submit pull requests.</p>
";

        #endregion HTML_DOC
    } // end FailLogUtils

    #endregion UTILITIES
} // end namespace PRoConEvents