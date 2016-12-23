/*
 * Exchange Web Services Managed API
 *
 * Copyright (c) Microsoft Corporation
 * All rights reserved.
 *
 * MIT License
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this
 * software and associated documentation files (the "Software"), to deal in the Software
 * without restriction, including without limitation the rights to use, copy, modify, merge,
 * publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
 * to whom the Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or
 * substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
 * INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
 * PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
 * FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
 * OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

namespace Microsoft.Exchange.WebServices.Data
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents an implementation of the IEwsHttpWebRequest interface that uses HttpWebRequest.
    /// </summary>
    internal class EwsHttpWebRequest : IEwsHttpWebRequest
    {
        string _userAgent;
        int _timeout = 100000;
        bool _keepAlive;
        MediaTypeHeaderValue _contentType;
        string _connectionGroupName;
        string _method = "GET";
        string _accept;
        WebHeaderCollection _headers = new WebHeaderCollection();
        X509CertificateCollection _clientCertificates;

        sealed class ManualMemoryStream : MemoryStream
        {
            protected override void Dispose(bool disposing)
            {
                Flush();
                Seek(0, SeekOrigin.Begin);
            }
            public void ManualDispose()
            {
                base.Dispose(true);
            }
        }

        readonly HttpClientHandler _clientHandler = new HttpClientHandler();
        readonly ManualMemoryStream _requestStream = new ManualMemoryStream();
        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        readonly Uri _uri;

        /// <summary>
        /// Initializes a new instance of the <see cref="EwsHttpWebRequest"/> class.
        /// </summary>
        /// <param name="uri">The URI.</param>
        internal EwsHttpWebRequest(Uri uri)
        {
            _uri = uri;
#if NETSTANDARD1_3
            _clientHandler.ServerCertificateCustomValidationCallback = (message, certificate, chain, errors) => true;
#endif
        }

        #region IEwsHttpWebRequest Members

        /// <summary>
        /// Aborts this instance.
        /// </summary>
        void IEwsHttpWebRequest.Abort()
        {
            _cancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Gets a <see cref="T:System.IO.Stream"/> object to use to write request data.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.IO.Stream"/> to use to write request data.
        /// </returns>
        Task<Stream> IEwsHttpWebRequest.GetRequestStream()
        {
            return System.Threading.Tasks.Task.FromResult<Stream>(_requestStream);
        }

        /// <summary>
        /// Returns a response from an Internet resource.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Net.HttpWebResponse"/> that contains the response from the Internet resource.
        /// </returns>
        async Task<IEwsHttpWebResponse> IEwsHttpWebRequest.GetResponse()
        {
            using (var httpClient = new HttpClient(_clientHandler)
                {
                    Timeout = TimeSpan.FromMilliseconds(_timeout),
                })
            {
                if (_headers != null)
                    foreach(var key in _headers.AllKeys)
                        httpClient.DefaultRequestHeaders.Add(key, _headers[key]);
                if (_userAgent != null)
                    httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd(_userAgent);
                if (_accept != null)
                    httpClient.DefaultRequestHeaders.Accept.TryParseAdd(_accept);

                try
                {
                    var content = new StreamContent(_requestStream);
                    content.Headers.ContentType = _contentType;

                    var response = await httpClient.SendAsync(new HttpRequestMessage(new HttpMethod(_method), _uri)
                    {
                        Content = content
                    }, _cancellationTokenSource.Token);
                    if (!response.IsSuccessStatusCode)
                        throw new HttpWebException(response, WebExceptionStatus.ProtocolError);
                    return new EwsHttpWebResponse(response);
                }
                catch(HttpRequestException requestException)
                {
                    throw new HttpWebException(requestException.Message, 
                        WebExceptionStatus.UnknownError, requestException.InnerException);
                }
                finally
                {
                    _requestStream.ManualDispose();
                }
            }
        }

        /// <summary>
        /// Gets or sets the value of the Accept HTTP header.
        /// </summary>
        /// <returns>The value of the Accept HTTP header. The default value is null.</returns>
        string IEwsHttpWebRequest.Accept
        {
            get { return _accept; }
            set { _accept = value; }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether the request should follow redirection responses.
        /// </summary>
        /// <returns>
        /// True if the request should automatically follow redirection responses from the Internet resource; otherwise, false.
        /// The default value is true.
        /// </returns>
        bool IEwsHttpWebRequest.AllowAutoRedirect
        {
            get { return _clientHandler.AllowAutoRedirect; }
            set { _clientHandler.AllowAutoRedirect = value; }
        }

        /// <summary>
        /// Gets or sets the client certificates.
        /// </summary>
        /// <value></value>
        /// <returns>The collection of X509 client certificates.</returns>
        X509CertificateCollection IEwsHttpWebRequest.ClientCertificates
        {
            get { return _clientCertificates; }
            set { _clientCertificates = value; }
        }

        /// <summary>
        /// Gets or sets the value of the Content-type HTTP header.
        /// </summary>
        /// <returns>The value of the Content-type HTTP header. The default value is null.</returns>
        MediaTypeHeaderValue IEwsHttpWebRequest.ContentType
        {
            get { return _contentType; }
            set { _contentType = value; }
        }

        /// <summary>
        /// Gets or sets the cookie container.
        /// </summary>
        /// <value>The cookie container.</value>
        CookieContainer IEwsHttpWebRequest.CookieContainer
        {
            get { return _clientHandler.CookieContainer; }
            set { _clientHandler.CookieContainer = value; }
        }

        /// <summary>
        /// Gets or sets authentication information for the request.
        /// </summary>
        /// <returns>An <see cref="T:System.Net.ICredentials"/> that contains the authentication credentials associated with the request. The default is null.</returns>
        ICredentials IEwsHttpWebRequest.Credentials
        {
            get { return _clientHandler.Credentials; }
            set { _clientHandler.Credentials = value; }
        }

        /// <summary>
        /// Specifies a collection of the name/value pairs that make up the HTTP headers.
        /// </summary>
        /// <returns>A <see cref="T:System.Net.WebHeaderCollection"/> that contains the name/value pairs that make up the headers for the HTTP request.</returns>
        WebHeaderCollection IEwsHttpWebRequest.Headers
        {
            get { return  _headers; }
            set { _headers = value; }
        }

        /// <summary>
        /// Gets or sets the method for the request.
        /// </summary>
        /// <returns>The request method to use to contact the Internet resource. The default value is GET.</returns>
        /// <exception cref="T:System.ArgumentException">No method is supplied.-or- The method string contains invalid characters. </exception>
        string IEwsHttpWebRequest.Method
        {
            get { return _method; }
            set { _method = value; }
        }

        /// <summary>
        /// Gets or sets proxy information for the request.
        /// </summary>
        IWebProxy IEwsHttpWebRequest.Proxy
        {
            get { return _clientHandler.Proxy; }
            set { _clientHandler.Proxy = value; }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether to send an authenticate header with the request.
        /// </summary>
        /// <returns>true to send a WWW-authenticate HTTP header with requests after authentication has taken place; otherwise, false. The default is false.</returns>
        bool IEwsHttpWebRequest.PreAuthenticate
        {
            get { return _clientHandler.PreAuthenticate; }
            set { _clientHandler.PreAuthenticate = value; }
        }

        /// <summary>
        /// Gets the original Uniform Resource Identifier (URI) of the request.
        /// </summary>
        /// <returns>A <see cref="T:System.Uri"/> that contains the URI of the Internet resource passed to the <see cref="M:System.Net.WebRequest.Create(System.String)"/> method.</returns>
        Uri IEwsHttpWebRequest.RequestUri
        {
            get { return _uri; }
        }

        /// <summary>
        /// Gets or sets the time-out value in milliseconds for the <see cref="M:System.Net.HttpWebRequest.GetResponse"/> and <see cref="M:System.Net.HttpWebRequest.GetRequestStream"/> methods.
        /// </summary>
        /// <returns>The number of milliseconds to wait before the request times out. The default is 100,000 milliseconds (100 seconds).</returns>
        int IEwsHttpWebRequest.Timeout
        {
            get { return _timeout; }
            set { _timeout = value; }
        }

        /// <summary>
        /// Gets or sets a <see cref="T:System.Boolean"/> value that controls whether default credentials are sent with requests.
        /// </summary>
        /// <returns>true if the default credentials are used; otherwise false. The default value is false.</returns>
        bool IEwsHttpWebRequest.UseDefaultCredentials
        {
            get { return _clientHandler.UseDefaultCredentials; }
            set { _clientHandler.UseDefaultCredentials = value; }
        }

        /// <summary>
        /// Gets or sets the value of the User-agent HTTP header.
        /// </summary>
        /// <returns>The value of the User-agent HTTP header. The default value is null.The value for this property is stored in <see cref="T:System.Net.WebHeaderCollection"/>. If WebHeaderCollection is set, the property value is lost.</returns>
        string IEwsHttpWebRequest.UserAgent
        {
            get { return  _userAgent; }
            set { _userAgent = value; }
        }

        /// <summary>
        /// Gets or sets if the request to the internet resource should contain a Connection HTTP header with the value Keep-alive
        /// </summary>
        public bool KeepAlive
        {
            get { return _keepAlive; }
            set { _keepAlive = value; }
        }

        /// <summary>
        /// Gets or sets the name of the connection group for the request. 
        /// </summary>
        public string ConnectionGroupName
        {
            get { return _connectionGroupName; }
            set { _connectionGroupName = value; }
        }

#endregion
    }
}