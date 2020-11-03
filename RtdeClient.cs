using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using UR.Package;
using RobotRaconteur;
using System.Diagnostics;

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

    [Flags]
    public enum RTDE_ROBOT_STATUS_BITS : uint
    {
        power_on = 0x1,
        program_running = 0x2,
        teach_button_pressed = 0x4,
        power_button_pressed = 0x8
    }

    [Flags]
    public enum RTDE_SAFETY_STATUS_BITS
    {
        normal_mode = 0x1,
        reduced_mode = 0x2,
        protective_stopped = 0x4,
        recovery_mode = 0x8,
        safeguard_stopped = 0x10,
        system_emergency_stopped = 0x20,
        robot_emergency_stopped = 0x40,
        emergency_stopped = 0x80,
        violation = 0x100,
        fault = 0x200,
        safety_stopped = 0x400
    }

    public class RtdeClient : IDisposable
    {
        bool keep_going;
        string hostname;
        int port;

        PackageWriter pkg_writer;

        string[] outputs = { "output_int_register_0", "actual_q", "actual_qd", "actual_TCP_pose", "actual_TCP_speed", "target_q", "target_qd", "target_moment", "robot_status_bits", "safety_status_bits", 
            "actual_digital_input_bits", "actual_digital_output_bits",  "standard_analog_input0", "standard_analog_input1", "standard_analog_output0",
            "standard_analog_output1", "tool_analog_input0", "tool_analog_input1" };
        string[] inputs_1 = { "input_int_register_0", "input_int_register_1", "input_double_register_0", "input_double_register_1", "input_double_register_2",
                              "input_double_register_3", "input_double_register_4", "input_double_register_5"};

        string[] inputs_2 = { "standard_digital_output_mask", "standard_digital_output", "standard_analog_output_mask", "standard_analog_output_0", "standard_analog_output_1" };
        byte inputs_1_id;
        byte inputs_2_id;

        public double[] actual_q = new double[6];
        public double[] actual_qd = new double[6];
        public double[] actual_TCP_pose = new double[6];
        public double[] actual_TCP_speed = new double[6];
        public double[] target_q = new double[6];
        public double[] target_qd = new double[6];
        public double[] target_moment = new double[6];
        public uint robot_status_bits;
        public uint safety_status_bits;
        public ulong actual_digital_input_bits;
        public ulong actual_digital_output_bits;
        public double actual_analog_input0;
        public double actual_analog_input1;
        public double actual_analog_output0;
        public double actual_analog_output1;
        public double tool_analog_input0;
        public double tool_analog_input1;

        int cmd_seqno = 1;
        int recv_seqno = 0;
        long last_recv_seqno_change = 0;

        double[] joint_cmd_pos = null;

        byte digital_output_mask;
        byte digital_output;
        byte analog_output_mask;
        double analog_output_0;
        double analog_output_1;

        Thread thread;

        Stopwatch stopwatch;

        public void Start(Stopwatch stopwatch, string robot_hostname, int robot_rtde_port = 30004 )
        {
            this.stopwatch = stopwatch;
            hostname = robot_hostname;
            port = robot_rtde_port;
            pkg_writer = new PackageWriter();
            keep_going = true;

            thread = new Thread(_run);
            thread.Start();
        }

        public void Dispose()
        {
            keep_going = false;
        }

        public bool Connected
        {
            get
            {
                return stopwatch.ElapsedMilliseconds - last_recv_seqno_change < 500;
            }
        }

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
                        inputs_2_id = DoSetupControllerInputs2(net_stream);
                        DoPackageStart(net_stream);

                        LastException = null;                        
                        while (keep_going)
                        {
                            DoReceiveControllerOutputs(net_stream);
                            DoSendControllerInputs1(net_stream);
                            DoSendControllerInputs2(net_stream);
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
            return DoSetupControllerInputs(s, inputs_1);
        }

        byte DoSetupControllerInputs2(NetworkStream s)
        {
            return DoSetupControllerInputs(s, inputs_2);
        }

        byte DoSetupControllerInputs(NetworkStream s, string[] input_names)
        {
            pkg_writer.Begin(RtdePackageType.RTDE_CONTROL_PACKAGE_SETUP_INPUTS);
            pkg_writer.WriteString(String.Join(", ", input_names));
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
                var last_recv_seqno = recv_seqno;
                recv_seqno = res_reader.ReadInt32();
                if (recv_seqno != last_recv_seqno)
                {
                    last_recv_seqno_change = stopwatch.ElapsedMilliseconds;
                }
                res_reader.ReadDoubleVec6(actual_q);
                res_reader.ReadDoubleVec6(actual_qd);
                res_reader.ReadDoubleVec6(actual_TCP_pose);
                res_reader.ReadDoubleVec6(actual_TCP_speed);
                res_reader.ReadDoubleVec6(target_q);
                res_reader.ReadDoubleVec6(target_qd);
                res_reader.ReadDoubleVec6(target_moment);
                robot_status_bits = res_reader.ReadUInt32();
                safety_status_bits = res_reader.ReadUInt32();
                actual_digital_input_bits = res_reader.ReadUInt64();
                actual_digital_output_bits = res_reader.ReadUInt64();
                actual_analog_input0 = res_reader.ReadDouble();
                actual_analog_input1 = res_reader.ReadDouble();
                actual_analog_output0 = res_reader.ReadDouble();
                actual_analog_output1 = res_reader.ReadDouble();
                tool_analog_input0 = res_reader.ReadDouble();
                tool_analog_input1 = res_reader.ReadDouble();
            }
        }

        void DoSendControllerInputs1(NetworkStream s)
        {
            double[] cmd;
            lock(this)
            {
                cmd = joint_cmd_pos;
                joint_cmd_pos = null;
            }

            cmd_seqno = cmd_seqno != int.MaxValue ? cmd_seqno + 1 : 1;
            pkg_writer.Begin(RtdePackageType.RTDE_DATA_PACKAGE);
            pkg_writer.Write(inputs_1_id);
            pkg_writer.Write(cmd_seqno);
            if (cmd == null)
            {
                pkg_writer.Write((int)0);
                for (int i = 0; i < 6; i++) pkg_writer.Write((double)0);
            }
            else
            {
                pkg_writer.Write((int)1);
                pkg_writer.Write(cmd);

            }
            
            var req = pkg_writer.GetBytes();
            s.Write(req);
        }

        void DoSendControllerInputs2(NetworkStream s)
        {
            byte digital_output_mask_l;
            byte digital_output_l;
            byte analog_output_mask_l;
            double analog_output_0_l;
            double analog_output_1_l;

            lock (this)
            {
                digital_output_mask_l = digital_output_mask;
                digital_output_l = digital_output;
                analog_output_mask_l = analog_output_mask;
                analog_output_0_l = analog_output_0;
                analog_output_1_l = analog_output_1;
                digital_output_mask = 0;
                analog_output_mask = 0;
            }
                     
            if (digital_output_mask_l == 0 && analog_output_mask_l == 0)
            {
                return;
            }

            pkg_writer.Begin(RtdePackageType.RTDE_DATA_PACKAGE);
            pkg_writer.Write(inputs_2_id);
            pkg_writer.Write(digital_output_mask_l);
            pkg_writer.Write(digital_output_l);
            pkg_writer.Write(analog_output_mask_l);
            pkg_writer.Write(analog_output_0_l);
            pkg_writer.Write(analog_output_1_l);

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

        public void SetJointCommand(double[] joint_cmd)
        {
            Debug.Assert(joint_cmd.Length == 6);
            lock(this)
            {
                this.joint_cmd_pos = joint_cmd;
            }
        }

        public void ClearJointCommand()
        {
            lock(this)
            {
                this.joint_cmd_pos = null;
            }
        }

        public void SetDigitalOut(int signal, bool value)
        {
            Debug.Assert(signal >= 0 && signal < 8);
            lock (this)
            {

                if (value)
                {
                    digital_output |= (byte)(1 << signal);
                }
                else
                {
                    digital_output &= (byte)~(1 << signal);
                }
                digital_output_mask |= (byte)(1 << signal);
            }
        }

        public void SetAnalogOut(int signal, double value)
        {
            Debug.Assert(signal >= 0 && signal < 8);
            lock (this)
            {

                switch (signal)
                {
                    case 0:
                        analog_output_0 = value;
                        break;
                    case 1:
                        analog_output_1 = value;
                        break;
                    default:
                        throw new ArgumentException("Invalid analog signal number");
                }
                analog_output_mask |= (byte)(1 << signal);
            }
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
