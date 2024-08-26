using System.Threading;
using System.Windows;
using NUnit.Framework;

namespace KsWare.Presentation.XamlProcessing.Tests {

	[SetUpFixture]
	public class WpfAppInitializer {

		private static Thread _uiThread;
		private static AutoResetEvent _initComplete;

		[OneTimeSetUp]
		public void InitializeWpfApp() {
			_initComplete = new AutoResetEvent(false);
			_uiThread = new Thread(() => {
				// Starte die WPF-Anwendung
				var app = new Application();
				app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

				// Signalisiere, dass die Initialisierung abgeschlossen ist
				_initComplete.Set();

				// Starte die WPF-Anwendungsnachrichtenschleife
				app.Run();
			});

			_uiThread.SetApartmentState(ApartmentState.STA);
			_uiThread.IsBackground = true;
			_uiThread.Start();

			// Warte, bis die WPF-Anwendung initialisiert ist
			_initComplete.WaitOne();
		}

		[OneTimeTearDown]
		public void CleanupWpfApp() {
			// Beende die WPF-Anwendung
			Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
			_uiThread.Join();
			_initComplete.Dispose();
		}

	}

}
