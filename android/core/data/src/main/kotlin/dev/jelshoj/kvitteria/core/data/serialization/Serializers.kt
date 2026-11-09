/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

@file:OptIn(ExperimentalSerializationApi::class)

package dev.jelshoj.kvitteria.core.data.serialization

import dev.jelshoj.kvitteria.core.domain.member.MemberStatus
import dev.jelshoj.kvitteria.core.domain.member.Role
import dev.jelshoj.kvitteria.core.domain.receipt.ReceiptStatus
import dev.jelshoj.kvitteria.core.domain.receipt.ReconciliationState
import java.math.BigDecimal
import java.time.Instant
import java.time.LocalDateTime
import java.time.OffsetDateTime
import java.time.YearMonth
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.KSerializer
import kotlinx.serialization.SerializationException
import kotlinx.serialization.descriptors.PrimitiveKind
import kotlinx.serialization.descriptors.PrimitiveSerialDescriptor
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.encoding.Decoder
import kotlinx.serialization.encoding.Encoder
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonDecoder
import kotlinx.serialization.json.JsonEncoder
import kotlinx.serialization.json.JsonUnquotedLiteral
import kotlinx.serialization.json.jsonPrimitive

/** Everything the server sends may gain fields, so the app ignores what it doesn't read. */
val KvitteriaJson: Json = Json {
    ignoreUnknownKeys = true
    encodeDefaults = true
}

/**
 * Reads the JSON number's own text rather than going through `Double`, which would turn 284.30 into
 * something else entirely. .NET 10 also describes `decimal` as number-or-string, and this takes both.
 */
object MoneySerializer : KSerializer<BigDecimal> {
    override val descriptor: SerialDescriptor = PrimitiveSerialDescriptor("Money", PrimitiveKind.STRING)

    override fun deserialize(decoder: Decoder): BigDecimal {
        val text = (decoder as? JsonDecoder)?.decodeJsonElement()?.jsonPrimitive?.content
            ?: throw SerializationException("Money can only be read from JSON")
        return text.toBigDecimalOrNull() ?: throw SerializationException("'$text' is not an amount")
    }

    override fun serialize(encoder: Encoder, value: BigDecimal) {
        val json = encoder as? JsonEncoder ?: throw SerializationException("Money can only be written to JSON")
        json.encodeJsonElement(JsonUnquotedLiteral(value.toPlainString()))
    }
}

/** Reads an instant, taking a timestamp with no offset as UTC, which is how the server stores them. */
object InstantSerializer : KSerializer<Instant> {
    override val descriptor: SerialDescriptor = PrimitiveSerialDescriptor("Instant", PrimitiveKind.STRING)

    override fun deserialize(decoder: Decoder): Instant {
        val text = decoder.decodeString()
        return try {
            when (val parsed = DateTimeFormatter.ISO_DATE_TIME.parseBest(text, OffsetDateTime::from, LocalDateTime::from)) {
                is OffsetDateTime -> parsed.toInstant()
                is LocalDateTime -> parsed.toInstant(ZoneOffset.UTC)
                else -> throw SerializationException("'$text' is not an instant")
            }
        } catch (e: java.time.format.DateTimeParseException) {
            throw SerializationException("'$text' is not an instant", e)
        }
    }

    override fun serialize(encoder: Encoder, value: Instant) = encoder.encodeString(value.toString())
}

/** The wire's `yyyy-MM` month. */
object YearMonthSerializer : KSerializer<YearMonth> {
    override val descriptor: SerialDescriptor = PrimitiveSerialDescriptor("YearMonth", PrimitiveKind.STRING)

    override fun deserialize(decoder: Decoder): YearMonth {
        val text = decoder.decodeString()
        return try {
            YearMonth.parse(text)
        } catch (e: java.time.format.DateTimeParseException) {
            throw SerializationException("'$text' is not a month", e)
        }
    }

    override fun serialize(encoder: Encoder, value: YearMonth) = encoder.encodeString(value.toString())
}

/** `ReceiptStatus` predates the enums-as-names rule and is still a number on the wire. */
object ReceiptStatusSerializer : KSerializer<ReceiptStatus> {
    private val byOrdinal = listOf(
        ReceiptStatus.Queued,
        ReceiptStatus.Extracting,
        ReceiptStatus.Extracted,
        ReceiptStatus.Completed,
        ReceiptStatus.Failed,
    )

    override val descriptor: SerialDescriptor = PrimitiveSerialDescriptor("ReceiptStatus", PrimitiveKind.INT)

    override fun deserialize(decoder: Decoder): ReceiptStatus =
        byOrdinal.getOrElse(decoder.decodeInt()) { ReceiptStatus.Unknown }

    override fun serialize(encoder: Encoder, value: ReceiptStatus) =
        encoder.encodeInt(byOrdinal.indexOf(value))
}

/** An enum sent by name, where a name this app doesn't know yet reads as [unknown] rather than failing. */
abstract class NamedEnumSerializer<T : Enum<T>>(
    name: String,
    private val values: Array<T>,
    private val unknown: T,
) : KSerializer<T> {
    override val descriptor: SerialDescriptor = PrimitiveSerialDescriptor(name, PrimitiveKind.STRING)

    override fun deserialize(decoder: Decoder): T {
        val text = decoder.decodeString()
        return values.firstOrNull { it.name == text } ?: unknown
    }

    override fun serialize(encoder: Encoder, value: T) = encoder.encodeString(value.name)
}

object ReconciliationStateSerializer : NamedEnumSerializer<ReconciliationState>(
    "ReconciliationState",
    ReconciliationState.entries.toTypedArray(),
    ReconciliationState.Unknown,
)

object RoleSerializer : NamedEnumSerializer<Role>("Role", Role.entries.toTypedArray(), Role.Unknown)

object MemberStatusSerializer :
    NamedEnumSerializer<MemberStatus>("MemberStatus", MemberStatus.entries.toTypedArray(), MemberStatus.Unknown)
