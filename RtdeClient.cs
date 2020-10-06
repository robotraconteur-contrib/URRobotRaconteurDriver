using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using UR.Package;

namespace UR.RTDE
{
    public enum RtdePackageType
    {
        RTDE_REQUEST_PROTOCOL_VERSION = 86,
        RTDE_GET_URCONTROL_VERSION = 118,
        RTDE_TEXT_MESSAGE = 77,
        RTDE_DATA_PACKAGE = 85,
        RTDE_CONTROL_PACKAGE_SETUP_OUTPUTS = 79,
        RTDE_CONTROL_PACKAGE_SETUP_INPUTS = 73,
        RTDE_CONTROL_PACKAGE_START = 83,
        RTDE_CONTROL_PACKAGE_PAUSE = 80
    }

    public class RtdeClient : IDisposable
    {
        bool keep_going;
        string hostname;
        int port;

        PackageWriter pkg_writer;

        string[] outputs = { "actual_q", "actual_qd", "actual_TCP_pose", "actual_TCP_speed", "robot_status_bits", "safety_status_bits" };
        string[] inputs_1 = { "input_double_register_0", "input_double_register_1", "input_double_register_2",
                              "input_double_register_3", "input_double_register_4", "input_double_register_5"};
        byte inputs_1_id;

        double[] actual_q = new double[6];
        double[] actual_qd = new double[6];
        double[] actual_TCP_pose = new double[6];
        double[] actual_TCP_speed = new double[6];
        uint robot_status_bits;
        uint safety_status_bits;

        double[] joint_cmd_pos = new double[6];

        public void Start(string robot_hostname, int robot_rtde_port)
        {
            hostname = robot_hostname;
            port = robot_rtde_port;
            pkg_writer = new PackageWriter();
            keep_going = true;
        }

        public void Dispose()
        {
            keep_going = false;
        }

        public bool Connected => false;

        public Exception LastException { get; private set; }

        public void _run()
        {
            while(keep_going)
            {
                try
                {
                    using (var socket = new TcpClient(hostname, port))
                    {
                        var net_stream = socket.GetStream();

                        //DoRequestProtocolVersionPackage(net_stream);
                        DoSetupControllerOutputs(net_stream);
                        inputs_1_id = DoSetupControllerInputs1(net_stream);
                        DoPackageStart(net_stream);

                        LastException = null;
                        joint_cmd_pos[1] = -1.5;
                        joint_cmd_pos[2] = 1;
                        joint_cmd_pos[4] = 1;
                        while (keep_going)
                        {
                            DoReceiveControllerOutputs(net_stream);
                            DoSendControllerInputs1(net_stream);
                            joint_cmd_pos[0] += 0.001;
                        }                        
                        return;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Robot communication error: {e.ToString()}");
                    LastException = e;                   
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

        void DoRequestProtocolVersionPackage(NetworkStream s)
        {
            pkg_writer.Begin(RtdePackageType.RTDE_REQUEST_PROTOCOL_VERSION);
            pkg_writer.Write((ushort)1);
            var req = pkg_writer.GetBytes();
            s.Write(req);

            var res_reader = RecvPackage(s,RtdePackageType.RTDE_REQUEST_PROTOCOL_VERSION);
            if (!res_reader.ReadBool())
            {
                throw new IOException("Could not negotiate RTDE protocol with robot");
            }

        }

        void DoSetupControllerOutputs(NetworkStream s)
        {
            pkg_writer.Begin(RtdePackageType.RTDE_CONTROL_PACKAGE_SETUP_OUTPUTS);
            pkg_writer.WriteString(String.Join(", ", outputs));
            var req = pkg_writer.GetBytes();
            s.Write(req);

            var res_reader = RecvPackage(s, RtdePackageType.RTDE_CONTROL_PACKAGE_SETUP_OUTPUTS);
            var res_text = res_reader.ReadStringRemaining();
            if (res_text.Contains("NOT_FOUND"))
            {
                throw new IOException("Invalid controller output requested");
            }
            Console.WriteLine(res_text);
        }

        byte DoSetupControllerInputs1(NetworkStream s)
        {
            pkg_writer.Begin(RtdePackageType.RTDE_CONTROL_PACKAGE_SETUP_INPUTS);
            pkg_writer.WriteString(String.Join(", ", inputs_1));
            var req = pkg_writer.GetBytes();
            s.Write(req);

            var res_reader = RecvPackage(s, RtdePackageType.RTDE_CONTROL_PACKAGE_SETUP_INPUTS);
            byte id = res_reader.ReadByte();
            var res_text = res_reader.ReadStringRemaining();
            if (res_text.Contains("NOT_FOUND"))
            {
                throw new IOException("Invalid controller output requested");
            }

            if (res_text.Contains("IN_USE"))
            {
                throw new IOException("Requsted controller output in use");
            }
            Console.WriteLine(res_text);
            return id;
        }

        void DoPackageStart(NetworkStream s)
        {
            pkg_writer.Begin(RtdePackageType.RTDE_CONTROL_PACKAGE_START);            
            var req = pkg_writer.GetBytes();
            s.Write(req);

            var res_reader = RecvPackage(s, RtdePackageType.RTDE_CONTROL_PACKAGE_START);
            if (!res_reader.ReadBool())
            {
                throw new IOException("Could not start packages");
            }

        }

        void DoReceiveControllerOutputs(NetworkStream s)
        {
            var res_reader = RecvPackage(s, RtdePackageType.RTDE_DATA_PACKAGE);
            lock (this)
            {
                res_reader.ReadDoubleVec6(actual_q);
                res_reader.ReadDoubleVec6(actual_qd);
                res_reader.ReadDoubleVec6(actual_TCP_pose);
                res_reader.ReadDoubleVec6(actual_TCP_speed);
                robot_status_bits = res_reader.ReadUInt32();
                safety_status_bits = res_reader.ReadUInt32();
            }
        }

        void DoSendControllerInputs1(NetworkStream s)
        {
            pkg_writer.Begin(RtdePackageType.RTDE_DATA_PACKAGE);
            pkg_writer.Write(inputs_1_id);
            pkg_writer.Write(joint_cmd_pos);
            var req = pkg_writer.GetBytes();
            s.Write(req);

        }

        byte[] recv_buf = new byte[4096];
        byte[] recv_temp = new byte[8];
        PackageReader package_reader = new PackageReader();
        PackageReader RecvPackage(NetworkStream s, RtdePackageType package_type)
        {
            int pos = 0;
            do
            {
                var l =  s.Read(recv_buf, pos, 2 - pos);
                if (l == 0) throw new IOException("Connection closed");
                pos += l;
            }
            while (pos < 2);

            recv_temp[0] = recv_buf[1];
            recv_temp[1] = recv_buf[0];
            int len = BitConverter.ToUInt16(recv_temp, 0);

            do
            {
                var l = s.Read(recv_buf, pos, len - pos);
                if (l == 0) throw new IOException("Connection closed");
                pos += l;
            }
            while (pos < len);

            return package_reader.BeginRtde(new ArraySegment<byte>(recv_buf, 0, len), package_type);


        }



    }

    static class Extensions
    {
        public static PackageReader BeginRtde(this PackageReader r, ArraySegment<byte> package_buffer, RtdePackageType package_type)
        {
            var r1 = r.BeginRtde(package_buffer);
            if (((RtdePackageType)r1.PackageType) != package_type)
            {
                throw new IOException("Unexpected RTDE package type");
            }
            return r1;
        }

        public static void Begin(this PackageWriter w, RtdePackageType type)
        {
            w.Begin((byte)type);
        }
    }

    
}
