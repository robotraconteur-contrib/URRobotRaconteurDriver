using com.robotraconteur.robotics.robot;
using RobotRaconteur.Companion.Robot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UR.ControllerClient;

namespace URRobotRaconteurDriver
{
    public class URRobotReverseSocket : AbstractRobot, IURRobot
    {
        protected ControllerClient client;
        protected ControllerClientRT client_rt;
        protected ReverseSocketProgClient reverse_client;

        protected string robot_hostname;

        protected RobustURProgramRunner ur_program_runner;
        protected string ur_robot_prog;
        protected string driver_hostname;

        public URRobotReverseSocket(RobotInfo robot_info, string robot_hostname, string driver_hostname, string ur_robot_prog) : base(robot_info, 6)
        {
            this.robot_hostname = robot_hostname;
            _uses_homing = false;
            if (robot_info.joint_info == null)
            {
                _joint_names = new string[] { "shoulder_pan_joint", "shoulder_lift_joint", "elbow_joint", "wrist_1_joint", "wrist_2_joint", "wrist_3_joint" };
            }

            this.ur_robot_prog = ur_robot_prog.Replace("%(driver_hostname)",driver_hostname).Replace("%(driver_reverseport)","50001").Replace("\r\n","\n");
            this.driver_hostname = driver_hostname;

            // TODO: figure out why trajectory tolerance is so poor
            this._trajectory_error_tol = 5 * Math.PI / 180.0;
        }

        public override void _start_robot()
        {
            client = new ControllerClient();
            client.Start(robot_hostname);

            client_rt = new ControllerClientRT();
            client_rt.Start(robot_hostname);

            reverse_client = new ReverseSocketProgClient();
            reverse_client.Start();

            ur_program_runner = new RobustURProgramRunner();
            ur_program_runner.Start(ur_robot_prog, robot_hostname);

            base._start_robot();
        }

        protected override Task _send_disable()
        {
            throw new NotImplementedException();
        }

        protected override Task _send_enable()
        {
            throw new NotImplementedException();
        }

        protected override Task _send_reset_errors()
        {
            throw new NotImplementedException();
        }

        protected override void _send_robot_command(long now, double[] joint_pos_cmd, double[] joint_vel_cmd)
        {
            if (joint_pos_cmd != null)
            {
                reverse_client.SetServojCommand(joint_pos_cmd);
            }
            else
            if (joint_vel_cmd != null)
            {
                reverse_client.SetSpeedjCommand(joint_vel_cmd);
            }
        }

        protected override void _run_timestep(long now)
        {

            if (client.Connected)
            {
                _last_robot_state = now;
                lock (client)
                {
                    var robot_state = client.robot_state.robot_mode_data;
                    _homed = true;
                    _enabled = robot_state.real_robot_enabled && !robot_state.security_stopped;
                    _ready = robot_state.program_running;
                    _stopped = robot_state.security_stopped;
                    _error = false;
                    _estop_source = 0;
                    _operational_mode = RobotOperationalMode.cobot;
                }
            }

            if (client_rt.Connected)
            {

                // Bit of a hack for reverse socket connection notification
                _last_joint_state = now;
                _last_endpoint_state = now;
            }
            lock (client_rt)
            {
                var state_rt = client_rt.state;
                _joint_position = state_rt.q_actual;
                _joint_velocity = state_rt.qd_actual;
                _joint_effort = state_rt.m_target;
                _position_command = state_rt.q_target;
                //_velocity_command = state_rt.qd_target;

                var tcp_vec = state_rt.tool_vector;
                var ep_pose = new com.robotraconteur.geometry.Pose();
                ep_pose.position.x = tcp_vec[0];
                ep_pose.position.y = tcp_vec[1];
                ep_pose.position.z = tcp_vec[2];

                ep_pose.orientation = URRobotRtde.rvec_to_quaternion(tcp_vec);

                _endpoint_pose = new com.robotraconteur.geometry.Pose[] { ep_pose };

                var tcp_vel = state_rt.tcp_speed;
                var ep_vel = new com.robotraconteur.geometry.SpatialVelocity();
                ep_vel.angular.x = tcp_vel[3];
                ep_vel.angular.y = tcp_vel[4];
                ep_vel.angular.z = tcp_vel[5];
                ep_vel.linear.x = tcp_vel[0];
                ep_vel.linear.y = tcp_vel[1];
                ep_vel.linear.z = tcp_vel[2];

                _endpoint_vel = new com.robotraconteur.geometry.SpatialVelocity[] { ep_vel };
            }

            base._run_timestep(now);
        }

        protected override bool _verify_communication(long now)
        {
            bool res = base._verify_communication(now);
            if (!res) return false;
            
            lock (this)
            {
                ur_program_runner.UpdateConnectedStatus(reverse_client.Connected);

                if (!reverse_client.Connected)
                {
                    _communication_failure = true;
                    _command_mode = RobotCommandMode.invalid_state;
                    return false;
                }
                return true;
            }
        }

        public override void Dispose()
        {
            client?.Dispose();
            client_rt?.Dispose();
            reverse_client?.Dispose();
            ur_program_runner?.Dispose();
            base.Dispose();
        }

        public override Task async_setf_signal(string signal_name, double[] value_, int timeout = -1)
        {

            var digital_out_match = Regex.Match(signal_name, @"DO(\d+)");            

            if (digital_out_match.Success)
            {
                int digital_out_index = int.Parse(digital_out_match.Groups[1].Value);
                if (digital_out_index < 0 || digital_out_index > 9)
                {
                    throw new ArgumentException("Digital output DO0 through DO9 expected");
                }

                if (value_.Length != 1)
                {
                    throw new ArgumentException("Expected single element array for digital signal");
                }

                
                reverse_client.SetDigitalOut(digital_out_index, value_[0] != 0.0);
                return Task.FromResult(0);
            }

            var analog_out_match = Regex.Match(signal_name, @"AO(\d+)");
            if (analog_out_match.Success)
            {
                int analog_out_index = int.Parse(analog_out_match.Groups[1].Value);
                if (analog_out_index < 0 || analog_out_index > 1)
                {
                    throw new ArgumentException("Analog output AO0 through AO1 expected");
                }

                if (value_.Length != 1)
                {
                    throw new ArgumentException("Expected single element array for analog signal");
                }

                var v = value_[0];
                if (v < 0 || v > 1)
                {
                    throw new ArgumentException("Analog output command must be between 0 and 1");
                }
                reverse_client.SetAnalogOut(analog_out_index, v);
                return Task.FromResult(0);
            }

            throw new ArgumentException("Invalid signal name");
        }

        public override Task<double[]> async_getf_signal(string signal_name, int timeout = -1)
        {
            var digital_in_match = Regex.Match(signal_name, @"DI(\d+)");

            if (digital_in_match.Success)
            {
                int digital_in_index = int.Parse(digital_in_match.Groups[1].Value);
                if (digital_in_index < 0 || digital_in_index > 9)
                {
                    throw new ArgumentException("Digital input DI0 through DI9 expected");
                }                

                bool val = (client_rt.state.digital_input_bits & (1u >> digital_in_index)) != 0;
                
                return Task.FromResult(new double[] { val ? 1.0 : 0.0 });
            }

            var analog_in_match = Regex.Match(signal_name, @"AI(\d+)");
            if (analog_in_match.Success)
            {
                int analog_in_index = int.Parse(analog_in_match.Groups[1].Value);
                if (analog_in_index < 0 || analog_in_index > 4)
                {
                    throw new ArgumentException("Analog input AI0 through AI4 expected");
                }

                double val;
                switch (analog_in_index)
                {
                    case 0:
                        val = client.robot_state.master_board_data.analogInput0;
                        break;
                    case 1:
                        val = client.robot_state.master_board_data.analogInput1;
                        break;
                    case 2:
                        val = client.robot_state.tool_data.analogInput2;
                        break;
                    case 3:
                        val = client.robot_state.tool_data.analogInput3;
                        break;
                    default:
                        val = 0.0;
                        break;
                }

                return Task.FromResult(new double[] { val });
            }

            throw new ArgumentException("Invalid signal name");
        }
    }
}
