namespace bams.desktop.Utils;

/// <summary>
/// Stable application message identifiers used by the WPF client.
/// </summary>
public enum MessageCode
{
    Success = 1000,
    ClientError = 6000,
    NetworkUnavailable = 6001,
    InvalidServerResponse = 6003
}
