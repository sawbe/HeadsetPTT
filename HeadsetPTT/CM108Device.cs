using HeadsetPTT.Properties;
using HidSharp;
using SimWinInput;
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
        private readonly int controllerIndex;
        private readonly byte[] data;
        private HidStream? stream;
        private DeviceState state;
        private IAsyncResult? readResult;

        public CM108Device(HidDevice device, int gamepadIndex)
        {
            this.device = device;
            this.controllerIndex = gamepadIndex;    
            state = DeviceState.Connecting;
            data = new byte[device.GetMaxInputReportLength()];
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

                        SimGamePad.Instance.PlugIn(controllerIndex);
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
                            SimGamePad.Instance.Unplug(controllerIndex);
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
                                SimGamePad.Instance.Unplug(controllerIndex);
                                return false;
                            }

                            readResult = null;
                        }
                    }
                    return true;
                default:
                    SimGamePad.Instance.Unplug(controllerIndex);
                    return false;
            }
        }

        private void ProcessGPIO()
        {
            if (data[GPIO_RETURN_INDEX] == 0)//GPIO DOWN
            {
                PushState = PushState.Down;
                SimGamePad.Instance.SetControl((GamePadControl)User.Default.downPtt, controllerIndex);
            }
            else
            {
                PushState = PushState.Up;
                SimGamePad.Instance.ReleaseControl((GamePadControl)User.Default.downPtt, controllerIndex);
                SimGamePad.Instance.Use((GamePadControl)User.Default.upPtt, controllerIndex);
            }
        }
    }
}
