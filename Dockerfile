FROM ubuntu:jammy AS build-env
WORKDIR /app
RUN apt update && apt install wget sudo software-properties-common -y
RUN apt install dotnet-sdk-6.0 -y
COPY . ./
RUN dotnet publish /p:Configuration=Release /p:Platform="Any CPU" --framework=net6.0 -o output

FROM ubuntu:jammy
WORKDIR /app
COPY --from=build-env /app/output /opt/ur_robotraconteur_driver
COPY ./config/*.yml /config/

ENV DEBIAN_FRONTEND=noninteractive
ENV ROBOT_INFO_FILE=/config/ur5e_robot_default_config.yml
ENV ROBOT_HOSTNAME=127.0.0.1

RUN apt-get update && apt-get install wget sudo software-properties-common -y

RUN sudo apt install dotnet-runtime-6.0 -y

RUN sudo add-apt-repository ppa:robotraconteur/ppa -y \
    && sudo apt-get update \
    && sudo apt-get install librobotraconteur-net-native -y

CMD exec /opt/ur_robotraconteur_driver/URRobotRaconteurDriver --robot-info-file=$ROBOT_INFO_FILE --robot-hostname=$ROBOT_HOSTNAME
