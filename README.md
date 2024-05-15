# Caching-MySQL
[![AppVeyor build status](https://ci.appveyor.com/api/projects/status/d8hubjf1clswsd9n?svg=true)](https://ci.appveyor.com/project/ChaosEngine/caching-mysql)
[![NuGet](https://img.shields.io/nuget/v/Pomelo.Extensions.Caching.MySql.svg?style=flat-square&label=nuget)](https://www.nuget.org/packages/Pomelo.Extensions.Caching.MySql/)

Basing on https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed and modified accordingly:

### Distributed MySQL Server Cache

The Distributed MySQL Server Cache implementation (**AddDistributedMySqlCache**) allows the distributed cache to use a MySQL Server database as its backing store. To create a MySQL Server cached item table in a MySQL Server instance, you can use the `dotnet-mysql-cache` tool. The tool creates a table with the name and schema that you specify.

Create a table in MySQL Server by running the `dotnet mysql-cache create` command. Provide the MySQL Server connection string, instance (for example `server=192.169.0.1`), table name (for example, `NewTableName`) and optional database (for example, `MyDatabaseName`):

```dotnetcli
dotnet mysql-cache create "server=192.169.0.1;user id=userName;password=P4ssword123!;port=3306;database=MyDatabaseName;Allow User Variables=True" "NewTableName" --databaseName "MyDatabaseName"
```

A message is logged to indicate that the tool was successful:

```console
Table and index were created successfully.
```

The table created by the `dotnet-mysql-cache` tool has the following schema:

![MySQL Server Cache Table](<MySQL create table schema.png>)

> [!NOTE]
> An app should manipulate cache values using an instance of [IDistributedCache](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache?view=dotnet-plat-ext-8.0), not any other.

The example snippet how to implement MySql Server cache in `Program.cs`:

```cs
builder.Services.AddDistributedMySqlCache(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("DistCache_ConnectionString");
    options.SchemaName = "MyDatabaseName";  //optional
    options.TableName = "NewTableName";     //required
});
```

> [!NOTE]
> A **ConnectionString** (and optionally, **SchemaName** and **TableName**) are typically stored outside of source control (for example, stored by the [Secret Manager](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-8.0&tabs=windows) or in `appsettings.json`/`appsettings.{Environment}.json` files). The connection string may contain credentials that should be kept out of source control systems.

## Use the distributed cache

One can use same technique as described in this section [Use the distributed cache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-8.0#use-the-distributed-cache)
