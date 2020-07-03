using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LibDmd.Output.Virtual.Dmd
{
	/// <summary>
	/// An effect that transforms upscaled pixel into dots.
	/// </summary>
	/// <see cref="http://stackoverflow.com/questions/25452349/wpf-shader-effect-antialiasing-not-showing"/>
	public class DmdEffect : ShaderEffect
	{
		static DmdEffect()
		{
			// Associate _pixelShader with our compiled pixel shader
			_pixelShader.UriSource = Global.MakePackUri("Output/Virtual/Dmd/Dmd.ps");
		}
		private static readonly PixelShader _pixelShader = new PixelShader();

		public DmdEffect()
		{
			PixelShader = _pixelShader;
			UpdateShaderValue(InputProperty);
			UpdateShaderValue(DotWidthProperty);
			UpdateShaderValue(DotHeightProperty);
			UpdateShaderValue(PixelWidthProperty);
			UpdateShaderValue(PixelHeightProperty);
			UpdateShaderValue(SizeProperty);
		}

		public Brush Input
		{
			get { return (Brush)GetValue(InputProperty); }
			set { SetValue(InputProperty, value); }
		}

		public static readonly DependencyProperty InputProperty =
			RegisterPixelShaderSamplerProperty("Input", typeof(DmdEffect), 0);

		public float DotWidth
		{
			get { return (float)GetValue(DotWidthProperty); }
			set { SetValue(DotWidthProperty, value); }
		}

		public static readonly DependencyProperty DotWidthProperty =
			DependencyProperty.Register("DotWidth", typeof(float), typeof(DmdEffect),
			  new UIPropertyMetadata(128f, PixelShaderConstantCallback(0)));

		public float DotHeight
		{
			get { return (float)GetValue(DotHeightProperty); }
			set { SetValue(DotHeightProperty, value); }
		}

		public static readonly DependencyProperty DotHeightProperty =
			DependencyProperty.Register("DotHeight", typeof(float), typeof(DmdEffect),
			  new UIPropertyMetadata(32f, PixelShaderConstantCallback(1)));

		public float PixelWidth
		{
			get { return (float)GetValue(PixelWidthProperty); }
			set { SetValue(PixelWidthProperty, value); }
		}

		public static readonly DependencyProperty PixelWidthProperty =
			DependencyProperty.Register("PixelWidth", typeof(float), typeof(DmdEffect),
			  new UIPropertyMetadata(1024f, PixelShaderConstantCallback(2)));

		public float PixelHeight
		{
			get { return (float)GetValue(PixelHeightProperty); }
			set { SetValue(PixelHeightProperty, value); }
		}

		public static readonly DependencyProperty PixelHeightProperty =
			DependencyProperty.Register("PixelHeight", typeof(float), typeof(DmdEffect),
			  new UIPropertyMetadata(256f, PixelShaderConstantCallback(3)));

		public float Size
		{
			get { return (float)GetValue(SizeProperty); }
			set { SetValue(SizeProperty, value); }
		}

		public static readonly DependencyProperty SizeProperty =
			DependencyProperty.Register("Size", typeof(float), typeof(DmdEffect),
			  new UIPropertyMetadata(1.25f, PixelShaderConstantCallback(4)));

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
				if (_assemblyShortName == null) {
					var a = typeof(Global).Assembly;
					_assemblyShortName = a.ToString().Split(',')[0];
				}
				return _assemblyShortName;
			}
		}

		private static string _assemblyShortName;
	}
}
