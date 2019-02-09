using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using HidLibrary;
using ScpDriverInterface;
using System.Threading;
using System.Runtime.InteropServices;

namespace mi
{
    public class XiaomiGamepad
    {
        private byte[] Vibration { get; } = {0x20, 0x00, 0x00};
        private Mutex RumbleMutex { get; } = new Mutex();
        private MainForm Form { get; }
        private byte BatteryLevel { get; set; }

        public XiaomiGamepad(HidDevice device, ScpBus scpBus, int index, MainForm form)
        {
            Form = form;
            
            device.WriteFeatureData(Vibration);
            
            Thread rThread = new Thread(() => rumble_thread(device));
            // rThread.Priority = ThreadPriority.BelowNormal; 
            rThread.IsBackground = true;
            rThread.Start();

            Thread iThread = new Thread(() => input_thread(device, scpBus, index));
            iThread.Priority = ThreadPriority.Highest;
            iThread.IsBackground = true;
            iThread.Start();
        }

        private void rumble_thread(HidDevice device)
        {
            byte[] localVibration = { 0x20, 0x00, 0x00 };
            while (true)
            {
                RumbleMutex.WaitOne();
                if (localVibration[2] != Vibration[2] || Vibration[1] != localVibration[1])
                {
                    localVibration[2] = Vibration[2];
                    localVibration[1] = Vibration[1];
                    RumbleMutex.ReleaseMutex();
                    device.WriteFeatureData(localVibration);
                    //Console.WriteLine("Big Motor: {0}, Small Motor: {1}", Vibration[2], Vibration[1]);
                }
                else
                {
                    RumbleMutex.ReleaseMutex();
                }
                Thread.Sleep(20);
            }
        }

        private void input_thread(HidDevice device, ScpBus scpBus, int index)
        {
            scpBus.PlugIn(index);
            X360Controller controller = new X360Controller();
            int timeout = 30;
            long lastChanged = 0;
            long lastMiButton = 0;
            
            while (true)
            {
                HidDeviceData data = device.Read(timeout);
                var currentState = data.Data;
                bool changed = false;
                if (data.Status == HidDeviceData.ReadStatus.Success && currentState.Length >= 21 && currentState[0] == 4)
                {
                    Debug.WriteLine(Program.ByteArrayToHexString(currentState));
                    
                    X360Buttons buttons = X360Buttons.None;
                    if ((currentState[1] & 1) != 0) buttons |= X360Buttons.A;
                    if ((currentState[1] & 2) != 0) buttons |= X360Buttons.B;
                    if ((currentState[1] & 8) != 0) buttons |= X360Buttons.X;
                    if ((currentState[1] & 16) != 0) buttons |= X360Buttons.Y;
                    if ((currentState[1] & 64) != 0) buttons |= X360Buttons.LeftBumper;
                    if ((currentState[1] & 128) != 0) buttons |= X360Buttons.RightBumper;

                    if ((currentState[2] & 32) != 0) buttons |= X360Buttons.LeftStick;
                    if ((currentState[2] & 64) != 0) buttons |= X360Buttons.RightStick;

                    if (currentState[4] != 15)
                    {
                        if (currentState[4] == 0 || currentState[4] == 1 || currentState[4] == 7) buttons |= X360Buttons.Up;
                        if (currentState[4] == 4 || currentState[4] == 3 || currentState[4] == 5) buttons |= X360Buttons.Down;
                        if (currentState[4] == 6 || currentState[4] == 5 || currentState[4] == 7) buttons |= X360Buttons.Left;
                        if (currentState[4] == 2 || currentState[4] == 1 || currentState[4] == 3) buttons |= X360Buttons.Right;
                    }

                    if ((currentState[2] & 8) != 0) buttons |= X360Buttons.Start;
                    if ((currentState[2] & 4) != 0) buttons |= X360Buttons.Back;

                    if (currentState[19] != 0 && BatteryLevel != currentState[19])
                    {
                        BatteryLevel = currentState[19];
                        Form.CreateTextIcon(BatteryLevel.ToString());
                    }

                    if ((currentState[20] & 1) != 0)
                    {
                        lastMiButton = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);
                        buttons |= X360Buttons.Logo;
                    }
                    if (lastMiButton != 0) buttons |= X360Buttons.Logo;


                    if (controller.Buttons != buttons)
                    {
                        changed = true;
                        controller.Buttons = buttons;
                    }

                    short leftStickX = (short)((Math.Max(-127.0, currentState[5] - 128) / 127) * 32767);
                    if (leftStickX == -32767)
                        leftStickX = -32768;

                    if (leftStickX != controller.LeftStickX)
                    {
                        changed = true;
                        controller.LeftStickX = leftStickX;
                    }

                    short leftStickY = (short)((Math.Max(-127.0, currentState[6] - 128) / 127) * -32767);
                    if (leftStickY == -32767)
                        leftStickY = -32768;

                    if (leftStickY != controller.LeftStickY)
                    {
                        changed = true;
                        controller.LeftStickY = leftStickY;
                    }

                    short rightStickX = (short)((Math.Max(-127.0, currentState[7] - 128) / 127) * 32767);
                    if (rightStickX == -32767)
                        rightStickX = -32768;

                    if (rightStickX != controller.RightStickX)
                    {
                        changed = true;
                        controller.RightStickX = rightStickX;
                    }

                    short rightStickY = (short)((Math.Max(-127.0, currentState[8] - 128) / 127) * -32767);
                    if (rightStickY == -32767)
                        rightStickY = -32768;

                    if (rightStickY != controller.RightStickY)
                    {
                        changed = true;
                        controller.RightStickY = rightStickY;
                    }

                    if (controller.LeftTrigger != currentState[11])
                    {
                        changed = true;
                        controller.LeftTrigger = currentState[11];
                    }

                    if (controller.RightTrigger != currentState[12])
                    {
                        changed = true;
                        controller.RightTrigger = currentState[12];

                    }
                }

                if (data.Status == HidDeviceData.ReadStatus.WaitTimedOut || (!changed && ((lastChanged + timeout) < (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond))))
                {
                    changed = true;
                }

                if (changed)
                {
                    //Debug.WriteLine("changed");
                    //Debug.WriteLine((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond));
                    
                    byte[] outputReport = new byte[8];
                    scpBus.Report(index, controller.GetReport(), outputReport);

                    if (outputReport[1] == 0x08)
                    {
                        byte bigMotor = outputReport[3];
                        byte smallMotor = outputReport[4];
                        RumbleMutex.WaitOne();
                        if (bigMotor != Vibration[2] || Vibration[1] != smallMotor)
                        {
                            Vibration[1] = smallMotor;
                            Vibration[2] = bigMotor;
                        }
                        RumbleMutex.ReleaseMutex();
                    }

                    if (lastMiButton != 0)
                    {
                        if ((lastMiButton + 100) < (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond))
                        {
                            lastMiButton = 0;
                            controller.Buttons ^= X360Buttons.Logo;
                        }
                    }

                    lastChanged = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                }
            }
        }
    }

    static class Program
    {
        public static string ByteArrayToHexString(byte[] bytes)
        {
            return string.Join(string.Empty, Array.ConvertAll(bytes, b => b.ToString("X2")));
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

       
    }
}
