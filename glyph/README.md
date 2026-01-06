# Glyph Project

Glyph is a global, low-latency leader-key and layered key sequence engine designed for Windows. It provides an always-available overlay that is discoverable, context-aware, and capable of executing various actions such as launching applications, running scripts, and managing windows.

## Features

- **Global Key Capture**: Utilizes a low-level keyboard hook to capture input globally.
- **Context Awareness**: Adapts to the active application and user-defined modes.
- **Layered Key Sequences**: Supports multiple layers of key bindings with deterministic precedence.
- **Action Execution**: Allows users to launch applications, open files, run scripts, and send key sequences.
- **Safe and Debuggable**: Includes logging, dry-run capabilities, and clear failure reporting.

## Getting Started

### Prerequisites

- Windows 10 or 11
- .NET 8 SDK

### Installation

1. Clone the repository:
   ```
   git clone <repository-url>
   ```
2. Navigate to the project directory:
   ```
   cd glyph
   ```
3. Build the solution:
   ```
   dotnet build Glyph.sln
   ```

### Running the Application

To run the application, execute the following command:
```
dotnet run --project src/Glyph.App/Glyph.App.csproj
```

## Configuration

Configuration files are located in the `config` directory. The main configuration file is `config.toml`, which contains settings for the application. Key bindings and action definitions can be found in `keymap.toml` and `actions.toml`, respectively.

## Testing

Unit tests are located in the `tests` directory. To run the tests, use the following command:
```
dotnet test tests/Glyph.Core.Tests/Glyph.Core.Tests.csproj
```

## Contributing

Contributions are welcome! Please submit a pull request or open an issue for any enhancements or bug fixes.

## License

This project is licensed under the MIT License. See the LICENSE file for more details.