using Mono.Options;
using Mono.Unix;
using RobotRaconteur;
using RobotRaconteur.Companion.InfoParser;
using RobotRaconteur.Companion.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace URRobotRaconteurDriver
{
    class Program
    {
        static int Main(string[] args)
        {
            bool shouldShowHelp = false;
            string robot_info_file = null;
            string robot_name = null;
            string robot_hostname = null;
            string driver_hostname = null;
            bool wait_signal = false;
            string ur_script_file = null;
            bool cb2_compat = false;

            var options = new OptionSet {
                { "robot-info-file=", n => robot_info_file = n },
                { "robot-name=", "override the robot device name", n=>robot_name = n },
                { "robot-hostname=", n => robot_hostname = n },
                { "driver-hostname=", n=> driver_hostname = n },
                { "ur-script-file=", n =>  ur_script_file = n},
                { "cb2-compat", n => cb2_compat = n != null },
                { "h|help", "show this message and exit", h => shouldShowHelp = h != null },
                {"wait-signal", "wait for POSIX sigint or sigkill to exit", n=> wait_signal = n!=null}
            };

            List<string> extra;
            try
            {
                // parse the command line
                extra = options.Parse(args);
            }
            catch (OptionException e)
            {
                // output some error message
                Console.Write("URRobotRaconteurDriver: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `URRobotRaconteurDriver --help' for more information.");
                return 1;
            }

            if (shouldShowHelp)
            {
                Console.WriteLine("Usage: SawyerRobotRaconteurDriver [Options+]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return 0;
            }

            if (robot_info_file == null)
            {
                Console.WriteLine("error: robot-info-file must be specified");
                return 1;
            }

            string ur_robot_prog = null;

            if (cb2_compat)
            {
                if (ur_script_file != null)
                {
                    ur_robot_prog = File.ReadAllText(ur_script_file);
                }
                else
                {
                    using (var stream = typeof(Program).Assembly.GetManifestResourceStream("URRobotRaconteurDriver.ur_reverse_socket_control_loop.script"))
                    using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
                        ur_robot_prog = reader.ReadToEnd();
                }

            }
            else
            {
                if (ur_script_file != null)
                {
                    ur_robot_prog = File.ReadAllText(ur_script_file);
                }
                else
                {
                    using (var stream = typeof(Program).Assembly.GetManifestResourceStream("URRobotRaconteurDriver.ur_rtde_control_loop.script"))
                    using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
                        ur_robot_prog = reader.ReadToEnd();
                }
            }

            var robot_info = RobotInfoParser.LoadRobotInfoYamlWithIdentifierLocks(robot_info_file,robot_name);
            using (robot_info.Item2)
            {
                IURRobot robot;
                if (cb2_compat)
                {
                    robot = new URRobotReverseSocket(robot_info.Item1, robot_hostname, driver_hostname, ur_robot_prog);
                }
                else
                {
                    robot = new URRobotRtde(robot_info.Item1, robot_hostname, ur_robot_prog);
                }

                using (robot)
                {
                    robot._start_robot();
                    using (var node_setup = new ServerNodeSetup("ur_robot", 58652, args))
                    {
                        var robot_service_ctx = RobotRaconteurNode.s.RegisterService("robot", "com.robotraconteur.robotics.robot", robot);
                        robot_service_ctx.SetServiceAttributes(AttributesUtil.GetDefaultServiceAtributesFromDeviceInfo(robot_info.Item1.device_info));

                        if (!wait_signal)
                        {
                            Console.WriteLine("Press enter to exit");
                            Console.ReadKey();
                        }
                        else
                        {
                            UnixSignal[] signals = new UnixSignal[]{
                                new UnixSignal (Mono.Unix.Native.Signum.SIGINT),
                                new UnixSignal (Mono.Unix.Native.Signum.SIGTERM),
                            };

                            Console.WriteLine("Press Ctrl-C to exit");
                            // block until a SIGINT or SIGTERM signal is generated.
                            int which = UnixSignal.WaitAny(signals, -1);

                            Console.WriteLine("Got a {0} signal, exiting", signals[which].Signum);
                        }
                    }
                }
            }

            // Give time for the reset program to be sent to UR robot

            System.Threading.Thread.Sleep(2000);

            return 0;
        }
    }
}
