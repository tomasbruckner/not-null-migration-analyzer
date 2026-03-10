# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.1.0] - 2026-03-10

### Added
- NOTNULL001: Detect nullable properties and fields that are only assigned non-nullable values
- Code fix to remove nullable annotation (`?` and `Nullable<T>`)
- Interface property aggregation — reports when all implementations are non-nullable
