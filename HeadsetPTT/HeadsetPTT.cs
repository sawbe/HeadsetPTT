using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HeadsetPTT.Properties;
using HidSharp;
using SimWinInput;
using static System.Windows.Forms.AxHost;

namespace HeadsetPTT
{
    public class HeadsetPTT : ApplicationContext
    { 

        public const int CM108_VID = 0x0D8C;
        public const int CM108_PID_MIN = 0x0008;
        public const int CM108_PID_MAX = 0x000F;
        public const int MAX_DEVICE_COUNT = 4;

        private readonly CM108Device?[] devices = new CM108Device?[MAX_DEVICE_COUNT];
        private readonly CancellationTokenSource cancellationToken;
        private readonly Thread deviceThread;
        private bool devicesChanged = true;
        private DeviceState connectionState = DeviceState.Searching;
        private bool pttDown;
        private int deviceCount = 0;

        private readonly ContextMenuStrip trayMenu;
        private readonly ToolStripMenuItem trayItemStatus;
        private readonly ToolStripMenuItem trayItemSettings;
        private readonly ToolStripMenuItem trayItemExit;
        private readonly NotifyIcon trayIcon;
        private readonly NotifyIcon trayIconConnected;
        private readonly NotifyIcon trayIconPtt;
        private readonly SettingsForm settingsForm;

        public HeadsetPTT()
        {
            Application.ApplicationExit += Application_ApplicationExit;

            settingsForm = new SettingsForm();
            _ = settingsForm.Handle;//this triggers the form to be created, allowing me to invoke without displaying form

            trayMenu = new ContextMenuStrip();
            trayItemStatus = new ToolStripMenuItem("status") { Enabled = false };
            trayItemSettings = new ToolStripMenuItem("Settings");
            trayItemExit = new ToolStripMenuItem("Exit");

            trayMenu.Items.Add(trayItemStatus);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(trayItemSettings);
            trayMenu.Items.Add(trayItemExit);

            trayIcon = new NotifyIcon()
            {
                Icon = Properties.Resources.headset,
                Text = "HeadsetPTT",
                BalloonTipText = "CM108 Headset Push To Talk",
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            trayIconConnected = new NotifyIcon()
            {
                Icon = Properties.Resources.headset_green,
                Text = trayIcon.Text,
                BalloonTipText = trayIcon.BalloonTipText,
                ContextMenuStrip = trayMenu,
                Visible = false
            };

            trayIconPtt = new NotifyIcon()
            {
                Icon = Properties.Resources.headset_ptt,
                Text = trayIcon.Text,
                BalloonTipText = trayIcon.BalloonTipText,
                ContextMenuStrip = trayMenu,
                Visible = false
            };

            SimGamePad.Instance.Initialize();

            cancellationToken = new CancellationTokenSource();
            deviceThread = new Thread(DeviceLoop) { IsBackground = true, Name = "HeadsetPTT Devices" };
            deviceThread.Start();

            trayMenu.ItemClicked += TrayMenu_ItemClicked;
            trayIcon.DoubleClick += TrayIcon_DoubleClick;
            trayIconConnected.DoubleClick += TrayIcon_DoubleClick;
            trayIconPtt.DoubleClick += TrayIcon_DoubleClick;
            DeviceList.Local.Changed += DeviceList_Local_Changed;

            if(User.Default.firstRun)
            {
                User.Default.firstRun = false;
                settingsForm.Show();
            }
        }

        private void Application_ApplicationExit(object? sender, EventArgs e)
        {
            cancellationToken.Cancel();
            deviceThread.Join(1000);
            SimGamePad.Instance.ShutDown();
        }

        private void DeviceLoop()
        {
            while(!cancellationToken.IsCancellationRequested)
            {
                for(int i = 0; i<MAX_DEVICE_COUNT; i++)
                {
                    if(devices[i]?.TryUpdate() == false)
                    {
                        //update failed, remove device
                        devices[i] = null;
                    }
                }

                if(devicesChanged)//check for new devices
                {
                    devicesChanged = false;
                    foreach(var dev in DeviceList.Local.GetHidDevices(vendorID: CM108_VID))
                    {
                        if (dev.ProductID < CM108_PID_MIN || dev.ProductID > CM108_PID_MAX)
                            continue;

                        if(!devices.Any(d=>d?.Path == dev.DevicePath))
                        {
                            for(int i = 0; i<MAX_DEVICE_COUNT; i++)
                            {
                                if (devices[i] == null)
                                {
                                    devices[i] = new CM108Device(dev, i);
                                    break;
                                }
                            }
                        }
                    }
                }

                DeviceState currDevState = DeviceState.Searching;
                bool currPttDown = false;
                int count = 0;
                for(int i = 0; i< MAX_DEVICE_COUNT; i++)
                {
                    var dev = devices[i];
                    if (dev == null)
                        continue;

                    count++;
                    if(dev.DeviceState > currDevState)
                        currDevState = dev.DeviceState;
                    if (dev.PushState == PushState.Down)
                        currPttDown = true;
                }

                if(connectionState != currDevState || count != deviceCount)
                {
                    settingsForm.UpdateConnectionState(currDevState, count);
                    connectionState = currDevState;
                    deviceCount = count;
                    InvokeUpdateTrayIcon();
                    InvokeUpdateTrayMenuState();
                }
                if(currPttDown != pttDown)
                {
                    pttDown = currPttDown;
                    InvokeUpdateTrayIcon();
                }    

                try
                {
                    cancellationToken.Token.WaitHandle.WaitOne(10);
                }
                catch { }
            }
        }

        private void InvokeUpdateTrayMenuState()
        {
            settingsForm.Invoke(new Action(() => UpdateTrayMenuState()));
        }

        private void UpdateTrayMenuState()
        {
            trayItemStatus.Text = connectionState.ToString();
        }

        private void InvokeUpdateTrayIcon()
        {
            settingsForm.Invoke(new Action(() => UpdateTrayIcon()));
        }

        private void UpdateTrayIcon()
        {
            if (pttDown)
            {
                trayIconPtt.Visible = true;
                trayIcon.Visible = false;
                trayIconConnected.Visible = false;
            }
            else
            {
                if (connectionState > DeviceState.Searching)
                {
                    trayIconPtt.Visible = false;
                    trayIcon.Visible = false;
                    trayIconConnected.Visible = true;
                }
                else
                {
                    trayIconPtt.Visible = false;
                    trayIcon.Visible = true;
                    trayIconConnected.Visible = false;
                }
            }
        }

        private void TrayMenu_ItemClicked(object? sender, ToolStripItemClickedEventArgs e)
        {
            if(e.ClickedItem == trayItemSettings)
            {
                ShowHideSettings();
            }
            else if(e.ClickedItem == trayItemExit)
            {
                ExitThread();   
            }
        }

        private void TrayIcon_DoubleClick(object? sender, EventArgs e)
        {
            ShowHideSettings();
        }

        private void ShowHideSettings()
        {
            if (!settingsForm.Visible)
                settingsForm.Show();
            else
                settingsForm.Hide();
        }

        private void DeviceList_Local_Changed(object? sender, DeviceListChangedEventArgs e)
        {
            devicesChanged = true;
        }       
    }
}
