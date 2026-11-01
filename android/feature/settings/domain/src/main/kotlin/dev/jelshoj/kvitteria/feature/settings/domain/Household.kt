/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.settings.domain

import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.member.Member
import dev.jelshoj.kvitteria.core.domain.member.MemberStatus
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.member.Role

data class MemberPermissions(
    val granted: Set<Permission>,
    val available: List<Permission>,
)

/** Writes on the household. Reads of its members are shared, so they live in `core`. */
interface HouseholdRepository {
    /** Pre-authorizes an address. Nothing is emailed: the person signs up themselves. */
    suspend fun invite(email: String, displayName: String?, role: Role): Result<Member, ApiError>

    suspend fun updateMember(id: String, displayName: String?, role: Role, status: MemberStatus?): Result<Member, ApiError>

    suspend fun memberPermissions(): Result<MemberPermissions, ApiError>

    /** The endpoint replaces the whole set. */
    suspend fun saveMemberPermissions(granted: Set<Permission>): Result<MemberPermissions, ApiError>
}
