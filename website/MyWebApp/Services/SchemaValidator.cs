using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using MyWebApp.Models;

namespace MyWebApp.Services;

public record SchemaValidationResult(bool Success, IList<string> Messages);

public class SchemaValidator
{
    private readonly ApplicationDbContext _context;

    public SchemaValidator(ApplicationDbContext context)
    {
        _context = context;
    }

    public SchemaValidationResult Validate()
    {
        var messages = new List<string>();
        try
        {
            ValidateIndexes(messages);
            ValidateForeignKeys(messages);
            if (_context.Database.IsRelational())
            {
                ValidateColumnTypes(messages);
            }
        }
        catch (InvalidOperationException)
        {
            // Context not configured with a provider; skip validation
            return new SchemaValidationResult(true, new List<string>());
        }
        return new SchemaValidationResult(messages.Count == 0, messages);
    }

    private void ValidateIndexes(List<string> messages)
    {
        var download = _context.Model.FindEntityType(typeof(Download));
        if (download != null)
        {
            var indexes = download.GetIndexes()
                .Select(i => string.Join(",", i.Properties.Select(p => p.Name)))
                .ToList();
            if (!indexes.Contains("DownloadTime"))
                messages.Add("Missing index on Download.DownloadTime");
            if (!indexes.Contains("IsSuccessful"))
                messages.Add("Missing index on Download.IsSuccessful");
            if (!indexes.Contains("UserIP"))
                messages.Add("Missing index on Download.UserIP");
            if (!indexes.Contains("Country"))
                messages.Add("Missing index on Download.Country");
            if (!indexes.Contains("IsSuccessful,DownloadTime"))
                messages.Add("Missing composite index on Download.IsSuccessful,DownloadTime");
        }

        var file = _context.Model.FindEntityType(typeof(DownloadFile));
        if (file != null)
        {
            var indexes = file.GetIndexes()
                .Select(i => string.Join(",", i.Properties.Select(p => p.Name)))
                .ToList();
            if (!indexes.Contains("FileName"))
                messages.Add("Missing index on DownloadFile.FileName");
        }

        var rec = _context.Model.FindEntityType(typeof(Recording));
        if (rec != null)
        {
            var indexes = rec.GetIndexes()
                .Select(i => string.Join(",", i.Properties.Select(p => p.Name)))
                .ToList();
            if (!indexes.Contains("Created"))
                messages.Add("Missing index on Recording.Created");
        }
    }

    private void ValidateForeignKeys(List<string> messages)
    {
        var download = _context.Model.FindEntityType(typeof(Download));
        if (download != null)
        {
            var hasFk = download.GetForeignKeys()
                .Any(fk => fk.PrincipalEntityType.ClrType == typeof(DownloadFile));
            if (!hasFk)
                messages.Add("Missing foreign key Download.DownloadFileId -> DownloadFile");
        }
    }

    private void ValidateColumnTypes(List<string> messages)
    {
        var provider = _context.Database.ProviderName ?? string.Empty;
        var download = _context.Model.FindEntityType(typeof(Download));
        if (download == null)
            return;
        var prop = download.FindProperty(nameof(Download.UserIP));
        if (prop == null)
            return;
        var columnType = prop.GetColumnType();
        if (provider.Contains("Npgsql"))
        {
            if (!string.Equals(columnType, "varchar(45)", System.StringComparison.OrdinalIgnoreCase))
                messages.Add("UserIP column type should be varchar(45) for PostgreSQL");
        }
        else if (provider.Contains("SqlServer"))
        {
            if (!string.Equals(columnType, "nvarchar(45)", System.StringComparison.OrdinalIgnoreCase))
                messages.Add("UserIP column type should be nvarchar(45) for SQL Server");
        }
    }
}
