using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;

namespace Installer.Actions
{
	public class CustomActions
	{
		[CustomAction]
		public static ActionResult GetVpmFolder(Session session)
		{
			session.Log("Setting DIRFROMREG!");
			try {
				session.Log("Directory search");
				RegistryKey reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\VPinMAME.Controller\CLSID");
				if (reg != null) {
					var clsid = reg.GetValue(null).ToString();

					reg = Registry.ClassesRoot.OpenSubKey(@"WOW6432Node\CLSID\" + clsid + @"\InprocServer32");
					if (reg != null) {
						session["DIRFROMREG"] = Path.GetDirectoryName(reg.GetValue(null).ToString());
						session.Log(@"directory is " + session["DIRFROMREG"]);

					} else {
						session.Log(@"Could not find CLSID " + clsid);
					}
					
				} else {
					session.Log(@"Looks like VPinMAME.Controller is not registered on the system!");
				}
			} catch (Exception e) {
				session.Log(@"Error " + e);
			}
			return ActionResult.Success;
		}
	}
}
