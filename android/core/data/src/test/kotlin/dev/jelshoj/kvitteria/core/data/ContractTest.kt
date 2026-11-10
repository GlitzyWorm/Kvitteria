/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

@file:OptIn(ExperimentalSerializationApi::class)

package dev.jelshoj.kvitteria.core.data

import assertk.assertThat
import assertk.assertions.containsExactly
import assertk.assertions.isEmpty
import dev.jelshoj.kvitteria.core.data.insights.CategoryAmountDto
import dev.jelshoj.kvitteria.core.data.insights.CategoryBreakdownDto
import dev.jelshoj.kvitteria.core.data.insights.MealCoverageMonthDto
import dev.jelshoj.kvitteria.core.data.insights.MealCoverageStatsDto
import dev.jelshoj.kvitteria.core.data.insights.PriceHistoryDto
import dev.jelshoj.kvitteria.core.data.insights.PriceMonthDto
import dev.jelshoj.kvitteria.core.data.insights.PricedItemDto
import dev.jelshoj.kvitteria.core.data.insights.PricedNameDto
import dev.jelshoj.kvitteria.core.data.insights.SpendingSummaryDto
import dev.jelshoj.kvitteria.core.data.insights.SpendingTrendMonthDto
import dev.jelshoj.kvitteria.core.data.member.AppUserDto
import dev.jelshoj.kvitteria.core.data.member.CurrentUserDto
import dev.jelshoj.kvitteria.core.data.receipt.AcceptDifferenceDto
import dev.jelshoj.kvitteria.core.data.receipt.CreateReceiptLineDto
import dev.jelshoj.kvitteria.core.data.receipt.MealCoverageEntryDto
import dev.jelshoj.kvitteria.core.data.receipt.PatchReceiptDto
import dev.jelshoj.kvitteria.core.data.receipt.ReceiptAcceptanceDto
import dev.jelshoj.kvitteria.core.data.receipt.PagedResultDto
import dev.jelshoj.kvitteria.core.data.receipt.PatchReceiptLineDto
import dev.jelshoj.kvitteria.core.data.receipt.PutReceiptLineCategoryDto
import dev.jelshoj.kvitteria.core.data.receipt.PutReceiptLineMealDto
import dev.jelshoj.kvitteria.core.data.receipt.PutReceiptMealCoverageDto
import dev.jelshoj.kvitteria.core.data.receipt.ReceiptDto
import dev.jelshoj.kvitteria.core.data.receipt.ReceiptLineDto
import dev.jelshoj.kvitteria.core.data.receipt.ReceiptMealCoverageDto
import dev.jelshoj.kvitteria.core.data.taxonomy.CanonicalItemDto
import dev.jelshoj.kvitteria.core.data.taxonomy.CategoryDto
import dev.jelshoj.kvitteria.core.data.taxonomy.CreateCategoryDto
import dev.jelshoj.kvitteria.core.data.taxonomy.MealDto
import dev.jelshoj.kvitteria.core.data.session.VersionDto
import dev.jelshoj.kvitteria.core.data.taxonomy.UpdateCategoryDto
import dev.jelshoj.kvitteria.core.data.update.UpdateStatusDto
import dev.jelshoj.kvitteria.core.data.work.FailedCountDto
import dev.jelshoj.kvitteria.core.data.work.UnassignedLineCountDto
import dev.jelshoj.kvitteria.feature.capture.data.ReceiptUploadDto
import dev.jelshoj.kvitteria.feature.pipeline.data.MailboxActivityDto
import dev.jelshoj.kvitteria.feature.pipeline.data.MailboxHealthDto
import dev.jelshoj.kvitteria.feature.pipeline.data.PipelineStatusDto
import dev.jelshoj.kvitteria.feature.pipeline.data.ReceiptFailureDto
import dev.jelshoj.kvitteria.feature.settings.data.CreateAppUserDto
import dev.jelshoj.kvitteria.feature.settings.data.CreateMailboxDto
import dev.jelshoj.kvitteria.feature.settings.data.MailboxDto
import dev.jelshoj.kvitteria.feature.settings.data.MemberPermissionsDto
import dev.jelshoj.kvitteria.feature.settings.data.OllamaModelsDto
import dev.jelshoj.kvitteria.feature.settings.data.PatchAppUserDto
import dev.jelshoj.kvitteria.feature.settings.data.PipelineSettingsDto
import dev.jelshoj.kvitteria.feature.settings.data.PutMailboxDto
import dev.jelshoj.kvitteria.feature.settings.data.PutMemberPermissionsDto
import dev.jelshoj.kvitteria.feature.sort.data.SortReceiptLineDto
import dev.jelshoj.kvitteria.feature.sort.data.SortReceiptLineResultDto
import dev.jelshoj.kvitteria.feature.sort.data.UnassignedLineDto
import java.io.File
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.KSerializer
import kotlinx.serialization.Serializable
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.jupiter.api.Test

/**
 * The app can't compile the server's DTOs any more, so this catches what a shared project used to catch
 * at compile time. It compares names and nullability only, because .NET 10 describes `decimal` as
 * number-or-string.
 */
class ContractTest {
    private class Model(val schema: String, val serializer: KSerializer<*>, val sent: Boolean = false)

    /** A model the app sends can't be nullable-mismatched: it decides for itself what to put there. */
    private fun sent(schema: String, serializer: KSerializer<*>) = Model(schema, serializer, sent = true)

    private val models = listOf(
        Model("ReceiptDto", ReceiptDto.serializer()),
        Model("ReceiptLineDto", ReceiptLineDto.serializer()),
        Model("ReceiptAcceptanceDto", ReceiptAcceptanceDto.serializer()),
        sent("PatchReceiptDto", PatchReceiptDto.serializer()),
        sent("CreateReceiptLineDto", CreateReceiptLineDto.serializer()),
        sent("AcceptDifferenceDto", AcceptDifferenceDto.serializer()),
        Model("ReceiptMealCoverageDto", ReceiptMealCoverageDto.serializer()),
        Model("PagedResultOfReceiptDto", PagedResultDto.serializer(ReceiptDto.serializer())),
        Model("PagedResultOfCanonicalItemDto", PagedResultDto.serializer(CanonicalItemDto.serializer())),
        Model("PagedResultOfPricedItemDto", PagedResultDto.serializer(PricedItemDto.serializer())),
        sent("PutReceiptLineCategoryDto", PutReceiptLineCategoryDto.serializer()),
        sent("PutReceiptLineMealDto", PutReceiptLineMealDto.serializer()),
        sent("PatchReceiptLineDto", PatchReceiptLineDto.serializer()),
        sent("PutReceiptMealCoverageDto", PutReceiptMealCoverageDto.serializer()),
        sent("MealCoverageEntryDto", MealCoverageEntryDto.serializer()),
        Model("CategoryDto", CategoryDto.serializer()),
        Model("MealDto", MealDto.serializer()),
        Model("CanonicalItemDto", CanonicalItemDto.serializer()),
        sent("CreateCategoryDto", CreateCategoryDto.serializer()),
        sent("UpdateCategoryDto", UpdateCategoryDto.serializer()),
        Model("CurrentUserDto", CurrentUserDto.serializer()),
        Model("AppUserDto", AppUserDto.serializer()),
        Model("SpendingSummaryDto", SpendingSummaryDto.serializer()),
        Model("CategoryAmountDto", CategoryAmountDto.serializer()),
        Model("CategoryBreakdownDto", CategoryBreakdownDto.serializer()),
        Model("SpendingTrendMonthDto", SpendingTrendMonthDto.serializer()),
        Model("PricedItemDto", PricedItemDto.serializer()),
        Model("PricedNameDto", PricedNameDto.serializer()),
        Model("PriceMonthDto", PriceMonthDto.serializer()),
        Model("PriceHistoryDto", PriceHistoryDto.serializer()),
        Model("MealCoverageMonthDto", MealCoverageMonthDto.serializer()),
        Model("MealCoverageStatsDto", MealCoverageStatsDto.serializer()),
        Model("VersionDto", VersionDto.serializer()),
        Model("UpdateStatusDto", UpdateStatusDto.serializer()),
        Model("UnassignedLineCountDto", UnassignedLineCountDto.serializer()),
        Model("PipelineStatusDto", FailedCountDto.serializer()),
        Model("UnassignedLineDto", UnassignedLineDto.serializer()),
        Model("PagedResultOfUnassignedLineDto", PagedResultDto.serializer(UnassignedLineDto.serializer())),
        sent("SortReceiptLineDto", SortReceiptLineDto.serializer()),
        Model("SortReceiptLineResultDto", SortReceiptLineResultDto.serializer()),
        Model("PipelineStatusDto", PipelineStatusDto.serializer()),
        Model("MailboxActivityDto", MailboxActivityDto.serializer()),
        Model("ReceiptFailureDto", ReceiptFailureDto.serializer()),
        Model("PagedResultOfReceiptFailureDto", PagedResultDto.serializer(ReceiptFailureDto.serializer())),
        Model("MailboxDto", MailboxHealthDto.serializer()),
        Model("MailboxDto", MailboxDto.serializer()),
        sent("CreateMailboxDto", CreateMailboxDto.serializer()),
        sent("PutMailboxDto", PutMailboxDto.serializer()),
        Model("PipelineSettingsDto", PipelineSettingsDto.serializer()),
        sent("PutPipelineSettingsDto", PipelineSettingsDto.serializer()),
        Model("OllamaModelsDto", OllamaModelsDto.serializer()),
        sent("CreateAppUserDto", CreateAppUserDto.serializer()),
        sent("PatchAppUserDto", PatchAppUserDto.serializer()),
        Model("MemberPermissionsDto", MemberPermissionsDto.serializer()),
        sent("PutMemberPermissionsDto", PutMemberPermissionsDto.serializer()),
        Model("ReceiptUploadDto", ReceiptUploadDto.serializer()),
    )

    private val schemas: JsonObject by lazy {
        val file = generateSequence(File("").absoluteFile) { it.parentFile }
            .map { File(it, "docs/openapi/kvitteria.json") }
            .first { it.exists() }
        Json.parseToJsonElement(file.readText()).jsonObject.getValue("components").jsonObject.getValue("schemas").jsonObject
    }

    private fun JsonElement.isNullable(): Boolean {
        val schema = this as? JsonObject ?: return false
        val type = schema["type"]
        val inType = when (type) {
            is JsonArray -> type.any { (it as? JsonPrimitive)?.content == "null" }
            is JsonPrimitive -> type.content == "null"
            else -> false
        }
        return inType || listOf("oneOf", "anyOf").any { key ->
            (schema[key] as? JsonArray).orEmpty().any { it.isNullable() }
        }
    }

    private fun violations(name: String, descriptor: SerialDescriptor, sent: Boolean, schema: JsonObject): List<String> {
        val properties = schema["properties"]?.jsonObject.orEmpty()
        val required = schema["required"]?.jsonArray.orEmpty().map { it.jsonPrimitive.content }

        return (0 until descriptor.elementsCount).mapNotNull { index ->
            val field = descriptor.getElementName(index)
            val property = properties[field]
            val appRequires = !descriptor.isElementOptional(index) && !descriptor.getElementDescriptor(index).isNullable
            when {
                property == null -> "$name.$field is not in the document"
                sent || !appRequires -> null
                property.isNullable() -> "$name.$field is nullable in the document and required here"
                field !in required -> "$name.$field is optional in the document and required here"
                else -> null
            }
        }
    }

    @Test
    fun `every wire model matches its schema`() {
        val problems = models.flatMap { model ->
            val schema = schemas[model.schema]?.jsonObject
            if (schema == null) {
                listOf("${model.schema} is not in the document")
            } else {
                violations(model.schema, model.serializer.descriptor, model.sent, schema)
            }
        }
        assertThat(problems).isEmpty()
    }

    @Serializable
    private data class Reader(val id: String, val renamedOnTheServer: String)

    @Serializable
    private data class Wanting(val id: String, val nullableThere: String)

    private fun schema(json: String) = Json.parseToJsonElement(json).jsonObject

    @Test
    fun `a renamed field names the model and field`() {
        val found = violations(
            "ReaderDto",
            Reader.serializer().descriptor,
            sent = false,
            schema("""{"required":["id","newName"],"properties":{"id":{"type":"string"},"newName":{"type":"string"}}}"""),
        )
        assertThat(found).containsExactly("ReaderDto.renamedOnTheServer is not in the document")
    }

    @Test
    fun `a nullable field the app requires is reported`() {
        val found = violations(
            "WantingDto",
            Wanting.serializer().descriptor,
            sent = false,
            schema(
                """{"required":["id","nullableThere"],
                    "properties":{"id":{"type":"string"},"nullableThere":{"type":["null","string"]}}}""",
            ),
        )
        assertThat(found).containsExactly("WantingDto.nullableThere is nullable in the document and required here")
    }

    @Test
    fun `a field the server added is ignored`() {
        val found = violations(
            "WantingDto",
            Wanting.serializer().descriptor,
            sent = false,
            schema(
                """{"required":["id","nullableThere","added"],"properties":{"id":{"type":"string"},
                    "nullableThere":{"type":"string"},"added":{"type":"string"}}}""",
            ),
        )
        assertThat(found).isEmpty()
    }
}
