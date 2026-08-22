# Change Log
All notable changes to this project will be documented in this file.
 
The format is based on [Keep a Changelog](http://keepachangelog.com/)
and this project adheres to [Semantic Versioning](http://semver.org/).
 
## [Unreleased]

### Added

- Deterministic, read-only group planning through `AddressablesAutomation.Analyze`.
- Confirmed transactional Apply with stale-plan rejection, recovery snapshots, rollback, and manual recovery.
- Relative-path addresses, assembly-qualified type filters, convergent group/address/label operations, missing group/schema planning, and sorted reports.
- Project Settings, editor-window, and CLI dry-run/apply surfaces.

### Changed

- Group synchronization now operates only on explicit rule-owned entries and preserves unrelated labels by default.
- Legacy unique simple type names and the serialized `Lables` field migrate through explicit preview; ambiguous types remain blocking diagnostics.

### Fixed

- Removed the active global Default Group cleanup and the filename-only, create-only group updater.
- Addressable folder entries, duplicate generated addresses, read-only/non-buildable groups, failed loads, and incompatible asset claims now fail safely before mutation.
 
## [1.2.4] - 2017-03-15
  
Here we would have the update steps for 1.2.4 for people to follow.
 
### Added
 
### Changed
  
- [PROJECTNAME-ZZZZ](http://tickets.projectname.com/browse/PROJECTNAME-ZZZZ)
  PATCH Drupal.org is now used for composer.
 
### Fixed
 
- [PROJECTNAME-TTTT](http://tickets.projectname.com/browse/PROJECTNAME-TTTT)
  PATCH Add logic to runsheet teaser delete to delete corresponding
  schedule cards.
