using System.Configuration.Install;
using System.ServiceProcess;
using System.ComponentModel;

namespace MipadService
{
	[RunInstaller(true)]
	public class MiServiceInstaller : Installer
	{
		private const string ServiceName = MipadService.ServiceShortName;
		private const string DisplayName = "Xiaomi Gamepad Service";

		public MiServiceInstaller()
		{
			var processInstaller = new ServiceProcessInstaller();
			var serviceInstaller = new ServiceInstaller();

			processInstaller.Account = ServiceAccount.LocalSystem;
			serviceInstaller.StartType = ServiceStartMode.Automatic;
			serviceInstaller.ServiceName = ServiceName;
			serviceInstaller.DisplayName = DisplayName;

			Installers.Add(serviceInstaller);
			Installers.Add(processInstaller);

			Committed += MiServiceInstaller_Committed;
		}

		private static void MiServiceInstaller_Committed(object sender, InstallEventArgs e)
		{
			// Auto Start the Service Once Installation is Finished.
			var controller = new ServiceController(ServiceName);
			controller.Start();
		}
	}
}