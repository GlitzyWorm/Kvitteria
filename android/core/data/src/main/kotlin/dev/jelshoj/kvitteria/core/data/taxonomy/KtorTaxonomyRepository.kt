/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.taxonomy

import dev.jelshoj.kvitteria.core.data.network.ApiHttpClient
import dev.jelshoj.kvitteria.core.data.network.safeCall
import dev.jelshoj.kvitteria.core.data.receipt.PagedResultDto
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.map
import dev.jelshoj.kvitteria.core.domain.onSuccess
import dev.jelshoj.kvitteria.core.domain.taxonomy.CanonicalItem
import dev.jelshoj.kvitteria.core.domain.taxonomy.Category
import dev.jelshoj.kvitteria.core.domain.taxonomy.Meal
import dev.jelshoj.kvitteria.core.domain.taxonomy.TaxonomyRepository
import io.ktor.client.request.get
import io.ktor.client.request.parameter
import io.ktor.client.request.patch
import io.ktor.client.request.post
import io.ktor.client.request.setBody
import io.ktor.http.ContentType
import io.ktor.http.contentType
import kotlinx.serialization.Serializable

@Serializable
data class CategoryDto(
    val id: String,
    val displayName: String,
    val parentCategoryId: String?,
    val childCategories: List<CategoryDto>,
    val sortOrder: Int,
)

@Serializable
data class MealDto(
    val id: String,
    val displayName: String,
    val sortOrder: Int,
    val tracksDayCoverage: Boolean,
)

@Serializable
data class CanonicalItemDto(
    val id: String,
    val displayName: String,
    val categoryId: String?,
    val defaultMealId: String?,
)

@Serializable
data class CreateCategoryDto(val displayName: String, val parentCategoryId: String?)

@Serializable
data class UpdateCategoryDto(
    val displayName: String?,
    val parentCategoryId: String?,
    val clearParent: Boolean,
)

fun CategoryDto.toCategory(): Category =
    Category(id, displayName, parentCategoryId, childCategories.map { it.toCategory() }, sortOrder)

fun MealDto.toMeal(): Meal = Meal(id, displayName, sortOrder, tracksDayCoverage)

fun CanonicalItemDto.toCanonicalItem(): CanonicalItem = CanonicalItem(id, displayName, categoryId, defaultMealId)

class KtorTaxonomyRepository(private val api: ApiHttpClient) : TaxonomyRepository {
    @Volatile private var categories: List<Category>? = null

    @Volatile private var meals: List<Meal>? = null

    @Volatile private var items: List<CanonicalItem>? = null

    override suspend fun categories(force: Boolean): Result<List<Category>, ApiError> {
        categories?.takeUnless { force }?.let { return Result.Success(it) }
        return safeCall<List<CategoryDto>> { api.client.get("/api/categories") }
            .map { list -> list.map { it.toCategory() } }
            .onSuccess { categories = it }
    }

    override suspend fun meals(force: Boolean): Result<List<Meal>, ApiError> {
        meals?.takeUnless { force }?.let { return Result.Success(it) }
        return safeCall<List<MealDto>> { api.client.get("/api/meals") }
            .map { list -> list.map { it.toMeal() } }
            .onSuccess { meals = it }
    }

    override suspend fun items(force: Boolean): Result<List<CanonicalItem>, ApiError> {
        items?.takeUnless { force }?.let { return Result.Success(it) }

        val collected = mutableListOf<CanonicalItem>()
        var page = 1
        while (true) {
            val result = safeCall<PagedResultDto<CanonicalItemDto>> {
                api.client.get("/api/canonical-items") {
                    parameter("page", page)
                    parameter("pageSize", ITEM_PAGE_SIZE)
                }
            }
            when (result) {
                is Result.Error -> return result
                is Result.Success -> {
                    collected += result.data.items.map { it.toCanonicalItem() }
                    val done = result.data.items.isEmpty() || collected.size >= result.data.totalCount
                    if (done) {
                        items = collected
                        return Result.Success(collected)
                    }
                }
            }
            page++
        }
    }

    override suspend fun createCategory(name: String, parentId: String?): Result<Category, ApiError> =
        safeCall<CategoryDto> {
            api.client.post("/api/categories") {
                contentType(ContentType.Application.Json)
                setBody(CreateCategoryDto(name, parentId))
            }
        }.map { it.toCategory() }.onSuccess { categories = null }

    override suspend fun updateCategory(id: String, name: String, parentId: String?): Result<Category, ApiError> =
        safeCall<CategoryDto> {
            api.client.patch("/api/categories/$id") {
                contentType(ContentType.Application.Json)
                setBody(UpdateCategoryDto(name, parentId, clearParent = parentId == null))
            }
        }.map { it.toCategory() }.onSuccess { categories = null }

    override fun invalidateItems() {
        items = null
    }

    private companion object {
        const val ITEM_PAGE_SIZE = 100
    }
}
