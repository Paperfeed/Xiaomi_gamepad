using System.ServiceProcess;

namespace MipadService
{
	internal static class Program
    {
	    private static void Main()
        {
	        var servicesToRun = new ServiceBase[]
	        {
		        new MipadService()
	        };

	        ServiceBase.Run(servicesToRun);
        }
    }
}