# Changelog

## [1.4.0](https://github.com/GlitzyWorm/Kvitteria/compare/backend-v1.3.0...backend-v1.4.0) (2026-11-10)


### Features

* correct unreconciled receipts and accept a difference ([#23](https://github.com/GlitzyWorm/Kvitteria/issues/23)) ([61fb5b3](https://github.com/GlitzyWorm/Kvitteria/commit/61fb5b3894fd364526acaeb15b76f8fb83a240c3))
* extract receipts as labelled rows with one shared prompt ([#26](https://github.com/GlitzyWorm/Kvitteria/issues/26)) ([d41b573](https://github.com/GlitzyWorm/Kvitteria/commit/d41b573e56cffb0758ad0e54e87247c15671186b))
* show raw names and a labelled chart in Prices ([#25](https://github.com/GlitzyWorm/Kvitteria/issues/25)) ([9c15bf3](https://github.com/GlitzyWorm/Kvitteria/commit/9c15bf3731867404f67dd83d067068df5940adaa))

## [1.3.0](https://github.com/GlitzyWorm/Kvitteria/compare/backend-v1.2.0...backend-v1.3.0) (2026-11-08)


### Features

* add receipts from photos and screenshots ([#21](https://github.com/GlitzyWorm/Kvitteria/issues/21)) ([70f0253](https://github.com/GlitzyWorm/Kvitteria/commit/70f0253ba9a131813dc8f5b6bfc23570ecafdf91))

## [1.2.0](https://github.com/GlitzyWorm/Kvitteria/compare/backend-v1.1.0...backend-v1.2.0) (2026-11-02)


### Features

* update the server from the app ([#18](https://github.com/GlitzyWorm/Kvitteria/issues/18)) ([490d44f](https://github.com/GlitzyWorm/Kvitteria/commit/490d44fcc48c4140425e912bb0eb4caadc0a80eb))


### Bug Fixes

* **backend:** limit Ollama threads to the CPUs it may use ([#20](https://github.com/GlitzyWorm/Kvitteria/issues/20)) ([343a67e](https://github.com/GlitzyWorm/Kvitteria/commit/343a67ece97e17f1438d5aebcc69575ec155b085))

## [1.1.0](https://github.com/GlitzyWorm/Kvitteria/compare/backend-v1.0.0...backend-v1.1.0) (2026-11-01)


### Features

* **android:** build the native app and retire MAUI ([#13](https://github.com/GlitzyWorm/Kvitteria/issues/13)) ([976af4d](https://github.com/GlitzyWorm/Kvitteria/commit/976af4df266d702919741a30f63370b548aca267))
* **backend:** add spending insights and receipt search ([#12](https://github.com/GlitzyWorm/Kvitteria/issues/12)) ([3a9ee5f](https://github.com/GlitzyWorm/Kvitteria/commit/3a9ee5ffcd687bdcedcf966bbc44f2895f0b3cc7))
* **backend:** atomic line sorting and the unassigned queue ([#11](https://github.com/GlitzyWorm/Kvitteria/issues/11)) ([950ffef](https://github.com/GlitzyWorm/Kvitteria/commit/950ffef633439ee39a9937af1fcd90f6396f40fc))
* **backend:** keep mail durably and track pipeline failures ([#9](https://github.com/GlitzyWorm/Kvitteria/issues/9)) ([c248141](https://github.com/GlitzyWorm/Kvitteria/commit/c248141d85f547de279728535d8e5df818cf9fb5))
* **contracts:** Add a shared contracts project ([77dbf6c](https://github.com/GlitzyWorm/Kvitteria/commit/77dbf6c9351ec09eadc913f861527ec95c30e60f))
* **mobile:** add editable Pipeline settings tab ([#8](https://github.com/GlitzyWorm/Kvitteria/issues/8)) ([2564ae8](https://github.com/GlitzyWorm/Kvitteria/commit/2564ae8c8383c3b6ebffa794af60b8c4e5a00f64))
* **mobile:** Implement Kvitteria design system and theme ([eb512db](https://github.com/GlitzyWorm/Kvitteria/commit/eb512db49d74ea2cfe52ad37a12addb1ce1bec0d))
* **mobile:** Inbox tab with mailbox management ([#7](https://github.com/GlitzyWorm/Kvitteria/issues/7)) ([d353f0b](https://github.com/GlitzyWorm/Kvitteria/commit/d353f0b16a629966ea8e60b348628e2e029fc14f))
* reach the stack from a phone, safely ([#15](https://github.com/GlitzyWorm/Kvitteria/issues/15)) ([bd74c47](https://github.com/GlitzyWorm/Kvitteria/commit/bd74c47b9d0fdc5f3a2c9fdf7d18f60b20c05cdb))


### Build System

* move TargetFramework into the backend projects ([f6e5519](https://github.com/GlitzyWorm/Kvitteria/commit/f6e55195c03a881db2909bd294960f611f4fb8c2))

## 1.0.0 (2026-08-21)


### Features

* configure from the environment, migrate on startup, harden token validation ([5a059d9](https://github.com/GlitzyWorm/Kvitteria/commit/5a059d990e437e50e3906dc8e55842e0d2d9d0e1))
* report readiness per dependency and expose the running version ([f4a0d68](https://github.com/GlitzyWorm/Kvitteria/commit/f4a0d684748f2c73a8ce8eead6974aff2f3848c3))


### Bug Fixes

* **build:** let the release-managed version reach the image ([6d53010](https://github.com/GlitzyWorm/Kvitteria/commit/6d53010ddf80a3f2b1298962cbccc4fa46f9cba0))
* **build:** let the release-managed version reach the image ([4d9c7ae](https://github.com/GlitzyWorm/Kvitteria/commit/4d9c7aea83b3b083b36b6695b918d61e5a776486))
* point the seeded Ollama model at the tag compose pulls ([59784e6](https://github.com/GlitzyWorm/Kvitteria/commit/59784e678de492d3e3b12a547104abfcda6aca29))


### Refactoring

* rename SupabaseUserId to AuthUserId ([3dddfbe](https://github.com/GlitzyWorm/Kvitteria/commit/3dddfbe392e6caee2c9d677107bc745b95322e99))


### Build System

* centralize MSBuild properties and stamp a version into the image ([06c6510](https://github.com/GlitzyWorm/Kvitteria/commit/06c65107448d25f6843694849938ea8b7966720d))
