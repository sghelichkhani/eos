FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS builder
ARG VERSION
RUN apt-get update && apt-get install -y --no-install-recommends libc6-dev libmpich-dev mpich make gcc && rm -rf /var/lib/apt/lists/*
ADD ./ /usr/src/
WORKDIR /usr/src
RUN make -C LPSolve CC=gcc CONF=Release ARCH=linux-x64
RUN make -C MPIGlue CC=mpicc CONF=Release ARCH=linux-x64
RUN dotnet publish EoS.CommandLineTool/EoS.CommandLineTool.fsproj -c Release -p:"Version=$VERSION" -f net8.0 -r linux-x64 --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:8.0-jammy
MAINTAINER Thomas C. Chust <chust@web.de>
ARG DEFAULTDB=SLB11
RUN apt-get update && apt-get install -y --no-install-recommends libudunits2-0 libmpich12 mpich && rm -rf /var/lib/apt/lists/*
COPY --from=builder /usr/src/LICENSE.txt /usr/src/ICON.svg /usr/src/ICON.ico /usr/src/README.md /app/
COPY --from=builder /usr/src/EoS.CommandLineTool/bin/Release/net8.0/linux-x64/publish /app/bin
COPY --from=builder /usr/src/LPSolve/bin/Release/linux-x64/* /app/bin/
COPY --from=builder /usr/src/MPIGlue/bin/Release/linux-x64/* /app/bin/
RUN ln -s /usr/lib/x86_64-linux-gnu/libudunits2.so.0 /app/bin/libudunits2.so \
 && cd /app && ln -s bin/eos eos \
 && cd bin && ln -s "${DEFAULTDB}.xml" default.xml
ENV NPROC=1
ENTRYPOINT ["/bin/sh", "-c", "exec /usr/bin/mpiexec -n \"$NPROC\" /app/eos \"$@\"", "--"]
