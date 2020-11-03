using com.robotraconteur.robotics.robot;
using RobotRaconteur.Companion.Robot;
using System;
using System.Collections.Generic;
using System.Text;

namespace URRobotRaconteurDriver
{
    interface IURRobot : Robot, IDisposable
    {
        void _start_robot();
    }
}
