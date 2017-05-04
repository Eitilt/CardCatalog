# Change Log
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/)
and this project adheres to [Semantic Versioning](http://semver.org/).

## 0.3.1
### Changed
- Update internals to match patterns in `Core` 0.3.0

## 0.3.0
### Added
- ID3v2.3 support brought to same level as ID3v2.4

## 0.2.4
### Added
- Set neutral resource language

## 0.2.3
### Changed
- Moved shared strings out of ID3v2.4-specific resources
### Fixed
- Default field names will now change if the locale does while the
  program is running
- Dictionary extension methods in referenced package have been moved to
  new namespace

## 0.2.2
### Added
- Transparently moved fields shared by ID3v2.3 and v2.4 to parent class
  in preparation for supporting the former
### Changed
- `APIC` uses new `ImageData` structure rather than wrapping raw data
  and MIME type into tuple
- `ImageCategory` enum is now contained within the base `ID3v2` class
  rather than any particular field

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