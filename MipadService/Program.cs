using System;

namespace MipadService
{
    internal class Program
    {
        private static void Main()
        {
            Console.WriteLine(@"You can not run a Windows Service from the commandline. "
                              + @"Use InstallUtil.exe from the .net framework to install the service.");

#if DEBUG
            Thread.Sleep(10000);
#endif

            System.ServiceProcess.ServiceBase.Run(new MipadService());
        }
    }
}