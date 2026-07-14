namespace Vulgata.Core.Entities;

[Flags]
public enum ApiType
{
    None = 0,
    ChatCompletions = 1 << 0,
    Responses = 1 << 1,
    Messages = 1 << 2,
}
