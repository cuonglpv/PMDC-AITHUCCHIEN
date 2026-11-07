using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;

namespace ClaimLens.Api.Features.Evidence;

/// <summary>Which BR-03 threshold a field's confidence is held to. The numbers live in AiJobs/Thresholds (C3 rule 4).</summary>
public enum ThresholdGroup
{
    General,

    /// <summary>Insured name, ID number, policy number, invoice total and line amount.</summary>
    MoneyAndIdentity,
}

/// <summary>How a value is read into the claim_field columns: text only, or also value_amount, value_date or a quantity.</summary>
public enum FieldValueKind
{
    Text,
    Money,
    Date,
    Quantity,
}

/// <summary>A field of a page type. Required fields must be resolved before a claim is READY_FOR_REVIEW.</summary>
public sealed record CatalogueField(FieldKey Key, bool Required);

/// <summary>The fields of one page type: header fields (item 0) and the fields repeated per line or prescription item.</summary>
public sealed record PageTypeFields(IReadOnlyList<CatalogueField> Header, IReadOnlyList<CatalogueField> Item)
{
    public IEnumerable<CatalogueField> All => Header.Concat(Item);
}

/// <summary>
/// The field catalogue (US05, BR-03). Fields are read by the type of the document they are on. Only the fields that
/// validation and payment need are required. Expense lines come only from INVOICE items, so an itemised bill or a
/// receipt is never counted twice.
/// </summary>
public static class FieldCatalogue
{
    private static readonly PageTypeFields None = new([], []);

    private static readonly IReadOnlyDictionary<PageType, PageTypeFields> ByType = new Dictionary<PageType, PageTypeFields>
    {
        [PageType.Invoice] = new(
            [
                new(FieldKey.InsuredName, Required: true),
                new(FieldKey.ProviderName, Required: true),
                new(FieldKey.InvoiceNumber, Required: true),
                new(FieldKey.ServiceDate, Required: true),
                new(FieldKey.InvoiceTotal, Required: true),
                new(FieldKey.ProviderTaxCode, Required: false),
            ],
            [
                new(FieldKey.LineDescription, Required: true),
                new(FieldKey.LineAmount, Required: true),
                new(FieldKey.LineQuantity, Required: false),
                new(FieldKey.LineUnitPrice, Required: false),
            ]),
        [PageType.Receipt] = new(
            [
                new(FieldKey.InvoiceTotal, Required: true),
                new(FieldKey.ProviderName, Required: false),
                new(FieldKey.InvoiceNumber, Required: false),
                new(FieldKey.ServiceDate, Required: false),
            ],
            []),
        [PageType.VisitSummary] = new(
            [
                new(FieldKey.InsuredName, Required: true),
                new(FieldKey.ServiceDate, Required: true),
                new(FieldKey.Diagnosis, Required: true),
                new(FieldKey.NationalId, Required: false),
                new(FieldKey.PolicyNumber, Required: false),
            ],
            []),
        [PageType.Prescription] = new(
            [
                new(FieldKey.InsuredName, Required: false),
                new(FieldKey.ServiceDate, Required: false),
                new(FieldKey.Diagnosis, Required: false),
            ],
            [
                new(FieldKey.Medication, Required: true),
            ]),
        [PageType.LabResult] = new(
            [
                new(FieldKey.InsuredName, Required: false),
                new(FieldKey.ServiceDate, Required: false),
            ],
            []),
    };

    private static readonly HashSet<FieldKey> MoneyAndIdentity =
    [
        FieldKey.InsuredName,
        FieldKey.NationalId,
        FieldKey.PolicyNumber,
        FieldKey.InvoiceTotal,
        FieldKey.LineAmount,
    ];

    /// <summary>The fields read from a document of this type. OTHER and UNCLASSIFIED have none.</summary>
    public static PageTypeFields For(PageType type) => ByType.TryGetValue(type, out var fields) ? fields : None;

    public static ThresholdGroup GroupOf(FieldKey key) =>
        MoneyAndIdentity.Contains(key) ? ThresholdGroup.MoneyAndIdentity : ThresholdGroup.General;

    public static FieldValueKind KindOf(FieldKey key) =>
        key switch
        {
            FieldKey.InvoiceTotal or FieldKey.LineAmount or FieldKey.LineUnitPrice => FieldValueKind.Money,
            FieldKey.ServiceDate => FieldValueKind.Date,
            FieldKey.LineQuantity => FieldValueKind.Quantity,
            _ => FieldValueKind.Text,
        };

    /// <summary>A full national ID never leaves the API (BR-11); responses carry a masked form.</summary>
    public static bool IsSensitive(FieldKey key) => key == FieldKey.NationalId;

    /// <summary>Line and prescription-item fields, which repeat per item (item_no 1..n).</summary>
    public static bool IsItemField(FieldKey key) =>
        key is FieldKey.LineDescription or FieldKey.LineQuantity or FieldKey.LineUnitPrice or FieldKey.LineAmount or FieldKey.Medication;

    /// <summary>Only invoices give expense lines (US05), so a receipt or an itemised bill does not count them twice.</summary>
    public static bool GivesExpenseLines(PageType type) => type == PageType.Invoice;

    /// <summary>
    /// A document's type is the type of its first classified page, in page order; null while every page is UNCLASSIFIED.
    /// </summary>
    public static PageType? DocumentType(IEnumerable<DocumentPage> pages) =>
        pages
            .OrderBy(page => page.PageNo)
            .Select(page => (PageType?)page.PageType)
            .FirstOrDefault(type => type != PageType.Unclassified);

    /// <summary>The status of a field that counts as handled for READY_FOR_REVIEW (docs/06-data-model.md, section 7).</summary>
    public static bool IsResolved(FieldStatus status) =>
        status is FieldStatus.AutoAccepted or FieldStatus.Confirmed or FieldStatus.Corrected or FieldStatus.Entered or FieldStatus.NotPresent;
}
