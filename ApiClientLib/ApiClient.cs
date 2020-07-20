using Microsoft.Owin.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;


namespace ApiClientLib
{
    public class ApiClient<TModel, TCollection, TResult> 
        where TModel: class
        where TCollection : class
        where TResult : class
    {
        protected JsonSerializerSettings jsonSerializerSettings;

        public virtual string BaseUrl { get; set; }
        public virtual string ApiEndPoint { get; set; }

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

        protected virtual string EndPoint
        {
            get
            {
                if (string.IsNullOrEmpty(ApiEndPoint))
                {
                    return BaseUrl;
                }
                return BaseUrl + ApiEndPoint;
            }
        }

        public ApiClient(string baseUrl, TimeSpan timeout) : this(baseUrl)
        {
            Timeout = timeout;
        }

        public ApiClient(string baseUrl)
        {
            this.BaseUrl = baseUrl;

            jsonSerializerSettings = new JsonSerializerSettings();
            jsonSerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            jsonSerializerSettings.Converters.Add(new StringEnumConverter());
            jsonSerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
        }

        protected virtual T DeserializeObject<T>(string json) 
        {
            return JsonConvert.DeserializeObject<T>(json, jsonSerializerSettings);
        }

        protected virtual string SerializeObject<T>(T entity)
        {
            var json = JsonConvert.SerializeObject(entity, jsonSerializerSettings);
            return json;
        }

        protected HttpRequestMessage CreateGetRequest(string url)
        {
            HttpRequestMessage get = new HttpRequestMessage(HttpMethod.Get, url);
            return get;
        }

        protected HttpRequestMessage CreatePostRequest(string url)
        {
            HttpRequestMessage post = new HttpRequestMessage(HttpMethod.Post, url);

            return post;
        }

        protected HttpRequestMessage CreateDeleteRequest(string url)
        {
            HttpRequestMessage post = new HttpRequestMessage(HttpMethod.Delete, url);

            return post;
        }

        public async Task HandleError(HttpResponseMessage response)
        {
            var errorResponse = await ReadResponse(response);

            throw new ApiException(errorResponse);
        }

        private async Task<string> ReadResponse(HttpResponseMessage response)
        {
            var contentResponse = await response.Content.ReadAsStringAsync()
                                        .ConfigureAwait(false);
            return contentResponse;
        }

        public async Task<T> ReadResponse<T>(HttpResponseMessage response)
        {
            var responseContent = await ReadResponse(response).ConfigureAwait(false);
            return DeserializeObject<T>(responseContent);
        }

        public async Task<TCollection> ReadListResponse(HttpResponseMessage response)
        {
            var result = await ReadResponse<TCollection>(response).ConfigureAwait(false);
            return result;
        }

        protected virtual HttpClient GetHttpClient()
        {
            return new HttpClient()
            {
                Timeout = Timeout
            };
        }

        protected virtual void SetAuthorization(HttpRequestMessage requestMessage)
        {
        }

        protected async Task<T> GetResource<T>(string resourceUrl,
            IDictionary<string, string> nvc = null)
        {
            T result;

            if (nvc != null)
            {
                resourceUrl = WebUtilities.AddQueryString(resourceUrl, nvc);
            }

            using (HttpRequestMessage get = CreateGetRequest(resourceUrl))
            {
                SetAuthorization(get);

                using (var httpClient = GetHttpClient())
                {
                    var response = await httpClient.SendAsync(get)
                        .ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        result = await ReadResponse<T>(response)
                        .ConfigureAwait(false);

                        return result;
                    }
                    else
                    {
                        await HandleError(response);
                        return default;
                    }
                }
            }
        }

        public async Task<TModel> Get(string itemId)
        {
            string baseUrl = EndPoint + "/" + itemId;
            return await GetResource<TModel>(baseUrl);
        }

        protected async Task<TRes> PostResource<T, TRes>(T entity,
            string resourceUrl, HttpMethods method = HttpMethods.Post)
            where T : class
            where TRes : class
        {
            TRes result = default;

            HttpRequestMessage request;
            switch (method)
            {
                case HttpMethods.Post:
                    request = CreatePostRequest(resourceUrl);
                    break;
                case HttpMethods.Delete:
                    request = CreateDeleteRequest(resourceUrl);
                    break;
                default:
                    request = CreateDeleteRequest(resourceUrl);
                    break;
            }

            using (request)
            {
                SetAuthorization(request);

                if (entity != null)
                {
                    var json = SerializeObject(entity);
                    
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    request.Content = content;
                }

                using (var httpClient = GetHttpClient())
                {
                    var response = await httpClient.SendAsync(request)
                        .ConfigureAwait(false);

                    var httpCode = response.StatusCode;
                    if (httpCode == HttpStatusCode.Accepted || httpCode == HttpStatusCode.NoContent)
                    {
                        result = null;
                    }
                    else if (httpCode == HttpStatusCode.Created || httpCode == HttpStatusCode.OK)
                    {
                        result = await ReadResponse<TRes>(response)
                                        .ConfigureAwait(false);
                    }
                    else
                    {
                        await HandleError(response)
                                .ConfigureAwait(false);
                    }

                }
            }

            return result;
        }

        public async Task<TResult> Create(TModel resource, string action = null)
        {
            string baseUrl = EndPoint;
            if (!string.IsNullOrEmpty(action))
            {
                baseUrl += "/" + action;
            }
            return await PostResource<TModel, TResult>(resource, baseUrl).ConfigureAwait(false);
        }

        public async Task Delete(string id)
        {
            string baseUrl = EndPoint + "/" + id;
            await PostResource<TModel, TResult>(null, baseUrl, HttpMethods.Delete).ConfigureAwait(false);
            return;
        }
    }
}
