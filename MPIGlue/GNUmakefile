HEADERS = \
	mpiglue.h

SOURCES = \
	mpiglue.c

DEFINES = \
	MPIGLUE_EXPORTS

CC = mpicc
CONF = Debug
ARCH = $(shell uname -sm | tr 'A-Z ' 'a-z-')

ifeq ($(CONF),Release)
	OPTIMIZE = 2
else
	OPTIMIZE = 1
	DEFINES += DEBUG
endif

CFLAGS += -shared -O$(OPTIMIZE)
CPPFLAGS += $(addprefix -D,$(DEFINES))
LDFLAGS =
LDLIBS =

OS := $(firstword $(subst -, ,$(ARCH)))
CPU := $(lastword $(subst -, ,$(ARCH)))
ifeq ($(OS),linux)
	LIB = lib
	DLL = .so
	CFLAGS += -fPIC
else ifeq ($(OS),osx)
	LIB = lib
	DLL = .dylib
	CFLAGS += -fPIC
ifeq ($(CPU),x64)
	CFLAGS += -arch x86_64
else
	CFLAGS += -arch $(CPU)
endif
else
	LIB =
	DLL = .dll
	CFLAGS += -I'$(MSMPI_INC)'
	LDFLAGS += -static -Wl,--no-undefined
ifeq ($(CPU),x86)
	CFLAGS += -m32
	LDFLAGS += -L'$(MSMPI_LIB32)' -lmsmpi
else
	CFLAGS += -m64
	LDFLAGS += -L'$(MSMPI_LIB64)' -lmsmpi
endif
endif

bin/$(CONF)/$(ARCH)/$(LIB)mpiglue$(DLL): bin/$(CONF)/$(ARCH) $(HEADERS) $(SOURCES)
	$(CC) $(CFLAGS) $(CPPFLAGS) $(SOURCES) -o $@ $(LDFLAGS) $(LDLIBS)

bin/$(CONF)/$(ARCH):
	mkdir -p $@
