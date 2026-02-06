FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.18 AS builder
ARG VERSION
RUN apk add --no-cache build-base linux-headers openmpi openmpi-dev expat-dev curl

ADD ./ /usr/src/

WORKDIR /tmp
ARG UDUNITS_VERSION=2.2.28
ARG UDUNITS_SHA256=590baec83161a3fd62c00efa66f6113cec8a7c461e3f61a5182167e0cc5d579e
RUN curl -L "https://artifacts.unidata.ucar.edu/repository/downloads-udunits/$UDUNITS_VERSION/udunits-$UDUNITS_VERSION.tar.gz" -o "udunits-$UDUNITS_VERSION.tar.gz" \
 && echo "$UDUNITS_SHA256 *udunits-$UDUNITS_VERSION.tar.gz" | sha256sum -c - \
 && tar -xzf "udunits-$UDUNITS_VERSION.tar.gz" -C /usr/src \
 && rm -f "udunits-$UDUNITS_VERSION.tar.gz"

WORKDIR /usr/src
RUN cd udunits-$UDUNITS_VERSION \
 && ./configure --prefix=/app --libdir=/app/bin --enable-shared --disable-static \
 && make && make install DESTDIR=/usr/src/udunits-linux-musl-x64
RUN make -C LPSolve CC=gcc CONF=Release ARCH=linux-musl-x64
RUN make -C MPIGlue CC=mpicc CONF=Release ARCH=linux-musl-x64
RUN dotnet publish EoS.CommandLineTool/EoS.CommandLineTool.fsproj -c Release -p:"Version=$VERSION" -f net8.0 -r linux-musl-x64 --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine3.18
MAINTAINER Thomas C. Chust <chust@web.de>
ARG DEFAULTDB=SLB11
RUN apk add --no-cache dropbear-ssh openmpi expat
ENV OMPI_ALLOW_RUN_AS_ROOT=1 OMPI_ALLOW_RUN_AS_ROOT_CONFIRM=1
COPY --from=builder /usr/src/LICENSE.txt /usr/src/ICON.svg /usr/src/ICON.ico /usr/src/README.md /app/
COPY --from=builder /usr/src/EoS.CommandLineTool/bin/Release/net8.0/linux-musl-x64/publish /app/bin
COPY --from=builder /usr/src/LPSolve/bin/Release/linux-musl-x64/* /app/bin/
COPY --from=builder /usr/src/MPIGlue/bin/Release/linux-musl-x64/* /app/bin/
COPY --from=builder /usr/src/udunits-linux-musl-x64/app/bin/* /app/bin/
COPY --from=builder /usr/src/udunits-linux-musl-x64/app/share/udunits /app/share/udunits
RUN cd /app && ln -s bin/eos eos \
 && cd bin && ln -s "${DEFAULTDB}.xml" default.xml
ENV NPROC=1
ENTRYPOINT ["/bin/sh", "-c", "exec /usr/bin/mpiexec -n \"$NPROC\" /app/eos \"$@\"", "--"]
