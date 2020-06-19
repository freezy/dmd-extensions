using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LibDmd.Output.Virtual.AlphaNumeric
{
	public class AlphaNumEffect : ShaderEffect
	{
		static AlphaNumEffect()
		{
			// Associate _pixelShader with our compiled pixel shader
			// fxc /O0 /Fc /Zi /T ps_5_0 /Gec /Fo AlphaNum.ps AlphaNum.fx
			_pixelShader.UriSource = Global.MakePackUri("Output/Virtual/AlphaNumeric/AlphaNum.ps");
		}
		private static readonly PixelShader _pixelShader = new PixelShader();

		public AlphaNumEffect()
		{
			PixelShader = _pixelShader;
			//UpdateShaderValue(SegmentsProperty);
			UpdateShaderValue(SegmentWidthProperty);
			UpdateShaderValue(TargetWidthProperty);
			UpdateShaderValue(TargetHeightProperty);
		}

		//#region Segments
		//public int[] Segments
		//{
		//	get { return (int[])GetValue(SegmentsProperty); }
		//	set { SetValue(SegmentsProperty, value); }
		//}

		//public static readonly DependencyProperty SegmentsProperty =
		//	DependencyProperty.Register("Segments", typeof(int[]), typeof(AlphaNumEffect),
		//	  new UIPropertyMetadata(new int[] { 0x0 }, PixelShaderConstantCallback(0)));
		//#endregion

		#region SegmentWidth
		public float SegmentWidth
		{
			get { return (float)GetValue(SegmentWidthProperty); }
			set { SetValue(SegmentWidthProperty, value); }
		}

		public static readonly DependencyProperty SegmentWidthProperty =
			DependencyProperty.Register("SegmentWidth", typeof(float), typeof(AlphaNumEffect),
			  new UIPropertyMetadata(0.07f, PixelShaderConstantCallback(0)));
		#endregion

		#region TargetDim
		public int TargetWidth
		{
			get { return (int)GetValue(TargetWidthProperty); }
			set { SetValue(TargetWidthProperty, value); }
		}

		public static readonly DependencyProperty TargetWidthProperty =
			DependencyProperty.Register("TargetWidth", typeof(int), typeof(AlphaNumEffect),
			  new UIPropertyMetadata(0, PixelShaderConstantCallback(0)));
		#endregion

		#region TargetHeight
		public int TargetHeight
		{
			get { return (int)GetValue(TargetHeightProperty); }
			set { SetValue(TargetHeightProperty, value); }
		}

		public static readonly DependencyProperty TargetHeightProperty =
			DependencyProperty.Register("TargetHeight", typeof(int), typeof(AlphaNumEffect),
			  new UIPropertyMetadata(0, PixelShaderConstantCallback(0)));
		#endregion

		#region NumLines
		public int NumLines
		{
			get { return (int)GetValue(TargetWidthProperty); }
			set { SetValue(NumLinesProperty, value); }
		}

		public static readonly DependencyProperty NumLinesProperty =
			DependencyProperty.Register("NumLines", typeof(int), typeof(AlphaNumEffect),
			  new UIPropertyMetadata(0, PixelShaderConstantCallback(0)));
		#endregion

		#region NumChars
		public int NumChars
		{
			get { return (int)GetValue(NumCharsProperty); }
			set { SetValue(NumCharsProperty, value); }
		}

		public static readonly DependencyProperty NumCharsProperty =
			DependencyProperty.Register("NumChars", typeof(int), typeof(AlphaNumEffect),
			  new UIPropertyMetadata(0, PixelShaderConstantCallback(0)));
		#endregion
	}

	internal static class Global
	{
		public static Uri MakePackUri(string relativeFile)
		{
			var uriString = "pack://application:,,,/" + AssemblyShortName + ";component/" + relativeFile;
			return new Uri(uriString);
		}

		private static string AssemblyShortName
		{
			get
			{
				if (_assemblyShortName == null)
				{
					var a = typeof(Global).Assembly;
					_assemblyShortName = a.ToString().Split(',')[0];
				}
				return _assemblyShortName;
			}
		}

		private static string _assemblyShortName;
	}
}
