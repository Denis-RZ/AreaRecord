using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MyWebApp.Services;

public class QueryLoggingInterceptor : DbCommandInterceptor
{
    private readonly ILogger<QueryLoggingInterceptor> _logger;
    private readonly QueryMetrics _metrics;
    private readonly TimeSpan _slowThreshold = TimeSpan.FromSeconds(2);

    public QueryLoggingInterceptor(ILogger<QueryLoggingInterceptor> logger, QueryMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    private void LogCommand(DbCommand command, CommandExecutedEventData eventData)
    {
        var duration = eventData.Duration;
        _metrics.Add(duration);
        _logger.LogInformation("Query executed in {Duration}ms", duration.TotalMilliseconds);
        if (duration > _slowThreshold)
        {
            _logger.LogWarning("Slow query ({Duration}ms): {Command}", duration.TotalMilliseconds, command.CommandText);
        }
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        LogCommand(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        LogCommand(command, eventData);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        LogCommand(command, eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        LogCommand(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        LogCommand(command, eventData);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        LogCommand(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }
}
