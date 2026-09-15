using bams.server.Messages;

namespace bams.server.DTO.Common;

public sealed record ApiMessageResponse<T>(
    int Code,
    string Name,
    string Message,
    T Data)
{
    // Creates a typed success response while keeping the message resolved centrally.
    public static ApiMessageResponse<T> FromCode(
        MessageCode code,
        T data)
    {
        return new ApiMessageResponse<T>(
            (int)code,
            code.ToString(),
            MessageCatalog.GetMessage(code),
            data);
    }
}
