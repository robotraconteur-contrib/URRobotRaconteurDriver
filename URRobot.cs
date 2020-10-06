using com.robotraconteur.robotics.robot;
using RobotRaconteurWeb.StandardRobDefLib.Robot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UR.ControllerClient;

namespace URRobotRaconteurDriver
{
    public class URRobot : AbstractRobot
    {
        protected ControllerClient client;
        protected ControllerClientRT client_rt;
        protected ReverseSocketProgClient reverse_client;

        protected string robot_hostname;

        public URRobot(RobotInfo robot_info, string robot_hostname) : base(robot_info, 6)
        {
            this.robot_hostname = robot_hostname;
            _uses_homing = false;
            if (robot_info.joint_info == null)
            {
                _joint_names = new string[] { "shoulder_pan_joint", "shoulder_lift_joint", "elbow_joint", "wrist_1_joint", "wrist_2_joint", "wrist_3_joint" };
            }
        }

        public override void _start_robot()
        {

            client = new ControllerClient();
            client.Start(robot_hostname);

            client_rt = new ControllerClientRT();
            client_rt.Start(robot_hostname);

            reverse_client = new ReverseSocketProgClient();
            reverse_client.Start();

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

                var tcp_vec = state_rt.tool_vector;
                var ep_pose = new com.robotraconteur.geometry.Pose();
                ep_pose.position.x = tcp_vec[0];
                ep_pose.position.y = tcp_vec[1];
                ep_pose.position.z = tcp_vec[2];

                // TODO: orientation

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
            base.Dispose();
        }

        public override Task setf_signal(string signal_name, double[] value_, CancellationToken rr_cancel = default)
        {
            var signal_names = Enumerable.Range(0, 8).Select(x => $"D{x}").ToArray();

            if (signal_names.Contains(signal_name))
            {
                if (value_.Length != 1)
                {
                    throw new ArgumentException("Expected single element array for digital signal");
                }

                int signal_index = Int32.Parse(signal_name.Replace("D", ""));

                reverse_client.SetDigitalOut(signal_index, value_[0] != 0.0);
                return Task.FromResult(0);
            }

            throw new ArgumentException("Invalid signal name");

        }
    }
}
