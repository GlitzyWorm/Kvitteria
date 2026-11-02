# deployment/release-bundle Specification

## Purpose

Defines what a backend release publishes alongside its image, so an update can replace every file the stack runs
from and a release is only offered once it can actually be installed.

## Requirements

### Requirement: A release carries a stack bundle
Each backend release SHALL publish, as release assets, a versioned archive of everything the stack runs from
outside the image, and a checksum for it. The archive SHALL contain the compose files, the Caddyfile, the
database init scripts, `.env.example` and the update tooling. It SHALL NOT contain `compose.override.yaml`.

#### Scenario: Contents
- **WHEN** a person lists the bundle of a release
- **THEN** it holds `compose.yaml`, `compose.public.yaml`, `compose.gpu.yaml`, the Caddyfile, the init scripts, the
  scripts and the systemd units, and no development override

#### Scenario: Name carries the version
- **WHEN** release 1.2.0 is published
- **THEN** its bundle's asset name contains `1.2.0`

### Requirement: The bundle marks a release as installable
The bundle SHALL be uploaded only after the release's image has been pushed to the registry. Every client SHALL treat
a backend release without a bundle as not available.

#### Scenario: Image still building
- **WHEN** a release exists and its image is not yet pushed
- **THEN** the release has no bundle and no check reports it

#### Scenario: Publishing failed
- **WHEN** the image push fails
- **THEN** no bundle is uploaded and the previous release remains the newest available

#### Scenario: Older releases
- **WHEN** a release predates bundles
- **THEN** it is never offered as an update

### Requirement: Republishing replaces the bundle
Re-running publication for a version SHALL replace that version's bundle and checksum rather than failing because
they exist.

#### Scenario: Manual re-publish
- **WHEN** a maintainer re-publishes a version whose bundle already exists
- **THEN** the run succeeds and the release holds one bundle, built from that version's tag

### Requirement: A release does not need a new secret to start
A release SHALL give every new environment variable a default in the compose files, so a bundle applied over an
existing `.env` starts without that file being edited.

#### Scenario: New variable
- **WHEN** a release introduces a variable and an existing `.env` lacks it
- **THEN** the stack starts using the default
