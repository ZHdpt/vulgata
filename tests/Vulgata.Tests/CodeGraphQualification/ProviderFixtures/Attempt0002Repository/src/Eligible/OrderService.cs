using QualificationRepository.Filtered;

namespace QualificationRepository.Eligible;

public sealed class OrderService : BaseProcessor
{
    public override string Process(string value) => GeneratedClient.Normalize(value);
}
