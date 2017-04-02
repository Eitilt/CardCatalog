# Change Log
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/)
and this project adheres to [Semantic Versioning](http://semver.org/).

## Unreleased
### Changed
- `APIC` uses new `ImageData` structure rather than wrapping raw data
  and MIME type into tuple

## 0.2.1
### Fixed
- Forgot `ReadByte()` call in `APIC` parsing, leading to happily eating
  over five gigabytes of memory in adding the same byte over and over

## 0.2.0
### Changed
- Field values are now boxed to `object` to allow a better range of
  types, rather than forcing `string` representations
- Support for fields containing images

## 0.1.1
### Changed
- `MetadataTag.Fields` is now represented as a single `IEnumerable`
  rather than as a dictionary

## 0.1.0.1 (Nuget 0.1.0)
### Added
- This change log
- Nuget packaging