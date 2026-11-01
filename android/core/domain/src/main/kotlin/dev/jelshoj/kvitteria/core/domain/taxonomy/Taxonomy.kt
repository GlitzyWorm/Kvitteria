/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.domain.taxonomy

import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result

data class Category(
    val id: String,
    val name: String,
    val parentId: String?,
    val children: List<Category>,
    val sortOrder: Int,
)

data class Meal(
    val id: String,
    val name: String,
    val sortOrder: Int,
    val tracksDayCoverage: Boolean,
)

data class CanonicalItem(
    val id: String,
    val name: String,
    val categoryId: String?,
    val defaultMealId: String?,
)

/** Cached in memory for the session and invalidated by writes in the app. [force] skips the cache. */
interface TaxonomyRepository {
    suspend fun categories(force: Boolean = false): Result<List<Category>, ApiError>

    suspend fun meals(force: Boolean = false): Result<List<Meal>, ApiError>

    /** Every item, paged through at 100 per page. */
    suspend fun items(force: Boolean = false): Result<List<CanonicalItem>, ApiError>

    suspend fun createCategory(name: String, parentId: String?): Result<Category, ApiError>

    suspend fun updateCategory(id: String, name: String, parentId: String?): Result<Category, ApiError>

    /** Forgets the item list, because a sort may have created an item. */
    fun invalidateItems()
}
