/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data

import assertk.assertThat
import assertk.assertions.containsExactly
import assertk.assertions.containsOnly
import assertk.assertions.isEqualTo
import assertk.assertions.isFalse
import assertk.assertions.isNull
import assertk.assertions.isTrue
import dev.jelshoj.kvitteria.core.data.insights.CategoryBreakdownDto
import dev.jelshoj.kvitteria.core.data.insights.MealCoverageStatsDto
import dev.jelshoj.kvitteria.core.data.insights.PriceHistoryDto
import dev.jelshoj.kvitteria.core.data.insights.PricedItemDto
import dev.jelshoj.kvitteria.core.data.insights.SpendingSummaryDto
import dev.jelshoj.kvitteria.core.data.insights.toCategoryBreakdown
import dev.jelshoj.kvitteria.core.data.insights.toMealCoverageStats
import dev.jelshoj.kvitteria.core.data.insights.toPriceHistory
import dev.jelshoj.kvitteria.core.data.insights.toPricedItem
import dev.jelshoj.kvitteria.core.data.insights.toSpendingSummary
import dev.jelshoj.kvitteria.core.data.member.AppUserDto
import dev.jelshoj.kvitteria.core.data.member.CurrentUserDto
import dev.jelshoj.kvitteria.core.data.member.toCurrentMember
import dev.jelshoj.kvitteria.core.data.member.toMember
import dev.jelshoj.kvitteria.core.data.receipt.PagedResultDto
import dev.jelshoj.kvitteria.core.data.receipt.ReceiptDto
import dev.jelshoj.kvitteria.core.data.receipt.toPage
import dev.jelshoj.kvitteria.core.data.receipt.toReceipt
import dev.jelshoj.kvitteria.core.data.serialization.KvitteriaJson
import dev.jelshoj.kvitteria.core.data.taxonomy.CategoryDto
import dev.jelshoj.kvitteria.core.data.taxonomy.toCategory
import dev.jelshoj.kvitteria.core.domain.member.MemberStatus
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.member.Role
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptStatus
import java.math.BigDecimal
import java.time.Instant
import java.time.YearMonth
import org.junit.jupiter.api.Test

class MappersTest {
    private inline fun <reified T> decode(json: String): T = KvitteriaJson.decodeFromString(json)

    private val receiptJson = """
        {"id":"r1","merchant":"NETTO","purchaseDate":"2026-03-01T10:15:30Z","grandTotal":284.30,"pantTotal":3.00,
         "reconciliation":"Unreconciled","acceptance":null,"status":3,"error":"Differs by 0,50 kr","memberId":null,"memberDisplayName":null,
         "receiptLines":[{"id":"l1","rawName":"MÆLK","quantity":2,"unitPrice":10.75,"lineTotal":21.50,
           "canonicalItemId":"i1","canonicalItemDisplayName":"Mælk","mealId":null,"mealDisplayName":null,
           "categoryId":"c1","categoryDisplayName":"Mejeri"},
          {"id":"l2","rawName":"BRØD","quantity":1,"unitPrice":259.30,"lineTotal":259.30,
           "canonicalItemId":null,"canonicalItemDisplayName":null,"mealId":null,"mealDisplayName":null,
           "categoryId":null,"categoryDisplayName":null}],
         "receiptMealCoverage":[{"mealId":"m1","mealDisplayName":"Aftensmad","daysCovered":4}]}
    """.trimIndent()

    @Test
    fun `a receipt maps with its lines, coverage and warning`() {
        val receipt = decode<ReceiptDto>(receiptJson).toReceipt()

        assertThat(receipt.grandTotal).isEqualTo(BigDecimal("284.30"))
        assertThat(receipt.purchaseDate).isEqualTo(Instant.parse("2026-03-01T10:15:30Z"))
        assertThat(receipt.status).isEqualTo(ReceiptStatus.Completed)
        assertThat(receipt.warning).isEqualTo("Differs by 0,50 kr")
        assertThat(receipt.memberName).isNull()
        assertThat(receipt.lines.map { it.canonicalItemName }).containsExactly("Mælk", null)
        assertThat(receipt.coverage.single().daysCovered).isEqualTo(4)
    }

    @Test
    fun `the difference is the total less the lines and pant, exactly`() {
        val receipt = decode<ReceiptDto>(receiptJson).toReceipt()
        assertThat(receipt.difference).isEqualTo(BigDecimal("0.50"))
    }

    @Test
    fun `a page maps its items and knows whether more follow`() {
        val first = PagedResultDto(listOf(1, 2), page = 1, pageSize = 2, totalCount = 3).toPage { it * 2 }
        assertThat(first.items).containsExactly(2, 4)
        assertThat(first.hasMore).isTrue()

        val last = PagedResultDto(listOf(3), page = 2, pageSize = 2, totalCount = 3).toPage { it }
        assertThat(last.hasMore).isFalse()
    }

    @Test
    fun `a category tree maps its children`() {
        val tree = decode<CategoryDto>(
            """{"id":"p","displayName":"Mad","parentCategoryId":null,"sortOrder":0,
                "childCategories":[{"id":"c","displayName":"Frugt & Grønt","parentCategoryId":"p",
                "childCategories":[],"sortOrder":1}]}""",
        ).toCategory()

        assertThat(tree.children.single().name).isEqualTo("Frugt & Grønt")
        assertThat(tree.children.single().parentId).isEqualTo("p")
    }

    @Test
    fun `the current member keeps the permissions this version knows`() {
        val member = decode<CurrentUserDto>(
            """{"id":"u","email":"a@b.dk","displayName":"A","role":"Admin","status":"Active",
                "permissions":["CategorizeLines","SomethingNew","ManageUsers"]}""",
        ).toCurrentMember()

        assertThat(member.role).isEqualTo(Role.Admin)
        assertThat(member.permissions).containsOnly(Permission.CategorizeLines, Permission.ManageUsers)
        assertThat(member.can(Permission.ManageUsers)).isTrue()
        assertThat(member.can(Permission.EditReceipts)).isFalse()
    }

    @Test
    fun `a member who never signed in has no last seen`() {
        val member = decode<AppUserDto>(
            """{"id":"u","email":"a@b.dk","displayName":"A","role":"Member","status":"Pending",
                "hasSignedIn":false,"createdAt":"2026-03-01T10:15:30","lastSeenAt":null}""",
        ).toMember()

        assertThat(member.status).isEqualTo(MemberStatus.Pending)
        assertThat(member.lastSeenAt).isNull()
        assertThat(member.createdAt).isEqualTo(Instant.parse("2026-03-01T10:15:30Z"))
    }

    @Test
    fun `a summary keeps a null change`() {
        val summary = decode<SpendingSummaryDto>(
            """{"month":"2026-03","total":2438.27,"receiptCount":12,"pantTotal":6.00,"linesTotal":2432.27,
                "uncategorizedTotal":0,"previousTotal":0,"changePercent":null}""",
        ).toSpendingSummary()

        assertThat(summary.month).isEqualTo(YearMonth.of(2026, 3))
        assertThat(summary.total).isEqualTo(BigDecimal("2438.27"))
        assertThat(summary.changePercent).isNull()
    }

    @Test
    fun `a drill-down has no uncategorized row`() {
        val breakdown = decode<CategoryBreakdownDto>(
            """{"month":"2026-03","compareToMonth":null,"parentCategoryId":"p","uncategorized":null,
                "rows":[{"categoryId":"c","displayName":"Mejeri","amount":-12.5,"compareAmount":null,"changePercent":null}]}""",
        ).toCategoryBreakdown()

        assertThat(breakdown.compareTo).isNull()
        assertThat(breakdown.uncategorized).isNull()
        assertThat(breakdown.rows.single().amount).isEqualTo(BigDecimal("-12.5"))
    }

    @Test
    fun `a price gap stays a null rather than a zero`() {
        val history = decode<PriceHistoryDto>(
            """{"itemId":"i","displayName":"Mælk","latestPrice":10.75,"lowestPrice":9.95,"changePercent":2.4,
                "rawNameKey":null,"rawNameLabel":null,
                "months":[{"month":"2026-02","price":null,"purchaseCount":0},
                          {"month":"2026-03","price":10.75,"purchaseCount":3}]}""",
        ).toPriceHistory()

        assertThat(history.months.map { it.price }).containsExactly(null, BigDecimal("10.75"))
        assertThat(history.months.map { it.purchaseCount }).containsExactly(0, 3)
        assertThat(history.nameKey).isNull()
    }

    @Test
    fun `a narrowed history carries its raw name`() {
        val history = decode<PriceHistoryDto>(
            """{"itemId":"i","displayName":"Mælk","latestPrice":null,"lowestPrice":null,"changePercent":null,
                "rawNameKey":"arla letmælk 1l","rawNameLabel":"ARLA LETMÆLK 1L","months":[]}""",
        ).toPriceHistory()

        assertThat(history.nameKey).isEqualTo("arla letmælk 1l")
        assertThat(history.nameLabel).isEqualTo("ARLA LETMÆLK 1L")
    }

    @Test
    fun `a priced item lists its raw names`() {
        val item = decode<PricedItemDto>(
            """{"id":"i","displayName":"Mælk","purchaseCount":5,
                "names":[{"key":"arla letmælk 1l","label":"ARLA LETMÆLK 1L","purchaseCount":3},
                         {"key":"minimælk","label":"Minimælk","purchaseCount":2}]}""",
        ).toPricedItem()

        assertThat(item.names.map { it.label }).containsExactly("ARLA LETMÆLK 1L", "Minimælk")
        assertThat(item.names.first().key).isEqualTo("arla letmælk 1l")
    }

    @Test
    fun `a meal month with no coverage has no cost per day`() {
        val stats = decode<MealCoverageStatsDto>(
            """{"mealId":"m","displayName":"Aftensmad",
                "months":[{"month":"2026-03","spend":500.00,"daysCovered":0,"costPerDay":null}]}""",
        ).toMealCoverageStats()

        assertThat(stats.months.single().costPerDay).isNull()
    }
}
