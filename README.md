# nk7-logger

Lightweight logging service for Unity built on `Microsoft.Extensions.Logging`, ZLogger and ZString, with allocation-conscious interpolated string handlers, Unity console output, per-session log files and an optional single log file.

## Features

- `ILoggerService` facade for `Trace`, `Debug`, `Information`, `Warning`, `Error`, and `Critical`.
- Message, exception, exception + message, and interpolated-string overloads for every log level.
- Interpolated string handlers backed by ZString: a disabled level builds nothing and allocates nothing, an enabled entry goes to the file without allocating at a steady rate. Measurements are in [Allocations](#allocations).
- Every entry reaches the Unity console as its own message with its own level: Trace/Debug/Information to `Debug.Log`, Warning to `Debug.LogWarning`, Error/Critical to `Debug.LogError`.
- `LoggerFactory.GetSessionLogger` writes each app session to its own file, keeps the last sessions with a size cap, and filters the console and the file by separate levels.
- `LoggerLocator` gives packages and the app one shared logger: it writes to the Unity console until the app plugs in its own.
- `LoggerFactory.Close` and `LoggerFactory.Shutdown` write the pending entries and close the log files; every logger is closed automatically when the app quits.
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
- [Allocations](#allocations)
- [Why Not ZLogger Directly](#why-not-zlogger-directly)
- [Session Logs](#session-logs)
- [Closing Loggers](#closing-loggers)
- [LoggerLocator](#loggerlocator)
- [Lifecycle](#lifecycle)
- [Console Output](#console-output)
- [Runtime API](#runtime-api)
- [Tests](#tests)
- [Requirements](#requirements)

## Installation

### Unity Package Manager
1. The package depends on `org.nuget.zlogger` and `org.nuget.zstring`. Unity does not fetch dependencies of a Git package from a registry the project does not know, so add the OpenUPM registry with the `org.nuget` scope to `Packages/manifest.json`:

```json
"scopedRegistries": [
  {
    "name": "package.openupm.com",
    "url": "https://package.openupm.com",
    "scopes": [
      "org.nuget"
    ]
  }
]
```

2. Open Unity Package Manager (`Window -> Package Manager`).
3. Click `+ -> Add package from git URL...`.
4. Enter `https://github.com/lsd7nk/nk7-logger.git?path=src`.

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

## Allocations

What a call like `_logger.Information($"value {number}")` costs on the thread that logs:

- **Disabled level.** The handler checks `IsEnabled` first, so the text is not built and nothing is allocated.
- **Enabled level, file output.** The text is built in a ZString buffer, copied into a pooled entry and put into a queue. The package's background thread writes it to the file as UTF-8. No `string` is created and, at a steady rate, nothing is allocated.
- **Enabled level, console output.** `Debug.Log` takes a `string`, so each entry that reaches the console creates one.
- A value with a format specifier or alignment (`{value:D3}`, `{value,10}`) is formatted through a temporary `string`.
- A message longer than 1024 characters gets a buffer of its own, which is not kept for reuse.
- The `string` overloads (`_logger.Information("text")`) pass the string you give them and check the level before the call goes further.

Number of GC allocations on the logging thread, one `int` in the message, console off, file on (`LoggerAllocationTests`, Unity 6000.0.66f2, Editor, EditMode):

| Calls | `ILoggerService` | ZLogger `ZLog*($"...")` with ZLogger's file writer | `ILogger.LogX($"...")` |
| --- | --- | --- | --- |
| 1000 `Debug` calls, level disabled | 0 | 0 | 2000 |
| 200 `Information` calls 2 ms apart, level enabled | 0 | 800 to 802 | 1200 |
| 1000 `Information` calls in a row, level enabled | 842 to 1141 the first time, 1 to 108 when repeated | 3003, and 3001 when repeated | 6124 to 6196 |

Where a cell has a range, the numbers come from several runs.

- The numbers count allocations, not bytes, and cover only the thread that logs. This table is from the Editor; the numbers from a phone are below.
- Calls in a row are logged faster than the file is written, so entries wait in the queue and each holds a pooled object. The first such run creates those objects (up to 1024 are kept), a repeated run reuses them.
- With an output that only takes the entry and gives it back, an enabled entry allocates nothing in both modes (`EnabledEntryAllocatesNothingBeforeTheOutput`).

The same calls in a development IL2CPP build on an Android phone (Samsung SM-A145F, Android 15, arm64, `LoggerPlayerAllocationTests`):

| Calls | `ILoggerService` | ZLogger `ZLog*($"...")` with ZLogger's file writer | `ILogger.LogX($"...")` |
| --- | --- | --- | --- |
| 1000 `Debug` calls, level disabled | 0 | 0 | 2000 |
| 200 `Information` calls 2 ms apart, level enabled | 0 | 800 | 1200 |
| 1000 `Information` calls in a row, level enabled | 871 the first time, 0 when repeated | 3036, and 3020 when repeated | not measured |

The counter these tests use works only in the Editor and in development builds, so a release build was not measured.

The package writes files with its own background thread because ZLogger's file writer costs one allocation per entry at a steady rate: it takes entries through a `Channel`, and a write that finds its reader waiting allocates to wake it. Both writers measured with the same calls, 200 of them 2 ms apart (`ZLoggerOwnMethodsForComparison`):

| Calls | The package's file writer | ZLogger's file writer |
| --- | --- | --- |
| `ILoggerService.Information($"...")` | 0 | 202 |
| ZLogger `ZLogInformation($"...")` | 600 | 800 |

The other three allocations of `ZLog*` happen before the writer and do not depend on the arguments: with an output that discards the entry, 1000 calls gave 3000 with no argument, with one `int`, with two and with a `string`. `ZLog*` hands its log state to `ZLoggerLogger.Log<TState>` as a struct, and that method type-tests and casts the state before it creates the entry. In the Unity Editor each type test or interface cast of a struct through a generic parameter allocated once (1000 for 1000 calls, measured apart from ZLogger). The package passes a class, so the same checks cost it nothing. On the phone `ZLog*` cost the same three allocations per call before the writer (600 for 200 calls with the package's file writer); the separate measurement of type tests was not obtained there.

On WebGL the package does not start a thread and leaves file output to ZLogger's writer, so that allocation stays there. A WebGL build was not tested; the tests run that path in the Editor.

## Why Not ZLogger Directly

ZLogger formats the entries and carries them through `ILogger`. The package adds what an app usually writes around it:

- `ILoggerService`: a small interface for call sites, with `EmptyLoggerService` to turn logging off. Call sites need no ZLogger types.
- File writer: a queue and a background thread of its own, so handing an entry to the file does not allocate.
- Session files: a file per launch, the last sessions kept, a size cap per session.
- `LoggerLocator`: one logger for packages and the app, with the Unity console as the fallback.
- Closing: `Close`, `Shutdown`, and automatic closing on quit and on Play Mode start, including with Domain Reload disabled.
- Console output: every entry is its own `Debug.Log*` call with its own level, made on the thread that logs.

Entries written through `ILoggerService` are plain text: they carry no structured parameters. For structured or JSON logs use ZLogger directly.

## Session Logs

`GetSessionLogger` opens a new file on every launch and sets the console and file levels separately:

```csharp
ILogger logger = LoggerFactory.GetSessionLogger(
    consoleMinimumLevel: LogLevel.Error,
    sessionLog: new SessionLogOptions(
        directory: Path.Combine(Application.persistentDataPath, "Logs"),
        minimumLevel: LogLevel.Information,
        maxSessions: 10,                       // optional, 5 by default
        maxSessionBytes: 20 * 1024 * 1024));   // optional, 5 MB by default
```

- The session file is `session-<UTC time>.log` in `directory`. Each entry starts with the UTC time and the short level: `2026-10-29 07:53:52.123 [INF] Game started`.
- The last `maxSessions` sessions are kept (5 by default), the current one included. Older `session-*.log` files are deleted when a new session starts; other files in the folder are left alone.
- A session is written up to `maxSessionBytes` (5 MB by default). Entries that do not fit are dropped until the session ends.
- A new file name is always later than the newest existing one, so a device clock turned back still puts the current session first and drops the oldest ones.
- The file is written on the package's background thread and opened with `FileShare.ReadWrite`, so it can be read while it is being written.
- `SessionLogFiles.GetSessionFiles(directory)` returns the session file paths, newest first.
- `LogLevel.None` for the console or the file turns that output off.

## Closing Loggers

Files are written on a background thread. Close a logger to be sure its last entries are in the file and to release the file:

```csharp
LoggerFactory.Close(logger);   // one logger
LoggerFactory.Shutdown();      // every logger the factory created
```

- `Close` returns once everything logged so far is in the file and the file is closed. Entries logged to that logger afterwards do not reach the file and do not throw.
- Closing the same logger twice, `null` or a logger the factory did not create does nothing.
- `Shutdown` runs by itself when the app quits (`Application.quitting`) and on every Play Mode start, so loggers left from the previous run are closed even with Domain Reload disabled. Loggers created in Edit Mode are closed by it too.

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
- On every Play Mode start the locator forgets the app logger, so with Domain Reload disabled the logger of the previous run is not reused. Call `Initialize` at startup of every run.

## Lifecycle
- `LoggerFactory.GetLogger` builds an `ILogger` pipeline with Unity console output and an optional file sink.
- Unity console output goes through `UnityDebugLogProcessor`: it receives every entry whole and writes it with one `Debug.Log`, `Debug.LogWarning` or `Debug.LogError` call, using the entry's own level.
- `Debug.Log*` is called synchronously on the thread that logs, not on a background thread. Entries with `LogLevel.None` are not written to the console.
- File output: entries are put into a queue and written by one background thread per logger (`Nk7.Logger file writer`) with ZLogger plain-text formatting. Every entry is written to the file on its own, and the file is flushed after each batch taken from the queue.
- The console and the file are served by one ZLogger log processor, which filters each output by its own level. The factory minimum level is the lower of the two.
- On WebGL the file goes through `AddZLoggerFile` and `AddZLoggerStream`, and each output is filtered by its own level through `AddFilter` by provider type.
- Interpolated string handlers check `IsEnabled` before creating a ZString builder. For an enabled level the text goes into a pooled entry that ZLogger writes as UTF-8; the entry returns to the pool once every output has written it.
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

    public static void Close(ILogger logger);

    public static void Shutdown();
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

`Tests/Runtime` (`Nk7.Logger.Tests.Runtime`) holds one PlayMode test, `LoggerPlayerAllocationTests`. It measures the allocation counts on the platform it runs on, the Editor or a development player on a device, and writes them to the test message and to the log, every line starting with `[nk7-logger-allocations]`.

## Requirements

- Unity 2022.2+ with C# 10 support
- `org.nuget.zlogger` 2.5.10
- `org.nuget.zstring` 2.6.0
- UPM package name: `com.nk7.logger`
