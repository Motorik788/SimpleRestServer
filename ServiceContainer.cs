using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

using Newtonsoft.Json;

namespace Rest
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class PathAttribute : Attribute
    {
        public string UriTemplate;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class WebInvokeAttribute : Attribute
    {
        public HttpMethod Method;
        public RequestResponseType ResponseType;
        public RequestResponseType RequestType;
    }


    public enum HttpMethod
    {
        Get,
        Post,
        Put,
        Delete,
        none
    }

    /// <summary>
    /// В архитекуре присутсвует возможность сериализации в xml, но не реализована -> use json only
    /// </summary>
    public enum RequestResponseType
    {
        Json,
        Xml
    }

    /// <summary>
    /// Для использования данного класса, приложение должно стартовать с правами администратора
    /// </summary>
    public class ServiceContainer
    {
        HttpListener listener = new HttpListener();
        volatile bool isRunning = false;

        UriTree tree = new UriTree();

        public ServiceContainer(ushort port = 80)
        {
            Port = port;
            listener.Prefixes.Add($"http://*:{port}/");
        }

        public readonly ushort Port;

        public void RegisterService(Type type)
        {
            var pathAtrService = type.CustomAttributes.FirstOrDefault(x => x.AttributeType == typeof(PathAttribute));
            if (pathAtrService != null)
            {
                string uriTemplate = null;
                try
                {
                    uriTemplate = pathAtrService.NamedArguments.First(x => x.MemberName == "UriTemplate").TypedValue.Value as string;
                }
                catch (Exception)
                {
                    throw new Exception("Не задано значение UriTemplate у атрибута Path");
                }
                var ntree = UriTemplateParser.Parse(uriTemplate);

                var methods = type.GetMethods().Where(x => x.CustomAttributes.FirstOrDefault(y => y.AttributeType == typeof(WebInvokeAttribute)) != null);
                foreach (var item in methods)
                {
                    var webAtr = item.CustomAttributes.First(x => x.AttributeType == typeof(WebInvokeAttribute));
                    var pathAtrMethod = item.CustomAttributes.FirstOrDefault(x => x.AttributeType == typeof(PathAttribute));
                    if (pathAtrMethod != null)
                    {
                        uriTemplate = pathAtrMethod.NamedArguments.First(x => x.MemberName == "UriTemplate").TypedValue.Value as string;
                        var httpMethod = webAtr.NamedArguments.First(x => x.MemberName == "Method");
                        var ntree2 = UriTemplateParser.Parse(uriTemplate, (HttpMethod)httpMethod.TypedValue.Value, item);
                        ntree.UnionChild(ntree2);
                    }
                }
                tree.Union(ntree);
            }
            else
                throw new Exception("Класс должен иметь атрибут Path!");
        }

        public void Start()
        {
            Task.Run(() =>
            {
                try
                {
                    listener.Start();
                }
                catch (Exception)
                {                  
                    return;                   
                }

                isRunning = true;
                while (isRunning)
                {
                    Handler(listener.GetContext());
                }
            }).Wait(1000);
            if (!isRunning)
                throw new Exception("Run application with admin rules");
        }

        public void Stop()
        {
            isRunning = false;
            listener.Stop();
        }

        void SerializeResponse(HttpListenerResponse response, RequestResponseType type, object ob)
        {
            string serOb = null;
            if (type == RequestResponseType.Json)
            {
                response.ContentType = "application/json";
                serOb = JsonConvert.SerializeObject(ob);
            }
            else
            {
                response.ContentType = "application/xml";
            }

            using (var writer = new System.IO.StreamWriter(response.OutputStream))
                writer.Write(serOb);
        }

        bool IsPostPutExistContent(HttpMethod httpMethod, HttpListenerRequest request)
        {
            return (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put) && request.ContentLength64 > 0;
        }

        object DeserializeRequest(HttpListenerRequest request, System.Reflection.MethodInfo methodInfo, Type paramType)
        {
            object res = null;
            var webAtr = methodInfo.CustomAttributes.
                First(x => x.AttributeType == typeof(WebInvokeAttribute)).NamedArguments.FirstOrDefault(y => y.MemberName == "RequestType");
            var contentType = webAtr.MemberInfo != null ? (RequestResponseType)webAtr.TypedValue.Value : RequestResponseType.Json;
            if (contentType == RequestResponseType.Json)
            {
                string json = null;
                using (var reader = new System.IO.StreamReader(request.InputStream))
                    json = reader.ReadToEnd();
                res = JsonConvert.DeserializeObject(json, paramType);
            }
            else
            {

            }
            return res;
        }

        void InvokeWrapper(HttpListenerContext context, Dictionary<string, object> param, UriTreeNode node)
        {
            var p = node.Method.GetParameters();
            object[] par = new object[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                par[i] = param.ContainsKey(p[i].Name) ? Convert.ChangeType(param[p[i].Name], p[i].ParameterType)
                    : (p[i].ParameterType.IsValueType ? Activator.CreateInstance(p[i].ParameterType)
                    : (IsPostPutExistContent(node.HttpMethod, context.Request) ? DeserializeRequest(context.Request, node.Method, p[i].ParameterType) : null));
            }

            try
            {
                var res = node.Method.Invoke(Activator.CreateInstance(node.Method.DeclaringType), par);
                object respObj = null;
                if (node.Method.ReturnType != typeof(void))
                {
                    respObj = new
                    {
                        status = 200,
                        result = res
                    };
                }
                else
                {
                    respObj = new
                    {
                        status = 200
                    };
                }
                context.Response.ContentEncoding = Encoding.Default;
                var typeResp = node.Method.CustomAttributes.
                     First(x => x.AttributeType == typeof(WebInvokeAttribute)).NamedArguments.FirstOrDefault(y => y.MemberName == "ResponseType");

                SerializeResponse(context.Response, typeResp.MemberInfo != null ? (RequestResponseType)typeResp.TypedValue.Value : RequestResponseType.Json, respObj);
                context.Response.StatusCode = 200;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                context.Response.Close();
            }
        }

        private void Handler(HttpListenerContext context)
        {
            if (!context.Request.RawUrl.Contains("favicon.ico"))
            {
                Task.Run(() =>
                {
                    var path = context.Request.RawUrl.Contains("?") ? context.Request.RawUrl.Remove(context.Request.RawUrl.IndexOf('?')).Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                    : context.Request.RawUrl.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    HttpMethod httpMethod = HttpMethod.Get;
                    switch (context.Request.HttpMethod)
                    {
                        case "POST":
                            httpMethod = HttpMethod.Post;
                            break;
                        case "PUT":
                            httpMethod = HttpMethod.Put;
                            break;
                        case "DELETE":
                            httpMethod = HttpMethod.Delete;
                            break;
                        default:
                            break;
                    }
                    Dictionary<string, object> param = new Dictionary<string, object>();
                    foreach (var item in context.Request.QueryString.AllKeys)
                        param.Add(item.Trim(), context.Request.QueryString[item].Trim());
                    var node = tree.Find(path, httpMethod, param);
                    if (node != null && node.Method != null)
                        InvokeWrapper(context, param, node);
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        context.Response.Close();
                    }
                });
            }
        }
    }
}
