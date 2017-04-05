using System;
using System.Linq;
using System.Threading.Tasks;
using Davenport;
using Xunit;

namespace Davenport.Tests
{
    public class ClientTests
    {
        Client<MyTestClass> Client { get; }

        public ClientTests()
        {
            var config = new Configuration()
            {
                CouchUrl = "http://localhost:5984",
                DatabaseName = "davenport_net"
            };
            Client = new Client<MyTestClass>(config);

            config.Warning += (object sender, string message) => Console.WriteLine(message);
        }

        [Fact(DisplayName = "Client PostAsync")]
        public async Task PostAsync()
        {
            var doc = await Client.PostAsync(new MyTestClass()
            {
                Foo = "test value",
                Baz = 11
            });

            Assert.False(string.IsNullOrEmpty(doc.Id));
            Assert.False(string.IsNullOrEmpty(doc.Rev));
            Assert.True(doc.Ok);
        }

        [Fact(DisplayName = "Client GetAsync")]
        public async Task GetAsync()
        {
            var created = await Client.PostAsync(new MyTestClass()
            {
                Foo = "test value",
                Bar = false,
                Baz = 11,
                Bat = 5,
            });
            var doc = await Client.GetAsync(created.Id);

            Assert.Equal(doc.Foo, "test value");
            Assert.False(doc.Bar);
            Assert.Equal(doc.Baz, 11);
            Assert.True(doc.Bat.HasValue);
            Assert.Equal(doc.Bat, 5);
        }
    }
}