# URRobotRaconteurDriver

## Introduction

Robot Raconteur standard robot driver for UR robots. Supports CB2, CB3, and e-Series robots.

Reverse Socket CB2 compatibilty mode is based on the ROS Industrial UR driver project: https://github.com/ros-industrial/universal_robot

## Running With Docker

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

    dotnet build --configuration Release -o build  URRobotRaconteurDriver.csproj

Run the driver:

    dotnet URRobotRaconteurDriver.dll --robot-info-file=../ur5e_robot_default_config.yml --robot-hostname=<robot_ip>

Replace `<robot_ip>` with the IP address of the robot.