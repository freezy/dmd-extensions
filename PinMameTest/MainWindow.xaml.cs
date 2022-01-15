using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace PinMameTest
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// 
	///            !!! DisconnectedContext can be ignored (don't break) !!!
	/// 
	/// </summary>
	public partial class MainWindow : Window
	{
		private VPinMameController Controller;

		public MainWindow()
		{
			InitializeComponent();

			Console.WriteLine("[{0}] Starting...", Thread.CurrentThread.ManagedThreadId);

			Controller = new VPinMameController();
			//Controller.Run("sshtl_l7").Subscribe(status => {
			//Controller.Run("sprk_103").Subscribe(status => {
			//Controller.Run("gw_l5").Subscribe(status => {
			//Controller.Run("fg_1200af").Subscribe(status => {
			Controller.Run("mm_109c").Subscribe(status => {
			//Controller.Run("ffv104").Subscribe(status => {

				Console.WriteLine("[{0}] Game status: {1}", Thread.CurrentThread.ManagedThreadId, status);
			});
		}
	}
}
