# The dedicated server image (SERVER.md). The build context is a Linux server build: Build/LinuxServer from
# docker/build-server.sh (GoF2Server.x86_64), or an unpacked Linux release (BINARY=GoF2Remake.x86_64, its -server mode).
# libicu: IL2CPP's globalization (without it the player dies at start); the data folder (-profiledir) is the volume /data.
FROM ubuntu:26.04
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates libglib2.0-0t64 libicu78 \
 && rm -rf /var/lib/apt/lists/* && useradd -m -u 10001 gof2 && install -d -o gof2 /data
ARG BINARY=GoF2Server.x86_64
COPY . /opt/gof2
RUN ln -s "/opt/gof2/$BINARY" /opt/gof2/server
USER gof2
WORKDIR /opt/gof2
VOLUME /data
EXPOSE 7777/udp
ENTRYPOINT ["./server", "-batchmode", "-nographics", "-server", "-logFile", "-", "-profiledir", "/data"]
