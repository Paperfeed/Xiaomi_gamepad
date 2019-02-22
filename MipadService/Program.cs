using System;
using System.Collections;
using System.Configuration.Install;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.ServiceProcess;
using System.Windows.Forms;

namespace MipadService
{
    internal static class Program
    {
        private static string ExecutablePath;
        
        private static void Main()
        {
            if (Environment.UserInteractive)
            {
                DialogResult result;
                var directoryPath = Environment.Is64BitOperatingSystem
                    ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                    : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var filePath = Path.Combine(directoryPath, MipadService.ServiceShortName);
                ExecutablePath = Path.Combine(filePath, "MipadService.exe");
                
                if (!IsInstalled())
                {
                    // Install service if it isn't installed already
                    result = MessageBox.Show("Press yes to install MiX360 as a service. This will allow your Xiaomi gamepad to function as an Xbox controller without having to start a program. To uninstall simply run this executable again.",
                        "Do you want to install MiX360?",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result != DialogResult.Yes) return;

                    if (CheckAuthorization(directoryPath))
                    {
                        Directory.CreateDirectory(filePath);
                        
                        File.Copy(Assembly.GetEntryAssembly().Location, ExecutablePath,true);
                        
                        InstallService();
                    }
                    else
                    {
                        MessageBox.Show("Something went wrong with the installation of the service. Try running as an administrator?",
                            "Error installing MiX360",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
                else
                {
                    // Uninstall service if it exists
                    result = MessageBox.Show("Press yes to uninstall the MiX360 service.",
                        "Do you want to uninstall MiX360?",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result != DialogResult.Yes) return;

                    if (CheckAuthorization(directoryPath))
                    {
                        Directory.Delete(filePath);
                    }

                    StopService();
                    UninstallService();
                }
            }
            else
            {
                // Executable wasn't run by user, so start the service instead
                ServiceBase.Run(new MipadService());
            }
        }

        
        private static bool CheckAuthorization(string directory)
        {
            try
            {
                var collection = Directory.GetAccessControl(directory)
                    .GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount));

                foreach (FileSystemAccessRule rule in collection)
                {
                    if (rule.AccessControlType == AccessControlType.Allow) break;
                }
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }

        
        private static bool IsInstalled()
        {
            using (var controller = new ServiceController(MipadService.ServiceShortName))
            {
                try
                {
                    ServiceControllerStatus status = controller.Status;
                }
                catch
                {
                    return false;
                }

                return true;
            }
        }

        
        private static bool IsRunning()
        {
            using (var controller = new ServiceController(MipadService.ServiceShortName))
            {
                if (!IsInstalled()) return false;
                return (controller.Status == ServiceControllerStatus.Running);
            }
        }

        
        private static AssemblyInstaller GetInstaller()
        {
//            var installer = new AssemblyInstaller(typeof(MipadService).Assembly, null);
            var installer = new AssemblyInstaller(ExecutablePath, null);
            installer.UseNewContext = true;
            
            return installer;
        }

        
        private static void InstallService()
        {
            if (IsInstalled()) return;

            using (var installer = GetInstaller())
            {
                IDictionary state = new Hashtable();
                try
                {
                    installer.Install(state);
                    installer.Commit(state);
                }
                catch
                {
                    installer.Rollback(state);
                }
            }
        }

        
        private static void UninstallService()
        {
            if (!IsInstalled()) return;

            using (var installer = GetInstaller())
            {
                IDictionary state = new Hashtable();
                installer.Uninstall(state);
            }
        }

        
        private static void StartService()
        {
            if (!IsInstalled()) return;

            using (var controller = new ServiceController(MipadService.ServiceShortName))
            {
                if (controller.Status == ServiceControllerStatus.Running) return;

                controller.Start();
                controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
            }
        }

        private static void StopService()
        {
            if (!IsInstalled()) return;
            
            using (var controller = new ServiceController(MipadService.ServiceShortName))
            {
                if (controller.Status == ServiceControllerStatus.Stopped) return;

                controller.Stop();
                controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
            }
        }
        
        
    }
}