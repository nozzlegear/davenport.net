# Davenport.NET

[![Build status](https://ci.appveyor.com/api/projects/status/8cy7l4f582fnuf9g/branch/master?svg=true)](https://ci.appveyor.com/project/nozzlegear/davenport-net/branch/master)
[![NuGet](https://img.shields.io/nuget/v/Davenport.svg?maxAge=3600)](https://www.nuget.org/packages/Davenport/)
[![license](https://img.shields.io/github/license/nozzlegear/davenport.net.svg?maxAge=3600)](https://raw.githubusercontent.com/nozzlegear/davenport.net/master/LICENSE)

Davenport.NET is a .NET implementation of [Davenport](https://github.com/nozzlegear/davenport). Davenport is a CouchDB client for simplifying common tasks like get, list, create, update and delete.

## Installation

Davenport is published on [Nuget](https://nuget.org/packages/davenport). You can install Davenport from the Dotnet command line:

```sh
dotnet add package davenport
```

Or use the Visual Studio package manager console:

```sh
install-package davenport
```

## Client vs ConfigureDatabaseAsync

Davenport can handle the initial setup of your databases, manage your find indexes, design documents and views at startup. You can do this with `Davenport.Configuration.ConfigureDatabaseAsync`. It will also check that your CouchDB instance is at version 2.0, which is required by many Davenport methods.

Once configured, Davenport will return an instance of `Client` that's ready to use:

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

## where DocumentType : CouchDoc

All CouchDB documents are assigned an `_id` and `_rev` parameter on every create or update call. To ensure that Davenport can actually execute requests against your database and documents, all objects sent through the `Client` **must** inherit from the `Davenport.CouchDoc` class. The `CouchDoc` class implements the `Id` and `Rev` strings for you, which are then JSON serialized to the `_id` and `_rev` strings when sent to CouchDB.

## JSON serialization

Like many .NET packages, Davenport uses Newtonsoft.Json to handle de/serialization to and from JSON. That means your document classes can use the entire cadre of Json.Net attributes and serializers on your properties.

For example, you can use the `[JsonProperty("foo")]` attribute on a `Foo` property, which forces the property name to `foo` when sent to your database, and deserializes it back to `Foo` when returned. This is what Davenport uses internally to map `Id` and `Rev` on the `CouchDoc` class to `_id` and `_rev` in CouchDB.

```cs
public class MyClass : Davenport.CouchDoc
{
    [JsonProperty("foo")]
    public string Foo { get; set; }

    // Serializes to { "foo" : "value" }
}
```

**Davenport is configured to ignore null property values when serializing and deserializing**.

## Warnings

Though rare if your database and indexes are configured properly, CouchDB may return a warning with `find` requests, particularly ones that used an index that wasn't configured in your database. Instead of logging to Console.WriteLine, Davenport includes a Warning event on all Configuration objects which you can use to log the message in a way more conducive to your application:

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

There are four different events that will create a warning message:

1. You executed a `FindBy*Async` operation and looked up documents based on indexes (properties) that weren't configured in your database.
    - Hint: You can use `Davenport.Configuration.ConfigureDatabaseAsync` to create indexes. Using indexes improves the performance of your `FindAsync` calls.
2. Davenport is updating or creating a design doc when configuring a database.
3. Your CouchDB installation is using a version less than 2.0.0.
    - If this is the case, you won't be able to use `FindBy*Async` methods, or any other Davenport methods that use `FindBy*Async` internally, e.g. `ExistsBy*Async` and `CountBy*Async`.
4. You're attempting to delete a document with `DeleteAsync`, but you don't pass a document revision id.
    - In some cases, this may cause a document conflict error.

