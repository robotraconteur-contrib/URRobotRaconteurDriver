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
    public class ControllerStateRT_V18
    {
        public double time;
        public double[] q_target = new double[6];
        public double[] qd_target = new double[6];
        public double[] qdd_target = new double[6];
        public double[] i_target = new double[6];
        public double[] m_target = new double[6];
        public double[] q_actual = new double[6];
        public double[] qd_actual = new double[6];
        public double[] i_actual = new double[6];
        public double[] tool_acc_values = new double[3];
        public double[] tcp_force = new double[6];
        public double[] tool_vector = new double[6];
        public double[] tcp_speed = new double[6];
        public ulong digital_input_bits;
        public double[] motor_temperatures = new double[6];
        public double controller_timer;
        public double test_value;
        public ulong robot_mode;
        public double[] joint_mode = new double[6];

        public void Read(PackageReader r)
        {
            time = r.ReadDouble();
            r.ReadDoubleVec6(q_target);
            r.ReadDoubleVec6(qd_target);
            r.ReadDoubleVec6(qdd_target);
            r.ReadDoubleVec6(i_target);
            r.ReadDoubleVec6(m_target);
            r.ReadDoubleVec6(q_actual);
            r.ReadDoubleVec6(qd_actual);
            r.ReadDoubleVec6(i_actual);
            r.ReadDoubleVec3(tool_acc_values);
            r.Skip(15 * 8);
            r.ReadDoubleVec6(tcp_force);
            r.ReadDoubleVec6(tool_vector);
            r.ReadDoubleVec6(tcp_speed);
            digital_input_bits = r.ReadUInt64();
            r.ReadDoubleVec6(motor_temperatures);
            controller_timer = r.ReadDouble();
            test_value = r.ReadDouble();
            robot_mode = r.ReadUInt64();
            r.ReadDoubleVec6(joint_mode);
        }
    }



    public class ControllerClientRT : IDisposable
    {
        bool keep_going;
        string hostname;
        int port;

        public ControllerStateRT_V18 state = new ControllerStateRT_V18();

        Thread thread;
        public void Start(string robot_hostname, int robot_rt_port = 30003)
        {
            hostname = robot_hostname;
            port = robot_rt_port;
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

                        while (keep_going)
                        {
                            Connected = true;
                            DoReceiveControllerOutputs(net_stream);
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

        void DoReceiveControllerOutputs(NetworkStream s)
        {
            var res_reader = RecvPackage(s);
            lock (this)
            {
                state.Read(res_reader);
            }
        }


        byte[] recv_buf = new byte[4096];
        byte[] recv_temp = new byte[8];
        PackageReader package_reader = new PackageReader();
        PackageReader RecvPackage(NetworkStream s)
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

            do
            {
                var l = s.Read(recv_buf, pos, len - pos);
                if (l == 0) throw new IOException("Connection closed");
                pos += l;
            }
            while (pos < len);

            return package_reader.BeginController(new ArraySegment<byte>(recv_buf, 0, len));


        }



    }



}
