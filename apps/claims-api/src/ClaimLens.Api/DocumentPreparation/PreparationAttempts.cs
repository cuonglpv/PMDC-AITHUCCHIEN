using Microsoft.Extensions.Options;

namespace ClaimLens.Api.DocumentPreparation;

/// <summary>
/// Failed preparation attempts that were not the file's fault, such as the converter or storage being down. A file that
/// failed waits while the worker goes on with the files behind it; after Workers:DocumentPreparation:MaxAttempts it is
/// rejected so the officer can upload it again.
/// </summary>
public sealed class PreparationAttempts(IOptions<WorkersOptions> options, TimeProvider clock)
    : FailedAttempts(() => options.Value.DocumentPreparation, clock);
