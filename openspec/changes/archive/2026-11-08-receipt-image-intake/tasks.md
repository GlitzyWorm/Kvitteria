# Tasks

## 1. Schema and model

- [x] 1.1 Add `ReceiptImage` (`Id`, `Content`, `Sha256` unique, `Width`, `Height`, `CreatedAt`) and its `DbSet`. Add `IntakeSource` stored as its name, plus `Source`, `CapturedAt` and `ImageId` on `ReceiptIntake`. Add `ImageId` and `PossibleDuplicateOfId` (self-FK, `SetNull`) on `Receipt`. Verify `dotnet build` passes.
- [x] 1.2 Append `AddReceipts` to `Permission` and to `DatabaseSeeder.DefaultMemberPermissions`. Verify the existing permission-name parsing and effective-permission tests still pass.
- [x] 1.3 Scaffold the migration with `dotnet ef migrations add ReceiptImageIntake --project Backend`. Default existing intakes to `'Mail'`, and append `AddReceipts` to `HouseholdSettings.MemberPermissions` when absent. Verify on a copy of the dev database (Postgres on 54322) that existing receipts and intakes load, and that `GET /api/users/me` for a member lists `AddReceipts`.
- [x] 1.4 Confirm that receipt IDs are generated in insertion order (UUIDv7) by inserting two receipts on the dev stack and comparing. If not, add `Receipt.CreatedAt` to the migration instead. Record which in design.md decision 9.

## 2. Image limits and pure helpers

- [x] 2.1 Add `ReceiptImageLimits` (1 000 000 px, 2 MB) and `JpegHeader.TryRead` (SOI, first SOF0–SOF15 excluding DHT/JPG/DAC, EOI) with Backend.Tests covering:
  - a real JPEG's dimensions;
  - a progressive JPEG;
  - a truncated file;
  - a PNG;
  - an empty body;
  - over budget by one pixel.

  Verify `dotnet test Backend.Tests/Backend.Tests.csproj` passes.
- [x] 2.2 Add `DanishTime.DayRangeUtc` with tests for:
  - an ordinary day;
  - 23:50 against 00:10 Danish time;
  - the DST spring-forward and fall-back days.

  Verify the tests pass.
- [x] 2.3 Add `ReceiptTranscription.IsNoReceipt` with tests for `NO_RECEIPT`, lowercase, surrounding whitespace, a trailing full stop, and a real transcription containing the word. Verify the tests pass.

## 3. Ollama: vision and transcription

- [x] 3.1 Add `images` to `OllamaChatMessage` (omitted when null), `OllamaClient.GetCapabilitiesAsync(model)` reading `/api/show`, and `TranscribeAsync(byte[] jpeg, ct)` with the transcription prompt from design decision 2, keeping the existing transport/response exception split. Verify against the dev stack's Ollama that capabilities for `qwen3.5:4b` include `vision`, and that a local sample image transcribes.
- [x] 3.2 Add `ReceiptExtractionPrompt.PhotoSystemPrompt` per design decision 3, and give `ExtractAsync` a prompt parameter, which mail call sites pass as `SystemPrompt`. Verify mail extraction is unchanged by re-running a stored mail `BodyText` through the dev stack.
- [ ] 3.3 Tune `PhotoSystemPrompt` against the four benchmark images, kept outside the repository because they contain personal data. Run each transcription through extraction at least three times on the LXC (or a machine of the same speed). The bar:
  - each of the four reconciles within the configured attempts in most runs;
  - the dates resolve to 31 Oct (SuperBrugsen), 26 Oct 13:07 (Netto) and 1 Nov 10:13 (Føtex);
  - trailing-minus `RABAT` lines are extracted as negatives.

  Record the results in the memory notes, not the repo.

## 4. Durable intake for uploads

- [x] 4.1 Extend `ReceiptIntakeStore`:
  - `RecordUploadAsync`, which inserts the image and intake in one transaction, returns null on the unique-hash conflict, and sets `MailDate` to `CapturedAt ?? ReceivedAt`;
  - `SaveTranscriptionAsync`, conditional on `Extracting`;
  - `ToRecord` carrying `Source` and `ImageId`.

  Verify by uploading twice on the dev stack: the second insert reports a conflict and leaves one row.
- [x] 4.2 Extend `ReceiptRecord` with `Source` and `ImageId`, and make `Describe` in both workers name uploads as `upload <id>`. Verify the log lines in `docs/troubleshooting.md` still match, or update them.
- [x] 4.3 Add the transcription branch to `ExtractionWorker`: load the image, `TranscribeAsync`, check `IsNoReceipt` (fail with a fixed error and store no text), `SaveTranscriptionAsync`, then extract with `PhotoSystemPrompt`. Mail keeps `SystemPrompt`. Verify on the dev stack:
  - an upload becomes a receipt;
  - a photo with no receipt fails with the no-receipt error;
  - stopping the backend mid-transcription and starting it again transcribes again;
  - a failed upload with a stored transcription retries without a second vision call (check the Ollama log).
- [x] 4.4 Update `ReceiptIngestionService` to copy `ImageId` to the receipt. Verify that an image-sourced receipt has its image and that the intake row is gone.
- [x] 4.5 Make dismiss in `PipelineEndpoints` delete an upload's image in the same transaction. Verify on the dev stack that dismissing a failed upload removes its `ReceiptImages` row.

## 5. Upload, image and delete endpoints

- [x] 5.1 Add `POST /api/receipts/uploads` per design decision 8: multipart form, `RequestSizeLimit`, `DisableAntiforgery`, `AddReceipts` policy, `JpegHeader` validation, the vision check (409 naming the model, accepted when Ollama is unreachable), the unique-hash 409, `TryEnqueue` with rollback and a 503, and a 202 with `ReceiptUploadDto`. Declare `.WithName`, `.Produces` and `.ProducesProblem` for every status. Verify each scenario in `specs/pipeline/image-intake` with `curl` and a token from `scripts/token.sh`.
- [x] 5.2 Add `GET /api/receipts/{id}/image` with the immutable private cache header and a 404 problem for no receipt or no image. Verify with `curl` that the bytes match the uploaded file's SHA-256.
- [x] 5.3 Add `DELETE /api/receipts/{id}` gated by `EditReceipts`: one transaction that removes the image, the receipt, its lines and coverage, and nulls duplicate flags. Verify the `specs/receipts/deletion` scenarios with `curl`, including that an alias the receipt taught still exists afterwards.
- [x] 5.4 Add duplicate flagging to `ReceiptIngestionService` (same merchant, same `GrandTotal`, `DayRangeUtc`, earliest first). Verify on the dev stack:
  - uploading the SuperBrugsen screenshot and then its full receipt flags the second against the first;
  - deleting the first clears the flag.
- [x] 5.5 Add `HasImage` and `PossibleDuplicateOfId` to `ReceiptDto`, and `Source`, `ReceivedAt` and `HasTranscription` to `ReceiptFailureDto`, with `ToDto` updates (and the `Include`s the `ToDto` gotcha demands). Add `ReceiptUploadDto`. Include upload intakes in the status counts. Regenerate `docs/openapi/kvitteria.json` with `dotnet build Backend/Backend.csproj -p:GenerateOpenApi=true`, and verify `git diff` shows only additions.
- [x] 5.6 Update `CLAUDE.md`: the pipeline architecture (transcription step, upload source, the image table and why it is not a volume), the new permission, and the gotchas (1 MP budget and why, sample images never committed, `DisableAntiforgery`). Update `docs/configuration.md` (vision model requirement, RAM headroom, storage per image) and `README.md` (the new source). Verify every command quoted in the changed sections runs as written.

## 6. Android: core wire models and receipt repository

- [x] 6.1 Add the new fields to the app's receipt and failure DTOs and domain models, add the `ReceiptUploadDto` wire model, add `AddReceipts` to the permission enum with Danish and English label and description strings, and register the new DTOs in `ContractTest`. Verify with `./gradlew spotlessCheck testDebugUnitTest test`.
- [x] 6.2 Add `delete(id)` and `image(id)` to `ReceiptRepository` and `KtorReceiptRepository`. Delete evicts the receipt from the in-memory list cache. Add `MockEngine` tests for 204, 404 and the image bytes. Verify the tests pass.

## 7. Android: capture feature

- [x] 7.1 Create `feature/capture/{domain,data,presentation}` with the convention plugins and MPL headers, add the ML Kit document scanner and `androidx.exifinterface` to the version catalog, and wire the Koin modules in `app`. Verify `./gradlew spotlessCheck assembleDebug` passes.
- [x] 7.2 Implement `AndroidImagePreparer` per design decision 13 (bounds-first decode, EXIF rotation, ≤ 1 MP, JPEG 85, `DateTimeOriginal` as `capturedAt`), with the pixel arithmetic in a pure function. Unit-test that function for 4000×3000, 1080×2400, an image already within budget (not upscaled) and a rotated orientation. Verify the tests pass, and that an uploaded file from a GPS-tagged photo has no EXIF (`exiftool` on the bytes fetched from the image endpoint).
- [x] 7.3 Implement `KtorReceiptUploader` with `submitFormWithBinaryData`, mapping 202, 400, 403, 409 and 503 to `Result<_, ApiError>`. Add `MockEngine` tests for each. Verify the tests pass.
- [x] 7.4 Build the add-receipt sheet (scan and choose image, with scan hidden when unavailable), the scanner launcher, the photo picker, `PendingCapture` and the confirm screen as MVI. Add ViewModel tests with fakes for:
  - upload accepted;
  - a 409 that keeps the image;
  - no connection that keeps the image;
  - discard.

  Verify the tests pass.
- [x] 7.5 Add the plus action to Oversigt and Kvitteringer, shown only with `AddReceipts`. Verify on the AVD that it is hidden for a member without the permission and opens the sheet otherwise.
- [x] 7.6 Add the `ACTION_SEND image/*` intent filter, preparation in `onCreate`/`onNewIntent`, and `KvitteriaRoot` opening the confirm route once in `MainRoute`, discarding with a message when `AddReceipts` is missing. Verify on the AVD:
  - sharing a screenshot from the gallery while signed in opens the confirm screen;
  - sharing while signed out opens it after sign-in.

## 8. Android: receipt and pipeline screens

- [x] 8.1 Add the image thumbnail, the full-screen zoomable viewer and the image retry state to receipt detail. Add the possible-duplicate mark, which opens the matching receipt. Add delete with confirmation behind `EditReceipts`, including the 404 handling. Add ViewModel tests for delete success, delete 404 and the image load failing. Verify the tests pass and check each on the AVD.
- [x] 8.2 Show failed uploads in Problems as uploaded images with member, upload time and error, and no subject or sender. Add a ViewModel or rendering test for a mixed list. Verify the test passes.
- [x] 8.3 Check every new string in Danish and English, and the design-gallery entries for any new `Kv*` usage. Verify with `adb shell am start -n dev.jelshoj.kvitteria/.DesignGalleryActivity` and by switching the app language in settings.

## 9. End-to-end verification

- [ ] 9.1 On a throwaway compose stack (per the Android e2e recipe), with the AVD:
  - scan a printed receipt;
  - pick a store-app screenshot;
  - share a second screenshot of the same purchase.

  Confirm all three become receipts with images, the duplicate is flagged and can be deleted, and the queued and extracting counts move on Pipeline meanwhile.
- [ ] 9.2 On the LXC after deploying, upload one real photo and watch `free -m` and `docker compose logs -f backend` through transcription. Confirm memory stays below the guest limit and the receipt is stored. Note the measured time in the deployment memory notes.

## Workflow follow-up

- Run `/opsx:archive` once the change is merged and verified on the LXC.
