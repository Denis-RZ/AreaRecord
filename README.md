# AreaRecord
Record a selected area with presets, hotkeys, and live stats.

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
