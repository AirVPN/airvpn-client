﻿// <eddie_source_header>
// This file is part of Eddie/AirVPN software.
// Copyright (C)2014-2016 AirVPN (support@airvpn.org) / https://airvpn.org
//
// Eddie is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Eddie is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Eddie. If not, see <http://www.gnu.org/licenses/>.
// </eddie_source_header>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Xml;
using Eddie.Core;

// If errors occur here, probably Xamarin update cause trouble. Remove Xamarin.Mac reference and re-add by browsing to path
// /Library/Frameworks/Xamarin.Mac.framework/Versions/Current/lib/mono/4.5/Xamarin.Mac.dll
using AppKit;
using Foundation;
using System.Net;

namespace Eddie.Platform.MacOS
{
	public class Platform : Core.Platform
	{
		private string m_name = "";
		private string m_version = "";
		private string m_architecture = "";

		private List<DnsSwitchEntry> m_listDnsSwitch = new List<DnsSwitchEntry>();
		private List<IpV6ModeEntry> m_listIpV6Mode = new List<IpV6ModeEntry>();

        private string m_launchdPath = "/Library/LaunchDaemons/org.airvpn.eddie.ui.elevated.plist";

        /*
		private UnixSignal[] m_signals = new UnixSignal[] {
			new UnixSignal (Mono.Unix.Native.Signum.SIGTERM),
			new UnixSignal (Mono.Unix.Native.Signum.SIGINT),
			new UnixSignal (Mono.Unix.Native.Signum.SIGUSR1),
			new UnixSignal (Mono.Unix.Native.Signum.SIGUSR2),
		};
		*/

        // Override
        public Platform()
		{
		}

		public override string GetCode()
		{
			return "MacOS";
		}

		public override string GetName()
		{
			return m_name;
		}

		public override string GetVersion()
		{
			return m_version;
		}

		public override bool OnInit()
		{
			base.OnInit();

			if (Engine.Instance.StartCommandLine.Exists("cli"))
				NSApplication.Init(); // Requested in CLI edition to call NSPipe, NSTask etc.

			//m_version = SystemShell.Shell("/usr/bin/uname", new string[] { "-a" }).Trim();
			//m_architecture = NormalizeArchitecture(SystemShell.Shell("/usr/bin/uname", new string[] { "-m" }).Trim());

			m_name = NSProcessInfo.ProcessInfo.OperatingSystemVersionString.Trim();
			m_name = m_name.Replace("Version", "macOS").Trim();
			if (m_name.IndexOf('(') != -1)
				m_name = m_name.Substring(0, m_name.IndexOf('(')).Trim();
			m_version = NSProcessInfo.ProcessInfo.OperatingSystemVersionString.Replace("Version ","").Trim();
			m_architecture = base.GetArchitecture();

            try
            {
                bool result = (NativeMethods.eddie_init() == 0);
                if (result == false)
                    throw new Exception("fail");

                NativeMethods.eddie_signal((int)NativeMethods.Signum.SIGHUP, SignalCallback);
                NativeMethods.eddie_signal((int)NativeMethods.Signum.SIGINT, SignalCallback);
                NativeMethods.eddie_signal((int)NativeMethods.Signum.SIGTERM, SignalCallback);
                NativeMethods.eddie_signal((int)NativeMethods.Signum.SIGUSR1, SignalCallback);
                NativeMethods.eddie_signal((int)NativeMethods.Signum.SIGUSR2, SignalCallback);
            }
            catch
            {
                Console.WriteLine("Unable to initialize native library. Maybe a CPU architecture issue.");
                return false;
            }

            return true;
		}

		private static void SignalCallback(int signum)
		{
			NativeMethods.Signum sig = (NativeMethods.Signum)signum;
            if (sig == NativeMethods.Signum.SIGHUP)
                Engine.Instance.OnSignal("SIGHUP");
            else if (sig == NativeMethods.Signum.SIGINT)
				Engine.Instance.OnSignal("SIGINT");
			else if (sig == NativeMethods.Signum.SIGTERM)
				Engine.Instance.OnSignal("SIGTERM");
			else if (sig == NativeMethods.Signum.SIGUSR1)
				Engine.Instance.OnSignal("SIGUSR1");
			else if (sig == NativeMethods.Signum.SIGUSR2)
				Engine.Instance.OnSignal("SIGUSR2");
		}

		public override string GetOsArchitecture()
		{
			return m_architecture;
		}

		public override string GetDefaultDataPath()
		{
			// Only in OSX, always save in 'home' path also with portable edition.
			return "home";
		}

		public override ElevatedProcess StartElevated()
		{
			Elevated e = new Elevated();			
			e.Start();
			return e;
		}

		public override bool IsAdmin()
		{
			// With root privileges by RootLauncher.cs, Environment.UserName still return the normal username, 'whoami' return 'root'.
			string u = SystemShell.Shell(LocateExecutable("whoami"), new string[] { }).ToLowerInvariant().Trim();
			// return true; // Uncomment for debugging
			return (u == "root");
		}

		public override bool IsUnixSystem()
		{
			return true;
		}

		protected override string GetElevatedHelperPathImpl()
		{
			return FileGetPhysicalPath(GetApplicationPath() + "/eddie-cli-elevated");			
		}

        public override bool CheckElevatedSocketAllowed(IPEndPoint localEndpoint, IPEndPoint remoteEndpoint)
        {
            return true;
        }

        public override bool CheckElevatedProcessAllowed(string remotePath)
        {
            // TOFIX
            /*
            string localPath = System.Reflection.Assembly.GetEntryAssembly().Location;
            Engine.Instance.Logs.LogVerbose(localPath);
            Engine.Instance.Logs.LogVerbose(remotePath);

            string codesignPath = "/usr/bin/codesign"; // Note: absolute path to avoid ENV
            */            

            return true;
        }

        public override bool GetAutoStart()
		{
			return false;
		}

		public override bool SetAutoStart(bool value)
		{
			return false;
		}

		public override bool AllowService()
		{
			return true;
		}

		public override string AllowServiceUserDescription()
		{
			return "If checked, install a Launchd daemon";
		}

		protected override bool GetServiceImpl()
		{
            return FileExists(m_launchdPath);			
		}

		protected override bool SetServiceImpl(bool value)
		{
			if (GetServiceImpl() == value)
				return true;

            if (value)
            {
                RunProcessAsRoot(GetElevatedHelperPath(), new string[] { "service=install", "service_port=" + Engine.Instance.GetElevatedServicePort() }, Engine.Instance.ConsoleMode);
                return (GetService() == true);
            }
            else
            {
                RunProcessAsRoot(GetElevatedHelperPath(), new string[] { "service=uninstall" }, Engine.Instance.ConsoleMode);
                return (GetService() == false);
            }
            /*
			System.Diagnostics.ProcessStartInfo processStart = new System.Diagnostics.ProcessStartInfo();
			processStart.Verb = "runas";
			processStart.CreateNoWindow = true;
			processStart.UseShellExecute = true;

            if (value)
			{
                processStart.FileName = "osascript";
                processStart.Arguments = " -e 'do shell script \"" + GetElevatedHelperPath() + " service-install" + "\" with prompt \"" + UtilsString.StringSafe(LanguageManager.GetText("HelperPrivilegesPromptInstall")) + "\" with administrator privileges'";
                System.Diagnostics.Process.Start(processStart);
                //RunProcessAsRoot(GetElevatedHelperPath(), new string[] { "service-install" }, Engine.Instance.ConsoleMode);
				return (GetService() == true);
			}
			else
			{
                processStart.FileName = "osascript";
                processStart.Arguments = " -e 'do shell script \"" + GetElevatedHelperPath() + " service-uninstall" + "\" with prompt \"" + UtilsString.StringSafe(LanguageManager.GetText("HelperPrivilegesPromptUninstall")) + "\" with administrator privileges'";
                System.Diagnostics.Process.Start(processStart);
                //RunProcessAsRoot(GetElevatedHelperPath(), new string[] { "service-uninstall" }, Engine.Instance.ConsoleMode);
                return (GetService() == false);
			}
            */
        }

		public override string DirSep
		{
			get
			{
				return "/";
			}
		}

		public override string EnvPathSep
		{
			get
			{
				return ":";
			}
		}

		public override bool FileImmutableGet(string path)
		{
			if ((path == "") || (FileExists(path) == false))
				return false;

			int result = NativeMethods.eddie_file_get_immutable(path);
			return (result == 1);
		}

		public override void FileImmutableSet(string path, bool value)
		{
			if ((path == "") || (FileExists(path) == false))
                return;

            if (FileImmutableGet(path) == value)
                return;

            Engine.Instance.Elevated.DoCommandSync("file-immutable-set", "path", path, "flag", (value ? "1" : "0"));            
        }

		public override bool FileEnsurePermission(string path, string mode)
		{
			if ((path == "") || (Platform.Instance.FileExists(path) == false))
				return false;

			int result = NativeMethods.eddie_file_get_mode(path);
			if (result == -1)
			{
				Engine.Instance.Logs.Log(LogType.Warning, "Failed to detect permissions on '" + path + "'.");
				return false;
			}
			int newResult = 0;
			if (mode == "600")
				newResult = (int)NativeMethods.FileMode.Mode0600;
			else if (mode == "644")
				newResult = (int)NativeMethods.FileMode.Mode0644;

			if (newResult == 0)
			{
				Engine.Instance.Logs.Log(LogType.Warning, "Unexpected permission '" + mode + "'");
				return false;
			}

			if (newResult != result)
			{
				result = NativeMethods.eddie_file_set_mode(path, newResult);
				if (result == -1)
				{
					Engine.Instance.Logs.Log(LogType.Warning, "Failed to set permissions on '" + path + "'.");
					return false;
				}
			}

			return true;
		}
        
		public override bool FileEnsureExecutablePermission(string path)
		{
			if ((path == "") || (FileExists(path) == false))
				return false;

			int result = NativeMethods.eddie_file_get_mode(path);
			if (result == -1)
			{
				Engine.Instance.Logs.Log(LogType.Warning, "Failed to detect if '" + path + "' is executable");
				return false;
			}

			int newResult = result | 73; // +x :<> (S_IXUSR | S_IXGRP | S_IXOTH) 

			if (newResult != result)
			{
				result = NativeMethods.eddie_file_set_mode(path, newResult);
				if (result == -1)
				{
					Engine.Instance.Logs.Log(LogType.Warning, "Failed to mark '" + path + "' as executable");
					return false;
				}
			}

			return true;
		}

        public override bool FileRunAsRoot(string path)
        {
            return NativeMethods.eddie_file_get_runasroot(path);
        }

        public override string GetExecutableReport(string path)
		{
			string otoolPath = LocateExecutable("otool");
			if (otoolPath != "")
				return SystemShell.Shell2(otoolPath, "-L", SystemShell.EscapePath(path));
			else
				return "'otool' " + LanguageManager.GetText("NotFound");
		}

		public override string GetExecutablePathEx()
		{
			string currentPath = System.Reflection.Assembly.GetEntryAssembly().Location;
			if (new FileInfo(currentPath).Directory.Name == "MonoBundle")
			{
				// OSX Bundle detected, use the launcher executable
				currentPath = currentPath.Replace("/MonoBundle/", "/MacOS/").Replace(".exe", "");
			}
			else if (Process.GetCurrentProcess().ProcessName.StartsWith("mono", StringComparison.InvariantCultureIgnoreCase))
			{
				// mono <app>, Entry Assembly path it's ok
			}
			else
			{
				currentPath = Process.GetCurrentProcess().MainModule.FileName;
			}
			return currentPath;
		}

		protected override string GetUserPathEx()
		{
			string basePath = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (basePath == null)
                basePath = "";
            if (basePath == "")
                basePath = Environment.GetEnvironmentVariable("HOME") + DirSep + ".config";

            return basePath + DirSep + "eddie";
        }

		public override bool ProcessKillSoft(Process process)
		{
			return (NativeMethods.eddie_kill(process.Id, (int)NativeMethods.Signum.SIGTERM) == 0);
		}

		public override int GetRecommendedRcvBufDirective()
		{
			return 256 * 1024;
		}

		public override int GetRecommendedSndBufDirective()
		{
			return 256 * 1024;
		}

		public override void FlushDNS()
		{
			base.FlushDNS();

            Engine.Instance.Elevated.DoCommandSync("dns-flush", "services", Engine.Instance.Storage.Get("linux.dns.services"));            
		}

        public override int StartProcessAsRoot(string path, string[] arguments, bool consoleMode)
        {
            bool canRunAsRoot = Platform.Instance.FileRunAsRoot(path);

            System.Diagnostics.Process processShell = null;
            bool processDirectResult = false;

            if (canRunAsRoot)
            {
                processShell = new System.Diagnostics.Process();
                processShell.StartInfo.FileName = path;
                processShell.StartInfo.Arguments = string.Join(" ", arguments);
            }
            else
            {
                if (Engine.Instance.ConsoleMode)
                {
                    processShell = new System.Diagnostics.Process();
                    processShell.StartInfo.FileName = "sudo";
                    processShell.StartInfo.Arguments = "\"" + path + "\" " + string.Join(" ", arguments);
                }
                else
                {
                    // Alternate version via osascript
                    //processShell = new System.Diagnostics.Process();
                    //processShell.StartInfo.FileName = "osascript";
                    //processShell.StartInfo.Arguments = " -e 'do shell script \"" + path + " " + string.Join(" ", arguments) + "\" with prompt \"" + LanguageManager.GetText("HelperPrivilegesPrompt").Safe() + "\" with administrator privileges'";

                    // Alternate version with RootLauncher
                    // TOFIX: pending pid, create launchd don't start it (no root?)
                    // Required for elevated-spot-check-parent-pid
                    processDirectResult = RootLauncher.LaunchExternalTool(path, arguments);
                }
            }

            if (processShell != null)
            {
                processShell.StartInfo.WorkingDirectory = "";
                processShell.StartInfo.Verb = "run";
                processShell.StartInfo.CreateNoWindow = true;
                processShell.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                processShell.StartInfo.UseShellExecute = false;
                processShell.Start();
                return processShell.Id;
            }
            else
            {
                return processDirectResult ? -1 : 0;
            }
        }

        public override void ShellSyncCore(string path, string[] arguments, string autoWriteStdin, out string stdout, out string stderr, out int exitCode)
		{
			if (autoWriteStdin != "")
				throw new Exception("ShellSyncCore::AutoWriteStdin not supported in macOS"); // Never need yet, used only in Linux

			try
			{
				var pipeOut = new NSPipe();
				var pipeErr = new NSPipe();

				var t = new NSTask();

				t.LaunchPath = path;
				t.Arguments = arguments;
				t.StandardOutput = pipeOut;
				t.StandardError = pipeErr;

				t.Launch();
				t.WaitUntilExit();
				//t.Release();
				t.Dispose();

				NSFileHandle fileOut = pipeOut.ReadHandle;
				stdout = fileOut.ReadDataToEndOfFile().ToString();
				fileOut.CloseFile();

				NSFileHandle fileErr = pipeErr.ReadHandle;
				stderr = fileErr.ReadDataToEndOfFile().ToString();
				fileErr.CloseFile();

				exitCode = t.TerminationStatus;
			}
			catch (Exception ex)
			{
				stdout = "";
				stderr = "Error: " + ex.Message;
				exitCode = -1;
			}
		}

		public override string LocateResource(string relativePath)
		{
			string resPath = NormalizePath(GetApplicationPath() + "/../Resources/" + relativePath);
			if (File.Exists(resPath))
				return resPath;

			return base.LocateResource(relativePath);
		}

		/* // This works, but we use base to avoid shell.
		public override long Ping(IpAddress host, int timeoutSec)
		{
			if ((host == null) || (host.Valid == false))
				return -1;

            {
                float iMS = -1;

                string pingPath = LocateExecutable("ping");
                if (pingPath != "")
                {
                    SystemShell s = new SystemShell();
                    s.Path = pingPath;
                    s.Arguments.Add("-c 1");
                    s.Arguments.Add("-t " + timeoutSec.ToString());
                    s.Arguments.Add("-W " + timeoutSec.ToString());
                    s.Arguments.Add("-q");
                    s.Arguments.Add("-n");
                    s.Arguments.Add(host.Address);
                    s.NoDebugLog = true;

                    if (s.Run())
                    {
                        string result = s.Output;
                        string sMS = UtilsString.ExtractBetween(result.ToLowerInvariant(), "min/avg/max/stddev = ", "/");
                        if (float.TryParse(sMS, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out iMS) == false)
                            iMS = -1;
                    }
                }

                return (long)iMS;
            }
        }
        */

		public override string GetDriverVersion(string driver)
		{
			return "Expected";
		}

		public override bool RouteAdd(Json jRoute)
		{
			IpAddress ip = jRoute["address"].Value as string;
			if (ip.Valid == false)
				return false;
			IpAddress gateway = jRoute["gateway"].Value as string;
			if (gateway.Valid == false)
				return false;
                        
            ElevatedProcess.Command c = new ElevatedProcess.Command();
            c.Parameters["command"] = "route";
            if (ip.IsV4)
                c.Parameters["layer"] = "ipv4";
            else if (ip.IsV6)
                c.Parameters["layer"] = "ipv6";
            c.Parameters["action"] = "add";
            c.Parameters["cidr"] = ip.ToCIDR();
            c.Parameters["gateway"] = gateway.Address;                
            string result = Engine.Instance.Elevated.DoCommandSync(c);
            if (result == "")
            {
                return base.RouteAdd(jRoute);
            }
            else
            {
                Engine.Instance.Logs.LogWarning(LanguageManager.GetText("RouteAddFailed", ip.ToCIDR(), gateway.ToCIDR(), result));

                return false;
            }
                        
		}

		public override bool RouteRemove(Json jRoute)
		{
			IpAddress ip = jRoute["address"].Value as string;
			if (ip.Valid == false)
				return false;
			IpAddress gateway = jRoute["gateway"].Value as string;
			if (gateway.Valid == false)
				return false;

            ElevatedProcess.Command c = new ElevatedProcess.Command();
            c.Parameters["command"] = "route";
            if (ip.IsV4)
                c.Parameters["layer"] = "ipv4";
            else if (ip.IsV6)
                c.Parameters["layer"] = "ipv6";
            c.Parameters["action"] = "delete";
            c.Parameters["cidr"] = ip.ToCIDR();
            c.Parameters["gateway"] = gateway.Address;
                
            string result = Engine.Instance.Elevated.DoCommandSync(c);
            if (result == "")
            {
                return base.RouteRemove(jRoute);
            }
            else
            {
                Engine.Instance.Logs.LogWarning(LanguageManager.GetText("RouteDelFailed", ip.ToCIDR(), gateway.ToCIDR(), result));

                return false;
            }               
		}

		public override IpAddresses ResolveDNS(string host)
		{
			// Base method with Dns.GetHostEntry have cache issue, for example on Fedora. OS X it's based on Mono.
			// Also, base methods with Dns.GetHostEntry sometime don't fetch AAAA IPv6 addresses.

			IpAddresses result = new IpAddresses();

			string hostPath = LocateExecutable("host");
			if (hostPath != "")
			{
				// Note: CNAME record are automatically followed.
				SystemShell s = new SystemShell();
				s.Path = LocateExecutable("host");
				s.Arguments.Add("-W 5");
				s.Arguments.Add(SystemShell.EscapeHost(host));
				s.NoDebugLog = true;
				if (s.Run())
				{
					string hostout = s.Output;
					foreach (string line in hostout.Split('\n'))
					{
						string ipv4 = line.RegExMatchOne("^.*? has address (.*?)$");
						if (ipv4 != "")
							result.Add(ipv4.Trim());

						string ipv6 = line.RegExMatchOne("^.*? has IPv6 address (.*?)$");
						if (ipv6 != "")
							result.Add(ipv6.Trim());
					}
				}
			}

			return result;
		}

		public override IpAddresses DetectDNS()
		{
			IpAddresses list = new IpAddresses();

			// Method1: Don't return DHCP DNS
			string networksetupPath = LocateExecutable("networksetup");
			if (networksetupPath != "")
			{
				string[] interfaces = GetInterfaces();
				foreach (string i in interfaces)
				{
					string i2 = i.Trim();

					string current = SystemShell.Shell(networksetupPath, new string[] { "-getdnsservers", SystemShell.EscapeInsideQuote(i2) });

					foreach (string line in current.Split('\n'))
					{
						string field = line.Trim();
						list.Add(field);
					}

				}
			}

			// Method2 - More info about DHCP DNS
			string scutilPath = LocateExecutable("scutil");
			if (scutilPath != "")
			{
				string scutilOut = SystemShell.Shell1(scutilPath, "--dns");
				List<List<string>> result = scutilOut.Replace(" ", "").RegExMatchMulti("nameserver\\[[0-9]+\\]:([0-9:\\.]+)");
				foreach (List<string> match in result)
				{
					foreach (string field in match)
					{
						list.Add(field);
					} 
				}
			}

            // Method3 - Compatibility
            try
            {
                if (FileExists("/etc/resolv.conf"))
                {
                    string o = FileContentsReadText("/etc/resolv.conf");
                    foreach (string line in o.Split('\n'))
                    {
                        if (line.Trim().StartsWith("#", StringComparison.InvariantCulture))
                            continue;
                        if (line.Trim().StartsWith("nameserver", StringComparison.InvariantCulture))
                        {
                            string field = line.Substring(11).Trim();
                            list.Add(field);
                        }
                    }
                }
            }
            catch
            {
                // Can be unreadable (root 600), ignore
            }

			return list;
		}

		public override void OnReport(Report report)
		{
			base.OnReport(report);

			report.Add("ifconfig", (LocateExecutable("ifconfig") != "") ? SystemShell.Shell0(LocateExecutable("ifconfig")) : "'ifconfig' " + LanguageManager.GetText("NotFound"));
		}

		public override Dictionary<string, string> GetProcessesList()
		{
			// We experience some crash under OSX with the base method.
			Dictionary<string, string> result = new Dictionary<string, string>();
			string psPath = LocateExecutable("ps");
			if (psPath != "")
			{
				string resultS = SystemShell.Shell2(psPath, "-eo", "pid,command");
				string[] resultA = resultS.Split('\n');
				foreach (string pS in resultA)
				{
					int posS = pS.IndexOf(' ');
					if (posS != -1)
					{
						string pid = pS.Substring(0, posS).Trim();
						string name = pS.Substring(posS).Trim();
						result[pid] = name;
					}
				}
			}

			return result;
		}

		public override bool OnCheckEnvironmentApp()
		{
			bool fatal = false;
			string networksetupPath = LocateExecutable("networksetup");
			if (networksetupPath == "")
			{
				Engine.Instance.Logs.Log(LogType.Error, "'networksetup' " + LanguageManager.GetText("NotFound"));

				fatal = true;
			}

			string pfctlPath = LocateExecutable("pfctl");
			if (pfctlPath == "")
				Engine.Instance.Logs.Log(LogType.Error, "'pfctl' " + LanguageManager.GetText("NotFound"));


			string hostPath = LocateExecutable("host");
			if (hostPath == "")
				Engine.Instance.Logs.Log(LogType.Error, "'host' " + LanguageManager.GetText("NotFound"));

			string psPath = LocateExecutable("ps");
			if (psPath == "")
				Engine.Instance.Logs.Log(LogType.Error, "'ps' " + LanguageManager.GetText("NotFound"));


			return (fatal == false);
		}

		public override bool OnCheckEnvironmentSession()
		{
			return true;
		}

		public override void OnNetworkLockManagerInit()
		{
			base.OnNetworkLockManagerInit();

			Engine.Instance.NetworkLockManager.AddPlugin(new NetworkLockOsxPf());
		}

		public override string OnNetworkLockRecommendedMode()
		{
			return "osx_pf";
		}

		public override void OnRecoveryLoad(XmlElement root)
		{
			XmlElement nodeDns = root.GetFirstElementByTagName("DnsSwitch");
			if (nodeDns != null)
			{
				foreach (XmlElement nodeEntry in nodeDns.ChildNodes)
				{
					DnsSwitchEntry entry = new DnsSwitchEntry();
					entry.ReadXML(nodeEntry);
					m_listDnsSwitch.Add(entry);
				}
			}

			XmlElement nodeIpV6 = root.GetFirstElementByTagName("IpV6");
			if (nodeIpV6 != null)
			{
				foreach (XmlElement nodeEntry in nodeIpV6.ChildNodes)
				{
					IpV6ModeEntry entry = new IpV6ModeEntry();
					entry.ReadXML(nodeEntry);
					m_listIpV6Mode.Add(entry); 
				}
			}

			base.OnRecoveryLoad(root);
		}

		public override void OnRecoverySave(XmlElement root)
		{
			base.OnRecoverySave(root);

			XmlDocument doc = root.OwnerDocument;

			if (m_listDnsSwitch.Count != 0)
			{
				XmlElement nodeDns = (XmlElement)root.AppendChild(doc.CreateElement("DnsSwitch"));
				foreach (DnsSwitchEntry entry in m_listDnsSwitch)
				{
					XmlElement nodeEntry = nodeDns.AppendChild(doc.CreateElement("entry")) as XmlElement;
					entry.WriteXML(nodeEntry);
				}
			}

			if (m_listIpV6Mode.Count != 0)
			{
				XmlElement nodeDns = (XmlElement)root.AppendChild(doc.CreateElement("IpV6"));
				foreach (IpV6ModeEntry entry in m_listIpV6Mode)
				{
					XmlElement nodeEntry = nodeDns.AppendChild(doc.CreateElement("entry")) as XmlElement;
					entry.WriteXML(nodeEntry);
				}
			}
		}

		public override bool OnIPv6Block()
        {            
            string result = Engine.Instance.Elevated.DoCommandSync("ipv6-block");
            if (result != "")
            {
                foreach (string resultItem in result.Split('\n'))
                {
                    string[] fields = resultItem.Split(';');
                    if (fields.Length != 6)
                        continue;
                    if (fields[0] == "SwitchIPv6")
                    {
                        string interfaceName = fields[1];

                        IpV6ModeEntry entry = new IpV6ModeEntry();
                        entry.Interface = interfaceName;
                        entry.Mode = fields[2];
                        entry.Address = fields[3];
                        entry.Router = fields[4];
                        entry.PrefixLength = fields[5];                            
                        m_listIpV6Mode.Add(entry);

                        Engine.Instance.Logs.Log(LogType.Verbose, LanguageManager.GetText("OsMacNetworkAdapterIPv6Disabled", interfaceName));
                    }
                }
            }   

			Recovery.Save();

			base.OnIPv6Block();

			return true;
		}

		public override bool OnIPv6Restore()
        {
            foreach (IpV6ModeEntry entry in m_listIpV6Mode)
            {
                Elevated.Command c = new Elevated.Command();
                c.Parameters["command"] = "ipv6-restore";
                c.Parameters["interface"] = entry.Interface;
                c.Parameters["mode"] = entry.Mode;
                c.Parameters["address"] = entry.Address;
                c.Parameters["router"] = entry.Router;
                c.Parameters["prefix"] = entry.PrefixLength;
                Engine.Instance.Elevated.DoCommandSync(c);

                Engine.Instance.Logs.Log(LogType.Verbose, LanguageManager.GetText("OsMacNetworkAdapterIPv6Restored", entry.Interface));
            }            
            
			m_listIpV6Mode.Clear();

			Recovery.Save();

			base.OnIPv6Restore();

			return true;
		}

		public override bool OnDnsSwitchDo(ConnectionActive connectionActive, IpAddresses dns)
		{
			string mode = Engine.Instance.Storage.GetLower("dns.mode");

			if (mode == "auto")
			{
                string result = Engine.Instance.Elevated.DoCommandSync("dns-switch-do", "dns", dns.ToString());
                if(result != "")
                {
                    foreach (string resultItem in result.Split('\n'))
                    {
                            string[] fields = resultItem.Split(';');
                        if (fields.Length != 3)
                            continue;
                        if (fields[0] == "SwitchDNS")
                        {
                            string interfaceName = fields[1];
                            IpAddresses oldIPs = new IpAddresses(fields[2]);

                            DnsSwitchEntry e = new DnsSwitchEntry();
                            e.Name = interfaceName;
                            e.Dns = oldIPs.Addresses;
                            m_listDnsSwitch.Add(e);

                            Engine.Instance.Logs.Log(LogType.Verbose, LanguageManager.GetText("OsMacNetworkAdapterDnsDone", interfaceName, ((oldIPs.Count == 0) ? "Automatic" : oldIPs.Addresses), dns.Addresses));
                        }
                    }
                }
                
				Recovery.Save();
			}

			base.OnDnsSwitchDo(connectionActive, dns);

			return true;
		}

		public override bool OnDnsSwitchRestore()
        {
			foreach (DnsSwitchEntry e in m_listDnsSwitch)
			{				
				IpAddresses dns = new IpAddresses();
				dns.Add(e.Dns);

                string result = Engine.Instance.Elevated.DoCommandSync("dns-switch-restore", "interface", e.Name, "dns", dns.ToString());

                Engine.Instance.Logs.Log(LogType.Verbose, LanguageManager.GetText("OsMacNetworkAdapterDnsRestored", e.Name, ((e.Dns == "") ? "Automatic" : e.Dns)));                
			}

			m_listDnsSwitch.Clear();

			Recovery.Save();

			base.OnDnsSwitchRestore();

			return true;
		}
        
        public override Json GetRealtimeNetworkStats()
        {
            // Mono NetworkInterface::GetIPv4Statistics().BytesReceived always return 0 under OSX.

            Json result = new Json();
            result.EnsureArray();

            int maxLen = 1024;
            byte[] buf = new byte[maxLen];
            NativeMethods.eddie_get_realtime_network_stats(buf, maxLen);
            string jNativeStr = System.Text.Encoding.ASCII.GetString(buf);

            Json jNative = Json.Parse(jNativeStr);

            // Expect the sequence is the same.
            // C++ edition don't detect interface name right now, otherwise NetworkInterface here can be removed.
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            for(int i = 0;i< interfaces.Length;i++)
            {
                if(i<jNative.GetArray().Count)
                {
                    Json jNativeIf = jNative.GetIndex(i) as Json;

                    Int64 rcv = jNativeIf["rcv"].ValueInt64;
                    Int64 snd = jNativeIf["snd"].ValueInt64;
                    Json jInterface = new Json();
                    jInterface["id"].Value = interfaces[i].Id;
                    jInterface["rcv"].Value = rcv;
                    jInterface["snd"].Value = snd;
                    result.Append(jInterface);
                }
            }

            return result;
        }

        public override void OnJsonNetworkInfo(Json jNetworkInfo)
		{
			// Step1: Set IPv6 support to true by default.
			// From base virtual, always 'false'. Missing Mono implementation? 
			// After for interfaces listed by 'networksetup -listallhardwareports' we detect specific support.
			foreach (Json jNetworkInterface in jNetworkInfo["interfaces"].Json.GetArray())
			{
				jNetworkInterface["support_ipv6"].Value = true;
			}

			// Step2: Query 'networksetup -listallhardwareports' to obtain a more accurate device friendly names.
			string networksetupPath = LocateExecutable("networksetup");
			if (networksetupPath != "")
			{
				string nsOutput = SystemShell.Shell1(networksetupPath, "-listallhardwareports");
				string lastName = "";
				foreach (string line in nsOutput.Split('\n'))
				{
					if (line.StartsWith("Hardware Port: ", StringComparison.InvariantCulture))
						lastName = line.Substring(15).Trim();
					if (line.StartsWith("Device:", StringComparison.InvariantCulture))
					{
						string deviceId = line.Substring(7).Trim();
						foreach (Json jNetworkInterface in jNetworkInfo["interfaces"].Json.GetArray())
						{
							if (jNetworkInterface["id"].Value as string == deviceId)
							{
								// Set friendly name
								jNetworkInterface["friendly"].Value = lastName;

								// Detect IPv6 support
								string getInfo = SystemShell.Shell(LocateExecutable("networksetup"), new string[] { "-getinfo", SystemShell.EscapeInsideQuote(lastName) });

								string mode = getInfo.RegExMatchOne("^IPv6: (.*?)$").Trim();

								if (mode == "Off")
									jNetworkInterface["support_ipv6"].Value = false;
								else
									jNetworkInterface["support_ipv6"].Value = true;

								break;
							}
						}
					}
				}
			}
		}

		public override void OnJsonRouteList(Json jRoutesList)
        {
			base.OnJsonRouteList(jRoutesList);

			string netstatPath = LocateExecutable("netstat"); 
			if (netstatPath != "")
			{
				string result = SystemShell.Shell1(netstatPath, "-rnl");

				string[] lines = result.Split('\n');
				foreach (string line in lines)
				{
					if (line == "Routing tables")
						continue;
					if (line == "Internet:")
						continue;
					if (line == "Internet6:")
						continue;

					string[] fields = line.CleanSpace().Split(' ');

					if ((fields.Length > 0) && (fields[0].ToLowerInvariant().Trim() == "destination"))
						continue;

					if (fields.Length >= 7)
					{
						Json jRoute = new Json();
						IpAddress address = new IpAddress();
						if (fields[0].ToLowerInvariant().Trim() == "default")
						{
							IpAddress gateway = new IpAddress(fields[1]);
							if (gateway.Valid == false)
								continue;
							if (gateway.IsV4)
								address = IpAddress.DefaultIPv4;
							else if (gateway.IsV6)
								address = IpAddress.DefaultIPv6;
						}
						else
							address.Parse(fields[0]);
						if (address.Valid == false)
							continue;
						jRoute["address"].Value = address.ToCIDR();
						jRoute["gateway"].Value = fields[1];
						jRoute["flags"].Value = fields[2];
						jRoute["refs"].Value = fields[3];
						jRoute["use"].Value = fields[4];
						jRoute["mtu"].Value = fields[5];
						jRoute["interface"].Value = fields[6];
						if (fields.Length > 7)
							jRoute["expire"].Value = fields[7];
						jRoutesList.Append(jRoute);
					}
				}
			}
		}

		public string[] GetInterfaces()
		{
			List<string> result = new List<string>();
			foreach (string line in SystemShell.Shell(LocateExecutable("networksetup"), new string[] { "-listallnetworkservices" }).Split('\n'))
			{
				if (line.StartsWith("An asterisk", StringComparison.InvariantCultureIgnoreCase))
					continue;
				if (line.Trim() == "")
					continue;
				result.Add(line.Trim());
			}

			return result.ToArray();
		}

		public override string OsCredentialSystemName()
		{
			return "macOS Keychain";
		}

		public override string OsCredentialSystemRead(string name)
		{
			byte[] b = null;
			var code = Security.SecKeyChain.FindGenericPassword(Constants.Name + " - " + name, name, out b);
			if (code == Security.SecStatusCode.Success)
				return Encoding.UTF8.GetString(b);
			else
				return "";
		}

		public override bool OsCredentialSystemWrite(string name, string password)
		{
			var b = Encoding.UTF8.GetBytes(password);

			if (OsCredentialSystemDelete(name) == false) // Otherwise is not overwritten
				return false;

			Security.SecStatusCode ssc;
			ssc = Security.SecKeyChain.AddGenericPassword(Constants.Name + " - " + name, name, b);
			return (ssc == Security.SecStatusCode.Success);
		}

		public override bool OsCredentialSystemDelete(string name)
		{
			Security.SecRecord sr = new Security.SecRecord(Security.SecKind.GenericPassword);
			sr.Service = Constants.Name + " - " + name;
			sr.Account = name;
			Security.SecStatusCode ssc;
			Security.SecRecord find = Security.SecKeyChain.QueryAsRecord(sr, out ssc);
			if (ssc == Security.SecStatusCode.Success)
			{
				sr.ValueData = find.ValueData;
				Security.SecStatusCode ssc2 = Security.SecKeyChain.Remove(sr);
				if (ssc2 == Security.SecStatusCode.Success)
					return true;
				else
					return false;
			}
			else if (ssc == Security.SecStatusCode.ItemNotFound)
				return true;
			else
				return false;
		}

		public override List<string> GetTrustedPaths()
		{
			List<string> list = base.GetTrustedPaths();
			/*
            list.Add("/bin");
            list.Add("/usr/bin");
            list.Add("/sbin");
            list.Add("/usr/sbin");
            */
			list.Add(LocateExecutable("codesign"));
			list.Add(LocateExecutable("whoami")); // ClodoTemp, remove with elevation

			return list;
		}

		public override string FileGetSignedId(string path)
		{
			string codesignPath = LocateExecutable("codesign");
			SystemShell cmdV = new SystemShell();
			cmdV.Path = codesignPath;
			cmdV.Arguments.Add("-v");
			cmdV.Arguments.Add(SystemShell.EscapePath(path));
			cmdV.Run();
			if (cmdV.Output != "") // ExitCode always 0
				return "No: Invalid signature (tampered?)";

			SystemShell cmdS = new SystemShell();
			cmdS.Path = codesignPath;
			cmdS.Arguments.Clear();
			cmdS.Arguments.Add("-dv");
			cmdS.Arguments.Add("--verbose=4");
			cmdS.Arguments.Add(SystemShell.EscapePath(path));
			cmdS.Run();

			string codesignResult = cmdS.Output;
			string o = "";
			foreach (string line in codesignResult.Split('\n'))
			{
				int posE = line.IndexOf("=", StringComparison.InvariantCulture);
				if (posE != -1)
				{
					string k = line.Substring(0, posE);
					if (k == "Authority")
					{
						if (o != "")
							o += " - ";
						o += line.Substring(posE + 1);
					}
				}
			}

			if (o != "")
				return o;
			else
				return base.FileGetSignedId(path);
		}
	}

	public class DnsSwitchEntry
	{
		public string Name;
		public string Dns;

		public void ReadXML(XmlElement node)
		{
			Name = node.GetAttributeString("name", "");
			Dns = node.GetAttributeString("dns", "");
		}

		public void WriteXML(XmlElement node)
		{
			node.SetAttributeString("name", Name);
			node.SetAttributeString("dns", Dns);
		}
	}

	public class IpV6ModeEntry
	{
		public string Interface;
		public string Mode;
		public string Address;
		public string Router;
		public string PrefixLength;

		public void ReadXML(XmlElement node)
		{
			Interface = node.GetAttributeString("interface", "");
			Mode = node.GetAttributeString("mode", "");
			Address = node.GetAttributeString("address", "");
			Router = node.GetAttributeString("router", "");
			PrefixLength = node.GetAttributeString("prefix_length", "");
		}

		public void WriteXML(XmlElement node)
		{
			node.SetAttributeString("interface", Interface);
			node.SetAttributeString("mode", Mode);
			node.SetAttributeString("address", Address);
			node.SetAttributeString("router", Router);
			node.SetAttributeString("prefix_length", PrefixLength);
		}
	}
}

