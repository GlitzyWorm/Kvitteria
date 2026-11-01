/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.domain.member

import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result
import java.time.Instant

enum class Role { Admin, Member, Unknown }

enum class MemberStatus { Pending, Active, Disabled, Unknown }

enum class Permission { CategorizeLines, ManageTaxonomy, EditReceipts, ManageUsers, ManageSettings, ManageMailboxes }

/** The signed-in member. [permissions] is the server's resolved set, so an admin holds all of them. */
data class CurrentMember(
    val id: String,
    val email: String,
    val displayName: String,
    val role: Role,
    val status: MemberStatus,
    val permissions: Set<Permission>,
) {
    fun can(permission: Permission): Boolean = permission in permissions
}

data class Member(
    val id: String,
    val email: String,
    val displayName: String,
    val role: Role,
    val status: MemberStatus,
    val hasSignedIn: Boolean,
    val createdAt: Instant,
    val lastSeenAt: Instant?,
)

interface MemberRepository {
    /** Also updates the session's member, so a permission change reaches every screen. */
    suspend fun refreshCurrent(): Result<CurrentMember, ApiError>

    suspend fun members(): Result<List<Member>, ApiError>
}
