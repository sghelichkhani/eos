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
#ifndef _MPIGLUE_H_
#define _MPIGLUE_H_

#include <stdint.h>

#if defined(_WIN32) || defined(WIN32) || defined(_WIN64) || defined(WIN64)
# if MPIGLUE_EXPORTS
#   define MPIGLUE_EXPORT __declspec(dllexport)
# else
#   define MPIGLUE_EXPORT __declspec(dllimport)
# endif
#else
# define MPIGLUE_EXPORT extern
#endif

#ifdef __cplusplus
extern "C" {
#endif

/**
 * Convert an MPI error code into a string.
 * @param errorcode Error code [in].
 * @param result Error message buffer [out].
 * @param resultlen Error message length [in: maximum buffer length, out: actual message length].
 * @return Zero iff the error message could be determined successfully.
 */
MPIGLUE_EXPORT int32_t MPIGlue_Error_string(int32_t errorcode, char *result, int32_t *resultlen);

/**
 * Initialize the MPI library and obtain a handle to the world
 * communicator. The error handler for the communicator is set to
 * ERRORS_RETURN.
 * @param newcomm World communicator [out]
 * @return MPI error code
 */
MPIGLUE_EXPORT int32_t MPIGlue_Comm_create_world(intptr_t *newcomm);

/**
 * Determine the number of processes connected to a communicator.
 * @param comm Communicator
 * @param size Number of connected processes [out]
 * @return MPI error code
 */
MPIGLUE_EXPORT int32_t MPIGlue_Comm_size(intptr_t comm, int32_t *size);

/**
 * Determine the rank of the current process within a communicator.
 * @param comm Communicator
 * @param size Rank of current process [out]
 * @return MPI error code
 */
MPIGLUE_EXPORT int32_t MPIGlue_Comm_rank(intptr_t comm, int32_t *rank);

/**
 * Abort all processes connected to a communicator.
 * @param comm Communicator
 * @param errorcode Hint for the operating system exit status
 * @return MPI error code
 */
MPIGLUE_EXPORT int32_t MPIGlue_Abort(intptr_t comm, int32_t errorcode);

/**
 * Wait until all processes connected to a communicator have passed
 * the barrier.
 * @param comm Communicator
 * @return MPI error code
 */
MPIGLUE_EXPORT int32_t MPIGlue_Barrier(intptr_t comm);

/**
 * Send an opaque byte buffer.
 * @param comm Communicator connected to the destination process
 * @param buffer Data to send
 * @param length Number of bytes to send
 * @param dest Rank of destination process
 * @param tag Message tag
 * @return MPI error code
 */
MPIGLUE_EXPORT int32_t MPIGlue_Send(intptr_t comm, const void *buffer, int32_t length, int32_t dest, int32_t tag);

/**
 * Begin reception of an opaque byte buffer.
 * @param comm Communicator connected to the source process.
 * @param message Message handle [out]
 * @param length Number of bytes in the message [out]
 * @param source Rank of source process, negative input matches any
 * rank [in: value to match, out: actual value]
 * @param tag Message tag, negative input matches any tag [in: value
 * to match, out: actual value]
 * @return MPI error code
 */
MPIGLUE_EXPORT int32_t MPIGlue_Recv_begin(intptr_t comm, intptr_t *message, int32_t *length, int32_t *source, int32_t *tag);

/**
 * Complete reception of an opaque byte buffer.
 * @param comm Communicator connected to the source process.
 * @param message Message handle, cleared on output [in/out]
 * @param buffer Buffer for received data
 * @param length Buffer capacity
 * @param source Rank of source process
 * @param tag Message tag
 * @return MPI error code
 */
MPIGLUE_EXPORT int32_t MPIGlue_Recv_end(intptr_t *message, void *buffer, int32_t length, int32_t source, int32_t tag);

/**
 * Destroy an MPI communicator or deinitialize the MPI library if the
 * world communicator is passed in.
 * @param comm Communicator to destroy, cleared on output [in/out]
 * @return MPI error code
 */
MPIGLUE_EXPORT int32_t MPIGlue_Comm_free(intptr_t *comm);

#ifdef __cplusplus
}
#endif

#endif /* _MPIGLUE_H_ */
