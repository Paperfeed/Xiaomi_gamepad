using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using HidLibrary;
using ScpDriverInterface;
using NativeMethods = HidLibrary.NativeMethods;
using Timer = System.Timers.Timer;


namespace MipadService
{
    public class MipadService : ServiceBase
    {
        public const string ServiceShortName = "MiX360";
        private static readonly ScpBus GlobalScpBus = new ScpBus();

        private static readonly Dictionary<string, XiaomiGamepad> MappedDevices =
            new Dictionary<string, XiaomiGamepad>();

        private int lastResults;
        private Timer timer;

        public MipadService()
        {
            ServiceName = ServiceShortName;
            CanStop = true;
            CanPauseAndContinue = true;
            AutoLog = false;
        }

        protected override void OnStart(string[] args)
        {
            //Debugger.Launch();
            base.OnStart(args);

            timer = new Timer {Interval = 5000};
            timer.Elapsed += OnTimer;
            timer.Start();
        }

        protected override void OnPause()
        {
            timer.Enabled = false;
        }

        protected override void OnContinue()
        {
            timer.Enabled = true;
        }

        protected override void OnStop()
        {
            base.OnStop();
            timer.Dispose();
        }

        private void OnTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            var result = SearchDevice();

            if (result <= 0 || lastResults == result) return;

            lastResults = result;

            //String text = result + " device(s) connected";
            //EventLog.WriteEntry(text, EventLogEntryType.Information);
        }

        private static int SearchDevice()
        {
            var compatibleDevices = HidDevices.Enumerate(0x2717, 0x3144).ToList();
            var scpBus = GlobalScpBus;
            var alreadyMapped = MappedDevices;

            foreach (var deviceInstance in compatibleDevices)
            {
                var device = deviceInstance;

                if (alreadyMapped.ContainsKey(device.DevicePath)) continue;

                try
                {
                    device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.Exclusive);
                }
                catch
                {
                    var instanceId = DevicePathToInstanceId(deviceInstance.DevicePath);
                    if (TryReEnableDevice(instanceId))
                    {
                        try
                        {
                            device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.Exclusive);
                        }
                        catch
                        {
                            device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped,
                                ShareMode.ShareRead | ShareMode.ShareWrite);
                        }
                    }
                    else
                    {
                        device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped,
                            ShareMode.ShareRead | ShareMode.ShareWrite);
                    }
                }

                byte[] vibration = {0x20, 0x00, 0x00};
                if (device.WriteFeatureData(vibration) == false)
                {
                    device.CloseDevice();
                    continue;
                }

                byte[] serialNumber;
                byte[] product;
                device.ReadSerialNumber(out serialNumber);
                device.ReadProduct(out product);

                var index = MappedDevices.Count + 1;
                var gamepad = new XiaomiGamepad(device, scpBus, index);

                MappedDevices.Add(device.DevicePath, gamepad);
            }

            return MappedDevices.Count;
        }


        private static string DevicePathToInstanceId(string devicePath)
        {
            var deviceInstanceId = devicePath;

            deviceInstanceId = deviceInstanceId.Remove(0, deviceInstanceId.LastIndexOf('\\') + 1);
            deviceInstanceId = deviceInstanceId.Remove(deviceInstanceId.LastIndexOf('{'));
            deviceInstanceId = deviceInstanceId.Replace('#', '\\');

            if (deviceInstanceId.EndsWith("\\"))
            {
                deviceInstanceId = deviceInstanceId.Remove(deviceInstanceId.Length - 1);
            }

            return deviceInstanceId;
        }

        private static bool TryReEnableDevice(string deviceInstanceId)
        {
            try
            {
                var hidGuid = new Guid();
                NativeMethods.HidD_GetHidGuid(ref hidGuid);

                var deviceInfoSet = NativeMethods.SetupDiGetClassDevs(ref hidGuid, deviceInstanceId, 0,
                    NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);

                var deviceInfoData = new NativeMethods.SP_DEVINFO_DATA();
                deviceInfoData.cbSize = Marshal.SizeOf(deviceInfoData);

                var success = NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, 0, ref deviceInfoData);
                success = NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, 1,
                    ref deviceInfoData); // Checks that we have a unique device

                var propChangeParams = new NativeMethods.SP_PROPCHANGE_PARAMS();
                propChangeParams.classInstallHeader.cbSize = Marshal.SizeOf(propChangeParams.classInstallHeader);
                propChangeParams.classInstallHeader.installFunction = NativeMethods.DIF_PROPERTYCHANGE;
                propChangeParams.stateChange = NativeMethods.DICS_DISABLE;
                propChangeParams.scope = NativeMethods.DICS_FLAG_GLOBAL;
                propChangeParams.hwProfile = 0;

                success = NativeMethods.SetupDiSetClassInstallParams(deviceInfoSet, ref deviceInfoData,
                    ref propChangeParams, Marshal.SizeOf(propChangeParams));

                if (!success) return false;

                success = NativeMethods.SetupDiCallClassInstaller(NativeMethods.DIF_PROPERTYCHANGE, deviceInfoSet,
                    ref deviceInfoData);

                if (!success) return false;

                propChangeParams.stateChange = NativeMethods.DICS_ENABLE;
                success = NativeMethods.SetupDiSetClassInstallParams(deviceInfoSet, ref deviceInfoData,
                    ref propChangeParams, Marshal.SizeOf(propChangeParams));

                if (!success) return false;

                success = NativeMethods.SetupDiCallClassInstaller(NativeMethods.DIF_PROPERTYCHANGE, deviceInfoSet,
                    ref deviceInfoData);

                if (!success) return false;

                NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}