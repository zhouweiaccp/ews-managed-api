using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.Exchange.WebServices.Data
{
    public class HttpWebException : InvalidOperationException
    {
        public HttpWebException(HttpResponseMessage response, WebExceptionStatus status)
        {
            Response = response;
            Status = status;
        }

        public HttpWebException(string message, WebExceptionStatus status)
            : base(message)
        {
            Status = status;
        }

        public HttpWebException(string message, WebExceptionStatus status, Exception innerException)
            : base(message, innerException)
        {
            Status = status;
        }

        public HttpResponseMessage Response { get; }
        public WebExceptionStatus Status { get; }
    }
}
