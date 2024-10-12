<p align="center"><img src="https://raw.githubusercontent.com/robotraconteur/robotraconteur/refs/heads/master/docs/figures/logo-header.svg"></p>

# URRobotRaconteurDriver

## Introduction

Robot Raconteur standard robot driver for UR robots. Supports CB2, CB3, and e-Series robots.

This driver communicates with the robot using the RTDE, reverse sockets, and script download interfaces.
This driver uses streaming position control to command the robot. RTDE is used for e-Series and newer robots.
Reverse sockets are used for CB2 and CB3 robots.

Reverse Socket CB2 compatibilty mode is based on the ROS Industrial UR driver project: https://github.com/ros-industrial/universal_robot

Example driver clients are in the `examples/` directory. This driver supports jog, position, and trajectory
command modes for the standard `com.robotraconteur.robotics.robot.Robot` service type.

The [Robot Raconteur Training Simulator](https://github.com/robotraconteur-contrib/robotraconteur_training_sim) contains simulated UR5e robots in the multi-robot scene
and 2ur5e scene.

## Connection Info

The default connection information is as follows. These details may be changed using `--robotraconteur-*` command
line options when starting the service. Also see the
[Robot Raconteur Service Browser](https://github.com/robotraconteur/RobotRaconteur_ServiceBrowser) to detect
services on the network.

- URL: `rr+tcp://localhost:58652?service=robot`
- Device Name: `ur5e_robot` or the name of the robot in the configuration file
- Node Name: `ur_robot`
- Service Name: `robot`
- Root Object Type:
  - `com.robotraconteur.robotics.robot.Robot`

## Command Line Arguments

The following command line arguments are available:

* `--robot-info-file=` - The robot info file. Info files are available in the `config/` directory. See [robot info file documentation](https://github.com/robotraconteur/robotraconteur_standard_robdef/blob/master/docs/info_files/robot.md)
* `--robot-name=` - Overrides the robot device name. Defaults to `ur5e_robot` or the name in the robot info file.
* `--robot-hostname=` - The hostname or IP address of the robot. Defaults to `localhost`.
* `--driver-hostname=` - The hostname or IP address of the driver. Defaults to `localhost`. Only used for reverse socket connections.
* `--ur-script-file=` - The URScript file to run on the robot. By default a script for streaming position control is run.
* `--cb2-comat` - Enable CB2 compatibility mode. Only used for CB2 and CB3 robots. Uses reverse socket connections.

The [common Robot Raconteur node options](https://github.com/robotraconteur/robotraconteur/wiki/Command-Line-Options) are also available.

## Running the driver

Zip files containing the driver are available on the
[Releases](https://github.com/robotraconteur-contrib/URRobotRaconteurDriver/releases) page.
Download the zip file and extract it to a directory.
The .NET 6.0 runtime is required to run the driver. This driver will run on Windows and Linux.

The driver can be run using the following command:

```
URRobotRaconteurDriver.exe --robot-info-file=config/ur5e_robot_default_config.yml --robot-hostname=<robot_ip>
```

Use the `dotnet` command to run the driver on Linux:

```
dotnet URRobotRaconteurDriver.dll --robot-info-file=config/ur5e_robot_default_config.yml --robot-hostname=<robot_ip>
```

Use the appropriate robot info file for your robot.

## Running the driver using docker

Docker is the simplest way to run the driver on Linux. Use the following command to run the driver:

```bash
sudo docker run --rm --net=host -v /var/run/robotraconteur:/var/run/robotraconteur -v /var/lib/robotraconteur:/var/lib/robotraconteur -e ROBOT_HOSTNAME=192.168.55.2 -e ROBOT_INFO_FILE=/config/ur5e_robot_default_config.yml  --privileged  wasontech/ur-robotraconteur-driver
```

Replace `ROBOT_HOSTNAME` and `ROBOT_INFO_FILE` values with the appropriate values for your configuration.
The IP address of the robot for `ROBOT_HOSTNAME` can be found on the teach pendant on the "About" screen. It may
be necessary to mount a docker "volume" to access configuration yml files that are not included in the docker image.
See the docker documentation for instructions on mounting a local directory as a volume so it can be accessed
inside the docker.

## Building

### Ubuntu

Install the DotNet SDK version 6:

    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    sudo dpkg -i packages-microsoft-prod.deb
    sudo apt install dotnet-sdk-6

Install the Robot Raconteur C\# Native Library:

    sudo add-apt-repository ppa:robotraconteur/ppa
    sudo apt-get update
    sudo apt-get install librobotraconteur-net-native

Build the driver:

    cd src
    dotnet build --configuration Release -o build  URRobotRaconteurDriver.csproj

Run the driver:

    cd src
    dotnet URRobotRaconteurDriver.dll --robot-info-file=../config/ur5e_robot_default_config.yml --robot-hostname=<robot_ip>

Replace `<robot_ip>` with the IP address of the robot.

## Acknowledgment

This work was supported in part by the Advanced Robotics for Manufacturing ("ARM") Institute under Agreement Number W911NF-17-3-0004 sponsored by the Office of the Secretary of Defense. The views and conclusions contained in this document are those of the authors and should not be interpreted as representing the official policies, either expressed or implied, of either ARM or the Office of the Secretary of Defense of the U.S. Government. The U.S. Government is authorized to reproduce and distribute reprints for Government purposes, notwithstanding any copyright notation herein.

This work was supported in part by the New York State Empire State Development Division of Science, Technology and Innovation (NYSTAR) under contract C160142.

![](https://github.com/robotraconteur/robotraconteur/blob/master/docs/figures/arm_logo.jpg?raw=true)
![](https://github.com/robotraconteur/robotraconteur/blob/master/docs/figures/nys_logo.jpg?raw=true)
