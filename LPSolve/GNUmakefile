HEADERS = \
	eosmem.h \
	talloc.h \
	colamd.h \
	commonlib.h \
	ini.h \
	lp_crash.h \
	lp_Hash.h \
	lp_lib.h \
	lp_LUSOL.h \
	lp_matrix.h \
	lp_MDO.h \
	lp_mipbb.h \
	lp_MPS.h \
	lp_presolve.h \
	lp_price.h \
	lp_pricePSE.h \
	lp_report.h \
	lp_rlp.h \
	lp_scale.h \
	lp_simplex.h \
	lp_SOS.h \
	lp_utils.h \
	lp_wlp.h \
	lusol.h \
	mmio.h \
	myblas.h \
	yacc_read.h \
	lp_bit.h \
	lp_BFP.h \
	lp_types.h \
	lp_BFP1.c \
	lp_BFP2.c \
	lpkit.h \
	lusol1.c \
	lusol2.c \
	lusol6a.c \
	lusol7a.c \
	lusol8a.c \
	lusol6l0.c \
	lusol6u.c \

SOURCES = \
	eosmem.c \
	talloc.c \
	colamd.c \
	commonlib.c \
	ini.c \
	lp_crash.c \
	lp_Hash.c \
	lp_lib.c \
	lp_LUSOL.c \
	lp_matrix.c \
	lp_MDO.c \
	lp_mipbb.c \
	lp_MPS.c \
	lp_params.c \
	lp_presolve.c \
	lp_price.c \
	lp_pricePSE.c \
	lp_report.c \
	lp_rlp.c \
	lp_scale.c \
	lp_simplex.c \
	lp_SOS.c \
	lp_utils.c \
	lp_wlp.c \
	lusol.c \
	mmio.c \
	myblas.c \
	yacc_read.c \

DEFINES = \
	YY_NEVER_INTERACTIVE \
	PARSER_LP \
	INTEGERTIME \
	INVERSE_ACTIVE=INVERSE_LUSOL \
	RoleIsExternalInvEngine \
	LoadInverseLib=0 \
	LoadLanguageLib=0 \
	LoadableBlasLib=0 \

CC = cc
CONF = Debug
ARCH = $(shell uname -sm | tr 'A-Z ' 'a-z-')

ifeq ($(CONF),Release)
	OPTIMIZE = 2
else
	OPTIMIZE = 1
	DEFINES += DEBUG
endif

CFLAGS += -shared -O$(OPTIMIZE)
CPPFLAGS += -D'INLINE=static inline' $(addprefix -D,$(DEFINES))
LDFLAGS =
LDLIBS = -lm

OS := $(firstword $(subst -, ,$(ARCH)))
CPU := $(lastword $(subst -, ,$(ARCH)))
ifeq ($(OS),linux)
	LIB = lib
	DLL = .so
	CFLAGS += -fPIC
else ifeq ($(OS),osx)
	LIB = lib
	DLL = .dylib
	DEFINES += NO_STRNLEN
	CFLAGS += -fPIC -pthread
ifeq ($(CPU),x64)
	CFLAGS += -arch x86_64
else
	CFLAGS += -arch $(CPU)
endif
else
	LIB =
	DLL = .dll
	DEFINES += _USRDLL
	LDFLAGS += -static -Wl,--allow-multiple-definition,--no-undefined,--add-stdcall-alias
ifeq ($(CPU),x86)
	CFLAGS += -m32
else
	CFLAGS += -m64
endif
endif

bin/$(CONF)/$(ARCH)/$(LIB)lpsolve55$(DLL): bin/$(CONF)/$(ARCH) $(HEADERS) $(SOURCES)
	$(CC) $(CFLAGS) $(CPPFLAGS) $(SOURCES) -o $@ $(LDFLAGS) $(LDLIBS)

bin/$(CONF)/$(ARCH):
	mkdir -p $@
