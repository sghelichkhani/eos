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
#include "eosmem.h"
#include "talloc.h"
#include <stdio.h>

static __thread void *current_eosmem = NULL;

void * __WINAPI eosmem_new(void) {
  return talloc_new(NULL);
}

void __WINAPI eosmem_destroy(void *pool) {
# ifdef DEBUG
  talloc_report_full(pool, stderr);
# endif
  talloc_free(pool);
}

void __WINAPI eosmem_use(void *pool) {
  current_eosmem = pool;
}

void *eosmem_alloc(size_t size) {
  return talloc_zero_size(current_eosmem, size);
}

void *eosmem_realloc(void *ptr, size_t size) {
  return talloc_realloc_fn(current_eosmem, ptr, size);
}

char *eosmem_strdup(const char *str) {
  return talloc_strdup(current_eosmem, str);
}

void eosmem_free(void *ptr) {
  talloc_free(ptr);
}
