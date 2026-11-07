namespace ClaimLens.Api.Http;

public sealed record MappedConstraint(int Status, string Code, string Title);

/// <summary>Database constraint names to error codes, from the table in docs/07-api-contract.md.</summary>
public static class ConstraintErrorMap
{
    private const int Forbidden = StatusCodes.Status403Forbidden;
    private const int Conflict = StatusCodes.Status409Conflict;
    private const int Unprocessable = StatusCodes.Status422UnprocessableEntity;

    public static readonly IReadOnlyDictionary<string, MappedConstraint> All = new Dictionary<string, MappedConstraint>(StringComparer.Ordinal)
    {
        ["ck_claim_lifecycle"] = new(Conflict, "CLAIM_STATE_CONFLICT", "The claim's status does not allow this"),
        ["ck_claim_final_needs_decision"] = new(Conflict, "CLAIM_STATE_CONFLICT", "The claim's status does not allow this"),
        ["ux_rule_set_one_draft"] = new(Conflict, "DRAFT_ALREADY_EXISTS", "This plan already has a draft"),
        // Two administrators creating a draft at once can meet here first, before ux_rule_set_one_draft.
        ["ux_rule_set_version_no"] = new(Conflict, "DRAFT_ALREADY_EXISTS", "This plan already has a draft"),
        ["ck_rule_set_frozen"] = new(Conflict, "RULE_SET_NOT_DRAFT", "Only a draft can change"),
        // A discard and a publish of the same draft can race; the second meets this.
        ["ck_rule_set_status_transition"] = new(Conflict, "RULE_SET_NOT_DRAFT", "Only a draft can change"),
        ["ck_rule_set_effective_later"] = new(Unprocessable, "EFFECTIVE_DATE_NOT_LATER", "The effective date must be later than the current version's"),
        ["ux_rule_set_published_effective"] = new(Unprocessable, "EFFECTIVE_DATE_NOT_LATER", "The effective date must be later than the current version's"),
        ["ck_rule_set_categories"] = new(Unprocessable, "CATEGORY_NOT_CONFIGURED", "Every active category must be covered or excluded"),
        ["ck_rule_category"] = new(Unprocessable, "RULE_INVALID", "This rule is missing a value of its type"),
        ["ck_rule_rate"] = new(Unprocessable, "RULE_INVALID", "This rule is missing a value of its type"),
        ["ck_rule_limit"] = new(Unprocessable, "RULE_INVALID", "This rule is missing a value of its type"),
        ["ck_rule_waiting"] = new(Unprocessable, "RULE_INVALID", "This rule is missing a value of its type"),
        ["claim_policy_member"] = new(Unprocessable, "POLICY_MEMBER_MISMATCH", "The policy belongs to another member"),
        ["ck_claim_field_source_required"] = new(Unprocessable, "SOURCE_REQUIRED", "A value needs a source region"),
        ["ck_alert_total_not_dismissible"] = new(Unprocessable, "ALERT_NOT_DISMISSIBLE", "A claimed-total mismatch must be fixed"),
        ["ck_override_cap"] = new(Unprocessable, "OVERRIDE_EXCEEDS_LINE", "The amount is above the line amount"),
        ["ck_override_result"] = new(Unprocessable, "OVERRIDE_AMOUNT_MISMATCH", "The amount does not fit the result"),
        ["ck_recommendation_mismatch_reason"] = new(Unprocessable, "OUTCOME_REASON_REQUIRED", "A different outcome needs a reason and a note"),
        ["ck_decision_maker_checker"] = new(Forbidden, "MAKER_CHECKER", "You submitted this recommendation, so another approver must decide it"),
        ["ck_decision_br13_approved"] = new(Unprocessable, "DECISION_NOT_ALLOWED", "The action does not match the payable"),
        ["ck_decision_br13_partial"] = new(Unprocessable, "DECISION_NOT_ALLOWED", "The action does not match the payable"),
        ["ck_decision_br13_rejected"] = new(Unprocessable, "DECISION_REASON_REQUIRED", "A rejection needs a reason code"),
    };

    public static MappedConstraint? Find(string constraint) => All.GetValueOrDefault(constraint);
}
