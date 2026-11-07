import type { components } from "./schema";

type Schemas = components["schemas"];

export type StaffRole = Schemas["StaffRole"];
export type StaffUser = Schemas["StaffUser"];
export type AuthSession = Schemas["AuthSession"];
export type ErrorCode = Schemas["ErrorCode"];
export type Problem = Schemas["Problem"];

export type ClaimStatus = Schemas["ClaimStatus"];
export type ClaimDetail = Schemas["ClaimDetail"];
export type ClaimSaved = Schemas["ClaimSaved"];
export type ClaimQueueItem = Schemas["ClaimQueueItem"];
export type ClaimQueuePage = Schemas["ClaimQueuePage"];
export type ClaimStatusSnapshot = Schemas["ClaimStatusSnapshot"];
export type QueueView = Schemas["QueueView"];
export type IntakeChannel = Schemas["IntakeChannel"];
export type PolicyStatus = Schemas["PolicyStatus"];
export type PolicySummary = Schemas["PolicySummary"];
export type MemberSearchResult = Schemas["MemberSearchResult"];
export type Warning = Schemas["Warning"];
export type WarningCode = Schemas["WarningCode"];
export type VersionConflictDetails = Schemas["VersionConflictDetails"];

export type Document = Schemas["Document"];
export type DocumentPage = Schemas["DocumentPage"];
export type DocumentStatus = Schemas["DocumentStatus"];
export type PageType = Schemas["PageType"];
export type PageQuality = Schemas["PageQuality"];
export type PageAiStatus = Schemas["PageAiStatus"];
export type ValueSource = Schemas["ValueSource"];
export type StaffRef = Schemas["StaffRef"];

export type AiTask = Schemas["AiTask"];
export type AiRunStatus = Schemas["AiRunStatus"];
export type AiRunState = Schemas["AiRunState"];
export type ConfidenceLevel = Schemas["ConfidenceLevel"];
export type PageAiSuggestion = Schemas["PageAiSuggestion"];
export type ChosenPageType = Schemas["ReclassifyPageRequest"]["pageType"];
