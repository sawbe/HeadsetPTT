using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeadsetPTT
{
    public enum DeviceState
    {
        Closed = -1,
        Searching,
        Connecting,
        Connected,        
    }

    public enum PushState
    {
        Up,
        Down,
    }

    public enum Xbox360ButtonName
    { 
        None = -1,
        Up,
        Down,
        Left,
        Right,
        Start,
        Back,
        LeftThumb,
        RightThumb,
        LeftShoulder,
        RightShoulder,
        Guide,
        A,
        B,
        X,
        Y
    }

}
