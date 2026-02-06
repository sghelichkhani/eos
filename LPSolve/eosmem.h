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
#ifndef _eosmem_h_
#define _eosmem_h_

#include "lp_types.h"

void __EXPORT_TYPE * __WINAPI eosmem_new(void);
void __EXPORT_TYPE __WINAPI eosmem_destroy(void *pool);
void __EXPORT_TYPE __WINAPI eosmem_use(void *pool);

#include <string.h>
#include <stdlib.h>

#ifdef __cplusplus
extern "C" {
#endif

extern void *eosmem_alloc(size_t size);
extern void *eosmem_realloc(void *ptr, size_t size);
extern char *eosmem_strdup(const char *str);
extern void eosmem_free(void *ptr);

#ifdef __cplusplus
}
#endif

#ifdef malloc
#undef malloc
#endif
#ifdef calloc
#undef calloc
#endif
#ifdef realloc
#undef realloc
#endif
#ifdef strdup
#undef strdup
#endif
#ifdef free
#undef free
#endif

#define malloc eosmem_alloc
#define calloc(nmemb, size) (eosmem_alloc((nmemb) * (size)))
#define realloc eosmem_realloc
#define strdup eosmem_strdup
#define free eosmem_free

#endif /* _eosmem_h_ */
