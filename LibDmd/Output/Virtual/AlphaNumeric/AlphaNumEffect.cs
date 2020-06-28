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
			// fxc /O1 /Fc /Zi /T ps_5_0 /Gec /Fo AlphaNum.ps AlphaNum.fx
			_pixelShader.UriSource = Global.MakePackUri("Output/Virtual/AlphaNumeric/AlphaNum.ps");
		}
		private static readonly PixelShader _pixelShader = new PixelShader();

		public AlphaNumEffect()
		{
			PixelShader = _pixelShader;
			//UpdateShaderValue(SegmentsProperty);
			UpdateShaderValue(InputProperty);
			UpdateShaderValue(SegmentWidthProperty);
			UpdateShaderValue(TargetWidthProperty);
			UpdateShaderValue(TargetHeightProperty);
			UpdateShaderValue(NumLinesProperty);
			UpdateShaderValue(NumCharsProperty);
			UpdateShaderValue(NumSegmentsProperty);
		}

		#region Input
		public Brush Input
		{
			get { return (Brush)GetValue(InputProperty); }
			set { SetValue(InputProperty, value); }
		}

		public static readonly DependencyProperty InputProperty =
			RegisterPixelShaderSamplerProperty("Input", typeof(AlphaNumEffect), 0);
		#endregion

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
		public float TargetWidth
		{
			get { return (float)GetValue(TargetWidthProperty); }
			set { SetValue(TargetWidthProperty, value); }
		}

		public static readonly DependencyProperty TargetWidthProperty =
			DependencyProperty.Register("TargetWidth", typeof(float), typeof(AlphaNumEffect),
			  new UIPropertyMetadata(1280f, PixelShaderConstantCallback(1)));
		#endregion

		#region TargetHeight
		public float TargetHeight
		{
			get { return (float)GetValue(TargetHeightProperty); }
			set { SetValue(TargetHeightProperty, value); }
		}

		public static readonly DependencyProperty TargetHeightProperty =
			DependencyProperty.Register("TargetHeight", typeof(float), typeof(AlphaNumEffect),
			  new UIPropertyMetadata(120f, PixelShaderConstantCallback(2)));
		#endregion

		#region NumLines
		public float NumLines
		{
			get { return (float)GetValue(NumLinesProperty); }
			set { SetValue(NumLinesProperty, value); }
		}

		public static readonly DependencyProperty NumLinesProperty =
			DependencyProperty.Register("NumLines", typeof(float), typeof(AlphaNumEffect),
			  new UIPropertyMetadata(1f, PixelShaderConstantCallback(3)));
		#endregion

		#region NumChars
		public float NumChars
		{
			get { return (float)GetValue(NumCharsProperty); }
			set { SetValue(NumCharsProperty, value); }
		}

		public static readonly DependencyProperty NumCharsProperty =
			DependencyProperty.Register("NumChars", typeof(float), typeof(AlphaNumEffect),
			  new UIPropertyMetadata(0f, PixelShaderConstantCallback(4)));
		#endregion

		#region NumSegments
		public float NumSegments
		{
			get { return (float)GetValue(NumSegmentsProperty); }
			set { SetValue(NumSegmentsProperty, value); }
		}

		public static readonly DependencyProperty NumSegmentsProperty =
			DependencyProperty.Register("NumSegments", typeof(float), typeof(AlphaNumEffect),
			  new UIPropertyMetadata(0f, PixelShaderConstantCallback(5)));
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
