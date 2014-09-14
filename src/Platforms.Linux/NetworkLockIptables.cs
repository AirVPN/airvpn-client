﻿// <airvpn_source_header>
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
using System.IO;
using System.Text;
using System.Xml;
using AirVPN.Core;

namespace AirVPN.Platforms
{
	public class NetworkLockIptables : NetworkLockPlugin
	{
		private List<IpAddressRange> m_currentList = new List<IpAddressRange>();

		public override string GetCode()
		{
			return "linux_iptables";
		}

		public override string GetName()
		{
			return "Linux IPTables";
		}

		public override bool GetSupport()
		{
			if (Exec("iptables --version").IndexOf("iptables v") != 0)
				return false;

			if (Exec("iptables-save").IndexOf("# Generated by iptables-save v") != 0)
				return false;

			return true;
		}

		public string GetBackupPath()
		{
			return Storage.DataPath + Platform.Instance.DirSep + "iptables.dat";
		}

		public override void Activation()
		{
			base.Activation();

			string rulesBackupSession = GetBackupPath();

			if (File.Exists(rulesBackupSession))
				throw new Exception(Messages.NetworkLockUnexpectedAlreadyActive);

			// Backup
			Exec("iptables-save >\"" + rulesBackupSession + "\"");

			// Flush
			Exec("iptables -F");
			Exec("iptables -t nat -F");
			Exec("iptables -t mangle -F");

			// Local
			Exec("iptables -A INPUT -i lo -j ACCEPT");
			Exec("iptables -A OUTPUT -o lo -j ACCEPT");

			// Make sure you can communicate with any DHCP server
			Exec("iptables -A OUTPUT -d 255.255.255.255 -j ACCEPT");
			Exec("iptables -A INPUT -s 255.255.255.255 -j ACCEPT");

			// Make sure that you can communicate within your own private networks
			Exec("iptables -A INPUT -s 192.168.0.0/16 -d 192.168.0.0/16 -j ACCEPT");
			Exec("iptables -A OUTPUT -s 192.168.0.0/16 -d 192.168.0.0/16 -j ACCEPT");
			Exec("iptables -A INPUT -s 10.0.0.0/8 -d 10.0.0.0/8 -j ACCEPT");
			Exec("iptables -A OUTPUT -s 10.0.0.0/8 -d 10.0.0.0/8 -j ACCEPT");
			Exec("iptables -A INPUT -s 172.16.0.0/12 -d 172.16.0.0/12 -j ACCEPT");
			Exec("iptables -A OUTPUT -s 172.16.0.0/12 -d 172.16.0.0/12 -j ACCEPT");

			// Allow incoming pings (can be disabled)
			Exec("iptables -A INPUT -p icmp --icmp-type echo-request -j ACCEPT");

			// Allow established sessions to receive traffic: 
			Exec("iptables -A INPUT -m state --state ESTABLISHED,RELATED -j ACCEPT");

			// Allow TUN 
			Exec("iptables -A INPUT -i tun+ -j ACCEPT");
			Exec("iptables -A FORWARD -i tun+ -j ACCEPT");
			Exec("iptables -A OUTPUT -o tun+ -j ACCEPT");

			// Block All
			Exec("iptables -A OUTPUT -j DROP");
			Exec("iptables -A INPUT -j DROP");
			Exec("iptables -A FORWARD -j DROP");


			OnUpdateIps();

			
		}

		public override void Deactivation()
		{
			base.Deactivation();

			string rulesBackupSession = GetBackupPath();

			if (File.Exists(rulesBackupSession))
			{
				// Flush
				Exec("iptables -F");
				Exec("iptables -t nat -F");
				Exec("iptables -t mangle -F");

				// Backup
				Exec("iptables-restore <\"" + rulesBackupSession + "\"");

				File.Delete(rulesBackupSession);

				m_currentList.Clear();
			}
		}

		public override void OnUpdateIps()
		{
			base.OnUpdateIps();

			List<IpAddressRange> ipsFirewalled = GetAllIps();

			// Remove IP not present in the new list
			foreach (IpAddressRange ip in m_currentList)
			{
				if(ipsFirewalled.Contains(ip) == false)
				{
					// Delete
					string cmd = "iptables -D OUTPUT -d " + ip.ToCIDR() + " -j ACCEPT";
					Exec(cmd);
				}
			}

			// Add IP
			foreach (IpAddressRange ip in ipsFirewalled)
			{
				if (m_currentList.Contains(ip) == false)
				{
					// Add
					string cmd = "iptables -I OUTPUT 1 -d " + ip.ToCIDR() + " -j ACCEPT";
					Exec(cmd);
				}
			}

			m_currentList = ipsFirewalled;
		}
	}
}
