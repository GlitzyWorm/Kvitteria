/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 */

package dev.jelshoj.kvitteria.feature.capture.data

import dev.jelshoj.kvitteria.core.data.network.ApiHttpClient
import dev.jelshoj.kvitteria.core.data.network.safeCall
import dev.jelshoj.kvitteria.core.domain.ApiError
import dev.jelshoj.kvitteria.core.domain.Result
import dev.jelshoj.kvitteria.core.domain.map
import dev.jelshoj.kvitteria.feature.capture.domain.PreparedImage
import dev.jelshoj.kvitteria.feature.capture.domain.ReceiptUploader
import io.ktor.client.request.forms.formData
import io.ktor.client.request.forms.submitFormWithBinaryData
import io.ktor.http.Headers
import io.ktor.http.HttpHeaders
import kotlinx.serialization.Serializable

@Serializable
data class ReceiptUploadDto(val intakeId: String)

/**
 * Posts the image as multipart form data. A 202 gives the intake ID; a 400, 403, 409 or 503 comes back as an
 * [ApiError] carrying the server's own message, which names what was wrong.
 */
class KtorReceiptUploader(private val api: ApiHttpClient) : ReceiptUploader {
    override suspend fun upload(image: PreparedImage): Result<String, ApiError> =
        safeCall<ReceiptUploadDto> {
            api.client.submitFormWithBinaryData(
                url = "/api/receipts/uploads",
                formData = formData {
                    append(
                        "image",
                        image.bytes,
                        Headers.build {
                            append(HttpHeaders.ContentType, "image/jpeg")
                            append(HttpHeaders.ContentDisposition, "filename=\"receipt.jpg\"")
                        },
                    )
                    image.capturedAt?.let { append("capturedAt", it.toString()) }
                },
            )
        }.map { it.intakeId }
}
