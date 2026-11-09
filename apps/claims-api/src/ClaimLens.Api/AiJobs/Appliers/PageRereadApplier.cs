using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Evidence;
using ClaimLens.Api.Features.Lifecycle;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.AiJobs.Appliers;

/// <summary>
/// PAGE_REREAD (US06): one page read again. The `ai_*` columns always update. A slot a person resolved keeps its value;
/// the others, auto-accepted ones included, take the new reading by the thresholds. A new item on an invoice page adds an
/// expense line; a line the reviewer removed stays removed. A slot read on another page changes only for a more confident
/// reading, because this page not finding it says nothing about that page. A failed run leaves that page to a person,
/// and everything else as it was.
/// </summary>
public sealed class PageRereadApplier(ClaimLensDbContext db, FieldReadings readings, TimeProvider clock)
{
    private sealed record Reading(
        FieldStatus Status,
        string? ValueText,
        long? ValueAmount,
        DateOnly? ValueDate,
        Guid? SourcePageId,
        FieldBox? SourceBbox,
        string? AiValue,
        decimal? AiConfidence,
        ConfidenceLevel? AiLevel,
        Guid? AiPageId,
        FieldBox? AiBbox);

    /// <summary>What to ask, or null when the page is gone. A type with no fields gives an empty catalogue, and no call.</summary>
    public async Task<PageRereadRequest?> RequestAsync(AiRun run, CancellationToken cancel)
    {
        var page = await db.DocumentPages.AsNoTracking().SingleOrDefaultAsync(row => row.Id == run.ScopePageId && row.ClaimId == run.ClaimId, cancel);
        if (page is null)
        {
            return null;
        }

        var catalogue = FieldCatalogue.For(await TypeOfAsync(page, cancel)).All.Select(field => field.Key).Distinct().Order().ToList();
        return new PageRereadRequest(
            run.Id,
            new ModelRef(run.ModelName, run.ModelVersion),
            page.DocumentId,
            new TypedPageRef(page.Id, page.ImageKey, page.PageType == PageType.Unclassified ? null : page.PageType),
            catalogue);
    }

    /// <summary>Applies the answer inside the caller's transaction. Returns what the run's audit event records.</summary>
    public async Task<object> ApplyAsync(AiRun run, PageRereadResponse response, CancellationToken cancel)
    {
        var claim = await LockClaimAsync(run.ClaimId, cancel);
        var page = await db.DocumentPages.SingleAsync(row => row.Id == run.ScopePageId, cancel);
        if (response.DocumentId != page.DocumentId)
        {
            throw new DocumentAiException(DocumentAiException.BadResponse, transient: true, "The answer is about another document.");
        }

        var type = await TypeOfAsync(page, cancel);
        var catalogue = FieldCatalogue.For(type);
        var pageIds = new HashSet<Guid> { page.Id };
        var suggestions = response.Fields
            .Select(FieldReadings.Checked)
            .Where(suggestion => FieldCatalogue.IsItemField(suggestion.FieldKey)
                ? suggestion.ItemNo is >= 1 and <= short.MaxValue
                : suggestion.ItemNo == 0)
            .Where(suggestion => catalogue.All.Any(field => field.Key == suggestion.FieldKey))
            .ToList();

        var slots = await db.ClaimFields.Where(slot => slot.DocumentId == page.DocumentId).ToListAsync(cancel);
        var lines = await db.ExpenseLines.Where(line => line.DocumentId == page.DocumentId).ToDictionaryAsync(line => line.Id, cancel);
        bool Removed(ClaimField slot) => slot.ExpenseLineId is { } id && lines[id].RemovedAt is not null;

        var visited = new HashSet<Guid>();
        var added = new List<ClaimField>();
        var changed = 0;

        void Read(ClaimField slot, FieldSuggestion? suggestion)
        {
            var before = Snapshot(slot);
            readings.Record(slot, run, suggestion, pageIds);
            if (!FieldCatalogue.IsResolvedByPerson(slot.Status))
            {
                FieldReadings.TakeValue(slot);
                if (slot.ExpenseLineId is { } lineId)
                {
                    FieldValues.FollowOnLine(lines[lineId], slot);
                }
            }

            if (Snapshot(slot) != before)
            {
                slot.RowVersion++;
                changed++;
                db.AuditEvents.Add(RereadEvent(run, slot, before));
            }
        }

        // The header, then each item the answer names. A new item brings all its slots, and its line on an invoice.
        var items = suggestions.Where(suggestion => suggestion.ItemNo > 0).Select(suggestion => (short)suggestion.ItemNo).Distinct().Order();
        foreach (var (itemNo, fields) in new[] { ((short)0, catalogue.Header) }.Concat(items.Select(item => (item, catalogue.Item))))
        {
            var ofItem = slots.Where(slot => slot.ItemNo == itemNo && fields.Any(field => field.Key == slot.FieldKey)).ToList();
            var lineId = ofItem.Select(slot => slot.ExpenseLineId).FirstOrDefault(id => id is not null);
            if (itemNo > 0 && ofItem.Count == 0 && FieldCatalogue.GivesExpenseLines(type))
            {
                var line = new ExpenseLine
                {
                    Id = Guid.CreateVersion7(),
                    ClaimId = run.ClaimId,
                    DocumentId = page.DocumentId,
                    LineNo = itemNo,
                    Origin = LineOrigin.AiExtracted,
                    MappingStatus = MappingStatus.Pending,
                };
                db.ExpenseLines.Add(line);
                lines[line.Id] = line;
                lineId = line.Id;
            }

            foreach (var field in fields)
            {
                var suggestion = FieldReadings.Best(suggestions, field.Key, itemNo);
                var slot = ofItem.SingleOrDefault(row => row.FieldKey == field.Key);
                if (slot is null)
                {
                    // An item's slots come as a set; a header slot only for what the AI actually gave.
                    if (itemNo == 0 && suggestion is null)
                    {
                        continue;
                    }

                    if (lineId is { } id && lines[id].RemovedAt is not null)
                    {
                        continue;
                    }

                    slot = FieldSlots.Empty(run.ClaimId, page.DocumentId, field.Key, itemNo, lineId);
                    readings.Record(slot, run, suggestion, pageIds);
                    FieldReadings.TakeValue(slot);
                    if (lineId is { } newLine)
                    {
                        FieldValues.FollowOnLine(lines[newLine], slot);
                    }

                    added.Add(slot);
                    continue;
                }

                if (suggestion is null)
                {
                    continue;
                }

                visited.Add(slot.Id);
                if (!Removed(slot) && (!ReadOnAnotherPage(slot, page.Id) || MoreConfident(suggestion, slot)))
                {
                    Read(slot, suggestion);
                }
            }
        }

        // What this page gave before and no longer does: the AI does not read it there any more.
        foreach (var slot in slots.Where(slot => slot.AiPageId == page.Id && !visited.Contains(slot.Id) && !Removed(slot)))
        {
            Read(slot, null);
        }

        db.ClaimFields.AddRange(added);
        foreach (var accepted in added.Where(slot => slot.Status == FieldStatus.AutoAccepted))
        {
            db.AuditEvents.Add(readings.AutoAccepted(run, accepted));
        }

        page.AiStatus = PageAiStatus.Done;
        await ClaimProgression.AfterChangeAsync(db, claim, clock.GetUtcNow(), (before, after) => StatusEvent(run, claim.Id, before, after), cancel);

        return new { pageId = page.Id, fields = new { changed, added = added.Count }, claimStatus = claim.Status };
    }

    /// <summary>
    /// The manual fallback for this page only (US06): it shows FAILED, the empty slots of its type are there to enter, and
    /// everything already on the claim stays as it was.
    /// </summary>
    public async Task<object> FailAsync(AiRun run, CancellationToken cancel)
    {
        await LockClaimAsync(run.ClaimId, cancel);
        var page = await db.DocumentPages.SingleOrDefaultAsync(row => row.Id == run.ScopePageId, cancel);
        if (page is null)
        {
            return new { fallback = true, emptyFields = 0 };
        }

        page.AiStatus = PageAiStatus.Failed;
        var created = await FieldSlots.AddMissingHeaderAsync(db, run.ClaimId, page.DocumentId, [await TypeOfAsync(page, cancel)], cancel);
        return new { fallback = true, pageId = page.Id, emptyFields = created.Count };
    }

    /// <summary>The run is RUNNING: its page shows as being read.</summary>
    public Task StartAsync(AiRun run, CancellationToken cancel) => SetPageStatusAsync(run, PageAiStatus.Reading, cancel);

    /// <summary>Back to QUEUED for another attempt.</summary>
    public Task RequeueAsync(AiRun run, CancellationToken cancel) => SetPageStatusAsync(run, PageAiStatus.Queued, cancel);

    private Task SetPageStatusAsync(AiRun run, PageAiStatus status, CancellationToken cancel) =>
        db.DocumentPages
            .Where(page => page.Id == run.ScopePageId)
            .ExecuteUpdateAsync(set => set.SetProperty(page => page.AiStatus, status), cancel);

    /// <summary>The type this page is read as: its own, or an Unclassified page's document's.</summary>
    private async Task<PageType> TypeOfAsync(DocumentPage page, CancellationToken cancel)
    {
        if (page.PageType != PageType.Unclassified)
        {
            return page.PageType;
        }

        var pages = await db.DocumentPages.AsNoTracking().Where(row => row.DocumentId == page.DocumentId).ToListAsync(cancel);
        return FieldCatalogue.DocumentType(pages) ?? PageType.Unclassified;
    }

    private static bool ReadOnAnotherPage(ClaimField slot, Guid pageId) => slot.AiPageId is { } other && other != pageId;

    private static bool MoreConfident(FieldSuggestion suggestion, ClaimField slot) =>
        !string.IsNullOrWhiteSpace(suggestion.Value) && Thresholds.Stored(suggestion.Confidence) > (slot.AiConfidence ?? 0);

    private static Reading Snapshot(ClaimField slot) =>
        new(
            slot.Status,
            slot.ValueText,
            slot.ValueAmount,
            slot.ValueDate,
            slot.SourcePageId,
            FieldBox.Parse(slot.SourceBbox),
            slot.AiValue,
            slot.AiConfidence,
            slot.AiLevel,
            slot.AiPageId,
            FieldBox.Parse(slot.AiBbox));

    /// <summary>The audit trail keeps the reading the row overwrote (docs/06-data-model.md, D11), with a national ID masked.</summary>
    private AuditEvent RereadEvent(AiRun run, ClaimField slot, Reading before)
    {
        var audit = AuditLog.SystemEvent(
            clock,
            slot.ClaimId,
            "FIELD_AI_REREAD",
            "claim_field",
            slot.Id.ToString(),
            before: new
            {
                before.Status,
                value = FieldResponses.Shown(slot.FieldKey, before.ValueText),
                aiValue = FieldResponses.Shown(slot.FieldKey, before.AiValue),
                confidence = before.AiConfidence,
                level = before.AiLevel,
            },
            after: new
            {
                slot.FieldKey,
                slot.ItemNo,
                slot.Status,
                value = FieldResponses.Shown(slot.FieldKey, slot.ValueText),
                aiValue = FieldResponses.Shown(slot.FieldKey, slot.AiValue),
                confidence = slot.AiConfidence,
                level = slot.AiLevel,
                acceptedBy = slot.Status == FieldStatus.AutoAccepted ? "threshold" : null,
            });
        audit.AiRunId = run.Id;
        return audit;
    }

    private AuditEvent StatusEvent(AiRun run, Guid claimId, ClaimStatus before, ClaimStatus after)
    {
        var audit = AuditLog.SystemEvent(
            clock,
            claimId,
            "CLAIM_STATUS_CHANGED",
            "claim",
            claimId.ToString(),
            before: new { status = before },
            after: new { status = after });
        audit.AiRunId = run.Id;
        return audit;
    }

    private async Task<Claim> LockClaimAsync(Guid claimId, CancellationToken cancel) =>
        (await db.Claims.FromSql($"select * from claim where id = {claimId} for update").ToListAsync(cancel)).Single();
}
