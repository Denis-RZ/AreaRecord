# AreaRecord
Record a selected area with presets, hotkeys, and live stats.

The repository now separates the codebase into two main parts:

- `website/` – ASP.NET Core web application.
- `extension/` – Chrome extension files.
  The solution file `MyWebApp.sln` resides in `website/`.

## Restoring Dependencies

After cloning the repository, restore the required packages for the website:

```bash
cd website
dotnet restore MyWebApp.sln
libman restore
```

## Configuring the Database Connection

`appsettings.json` contains the default connection string used by the
application:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MyWebAppDb;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

You can override this value in several ways:

1. Create an `appsettings.{Environment}.json` file (for example
   `appsettings.Production.json`) containing a different `DefaultConnection`.
2. Set the environment variable `ConnectionStrings__DefaultConnection`.
3. Pass a command‑line argument `--ConnectionStrings:DefaultConnection="<your connection string>"`.

When switching to another provider (e.g. PostgreSQL or SQLite) update
`Program.cs` so the `DbContext` uses the appropriate `Use*` method instead of
`UseSqlServer`.

## Database Migrations

Entity Framework Core migrations are used to create and evolve the database schema. After modifying the `DbContext` or model classes, run the following commands from the `MyWebApp` project directory:

```bash
# Add a new migration
dotnet ef migrations add <MigrationName>

# Apply migrations to the configured database
dotnet ef database update
```

To generate a SQL script that can be executed manually or on another server, use:

```bash
# Generate a SQL script for all pending migrations
dotnet ef migrations script > update.sql
```

## Deploying Migrations Remotely

1. Build the project and ensure migrations compile.
2. Copy the generated `update.sql` file to the remote server.
3. Execute the script using the database's command-line tools (for SQL Server you can use `sqlcmd`):

```bash
sqlcmd -S <server> -d <database> -i update.sql
```

This approach avoids installing the .NET SDK on the server while keeping the database schema in sync.

## Running Tests

Unit tests live in the `MyWebApp.Tests` project inside the `website` folder.
To execute them locally run:

```bash
cd website
dotnet test MyWebApp.sln
```

This will build the solution and run all tests.

## Chrome Extension

The extension source resides in the `extension/` folder. Load this folder in
Chrome's extensions page when running the extension locally or packaging it.
