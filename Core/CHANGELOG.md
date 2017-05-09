# Change Log
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/)
and this project adheres to [Semantic Versioning](http://semver.org/).

## 0.3.1
### Added
- Optional logging using the `Microsoft.Extensions.Logging` framework

## 0.3.0
### Added
- Fields store the raw data before parsing

## 0.2.2
### Added
- Set neutral resource language
### Changed
- `MetadataFormat` renamed to `FormatRegistry`
- `RefreshFormats` and `Register(Assembly)` within `FormatRegistry`
  renamed to `RegisterAll`
- `RegisterAll(Assembly)` should be faster due to less reflection calls
### Removed
- (Nonfunctional) registration of all loaded formats on package call
  - Namely, `ScanAssemblyAttribute`

## 0.2.1
### Added
- `ImageData` includes field to identify type of file encoding
### Fixed
- Dictionary extension methods in referenced package have been moved to
  new namespace

## 0.2.0
### Changed
- Field values are now boxed to `object` to allow a better range of
  types, rather than forcing `string` representations
- Support for fields containing images

## 0.1.1
### Changed
- `MetadataTag.Fields` is now represented as a single `IEnumerable`
  rather than as a dictionary

## 0.1.0 (assembly 0.1.0.1)
### Added
- This change log
- Nuget packaging