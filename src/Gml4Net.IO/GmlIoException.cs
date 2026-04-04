namespace Gml4Net.IO;

/// <summary>
/// Exception for transport-level errors in the I/O layer.
/// Distinct from <see cref="Gml4Net.Model.GmlParseIssue"/> which handles parse-level diagnostics.
/// </summary>
public sealed class GmlIoException : Exception
{
    /// <summary>
    /// Machine-readable error code ("file_not_found", "file_read_error", "http_error", "network_error", "ows_exception").
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// HTTP status code, only set for "http_error".
    /// </summary>
    public int? HttpStatusCode { get; }

    /// <summary>
    /// Creates a new I/O exception.
    /// </summary>
    /// <param name="errorCode">Machine-readable error code.</param>
    /// <param name="message">Human-readable message.</param>
    /// <param name="innerException">Optional inner exception.</param>
    /// <param name="httpStatusCode">Optional HTTP status code.</param>
    public GmlIoException(string errorCode, string message, Exception? innerException = null, int? httpStatusCode = null)
        : base(message, innerException)
    {
        ArgumentNullException.ThrowIfNull(errorCode);
        ErrorCode = errorCode;
        HttpStatusCode = httpStatusCode;
    }
}
