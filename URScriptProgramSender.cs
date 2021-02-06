using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace URRobotRaconteurDriver
{
    public class URScriptProgramSender : IDisposable
    {
        bool keep_going;
        string hostname;
        int port;
        
        public void Start(string robot_hostname, int robot_port = 30001)
        {
            hostname = robot_hostname;
            port = robot_port;
            keep_going = true;            
        }
                
        public void SendAndRunProgram(string program)
        {
            byte[] program_bytes = Encoding.ASCII.GetBytes(program);

            var client = new TcpClient();
            var result = client.BeginConnect(hostname, port, null, null);

            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));

            if (!success)
            {
                throw new Exception("Failed to send UR program");
            }
            
            client.EndConnect(result);
            
            using(client)
            {
                client.GetStream().Write(program_bytes, 0, program_bytes.Length);
                client.GetStream().Flush();                
            }            
        }

        public void SendReset()
        {
            string reset_prog = "def resetProg():\n  sleep(0.0)\nend\n";
            SendAndRunProgram(reset_prog);
        }

        public void Dispose()
        {
            keep_going = false;
        }
    }

    public class RobustURProgramRunner : IDisposable
    {
        bool keep_going;
        string hostname;
        int port;
        Thread thread;
        string prog;

        public void Start(string ur_robot_prog, string robot_hostname, int robot_port = 30001)
        {
            hostname = robot_hostname;
            port = robot_port;
            keep_going = true;
            prog = ur_robot_prog;

            ur_sender.Start(robot_hostname, robot_port);

            thread = new Thread(_run);
            thread.Start();
        }

        public void _run()
        {
            while (keep_going)
            {
                try
                {
                    while(keep_going)
                    {
                        lock(this)
                        {
                            while (reverse_socket_connected && keep_going)
                            {
                                Monitor.Wait(this, 250);
                            }
                        }

                        if (!keep_going) break;

                        ur_sender.SendReset();

                        if (!keep_going) break;
                        lock (exit_monitor)
                        {
                            Monitor.Wait(exit_monitor, 1000);
                        }

                        if (!keep_going) break;

                        ur_sender.SendAndRunProgram(prog);

                        if (!keep_going) break;
                        lock (exit_monitor)
                        {
                            Monitor.Wait(exit_monitor, 2500);
                        }
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine($"Robot communication error: {e.ToString()}");
                }

                for (int i = 0; i < 20; i++)
                {
                    if (!keep_going)
                        break;
                    Thread.Sleep(100);
                }

                Console.WriteLine($"Retrying send UR program!");
            }

            try
            {
                ur_sender?.SendReset();
            }
            catch { }
        }

        
        bool reverse_socket_connected;

        public void UpdateConnectedStatus(bool connected)
        {
            lock (this)
            {
                reverse_socket_connected = connected;
                if (!connected)
                {
                    Monitor.PulseAll(this);
                }
            }
        }

        object exit_monitor = new object();

        public void Dispose()
        {
            keep_going = false;
            try
            {
                Monitor.PulseAll(this);
                Monitor.PulseAll(exit_monitor);
            }
            catch(Exception) { }
        }

        URScriptProgramSender ur_sender = new URScriptProgramSender();
    }
}
