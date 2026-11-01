/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.core.data.member

import dev.jelshoj.kvitteria.core.data.network.ApiHttpClient
import dev.jelshoj.kvitteria.core.data.network.safeCall
import dev.jelshoj.kvitteria.core.data.serialization.InstantSerializer
import dev.jelshoj.kvitteria.core.data.serialization.MemberStatusSerializer
import dev.jelshoj.kvitteria.core.data.serialization.RoleSerializer
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.map
import dev.jelshoj.kvitteria.core.domain.member.CurrentMember
import dev.jelshoj.kvitteria.core.domain.member.Member
import dev.jelshoj.kvitteria.core.domain.member.MemberRepository
import dev.jelshoj.kvitteria.core.domain.member.MemberStatus
import dev.jelshoj.kvitteria.core.domain.member.Permission
import dev.jelshoj.kvitteria.core.domain.member.Role
import dev.jelshoj.kvitteria.core.domain.onSuccess
import dev.jelshoj.kvitteria.core.domain.session.SessionManager
import io.ktor.client.request.get
import java.time.Instant
import kotlinx.serialization.Serializable

@Serializable
data class CurrentUserDto(
    val id: String,
    val email: String,
    val displayName: String,
    @Serializable(with = RoleSerializer::class) val role: Role,
    @Serializable(with = MemberStatusSerializer::class) val status: MemberStatus,
    val permissions: List<String>,
)

@Serializable
data class AppUserDto(
    val id: String,
    val email: String,
    val displayName: String,
    @Serializable(with = RoleSerializer::class) val role: Role,
    @Serializable(with = MemberStatusSerializer::class) val status: MemberStatus,
    val hasSignedIn: Boolean,
    @Serializable(with = InstantSerializer::class) val createdAt: Instant,
    @Serializable(with = InstantSerializer::class) val lastSeenAt: Instant?,
)

/** A permission this version doesn't know yet can't gate anything here, so it's dropped. */
fun List<String>.toPermissions(): Set<Permission> =
    mapNotNullTo(mutableSetOf()) { name -> Permission.entries.firstOrNull { it.name == name } }

fun CurrentUserDto.toCurrentMember(): CurrentMember =
    CurrentMember(id, email, displayName, role, status, permissions.toPermissions())

fun AppUserDto.toMember(): Member =
    Member(id, email, displayName, role, status, hasSignedIn, createdAt, lastSeenAt)

class KtorMemberRepository(
    private val api: ApiHttpClient,
    private val sessions: SessionManager,
) : MemberRepository {
    override suspend fun refreshCurrent(): Result<CurrentMember, ApiError> =
        safeCall<CurrentUserDto> { api.client.get("/api/users/me") }
            .map { it.toCurrentMember() }
            .onSuccess(sessions::setMember)

    override suspend fun members(): Result<List<Member>, ApiError> =
        safeCall<List<AppUserDto>> { api.client.get("/api/users") }
            .map { list -> list.map { it.toMember() } }
}
