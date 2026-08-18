using PetSalon.Core.Entities;

namespace PetSalon.Core.Abstractions;

public sealed record ContractRenderData(
    Owner Owner,
    Pet Pet,
    Appointment Appointment,
    GroomingRecord GroomingRecord,
    byte[]? OwnerSignaturePng,
    string HospitalName,
    string HospitalPhone,
    string HospitalAddress,
    byte[]? ShopSignaturePng = null);

public sealed record ContractGenerateOutput(string AbsolutePath, int Version);

public interface IPdfGenerator
{
    Task<ContractGenerateOutput> GenerateContractAsync(
        ContractRenderData data,
        string outputDir,
        int nextVersion,
        CancellationToken ct = default);
}
