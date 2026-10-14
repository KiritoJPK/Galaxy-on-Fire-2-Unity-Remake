# The Unity editor that builds the Linux dedicated server from this checkout (docker/build-server.sh): GameCI's base
# image with the editor, Linux IL2CPP and Linux Dedicated Server modules from Unity's download server. VERSION and
# CHANGESET are ProjectSettings/ProjectVersion.txt's; build-server.sh passes them. clang / lld: the IL2CPP link step.
FROM unityci/base:ubuntu-3
ARG VERSION
ARG CHANGESET
RUN apt-get -q update && apt-get -q install -y --no-install-recommends build-essential clang lld xz-utils \
 && rm -rf /var/lib/apt/lists/*
RUN set -e; base=https://download.unity3d.com/download_unity/$CHANGESET; mkdir -p /opt/unity; \
    for p in LinuxEditorInstaller/Unity-$VERSION.tar.xz \
             LinuxEditorTargetInstaller/UnitySetup-Linux-IL2CPP-Support-for-Editor-$VERSION.tar.xz \
             LinuxEditorTargetInstaller/UnitySetup-Linux-Server-Support-for-Editor-$VERSION.tar.xz; do \
      curl -fsSL "$base/$p" | tar -xJ -C /opt/unity; done; \
    test -x /opt/unity/Editor/Unity
# On a Linux host the IL2CPP toolchain unpacks only with this /bin/sh (see unity-sh).
COPY unity-sh /usr/local/bin/unity-sh
RUN ln -sf /usr/local/bin/unity-sh /bin/sh
