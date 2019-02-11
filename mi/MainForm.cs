using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using HidLibrary;
using ScpDriverInterface;
using NativeMethods = HidLibrary.NativeMethods;

namespace mi
{
    public partial class MainForm : Form
    {
        private static ScpBus GlobalScpBus { get; set; }
        private static Dictionary<string, XiaomiGamepad> MappedDevices { get; set; }

        public MainForm()
        {
            InitializeComponent();
        }

        private void InvokeUI(Action a)
        {
            BeginInvoke(new MethodInvoker(a));
        }

        private void DetectDevices()
        {
            Int32 lastResults = 0;
            while (true)
            {
                Int32 result = SearchDevice();
                if (result > 0)
                {
                    if (lastResults != result)
                    {
                        String text = result + " device(s) connected";
                        lastResults = result;
                        InvokeUI(() =>
                        {
                            notifyIcon1.BalloonTipTitle = @"MiX360 Gamepad";
                            notifyIcon1.BalloonTipText = text;
                            notifyIcon1.ShowBalloonTip(500);
                        });
                    }
                }

                Thread.Sleep(5000);
            }
        }


        private Int32 SearchDevice()
        {
            var compatibleDevices = HidDevices.Enumerate(0x2717, 0x3144).ToList();
            ScpBus scpBus = GlobalScpBus;
            Dictionary<string, XiaomiGamepad> alreadyMapped = MappedDevices;

            //Debug.WriteLine(Device.DevicePath);
            foreach (var deviceInstance in compatibleDevices)
            {
                HidDevice device = deviceInstance;
                if (alreadyMapped.ContainsKey(device.DevicePath))
                {
                    continue;
                }

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

                Int32 index = MappedDevices.Count + 1;
                XiaomiGamepad gamepad = new XiaomiGamepad(device, scpBus, index, this);
                MappedDevices.Add(device.DevicePath, gamepad);
            }

            return MappedDevices.Count;
        }


        private static string DevicePathToInstanceId(string devicePath)
        {
            string deviceInstanceId = devicePath;
            deviceInstanceId = deviceInstanceId.Remove(0, deviceInstanceId.LastIndexOf('\\') + 1);
            deviceInstanceId = deviceInstanceId.Remove(deviceInstanceId.LastIndexOf('{'));
            deviceInstanceId = deviceInstanceId.Replace('#', '\\');
            if (deviceInstanceId.EndsWith("\\"))
            {
                deviceInstanceId = deviceInstanceId.Remove(deviceInstanceId.Length - 1);
            }

            return deviceInstanceId;
        }

        private bool TryReEnableDevice(string deviceInstanceId)
        {
            try
            {
                bool success;

                Guid hidGuid = new Guid();
                NativeMethods.HidD_GetHidGuid(ref hidGuid);
                IntPtr deviceInfoSet = NativeMethods.SetupDiGetClassDevs(ref hidGuid, deviceInstanceId, 0,
                    NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);
                NativeMethods.SP_DEVINFO_DATA deviceInfoData = new NativeMethods.SP_DEVINFO_DATA();
                deviceInfoData.cbSize = Marshal.SizeOf(deviceInfoData);
                success = NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, 0, ref deviceInfoData);
                success = NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, 1,
                    ref deviceInfoData); // Checks that we have a unique device

                NativeMethods.SP_PROPCHANGE_PARAMS propChangeParams = new NativeMethods.SP_PROPCHANGE_PARAMS();
                propChangeParams.classInstallHeader.cbSize = Marshal.SizeOf(propChangeParams.classInstallHeader);
                propChangeParams.classInstallHeader.installFunction = NativeMethods.DIF_PROPERTYCHANGE;
                propChangeParams.stateChange = NativeMethods.DICS_DISABLE;
                propChangeParams.scope = NativeMethods.DICS_FLAG_GLOBAL;
                propChangeParams.hwProfile = 0;
                success = NativeMethods.SetupDiSetClassInstallParams(deviceInfoSet, ref deviceInfoData,
                    ref propChangeParams, Marshal.SizeOf(propChangeParams));
                if (!success)
                {
                    return false;
                }

                success = NativeMethods.SetupDiCallClassInstaller(NativeMethods.DIF_PROPERTYCHANGE, deviceInfoSet,
                    ref deviceInfoData);
                if (!success)
                {
                    return false;
                }

                propChangeParams.stateChange = NativeMethods.DICS_ENABLE;
                success = NativeMethods.SetupDiSetClassInstallParams(deviceInfoSet, ref deviceInfoData,
                    ref propChangeParams, Marshal.SizeOf(propChangeParams));
                if (!success)
                {
                    return false;
                }

                success = NativeMethods.SetupDiCallClassInstaller(NativeMethods.DIF_PROPERTYCHANGE, deviceInfoSet,
                    ref deviceInfoData);
                if (!success)
                {
                    return false;
                }

                NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            GlobalScpBus.UnplugAll();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
//            Hide();

            var scpBus = new ScpBus();
            scpBus.UnplugAll();
            GlobalScpBus = scpBus;

            MappedDevices = new Dictionary<string, XiaomiGamepad>();

            var detectThread = new Thread(DetectDevices);
            detectThread.IsBackground = true;
            detectThread.Start();
        }

        private void mnuItemExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        public void CreateTextIcon(short batteryLevel)
        {
            ComponentResourceManager resources = new ComponentResourceManager(typeof(MainForm));
            Font font = new Font("Microsoft Sans Serif", 9, FontStyle.Regular, GraphicsUnit.Pixel);
            Bitmap layer = (Bitmap)(resources.GetObject("BatteryIcon"));
            Graphics g;

            layer.MakeTransparent();
            g = Graphics.FromImage(layer);
            Color batteryLevelColor;

            if      (batteryLevel > 80) { batteryLevelColor = Color.Lime; }
            else if (batteryLevel > 50) { batteryLevelColor = Color.GreenYellow; } 
            else if (batteryLevel > 40) { batteryLevelColor = Color.Gold; }
            else if (batteryLevel > 25) { batteryLevelColor = Color.Orange; }
            else if (batteryLevel > 15) { batteryLevelColor = Color.DarkOrange; }
            else if (batteryLevel > 5)  { batteryLevelColor = Color.OrangeRed; }
            else                        { batteryLevelColor = Color.Red; }

            var batteryString = batteryLevel.ToString();
            if (batteryLevel < 10) batteryString.PadLeft(2, '0');

            Brush brush = new SolidBrush(batteryLevelColor);

            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            g.DrawString(batteryString, font, brush, 2, 2);
            notifyIcon1.Icon = Icon.FromHandle(layer.GetHicon());
            //DestroyIcon(hIcon.ToInt32);
        }
    }
}