# nk7-logger

Лёгкий логгер для Unity на базе `Microsoft.Extensions.Logging`, ZLogger и ZString с интерполированными строками без сборки сообщений при выключенном уровне, выводом в консоль Unity, файлами сессий и опциональным единым файлом лога.

## Особенности

- Фасад `ILoggerService` для уровней `Trace`, `Debug`, `Information`, `Warning`, `Error` и `Critical`.
- Перегрузки для сообщения, исключения, исключения + сообщения и интерполированной строки на каждом уровне.
- Интерполированные строковые обработчики на ZString: сообщение не собирается, если уровень логирования выключен.
- Каждая запись попадает в консоль Unity отдельным сообщением со своим уровнем: Trace/Debug/Information в `Debug.Log`, Warning в `Debug.LogWarning`, Error/Critical в `Debug.LogError`.
- `LoggerFactory.GetSessionLogger` пишет каждую сессию приложения в отдельный файл, хранит последние сессии с лимитом размера и фильтрует консоль и файл по своим уровням.
- `LoggerLocator` даёт пакетам и приложению один общий логгер: пока приложение не подключило свой, он пишет в консоль Unity.
- `EmptyLoggerService` — no-op реализация для отключённого логирования.
- Пакет включает `Runtime/csc.rsp` с `-langversion:10` для обработчиков интерполированных строк.

## Содержание
- [Установка](#установка)
  - [Unity Package Manager](#unity-package-manager)
  - [Ручная установка](#ручная-установка)
- [Быстрый старт](#быстрый-старт)
  - [1. Включите C# 10](#1-включите-c-10)
  - [2. Создайте логгер](#2-создайте-логгер)
  - [3. Оберните его](#3-оберните-его)
  - [4. Пишите логи](#4-пишите-логи)
- [Логи сессий](#логи-сессий)
- [LoggerLocator](#loggerlocator)
- [Жизненный цикл](#жизненный-цикл)
- [Вывод в консоль Unity](#вывод-в-консоль-unity)
- [Runtime API](#runtime-api)
- [Тесты](#тесты)
- [Требования](#требования)

## Установка

### Unity Package Manager
1. Откройте Unity Package Manager (`Window -> Package Manager`).
2. Нажмите `+ -> Add package from git URL...`.
3. Вставьте `https://github.com/s-elovikov/nk7-logger.git?path=src`.

Unity не обновляет git-пакеты автоматически — при необходимости меняйте хеш вручную или используйте [UPM Git Extension](https://github.com/mob-sakai/UpmGitExtension).

### Ручная установка
Скопируйте папку `src` в проект и добавьте ссылку на asmdef `Nk7.Logger`.

## Быстрый старт

### 1. Включите C# 10
- C# 10 настраивается пакетом через `Runtime/csc.rsp` (`-langversion:10`).
- `csc.rsp` действует только на сборку пакета. Чтобы `$"..."` в вашем коде шли через обработчики, вашей сборке тоже нужен `-langversion:10`.
- Если в проекте используются свои response-файлы компилятора, сохраните версию языка не ниже 10.

### 2. Создайте логгер
Используйте `LoggerFactory.GetLogger`, чтобы получить `Microsoft.Extensions.Logging.ILogger`.

```csharp
using Microsoft.Extensions.Logging;
using Nk7.Logger;

ILogger logger = LoggerFactory.GetLogger(
    minimumLogLevel: LogLevel.Information,
    loggerFilePath: "Logs/app.log");
```

`loggerFilePath` опционален. Если путь передан, недостающие директории создаются автоматически, а существующий файл очищается. Чтобы хранить файл на каждый запуск, используйте [логи сессий](#логи-сессий).

### 3. Оберните его
Оберните логгер в `LoggerService`, чтобы использовать удобный API `ILoggerService`.

```csharp
ILoggerService loggerService = new LoggerService(logger);
```

Если логирование нужно отключить, используйте `EmptyLoggerService`.

```csharp
ILoggerService loggerService = new EmptyLoggerService();
```

### 4. Пишите логи
Для простых сообщений используйте строковые перегрузки, а для пропуска форматирования на выключенных уровнях — интерполированные строки.

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

        _logger.Information($"Игрок {playerId} подключился");
        _logger.Warning("Предупреждение о низкой памяти");
        _logger.Error(new System.Exception("Boom"), $"Не удалось загрузить профиль {playerId}");
    }
}
```

## Логи сессий

`GetSessionLogger` открывает новый файл на каждый запуск и задаёт уровни консоли и файла по отдельности:

```csharp
ILogger logger = LoggerFactory.GetSessionLogger(
    consoleMinimumLevel: LogLevel.Error,
    sessionLog: new SessionLogOptions(
        directory: Path.Combine(Application.persistentDataPath, "Logs"),
        minimumLevel: LogLevel.Information));
```

- Файл сессии — `session-<UTC-время>.log` в `directory`. Запись начинается с UTC-времени и короткого уровня: `2026-10-29 07:53:52.123 [INF] Game started`.
- Хранятся последние `maxSessions` сессий (по умолчанию 5), включая текущую. Более старые файлы `session-*.log` удаляются при старте новой сессии, чужие файлы в папке не трогаются.
- Сессия пишется до `maxSessionBytes` (по умолчанию 5 МБ). Записи, которые не помещаются, отбрасываются до конца сессии.
- Имя нового файла всегда позже самого нового из существующих, поэтому при переведённых назад часах устройства текущая сессия всё равно первая, а удаляются самые старые.
- Файл пишется в фоновом потоке ZLogger и открыт с `FileShare.ReadWrite`, поэтому его можно читать во время записи.
- `SessionLogFiles.GetSessionFiles(directory)` отдаёт пути файлов сессий, от новой к старой.
- `LogLevel.None` для консоли или файла выключает этот вывод.

## LoggerLocator

Одна точка, через которую пакеты и приложение берут логгер, не зная, как он устроен:

```csharp
LoggerLocator.Instance.Information("Purchase started");
```

- Пока приложение не задало логгер, `Instance` пишет все уровни в консоль Unity — так же, как `Debug.Log`, `Debug.LogWarning` и `Debug.LogError`.
- Приложение задаёт свой логгер один раз при старте, после этого записи пакетов идут в него, в том числе в файл сессии:

```csharp
var loggerService = new LoggerService(LoggerFactory.GetSessionLogger(LogLevel.Error, sessionLog));

LoggerLocator.Initialize(loggerService);
```

- `LoggerLocator.Initialize(null)` возвращает вывод в консоль Unity.
- `Instance` — всегда один и тот же объект, который передаёт каждый вызов текущему логгеру. Его можно сохранить в поле до `Initialize`: после `Initialize` записи всё равно пойдут в логгер приложения.

## Жизненный цикл
- `LoggerFactory.GetLogger` собирает пайплайн `ILogger` с выводом в консоль Unity и опциональным файловым выводом.
- Вывод в консоль идёт через `UnityDebugLogProcessor`: он получает каждую запись целиком и пишет её одним вызовом `Debug.Log`, `Debug.LogWarning` или `Debug.LogError` по уровню самой записи.
- `Debug.Log*` вызывается синхронно в том потоке, который пишет лог, а не в фоновом потоке ZLogger. Записи с уровнем `LogLevel.None` в консоль не пишутся.
- Файловый вывод использует plain-text форматирование ZLogger, если передан `loggerFilePath`.
- `LoggerFactory.GetSessionLogger` подключает тот же вывод в консоль и файл сессии через `AddZLoggerStream`. Минимальный уровень фабрики — меньший из двух, а каждый вывод дополнительно фильтруется своим уровнем через `AddFilter` по типу провайдера.
- Интерполированные строковые обработчики проверяют `IsEnabled` до создания ZString builder.
- `LoggerService` проверяет включённый уровень перед передачей строковых перегрузок и исключений в `Microsoft.Extensions.Logging`.

## Вывод в консоль Unity

| Уровень | Метод Unity |
| --- | --- |
| `Trace` | `Debug.Log` |
| `Debug` | `Debug.Log` |
| `Information` | `Debug.Log` |
| `Warning` | `Debug.LogWarning` |
| `Error` | `Debug.LogError` |
| `Critical` | `Debug.LogError` |

До 2.0.1 консоль получала записи через `Stream`, и фоновый писатель ZLogger мог отдать ему несколько записей в одном байтовом буфере. Такие записи склеивались в одно сообщение консоли, маркер уровня второй и следующих оставался буквой в тексте, а уровень всей пачки определялся первой записью. С 2.1.0 каждая запись доставляется отдельно.

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

- Оба метода возвращают `Microsoft.Extensions.Logging.ILogger` с категорией `Application`.
- `GetLogger` по умолчанию использует `LogLevel.Error`; при передаче `LogLevel.None` используется запасной уровень `LogLevel.Warning`.

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

`SessionLogOptions` не принимает пустую папку, меньше одной сессии и размер меньше одного байта.

```csharp
public sealed class LoggerService : ILoggerService
{
    public LoggerService(ILogger logger);
}
```

`ILoggerService` предоставляет:

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

Для тех же шести уровней есть перегрузки с интерполированными строками:

```csharp
_logger.Debug($"Position: {transform.position}");
_logger.Error(exception, $"Не удалось загрузить предмет {itemId}");
```

## Тесты

EditMode-тесты лежат в `Tests/Editor` (`Nk7.Logger.Tests.Editor`, категория `Logger`). Добавьте пакет в `testables` в `Packages/manifest.json` проекта и запустите их в Test Runner:

```json
"testables": [
  "com.nk7.logger"
]
```

Тесты компилируются, только если установлен `com.unity.test-framework` 1.1.0+.

## Требования

- Unity 2021.3+ с поддержкой C# 10
- `org.nuget.zlogger` 2.5.10
- `org.nuget.zstring` 2.6.0
- UPM package name: `com.nk7.logger`
