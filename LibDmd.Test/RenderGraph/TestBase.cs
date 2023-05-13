using NUnit.Framework;

namespace LibDmd.Test
{
	public class TestBase
	{
		private TestContext testContextInstance;

		/// <summary>
		/// Gets or sets the test context which provides
		/// information about and functionality for the current test run.
		/// </summary>
		public TestContext TestContext
		{
			get { return testContextInstance; }
			set { testContextInstance = value; }
		}

		protected void Print(object obj)
		{
			TestContext.WriteLine(obj);
		}

	}
}
