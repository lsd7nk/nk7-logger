# nk7-logger

Лёгкий логгер для Unity на базе `Microsoft.Extensions.Logging`, ZLogger и ZString с интерполированными строками без сборки сообщений при выключенном уровне, выводом в консоль Unity, файлами сессий и опциональным единым файлом лога.

## Особенности

- Фасад `ILoggerService` для уровней `Trace`, `Debug`, `Information`, `Warning`, `Error` и `Critical`.
- Перегрузки для сообщения, исключения, исключения + сообщения и интерполированной строки на каждом уровне.
- Интерполированные строковые обработчики на ZString: на выключенном уровне сообщение не собирается и ничего не аллоцируется, на включённом запись уходит в файл без аллокаций при равномерной записи. Замеры — в разделе [Аллокации](#аллокации).
- Каждая запись попадает в консоль Unity отдельным сообщением со своим уровнем: Trace/Debug/Information в `Debug.Log`, Warning в `Debug.LogWarning`, Error/Critical в `Debug.LogError`.
- `LoggerFactory.GetSessionLogger` пишет каждую сессию приложения в отдельный файл, хранит последние сессии с лимитом размера и фильтрует консоль и файл по своим уровням.
- `LoggerLocator` даёт пакетам и приложению один общий логгер: пока приложение не подключило свой, он пишет в консоль Unity.
- `LoggerFactory.Close` и `LoggerFactory.Shutdown` дописывают накопленные записи и закрывают файлы логов; при выходе из приложения все логгеры закрываются автоматически.
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
- [Аллокации](#аллокации)
- [Почему не ZLogger напрямую](#почему-не-zlogger-напрямую)
- [Логи сессий](#логи-сессий)
- [Закрытие логгеров](#закрытие-логгеров)
- [LoggerLocator](#loggerlocator)
- [Жизненный цикл](#жизненный-цикл)
- [Вывод в консоль Unity](#вывод-в-консоль-unity)
- [Runtime API](#runtime-api)
- [Тесты](#тесты)
- [Требования](#требования)

## Установка

### Unity Package Manager
1. Пакет зависит от `org.nuget.zlogger` и `org.nuget.zstring`. Unity не скачивает зависимости git-пакета из реестра, которого нет в проекте, поэтому добавьте реестр OpenUPM со scope `org.nuget` в `Packages/manifest.json`:

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

2. Откройте Unity Package Manager (`Window -> Package Manager`).
3. Нажмите `+ -> Add package from git URL...`.
4. Вставьте `https://github.com/lsd7nk/nk7-logger.git?path=src`.

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

## Аллокации

Во что обходится вызов вида `_logger.Information($"value {number}")` в потоке, который пишет лог:

- **Уровень выключен.** Обработчик сначала проверяет `IsEnabled`, поэтому текст не собирается и ничего не аллоцируется.
- **Уровень включён, вывод в файл.** Текст собирается в буфере ZString, копируется в запись из пула и кладётся в очередь. Фоновый поток пакета пишет её в файл в UTF-8. `string` не создаётся, и при равномерной записи ничего не аллоцируется.
- **Уровень включён, вывод в консоль.** `Debug.Log` принимает `string`, поэтому каждая запись, дошедшая до консоли, создаёт одну строку.
- Значение со спецификатором формата или выравниванием (`{value:D3}`, `{value,10}`) форматируется через временную `string`.
- Сообщение длиннее 1024 символов получает собственный буфер, который не сохраняется для повторного использования.
- Строковые перегрузки (`_logger.Information("text")`) передают вашу строку как есть и проверяют уровень до того, как вызов пойдёт дальше.

Количество GC-аллокаций в потоке, который пишет лог: один `int` в сообщении, консоль выключена, файл включён (`LoggerAllocationTests`, Unity 6000.0.66f2, редактор, EditMode):

| Вызовы | `ILoggerService` | `ILogger.LogX($"...")` |
| --- | --- | --- |
| 1000 вызовов `Debug`, уровень выключен | 0 | 2000 |
| 200 вызовов `Information` с паузой 2 мс, уровень включён | 0 | 1200 |
| 1000 вызовов `Information` подряд, уровень включён | 1141 в первый раз, 1 при повторе | 6191 |

- Числа — это количество аллокаций, а не байты, и только в потоке, который пишет лог. Замер сделан в редакторе; в билде не измерялось.
- Вызовы подряд идут быстрее, чем пишется файл, поэтому записи ждут в очереди и каждая держит объект из пула. Первая такая серия создаёт эти объекты (хранится до 1024), повторная их переиспользует.
- С выводом, который только принимает запись и возвращает её, включённая запись не аллоцирует ничего в обоих режимах (`EnabledEntryAllocatesNothingBeforeTheOutput`).

Пакет пишет файлы собственным фоновым потоком, потому что файловый писатель ZLogger стоит одну аллокацию на запись при равномерной записи: он получает записи через `Channel`, и запись, которая застала читателя в ожидании, аллоцирует, чтобы его разбудить. Замер до замены: 200 аллокаций на 200 вызовов с паузой 2 мс и 202 на 200 записей в голый `Channel` с теми же настройками.

В WebGL пакет не запускает поток и оставляет запись в файл писателю ZLogger, поэтому там эта аллокация остаётся. В WebGL-билде не проверялось; тесты гоняют этот путь в редакторе.

## Почему не ZLogger напрямую

ZLogger форматирует записи и проводит их через `ILogger`. Пакет добавляет то, что приложение обычно пишет вокруг него:

- `ILoggerService`: небольшой интерфейс для мест вызова и `EmptyLoggerService`, чтобы выключить логирование. Местам вызова не нужны типы ZLogger.
- Файловый писатель: своя очередь и свой фоновый поток, поэтому передача записи в файл не аллоцирует.
- Файлы сессий: файл на запуск, хранение последних сессий, лимит размера сессии.
- `LoggerLocator`: один логгер для пакетов и приложения, с консолью Unity как запасным выводом.
- Закрытие: `Close`, `Shutdown` и автоматическое закрытие при выходе и при старте Play Mode, в том числе с выключенным Domain Reload.
- Вывод в консоль: каждая запись — отдельный вызов `Debug.Log*` со своим уровнем, в том потоке, который пишет лог.

Записи через `ILoggerService` — обычный текст, без структурных параметров. Для структурных или JSON-логов используйте ZLogger напрямую.

## Логи сессий

`GetSessionLogger` открывает новый файл на каждый запуск и задаёт уровни консоли и файла по отдельности:

```csharp
ILogger logger = LoggerFactory.GetSessionLogger(
    consoleMinimumLevel: LogLevel.Error,
    sessionLog: new SessionLogOptions(
        directory: Path.Combine(Application.persistentDataPath, "Logs"),
        minimumLevel: LogLevel.Information,
        maxSessions: 10,                       // необязательно, по умолчанию 5
        maxSessionBytes: 20 * 1024 * 1024));   // необязательно, по умолчанию 5 МБ
```

- Файл сессии — `session-<UTC-время>.log` в `directory`. Запись начинается с UTC-времени и короткого уровня: `2026-10-29 07:53:52.123 [INF] Game started`.
- Хранятся последние `maxSessions` сессий (по умолчанию 5), включая текущую. Более старые файлы `session-*.log` удаляются при старте новой сессии, чужие файлы в папке не трогаются.
- Сессия пишется до `maxSessionBytes` (по умолчанию 5 МБ). Записи, которые не помещаются, отбрасываются до конца сессии.
- Имя нового файла всегда позже самого нового из существующих, поэтому при переведённых назад часах устройства текущая сессия всё равно первая, а удаляются самые старые.
- Файл пишется в фоновом потоке пакета и открыт с `FileShare.ReadWrite`, поэтому его можно читать во время записи.
- `SessionLogFiles.GetSessionFiles(directory)` отдаёт пути файлов сессий, от новой к старой.
- `LogLevel.None` для консоли или файла выключает этот вывод.

## Закрытие логгеров

Файлы пишутся в фоновом потоке. Закройте логгер, чтобы последние записи гарантированно оказались в файле, а сам файл освободился:

```csharp
LoggerFactory.Close(logger);   // один логгер
LoggerFactory.Shutdown();      // все логгеры, созданные фабрикой
```

- `Close` возвращает управление, когда всё записанное до этого момента уже в файле, а файл закрыт. Записи в этот логгер после закрытия в файл не попадают и исключений не бросают.
- Повторное закрытие того же логгера, `null` или логгер, созданный не фабрикой, ничего не делают.
- `Shutdown` вызывается сам при выходе из приложения (`Application.quitting`) и при каждом старте Play Mode, поэтому логгеры прошлого запуска закрываются даже при выключенном Domain Reload. Логгеры, созданные в Edit Mode, он тоже закрывает.

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
- При каждом старте Play Mode локатор забывает логгер приложения, поэтому при выключенном Domain Reload логгер прошлого запуска не переиспользуется. Вызывайте `Initialize` при старте каждого запуска.

## Жизненный цикл
- `LoggerFactory.GetLogger` собирает пайплайн `ILogger` с выводом в консоль Unity и опциональным файловым выводом.
- Вывод в консоль идёт через `UnityDebugLogProcessor`: он получает каждую запись целиком и пишет её одним вызовом `Debug.Log`, `Debug.LogWarning` или `Debug.LogError` по уровню самой записи.
- `Debug.Log*` вызывается синхронно в том потоке, который пишет лог, а не в фоновом. Записи с уровнем `LogLevel.None` в консоль не пишутся.
- Файловый вывод: записи кладутся в очередь, и их пишет один фоновый поток на логгер (`Nk7.Logger file writer`) с plain-text форматированием ZLogger. Каждая запись пишется в файл отдельно, а файл сбрасывается после каждой пачки, взятой из очереди.
- Консоль и файл обслуживает один log processor ZLogger, который фильтрует каждый вывод по его уровню. Минимальный уровень фабрики — меньший из двух.
- В WebGL файл идёт через `AddZLoggerFile` и `AddZLoggerStream`, а каждый вывод фильтруется своим уровнем через `AddFilter` по типу провайдера.
- Интерполированные строковые обработчики проверяют `IsEnabled` до создания ZString builder. На включённом уровне текст попадает в запись из пула, которую ZLogger пишет в UTF-8; запись возвращается в пул, когда её записали все выводы.
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

    public static void Close(ILogger logger);

    public static void Shutdown();
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

- Unity 2022.2+ с поддержкой C# 10
- `org.nuget.zlogger` 2.5.10
- `org.nuget.zstring` 2.6.0
- UPM package name: `com.nk7.logger`
