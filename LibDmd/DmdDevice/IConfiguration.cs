using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using IniParser;
using IniParser.Model;
using LibDmd.Common;
using LibDmd.Input;
using LibDmd.Output.Virtual.AlphaNumeric;
using NLog;
using SkiaSharp;

namespace LibDmd.DmdDevice
{
	public interface IConfiguration
	{
		IGlobalConfig Global { get; }
		IVirtualDmdConfig VirtualDmd { get; }
		IVirtualAlphaNumericDisplayConfig VirtualAlphaNumericDisplay { get; }
		IPinDmd1Config PinDmd1 { get; }
		IPinDmd2Config PinDmd2 { get; }
		IPinDmd3Config PinDmd3 { get; }
		IPin2DmdConfig Pin2Dmd { get; }
		IVideoConfig Video { get; }
		IGifConfig Gif { get; }
		IBitmapConfig Bitmap { get; }
		IVpdbConfig VpdbStream { get; }
		IBrowserConfig BrowserStream { get; }
		IPinUpConfig PinUp { get; }
}

	public interface IGlobalConfig 
	{
		ResizeMode Resize { get; }
		bool FlipHorizontally { get; }
		bool FlipVertically { get; }
		bool Colorize { get; }
		bool QuitWhenDone { get; }
		int QuitAfter { get; }
		bool NoClear { get; }
		Color DmdColor { get; }
	}

	public interface IPinDmd1Config
	{
		bool Enabled { get; }
	}

	public interface IPinDmd2Config
	{
		bool Enabled { get; }
	}

	public interface IPinDmd3Config
	{
		bool Enabled { get; }
		string Port { get; }
	}

	public interface IPin2DmdConfig
	{
		bool Enabled { get; }
		int Delay { get; }
	}

	public interface IVirtualDmdConfig
	{
		bool Enabled { get; }
		bool StayOnTop { get; }
		bool IgnoreAr { get; }
		bool UseRegistryPosition { get; }
		bool HideGrip { get; }
		double Left { get; }
		double Top { get; }
		double Width { get; }
		double Height { get; }
		double DotSize { get; }
		bool HasGameOverride(string key);
	}

	public interface IVirtualAlphaNumericDisplayConfig
	{
		bool Enabled { get; }
		RasterizeStyleDefinition Style { get; }
	}

	public interface IVideoConfig
	{
		bool Enabled { get; }
		string Path { get; }
	}

	public interface IGifConfig
	{
		bool Enabled { get; }
		string Path { get; }
	}

	public interface IBitmapConfig
	{
		bool Enabled { get; }
		string Path { get; }
	}

	public interface IBrowserConfig
	{
		bool Enabled { get; }
		int Port { get; }
	}

	public interface IVpdbConfig
	{
		bool Enabled { get; }
		string EndPoint { get; }
	}

	public interface IPinUpConfig
	{
		bool Enabled { get; }
		string GameName { get; }
	}
}
