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
			UpdateShaderValue(WidthProperty);
			UpdateShaderValue(HeightProperty);
			UpdateShaderValue(SizeProperty);
		}

		public Brush Input
		{
			get { return (Brush)GetValue(InputProperty); }
			set { SetValue(InputProperty, value); }
		}

		public static readonly DependencyProperty InputProperty =
			RegisterPixelShaderSamplerProperty("Input", typeof(DmdEffect), 0);

		public float Width
		{
			get { return (float)GetValue(WidthProperty); }
			set { SetValue(WidthProperty, value); }
		}

		public static readonly DependencyProperty WidthProperty =
			DependencyProperty.Register("Width", typeof(float), typeof(DmdEffect),
			  new UIPropertyMetadata(1024f, PixelShaderConstantCallback(0)));

		public float Height
		{
			get { return (float)GetValue(HeightProperty); }
			set { SetValue(HeightProperty, value); }
		}

		public static readonly DependencyProperty HeightProperty =
			DependencyProperty.Register("Height", typeof(float), typeof(DmdEffect),
			  new UIPropertyMetadata(256f, PixelShaderConstantCallback(1)));

		public float Size
		{
			get { return (float)GetValue(SizeProperty); }
			set { SetValue(SizeProperty, value); }
		}

		public static readonly DependencyProperty SizeProperty =
			DependencyProperty.Register("Size", typeof(float), typeof(DmdEffect),
			  new UIPropertyMetadata(1.0f, PixelShaderConstantCallback(2)));

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
