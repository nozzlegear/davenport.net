# Davenport.NET

[![Build status](https://ci.appveyor.com/api/projects/status/8cy7l4f582fnuf9g/branch/master?svg=true)](https://ci.appveyor.com/project/nozzlegear/davenport-net/branch/master)
[![NuGet](https://img.shields.io/nuget/v/Davenport.svg?maxAge=3600)](https://www.nuget.org/packages/Davenport/)
[![license](https://img.shields.io/github/license/nozzlegear/davenport.net.svg?maxAge=3600)](https://raw.githubusercontent.com/nozzlegear/davenport.net/master/LICENSE)

Davenport is the comfiest .NET wrapper for CouchDB. Its goal is to simplify interacting with the CouchDB API by wrapping things like getting, listing, creating, updating, finding, copying and deleting documents.

## Installation

Davenport is published on [Nuget](https://nuget.org/packages/davenport). You can install Davenport from the dotnet command line:

```sh
dotnet add package davenport
```

Or use the Visual Studio package manager console:

```sh
install-package davenport
```

And if you're using Paket, add this to your `paket.dependencies` file:

```sh
nuget davenport
# You probably want the F# wrapper if you're using paket
nuget davenport.fsharp
```

## C# Documentation

(Want to use Davenport with F#? [See below.](#f-documentation))

To use Davenport you'll need to create an instance of the `Client` class. Each `Client` is tied to a specific database which is more akin to a "table" in SQL terms, as each CouchDB installation can host thousands of databases; in fact, with CouchDB such a case is even encouraged with the database-per-user strategy.

To create a `Client`, you can either construct the class itself, *or* you can use `Davenport.Configuration.ConfigureDatabaseAsync` which will take care of creating the database (if it doesn't exist), creating design docs, and creating find indexes all at once. `ConfigureDatabaseAsync` will also check that your CouchDB instance is at version 2.0, which is required by the `FindBy*Async`, `CountBy*Async` and `ExistsBy*Async` methods.

```cs
// Create a Find index on the "Foo" property of documents in this database.
string[] indexes = { "Foo" };
var designDocs = new DesignDocConfig[]
{
    new DesignDocConfig()
    {
        Name = "list",
        Views = new View[]
        {
            new View()
            {
                // Name must be URL compatible, e.g. no spaces or invalid URL characters.
                Name = "only-bazs-greater-than-10",
                // Map and Reduce functions must be valid JavaScript strings.
                MapFunction = @"function (doc) {
                    if (doc.Baz > 10) {
                        emit(doc._id, doc);
                    }
                }",
                // Use the built-in _count reduce function.
                ReduceFunction = "_count"
            }
        }
    }
};

Client<DocumentType> client;

try
{
    client = await Configuration.ConfigureDatabaseAsync<DocumentType>(Config, indexes, designDocs);
}
catch (DavenportException ex)
{
    // Handle exception
}
```

It should be noted that configuring the design docs and their views is a **"dumb"** process. Davenport will check to see if the design documents you gave it match the ones that exist on your database **exactly**. That is to say, if you have a design doc named `list` on your database, and it has a view called `list-foos`, Davenport will check that the map and reduce functions match the ones you passed it *exactly, to the letter*. If not, Davenport will overwrite the view's map and reduce functions with the ones you gave it.

Davenport **does not** delete design documents that it encounters but weren't listed in your configuration. It will simply skip them.

If you don't need to configure your database or design docs from code, then you don't need to use `ConfigureDatabaseAsync` at all. You can just create a new `Client` which is immediately ready to interact with your database:

```cs
var client = new Davenport.Client<DocumentType>("http://localhost:5984", "my_database_name");
```

### `where DocumentType : CouchDoc`

CouchDB assigns all documents an `_id` and `_rev` property on every create or update call. To ensure that Davenport can actually execute requests against your database and documents, all objects sent through the `Client` **must** inherit from the `Davenport.CouchDoc` class. The `CouchDoc` class implements the `Id` and `Rev` strings for you, which are then mapped to the `_id` and `_rev` strings when sent to CouchDB.

Like many .NET packages, Davenport uses Newtonsoft.Json to handle de/serialization to and from JSON. That means your document classes can use the entire cadre of Json.Net attributes and serializers on your properties.

For example, you can use the `[JsonProperty("foo")]` attribute on a `Foo` property, which forces the property name to `foo` when sent to your database, and deserializes it back to `Foo` when returned. This is what Davenport uses internally to map `Id` and `Rev` on the `CouchDoc` class to `_id` and `_rev` in CouchDB.

```cs
public class MyClass : Davenport.CouchDoc
{
    [JsonProperty("foo")]
    public string Foo { get; set; }

    // Serializes to { "_id" : "some-id", "_rev" : "some-rev", "foo" : "value" }
}
```

**Davenport is configured to ignore null property values when serializing and deserializing**.

### Usage

Note that the code sample below does not cover all methods, just a fraction of them to show what's possible.

```cs
using Davenport;

public class MyDoc : CouchDoc
{
    string Foo { get; set; }
}

//...

// Create a client for working with the "my_database" database
var client = new Client<MyDoc>("localhost:5984", "my_database");
// Or, use an optional username and password to connect
var client = new Client<MyDoc>(new Configuration("localhost:5984", "my_database")
{
    Username = "username",
    Password = "password"
});

// Create the database if it doesn't exist
await client.CreateDatabaseAsync();

// Create a doc
var myDoc = await client.PostAsync(new MyDoc()
{
    Foo = "Hello world!"
});

// Get a doc
MyDoc doc;
try
{
    doc = await client.GetAsync(docId);
    // Or get one by a specific revision
    doc = await client.GetAsync(docId, rev);
}
catch (DavenportException e)
{
    if (e.Status == 404)
    {
        // Doc was not found
    }
    else
    {
        // Some other error
    }
}

// Find docs by the value of their Foo property
var docs = await client.FindByExprAsync(d => d.Foo == "Hello world!");
// Or use a dictionary
var docs = await client.FindBySelectorAsync(new Dictionary<string, FindExpression>()
{
    { "Foo", new FindExpression(ExpressionType.Equal, "Hello world!")}
});
```

### Getting around `where DocumentType : CouchDoc`

If you chafe under the rule that all documents must inherit from `CouchDoc`, you can circumvent this requirement by passing your own custom `JsonConverter` to the `Configuration` object used when creating a `Client`. In fact, this is exactly what the F# wrapper for Davenport does to allow passing any F# record type to Davenport. Here's the general strategy:

**Step one:** Create a class that inherits from CouchDoc, but has a `Data` property of the type you're expecting to send to CouchDB (or make it accept any type by using `object` as the F# wrapper does):

```cs
class CouchDocWrapper : CouchDoc
{
    public AnotherClass Data { get; set; }
}
```

**Step two:** Create a custom JsonConverter that will serialize and deserialize all instances of that wrapper class:

```cs
class WrapperConverter : JsonConverter
{
    public override CanConvert(Type objectType)
    {
        // Only convert CouchDocWrapper instances
        return objectType == typeof(CouchDocWrapper);
    }
}
```

**Step three:** Implement the `ReadJson` override:

```cs
class WrapperConverter : JsonConverter
{
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        // This method reads the json returned by CouchDB and converting it to a CouchDocWrapper

        var j = JObject.Load(reader);
        JToken id = j["_id"]; // Will be null if the field doesn't exist
        JToken rev = j["_rev"]; // Will be null if the field doesn't exist

        if (id != null)
        {
            // Remove the _id field
            j.Remove("_id");
        }

        if (rev != null)
        {
            // Remove the _rev field
            j.Remove("_rev");
        }

        // Let the jsonSerializer parse the remaining data to "AnotherClass"
        var data = j.ToObject<AnotherClass>(serializer);

        // Return a CouchDocWrapper
        return new CouchDocWrapper()
        {
            Id = id.Value<string>(),
            Rev = rev.Value<string>(),
            Data = data
        };
    }
}
```

**Step four:** Implement the `WriteJson` override:

```cs
class WrapperConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object objValue, JsonSerializer serializer)
    {
        if (objValue == null)
        {
            serializer.Serialize(writer, null);

            return;
        }

        // Because of the CanConvert override we know that this object is going to be a CouchDocWrapper
        var doc = (CouchDocWrapper) objValue;

        writer.WriteStartObject();

        var id = doc.Id;
        var rev = doc.Rev;
        var data = JObject.FromObject(doc.Data);

        // Write the _id and _rev values if they aren't null or empty. Writing either one when it isn't intended can make CouchDB throw an error
        if (!String.IsNullOrEmpty(id))
        {
            writer.WritePropertyName("_id");
            writer.WriteValue(id);
        }

        if (!String.IsNullOrEmpty(rev))
        {
            writer.WritePropertyName("_rev");
            writer.WriteValue(rev);
        }

        // Merge the data property with the doc so they're at the same level
        foreach (var prop in data.Cast<JProperty>().Where(p => p.Name != "Id" && p.Name != "Rev"))
        {
            prop.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
```

And finally, to use your new custom converter, pass it to the Configuration object you use to configure the database or construct the client:

```cs
var config = new Configuration("http://localhost:5984", "my_database")
{
    Converter = new WrapperConverter()
};
var client = new Client(config);
```

## F# documentation

I've built Davenport with an F# wrapper for the C# methods, making the package much more functional, idiomatic and easier to use with F#. To install the wrapper, just add the following to your `paket.dependencies` file:

```sh
nuget davenport
nuget davenport.fsharp
```

### Usage

Note that the code sample below does not cover all functions, just a fraction of them to show what's possible.

```fs
open Davenport.Fsharp.Wrapper

type MyDoc = {
    MyId: string
    MyRev: string
    Foo: string
}

let client =
    "localhost:5984"
    |> database "my_database" //All requests will be to "my_database"
    |> idField "MyId" //Map the "MyId" record label to the database's "_id" field
    |> revField "MyRev" //Map the "MyRev" record label to the database's "_rev" field
    |> username "username" //Optionally use a username to login
    |> password "password" //Optionally use a password to login
    |> converter someJsonConverter //Optionally use your own custom converter. NOTE: This must map your id and rev fields for you.

// Create the database if it doesn't exist
do! createDatabase client

// Create a doc
let! myDoc = client |> create ({ MyId = "SomeId"; MyRev = "SomeRev"; Foo = "Hello world!"})

// Get a doc
let! getResult = client |> get docId None
let doc =
    match getResult with
    | Some d -> d
    | None -> //No doc was found

// Get a doc by a specific revision
let! getResult = client |> get docId (Some rev)
let doc =
    match getResult with
    | Some d -> d
    | None -> //No doc was found

// Find docs by the value of their Foo property
let! docs = client |> find <@ fun (d: MyDoc) -> d.Foo = "Hello world!" @> None
// Or use a map
let! docs = client |> find (Map.ofSeq ["Foo", EqualTo "Hello world!"]) None
```

## Warnings

Though rare if your database and indexes are configured properly, CouchDB may return a warning with `find` requests, particularly ones that used an index that wasn't configured in your database. Instead of logging to Console.WriteLine, Davenport includes a Warning event on all Configuration objects which you can use to log the message in a way more conducive to your application.

To use it in C#:

```cs
var config = new Configuration("http://localhost:5984", "database_name");
var client = new Client<DocumentType>(config);

// Wire up the Warning event:
config.Warning += (object sender, string message) =>
{
    // Do whatever you want with the warning message.
    Console.WriteLine(message);
};
```

And to use it in F#:

```fs
let client =
    "localhost:5984"
    |> database "my_database"
    |> warning (Event.add (fun message -> printfn "%s" message))
```

There are four different events that will create a warning message:

1. You executed a `FindBy*Async` operation and looked up documents based on indexes (properties) that weren't configured in your database.
    - Hint: You can use `Davenport.Configuration.ConfigureDatabaseAsync` to create indexes. Using indexes improves the performance of your `FindAsync` calls.
2. Davenport is updating or creating a design doc when configuring a database.
3. Your CouchDB installation is using a version less than 2.0.0.
    - If this is the case, you won't be able to use `FindBy*Async` methods, or any other Davenport methods that use `FindBy*Async` internally, e.g. `ExistsBy*Async` and `CountBy*Async`.
4. You're attempting to delete a document with `DeleteAsync`, but you don't pass a document revision id.
    - In some cases, this may cause a document conflict error.

