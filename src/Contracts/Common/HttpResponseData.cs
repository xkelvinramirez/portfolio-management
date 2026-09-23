
using System.Net;

namespace Contracts.Common;

public record HttpResponseData<T>(HttpStatusCode StatusCode, T? Content);
