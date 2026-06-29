namespace Vulgata.Core.Entities;

[Flags]
public enum ApiTypeFlags
{
    None = 0,
    ChatCompletions = 1 << 0,
    Responses = 1 << 1,
    Messages = 1 << 2,
}
