/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.overview.presentation

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.WorkCounts
import dev.jelshoj.kvitteria.core.domain.insights.CategoryAmount
import dev.jelshoj.kvitteria.core.domain.insights.InsightsRepository
import dev.jelshoj.kvitteria.core.domain.insights.MealCoverageMonth
import dev.jelshoj.kvitteria.core.domain.insights.SpendingSummary
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.MemberStatus
import dev.jelshoj.kvitteria.core.domain.receipt.Receipt
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptRepository
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import dev.jelshoj.kvitteria.core.domain.taxonomy.TaxonomyRepository
import dev.jelshoj.kvitteria.core.presentation.DanishTime
import dev.jelshoj.kvitteria.core.presentation.Loadable
import dev.jelshoj.kvitteria.core.presentation.Loader
import java.math.BigDecimal
import java.time.Clock
import java.time.YearMonth
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.stateIn

data class DinnerFigures(val name: String, val month: MealCoverageMonth?)

data class OverviewData(
    val summary: SpendingSummary,
    val categories: List<CategoryAmount>,
    val uncategorized: CategoryAmount?,
    val dinner: DinnerFigures?,
    val latest: Receipt?,
    val memberCount: Int,
) {
    /** Up to the six largest, with uncategorized spend last when there is any. */
    val bars: List<CategoryAmount>
        get() = categories.sortedByDescending { it.amount }.take(MAX_BARS) +
            listOfNotNull(uncategorized?.takeIf { it.amount.signum() != 0 })

    val isEmpty: Boolean get() = latest == null

    private companion object {
        const val MAX_BARS = 6
    }
}

enum class Greeting { Morning, Afternoon, Evening }

data class OverviewState(
    val month: YearMonth,
    val isCurrentMonth: Boolean,
    val data: Loadable<OverviewData> = Loadable(),
    val memberName: String = "",
    val greeting: Greeting = Greeting.Morning,
    val unassigned: Int = 0,
)

private const val MORNING_ENDS = 10
private const val AFTERNOON_ENDS = 18

class OverviewViewModel(
    private val insights: InsightsRepository,
    private val receipts: ReceiptRepository,
    private val taxonomy: TaxonomyRepository,
    private val members: MemberRepository,
    private val clock: Clock,
    sessions: SessionManager,
    counts: WorkCounts,
) : ViewModel() {
    private val current = DanishTime.currentMonth(clock)
    private val month = MutableStateFlow(current)
    private val loader = Loader(viewModelScope, ::load)

    val state = combine(month, loader.state, sessions.member, counts.unassignedLines) { month, data, member, unassigned ->
        OverviewState(
            month = month,
            isCurrentMonth = month >= current,
            data = data,
            memberName = member?.displayName.orEmpty(),
            greeting = greeting(),
            unassigned = unassigned,
        )
    }.stateIn(viewModelScope, SharingStarted.Eagerly, OverviewState(current, true))

    private fun greeting(): Greeting {
        val hour = DanishTime.hour(clock)
        return when {
            hour < MORNING_ENDS -> Greeting.Morning
            hour < AFTERNOON_ENDS -> Greeting.Afternoon
            else -> Greeting.Evening
        }
    }

    private suspend fun load(): Result<OverviewData, ApiError> = coroutineScope {
        val shown = month.value
        val summary = async { insights.summary(shown) }
        val categories = async { insights.categories(shown, null, null) }
        val coverage = async { insights.mealCoverage(shown, 1) }
        val meals = async { taxonomy.meals() }
        val latest = async { receipts.receipts(1, 1) }
        val memberList = async { members.members() }

        val first = summary.await()
        val second = categories.await()
        when {
            first is Result.Error -> first
            second is Result.Error -> second
            else -> {
                val breakdown = (second as Result.Success).data
                val tracking = (meals.await() as? Result.Success)?.data.orEmpty().firstOrNull { it.tracksDayCoverage }
                val stats = (coverage.await() as? Result.Success)?.data.orEmpty()
                    .firstOrNull { it.mealId == tracking?.id }
                Result.Success(
                    OverviewData(
                        summary = (first as Result.Success).data,
                        categories = breakdown.rows,
                        uncategorized = breakdown.uncategorized,
                        dinner = tracking?.let { DinnerFigures(it.name, stats?.months?.lastOrNull()) },
                        latest = (latest.await() as? Result.Success)?.data?.items?.firstOrNull(),
                        memberCount = (memberList.await() as? Result.Success)?.data.orEmpty()
                            .count { it.status == MemberStatus.Active },
                    ),
                )
            }
        }
    }

    fun refresh() = loader.refresh()

    fun previousMonth() {
        month.value = month.value.minusMonths(1)
        loader.refresh()
    }

    fun nextMonth() {
        if (month.value < current) {
            month.value = month.value.plusMonths(1)
            loader.refresh()
        }
    }
}

/** A month's share in percent, or null when there's nothing to divide by. */
fun shareOf(part: BigDecimal, whole: BigDecimal): BigDecimal? =
    if (whole.signum() <= 0) null else part.multiply(BigDecimal(100)).divide(whole, 1, java.math.RoundingMode.HALF_UP)
