// <airvpn_source_header>
// This file is part of AirVPN Client software.
// Copyright (C)2014-2014 AirVPN (support@airvpn.org) / https://airvpn.org )
//
// AirVPN Client is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// AirVPN Client is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with AirVPN Client. If not, see <http://www.gnu.org/licenses/>.
// </airvpn_source_header>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Xml;
using System.Text;

namespace AirVPN.Core
{
    public class Platform
    {
		public static Platform Instance;

		// ----------------------------------------
        // Static - Also used before the derivated class is created
        // ----------------------------------------

		public static string ShellPlatformIndipendent(string FileName, string Arguments, string WorkingDirectory, bool WaitEnd, bool ShowWindow)
		{
			if (WaitEnd)
			{
				lock (Instance)
				{
					return ShellPlatformIndipendentEx(FileName, Arguments, WorkingDirectory, WaitEnd, ShowWindow);
				}
			}
			else
				return ShellPlatformIndipendentEx(FileName, Arguments, WorkingDirectory, WaitEnd, ShowWindow);
		}

		public static string ShellPlatformIndipendentEx(string FileName, string Arguments, string WorkingDirectory, bool WaitEnd, bool ShowWindow)
        {			
			bool debug = false;
			if ((Engine.Instance != null) && (Engine.Instance.DevelopmentEnvironment))
				debug = true;
            try
            {
				int startTime = Environment.TickCount;

                Process p = new Process();

                p.StartInfo.Arguments = Arguments;

                if (WorkingDirectory != "")
                    p.StartInfo.WorkingDirectory = WorkingDirectory;

                p.StartInfo.FileName = FileName;

                if (ShowWindow == false)
                {
                    //#do not show DOS window
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                }

                if (WaitEnd)
                {
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
					p.StartInfo.RedirectStandardError = true;
                }

                p.Start();

                if (WaitEnd)
                {
                    string Output = p.StandardOutput.ReadToEnd() + "\n" + p.StandardError.ReadToEnd();
                    p.WaitForExit();

					if (debug)
					{
						int endTime = Environment.TickCount;
						int deltaTime = endTime - startTime;
						Engine.Instance.Log(Engine.LogType.Verbose, "Shell of '" + FileName + "','" + Arguments + "' done sync in " + deltaTime.ToString() + " ms");
					}

                    return Output.Trim();
                }
                else
                {
                    return "";
                }
            }
            catch (Exception E)
            {
                return E.Message;
            }


        }
        
		/*
        public static void Init()
        {   
            if (IsUnix())
            {
				Platform.Instance = new Platforms.Linux();
            }
            else if (IsWindows())
            {
				Platform.Instance = new Platforms.Windows();				             
            }

			Platform.Instance.OnInit();
        }
		*/

        public static void DeInit()
        {
        }

		public static bool IsUnix()
        {
            return (Environment.OSVersion.Platform.ToString() == "Unix");
        }

		public static bool IsWindows()
        {
            return (Environment.OSVersion.VersionString.IndexOf("Windows") != -1);
        }

		private static bool IsLinux()
        {
			return Instance.GetCode() == "Linux";			
        }

		public string GetSystemCode()
        {
			string t = GetCode() + "_" + GetArchitecture();
            t = t.Replace(" ", "_");
            t = t.ToLower();
            return t;
        }
				
		public static string NormalizeArchitecture(string a)
		{
			if (a == "x86") return "x86";
			if (a == "i686") return "x86";
			if (a == "x64") return "x64";
			if (a == "AMD64") return "x64";
			if (a == "x86_64") return "x64";
			return "Unknown";
		}

		// ----------------------------------------
		// Constructor
		// ----------------------------------------

		public Platform()
		{
			Instance = this;
		}

        // ----------------------------------------
        // Virtual
        // ----------------------------------------

		public virtual string GetCode()
		{
			return "Unknown";
		}

		public virtual string GetArchitecture()
		{
			return "Unknown";
		}

        public virtual void NotImplemented()
        {
            throw new Exception("Not Implemented.");
        }

		public virtual string GetDefaultDataPath()
		{
			return "";
		}

        public virtual bool IsAdmin()
        {
            return false;            
        }

		public virtual bool IsUnixSystem()
		{
			return false;
		}

		public virtual bool IsLinuxSystem()
		{
			return IsUnixSystem();
		}

		public virtual bool IsWindowsSystem()
		{
			return (IsUnixSystem() == false);
		}

		

		public virtual string VersionDescription()
        {
            return Environment.OSVersion.VersionString;
        }

        public virtual bool IsTraySupported()
        {
            return false;
        }

		public virtual bool GetAutoStart()
		{
			return false;
		}

		public virtual bool SetAutoStart(bool value)
		{
			return false;
		}

		public virtual string NormalizeString(string val)
        {
            return val;
        }

        public virtual string DirSep
        {
            get
            {
                return "/";
            }
        }

		public virtual string NormalizePath(string p)
        {
            p = p.Replace("/", DirSep);
            p = p.Replace("\\", DirSep);

            char[] charsToTrimEnd = { '/', '\\' };
            p = p.TrimEnd(charsToTrimEnd);
            
            return p;
        }
                
        public virtual bool IsPath(string v)
        {
            // Filename or path (absolute or relative) ?
            return (v.IndexOf(DirSep) != -1);
        }

        public virtual string GetProgramFolder()
        {
            //Assembly.GetExecutingAssembly().Location
            //return new FileInfo(ExecutablePath).DirectoryName;
			return Path.GetDirectoryName(GetExecutablePath());
        }

		public virtual string GetExecutablePath()
		{
			return System.Reflection.Assembly.GetEntryAssembly().Location;
		}

        public virtual string GetUserFolder()
        {
            NotImplemented();
            return "";
        }

        public virtual void OpenUrl(string url)
        {
            System.Diagnostics.Process.Start(url);             
        }

		public virtual string ShellCmd(string Command)
        {
            NotImplemented();
            return "";
        }

		public virtual string Shell(string FileName, string Arguments)
		{
			return Shell(FileName, Arguments, "", true, false);
		}

		public virtual string Shell(string FileName, string Arguments, bool WaitEnd)
        {
            return Shell(FileName, Arguments, "", WaitEnd, false);
        }

		public virtual string Shell(string FileName, string Arguments, string WorkingDirectory, bool WaitEnd, bool ShowWindow)
        {
            return ShellPlatformIndipendent(FileName, Arguments, WorkingDirectory, WaitEnd, ShowWindow);
        }
        
        public virtual void FlushDNS()
        {
            NotImplemented();
        }

		public virtual void RouteAdd(RouteEntry r)
		{
			NotImplemented();
		}

		public virtual void RouteRemove(RouteEntry r)
		{
			NotImplemented();
		}

		public virtual bool WaitTunReady()
		{
			return true;
		}

		public virtual List<RouteEntry> RouteList()
		{
			NotImplemented();
			return null;
		}

		public virtual Dictionary<int, string> GetProcessesList()
		{
			Dictionary<int, string> result = new Dictionary<int, string>();

			System.Diagnostics.Process[] processlist = Process.GetProcesses();

			foreach (System.Diagnostics.Process p in processlist)
			{
				try
				{
					result[p.Id] = p.ProcessName.ToLowerInvariant();					
				}
				catch (System.InvalidOperationException)
				{
					// occur on some OSX process, ignore it.
				}
			}

			return result;
		}

		public virtual string GetTunStatsMode()
		{
			return "NetworkInterface";			
		}

        public virtual void LogSystemInfo()
        {
			Engine.Instance.Log(Engine.LogType.Verbose, "Operating System: " + Platform.Instance.VersionDescription());
        }

		public virtual string GenerateSystemReport()
		{
			string t = "";
			t += "Operating System: " + Platform.Instance.VersionDescription() + "\n";

			NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
			foreach (NetworkInterface adapter in interfaces)
			{
				t += "Network Interface: " + adapter.Name + " (" + adapter.Description + ", ID:" + adapter.Id.ToString() + ") - " + adapter.NetworkInterfaceType.ToString() + " - " + adapter.OperationalStatus.ToString();
				//t += " - Down:" + adapter.GetIPv4Statistics().BytesReceived.ToString();
				//t += " - Up:" + adapter.GetIPv4Statistics().BytesSent.ToString();
				t += "\n";
			}

			t += "\nRouting:\n";
			List<RouteEntry> routeEntries = RouteList();
			foreach (RouteEntry routeEntry in routeEntries)
			{
				t += routeEntry.ToString() + "\n";
			}

			return t;
		}

		public virtual void OnAppStart()
		{
		}

		public virtual void OnNetworkLockManagerInit()
		{
		}

		public virtual void OnSessionStart()
		{
		}

		public virtual void OnSessionStop()
		{
		}

		public virtual void OnDaemonOutput(string source, string message)
		{
		}

		// This is called every time, the OnRecoveryLoad only if Recovery.xml exists
		public virtual void OnRecovery()
		{
		}

		public virtual void OnRecoveryLoad(XmlElement root)
		{
		}

		public virtual void OnRecoverySave(XmlElement root)
		{
		}

		public virtual void OnBuildOvpn(ref string ovpn)
		{
		}

		public virtual void OnDnsSwitchDo(string dns)
		{
		}

		public virtual void OnDnsSwitchRestore()
		{
		}

		public virtual string GetDriverAvailable()
		{
			return Messages.NotImplemented;
		}

		public virtual bool CanInstallDriver()
		{
			return false;
		}

		public virtual bool CanUnInstallDriver()
		{
			return false;
		}

		public virtual void InstallDriver()
		{
			NotImplemented();
		}

		public virtual void UnInstallDriver()
		{
			NotImplemented();
		}

		public virtual bool IpV6Enabled()
		{
			return System.Net.Sockets.Socket.OSSupportsIPv6;
		}

		public virtual string GetGitDeployPath()
		{
			return GetProgramFolder () + "/../../../../deploy/" + Platform.Instance.GetSystemCode () + "/";
		}
    }
}
