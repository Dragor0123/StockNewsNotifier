using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockNewsNotifier.Data.Entities;

namespace StockNewsNotifier.Data;

public static class SourceSeeder
{
    public static async Task EnsureDefaultsAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var definition in SourceDefinitions.Defaults)
        {
            var existing = await db.Sources.FirstOrDefaultAsync(s => s.Name == definition.Name);
            if (existing == null)
            {
                existing = new Source
                {
                    Name = definition.Name,
                    DisplayName = definition.DisplayName,
                    BaseUrl = definition.BaseUrl,
                    Enabled = true
                };
                db.Sources.Add(existing);
            }
            else
            {
                // keep DisplayName/BaseUrl up-to-date
                existing.DisplayName ??= definition.DisplayName;
                existing.BaseUrl = definition.BaseUrl;
                existing.Enabled = true;
            }
        }

        await db.SaveChangesAsync();

        var yahoo = await db.Sources.FirstOrDefaultAsync(s => s.Name == "YahooFinance");
        if (yahoo == null)
        {
            return;
        }

        var watchItems = await db.WatchItems
            .Include(w => w.WatchItemSources)
            .ToListAsync();

        var added = false;
        foreach (var watch in watchItems)
        {
            if (!watch.WatchItemSources.Any(ws => ws.SourceId == yahoo.Id))
            {
                watch.WatchItemSources.Add(new WatchItemSource
                {
                    WatchItemId = watch.Id,
                    SourceId = yahoo.Id,
                    Enabled = true
                });
                added = true;
            }
        }

        if (added)
        {
            await db.SaveChangesAsync();
        }
    }
}
