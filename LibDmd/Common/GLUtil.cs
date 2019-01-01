using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibDmd.Output.Virtual.SkiaDmd.GLContext;
using SkiaSharp;

namespace LibDmd.Common
{
	public class GLUtil
	{
		private static GLUtil _instance;

		private GRContext _grContext;
		private WglContext _glContext = new WglContext();

		private GLUtil()
		{
			_glContext.MakeCurrent();
			_grContext = GRContext.Create(GRBackend.OpenGL);
		}

		public SKSurface CreateSurface(int width, int height)
		{
			return SKSurface.Create(_grContext, true, new SKImageInfo(width, height));
		}

		public static GLUtil GetInstance()
		{
			return _instance ?? (_instance = new GLUtil());
		}

		public void Destroy()
		{
			_grContext?.Dispose();
			_glContext.Destroy();
			_grContext = null;
			_glContext = null;
			_instance = null;
		}
	}
}
