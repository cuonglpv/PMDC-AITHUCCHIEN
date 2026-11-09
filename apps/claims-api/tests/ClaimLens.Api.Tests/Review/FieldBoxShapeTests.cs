using System.Net;
using System.Text.Json;
using static ClaimLens.Api.Tests.Features.IntakeData;
using static ClaimLens.Api.Tests.Review.ReviewData;

namespace ClaimLens.Api.Tests.Review;

/// <summary>
/// A region is the contract's Bbox: x, y, w and h, wherever it goes. The contract's schema allows extra keys, so its
/// check would not notice one; these tests name the keys.
/// </summary>
public sealed class FieldBoxShapeTests(ApiFactory api)
{
    private static readonly string[] BoxKeys = ["h", "w", "x", "y"];

    private static IEnumerable<string> Keys(JsonElement box) =>
        box.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);

    private Task<List<string>> StoredKeysAsync(string sql) =>
        api.Column<string>($"select key from jsonb_object_keys(({sql})) as key order by key");

    [Fact]
    public async Task A_drawn_region_has_only_its_four_numbers_in_the_row_the_answers_and_the_audit_trail()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer);
        var fields = await FieldsAsync(reviewer, claim);
        var provider = FieldOf(fields, "provider_name");
        var invoiceNumber = FieldOf(fields, "invoice_number");

        var edit = await PatchAsync(reviewer, claim, provider, new
        {
            rowVersion = Version(provider),
            action = "SET_VALUE",
            valueText = "Hồng Ngọc",
            sourcePageId = file.Page,
            sourceBbox = Region,
        });
        var accept = await AcceptAsync(reviewer, claim, invoiceNumber, new { rowVersion = Version(invoiceNumber), sourcePageId = file.Page, sourceBbox = Region });
        var stale = await PatchAsync(reviewer, claim, provider, new { rowVersion = Version(provider), action = "NOT_PRESENT" });

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var edited = (await JsonOf(edit)).GetProperty("field");
        Assert.Equal(BoxKeys, Keys(edited.GetProperty("sourceBbox")));
        Assert.Equal(BoxKeys, Keys(edited.GetProperty("ai").GetProperty("bbox")));
        Assert.Equal(BoxKeys, Keys((await JsonOf(accept)).GetProperty("field").GetProperty("sourceBbox")));
        Assert.Equal(BoxKeys, Keys((await JsonOf(stale)).GetProperty("details").GetProperty("current").GetProperty("sourceBbox")));

        var listed = await FieldOf(reviewer, claim, "provider_name");
        Assert.Equal(BoxKeys, Keys(listed.GetProperty("sourceBbox")));
        Assert.Equal(BoxKeys, Keys(listed.GetProperty("ai").GetProperty("bbox")));

        Assert.Equal(BoxKeys, await StoredKeysAsync($"select source_bbox from claim_field where id = '{Id(provider)}'"));
        Assert.Equal(BoxKeys, await StoredKeysAsync($"select ai_bbox from claim_field where id = '{Id(provider)}'"));
        Assert.Equal(BoxKeys, await StoredKeysAsync(
            $"select after_value->'sourceBbox' from audit_event where event_type = 'FIELD_CORRECTED' and entity_id = '{Id(provider)}'"));
        Assert.Equal(BoxKeys, await StoredKeysAsync(
            $"select after_value->'sourceBbox' from audit_event where event_type = 'FIELD_CONFIRMED' and entity_id = '{Id(invoiceNumber)}'"));
    }

    [Fact]
    public async Task A_box_stored_with_the_key_that_used_to_leak_still_reads_and_is_answered_without_it()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer);
        var provider = await FieldOf(reviewer, claim, "provider_name");
        await PatchAsync(reviewer, claim, provider, new { rowVersion = Version(provider), action = "SET_VALUE", valueText = "Hồng Ngọc" });
        await api.Sql(
            $$"""
            update claim_field
            set source_bbox = '{"h":0.1,"w":0.4,"x":0.2,"y":0.2,"liesOnThePage":true}'::jsonb
            where id = '{{Id(provider)}}'
            """);

        var box = (await FieldOf(reviewer, claim, "provider_name")).GetProperty("sourceBbox");

        Assert.Equal(BoxKeys, Keys(box));
        Assert.Equal(0.4m, box.GetProperty("w").GetDecimal());
    }
}
