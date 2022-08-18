    public interface IInternalApiRequest
    {
        Task<T> Request<T>(Uri route, HttpMethod method, object payload = null, Dictionary<string, string> header = null);
    }

    public class InternalApiRequest : IInternalApiRequest
    {
        private readonly IHttpClientFactory _clientFactory;

        [ActivatorUtilitiesConstructor]
        public InternalApiRequest(IHttpClientFactory clientFactory)
		{
            _clientFactory = clientFactory;
        }

        public InternalApiRequest()
        {

        }

        public async Task<T> Request<T>(Uri route, HttpMethod method, object payload = null, Dictionary<string, string> header = null)
        {
            var httpClient = _clientFactory.CreateClient();

            if (header != null)
            {
                httpClient.DefaultRequestHeaders.Clear();
                foreach (var item in header)
                {
                    httpClient.DefaultRequestHeaders.Add(item.Key, item.Value);
                }
            }

            string content;
            HttpResponseMessage response;
            HttpContent dataSend = null;

            if (payload != null)
            {
                if (payload is FormUrlEncodedContent)
                {
                    dataSend = payload as HttpContent;
                }
                else
                {
                    var json = JsonConvert.SerializeObject(payload);
                    dataSend = new StringContent(json, Encoding.UTF8, "application/json");
                }
            }

            if (method == HttpMethod.Get)
            {
                response = await httpClient.GetAsync(route);
                content = await response.Content.ReadAsStringAsync();
            }
            else if (method == HttpMethod.Post)
            {
                response = await httpClient.PostAsync(route, dataSend);
                content = await response.Content.ReadAsStringAsync();
            }
            else if (method == HttpMethod.Put)
            {
                response = await httpClient.PutAsync(route, dataSend);
                content = await response.Content.ReadAsStringAsync();
            }
            else if (method == new HttpMethod("Patch"))
            {
                response = await httpClient.SendAsync(new HttpRequestMessage(new HttpMethod("PATCH"), route)
                {
                    Content = dataSend
                });
                content = await response.Content.ReadAsStringAsync();
            }
            else if (method == HttpMethod.Delete)
            {
                response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete, route) 
                {
                    Content = dataSend
                });
                content = await response.Content.ReadAsStringAsync();
            }
            else
            {
                throw new BusinessException($"Erro ao enviar para API {route.Host}", HttpStatusCode.InternalServerError);
            }

            httpClient.Dispose();

            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
            {
                var error = new ExceptionResponse();
                try
                {
                    error = JsonConvert.DeserializeObject<ExceptionResponse>(content);
                }
                catch
                {
                }
                throw new BusinessException($"{((error is null || error.StatusCode == 0) ? content : error.Message)}", response.StatusCode);
            }

            if (Int32.TryParse(content, out int number) && typeof(T) == typeof(int))
            {
                return (T)Convert.ChangeType(number, typeof(T));
            }

            return (!string.IsNullOrEmpty(content) && !(content is null) && JContainer.Parse(content).HasValues) ?
                JsonConvert.DeserializeObject<T>(content) :
                default;
        }
    }