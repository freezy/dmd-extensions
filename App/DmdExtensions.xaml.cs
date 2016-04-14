using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Mindscape.Raygun4Net;

namespace App
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class DmdExtensions : Application
	{
		private readonly RaygunClient _raygunClient = new RaygunClient("E6b766fERbKAUzm/QNE/Sw==");

		public DmdExtensions()
		{
			DispatcherUnhandledException += OnDispatcherUnhandledException;
		}

		void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			_raygunClient.Send(e.Exception);
		}
	}
}
