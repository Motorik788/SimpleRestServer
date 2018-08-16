# SimpleRestServer
Библиотека для развертывания рест сервисов. Мой аналог Jersey для java.
Sample:

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rest;

class Program
{
    class TestObject
    {
        public int H = 12;
        public string Name = "fooooo";
    }

    [Path(UriTemplate = "service1")]
    class Service1
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name">Path Param</param>
        /// <param name="age">Query string param</param>
        /// <returns></returns>
        [Path(UriTemplate = "b/{name}/hello")]
        [WebInvoke(Method = HttpMethod.Get)]
        public string Hello(string name, int age)
        {
            return "Привет, " + name + ". Возраст = " + age;
        }

        [Path(UriTemplate = "b")]
        [WebInvoke(Method = HttpMethod.Get)]
        public TestObject GetObject()
        {
            return new TestObject();
        }

        [Path(UriTemplate = "b/testPost")]
        [WebInvoke(Method = HttpMethod.Post)]
        public string PostOnject(TestObject obj)
        {
            return "ok";
        }

        [Path(UriTemplate = "b/testPut")]
        [WebInvoke(Method = HttpMethod.Put)]
        public string PutObject(TestObject obj)
        {
            return obj.Name;
        }

        [Path(UriTemplate = "b/testParams/{var1}")]
        [WebInvoke(Method = HttpMethod.Put)]
        public string PutOnject(TestObject obj, int var1, string var2)
        {
            return obj.Name + " " + var1 + " " + var2;
        }
    }

    [Path(UriTemplate = "service2")]
    class Service2
    {
        [Path(UriTemplate = "test/del/{id}")]
        [WebInvoke(Method = HttpMethod.Delete)]
        public void Fooo(int id)
        {
            Console.WriteLine(id);
        }
    }

    public static void Main()
    {
        ServiceContainer container = new ServiceContainer(800);
        container.RegisterService(typeof(Service1));
        container.RegisterService(typeof(Service2));
        container.Start();
    }
}