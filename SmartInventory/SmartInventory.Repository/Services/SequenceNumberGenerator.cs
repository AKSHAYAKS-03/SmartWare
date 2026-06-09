using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Repository.Services;

public class SequenceNumberGenerator : ISequenceNumberGenerator
{
    private readonly AppDbContext _context;

    public SequenceNumberGenerator(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateAsync(string sequenceName, string prefix)
    {
        if (string.IsNullOrWhiteSpace(sequenceName))
            throw new ArgumentException("Sequence name cannot be empty.", nameof(sequenceName));
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix cannot be empty.", nameof(prefix));

        var sql = $"SELECT CONCAT('{prefix}-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('{sequenceName}')::text, 6, '0'))";

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            var result = await command.ExecuteScalarAsync();
            return result?.ToString() ?? throw new InvalidOperationException($"Failed to generate value for {sequenceName}.");
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }
}
