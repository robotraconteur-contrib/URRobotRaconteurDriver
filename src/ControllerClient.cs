using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UR.Package;
using System.Linq;

namespace UR.ControllerClient
{
    public enum PackageType
    {
        ROBOT_MODE_DATA = 0,
        JOINT_DATA = 1,
        TOOL_DATA = 2,
        MASTERBOARD_DATA = 3,
        CARTESIAN_INFO = 4,
        KINEMATICS_INFO = 5,
        CONFIGURATION_DATA = 6,
        FORCE_MODE_DATA = 7,
        ADDITIONAL_INFO = 8,
        CALIBRATION_DATA = 9
    }

    public enum RobotMode
    {
        RUNNING = 0,
        FREEDRIVE = 1,
        READY = 2,
        INITIALIZING = 3,
        SECURITY_STOPPED = 4,
        EMERGENCY_STOPPED = 5,
        FATAL_ERROR = 6,
        NO_POWER = 7,
        NOT_CONNECTED = 8,
        SHUTDOWN = 9,
        SAFEGUARD_STOP = 10
    }

    public class RobotModeData_V18
    {
        public ulong timestamp;
        public bool robot_connected;
        public bool real_robot_enabled;
        public bool power_on_robot;
        public bool emergency_stopped;
        public bool security_stopped;
        public bool program_running;
        public bool program_paused;
        public byte robot_mode;
        public double speed_fraction;

        public void Read(PackageReader r)
        {
            if (r.PackageRemaining != 25)
                throw new IOException("Invalid RobotModeData length");
            if (r.ReadByte() != (byte)PackageType.ROBOT_MODE_DATA)
                throw new IOException("Invalid RobotModeData package type");
            timestamp = r.ReadUInt64();
            robot_connected = r.ReadBool();
            real_robot_enabled = r.ReadBool();
            power_on_robot = r.ReadBool();
            emergency_stopped = r.ReadBool();
            security_stopped = r.ReadBool();
            program_running = r.ReadBool();
            program_paused = r.ReadBool();
            robot_mode = r.ReadByte();
            speed_fraction = r.ReadDouble();
        }
    }

    public class MasterboardData_V18
    {
        public ushort digitalInputBits;
        public ushort digitalOutputBits;
        public byte analogInputRange0;
        public byte analogInputRange1;
        public double analogInput0;
        public double analogInput1;
        public byte analogOutputDomain0;
        public byte analogOutputDomain1;
        public double analogOutput0;
        public double analogOutput1;
        public float masterBoardTemperature;
        public float robotVoltage48V;
        public float robotCurrent;
        public float masterIOCurrent;
        public byte masterSafetyState;
        public byte masterOnOffState;
        public byte euromap67InterfaceInstalled;

        public void Read(PackageReader r)
        {
            if (r.PackageRemaining != 60)
                throw new IOException("Invalid RobotModeData length");
            if (r.ReadByte() != (byte)PackageType.MASTERBOARD_DATA)
                throw new IOException("Invalid MasterboardData package type");

            digitalInputBits = r.ReadUInt16();
            digitalOutputBits = r.ReadUInt16();
            analogInputRange0 = r.ReadByte();
            analogInputRange1 = r.ReadByte();
            analogInput0 = r.ReadDouble();
            analogInput1 = r.ReadDouble();
            analogOutputDomain0 = r.ReadByte();
            analogOutputDomain1 = r.ReadByte();
            analogOutput0 = r.ReadDouble();
            analogOutput1 = r.ReadDouble();
            masterBoardTemperature = r.ReadSingle();
            robotVoltage48V = r.ReadSingle();
            robotCurrent = r.ReadSingle();
            masterIOCurrent = r.ReadSingle();
            masterSafetyState = r.ReadByte();
            masterOnOffState = r.ReadByte();
            euromap67InterfaceInstalled = r.ReadByte();

        }
    }

    public class ToolData_V18
    {
        public byte analogInputRange2;
        public byte analogInputRange3;
        public double analogInput2;
        public double analogInput3;
        public float toolVoltage48V;
        public byte toolOutputVoltage;
        public float toolCurrent;
        public float toolTemperature;
        public byte toolMode;

        public void Read(PackageReader r)
        {
            if (r.PackageRemaining != 33)
                throw new IOException("Invalid ToolData length");
            if (r.ReadByte() != (byte)PackageType.TOOL_DATA)
                throw new IOException("Invalid ToolData package type");

            analogInputRange2 = r.ReadByte();
            analogInputRange3 = r.ReadByte();
            analogInput2 = r.ReadDouble();
            analogInput3 = r.ReadDouble();
            toolVoltage48V = r.ReadSingle();
            toolOutputVoltage = r.ReadByte();
            toolCurrent = r.ReadSingle();
            toolTemperature = r.ReadSingle();
            toolMode = r.ReadByte();
        }
    }

    public class RobotState
    {
        public RobotModeData_V18 robot_mode_data = new RobotModeData_V18();
        public MasterboardData_V18 master_board_data = new MasterboardData_V18();
        public ToolData_V18 tool_data = new ToolData_V18();

        public void Read(PackageReader r)
        {
            r.Skip(1);
            while (r.PackageRemaining > 5)
            {
                var r2 = r.SubReader(out var package_type);
                if (package_type == (byte)PackageType.ROBOT_MODE_DATA)
                {
                    robot_mode_data.Read(r2);
                }
                if (package_type == (byte)PackageType.MASTERBOARD_DATA)
                {
                    master_board_data.Read(r2);
                }
                if (package_type == (byte)PackageType.TOOL_DATA)
                {
                    tool_data.Read(r2);
                }
            }

        }

    }



    public class ControllerClient : IDisposable
    {
        bool keep_going;
        string hostname;
        int port;

        public RobotState robot_state = new RobotState();


        Thread thread;

        public void Start(string robot_hostname, int robot_port = 30002)
        {
            hostname = robot_hostname;
            port = robot_port;
            keep_going = true;

            thread = new Thread(_run);
            thread.Start();
        }

        public void Dispose()
        {
            keep_going = false;
        }

        public bool Connected { get; private set; }

        public Exception LastException { get; private set; }

        public void _run()
        {
            while (keep_going)
            {
                try
                {
                    using (var socket = new TcpClient(hostname, port))
                    {
                        var net_stream = socket.GetStream();

                        Connected = true;
                        while (keep_going)
                        {
                            DoReceiveRobotState(net_stream);
                        }
                        return;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Robot communication error: {e.ToString()}");
                    LastException = e;
                }
                finally
                {
                    Connected = false;
                }

                for (int i = 0; i < 10; i++)
                {
                    if (!keep_going)
                        break;
                    Thread.Sleep(100);
                }

                Console.WriteLine($"Retrying robot connection!");

            }
        }

        void DoReceiveRobotState(NetworkStream s)
        {
            var res_reader = RecvPackage(s, out var package_type);
            if (package_type == 16)
            {
                lock (this)
                {
                    robot_state.Read(res_reader);
                }
            }
        }


        byte[] recv_buf = new byte[4096];
        byte[] recv_temp = new byte[8];
        PackageReader package_reader = new PackageReader();
        PackageReader RecvPackage(NetworkStream s, out byte package_type)
        {
            int pos = 0;
            do
            {
                var l = s.Read(recv_buf, pos, 4 - pos);
                if (l == 0) throw new IOException("Connection closed");
                pos += l;
            }
            while (pos < 2);

            recv_temp[0] = recv_buf[3];
            recv_temp[1] = recv_buf[2];
            recv_temp[2] = recv_buf[1];
            recv_temp[3] = recv_buf[0];
            int len = BitConverter.ToInt32(recv_temp, 0);

            if (len < 5)
            {
                throw new IOException("Invalid package length");
            }

            do
            {
                var l = s.Read(recv_buf, pos, len - pos);
                if (l == 0) throw new IOException("Connection closed");
                pos += l;
            }
            while (pos < len);

            package_type = recv_buf[4];

            return package_reader.BeginController(new ArraySegment<byte>(recv_buf, 0, len));


        }



    }



}
