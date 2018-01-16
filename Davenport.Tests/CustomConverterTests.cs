using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit;

namespace Davenport.Tests
{
    public class CustomConverterTests
    {
        Configuration Config = new Configuration("http://localhost:5984", "davenport_net_custom_converter")
        {
            Converter = new CustomConverter()
        };

        Client<CustomDoc> Client { get; }

        bool Created { get; set; } = false;

        public CustomConverterTests()
        {
            Config.Warning += (object sender, string message) =>
            {
                Console.WriteLine(message);
            };
            Client = new Client<CustomDoc>(Config);
        }

        async Task Create()
        {
            if (!Created)
            {
                await Configuration.CreateDatabaseAsync(Config);
                Created = true;
            }
        }

        [Fact(DisplayName = "Serializes CustomDoc into JSON"), Trait("Category", "CustomConverter")]
        public void SerializesCustomDocIntoJson()
        {
            var data = new CustomDoc(new CustomDocData()
            {
                MyId = "Hello",
                MyRev = "World",
                Foo = true
            });

            var jsonStr = JsonConvert.SerializeObject(data, Config.Converter);
            var expected = "{\"_id\":\"Hello\",\"_rev\":\"World\",\"Foo\":true}";

            Assert.NotNull(jsonStr);
            Assert.NotEmpty(jsonStr);
            Assert.Equal(expected, jsonStr);
        }

        [Fact(DisplayName = "Deserializes JSON into CustomDoc"), Trait("Category", "CustomConverter")]
        public void DeserializesJsonIntoCustomDoc()
        {
            var jsonStr = "{\"_id\":\"Henlo\",\"_rev\":\"Birb\",\"Foo\":true}";
            var doc = JsonConvert.DeserializeObject<CustomDoc>(jsonStr, Config.Converter);

            Assert.Equal("Henlo", doc.Id);
            Assert.Equal("Birb", doc.Rev);
            Assert.Equal("Henlo", doc.Data.MyId);
            Assert.Equal("Birb", doc.Data.MyRev);
            Assert.True(doc.Data.Foo);
        }

        [Fact(DisplayName = "Creates Custom Docs using custom converter"), Trait("Category", "CustomConverter")]
        public async Task CreatesCustomDocs()
        {
            await Create();

            var doc = await Client.PostAsync(new CustomDoc(new CustomDocData()
            {
                Foo = true
            }));

            Assert.False(string.IsNullOrEmpty(doc.Id));
            Assert.False(string.IsNullOrEmpty(doc.Rev));
            Assert.True(doc.Ok);
        }

        [Fact(DisplayName = "Gets Custom Docs using custom converter"), Trait("Category", "CustomConverter")]
        public async Task GetAsync()
        {
            await Create();

            var created = await Client.PostAsync(new CustomDoc(new CustomDocData()
            {
                Foo = true
            }));
            var doc = await Client.GetAsync(created.Id);

            Assert.False(string.IsNullOrEmpty(doc.Id));
            Assert.False(string.IsNullOrEmpty(doc.Rev));
            Assert.Equal(doc.Id, doc.Data.MyId);
            Assert.Equal(doc.Rev, doc.Data.MyRev);
            Assert.True(doc.Data.Foo);
        }
    }
}