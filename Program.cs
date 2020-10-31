using Mono.Options;
using Mono.Unix;
using RobotRaconteur;
using RobotRaconteur.Companion.InfoParser;
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
            string robot_hostname = null;
            string driver_hostname = null;
            bool wait_signal = false;
            string ur_script_file = null;

            var options = new OptionSet {
                { "robot-info-file=", n => robot_info_file = n },
                { "robot-hostname=", n => robot_hostname = n },
                { "driver-hostname=", n=> driver_hostname = n },
                { "ur-script-file=", n =>  ur_script_file = n},
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
            if (ur_script_file != null)
            {
                ur_robot_prog = File.ReadAllText(ur_script_file);
            }
            else
            {                
                using (var stream = typeof(Program).Assembly.GetManifestResourceStream("URRobotRaconteurDriver.ur_robot_prog.txt"))
                using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
                    ur_robot_prog = reader.ReadToEnd();
            }


            var robot_info = RobotInfoParser.LoadRobotInfoYamlWithIdentifierLocks(robot_info_file);
            using (robot_info.Item2)
            {

                using (var robot = new URRobot(robot_info.Item1, robot_hostname, driver_hostname, ur_robot_prog))
                {
                    robot._start_robot();
                    using (var node_setup = new ServerNodeSetup("ur_robot", 58652, args))
                    {
                        RobotRaconteurNode.s.RegisterService("robot", "com.robotraconteur.robotics.robot", robot);

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
