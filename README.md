# URRobotRaconteurDriver

## Introduction

Robot Raconteur standard robot driver for UR robots. Supports CB2, CB3, and e-Series robots.

Reverse Socket CB2 compatibilty mode is based on the ROS Industrial UR driver project: https://github.com/ros-industrial/universal_robot

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