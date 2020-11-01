using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using UR.Package;

namespace UR.ControllerClient
{
    enum ReverseSocketMsgTypeCode
    {
        MSG_OUT = 1,
        MSG_QUIT = 2,
        MSG_JOINT_STATES = 3,
        MSG_MOVEJ = 4,
        MSG_WAYPOINT_FINISHED = 5,
        MSG_STOPJ = 6,
        MSG_SERVOJ = 7,
        MSG_SET_PAYLOAD = 8,
        MSG_WRENCH = 9,
        MSG_SET_DIGITAL_OUT = 10,
        MSG_GET_IO = 11,
        MSG_SET_FLAG = 12,
        MSG_SET_TOOL_VOLTAGE = 13,
        MSG_SET_ANALOG_OUT = 14,
        MSG_BRAKING = 15,
        MSG_SERVOED=16,
        MSG_IDLE = 17,
        MSG_HELLO = 18,
        MSG_LISTENING = 19,
        MSG_RECV_NOTHING = 20,
        MSG_RECV_TOOMUCH = 21,
        MSG_RECV_QUIT = 22,
        MSG_RECV_MOVEJ = 23,
        MSG_PARAM_ERR = 24,
        MSG_MOVEJ_STARTED = 25,
        MSG_MOVEJ_FINISHED = 26,
        MSG_RECV_PAYLOAD = 27,
        MSG_RECV_STOPJ = 28,
        MSG_RECV_SET_DIGITAL_OUT = 29,
        MSG_RECV_SET_FLAG = 30,
        MSG_RECV_SET_ANALOG_OUT = 31,
        MSG_RECV_SET_TOOL_VOLTAGE = 32,
        MSG_RECV_UNKNOWN = 33,
        MSG_RECV_SERVOJ = 34,
        MSG_SPEEDJ = 35,
        MSG_PING = 36,
        MSG_RECV_PING = 37

    }

    public class ReverseSocketProgClient : IDisposable
    {
        public const double MULT_wrench = 10000.0;
        public const double MULT_payload = 1000.0;
        public const double MULT_jointstate = 10000.0;
        public const double MULT_time = 1000000.0;
        public const double MULT_blend = 1000.0;
        public const double MULT_analog = 1000000.0;

        bool keep_going;
        int port;

        Thread thread;

        public void Start(int listen_port = 50001)
        {
            port = listen_port;
            keep_going = true;

            thread = new Thread(_run);
            thread.Start();
        }

        public void Dispose()
        {
            keep_going = false;
            try
            {
                listener.Stop();
            }
            catch (Exception)
            {

            }
        }

        public bool Connected { get; private set; }

        public Exception LastException { get; private set; }
        TcpListener listener;

        double[] servoj_command = null;
        double[] speedj_command = null;

        AutoResetEvent sync = new AutoResetEvent(false);

        
        public void SetServojCommand(double[] cmd)
        {
            //Console.WriteLine("SetServojCommand {0}, {1}, {2}", cmd[0], cmd[1], cmd[2]);
            lock (this)
            {
                servoj_command = cmd;
                speedj_command = null;
                sync.Set();
            }            
        }

        public void SetSpeedjCommand(double[] cmd)
        {
            //Console.WriteLine("SetSpeedjCommand {0}, {1}, {2}", cmd[0], cmd[1], cmd[2]);
            lock (this)
            {
                speedj_command = cmd;
                servoj_command = null;
                sync.Set();
            }
        }

        byte[] recv_buf = new byte[4];
        byte[] recv_temp = new byte[4];
        ReverseSocketMsgTypeCode recv_msg_code(NetworkStream net_stream)
        {
            int len = net_stream.Read(recv_buf, 0, 4);
            if (len == 0) throw new IOException("Connection closed");
            if (len != 4) throw new IOException("Invalid response");

            recv_temp[0] = recv_buf[3];
            recv_temp[1] = recv_buf[2];
            recv_temp[2] = recv_buf[1];
            recv_temp[3] = recv_buf[0];
            int msg = BitConverter.ToInt32(recv_temp, 0);
            ReverseSocketMsgTypeCode msg2 = (ReverseSocketMsgTypeCode)msg;
            return msg2;
        }

        NetworkStream net_stream = null;

        public void _run()
        {
            listener = new TcpListener(IPAddress.Any, port);

            var w = new PackageWriter();

            try
            {
                while (keep_going)
                {
                    try
                    {
                        listener.Start();
                        using (var socket = listener.AcceptTcpClient())
                        {
                            listener.Stop();
                            net_stream = socket.GetStream();

                            var msg = recv_msg_code(net_stream);
                            if (msg != ReverseSocketMsgTypeCode.MSG_HELLO)
                            {
                                throw new IOException("Did not receive hello message from robot");
                            }

                            Connected = true;

                            //servoj_command = null;
                            //speedj_command = null;

                            while (keep_going)
                            {
                                double[] p;
                                double[] s;
                                lock (this)
                                {
                                    p = servoj_command;
                                    s = speedj_command;
                                }

                                Thread.Sleep(2);
                                if (p == null && s == null)
                                {
                                    w.Begin();
                                    w.Write((int)ReverseSocketMsgTypeCode.MSG_PING);                                    
                                    lock (this)
                                    {
                                        net_stream.Write(w.GetRawBytes());
                                    }
                                    var msg3 = recv_msg_code(net_stream);
                                    if (msg3 != ReverseSocketMsgTypeCode.MSG_RECV_PING)
                                    {
                                        throw new IOException("Invalid message type");
                                    }
                                    sync.WaitOne(10);
                                    continue;
                                }

                                if (p != null)
                                {
                                    lock (this)
                                    {
                                        servoj_command = null;
                                    }

                                    w.Begin();
                                    w.Write((int)ReverseSocketMsgTypeCode.MSG_SERVOJ);
                                    w.Write((int)999);
                                    for (int i = 0; i < 6; i++)
                                    {
                                        w.Write((int)(p[i] * MULT_jointstate));
                                    }
                                    w.Write((int)(0.2 * MULT_time));
                                    lock (this)
                                    {
                                        net_stream.Write(w.GetRawBytes());
                                    }

                                }
                                else if (s != null)
                                {
                                    lock (this)
                                    {
                                        speedj_command = null;
                                    }

                                    w.Begin();
                                    w.Write((int)ReverseSocketMsgTypeCode.MSG_SPEEDJ);
                                    w.Write((int)999);
                                    for (int i = 0; i < 6; i++)
                                    {
                                        w.Write((int)(s[i] * MULT_jointstate));
                                    }
                                    w.Write((int)(0.005 * MULT_time));

                                    lock (this)
                                    {
                                        net_stream.Write(w.GetRawBytes());
                                    }
                                }

                                var msg2 = recv_msg_code(net_stream);
                                if (msg2 != ReverseSocketMsgTypeCode.MSG_RECV_SERVOJ)
                                {
                                    throw new IOException("Invalid message type");
                                }
                                                                
                            }
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Robot reverse socket communication error: {e.ToString()}");
                        LastException = e;
                    }
                    finally
                    {
                        Connected = false;
                    }

                    for (int i = 0; i < 2; i++)
                    {
                        if (!keep_going)
                            break;
                        Thread.Sleep(100);
                    }

                    Console.WriteLine($"Retrying robot reverse socket connection!");

                }
            }
            finally
            {
                listener?.Stop();
            }

        }

        public void SetDigitalOut(int index, bool value)
        {
            var net_stream1 = net_stream;
            if (net_stream1 == null)
            {
                throw new InvalidOperationException("Robot is not connected");
            }

            var w = new PackageWriter();
            w.Begin();
            w.Write((int)ReverseSocketMsgTypeCode.MSG_SET_DIGITAL_OUT);
            w.Write((int)index);
            w.Write((int)(value ? 1 : 0));
            lock (this)
            {
                net_stream.Write(w.GetRawBytes());
            }

        }

        public void SetAnalogOut(int index, double value)
        {
            var net_stream1 = net_stream;
            if (net_stream1 == null)
            {
                throw new InvalidOperationException("Robot is not connected");
            }

            var w = new PackageWriter();
            w.Begin();
            w.Write((int)ReverseSocketMsgTypeCode.MSG_SET_ANALOG_OUT);
            w.Write((int)index);
            w.Write((int)(value*MULT_analog));
            lock (this)
            {
                net_stream.Write(w.GetRawBytes());
            }

        }
    }
}
