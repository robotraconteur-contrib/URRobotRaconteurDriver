using Mono.Options;
using RobotRaconteurWeb;
using RobotRaconteurWeb.InfoParser;
using System;
using System.Collections.Generic;

namespace URRobotRaconteurDriver
{
    class Program
    {
        static int Main(string[] args)
        {
            bool shouldShowHelp = false;
            string robot_info_file = null;
            string robot_hostname = null;

            var options = new OptionSet {
                { "robot-info-file=", n => robot_info_file = n },
                { "robot-hostname=", n => robot_hostname = n },
                { "h|help", "show this message and exit", h => shouldShowHelp = h != null }
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


            var robot_info = RobotInfoParser.LoadRobotInfoYamlWithIdentifierLocks(robot_info_file);
            using (robot_info.Item2)
            {

                using (var robot = new URRobot(robot_info.Item1, robot_hostname))
                {
                    robot._start_robot();
                    using (var node_setup = new ServerNodeSetup("ur_robot", 58652))
                    {
                        RobotRaconteurNode.s.RegisterService("ur_robot", "com.robotraconteur.robotics.robot", robot);

                        Console.WriteLine("Press enter to exit");
                        Console.ReadKey();

                        RobotRaconteurNode.s.Shutdown();
                    }
                }
            }

            return 0;
        }
    }
}
