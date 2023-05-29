# Serilog logging and EF Core logging

One of the most important tools a developer has for debugging code is to write logs to a log file and optionally write to the console. For logging while writing code a developer starting out with logging may elect to use one log file and over time can become difficult to get to current entries.

The goal of this article is to provide methods to log to a file for non-data related details and a file for logging data related details.

> **Note**
> All code has been written in Razor Pages using Entity Framework Core 7 in Microsoft Visual Studio 2022. Should be easy to use the code in an ASP.NET Core project.


## NuGet packages used

- Serilog.AspNetCore - 6.1.0
- Serilog.Extensions.Logging.File - 3.0.0
- Serilog.Sinks.Console - 4.1.0
- Serilog.Sinks.File - 5.0.0
- SeriLogThemesLibrary - 1.0.0.1
- Microsoft.EntityFrameworkCore.SqlServer - 7.0.2

## Log to

Each day a new folder is created under the root of the project with the date `LogFiles\yyyy-mm-dd`. Beneath this folder a file named `log.txt` for non-data details.txt and `EF_Log.txt` for EF Core logging.

## Log class for non-data information

- **Development** writes to a folder under debug\bin
- **Production** write to the same location as Development but for a real production environment set a path to where there is a central location for logs and provide a different unique name.

```csharp
public class SetupLogging
{
    public static void Development()
    {

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console(theme: SeriLogCustomThemes.Theme1())
            .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogFiles", $"{Now.Year}-{Now.Month}-{Now.Day}", "Log.txt"),
                rollingInterval: RollingInterval.Infinite,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}")
            .CreateLogger();
    }

    public static void Production()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogFiles", "Log.txt"),
                rollingInterval: RollingInterval.Day)
            .CreateBootstrapLogger();
    }
}
```

In Program.cs use the above methods dependent on environments.

```csharp
if (builder.Environment.IsDevelopment())
{
    SetupLogging.Development();
}
else
{
    SetupLogging.Production();
}
```

EF Core logging

Serilog will generate folders for logging as needed but not for EF Core so to ensure the log folder exists we add the following to the project file.

```xml
<Target Name="MakeMyDir" AfterTargets="Build">
    <MakeDir Directories="$(OutDir)LogFiles" />
</Target>
```

Next, the following class is responsible for EF Core logging.

```csharp
public class DbContextToFileLogger
{
    /// <summary>
    /// Log file name
    /// </summary>
    private readonly string _fileName = 
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
            "LogFiles", $"{Now.Year}-{Now.Month}-{Now.Day}", $"EF_Log.txt");

    /// <summary>
    /// Use to override log file name and path
    /// </summary>
    /// <param name="fileName"></param>
    public DbContextToFileLogger(string fileName)
    {
        _fileName = fileName;
    }

    /// <summary>
    /// Setup to use default file name for logging
    /// </summary>
    public DbContextToFileLogger()
    {

    }
    /// <summary>
    /// append message to the existing stream
    /// </summary>
    /// <param name="message"></param>
    [DebuggerStepThrough]
    public void Log(string message)
    {

        if (!File.Exists(_fileName))
        {
            File.CreateText(_fileName).Close();
        }

        StreamWriter streamWriter = new(_fileName, true);

        streamWriter.WriteLine(message);

        streamWriter.WriteLine(new string('-', 40));

        streamWriter.Flush();
        streamWriter.Close();
    }
}
```

Configuration is done in Program.cs

```csharp
if (builder.Environment.IsDevelopment())
{

    SetupLogging.Development();
    builder.Services.SensitiveDataLoggingConnection(builder);
}
else
{
    SetupLogging.Production();
    builder.Services.ProductionLoggingConnection(builder);
}
```

Using the following class

- As coded the connection string is read from appsettings
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=MockupApplication;Integrated Security=True"
  }
}
```
- There are several different configurations which a developer can modified to suit their needs. Take to to study these methods rather than copy-n-paste and use.

## Peek ahead

In the next article on Serilog we will look a conditional logging, how to read from appsettings.json to decide on to log or not to log.

```csharp
public static class DbContexts
{
    /// <summary>
    /// Test connection with exception handling
    /// </summary>
    /// <param name="context"><see cref="DbContext"/></param>
    /// <param name="ct">Provides a shorter time out from 30 seconds to in this case one second</param>
    /// <returns>true if database is accessible</returns>
    /// <remarks>
    /// Running asynchronous as synchronous.
    /// </remarks>
    public static bool CanConnectAsync(this DbContext context, CancellationToken ct)
    {
        try
        {
            return context.Database.CanConnectAsync(ct).Result;

        }
        catch
        {
            return false; 
        }
    }

    /// <summary>
    /// Enable sensitive logging for EF Core
    /// </summary>
    public static void SensitiveDataLoggingConnection(this IServiceCollection collection, WebApplicationBuilder builder)
    {

        collection.AddDbContextPool<Context>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
                .EnableSensitiveDataLogging()
                .LogTo(new DbContextToFileLogger().Log));
    }

    /// <summary>
    /// Single line logging with sensitive data enabled for EF Core
    /// </summary>
    public static void SingleLineSensitiveDataLoggingConnection(this IServiceCollection collection, WebApplicationBuilder builder)
    {

        collection.AddDbContextPool<Context>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
                .EnableSensitiveDataLogging().LogTo(
                    new DbContextToFileLogger().Log,
                    LogLevel.Debug,
                    DbContextLoggerOptions.DefaultWithLocalTime | DbContextLoggerOptions.SingleLine));

    }
    /// <summary>
    /// Production logging for EF Core
    /// </summary>
    /// <param name="collection"></param>
    public static void ProductionLoggingConnection(this IServiceCollection collection, WebApplicationBuilder builder)
    {
        
        collection.AddDbContextPool<Context>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
                .LogTo(
                    new DbContextToFileLogger().Log));

    }
}
```

## Trying the code out with an exception

Click the button on the Index page to invoke an exception which uses poor programming 

```csharp
public void OnPostInvokeException(int id)
{
    Log.Information("OnGet - before throwing exception with {P1}", id);
    try
    {
        // there is no record with this id
        var user = _context.UserLogin.First(x => x.Id == id);
    }
    catch (Exception e)
    {
        Log.Error(e, "");
    }

    Log.Information("OnGet - after throwing exception");
}
```

Use this instead of the above.

```csharp
public void OnPostInvokeException(int id)
{
    var user = _context.UserLogin.FirstOrDefault(x => x.Id == id);
    if (user == null)
    {
        Log.Information("No user with an id of {P1}", id);
    }
    else
    {
        Log.Information("Found user with email address {P1}", user.EmailAddress);
    }

}
```

## Summary

The code presented provides easy methods to split regular logging from EF Core logging in folders for the current day. Take time to read about configurations.

- [Configuration Basics](https://github.com/serilog/serilog/wiki/Configuration-Basics)

## Source code

Clone the following [GitHub repository](https://github.com/karenpayneoregon/serilog-ef-core-basics).

