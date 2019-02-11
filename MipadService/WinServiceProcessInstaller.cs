using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace MipadService
{
    [RunInstaller(true)]
    public class WinServiceProcessInstaller : Installer
    {
        private static string _serviceDisplayName = $"{MipadService.Servicename} Display Name";
        private static string _serviceName = MipadService.Servicename;

        public static void InitInstaller(string serviceDisplayName, string serviceName)
        {
            _serviceDisplayName = serviceDisplayName;
            _serviceName = serviceName;
        }

        public WinServiceProcessInstaller()
        {
            var serviceProcessInstaller = new ServiceProcessInstaller
            {
                Account = ServiceAccount.LocalSystem,
                Password = null,
                Username = null
            };

            var serviceInstaller = new ServiceInstaller
            {
                DisplayName = _serviceDisplayName,
                ServiceName = _serviceName,
                StartType = ServiceStartMode.Automatic
            };

            Installers.Add(serviceProcessInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}