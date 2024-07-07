# Zebra Designer Patcher

The program's objective is to make three specific changes to the IL instructions of a given method using `dnlib`:

1. Replace all `ldc.i4.0` instructions with `ldc.i4.1`.
2. Replace the last `call` instruction with `ldc.i4.1`.
3. Replace the last `ldsflda` instruction with `nop`.

## Requirements

- [.NET](https://dotnet.microsoft.com/download) (appropriate version for your development environment)
- [dnlib](https://github.com/0xd4d/dnlib) (can be added to the project via NuGet)

## Usage

- Activate the Zebra Designer Professional trial.
- Just patch it (:
