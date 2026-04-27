# Changelog

## [0.3.0] - 2026-04-27

### Added
- New lobby for taking surveys (https://github.com/ucf-research/vera-package/pull/16)
- New "preview account" functionality which disables WebXR uploads (https://github.com/ucf-research/vera-package/pull/19)
- Additional participant states (https://github.com/ucf-research/vera-package/pull/22)
- Run web-based surveys mid-experiment (https://github.com/ucf-research/vera-package/pull/23)
- Upload to arbitrary file types (non-CSV) mid-experiment (https://github.com/ucf-research/vera-package/pull/24)

### Adjusted
- Removed eventId as a required column (https://github.com/ucf-research/vera-package/pull/17)
- Made VERA settings window scrollable (https://github.com/ucf-research/vera-package/pull/18)


### Fixed
- Handling for locale differences (https://github.com/ucf-research/vera-package/pull/20)
- Additional handling for locale differences (https://github.com/ucf-research/vera-package/pull/21)

## [0.2.1] - 2026-02-27

### Fixed

- Prevent surveys from being fetched before initialization.
- Set participant status to COMPLETE after all files upload successfully. ​
- Track file uploads separately for each survey instance.

## [0.2.0] - 2026-2-26

### Added

- Running surveys (#8)
- Managing trials and participant flow (#3)
- Help guide (#13)

### Adjusted

- Telemetry files log every frame (#1)
- General updates to the telemetry file (#5)
- Adjustments to rotation formatting (#14)
- Quality of life updates (#4)
- General cleanup (#9)
- Live site points to new host (#11)
- Condition / IV values log using short encoding (#10)

### Fixed

- Define symbols copy to all build profiles (#12)

## [0.1.0] - 2025-12-9

### Added

- Initial development package release (#1)
