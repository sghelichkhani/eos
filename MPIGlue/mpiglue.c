/*
 * This file is part of EoS
 * Copyright (c) 2009-2014 Thomas Chust
 *                         Bayerisches Geoinstitut, Bayreuth
 *                         Ludwig-Maximilians-Universität, München
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */
#include "mpiglue.h"
#include <stddef.h>
#include <mpi.h>

#if MPI_VERSION >= 3
# define ENABLE_MPROBE
#endif

#define BEGIN_CHECKED int _errorcode = MPI_SUCCESS
#define CHECK(expr) ({ if ((_errorcode = (expr)) != MPI_SUCCESS) goto _error; })
#define END_CHECKED _error: return _errorcode;

int32_t MPIGlue_Error_string(int32_t errorcode, char *result, int32_t *resultlen) {
  return MPI_Error_string(errorcode, result, resultlen);
}

int32_t MPIGlue_Comm_create_world(intptr_t *newcomm) {
  int level;
  BEGIN_CHECKED;
  CHECK(MPI_Init_thread(NULL, NULL, MPI_THREAD_SERIALIZED, &level));
  CHECK(MPI_Comm_set_errhandler(MPI_COMM_WORLD, MPI_ERRORS_RETURN));
  *newcomm = (intptr_t) MPI_COMM_WORLD;
  END_CHECKED;
}

int32_t MPIGlue_Comm_size(intptr_t comm, int32_t *psize) {
  BEGIN_CHECKED;
  int size = *psize;
  CHECK(MPI_Comm_size((MPI_Comm) comm, &size));
  *psize = size;
  END_CHECKED;
}

int32_t MPIGlue_Comm_rank(intptr_t comm, int32_t *prank) {
  BEGIN_CHECKED;
  int rank = *prank;
  CHECK(MPI_Comm_rank((MPI_Comm) comm, &rank));
  *prank = rank;
  END_CHECKED;
}

int32_t MPIGlue_Abort(intptr_t comm, int32_t errorcode) {
  return MPI_Abort((MPI_Comm) comm, errorcode);
}

int32_t MPIGlue_Barrier(intptr_t comm) {
  return MPI_Barrier((MPI_Comm) comm);
}

int32_t MPIGlue_Send(intptr_t comm, const void *buffer, int32_t length, int32_t dest, int32_t tag) {
  return MPI_Send((void *) buffer, length, MPI_BYTE, dest, tag, (MPI_Comm) comm);
}

int32_t MPIGlue_Recv_begin(intptr_t comm, intptr_t *pmessage, int32_t *plength, int32_t *psource, int32_t *ptag) {
  MPI_Status status;
  BEGIN_CHECKED;
  int length = *plength;
  status.MPI_SOURCE = *psource;
  status.MPI_TAG = *ptag;
  if (status.MPI_SOURCE < 0) status.MPI_SOURCE = MPI_ANY_SOURCE;
  if (status.MPI_TAG < 0) status.MPI_TAG = MPI_ANY_TAG;
# ifdef ENABLE_MPROBE
  MPI_Message message = (MPI_Message) *pmessage;
  CHECK(MPI_Mprobe(status.MPI_SOURCE, status.MPI_TAG, (MPI_Comm) comm, &message, &status));
  *pmessage = (intptr_t) message;
# else
  CHECK(MPI_Probe(status.MPI_SOURCE, status.MPI_TAG, (MPI_Comm) comm, &status));
  *pmessage = comm;
# endif
  CHECK(MPI_Get_count(&status, MPI_BYTE, &length));
  *plength = length;
  *psource = status.MPI_SOURCE;
  *ptag = status.MPI_TAG;
  END_CHECKED;
}

int32_t MPIGlue_Recv_end(intptr_t *pmessage, void *buffer, int32_t length, int32_t source, int32_t tag) {
  MPI_Status status;
  BEGIN_CHECKED;
# ifdef ENABLE_MPROBE
  MPI_Message message = (MPI_Message) *pmessage;
  CHECK(MPI_Mrecv(buffer, length, MPI_BYTE, &message, &status));
  *pmessage = (intptr_t) message;
# else
  CHECK(MPI_Recv(buffer, length, MPI_BYTE, source, tag, (MPI_Comm) *pmessage, &status));
  *pmessage = (intptr_t) NULL;
# endif
  END_CHECKED;
}

int MPIGlue_Comm_free(intptr_t *pcomm) {
  BEGIN_CHECKED;
  MPI_Comm comm = (MPI_Comm) *pcomm;
  if (comm == MPI_COMM_WORLD) {
    CHECK(MPI_Finalize());
    comm = (MPI_Comm) (intptr_t) NULL;
  }
  else {
    CHECK(MPI_Comm_free(&comm));
  }
  *pcomm = (intptr_t) comm;
  END_CHECKED;
}
