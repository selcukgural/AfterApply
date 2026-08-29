using AfterApply.Api.Extensions;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Api.Imports;

/// <summary>
/// Pushes live LinkedIn/CSV import progress to the uploader. Clients join a per-batch group
/// after verifying (server-side, here) that the batch actually belongs to them — the batch id
/// alone isn't a secret worth trusting as an access token.
/// </summary>
[Authorize]
public sealed class ImportProgressHub(AppDbContext dbContext) : Hub
{
    public static string GroupName(Guid batchId) => $"import-batch-{batchId}";

    public async Task JoinBatch(Guid batchId)
    {
        var userId = Context.User!.GetUserId();

        var belongsToCaller = await dbContext.ImportBatches
            .AnyAsync(b => b.Id == batchId && b.UserId == userId);

        if (!belongsToCaller)
        {
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(batchId));
    }
}
