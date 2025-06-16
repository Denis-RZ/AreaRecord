# AreaRecord
Record a selected area with presets, hotkeys, and live stats.

The repository now separates the codebase into two main parts:

- `website/` – ASP.NET Core web application.
- `extension/` – Chrome extension files.
  The solution file `MyWebApp.sln` resides in `website/`.

## Restoring Dependencies

After cloning the repository, execute the setup script to restore all
dependencies:

```bash
./setup.sh
# On Windows you can run ./setup.ps1 instead
```
The script runs `dotnet restore website/MyWebApp.sln` followed by
`libman restore` in `website/MyWebApp` to download the client-side
libraries, including the Quill editor used on the admin content pages.
All submitted HTML is sanitized on the server using the `HtmlSanitizer`
library before being stored.

## Configuring the Database Connection

`appsettings.json` contains the default connection string and the provider
selection used by the application:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MyWebAppDb;Trusted_Connection=True;MultipleActiveResultSets=true"
},
"DatabaseProvider": "SqlServer"
```

You can override these values in several ways:

1. Create an `appsettings.{Environment}.json` file (for example
   `appsettings.Production.json`) containing a different `DefaultConnection`.
2. Set the environment variable `ConnectionStrings__DefaultConnection`.
3. Pass a command‑line argument `--ConnectionStrings:DefaultConnection="<your connection string>"`.
4. Set `DatabaseProvider` (`SqlServer`, `PostgreSQL` or `Sqlite`) using the same methods above.

Specify `DatabaseProvider` when switching providers; `Program.cs` will pick the correct `Use*` method automatically.

Connection strings built with `ConnectionHelper` add provider-specific optimizations. PostgreSQL enables pooling and prepared statements, SQLite uses a shared cache and enables WAL mode at startup, and SQL Server configures retries with a 60 second command timeout.

## Database Initialization

The schema is created automatically when the application starts using
`EnsureCreated`. Configure your connection string and provider as described
above and run:

```bash
cd website/MyWebApp
dotnet run
```

On first launch the database will be created and indexes added for the selected
provider. The `/Setup` page lets you test connections or switch providers and
will recreate the schema if necessary.

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

## Setup & Diagnostics

1. Navigate to the web application project and start the development server:

```bash
cd website/MyWebApp
dotnet run
```

2. With the site running, open your browser to `/Setup` to confirm the
   database connection can be established. The schema is created automatically
   at startup through `EnsureCreated()` when a connection is available.

3. For a quick reminder on how to import data manually,
   visit `/Setup/Import` while the site is running. The page shows the shell
   commands used to run the project and seed sample data.
