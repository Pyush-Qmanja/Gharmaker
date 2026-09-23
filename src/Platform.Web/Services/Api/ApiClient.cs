using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Common;

namespace Platform.Web.Services.Api;

/// <summary>
/// Thin, generic JSON client for Platform.Api. Every API call in the UI goes
/// through these four methods, so error handling lives in one place.
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Sends a GET request.
    /// </summary>
    /// <typeparam name="T">Response body type.</typeparam>
    /// <param name="path">Relative path including any query string.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The result.</returns>
    Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a POST request with a JSON body.
    /// </summary>
    /// <typeparam name="TRequest">Request body type.</typeparam>
    /// <typeparam name="TResponse">Response body type.</typeparam>
    /// <param name="path">Relative path.</param>
    /// <param name="body">Request body.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The result.</returns>
    Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a PUT request with a JSON body.
    /// </summary>
    /// <typeparam name="TRequest">Request body type.</typeparam>
    /// <typeparam name="TResponse">Response body type.</typeparam>
    /// <param name="path">Relative path.</param>
    /// <param name="body">Request body.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The result.</returns>
    Task<ApiResult<TResponse>> PutAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a DELETE request.
    /// </summary>
    /// <param name="path">Relative path.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The result.</returns>
    Task<ApiResult> DeleteAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads one file as <c>multipart/form-data</c> in a field named <c>file</c>.
    /// </summary>
    /// <typeparam name="TResponse">Response body type.</typeparam>
    /// <param name="path">Relative path.</param>
    /// <param name="content">File content.</param>
    /// <param name="fileName">File name sent to the API.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The result.</returns>
    Task<ApiResult<TResponse>> PostFileAsync<TResponse>(string path, Stream content, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file (e.g. an Excel template).
    /// </summary>
    /// <param name="path">Relative path.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The file on success.</returns>
    Task<ApiResult<ApiFile>> GetFileAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// A file downloaded from the API.
/// </summary>
/// <param name="Content">File bytes.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="FileName">Suggested file name.</param>
public sealed record ApiFile(byte[] Content, string ContentType, string FileName);

/// <summary>
/// <see cref="HttpClient"/> implementation of <see cref="IApiClient"/>. The
/// bearer token is attached by <see cref="BearerTokenHandler"/>.
/// </summary>
public sealed class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="httpClient">Configured with the API base address.</param>
    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(path, body, JsonDefaults.Options, cancellationToken);
        return await ReadAsync<TResponse>(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiResult<TResponse>> PutAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(path, body, JsonDefaults.Options, cancellationToken);
        return await ReadAsync<TResponse>(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiResult> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync(path, cancellationToken);
        return response.IsSuccessStatusCode
            ? new ApiResult { StatusCode = response.StatusCode }
            : await ReadErrorAsync<object>(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiResult<TResponse>> PostFileAsync<TResponse>(string path, Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(content);
        form.Add(file, "file", fileName);
        using var response = await _httpClient.PostAsync(path, form, cancellationToken);
        return await ReadAsync<TResponse>(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiResult<ApiFile>> GetFileAsync(string path, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return await ReadErrorAsync<ApiFile>(response, cancellationToken);
        }

        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        string fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? "download";
        return new ApiResult<ApiFile> { StatusCode = response.StatusCode, Value = new ApiFile(bytes, contentType, fileName) };
    }

    /// <summary>
    /// Converts a response into a typed result, reading the body on success
    /// and the ProblemDetails on failure.
    /// </summary>
    /// <typeparam name="T">Expected body type.</typeparam>
    /// <param name="response">HTTP response.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>The result.</returns>
    private static async Task<ApiResult<T>> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return await ReadErrorAsync<T>(response, cancellationToken);
        }

        T? value = await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Options, cancellationToken);
        return new ApiResult<T> { StatusCode = response.StatusCode, Value = value };
    }

    /// <summary>
    /// Reads a ProblemDetails / ValidationProblemDetails body into an error
    /// result. Tolerates an empty or non-JSON body.
    /// </summary>
    /// <typeparam name="T">Result body type.</typeparam>
    /// <param name="response">Failed HTTP response.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>An error result.</returns>
    private static async Task<ApiResult<T>> ReadErrorAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ValidationProblemDetails? problem = null;
        if (response.Content.Headers.ContentLength != 0)
        {
            try
            {
                problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonDefaults.Options, cancellationToken);
            }
            catch (System.Text.Json.JsonException)
            {
                // Non-JSON error body (e.g. a proxy page): fall back to the status text.
            }
        }

        return new ApiResult<T>
        {
            StatusCode = response.StatusCode,
            ErrorMessage = problem?.Detail ?? problem?.Title ?? response.ReasonPhrase,
            FieldErrors = problem?.Errors ?? new Dictionary<string, string[]>(),
        };
    }
}
