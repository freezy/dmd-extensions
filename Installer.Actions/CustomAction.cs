﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Installer.Actions
{
	public class CustomActions
	{

		[CustomAction]
		public static ActionResult GetVpmFolder(Session session)
		{
			try {
				session.Log("Searching in registry for VPM...");
				RegistryKey reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\VPinMAME.Controller\CLSID");
				if (reg != null) {
					var clsid = reg.GetValue(null).ToString();

					var x64Suffix = OsUtil.Is64BitOperatingSystem ? @"WOW6432Node\" : "";
					reg = Registry.ClassesRoot.OpenSubKey(x64Suffix + @"CLSID\" + clsid + @"\InprocServer32");
					if (reg != null) {
						session["VPMFROMREG"] = Path.GetDirectoryName(reg.GetValue(null).ToString());
						session.Log(@"Found VPM folder at " + session["VPMFROMREG"]);

					} else {
						session.Log(@"Could not find CLSID " + clsid + " of VPinMAME.dll.");
					}
					
				} else {
					session.Log(@"Looks like VPinMAME.Controller is not registered on the system!");
				}
			} catch (Exception e) {
				session.Log(@"Error " + e);
			}
			return ActionResult.Success;
		}

		[CustomAction]
		public static ActionResult GetProPinballFolder(Session session)
		{
			try {
				session.Log("Searching for Pro Pinball...");
				var steamInfo = new SteamInfo(session);
				var proPinball = 287900;
				if (!steamInfo.IsGameInstalled(proPinball)) {
					session.Log(@"Could not find Pro Pinball.");
					return ActionResult.Success;
				}
				session["PROPINBALLDIR"] = steamInfo.GetInstallationDirectory(proPinball);
				session.Log(@"Found Pro Pinball folder at " + session["PROPINBALLDIR"]);

			} catch (Exception e) {
				session.Log(@"Error " + e);
			}
			return ActionResult.Success;
		}

		[CustomAction]
		public static ActionResult GetFpFolder(Session session)
		{
			try
			{
				session.Log("Searching in registry for FP...");
				RegistryKey reg = Registry.ClassesRoot.OpenSubKey(@"TypeLib\{FB22A459-4AD0-4CB3-B959-15158F7139F5}\1.0\0\win32");
				if (reg != null) {
					var fpExePath = reg.GetValue(null).ToString();

					if (fpExePath != null) {
						session["FPFROMREG"] = Path.GetDirectoryName(fpExePath);
						session.Log(@"Found FP folder at " + session["FPFROMREG"]);

					} else {
						session.Log(@"Could not find FP path in typelib registry.");
					}

				} else {
					session.Log(@"Could not find FP path in registry.");
				}
			
				} catch (Exception e) {
				session.Log(@"Error " + e);
			}
			return ActionResult.Success;
		}

	}
}
