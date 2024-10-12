using com.robotraconteur.geometry;
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
using UR.RTDE;

namespace URRobotRaconteurDriver
{
    public class URRobotRtde : AbstractRobot, IURRobot
    {
        protected string robot_hostname;

        RtdeClient rtde_client;
        protected RobustURProgramRunner ur_program_runner;
        protected string ur_robot_prog;

        public static Quaternion rvec_to_quaternion(double[] rvec)
        {
            double norm = Math.Sqrt(Math.Pow(rvec[3], 2) + Math.Pow(rvec[4], 2) + Math.Pow(rvec[5], 2));
            if (norm < 1e-5)
            {
                return new Quaternion { w = 1, x = 0, y = 0, z = 0 };
            }

            double x = rvec[3] / norm;
            double y = rvec[4] / norm;
            double z = rvec[5] / norm;

            double s = Math.Sin(norm / 2.0);
            double c = Math.Cos(norm / 2.0);

            return new Quaternion
            {
                w = c,
                x = x * s,
                y = y * s,
                z = z * s
            };
        }

        public URRobotRtde(RobotInfo robot_info, string robot_hostname, string ur_robot_prog) : base(robot_info, 6)
        {
            this.robot_hostname = robot_hostname;
            _uses_homing = false;
            if (robot_info.joint_info == null)
            {
                _joint_names = new string[] { "shoulder_pan_joint", "shoulder_lift_joint", "elbow_joint", "wrist_1_joint", "wrist_2_joint", "wrist_3_joint" };
            }

            robot_info.robot_capabilities &= (uint)(RobotCapabilities.jog_command & RobotCapabilities.position_command & RobotCapabilities.trajectory_command);
            this.ur_robot_prog = ur_robot_prog;

            // TODO: figure out why trajectory tolerance is so poor
            //this._trajectory_error_tol = 5 * Math.PI / 180.0;
            this._trajectory_error_tol = 1000;
        }

        public override void _start_robot()
        {
            ur_program_runner = new RobustURProgramRunner();
            ur_program_runner.Start(ur_robot_prog, robot_hostname);

            rtde_client = new RtdeClient();


            base._start_robot();
            rtde_client.Start(_stopwatch, robot_hostname);
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
                rtde_client.SetJointCommand(joint_pos_cmd);
            }
            else
            {
                rtde_client.ClearJointCommand();
            }
        }

        protected override void _run_timestep(long now)
        {

            if (rtde_client.Connected)
            {
                _last_robot_state = now;
                uint status_bits;
                uint safety_bits;
                lock (rtde_client)
                {
                    status_bits = rtde_client.robot_status_bits;
                    safety_bits = rtde_client.safety_status_bits;
                }
                _homed = true;
                _enabled = (status_bits & ((uint)RTDE_ROBOT_STATUS_BITS.power_on)) != 0;
                _ready = (status_bits & ((uint)RTDE_ROBOT_STATUS_BITS.power_on)) != 0 && (status_bits & ((uint)RTDE_ROBOT_STATUS_BITS.program_running)) != 0;
                _stopped = (safety_bits & ((uint)RTDE_SAFETY_STATUS_BITS.protective_stopped)) != 0 || (safety_bits & ((uint)RTDE_SAFETY_STATUS_BITS.emergency_stopped)) != 0 || (safety_bits & ((uint)RTDE_SAFETY_STATUS_BITS.safety_stopped)) != 0;
                _error = (safety_bits & ((uint)RTDE_SAFETY_STATUS_BITS.fault)) != 0 || (safety_bits & ((uint)RTDE_SAFETY_STATUS_BITS.violation)) != 0;
                _estop_source = 0;
                _operational_mode = RobotOperationalMode.cobot;

            }

            if (rtde_client.Connected)
            {
                // Bit of a hack for reverse socket connection notification
                _last_joint_state = now;
                _last_endpoint_state = now;
            }
            lock (rtde_client)
            {
                _joint_position = rtde_client.actual_q;
                _joint_velocity = rtde_client.actual_qd;
                _joint_effort = rtde_client.target_moment;
                _position_command = rtde_client.target_q;
                //_velocity_command = rtde_client.target_qd;

                var ep_vec = rtde_client.actual_TCP_pose;
                var ep_pose = new com.robotraconteur.geometry.Pose();
                ep_pose.position.x = ep_vec[0];
                ep_pose.position.y = ep_vec[1];
                ep_pose.position.z = ep_vec[2];

                ep_pose.orientation = rvec_to_quaternion(ep_vec);

                _endpoint_pose = new com.robotraconteur.geometry.Pose[] { ep_pose };

                var tcp_vel = rtde_client.actual_TCP_speed;
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
                ur_program_runner.UpdateConnectedStatus(rtde_client.Connected);
                if (!rtde_client.Connected)
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
            rtde_client?.Dispose();
            ur_program_runner?.Dispose();
            base.Dispose();
        }

        public override Task async_setf_signal(string signal_name, double[] value_, int timeout = -1)
        {

            var digital_out_match = Regex.Match(signal_name, @"DO(\d+)");

            if (digital_out_match.Success)
            {
                int digital_out_index = int.Parse(digital_out_match.Groups[1].Value);
                if (digital_out_index < 0 || digital_out_index > 7)
                {
                    throw new ArgumentException("Digital output DO0 through DO7 expected");
                }

                if (value_.Length != 1)
                {
                    throw new ArgumentException("Expected single element array for digital signal");
                }


                rtde_client.SetDigitalOut(digital_out_index, value_[0] != 0.0);
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
                rtde_client.SetAnalogOut(analog_out_index, v);
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

                if (digital_in_index >= 8)
                {
                    // Skip configurable inputs, match reverse socket behavior
                    digital_in_index += 8;
                }

                bool val = (rtde_client.actual_digital_input_bits & (1u >> digital_in_index)) != 0;

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
                        val = rtde_client.actual_analog_input0;
                        break;
                    case 1:
                        val = rtde_client.actual_analog_input1;
                        break;
                    case 2:
                        val = rtde_client.tool_analog_input0;
                        break;
                    case 3:
                        val = rtde_client.tool_analog_input1;
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
