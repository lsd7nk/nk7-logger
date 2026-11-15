# nk7-logger

Lightweight logging service for Unity built on `Microsoft.Extensions.Logging`, ZLogger and ZString, with allocation-conscious interpolated string handlers, Unity console output, per-session log files and an optional single log file.

## Features

- `ILoggerService` facade for `Trace`, `Debug`, `Information`, `Warning`, `Error`, and `Critical`.
- Message, exception, exception + message, and interpolated-string overloads for every log level.
- Interpolated string handlers backed by ZString, so disabled log levels skip message building.
- Every entry reaches the Unity console as its own message with its own level: Trace/Debug/Information to `Debug.Log`, Warning to `Debug.LogWarning`, Error/Critical to `Debug.LogError`.
- `LoggerFactory.GetSessionLogger` writes each app session to its own file, keeps the last sessions with a size cap, and filters the console and the file by separate levels.
- `LoggerLocator` gives packages and the app one shared logger: it writes to the Unity console until the app plugs in its own.
- `EmptyLoggerService` provides a no-op drop-in when logging is disabled.
- Package includes `Runtime/csc.rsp` with `-langversion:10` for interpolated string handlers.

## Table of Contents
- [Installation](#installation)
  - [Unity Package Manager](#unity-package-manager)
  - [Manual Installation](#manual-installation)
- [Quick Start](#quick-start)
  - [1. Ensure C# 10](#1-ensure-c-10)
  - [2. Create a Logger](#2-create-a-logger)
  - [3. Wrap It](#3-wrap-it)
  - [4. Log Messages](#4-log-messages)
- [Session Logs](#session-logs)
- [LoggerLocator](#loggerlocator)
- [Lifecycle](#lifecycle)
- [Console Output](#console-output)
- [Runtime API](#runtime-api)
- [Tests](#tests)
- [Requirements](#requirements)

## Installation

### Unity Package Manager
1. Open Unity Package Manager (`Window -> Package Manager`).
2. Click `+ -> Add package from git URL...`.
3. Enter `https://github.com/lsd7nk/nk7-logger.git?path=src`.

Unity does not auto-update Git-based packages; update the hash manually when needed or use [UPM Git Extension](https://github.com/mob-sakai/UpmGitExtension).

### Manual Installation
Copy the `src` folder into your project and reference the `Nk7.Logger` asmdef.

## Quick Start

### 1. Ensure C# 10
- C# 10 is configured by the package through `Runtime/csc.rsp` (`-langversion:10`).
- `csc.rsp` applies only to the package assembly. For `$"..."` in your code to go through the handlers, your assembly also needs `-langversion:10`.
- Keep language version 10+ if you override compiler response files in your project.

### 2. Create a Logger
Use `LoggerFactory.GetLogger` to create a `Microsoft.Extensions.Logging.ILogger`.

```csharp
using Microsoft.Extensions.Logging;
using Nk7.Logger;

ILogger logger = LoggerFactory.GetLogger(
    minimumLogLevel: LogLevel.Information,
    loggerFilePath: "Logs/app.log");
```

`loggerFilePath` is optional. When it is provided, missing directories are created automatically and an existing file is cleared. To keep a file per app launch, use [session logs](#session-logs) instead.

### 3. Wrap It
Wrap the logger in `LoggerService` to use the `ILoggerService` convenience API.

```csharp
ILoggerService loggerService = new LoggerService(logger);
```

Use `EmptyLoggerService` as a drop-in replacement when logging should be disabled.

```csharp
ILoggerService loggerService = new EmptyLoggerService();
```

### 4. Log Messages
Use string overloads for simple messages and interpolated strings when formatting should be skipped for disabled levels.

```csharp
using UnityEngine;

public sealed class LoggerExample : MonoBehaviour
{
    private ILoggerService _logger;

    private void Awake()
    {
        ILogger logger = LoggerFactory.GetLogger(
            minimumLogLevel: LogLevel.Information,
            loggerFilePath: "Logs/app.log");

        _logger = new LoggerService(logger);
    }

    private void Start()
    {
        int playerId = 42;

        _logger.Information($"Player {playerId} connected");
        _logger.Warning("Low memory warning");
        _logger.Error(new System.Exception("Boom"), $"Failed to load profile {playerId}");
    }
}
```

## Session Logs

`GetSessionLogger` opens a new file on every launch and sets the console and file levels separately:

```csharp
ILogger logger = LoggerFactory.GetSessionLogger(
    consoleMinimumLevel: LogLevel.Error,
    sessionLog: new SessionLogOptions(
        directory: Path.Combine(Application.persistentDataPath, "Logs"),
        minimumLevel: LogLevel.Information));
```

- The session file is `session-<UTC time>.log` in `directory`. Each entry starts with the UTC time and the short level: `2026-10-29 07:53:52.123 [INF] Game started`.
- The last `maxSessions` sessions are kept (5 by default), the current one included. Older `session-*.log` files are deleted when a new session starts; other files in the folder are left alone.
- A session is written up to `maxSessionBytes` (5 MB by default). Entries that do not fit are dropped until the session ends.
- A new file name is always later than the newest existing one, so a device clock turned back still puts the current session first and drops the oldest ones.
- The file is written on the ZLogger background thread and opened with `FileShare.ReadWrite`, so it can be read while it is being written.
- `SessionLogFiles.GetSessionFiles(directory)` returns the session file paths, newest first.
- `LogLevel.None` for the console or the file turns that output off.

## LoggerLocator

One place where packages and the app get the logger without knowing how it is built:

```csharp
LoggerLocator.Instance.Information("Purchase started");
```

- Until the app sets a logger, `Instance` writes every level to the Unity console, like `Debug.Log`, `Debug.LogWarning` and `Debug.LogError`.
- The app sets its logger once at startup; after that, package entries go to it, including the session file:

```csharp
var loggerService = new LoggerService(LoggerFactory.GetSessionLogger(LogLevel.Error, sessionLog));

LoggerLocator.Initialize(loggerService);
```

- `LoggerLocator.Initialize(null)` returns the output to the Unity console.
- `Instance` is always the same object, which forwards every call to the current logger. It can be kept in a field before `Initialize`: after `Initialize` its entries still go to the app logger.

## Lifecycle
- `LoggerFactory.GetLogger` builds an `ILogger` pipeline with Unity console output and an optional file sink.
- Unity console output goes through `UnityDebugLogProcessor`: it receives every entry whole and writes it with one `Debug.Log`, `Debug.LogWarning` or `Debug.LogError` call, using the entry's own level.
- `Debug.Log*` is called synchronously on the thread that logs, not on the ZLogger background thread. Entries with `LogLevel.None` are not written to the console.
- File output uses ZLogger plain-text formatting when `loggerFilePath` is provided.
- `LoggerFactory.GetSessionLogger` adds the same console output and the session file through `AddZLoggerStream`. The factory minimum level is the lower of the two; each output is then filtered by its own level through `AddFilter` by provider type.
- Interpolated string handlers check `IsEnabled` before creating a ZString builder.
- `LoggerService` checks the enabled level before forwarding string and exception overloads to `Microsoft.Extensions.Logging`.

## Console Output

| Log level | Unity method |
| --- | --- |
| `Trace` | `Debug.Log` |
| `Debug` | `Debug.Log` |
| `Information` | `Debug.Log` |
| `Warning` | `Debug.LogWarning` |
| `Error` | `Debug.LogError` |
| `Critical` | `Debug.LogError` |

Up to 2.0.1 the console was fed through a `Stream`, and ZLogger's background writer could hand it several entries in one byte buffer. Those entries were merged into one console message, the level marker of the second and following ones leaked into the text as a letter, and the whole batch took the level of its first entry. Since 2.1.0 every entry is delivered on its own.

## Runtime API

```csharp
public static class LoggerFactory
{
    public static ILogger GetLogger(
        LogLevel minimumLogLevel = LogLevel.Error,
        string loggerFilePath = null);

    public static ILogger GetSessionLogger(
        LogLevel consoleMinimumLevel,
        SessionLogOptions sessionLog);
}
```

- Both return a `Microsoft.Extensions.Logging.ILogger` with category `Application`.
- `GetLogger` defaults to `LogLevel.Error` when `minimumLogLevel` is not specified; passing `LogLevel.None` falls back to `LogLevel.Warning`.

```csharp
public sealed class SessionLogOptions
{
    public SessionLogOptions(
        string directory,
        LogLevel minimumLevel,
        int maxSessions = 5,
        long maxSessionBytes = 5 * 1024 * 1024);
}

public static class SessionLogFiles
{
    public static IReadOnlyList<string> GetSessionFiles(string directory);
}

public static class LoggerLocator
{
    public static ILoggerService Instance { get; }
    public static void Initialize(ILoggerService logger);
}
```

`SessionLogOptions` rejects an empty directory, fewer than one session and a size below one byte.

```csharp
public sealed class LoggerService : ILoggerService
{
    public LoggerService(ILogger logger);
}
```

`ILoggerService` exposes:

```csharp
bool IsEnabled(LogLevel logLevel);

void Trace(string message);
void Debug(string message);
void Information(string message);
void Warning(string message);
void Error(string message);
void Critical(string message);

void Trace(Exception exception);
void Debug(Exception exception);
void Information(Exception exception);
void Warning(Exception exception);
void Error(Exception exception);
void Critical(Exception exception);

void Trace(Exception exception, string message);
void Debug(Exception exception, string message);
void Information(Exception exception, string message);
void Warning(Exception exception, string message);
void Error(Exception exception, string message);
void Critical(Exception exception, string message);
```

The same six levels also support interpolated-string overloads:

```csharp
_logger.Debug($"Position: {transform.position}");
_logger.Error(exception, $"Failed to load item {itemId}");
```

## Tests

EditMode tests live in `Tests/Editor` (`Nk7.Logger.Tests.Editor`, category `Logger`). Add the package to `testables` in your project's `Packages/manifest.json` and run them in the Test Runner:

```json
"testables": [
  "com.nk7.logger"
]
```

The tests compile only when `com.unity.test-framework` 1.1.0+ is installed.

## Requirements

- Unity 2021.3+ with C# 10 support
- `org.nuget.zlogger` 2.5.10
- `org.nuget.zstring` 2.6.0
- UPM package name: `com.nk7.logger`
