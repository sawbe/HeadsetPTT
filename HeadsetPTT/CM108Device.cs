using HeadsetPTT.Properties;
using HidSharp;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeadsetPTT
{
    public class CM108Device
    {
        private const int GPIO_STATE = 1;
        private const int GPIO_INDEX = 3;
        private const int GPIO_RETURN_INDEX = 2;
        private static byte[] gpio = new byte[5] { 0, 0, 1 << (GPIO_INDEX - 1), GPIO_STATE << (GPIO_INDEX - 1), 0 };

        public string Path => device.DevicePath;
        public DeviceState DeviceState => state;
        public PushState PushState { get; private set; } = PushState.Up;

        private readonly HidDevice device;
        private readonly byte[] data;
        private HidStream? stream;
        private DeviceState state;
        private IAsyncResult? readResult;
        private readonly ViGEmClient vigemClient;
        private readonly IXbox360Controller gamepad;
        private DateTime upReleaseTime = DateTime.MaxValue;

        public CM108Device(HidDevice device)
        {
            this.device = device; 
            state = DeviceState.Connecting;
            data = new byte[device.GetMaxInputReportLength()];
            vigemClient = new ViGEmClient();
            gamepad = vigemClient.CreateXbox360Controller();
        }

        /// <summary>
        /// Try open and read CM108 GPIO
        /// </summary>
        /// <returns>False if device is failed</returns>
        public bool TryUpdate()
        {
            switch(state)
            {
                case DeviceState.Connecting:
                    if(device.TryOpen(out stream))
                    {
                        state = DeviceState.Connected;
                        stream.ReadTimeout = Timeout.Infinite;

                        stream.Write(gpio);//Set GPIO

                        gamepad.Connect();
                        return true;
                    }
                    return false;
                case DeviceState.Connected:
                    if (stream is null)
                        return false;

                    if(readResult == null)//begin read
                    {
                        try
                        {
                            readResult = stream.BeginRead(data, 0, data.Length, null, null);
                        }
                        catch(ObjectDisposedException)
                        {
                            state = DeviceState.Closed;
                            gamepad.Disconnect();
                            return false;
                        }
                    }
                    else
                    {
                        if(readResult.IsCompleted)
                        {
                            try
                            {
                                if(stream.EndRead(readResult) > 0)
                                {
                                    ProcessGPIO();
                                }
                            }
                            catch(IOException)
                            {
                                state = DeviceState.Closed;
                                gamepad.Disconnect();
                                return false;
                            }

                            readResult = null;
                        }

                        CheckUpRelease();
                    }
                    return true;
                default:
                    gamepad.Disconnect();
                    return false;
            }
        }

        private void ProcessGPIO()
        {
            if (data[GPIO_RETURN_INDEX] == 0)//GPIO DOWN
            {
                PushState = PushState.Down;
                if (User.Default.downPtt > (int)Xbox360ButtonName.None)
                    gamepad.SetButtonState(User.Default.downPtt, true);
            }
            else
            {
                PushState = PushState.Up;
                if (User.Default.downPtt > (int)Xbox360ButtonName.None)
                    gamepad.SetButtonState(User.Default.downPtt, false);
                if (User.Default.upPtt > (int)Xbox360ButtonName.None)
                    gamepad.SetButtonState(User.Default.upPtt, true);
                upReleaseTime = DateTime.Now + TimeSpan.FromMilliseconds(200);
            }
        }

        private void CheckUpRelease()
        {
            if (DateTime.Now > upReleaseTime)
            {
                upReleaseTime = DateTime.MaxValue;
                if (User.Default.upPtt > (int)Xbox360ButtonName.None)
                    gamepad.SetButtonState(User.Default.upPtt, false);
            }
        }
    }
}
