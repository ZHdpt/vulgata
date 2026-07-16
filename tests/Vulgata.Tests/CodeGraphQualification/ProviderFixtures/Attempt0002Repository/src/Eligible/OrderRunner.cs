namespace QualificationRepository.Eligible;

public static class OrderRunner
{
    public static string Run(string value) => new OrderService().Process(value);
}
